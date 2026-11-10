using System;
using System.Collections.Generic;
using System.Linq;
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
        int m_RunSeed;

        /// <summary>Server: the seed of the whole run — every house, loot, robot and patrol follows from it (<see cref="LevelRandom"/>).</summary>
        public int RunSeed => m_RunSeed;

        public LevelInfo Level { get; private set; }
        /// <summary>What the team has seen of this house (every peer; the server reveals, the twin replicates).</summary>
        public ExplorationMap Exploration { get; private set; }
        /// <summary>Teammates' pings and marks on this peer.</summary>
        public PingBoard Pings { get; } = new();
        /// <summary>The route of this run through the sewers (every peer draws it from <see cref="LevelInfo.RunSeed"/>).</summary>
        public RunMap Route { get; private set; }
        /// <summary>The team's votes for the next house (in the sewer).</summary>
        public RouteVotes Votes { get; } = new();
        /// <summary>Server time the team leaves the house (everyone is hiding in the hatch), 0 = not leaving.</summary>
        public double LeaveAt { get; private set; }
        /// <summary>Server time the team goes on to the house it voted for, 0 = still voting.</summary>
        public double GoAt { get; private set; }
        /// <summary>The row of the next column the team voted for (-1 = not decided).</summary>
        public int ChosenLane { get; private set; } = -1;
        /// <summary>Hacked doors and cracked safes of this house (<see cref="DoorKey"/>, <see cref="SafeKey"/>).</summary>
        readonly HashSet<int> m_Opened = new();
        public IReadOnlyCollection<int> Opened => m_Opened;
        public int OpenedVersion { get; private set; }
        SewerLocation m_Sewer;

        public static int DoorKey(int door) => door;
        public static int SafeKey(int safe) => 1_000_000 + safe;
        public bool IsOpened(int key) => m_Opened.Contains(key);

        /// <summary>The ways on from the house just left (rows of the next column).</summary>
        public IReadOnlyList<int> RouteOptions => Route != null ? Route.Next(Level.Index, Level.Lane) : System.Array.Empty<int>();

        /// <summary>Where the team trades now: the hatch in a house, the stash board in the sewer.</summary>
        public Vector3? TradePosition =>
            Phase == RunPhase.Playing && ActiveLevel.Layout != null ? ActiveLevel.Layout.HatchPosition
            : Phase == RunPhase.Sewer && SewerLocation.Active != null && SewerLocation.Active.TradeSpot != null ? SewerLocation.Active.TradeSpot.position
            : null;

        /// <summary>A player standing here can sell and use the team stash.</summary>
        public bool CanTradeAt(Vector3 position) => TradePosition is Vector3 at && Hatch.IsNear(position, at);
        float m_NextReveal;
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
            AutoDoor.IsUnlocked = i => IsOpened(DoorKey(i));
            Items.SafeBox.IsOpened = i => IsOpened(SafeKey(i));
            if (!isServer) return;
            StartLevel(DevFlags.ForcedLevel, 0);
            DevFlags.ForcedLevel = 0;
        }

        public void Unbind()
        {
            ActiveLevel.Clear();
            LeaveSewerLocal();
            m_Link = null;
            if (Instance == this) Instance = null;
        }

        /// <summary>Client: the server's view of the run.</summary>
        public void ApplyReplicated(LevelInfo level, int quota, int collected, int runTotal, RunPhase phase, double alarmUntil,
            double leaveAt, double goAt, int chosenLane)
        {
            m_Run.ApplyReplicated(level.Index, quota, collected, runTotal, phase, alarmUntil);
            LeaveAt = leaveAt;
            GoAt = goAt;
            ChosenLane = chosenLane;
            if (!level.Equals(Level))
            {
                Level = level;
                EnsureRoute(level.RunSeed);
                BuildLocal();
            }
            SyncLocation();
        }

        /// <summary>Client: the server's hacked doors and cracked safes.</summary>
        public void ApplyOpened(IEnumerable<int> keys)
        {
            m_Opened.Clear();
            foreach (var k in keys) m_Opened.Add(k);
            OpenedVersion++;
        }

        void EnsureRoute(int runSeed)
        {
            if (Route == null || Route.RunSeed != runSeed) Route = new RunMap(runSeed);
        }

        // ------------------------------------------------------------------ the sewer between houses

        /// <summary>Every peer: build the sewer when the run is there, take it away when a house starts, flood it when the run ends there.</summary>
        void SyncLocation()
        {
            if (Phase == RunPhase.Sewer && m_Sewer == null) EnterSewerLocal();
            else if (Phase == RunPhase.Playing)
            {
                // The new house and the phase arrive as separate updates: the house may have been built (and taken
                // away by the sewer) before the phase said "playing" — then it is built again now.
                if (m_Sewer != null) LeaveSewerLocal();
                if (ActiveLevel.Layout == null) BuildLocal();
            }
            else if (Phase == RunPhase.Failed && m_Sewer != null) m_Sewer.Flood();
        }

        void EnterSewerLocal()
        {
            ActiveLevel.Clear();
            m_BuiltSeed = 0;
            Exploration = null;
            Pings.Clear();
            if (Art.Sewer == null) { Debug.LogError("[Game] LevelArt has no sewer"); return; }
            m_Sewer = Instantiate(Art.Sewer).GetComponent<SewerLocation>();
            ApplyAtmosphere();
            VisualStyles.Dress(m_Sewer.gameObject);
            Debug.Log("[Game] In the sewer");
        }

        void LeaveSewerLocal()
        {
            if (m_Sewer != null) Destroy(m_Sewer.gameObject);
            m_Sewer = null;
        }

        // ------------------------------------------------------------------ level lifecycle

        void BuildLocal()
        {
            var info = Level;
            if (info.Seed == 0 || info.Seed == m_BuiltSeed) return;
            m_BuiltSeed = info.Seed;
            LeaveSewerLocal();
            // Every peer: the streams of this house restart from its seed (cosmetic rolls match across peers too).
            LevelRandom.Begin(info.Seed);
            UnityEngine.Random.InitState(LevelRandom.Mix(info.Seed, (int)RandomChannel.Engine, 0));
            var level = Tuning.Level;
            var layout = LevelGenerator.Generate(info.Seed, info.Index, level.Building, level.Security, Mathf.Max(1, info.Players));
            ActiveLevel.Load(layout, Art);
            Exploration = new ExplorationMap(layout);
            Pings.Clear();
            ApplyAtmosphere();
            VisualStyles.Reapply();
            Debug.Log($"[Game] Level {info.Index} built: seed {info.Seed}, {layout.Width}x{layout.Height} rooms x {layout.Floors} floors for {layout.Players} players");
        }

        static void ApplyAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Stealth.Level.LightMath.GasFog;
            RenderSettings.fogDensity = 0.045f;
            // Night: no fill light from nowhere, no sky reflections — only the lamps and the moon light anything up.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Stealth.Level.LightMath.NightAmbient;
            RenderSettings.reflectionIntensity = 0f;
        }

        void StartLevel(int index, int lane)
        {
            foreach (var obj in m_Spawned) Session.Despawn(obj);
            m_Spawned.Clear();
            foreach (var item in WorldItem.All.ToArray()) Session.Despawn(item.gameObject);

            ScentTrail.Clear();
            // One seed per run: forced from the command line, or a fresh one when a run (house 0 or a test level) starts.
            if (DevFlags.ForcedSeed != 0) m_RunSeed = DevFlags.ForcedSeed;
            else if (index == DevFlags.ForcedLevel || m_RunSeed == 0) m_RunSeed = new System.Random().Next(1, int.MaxValue);
            DevFlags.ForcedSeed = 0;
            EnsureRoute(m_RunSeed);
            lane = Mathf.Clamp(lane, 0, Route.LanesAt(index) - 1);
            int seed = Route.Node(index, lane).Seed;
            // The house is sized for the team that is here now (friends who join later raise only the quota).
            int players = TeamSize;
            m_Opened.Clear();
            OpenedVersion++;
            LeaveAt = 0;
            GoAt = 0;
            ChosenLane = -1;
            Votes.Clear();
            Level = new LevelInfo { Seed = seed, Index = index, Players = players, Lane = lane, RunSeed = m_RunSeed };
            BuildLocal();
            var layout = ActiveLevel.Layout;

            // The level root prefab carries a configured NavMeshSurface (children, physics colliders, no ceiling layer).
            ActiveLevel.Root.GetComponent<NavMeshSurface>().BuildNavMesh();

            int totalValue = SpawnContent(layout, seed);
            m_Run.BeginLevel(index, totalValue, players);
            LevelStartTime = Time.time;
            foreach (var p in PlayerController.All) p.ResetForLevelServer();
            SyncLocation();
            Debug.Log($"[Game] Level {index} (lane {lane}) started (run seed {m_RunSeed}, house seed {seed}): items worth {totalValue}, quota {Quota}, robots {layout.RobotSpawns.Count}, cameras {layout.CameraSpots.Count}, lasers {layout.LaserSpots.Count}");
        }

        int SpawnContent(LevelLayout layout, int seed)
        {
            var rng = LevelRandom.Stream(RandomChannel.Loot);
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

        /// <summary>Players in the session (at least 1).</summary>
        static int TeamSize => Mathf.Max(1, PlayerController.All.Count);

        void Update()
        {
            Pings.Expire(Time.time);
            RevealMap();
            if (!m_IsServer) return;
            if (m_Run.SetPlayers(TeamSize))
                Banner($"В команде {m_Run.Players} {Plural(m_Run.Players, "игрок", "игрока", "игроков")} — квота этого дома: {Quota}");
            foreach (var p in PlayerController.All)
                if (!p.Downed) ScentTrail.Record(p.ClientId, p.transform.position, Time.time);
            if (Phase == RunPhase.Sewer) { UpdateVote(); return; }
            if (Phase != RunPhase.Playing || PlayerController.All.Count == 0) return;
            UpdateHatch();
            if (Phase != RunPhase.Playing) return;
            if (!InGracePeriod && m_Run.ShouldSendHunter(Time.time - LevelStartTime)) SpawnHunter();
            foreach (var p in PlayerController.All)
                if (!p.KnockedOut) return; // lying with HP left is not out: they get up
            if (m_Run.Wipe()) Banner($"Забег окончен: Вся команда выведена из строя. Итог забега: {RunTotal} очк.");
        }

        /// <summary>
        /// The map clears around the players: the server reveals around everyone, a client also around itself at once
        /// (it would arrive from the server a moment later anyway).
        /// </summary>
        void RevealMap()
        {
            var map = Exploration;
            if (map == null || Time.time < m_NextReveal) return;
            m_NextReveal = Time.time + 0.2f;
            float radius = Tuning.Player.Team.RevealRadius;
            foreach (var p in PlayerController.All)
            {
                if (!m_IsServer && p != PlayerController.Local) continue;
                var eye = p.Camera != null && p.IsOwner ? p.Camera.transform.position : p.transform.position + Vector3.up * p.Height * 0.9f;
                map.RevealAround(eye, radius);
            }
        }

        // ------------------------------------------------------------------ pings and marks

        /// <summary>Server: a player pinged a spot or marked a robot — everyone hears and sees it.</summary>
        public void CalloutServer(ulong owner, PingKind kind, Vector3 position, Detector target)
        {
            var team = Tuning.Player.Team;
            if (!Pings.TryCallout(owner, Time.time, team.Cooldown)) return;
            if (target != null) target.MarkServer(Session.ServerTimeF + team.MarkDuration);
            ulong id = target != null ? target.NetId : 0;
            if (m_Link != null) m_Link.BroadcastPing(owner, kind, position, id);
            else ReceivePing(owner, kind, position, id);
        }

        /// <summary>Every peer: a teammate's ping (delivered by the network twin).</summary>
        public void ReceivePing(ulong owner, PingKind kind, Vector3 position, ulong target)
        {
            var team = Tuning.Player.Team;
            float duration = kind == PingKind.Enemy ? team.MarkDuration : team.PingDuration;
            Pings.Add(new TeamPing { Owner = owner, Kind = kind, Position = position, Target = target, Born = Time.time, Until = Time.time + duration });
            var bank = Audio.Sfx.Bank;
            Audio.Sfx.Ui(kind == PingKind.Enemy ? bank?.Mark : bank?.Ping, 0.8f);
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

        /// <summary>This player votes for a house of the next column (in the sewer).</summary>
        public void RequestVote(int lane)
        {
            if (m_Link != null) m_Link.RequestVote(lane);
            else VoteServer(Session.LocalClientId, lane);
        }

        public void RequestRestart()
        {
            if (m_Link != null) m_Link.RequestRestart();
            else RestartRunServer();
        }

        /// <summary>
        /// Server: the house is left once everyone who can is hiding in the hatch (anyone can climb back out) — after a
        /// short countdown the team is in the sewer.
        /// </summary>
        void UpdateHatch()
        {
            bool hidden = RunState.TeamInHatch(PlayerController.All.Select(p => (p.InHatch, p.KnockedOut)));
            if (!hidden) { LeaveAt = 0; return; }
            if (LeaveAt <= 0) LeaveAt = Session.ServerTime + m_Run.Settings.HatchCountdown;
            else if (Session.ServerTime >= LeaveAt) GoToSewerServer();
        }

        void GoToSewerServer()
        {
            int down = PlayerController.All.Count(p => p.KnockedOut);
            LeaveAt = 0;
            foreach (var obj in m_Spawned) Session.Despawn(obj);
            m_Spawned.Clear();
            foreach (var item in WorldItem.All.ToArray()) Session.Despawn(item.gameObject);
            if (!m_Run.EnterSewer(down)) return;
            Votes.Clear();
            GoAt = 0;
            ChosenLane = -1;
            foreach (var p in PlayerController.All)
            {
                p.LeaveHatchServer();
                p.ResetForLevelServer();
            }
            SyncLocation();
            string left = down > 0 ? $" Оставили лежать: {down} — квота следующего дома выше на {Mathf.RoundToInt(m_Run.Settings.DeathPenalty * down * 100)}%." : "";
            Banner(QuotaMet
                ? $"Канализация. Квота выплачена — голосуйте за следующий дом ({Controls.Label(GameAction.Map)}).{left}"
                : $"Канализация. Квота {Collected} / {Quota}: допродайте добычу у доски, без квоты дальше не пустят.{left}");
        }

        public bool QuotaMet => m_Run.QuotaMet;

        /// <summary>Server: a player votes for a house of the next column.</summary>
        public void VoteServer(ulong clientId, int lane)
        {
            if (Phase != RunPhase.Sewer || GoAt > 0 || !RouteOptions.Contains(lane)) return;
            Votes.Cast(clientId, lane);
        }

        /// <summary>Server: everyone has voted — the winner is shown for a moment, then the team goes (or the run ends here).</summary>
        void UpdateVote()
        {
            if (GoAt > 0)
            {
                if (Session.ServerTime >= GoAt) AdvanceServer();
                return;
            }
            var options = RouteOptions;
            if (options.Count == 0 || !Votes.Everyone(PlayerController.All.Select(p => p.ClientId), options)) return;
            int top = options.Max(o => Votes.Count(o));
            bool tie = options.Count(o => Votes.Count(o) == top) > 1;
            ChosenLane = Votes.Winner(options, new System.Random(LevelRandom.Mix(m_RunSeed, 0x707E, Level.Index)));
            GoAt = Session.ServerTime + m_Run.Settings.VoteDelay;
            if (tie) Banner("Голоса разделились — дом выбран жребием");
        }

        void AdvanceServer()
        {
            GoAt = 0;
            int collected = Collected, quota = Quota;
            switch (m_Run.Advance())
            {
                case RunState.Exit.Failed:
                    ChosenLane = -1;
                    SyncLocation();
                    Banner($"Квота не выплачена ({collected} / {quota}). Коллекторы затопили канализацию. Забег окончен, итог: {RunTotal} очк.");
                    break;
                case RunState.Exit.NextLevel:
                    int next = Level.Index + 1, lane = ChosenLane;
                    StartLevel(next, lane);
                    Banner($"Дом №{next + 1}");
                    break;
            }
        }

        // ------------------------------------------------------------------ locked doors and safes

        /// <summary>Server: a player finished hacking a locked door.</summary>
        public void UnlockDoorServer(PlayerController player, int door)
        {
            if (Phase != RunPhase.Playing || player == null || player.Downed) return;
            var d = AutoDoor.All.FirstOrDefault(a => a.Index == door && a.HasLock);
            if (d == null || Vector3.Distance(d.transform.position, player.transform.position) > AutoDoor.HackReach + 1.5f) return;
            if (m_Opened.Add(DoorKey(door))) OpenedVersion++;
        }

        /// <summary>Server: a player cracked a safe — it opens and gives valuable loot.</summary>
        public void OpenSafeServer(PlayerController player, int safe)
        {
            if (Phase != RunPhase.Playing || player == null || player.Downed || IsOpened(SafeKey(safe))) return;
            var box = Items.SafeBox.All.FirstOrDefault(s => s.Index == safe);
            if (box == null || Vector3.Distance(box.transform.position, player.transform.position) > Items.SafeBox.Reach + 1.5f) return;
            m_Opened.Add(SafeKey(safe));
            OpenedVersion++;
            var s = Tuning.Items.Safes;
            var rng = new System.Random(box.Seed);
            int value = 0;
            for (int i = 0; i < s.LootItems; i++)
            {
                byte color = (byte)Mathf.Clamp(s.LootColors[rng.Next(s.LootColors.Length)], 0, ItemCatalog.Colors.Length - 1);
                byte shape = (byte)rng.Next(ItemCatalog.Shapes.Length);
                value += SpawnItemServer(shape, color, box.LootSpot(i), rng.Next(360)).Value;
            }
            Banner($"{player.Nick} вскрыл сейф: добыча на {value} очк.");
        }

        /// <summary>Server: a player pressed R after a failed run.</summary>
        public void RestartRunServer()
        {
            if (!m_Run.Restart()) return;
            foreach (var p in PlayerController.All) p.GetComponent<PlayerInventory>().ClearServer();
            Stash.Clear();
            StartLevel(0, 0);
            Banner("Новый забег");
        }

        // ------------------------------------------------------------------ banners

        static string Plural(int n, string one, string few, string many)
        {
            int d = n % 10, h = n % 100;
            if (d == 1 && h != 11) return one;
            return d >= 2 && d <= 4 && (h < 12 || h > 14) ? few : many;
        }

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
