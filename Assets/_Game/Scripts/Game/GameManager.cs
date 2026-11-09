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
        /// <summary>Test hook: robots stand still (gallery screenshots).</summary>
        public static bool FreezeRobots;
        /// <summary>Test hook: start the run at this level index.</summary>
        public static int ForcedLevel;

        [Header("Content")]
        public LevelArt Art;
        public NetworkObject ItemPrefab;
        [Tooltip("Indexed by RobotKind. Empty entries are skipped.")]
        public NetworkObject[] RobotPrefabs = new NetworkObject[RobotRoster.KindCount];
        public NetworkObject CameraPrefab;
        public NetworkObject LaserPrefab;

        [Header("Balance")]
        [Tooltip("Share of the total item value on a level required by the quota (level 0).")]
        public float QuotaShare = 0.35f;
        public float QuotaShareGrowth = 0.04f;
        [Tooltip("Quota increase per player who was down at the end of the previous level.")]
        public float DeathPenalty = 0.15f;
        public float AlarmDuration = 6f;
        [Tooltip("Sensors stay off for this long after a level starts: a moment to get your bearings.")]
        public float GracePeriod = 8f;

        public static float LevelStartTime { get; private set; }
        public static bool InGracePeriod => Instance != null && Time.time - LevelStartTime < Instance.GracePeriod;

        /// <summary>Does a hunter come now? After enough alarms, or when the team lingers too long.</summary>
        public static bool HunterDue(int alarms, float elapsed, int levelIndex, int huntersSoFar)
        {
            if (huntersSoFar >= 1 + levelIndex / 3) return false;
            int alarmLimit = (levelIndex == 0 ? 3 : 2) * (huntersSoFar + 1);
            float timeLimit = Mathf.Max(180f, 360f - levelIndex * 20f) * (huntersSoFar + 1);
            return alarms >= alarmLimit || elapsed >= timeLimit;
        }

        int m_AlarmCount;
        int m_Hunters;
        double m_LastAlarmCounted = -10;

        /// <summary>Quota for a level: a share of the loot value that grows with depth, plus death penalties.</summary>
        public static int QuotaFor(int totalValue, int levelIndex, int penalties, float share = 0.35f, float growth = 0.04f, float penalty = 0.15f)
        {
            float s = Mathf.Min(0.85f, share + growth * levelIndex);
            return Mathf.Max(10, Mathf.RoundToInt(totalValue * s * (1f + penalty * penalties) / 5f) * 5);
        }

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
            if (IsServer) { StartLevel(ForcedLevel); ForcedLevel = 0; }
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

            ScentTrail.Clear();
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
            Quota.Value = QuotaFor(totalValue, index, m_PendingPenalties, QuotaShare, QuotaShareGrowth, DeathPenalty);
            LevelStartTime = Time.time;
            m_AlarmCount = 0;
            m_Hunters = 0;
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
            int total = 0;
            foreach (var loot in LootPlanner.Plan(layout, rng))
            {
                var obj = Instantiate(ItemPrefab, loot.Position, Quaternion.Euler(0, rng.Next(4) * 90f + (float)rng.NextDouble() * 20f, 0));
                var item = obj.GetComponent<WorldItem>();
                item.Shape.Value = (byte)loot.Shape;
                item.Color.Value = (byte)loot.Color;
                obj.Spawn(true);
                total += item.Value;
            }

            // Robots.
            foreach (var spawn in layout.RobotSpawns)
            {
                var pos = spawn.Position;
                bool onFloor = !RobotRoster.IsDevice(spawn.Kind) || spawn.Kind == RobotKind.Lidar;
                if (onFloor && NavMesh.SamplePosition(spawn.Position, out var hit, 2f, NavMesh.AllAreas)) pos = hit.position;
                DebugSpawnServer(spawn.Kind, pos, spawn.Rotation);
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

        /// <summary>Spawns a robot / device of the given kind (server). Returns null if there is no prefab yet.</summary>
        public NetworkObject DebugSpawnServer(RobotKind kind, Vector3 position, Quaternion rotation)
        {
            int i = (int)kind;
            if (RobotPrefabs == null || i >= RobotPrefabs.Length || RobotPrefabs[i] == null) return null;
            var obj = Instantiate(RobotPrefabs[i], position, rotation);
            obj.Spawn(true);
            m_Spawned.Add(obj);
            return obj;
        }

        public void AddCollectedServer(int value)
        {
            Collected.Value += value;
            RunTotal.Value += value;
        }

        public void RaiseAlarmServer(Vector3 position, float radius)
        {
            // Count distinct alarms (one per few seconds), hunters arrive after enough of them.
            if (NetworkManager.ServerTime.Time - m_LastAlarmCounted > 4.0)
            {
                m_AlarmCount++;
                m_LastAlarmCounted = NetworkManager.ServerTime.Time;
            }
            AlarmUntil.Value = NetworkManager.ServerTime.Time + AlarmDuration;
            foreach (var robot in RobotController.All)
                if ((robot.transform.position - position).magnitude <= radius)
                    robot.InvestigateServer(position, true);
        }

        void Update()
        {
            if (IsServer)
                foreach (var p in PlayerController.All)
                    if (!p.Downed.Value) ScentTrail.Record(p.OwnerClientId, p.transform.position, Time.time);
            if (!IsServer || Phase.Value != RunPhase.Playing || PlayerController.All.Count == 0) return;
            if (!InGracePeriod && HunterDue(m_AlarmCount, Time.time - LevelStartTime, Level.Value.Index, m_Hunters)) SpawnHunter();
            foreach (var p in PlayerController.All)
                if (!p.Downed.Value) return;
            Fail("Вся команда выведена из строя");
        }

        /// <summary>The hunter enters through the furthest room of the ground floor from the players.</summary>
        void SpawnHunter()
        {
            m_Hunters++;
            var layout = LevelGenerator.Current;
            Vector3 best = layout.PatrolPoints[0];
            float bestScore = -1f;
            foreach (var p in layout.PatrolPoints)
            {
                if (LevelLayout.FloorOf(p.y) != 0) continue;
                float score = float.MaxValue;
                foreach (var pl in PlayerController.All) score = Mathf.Min(score, Vector3.Distance(p, pl.transform.position));
                if (score > bestScore) { bestScore = score; best = p; }
            }
            if (NavMesh.SamplePosition(best, out var hit, 3f, NavMesh.AllAreas)) best = hit.position;
            var obj = DebugSpawnServer(RobotKind.Hunter, best, Quaternion.identity);
            if (obj == null) return;
            // It knows roughly where you are: starts by investigating the nearest player.
            var robot = obj.GetComponent<RobotController>();
            PlayerController target = null;
            foreach (var pl in PlayerController.All) if (target == null || Vector3.Distance(pl.transform.position, best) < Vector3.Distance(target.transform.position, best)) target = pl;
            if (target != null) robot.InvestigateServer(target.transform.position, true);
            BannerRpc("В здание вошёл Охотник");
            Debug.Log($"[Game] Hunter spawned at {best} (alarms {m_AlarmCount})");
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
