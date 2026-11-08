using System;
using System.Collections.Generic;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Stealth.Game
{
    public enum RunPhase : byte { Playing, Failed }

    public struct LevelInfo : INetworkSerializeByMemcpy, IEquatable<LevelInfo>
    {
        public int Seed;
        public int Index;
        public bool Equals(LevelInfo other) => Seed == other.Seed && Index == other.Index;
    }

    /// <summary>
    /// Session state, spawned by the host. Owns the level lifecycle: generation (on every peer from a shared seed),
    /// navmesh + content spawning (server), quota checks and alarms.
    /// </summary>
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }
        /// <summary>Test hook: seed for the first level (0 = random).</summary>
        public static int ForcedSeed;
        /// <summary>Test hook: robots and sensors ignore players (UI screenshots).</summary>
        public static bool Peaceful;

        [Header("Content")]
        public LevelArt Art;
        public NetworkObject ItemPrefab;
        public NetworkObject GuardRobotPrefab;
        public NetworkObject ListenerRobotPrefab;
        public NetworkObject CameraPrefab;
        public NetworkObject LaserPrefab;

        [Header("Balance")]
        [Tooltip("Share of the total item value on a level required by the quota (level 0).")]
        public float QuotaShare = 0.35f;
        public float QuotaShareGrowth = 0.04f;
        [Tooltip("Quota increase per player who was down at the end of the previous level.")]
        public float DeathPenalty = 0.15f;
        public float AlarmDuration = 6f;

        public readonly NetworkVariable<LevelInfo> Level = new();
        public readonly NetworkVariable<int> Quota = new();
        public readonly NetworkVariable<int> Collected = new();
        public readonly NetworkVariable<int> RunTotal = new();
        public readonly NetworkVariable<RunPhase> Phase = new();
        public readonly NetworkVariable<double> AlarmUntil = new();

        public event Action<string> OnBanner;

        public bool AlarmActive => NetworkManager != null && NetworkManager.ServerTime.Time < AlarmUntil.Value;
        public string FailReason { get; private set; } = "";

        readonly List<NetworkObject> m_Spawned = new();
        int m_BuiltSeed;
        int m_PendingPenalties;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            Level.OnValueChanged += (_, _) => BuildLocal();
            if (IsServer) StartLevel(0);
            else if (Level.Value.Seed != 0) BuildLocal();
        }

        public override void OnNetworkDespawn()
        {
            LevelGenerator.Clear();
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ level lifecycle

        void BuildLocal()
        {
            var info = Level.Value;
            if (info.Seed == 0 || info.Seed == m_BuiltSeed) return;
            m_BuiltSeed = info.Seed;
            var layout = LevelGenerator.Generate(info.Seed, info.Index);
            LevelGenerator.Build(layout, Art);
            ApplyAtmosphere();
            Debug.Log($"[Game] Level {info.Index} built: seed {info.Seed}, {layout.Width}x{layout.Height} rooms");
        }

        static void ApplyAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.55f, 0.6f, 0.5f);
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.21f, 0.22f);
        }

        void StartLevel(int index)
        {
            foreach (var obj in m_Spawned)
                if (obj != null && obj.IsSpawned) obj.Despawn();
            m_Spawned.Clear();
            foreach (var item in WorldItem.All.ToArray())
                if (item.IsSpawned) item.NetworkObject.Despawn();

            int seed = ForcedSeed != 0 ? ForcedSeed : UnityEngine.Random.Range(1, int.MaxValue);
            ForcedSeed = 0;
            Level.Value = new LevelInfo { Seed = seed, Index = index };
            BuildLocal();
            var layout = LevelGenerator.Current;

            var surface = LevelGenerator.CurrentRoot.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~(1 << LevelGenerator.CeilingLayer);
            surface.BuildNavMesh();

            int totalValue = SpawnContent(layout, seed);
            float share = QuotaShare + QuotaShareGrowth * index;
            float penalty = 1f + DeathPenalty * m_PendingPenalties;
            Quota.Value = Mathf.Max(10, Mathf.RoundToInt(totalValue * share * penalty / 5f) * 5);
            Collected.Value = 0;
            Phase.Value = RunPhase.Playing;
            AlarmUntil.Value = 0;

            foreach (var p in PlayerController.All) p.ResetForLevelServer();
            Debug.Log($"[Game] Level {index} started: items worth {totalValue}, quota {Quota.Value}, robots {layout.RobotSpawns.Count}, cameras {layout.CameraSpots.Count}, lasers {layout.LaserSpots.Count}");
        }

        int SpawnContent(LevelLayout layout, int seed)
        {
            var rng = new System.Random(seed ^ 0x5bd1e995);
            int index = layout.LevelIndex;

            // Items.
            var spots = new List<Vector3>(layout.ItemSpots);
            int count = Mathf.Min(spots.Count, 6 + index * 2 + layout.Width * layout.Height / 2);
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                int pick = rng.Next(spots.Count);
                var pos = spots[pick];
                spots.RemoveAt(pick);
                var obj = Instantiate(ItemPrefab, pos, Quaternion.Euler(0, rng.Next(4) * 90f + (float)rng.NextDouble() * 20f, 0));
                var item = obj.GetComponent<WorldItem>();
                item.Shape.Value = (byte)rng.Next(ItemCatalog.Shapes.Length);
                item.Color.Value = (byte)ItemCatalog.RandomColor(rng, index * 2);
                obj.Spawn(true);
                total += item.Value;
            }

            // Robots.
            foreach (var spawn in layout.RobotSpawns)
            {
                var prefab = spawn.Type == 0 ? GuardRobotPrefab : ListenerRobotPrefab;
                var pos = NavMesh.SamplePosition(spawn.Position, out var hit, 4f, NavMesh.AllAreas) ? hit.position : spawn.Position;
                var obj = Instantiate(prefab, pos, Quaternion.Euler(0, rng.Next(360), 0));
                obj.Spawn(true);
                m_Spawned.Add(obj);
            }

            foreach (var pose in layout.CameraSpots)
            {
                var obj = Instantiate(CameraPrefab, pose.position, pose.rotation);
                obj.Spawn(true);
                m_Spawned.Add(obj);
            }

            foreach (var spot in layout.LaserSpots)
            {
                var obj = Instantiate(LaserPrefab, spot.Position, spot.Rotation);
                var gate = obj.GetComponent<LaserGate>();
                gate.Kind.Value = (byte)spot.Kind;
                gate.Width.Value = spot.Width;
                obj.Spawn(true);
                m_Spawned.Add(obj);
            }
            return total;
        }

        // ------------------------------------------------------------------ server API

        public void AddCollectedServer(int value)
        {
            Collected.Value += value;
            RunTotal.Value += value;
        }

        public void RaiseAlarmServer(Vector3 position, float radius)
        {
            AlarmUntil.Value = NetworkManager.ServerTime.Time + AlarmDuration;
            foreach (var robot in RobotController.All)
                if ((robot.transform.position - position).magnitude <= radius)
                    robot.InvestigateServer(position, true);
        }

        void Update()
        {
            if (!IsServer || Phase.Value != RunPhase.Playing || PlayerController.All.Count == 0) return;
            foreach (var p in PlayerController.All)
                if (!p.Downed.Value) return;
            Fail("Вся команда выведена из строя");
        }

        void Fail(string reason)
        {
            Phase.Value = RunPhase.Failed;
            BannerRpc($"Забег окончен: {reason}. Итог забега: {RunTotal.Value} очк.");
        }

        [Rpc(SendTo.Server)]
        public void FinishLevelRpc(RpcParams rpcParams = default)
        {
            if (Phase.Value != RunPhase.Playing) return;
            var sender = PlayerController.Find(rpcParams.Receive.SenderClientId);
            if (sender == null || !Hatch.IsNear(sender.transform.position)) return;

            if (Collected.Value < Quota.Value)
            {
                Fail($"квота не выполнена ({Collected.Value} / {Quota.Value})");
                return;
            }
            int down = 0;
            foreach (var p in PlayerController.All) if (p.Downed.Value) down++;
            m_PendingPenalties = down;
            int next = Level.Value.Index + 1;
            StartLevel(next);
            BannerRpc(down > 0
                ? $"Уровень пройден! Погибших: {down}, квота следующего дома выше на {Mathf.RoundToInt(DeathPenalty * down * 100)}%"
                : $"Уровень пройден! Дом №{next + 1}");
        }

        [Rpc(SendTo.Server)]
        public void RestartRunRpc()
        {
            if (Phase.Value != RunPhase.Failed) return;
            m_PendingPenalties = 0;
            RunTotal.Value = 0;
            foreach (var p in PlayerController.All) p.GetComponent<PlayerInventory>().ClearServer();
            StartLevel(0);
            BannerRpc("Новый забег");
        }

        [Rpc(SendTo.Everyone)]
        void BannerRpc(string text)
        {
            Debug.Log("[Game] " + text);
            OnBanner?.Invoke(text);
        }
    }
}
