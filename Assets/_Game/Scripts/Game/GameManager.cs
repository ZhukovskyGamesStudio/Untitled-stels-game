using System;
using System.Collections;
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
    public partial class GameManager : MonoBehaviour, IWorldState
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
        /// <summary>The new enemy mode: this house's time is up and its guards are off their docks for good.</summary>
        bool m_GuardsAwake;
        IGameLink m_Link;
        IGameLink m_Offline;
        /// <summary>Requests and broadcasts: through the network twin, or (no twin) handled right here.</summary>
        IGameLink Link => m_Link ?? (m_Offline ??= new OfflineGameLink(this));
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
        /// <summary>Hacked doors and cracked safes of this house (<see cref="OpenKeys"/>).</summary>
        readonly HashSet<int> m_Opened = new();
        public IReadOnlyCollection<int> Opened => m_Opened;
        public int OpenedVersion { get; private set; }
        SewerLocation m_Sewer;


        /// <summary>Server: loot hidden behind a drawer / locker door (<see cref="OpenKeys.Storage"/> → shape, colour) until it is opened.</summary>
        readonly Dictionary<int, (byte shape, byte color)> m_Hidden = new();

        /// <summary>Hacks already made from this terminal in this house (kept with the opened doors and safes, so replicated).</summary>
        public int TerminalUses(int terminal)
        {
            int n = 0;
            while (n < OpenKeys.TerminalUses && m_Opened.Contains(OpenKeys.Terminal(terminal, n))) n++;
            return n;
        }
        public bool IsOpened(int key) => m_Opened.Contains(key);
        public int TerminalCharges(int terminal) => Mathf.Max(0, Tuning.Player.Interaction.TerminalHacks - TerminalUses(terminal));
        double IWorldState.Now => Session.ServerTime;

        /// <summary>The ways on from the house just left (rows of the next column).</summary>
        public IReadOnlyList<int> RouteOptions => Route != null ? Route.Next(Level.Index, Level.Lane) : System.Array.Empty<int>();

        /// <summary>Where the team trades now: the hatch in a house, the stash board in the sewer.</summary>
        /// <summary>Where the stash board hangs for the local player (the hatch of the house they are at), null outside a house.</summary>
        public Vector3? TradePosition => PlayerController.Local != null ? TradePositionFor(PlayerController.Local.transform.position) : TradePositionFor(Vector3.zero);

        /// <summary>A player standing here can use the stash of a hatch (the entry or the exit of the house).</summary>
        public bool CanTradeAt(Vector3 position) => StashAt(position) != null;
        float m_NextReveal;
        /// <summary>The stash in the house's entry hatch: what came up from the car (it stays in the house unless carried to the exit).</summary>
        public TeamStash Stash { get; } = new();
        public RunState Run => m_Run;
        public int Quota => m_Run.Quota;
        public int Collected => m_Run.Collected;
        public int RunTotal => m_Run.RunTotal;
        public RunPhase Phase => m_Run.Phase;
        public double AlarmUntil => m_Run.AlarmUntil;
        /// <summary>Server time the next hunter comes anyway (0 = no more hunters in this house).</summary>
        public double HunterAt { get; private set; }
        /// <summary>Alarms that would bring the next hunter sooner (-1 = no more hunters in this house).</summary>
        public int HunterAlarmsLeft { get; private set; } = -1;
        bool m_HunterWarned;
        public bool AlarmActive => m_Run.AlarmActive(Session.ServerTime);

        /// <summary>The visual style the team looks at (index in <see cref="VisualStyles.All"/>, -1 = each its own default).</summary>
        public int StyleIndex { get; private set; } = -1;
        int m_AppliedStyle = -1;
        /// <summary>Every peer: the team's style changed — "Style: …" (the HUD shows it, replacing the previous one).</summary>
        public static event Action<string> StyleAnnounced;

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
            World.Set(this);
            if (!isServer) return;
            // A run starts in the sewer, at the first fork (tests and -level start right in a house).
            if (DevFlags.StartInHouse || DevFlags.ForcedLevel > 0)
            {
                m_RunSeed = 0; // a run that starts right in a house: a fresh seed
                StartLevel(DevFlags.ForcedLevel, 0);
            }
            else StartRunServer();
            DevFlags.ForcedLevel = 0;
        }

        public void Unbind()
        {
            ActiveLevel.Clear();
            LeaveSewerLocal();
            m_Link = null;
            World.Clear(this);
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

        /// <summary>Client: the server's forecast of the next hunter.</summary>
        public void ApplyHunter(double at, int alarmsLeft)
        {
            HunterAt = at;
            HunterAlarmsLeft = alarmsLeft;
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
            // on the drive the sewer stays as it is behind the drive scene (the next house is on its way)
            if (Phase == RunPhase.Sewer && DriveUntil > 0 && m_Sewer != null) return;
            // a new run after the water rose (or another run's sewer): built afresh, dry
            if (Phase == RunPhase.Sewer && m_Sewer != null && (m_Sewer.Flooded || m_SewerFor != (Level.RunSeed, Level.Index))) LeaveSewerLocal();
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
            m_Sewer.Build(SewerPlan.Make(Level.RunSeed, Level.Index));
            m_SewerFor = (Level.RunSeed, Level.Index);
            ApplyAtmosphere();
            VisualStyles.Dress(m_Sewer.gameObject);
            Debug.Log("[Game] In the sewer");
        }

        void LeaveSewerLocal()
        {
            if (m_Sewer != null) Destroy(m_Sewer.gameObject);
            m_Sewer = null;
        }

        GameObject m_DriveScene;
        (int seed, int depth) m_SewerFor;

        /// <summary>The drive starts from where this player's eyes are in the car (the scene glides out to its side view).</summary>
        static void BeginDriveScene(DriveScene scene)
        {
            if (scene == null) return;
            var me = PlayerController.Local;
            var car = Vehicles.Car.Active;
            if (me == null || me.Camera == null || car == null) { scene.Begin(null, 60f, 0.05f, 0f); return; }
            var eye = me.Camera.transform;
            var local = car.transform.InverseTransformPoint(eye.position);
            // at the car (in a seat or beside it): from the eyes; far from it: straight into the side view
            Pose? inCar = local.magnitude < 8f ? new Pose(local, Quaternion.Inverse(car.transform.rotation) * eye.rotation) : null;
            // the team rides along: everyone in a seat, as they look now (the driver at the wheel)
            var riders = new List<DriveScene.Rider>();
            var back = Quaternion.Inverse(car.transform.rotation);
            for (int seat = 0; seat < Vehicles.Car.SeatCount; seat++)
            {
                var p = PlayerController.Find(car.OccupantOf(seat));
                if (p == null || p.Visual == null) continue;
                riders.Add(new DriveScene.Rider
                {
                    Visual = p.Visual,
                    InCar = new Pose(car.transform.InverseTransformPoint(p.Visual.position), back * p.Visual.rotation),
                    Mine = p == me,
                });
            }
            scene.Begin(inCar, me.Camera.fieldOfView, me.Camera.nearClipPlane, car.Speed, riders);
        }

        // ------------------------------------------------------------------ level lifecycle

        /// <summary>
        /// Every peer: builds the house of <paramref name="info"/> (its layout from the background generation when it is
        /// the house the drive went to, else generated now), a piece per step.
        /// </summary>
        IEnumerator BuildLocalSteps(LevelInfo info)
        {
            if (info.Seed == 0 || info.Seed == m_BuiltSeed) yield break;
            m_BuiltSeed = info.Seed;
            LeaveSewerLocal();
            // Every peer: the streams of this house restart from its seed (cosmetic rolls match across peers too).
            LevelRandom.Begin(info.Seed);
            UnityEngine.Random.InitState(LevelRandom.Mix(info.Seed, (int)RandomChannel.Engine, 0));
            var level = Tuning.Level;
            var layout = TakePrepared(info) ?? LevelGenerator.Generate(info.Seed, info.Index, level.Building, level.Security, Mathf.Max(1, info.Players), level.Features,
                LevelGenerator.Classic, info.ClassicEnemies);
            foreach (var _ in ActiveLevel.LoadSteps(layout, Art)) yield return null;
            Exploration = new ExplorationMap(layout);
            Pings.Clear();
            ApplyAtmosphere();
            VisualStyles.Reapply();
            Debug.Log($"[Game] Level {info.Index} built: seed {info.Seed}, {HouseKinds.Name(layout.Kind)} ({layout.Mass}) {layout.SizeX:0}x{layout.SizeZ:0} m, {layout.Rooms.Count} rooms, {layout.Floors} floors for {layout.Players} players");
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

        void StartLevel(int index, int lane, int houseSeed = 0)
        {
            var all = StartLevelSteps(index, lane, houseSeed, false);
            while (all.MoveNext()) { }
        }

        /// <summary>
        /// Server: a house starts — built (a piece per step when <paramref name="arriving"/>: the drive scene is on), its
        /// content spawned, the team's car in its basement. Arriving, the team rides in in the seats it left in
        /// (<see cref="ArriveCarServer"/>); else everyone stands up in the basement and the car stands at the gate. The house's
        /// info and the phase go out together in the last step (a client builds it when it hears of it).
        /// </summary>
        IEnumerator StartLevelSteps(int index, int lane, int houseSeed, bool arriving)
        {
            ScentTrail.Clear();
            m_RunSeed = RunMap.SeedFor(m_RunSeed, DevFlags.ForcedSeed, () => new System.Random().Next(1, int.MaxValue));
            DevFlags.ForcedSeed = 0;
            EnsureRoute(m_RunSeed);
            lane = Mathf.Clamp(lane, 0, Route.LanesAt(index) - 1);
            int seed = houseSeed != 0 ? houseSeed : Route.Node(index, lane).Seed;
            // The house is sized for the team that is here now (friends who join later raise only the quota).
            int players = TeamSize;
            var info = new LevelInfo { Seed = seed, Index = index, Players = players, Lane = lane, RunSeed = m_RunSeed, ClassicEnemies = LevelGenerator.ClassicEnemies };
            if (!arriving) m_Prepared = null; // (a house started some other way: nothing generated for it)
            var build = BuildLocalSteps(info);
            while (build.MoveNext()) if (arriving) yield return null;
            var layout = ActiveLevel.Layout;

            // The level root prefab carries a configured NavMeshSurface (children, physics colliders, no ceiling layer).
            ActiveLevel.Root.GetComponent<NavMeshSurface>().BuildNavMesh();
            if (arriving) yield return null;

            foreach (var obj in m_Spawned) Session.Despawn(obj);
            m_Spawned.Clear();
            foreach (var item in WorldItem.All.ToArray()) Session.Despawn(item.gameObject);
            m_Opened.Clear();
            m_Hacked.Clear();
            OpenedVersion++;
            Lift = LiftState.ParkedAt(0); // a new house: the lift stands open on the ground floor
            LeaveAt = 0;
            GoAt = 0;
            ChosenLane = -1;
            Votes.Clear();
            Level = info;
            int totalValue = SpawnContent(layout, seed);
            m_Run.BeginLevel(index, totalValue, players);
            LevelStartTime = Time.time;
            m_GuardsAwake = false;
            UnloadCarServer();
            SpawnStorageBotServer(layout);
            foreach (var p in PlayerController.All) p.ResetForLevelServer(); // (the team arrives down in the basement)
            if (arriving) ArriveCarServer(layout);
            else SpawnBasementCarServer(layout, false);
            SyncLocation();
            Debug.Log($"[Game] Level {index} (lane {lane}) started (run seed {m_RunSeed}, house seed {seed}): items worth {totalValue}, quota {Quota}, robots {layout.RobotSpawns.Count}, cameras {layout.CameraSpots.Count}, lasers {layout.LaserSpots.Count}");
        }

        int SpawnContent(LevelLayout layout, int seed)
        {
            var rng = LevelRandom.Stream(RandomChannel.Loot);
            var hideRng = LevelRandom.Stream(RandomChannel.Storage);
            var free = new List<int>();
            var parts = new Dictionary<int, (Items.Storage storage, int part)>();
            foreach (var s in Items.Storage.All)
                if (s.Index >= 0)
                    for (int part = 0; part < s.PartCount; part++)
                    {
                        free.Add(OpenKeys.Storage(s.Index, part));
                        parts[OpenKeys.Storage(s.Index, part)] = (s, part);
                    }
            free.Sort(); // the same order on every run of the same house
            m_Hidden.Clear();
            HideShelterMap(layout, free, parts, hideRng);
            int total = 0;
            float inStorage = Tuning.Level.Loot.InStorageChance;
            foreach (var loot in LootPlanner.Plan(layout, rng, Tuning.Level.Loot))
            {
                float yaw = rng.Next(4) * 90f + (float)rng.NextDouble() * 20f;
                // a part it fits in: a shovel only stands in a tall compartment, small things go in drawers and cupboards too
                int key = Items.StorageMath.Hide(free, hideRng, inStorage, k => parts[k].storage.Fits(parts[k].part, loot.Shape));
                if (key >= 0)
                {
                    // hidden in a drawer or a locker: it appears when that part is first opened (it still counts for the quota)
                    m_Hidden[key] = ((byte)loot.Shape, (byte)loot.Color);
                    total += ItemCatalog.Value(loot.Shape, loot.Color);
                    continue;
                }
                total += SpawnItemServer((byte)loot.Shape, (byte)loot.Color, loot.Position, yaw).Value;
            }
            total += SpawnFeatures(layout);
            SpawnObjective(layout); // not in the total: the main job is optional, the quota does not count on it

            var wardens = new List<GameObject>();
            for (int si = 0; si < layout.RobotSpawns.Count; si++)
            {
                var spawn = layout.RobotSpawns[si];
                var pos = spawn.Position;
                bool onFloor = !RobotRoster.IsDevice(spawn.Kind) || spawn.Kind == RobotKind.Lidar;
                if (onFloor && NavMesh.SamplePosition(spawn.Position, out var hit, 2f, NavMesh.AllAreas)) pos = hit.position;
                var robot = SpawnRobotServer(spawn.Kind, pos, spawn.Rotation);
                if (spawn.Kind == RobotKind.Warden) wardens.Add(robot);
                if (spawn.Docked && robot != null && robot.TryGetComponent<RobotController>(out var docked)) docked.DockServer(pos, spawn.Rotation * Vector3.forward);
                GiveCageKey(layout, si, robot);
            }
            PostWardens(layout, wardens);

            foreach (var pose in layout.CameraSpots) SpawnRobotServer(RobotKind.Camera, pose.position, pose.rotation);

            foreach (var spot in layout.LaserSpots)
                m_Spawned.Add(Session.Spawn(LaserPrefab, spot.Position, spot.Rotation, go => go.GetComponent<LaserGate>().Configure(spot.Kind, spot.Width)));
            return total;
        }

        /// <summary>
        /// Server, an apartment block: the shelter map into a cupboard of its flat (a wardrobe, a chest of drawers, the hall's
        /// shoe cabinet…; the planned room's first) — it appears when that part is opened — or onto the room's floor.
        /// </summary>
        void HideShelterMap(LevelLayout layout, List<int> free, Dictionary<int, (Items.Storage storage, int part)> parts, System.Random rng)
        {
            if (layout.MapFlat < 0) return;
            int best = -1, bestRank = -1;
            foreach (int key in free)
            {
                var (storage, part) = parts[key];
                if (storage.Index < 0 || storage.Index >= layout.Props.Count || !storage.Fits(part, ItemCatalog.ShelterMap)) continue;
                var at = layout.Props[storage.Index].Position;
                if (HouseFeatures.FlatAt(layout, at) != layout.MapFlat) continue;
                var room = layout.Plan.RoomAt(at + Vector3.up * 0.3f);
                int rank = (room != null && room.Id == layout.MapRoom ? 100 : 0) + rng.Next(50);
                if (rank > bestRank) { bestRank = rank; best = key; }
            }
            if (best >= 0)
            {
                m_Hidden[best] = (ItemCatalog.ShelterMap, 0);
                free.Remove(best);
                return;
            }
            SpawnItemServer(ItemCatalog.ShelterMap, 0, layout.MapSpot + Vector3.up * 0.1f, rng.Next(360));
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

        /// <summary>Server, cheat: every robot and device switched off for good (hacked for a day).</summary>
        public void KillAllRobotsServer()
        {
            foreach (var d in Detector.All.ToArray()) d.HackServer(86400f);
            Banner(Loc.Pack("banner.cheat_robots_off"));
        }

        /// <summary>Server, cheat: the quota of this house is met.</summary>
        public void FillQuotaServer()
        {
            if (Collected < Quota) AddCollectedServer(Quota - Collected);
        }

        public void RaiseAlarmServer(Vector3 position, float radius)
        {
            m_Run.RaiseAlarm(Session.ServerTime);
            m_Bot?.HideServer(ActiveLevel.Layout); // the team's storage robot runs home to the basement
            foreach (var robot in RobotController.Robots)
                if (robot.Docked) robot.AlarmServer(position); // resting guards wake wherever they are
                else if ((robot.transform.position - position).magnitude <= radius)
                    robot.InvestigateServer(position, true);
        }

        /// <summary>Players in the session (at least 1).</summary>
        static int TeamSize => Mathf.Max(1, PlayerController.All.Count);

        void Update()
        {
            Pings.Expire(Time.time);
            RevealMap();
            PrepareNextHouse();
            UpdateDriveScene();
            if (!m_IsServer) return;
            if (DriveUntil > 0 && Phase == RunPhase.Playing && !m_Arriving && Session.ServerTime >= ArriveAt)
            {
                DriveUntil = 0; // the car stands at the gate: the team gets out
                Banner(Loc.Pack("banner.house", Level.Index + 1));
            }
            if (m_Run.SetPlayers(TeamSize))
                Banner(Loc.Pack("banner.team_quota", m_Run.Players, Quota));
            foreach (var p in PlayerController.All)
                if (!p.Downed && !p.Invisible) ScentTrail.Record(p.ClientId, p.transform.position, Time.time);
            if (Phase == RunPhase.Sewer) { UpdateSewer(); return; }
            if (Phase != RunPhase.Playing || PlayerController.All.Count == 0) return;
            UpdateLures();
            UpdateHacks();
            UpdateDryers();
            UpdateStorageDoor();
            UpdateLift();
            UpdateObjective();
            UpdateHatch();
            if (Phase != RunPhase.Playing) return;
            // the new enemy mode has no hunter: the alarm wakes the guards in the charging room instead
            bool classic = ActiveLevel.Layout == null || ActiveLevel.Layout.ClassicEnemies;
            if (classic && !InGracePeriod && m_Run.ShouldSendHunter(Time.time - LevelStartTime)) SpawnHunter();
            // the new mode: when the house's time is up (the hunter's timer) the guards leave their docks for good
            if (!classic && !m_GuardsAwake && m_Run.HunterForecast(Time.time - LevelStartTime).seconds == 0f)
            {
                m_GuardsAwake = true;
                foreach (var robot in RobotController.Robots) if (robot.Docked) robot.WakeForGoodServer();
                Banner(Loc.Pack("banner.guards_awake"));
            }
            UpdateHunterForecast();
            foreach (var p in PlayerController.All)
                if (!p.KnockedOut) return; // lying with HP left is not out: they get up
            if (m_Run.Wipe()) Banner(Loc.Pack("banner.run_over", RunTotal));
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
                map.RevealAround(eye, radius, Tuning.Player.Team.OutsideRevealRadius);
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
            Link.BroadcastPing(owner, kind, position, id);
        }

        /// <summary>Every peer: a teammate's ping (delivered by the network twin).</summary>
        public void ReceivePing(ulong owner, PingKind kind, Vector3 position, ulong target)
        {
            var team = Tuning.Player.Team;
            float duration = kind is PingKind.Enemy or PingKind.Thing ? team.MarkDuration : team.PingDuration;
            Pings.Add(new TeamPing { Owner = owner, Kind = kind, Position = position, Target = target, Born = Time.time, Until = Time.time + duration });
            var bank = Audio.Sfx.Bank;
            Audio.Sfx.Ui(kind is PingKind.Enemy or PingKind.Thing ? bank?.Mark : bank?.Ping, 0.8f);
        }

        /// <summary>Server: publish when the next hunter comes, and warn the team a minute before.</summary>
        void UpdateHunterForecast()
        {
            var (seconds, alarms) = m_Run.HunterForecast(Time.time - LevelStartTime);
            HunterAt = seconds < 0f ? 0 : Session.ServerTime + seconds;
            HunterAlarmsLeft = alarms;
            if (seconds < 0f || seconds > HunterWarning) { m_HunterWarned = false; return; }
            if (m_HunterWarned) return;
            m_HunterWarned = true;
            Banner(Loc.Pack("banner.hunter_soon"));
        }

        /// <summary>Seconds before the hunter comes that the team is warned.</summary>
        const float HunterWarning = 60f;

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
            // it sleeps where it came in until someone else spots a player (RobotSenses.FollowsSightings)
            Banner(Loc.Pack("banner.hunter_in"));
            Debug.Log($"[Game] Hunter spawned at {best} (alarms {m_Run.Alarms})");
        }

        // ------------------------------------------------------------------ requests



        /// <summary>Any player: switch the whole team to this visual style (F8).</summary>
        public void RequestStyle(int index)
        {
            Link.RequestStyle(index);
        }

        /// <summary>Server: the team's style (replicated by the twin, applied on the host at once).</summary>
        public void SetStyleServer(int index)
        {
            if (VisualStyles.At(index) == null) return;
            StyleIndex = index;
            ApplyStyle(index);
        }

        /// <summary>Every peer: put the team's style on (the twin calls it when the replicated index changes).</summary>
        public void ApplyStyle(int index)
        {
            var style = VisualStyles.At(index);
            if (style == null || index == m_AppliedStyle) return;
            m_AppliedStyle = index;
            VisualStyles.Apply(style);
            var me = PlayerController.Local;
            VisualStyles.SnapFog(me != null && ActiveLevel.Layout != null && !ActiveLevel.Layout.IsInside(me.transform.position));
            Debug.Log($"[Game] Team style: {style.name}");
            StyleAnnounced?.Invoke(Loc.F("banner.style", VisualStyles.TitleOf(style)));
        }

        public void RequestRestart()
        {
            Link.RequestRestart();
        }

        /// <summary>
        /// Server: the house is left once everyone who can is hiding in the hatch (anyone can climb back out) — after a
        /// short countdown the team is in the sewer.
        /// </summary>
        void UpdateHatch()
        {
            // everyone in a hatch (either one), at least one in the exit's
            // (the basement the team came in through counts as the way back: down there is "in the entry")
            bool hidden = RunState.TeamInHatch(PlayerController.All.Select(p => (p.InHatch || InBasement(p.transform.position), p.InHatch && p.HatchIndex == 1, p.KnockedOut)));
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
            DriveUntil = 0;
            int lost = LoadCarServer();
            m_Opened.Clear(); // the house is gone; the gate is shut
            m_Hacked.Clear();
            OpenedVersion++;
            foreach (var p in PlayerController.All)
            {
                // left lying in the house: back with the team, but whatever they carried stayed there
                if (p.KnockedOut) p.GetComponent<PlayerInventory>()?.ClearServer();
                p.LeaveHatchServer();
                p.ResetForLevelServer();
            }
            SyncLocation();
            SpawnCarServer();
            // only what the team lost: the sewer, the gate and the fuel are on the screen anyway
            if (down > 0) Banner(Loc.Pack("banner.left_behind", down, Mathf.RoundToInt(m_Run.Settings.DeathPenalty * down * 100)));
            if (lost > 0) Banner(Loc.Pack("banner.hatch_lost", lost));
        }

        public bool QuotaMet => m_Run.QuotaMet;

        /// <summary>
        /// Server: a new run — fresh seed, empty stash and backpacks, the team in the sewer before the first fork.
        /// </summary>
        void StartRunServer()
        {
            foreach (var obj in m_Spawned) Session.Despawn(obj);
            m_Spawned.Clear();
            foreach (var item in WorldItem.All.ToArray()) Session.Despawn(item.gameObject);
            ScentTrail.Clear();
            m_RunSeed = DevFlags.ForcedSeed != 0 ? DevFlags.ForcedSeed : new System.Random().Next(1, int.MaxValue);
            DevFlags.ForcedSeed = 0;
            EnsureRoute(m_RunSeed);
            m_Run.BeginRun();
            m_Opened.Clear();
            m_Hacked.Clear();
            ForgetLeavers();
            OpenedVersion++;
            LeaveAt = 0;
            GoAt = 0;
            ChosenLane = -1;
            Votes.Clear();
            Level = new LevelInfo { Seed = 0, Index = RunMap.StartDepth, Players = TeamSize, Lane = 0, RunSeed = m_RunSeed };
            Cargo.Clear();
            Stash.Clear();
            ExitStash.Clear();
            CancelArrival();
            DriveUntil = 0;
            m_Opened.Add(OpenKeys.Gate); // the start of the run: no gate to open
            OpenedVersion++;
            foreach (var p in PlayerController.All)
            {
                // left lying in the house: back with the team, but whatever they carried stayed there
                if (p.KnockedOut) p.GetComponent<PlayerInventory>()?.ClearServer();
                p.LeaveHatchServer();
                p.ResetForLevelServer();
            }
            SyncLocation();
            SpawnCarServer();
            Debug.Log($"[Game] Run started in the sewer (run seed {m_RunSeed}): {RouteOptions.Count} ways");
        }

        // ------------------------------------------------------------------ locked doors and safes

        /// <summary>Server: a player finished hacking a locked door.</summary>
        void UnlockDoorServer(PlayerController player, int door)
        {
            if (Phase != RunPhase.Playing || player == null || player.Downed) return;
            var d = AutoDoor.All.FirstOrDefault(a => a.Index == door && a.HasLock && !a.NeedsKey); // a key door needs its key
            if (d == null) return;
            var at = player.transform.position;
            bool byHand = ServerReach.Within(at, d.transform.position, AutoDoor.HackReach);
            if (d.Storage)
            {
                // the storage room's door: from its terminal, or by hand from inside
                bool terminal = SecurityTerminal.All.Any(t => t.Kind == TerminalKind.Doors
                    && ServerReach.WithinFlat(at, t.transform.position, SecurityTerminal.Reach) && Mathf.Abs(t.transform.position.y - at.y) < 2f);
                if (!terminal)
                {
                    // by hand from inside: open for now, so the player gets out; locked again behind them (UpdateStorageDoor)
                    if (!byHand || !AutoDoor.IsInside(d.transform.position, d.Inward, at)) return;
                    m_EscapeSeen = Time.time;
                    if (m_Opened.Add(OpenKeys.Escape(door))) OpenedVersion++;
                    return;
                }
            }
            else if (!byHand) return;
            if (m_Opened.Add(OpenKeys.Door(door))) OpenedVersion++;
        }

        static Vector3 Flat(Vector3 p) => new(p.x, 0f, p.z);

        /// <summary>Server: last time a player was in the storage room or at its door while it was held open from inside.</summary>
        float m_EscapeSeen;
        /// <summary>Server: how long the storage door stays open from inside with nobody left in the room or at the door, s.</summary>
        const float EscapeLinger = 1f;

        /// <summary>Server: the storage door opened from inside locks again once no player is in the room or at the door.</summary>
        void UpdateStorageDoor()
        {
            var d = AutoDoor.StorageDoor;
            if (d == null || !IsOpened(OpenKeys.Escape(d.Index))) return;
            var at = d.transform.position;
            foreach (var p in PlayerController.All)
                if (AutoDoor.AtDoor(at, p.transform.position) || AutoDoor.InRoom(at, d.Inward, d.Room, p.transform.position))
                {
                    m_EscapeSeen = Time.time;
                    return;
                }
            if (Time.time - m_EscapeSeen < EscapeLinger) return;
            m_Opened.Remove(OpenKeys.Escape(d.Index));
            OpenedVersion++;
        }

        /// <summary>Server: a player at a hacking terminal hacks a robot / device anywhere in the house (a charge is spent).</summary>
        public void RemoteHackServer(PlayerController player, int terminal, Detector target)
        {
            if (Phase != RunPhase.Playing || player == null || player.Downed || target == null || target.IsHacked) return;
            var t = SecurityTerminal.Find(terminal);
            if (t == null || t.Kind != TerminalKind.Hacking) return;
            if (!ServerReach.WithinFlat(player.transform.position, t.transform.position, SecurityTerminal.Reach)) return;
            int uses = TerminalUses(terminal);
            if (uses >= Tuning.Player.Interaction.TerminalHacks) return;
            m_Opened.Add(OpenKeys.Terminal(terminal, uses));
            OpenedVersion++;
            TeamHackServer(target);
        }

        /// <summary>
        /// Server: a player opens or closes a drawer / locker door (looking right at it). The first time a part with hidden
        /// loot opens, the item appears inside.
        /// </summary>
        void ToggleStorageServer(PlayerController player, int prop, int part)
        {
            if (Phase != RunPhase.Playing || player == null || player.Downed) return;
            var storage = Items.Storage.All.FirstOrDefault(s => s.Index == prop);
            if (storage == null || part < 0 || part >= storage.PartCount) return;
            if (Vector3.Distance(storage.transform.position, player.transform.position) > StorageReach) return;
            int key = OpenKeys.Storage(prop, part);
            if (!m_Opened.Remove(key)) m_Opened.Add(key);
            OpenedVersion++;
            if (!m_Opened.Contains(key) || !m_Hidden.Remove(key, out var loot)) return;
            var (pos, rot) = storage.LootPose(part, loot.shape);
            Session.Spawn(ItemPrefab, pos, rot, go => go.GetComponent<WorldItem>().SetLook(loot.shape, loot.color));
        }

        /// <summary>How far from a drawer cabinet / lockers a player can be to use them (the client asks by looking at a part).</summary>
        const float StorageReach = 3.2f;

        /// <summary>Server: a player cracked a safe — it opens and gives valuable loot.</summary>
        void OpenSafeServer(PlayerController player, int safe)
        {
            if (Phase != RunPhase.Playing || player == null || player.Downed || IsOpened(OpenKeys.Safe(safe))) return;
            var box = Items.SafeBox.All.FirstOrDefault(s => s.Index == safe);
            if (box == null || !ServerReach.Within(player.transform.position, box.transform.position, Items.SafeBox.Reach)) return;
            if (!Perception.ReachClear(player.Hands, box.transform.position + Vector3.up * 0.5f, box.transform)) return; // not through a wall
            m_Opened.Add(OpenKeys.Safe(safe));
            OpenedVersion++;
            var s = Tuning.Items.Safes;
            var rng = new System.Random(box.Seed);
            int value = 0;
            for (int i = 0; i < s.LootItems; i++)
            {
                byte color = (byte)Mathf.Clamp(s.LootColors[rng.Next(s.LootColors.Length)], 0, ItemCatalog.Colors.Length - 1);
                byte shape = ItemCatalog.LootShape(rng);
                value += SpawnItemServer(shape, color, box.LootSpot(i), rng.Next(360)).Value;
            }
            Banner(Loc.Pack("banner.safe", player.Nick, value));
        }

        /// <summary>Server: a player pressed R after a failed run.</summary>
        int m_CheatHouses;

        /// <summary>
        /// Server, cheat (F4 panel): a house of <paramref name="kind"/> starts now, whatever is going on — in a house, in the
        /// sewer, on the drive, after the run failed: the drive and the countdowns stop, the car's seats and the hatches empty,
        /// everyone stands up and comes up in the new house's entry hatch (its number stays: the same depth of the run).
        /// </summary>
        public void StartHouseServer(HouseKind kind)
        {
            CancelArrival();
            DriveUntil = 0;
            GoAt = 0;
            LeaveAt = 0;
            Vehicles.Car.Active?.ClearSeatsServer();
            foreach (var p in PlayerController.All) p.LeaveHatchServer();
            int index = Mathf.Max(0, Level.Index);
            int seed = HouseKinds.SeedOf(kind, index, LevelRandom.Mix(m_RunSeed, Level.Seed, ++m_CheatHouses));
            if (seed == 0) return;
            StartLevel(index, Mathf.Max(0, Level.Lane), seed);
            Banner(Loc.Pack("banner.house", index + 1));
        }

        public void RestartRunServer()
        {
            if (!m_Run.Restart()) return;
            foreach (var p in PlayerController.All) p.GetComponent<PlayerInventory>().ClearServer();
            Stash.Clear();
            StartRunServer();
        }

        // ------------------------------------------------------------------ banners

        /// <summary>Server: a banner for everyone — a <see cref="Loc.Pack"/>ed text, each peer reads it in its own language.</summary>
        void Banner(string text)
        {
            Link.BroadcastBanner(text);
        }

        /// <summary>Every peer: show a banner (delivered by the network twin).</summary>
        public void ShowBanner(string text)
        {
            text = Loc.Unpack(text);
            Debug.Log("[Game] " + text);
            OnBanner?.Invoke(text);
            BannerShown?.Invoke(text);
        }
    }
}
