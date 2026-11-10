using System;
using System.Collections.Generic;
using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Stealth.Game
{
    /// <summary>
    /// Session state, spawned by the host on the Session prefab. Owns the level lifecycle: generation (on every peer
    /// from a shared seed), navmesh + content spawning (server), quota, alarms and hunters. The rules are in
    /// <see cref="RunState"/>; the network twin (GameManagerNet) replicates <see cref="Level"/> and the run state and
    /// carries requests and banners (<see cref="IGameLink"/>).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Content")]
        public LevelArt Art;
        public GameObject ItemPrefab;
        [Tooltip("Indexed by RobotKind (robots, devices and the wall camera). Empty entries are skipped.")]
        public GameObject[] RobotPrefabs = new GameObject[RobotRoster.KindCount];
        public GameObject LaserPrefab;

        public static float LevelStartTime { get; private set; }
        /// <summary>Sensors stay off for a moment after a house starts (RunConfig.GracePeriod).</summary>
        public static bool InGracePeriod => Instance != null && !DevFlags.SkipGracePeriod && Time.time - LevelStartTime < Instance.m_Run.Settings.GracePeriod;

        /// <summary>Robots and devices may sense players now (not in the grace period, not disabled by a test flag).</summary>
        public static bool SensorsAllowed => !DevFlags.Peaceful && !DevFlags.FreezeRobots && !InGracePeriod;

        /// <summary>The run is going on (or there is no session yet, e.g. in tools): robots may act.</summary>
        public static bool IsPlaying => Instance == null || Instance.Phase == RunPhase.Playing;

        RunState m_Run = new();
        readonly List<GameObject> m_Spawned = new();
        IGameLink m_Link;
        bool m_IsServer;
        int m_BuiltSeed;

        public LevelInfo Level { get; private set; }
        /// <summary>The team's shared stash at the hatch: kept for the whole run, emptied by a new run.</summary>
        public TeamStash Stash { get; } = new();
        public RunState Run => m_Run;
        public int Quota => m_Run.Quota;
        public int Collected => m_Run.Collected;
        public int RunTotal => m_Run.RunTotal;
        public RunPhase Phase => m_Run.Phase;
        public double AlarmUntil => m_Run.AlarmUntil;
        public bool AlarmActive => m_Run.AlarmActive(Session.ServerTime);

        public event Action<string> OnBanner;
        /// <summary>Any banner on this peer (the sound director listens).</summary>
        public static event Action<string> BannerShown;

        // ------------------------------------------------------------------ binding (called by the network twin)

        public void Bind(IGameLink link, bool isServer)
        {
            Instance = this;
            m_Link = link;
            m_IsServer = isServer;
            m_Run = new RunState(Tuning.Run.Rules);
            if (!isServer) return;
            StartLevel(DevFlags.ForcedLevel);
            DevFlags.ForcedLevel = 0;
        }

        public void Unbind()
        {
            ActiveLevel.Clear();
            m_Link = null;
            if (Instance == this) Instance = null;
        }

        /// <summary>Client: the server's view of the run.</summary>
        public void ApplyReplicated(LevelInfo level, int quota, int collected, int runTotal, RunPhase phase, double alarmUntil)
        {
            m_Run.ApplyReplicated(level.Index, quota, collected, runTotal, phase, alarmUntil);
            if (!level.Equals(Level))
            {
                Level = level;
                BuildLocal();
            }
        }

        // ------------------------------------------------------------------ level lifecycle

        void BuildLocal()
        {
            var info = Level;
            if (info.Seed == 0 || info.Seed == m_BuiltSeed) return;
            m_BuiltSeed = info.Seed;
            var level = Tuning.Level;
            var layout = LevelGenerator.Generate(info.Seed, info.Index, level.Building, level.Security);
            ActiveLevel.Load(layout, Art);
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
            foreach (var obj in m_Spawned) Session.Despawn(obj);
            m_Spawned.Clear();
            foreach (var item in WorldItem.All.ToArray()) Session.Despawn(item.gameObject);

            ScentTrail.Clear();
            int seed = DevFlags.ForcedSeed != 0 ? DevFlags.ForcedSeed : UnityEngine.Random.Range(1, int.MaxValue);
            DevFlags.ForcedSeed = 0;
            Level = new LevelInfo { Seed = seed, Index = index };
            BuildLocal();
            var layout = ActiveLevel.Layout;

            // The level root prefab carries a configured NavMeshSurface (children, physics colliders, no ceiling layer).
            ActiveLevel.Root.GetComponent<NavMeshSurface>().BuildNavMesh();

            int totalValue = SpawnContent(layout, seed);
            m_Run.BeginLevel(index, totalValue);
            LevelStartTime = Time.time;
            foreach (var p in PlayerController.All) p.ResetForLevelServer();
            Debug.Log($"[Game] Level {index} started: items worth {totalValue}, quota {Quota}, robots {layout.RobotSpawns.Count}, cameras {layout.CameraSpots.Count}, lasers {layout.LaserSpots.Count}");
        }

        int SpawnContent(LevelLayout layout, int seed)
        {
            var rng = new System.Random(seed ^ 0x5bd1e995);
            int total = 0;
            foreach (var loot in LootPlanner.Plan(layout, rng, Tuning.Level.Loot))
            {
                var item = SpawnItemServer((byte)loot.Shape, (byte)loot.Color, loot.Position, rng.Next(4) * 90f + (float)rng.NextDouble() * 20f);
                total += item.Value;
            }

            foreach (var spawn in layout.RobotSpawns)
            {
                var pos = spawn.Position;
                bool onFloor = !RobotRoster.IsDevice(spawn.Kind) || spawn.Kind == RobotKind.Lidar;
                if (onFloor && NavMesh.SamplePosition(spawn.Position, out var hit, 2f, NavMesh.AllAreas)) pos = hit.position;
                SpawnRobotServer(spawn.Kind, pos, spawn.Rotation);
            }

            foreach (var pose in layout.CameraSpots) SpawnRobotServer(RobotKind.Camera, pose.position, pose.rotation);

            foreach (var spot in layout.LaserSpots)
                m_Spawned.Add(Session.Spawn(LaserPrefab, spot.Position, spot.Rotation, go => go.GetComponent<LaserGate>().Configure(spot.Kind, spot.Width)));
            return total;
        }

        // ------------------------------------------------------------------ server API

        /// <summary>Spawns a robot / device of the given kind (server). Returns null if there is no prefab.</summary>
        public GameObject SpawnRobotServer(RobotKind kind, Vector3 position, Quaternion rotation)
        {
            int i = (int)kind;
            if (RobotPrefabs == null || i >= RobotPrefabs.Length || RobotPrefabs[i] == null) return null;
            var obj = Session.Spawn(RobotPrefabs[i], position, rotation);
            m_Spawned.Add(obj);
            return obj;
        }

        /// <summary>Spawns a loot item (server): level loot and items dropped from inventories.</summary>
        public WorldItem SpawnItemServer(byte shape, byte color, Vector3 position, float yaw) =>
            Session.Spawn(ItemPrefab, position, Quaternion.Euler(0, yaw, 0), go => go.GetComponent<WorldItem>().SetLook(shape, color))
                .GetComponent<WorldItem>();

        public void AddCollectedServer(int value) => m_Run.Deposit(value);

        public void RaiseAlarmServer(Vector3 position, float radius)
        {
            m_Run.RaiseAlarm(Session.ServerTime);
            foreach (var robot in RobotController.Robots)
                if ((robot.transform.position - position).magnitude <= radius)
                    robot.InvestigateServer(position, true);
        }

        void Update()
        {
            if (!m_IsServer) return;
            foreach (var p in PlayerController.All)
                if (!p.Downed) ScentTrail.Record(p.ClientId, p.transform.position, Time.time);
            if (Phase != RunPhase.Playing || PlayerController.All.Count == 0) return;
            if (!InGracePeriod && m_Run.ShouldSendHunter(Time.time - LevelStartTime)) SpawnHunter();
            foreach (var p in PlayerController.All)
                if (!p.Downed) return;
            if (m_Run.Wipe()) Banner($"Забег окончен: Вся команда выведена из строя. Итог забега: {RunTotal} очк.");
        }

        /// <summary>The hunter enters through the furthest room of the ground floor from the players.</summary>
        void SpawnHunter()
        {
            m_Run.HunterSent();
            var layout = ActiveLevel.Layout;
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
            var obj = SpawnRobotServer(RobotKind.Hunter, best, Quaternion.identity);
            if (obj == null) return;
            // It knows roughly where you are: starts by investigating the nearest player.
            PlayerController target = null;
            foreach (var pl in PlayerController.All)
                if (target == null || Vector3.Distance(pl.transform.position, best) < Vector3.Distance(target.transform.position, best)) target = pl;
            if (target != null) obj.GetComponent<RobotController>().InvestigateServer(target.transform.position, true);
            Banner("В здание вошёл Охотник");
            Debug.Log($"[Game] Hunter spawned at {best} (alarms {m_Run.Alarms})");
        }

        // ------------------------------------------------------------------ requests

        public void RequestFinishLevel()
        {
            if (m_Link != null) m_Link.RequestFinishLevel();
            else FinishLevelServer(Session.LocalClientId);
        }

        public void RequestRestart()
        {
            if (m_Link != null) m_Link.RequestRestart();
            else RestartRunServer();
        }

        /// <summary>Server: a player at the hatch pressed F.</summary>
        public void FinishLevelServer(ulong senderClientId)
        {
            var sender = PlayerController.Find(senderClientId);
            if (sender == null || !Hatch.IsNear(sender.transform.position)) return;
            int down = 0;
            foreach (var p in PlayerController.All) if (p.Downed) down++;
            int collected = Collected, quota = Quota;
            switch (m_Run.Leave(down))
            {
                case RunState.Exit.Failed:
                    Banner($"Забег окончен: квота не выполнена ({collected} / {quota}). Итог забега: {RunTotal} очк.");
                    break;
                case RunState.Exit.NextLevel:
                    int next = Level.Index + 1;
                    StartLevel(next);
                    Banner(down > 0
                        ? $"Уровень пройден! Погибших: {down}, квота следующего дома выше на {Mathf.RoundToInt(m_Run.Settings.DeathPenalty * down * 100)}%"
                        : $"Уровень пройден! Дом №{next + 1}");
                    break;
            }
        }

        /// <summary>Server: a player pressed R after a failed run.</summary>
        public void RestartRunServer()
        {
            if (!m_Run.Restart()) return;
            foreach (var p in PlayerController.All) p.GetComponent<PlayerInventory>().ClearServer();
            Stash.Clear();
            StartLevel(0);
            Banner("Новый забег");
        }

        // ------------------------------------------------------------------ banners

        void Banner(string text)
        {
            if (m_Link != null) m_Link.BroadcastBanner(text);
            else ShowBanner(text);
        }

        /// <summary>Every peer: show a banner (delivered by the network twin).</summary>
        public void ShowBanner(string text)
        {
            Debug.Log("[Game] " + text);
            OnBanner?.Invoke(text);
            BannerShown?.Invoke(text);
        }
    }
}
