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

        /// <summary>
        /// Server: someone climbed up into the house (out of the basement or up the exit's ladder): its clock runs from then
        /// on without a break, wherever the team goes (the sewer under it included) — the hunter, the guards waking and the
        /// reinforcements count on it (<see cref="HouseTime"/>).
        /// </summary>
        bool m_HouseEntered;

        /// <summary>Server: seconds since the first player climbed up into this house (0 until then).</summary>
        float HouseTime => m_HouseEntered ? Time.time - LevelStartTime : 0f;

        /// <summary>
        /// The server time the house's clock started (the first player up in it), 0 before that — replicated: every peer
        /// shows the clock counting up and when the next reinforcement comes (<see cref="UI.HouseClockRules"/>).
        /// </summary>
        public double HouseClockAt { get; private set; }

        /// <summary>Client: the server's house clock.</summary>
        public void ApplyHouseClock(double at) => HouseClockAt = at;

        /// <summary>Seconds on the house's clock now (0 until someone is up in the house), on every peer.</summary>
        public float HouseClock => HouseClockAt > 0 ? Mathf.Max(0f, (float)(Session.ServerTime - HouseClockAt)) : 0f;

        /// <summary>Server: the clock starts when the first player is up in the house (the sensors' grace period with it).</summary>
        void UpdateHouseEntry()
        {
            if (m_HouseEntered) return;
            foreach (var p in PlayerController.All)
                if (!p.Downed && p.SeatIndex < 0 && p.transform.position.y > LevelLayout.Underground + 1f)
                {
                    m_HouseEntered = true;
                    LevelStartTime = Time.time;
                    HouseClockAt = Session.ServerTime;
                    return;
                }
        }
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
        /// <summary>The house's quota: the fuel a drive on costs by an average way (each way its share of it, <see cref="CostOf"/>).</summary>
        public int Quota => m_Run.Quota;
        public int Collected => m_Run.Collected;

        /// <summary>The fuel the drive to the house in row <paramref name="lane"/> of the next column costs (every peer works it out the same way).</summary>
        public int CostOf(int lane) => RunState.RouteCost(Quota, RunState.RouteShare(Level.RunSeed, Level.Index + 1, lane, m_Run.Settings.RouteFuel));

        /// <summary>The cheapest way on from here (what the team must at least get).</summary>
        public int CheapestCost
        {
            get
            {
                var options = RouteOptions;
                if (options.Count == 0) return Quota;
                int best = int.MaxValue;
                foreach (int lane in options) best = Mathf.Min(best, CostOf(lane));
                return best;
            }
        }

        /// <summary>
        /// The fuel the team is after now: the way chosen (the car standing in its tunnel), else the cheapest — the fuel on
        /// the HUD and "enough" everywhere mean this.
        /// </summary>
        public int FuelNeeded
        {
            get
            {
                if (ChosenLane >= 0) return CostOf(ChosenLane);
                var sewer = SewerLocation.Active;
                var car = Vehicles.Car.Active;
                var options = RouteOptions;
                if (sewer != null && car != null && options.Count > 0)
                {
                    int way = SewerBranches.WayOf(sewer.BranchAt(car.transform.position), options.Count);
                    if (way >= 0) return CostOf(options[way]);
                }
                return CheapestCost;
            }
        }
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
            // on the drive the sewer stays as it is (the next house goes up meanwhile; UpdateDrive takes it away behind the car)
            if (DriveUntil > 0 && m_Sewer != null && Phase is RunPhase.Sewer or RunPhase.Playing) { if (Phase == RunPhase.Playing && ActiveLevel.Layout == null) BuildLocal(); return; }
            // a new run after the water rose (or another run's sewer): built afresh, dry
            if (Phase == RunPhase.Sewer && m_Sewer != null && (m_Sewer.Flooded || m_SewerFor != (Level.RunSeed, Level.Index))) LeaveSewerLocal();
            if (Phase == RunPhase.Sewer && m_Sewer == null) EnterSewerLocal();
            else if (Phase == RunPhase.Playing)
            {
                // The new house and the phase arrive as separate updates: the house may have been built (and taken
                // away by the sewer) before the phase said "playing" — then it is built again now.
                if (m_Sewer != null && !IsHouseSewer(m_Sewer)) LeaveSewerLocal(); // (this house's own stays: EnsureHouseSewer)
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
            m_Sewer = Instantiate(Art.Sewer, SewerLocation.Origin, Quaternion.identity).GetComponent<SewerLocation>();
            m_Sewer.Build(SewerPlan.Make(Level.RunSeed, Level.Index));
            m_SewerFor = (Level.RunSeed, Level.Index);
            ApplyAtmosphere();
            VisualStyles.Dress(m_Sewer.gameObject);
            Debug.Log("[Game] In the sewer");
        }

        void LeaveSewerLocal()
        {
            if (m_SewerBuild != null) StopCoroutine(m_SewerBuild);
            m_SewerBuild = null;
            if (m_Sewer != null) Destroy(m_Sewer.gameObject);
            m_Sewer = null;
            m_SewerShifted = false;
        }

        Coroutine m_SewerBuild;
        /// <summary>The house's sewer went down out of the way on the drive (<see cref="ShiftSewerForDrive"/>).</summary>
        bool m_SewerShifted;

        /// <summary>This house's own sewer (under it, of its depth), not the one between houses or another house's.</summary>
        bool IsHouseSewer(SewerLocation sewer) => sewer != null && sewer.UnderHouse && m_SewerFor == (Level.RunSeed, Level.Index);

        /// <summary>
        /// Every peer, a house standing (not on the drive): its sewer goes on from the basement's gate under it — the descent
        /// from the exit hatch, the hall, the tunnels on — built a piece of a frame at a time; the basement's own gate and
        /// the stretch past it give way to it. Another house's sewer (left from the drive) goes first.
        /// </summary>
        void EnsureHouseSewer()
        {
            if (Phase != RunPhase.Playing || DriveUntil > 0 || Art == null || Art.Sewer == null) return;
            var l = ActiveLevel.Layout;
            if (l == null || !ActiveLevel.Ready || l.Seed != Level.Seed) return;
            if (m_Sewer != null)
            {
                if (IsHouseSewer(m_Sewer)) return;
                LeaveSewerLocal();
            }
            var frame = l.Cellar;
            var exit = frame.ToLocal(l.ExitHatchPosition); // (the passage to its shaft: SewerPlan.Passage)
            var plan = SewerPlan.Make(Level.RunSeed, Level.Index, new Vector2(exit.x, exit.z));
            var at = frame.ToWorld(new Vector3(Basement.GateX - plan.GateX, 0f, 0f)); // its gate where the basement's stands
            m_Sewer = Instantiate(Art.Sewer, at, frame.Rotation).GetComponent<SewerLocation>();
            m_Sewer.MarkUnderHouse(); // (before any frame passes: nobody takes it for the sewer between houses)
            m_SewerFor = (Level.RunSeed, Level.Index);
            m_SewerShifted = false;
            m_SewerBuild = StartCoroutine(BuildHouseSewer(m_Sewer, plan));
        }

        IEnumerator BuildHouseSewer(SewerLocation sewer, SewerPlan plan)
        {
            foreach (var _ in sewer.BuildSteps(plan)) yield return null;
            if (sewer == null) yield break;
            VisualStyles.Dress(sewer.gameObject);
            // the basement's stand-in gate and the stretch past it (built with the house) give way to the sewer's own
            var root = ActiveLevel.Root;
            if (root != null)
                foreach (Transform piece in root.transform)
                    if (piece.name == "BasementGate" || piece.name == "BasementFarCap" || piece.name.StartsWith("BasementBeyond", System.StringComparison.Ordinal))
                        piece.gameObject.SetActive(false);
            m_SewerBuild = null;
        }

        /// <summary>
        /// Every peer, the moment the car drives off: the house's sewer (the car in its tunnel, the team in the car) goes down
        /// deep under the house in one frame, as the sewer between houses always stood (<see cref="SewerLocation.Origin"/>):
        /// the next house goes up where this one stands, its basement and sewer with it. Everything in it moves together —
        /// the car's drive is worked out in its tunnel's frame — so nothing changes on screen; a body on foot down here
        /// (lying, left behind) moves with it.
        /// </summary>
        void ShiftSewerForDrive()
        {
            if (m_SewerShifted || m_Sewer == null || !m_Sewer.UnderHouse || DriveUntil <= 0) return;
            m_SewerShifted = true;
            var delta = new Vector3(0f, SewerLocation.Origin.y - m_Sewer.transform.position.y, 0f);
            var me = PlayerController.Local;
            bool carry = me != null && me.SeatIndex < 0 && me.transform.position.y < LevelLayout.Underground;
            m_Sewer.transform.position += delta;
            Physics.SyncTransforms();
            if (carry) me.TeleportOwner(me.transform.position + delta, me.transform.rotation);
        }

        (int seed, int depth) m_SewerFor;

        // ------------------------------------------------------------------ level lifecycle

        /// <summary>
        /// Every peer: builds the house of <paramref name="info"/> (its layout from the background generation when it is
        /// the house the drive went to, else generated now), a piece per step.
        /// </summary>
        IEnumerator BuildLocalSteps(LevelInfo info)
        {
            if (info.Seed == 0 || info.Seed == m_BuiltSeed) yield break;
            m_BuiltSeed = info.Seed;
            if (DriveUntil <= 0) LeaveSewerLocal(); // (on the drive the car is still in it: UpdateDrive takes it away)
            // Every peer: the streams of this house restart from its seed (cosmetic rolls match across peers too).
            LevelRandom.Begin(info.Seed);
            UnityEngine.Random.InitState(LevelRandom.Mix(info.Seed, (int)RandomChannel.Engine, 0));
            var level = Tuning.Level;
            var layout = TakePrepared(info) ?? LevelGenerator.Generate(info.Seed, info.Index, level.Building, level.Security, Mathf.Max(1, info.Players), level.Features);
            foreach (var _ in ActiveLevel.LoadSteps(layout, Art)) yield return null;
            Exploration = new ExplorationMap(layout);
            Pings.Clear();
            ApplyAtmosphere();
            foreach (var _ in VisualStyles.ReapplySteps()) yield return null; // (a piece of a frame at a time: FrameBudget)
            if (PlayerController.Local != null) PlayerController.Local.SnapAir(); // under the house in the sewer: its air at once
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
            RenderSettings.reflectionIntensity = 0f; // (the sky mirrors nothing; the rooms' probes do not depend on it)
            QualitySettings.realtimeReflectionProbes = true; // the rooms' reflections (RoomProbes)
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
            m_HouseEntered = false;
            HouseClockAt = 0;
            BeginHouseStatsServer();
            foreach (var obj in m_Spawned.ToArray())
            {
                Session.Despawn(obj);
                if (arriving && FrameBudget.Over) yield return null;
            }
            m_Spawned.Clear();
            foreach (var item in WorldItem.All.ToArray())
            {
                Session.Despawn(item.gameObject);
                if (arriving && FrameBudget.Over) yield return null;
            }
            ScentTrail.Clear();
            m_RunSeed = RunMap.SeedFor(m_RunSeed, DevFlags.ForcedSeed, () => new System.Random().Next(1, int.MaxValue));
            DevFlags.ForcedSeed = 0;
            EnsureRoute(m_RunSeed);
            lane = Mathf.Clamp(lane, 0, Route.LanesAt(index) - 1);
            int seed = houseSeed != 0 ? houseSeed : Route.Node(index, lane).Seed;
            // The house is sized for the team that is here now (friends who join later raise only the quota).
            int players = TeamSize;
            var info = new LevelInfo { Seed = seed, Index = index, Players = players, Lane = lane, RunSeed = m_RunSeed };
            if (!arriving) m_Prepared = null; // (a house started some other way: nothing generated for it)
            var build = BuildLocalSteps(info);
            while (build.MoveNext()) if (arriving) yield return null;
            var layout = ActiveLevel.Layout;

            // The level root prefab carries a configured NavMeshSurface (children, physics colliders, no ceiling layer).
            // On the drive it is baked on worker threads (the whole house in one frame was the drive's longest stall).
            var surface = ActiveLevel.Root.GetComponent<NavMeshSurface>();
            if (arriving)
            {
                var bake = BakeNavMeshAsync(surface);
                while (bake != null && !bake.isDone) yield return null;
                yield return null;
            }
            else surface.BuildNavMesh();

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
            var counted = new int[1];
            foreach (var _ in SpawnContentSteps(layout, seed, counted)) if (arriving) yield return null; // (a piece of a frame at a time)
            int totalValue = counted[0];
            m_Run.BeginLevel(index, totalValue, players);
            LevelStartTime = Time.time;
            m_GuardsAwake = false;
            m_Reinforcements = 0;
            if (arriving) { Stash.Clear(); ExitStash.Clear(); } // (the bed is unloaded when the car stops: the team watches it on the drive)
            else UnloadCarServer();
            SpawnStorageBotServer(layout);
            foreach (var p in PlayerController.All) p.ResetForLevelServer(); // (the team arrives down in the basement)
            if (arriving) ArriveCarServer(layout);
            else SpawnBasementCarServer(layout);
            SyncLocation();
            Debug.Log($"[Game] Level {index} (lane {lane}) started (run seed {m_RunSeed}, house seed {seed}): items worth {totalValue}, quota {Quota}, robots {layout.RobotSpawns.Count}, cameras {layout.CameraSpots.Count}, lasers {layout.LaserSpots.Count}");
        }

        /// <summary>Server: the navmesh of a just built house baked on worker threads (the sources are gathered now), or null.</summary>
        static AsyncOperation BakeNavMeshAsync(NavMeshSurface surface)
        {
            if (surface == null) return null;
            if (surface.navMeshData == null)
            {
                surface.navMeshData = new NavMeshData(surface.agentTypeID) { position = surface.transform.position, rotation = surface.transform.rotation };
                surface.AddData();
            }
            return surface.UpdateNavMesh(surface.navMeshData);
        }

        int SpawnContent(LevelLayout layout, int seed)
        {
            var counted = new int[1];
            foreach (var _ in SpawnContentSteps(layout, seed, counted)) { }
            return counted[0];
        }

        /// <summary>
        /// Server: the house's loot, features, main job, robots, cameras and lasers, a piece of a frame at a time
        /// (<see cref="FrameBudget"/>: on the drive); the loot's value (the quota counts on it) into <paramref name="counted"/>[0].
        /// </summary>
        IEnumerable<bool> SpawnContentSteps(LevelLayout layout, int seed, int[] counted)
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
                if (FrameBudget.Over) yield return true;
            }
            total += SpawnFeatures(layout);
            yield return true;
            SpawnObjective(layout); // not in the total: the main job is optional, the quota does not count on it
            counted[0] = total;
            yield return true;

            var wardens = new List<GameObject>();
            for (int si = 0; si < layout.RobotSpawns.Count; si++)
            {
                var spawn = layout.RobotSpawns[si];
                var pos = spawn.Position;
                bool onFloor = !RobotRoster.IsDevice(spawn.Kind) || spawn.Kind == RobotKind.Lidar;
                if (onFloor && NavMesh.SamplePosition(spawn.Position, out var hit, 2f, NavMesh.AllAreas)) pos = hit.position;
                var robot = SpawnRobotServer(spawn.Kind, pos, spawn.Rotation);
                if (spawn.Kind == RobotKind.Warden) wardens.Add(robot);
                // (its exact spot at the dock, not the navmesh's nearest: the dock's own carving would push it aside)
                if (spawn.Docked && robot != null && robot.TryGetComponent<RobotController>(out var docked)) docked.DockServer(spawn.Position, spawn.Rotation * Vector3.forward);
                GiveCageKey(layout, si, robot);
                if (FrameBudget.Over) yield return true;
            }
            PostWardens(layout, wardens);

            foreach (var pose in layout.CameraSpots)
            {
                SpawnRobotServer(RobotKind.Camera, pose.position, pose.rotation);
                if (FrameBudget.Over) yield return true;
            }

            foreach (var spot in layout.LaserSpots)
                m_Spawned.Add(Session.Spawn(LaserPrefab, spot.Position, spot.Rotation, go => go.GetComponent<LaserGate>().Configure(spot.Kind, spot.Width)));
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

        /// <summary>Server, cheat: fuel for any way on from this house.</summary>
        public void FillQuotaServer()
        {
            int most = Quota;
            foreach (int lane in RouteOptions) most = Mathf.Max(most, CostOf(lane));
            if (Collected < most) AddCollectedServer(most - Collected);
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
            UpdateDrive();
            EnsureHouseSewer();
            if (!m_IsServer) return;
            if (DriveUntil > 0 && Phase == RunPhase.Playing && !m_Arriving && Session.ServerTime >= ArriveAt)
            {
                DriveUntil = 0; // the car stands at the gate: the team gets out
                UnloadCarServer();
                Banner(Loc.Pack("banner.house", Level.Index + 1));
            }
            UpdateShredderServer();
            if (m_Run.SetPlayers(TeamSize))
                Banner(Loc.Pack("banner.team_quota", m_Run.Players, Quota));
            foreach (var p in PlayerController.All)
                if (!p.Downed && !p.Invisible) ScentTrail.Record(p.ClientId, p.transform.position, Time.time);
            UpdateStatsServer();
            if (Phase == RunPhase.Sewer) { UpdatePickingServer(); UpdateSewer(); return; }
            if (Phase != RunPhase.Playing || PlayerController.All.Count == 0) return;
            UpdateLures();
            UpdateHacks();
            UpdateDryers();
            UpdateStorageDoor();
            UpdateDoorsServer();
            UpdateLift();
            UpdateObjective();
            UpdateHouseEntry();
            if (m_Sewer != null && m_Sewer.UnderHouse && m_Sewer.Built && ActiveLevel.Ready) UpdateRouteServer(); // (may drive off)
            if (Phase != RunPhase.Playing) return;
            // the new enemy mode has no hunter: the alarm wakes the guards in the charging room instead
            bool classic = ActiveLevel.Layout == null || ActiveLevel.Layout.ClassicEnemies;
            if (classic && m_HouseEntered && !InGracePeriod && m_Run.ShouldSendHunter(HouseTime)) SpawnHunter();
            // the new mode: when the house's time is up (the hunter's timer) the guards leave their docks for good
            if (!classic && m_HouseEntered && !m_GuardsAwake && m_Run.HunterForecast(HouseTime).seconds == 0f)
            {
                m_GuardsAwake = true;
                foreach (var robot in RobotController.Robots) if (robot.Docked) robot.WakeForGoodServer();
                Banner(Loc.Pack("banner.guards_awake"));
            }
            UpdateHunterForecast();
            UpdateReinforcements();
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
                if (eye.y < LevelLayout.Underground) continue; // down in the basement or the sewer: the house's floor is not seen through
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
            float duration = kind is PingKind.Enemy or PingKind.Thing or PingKind.Help ? team.MarkDuration : team.PingDuration;
            Pings.Add(new TeamPing { Owner = owner, Kind = kind, Position = position, Target = target, Born = Time.time, Until = Time.time + duration });
            var bank = Audio.Sfx.Bank;
            Audio.Sfx.Ui(kind is PingKind.Enemy or PingKind.Thing ? bank?.Mark : bank?.Ping, 0.8f);
        }

        /// <summary>Server: publish when the next hunter comes, and warn the team a minute before.</summary>
        void UpdateHunterForecast()
        {
            var (seconds, alarms) = m_Run.HunterForecast(HouseTime);
            HunterAt = seconds < 0f ? 0 : Session.ServerTime + seconds;
            HunterAlarmsLeft = alarms;
            if (seconds < 0f || seconds > HunterWarning) { m_HunterWarned = false; return; }
            if (m_HunterWarned) return;
            m_HunterWarned = true;
            Banner(Loc.Pack("banner.hunter_soon"));
        }

        int m_Reinforcements;

        /// <summary>
        /// Server: the house sends more robots the longer the team stays — one 3 minutes in, the next 2 minutes later, then one
        /// a minute for good (<see cref="RobotRoster.ReinforcementsDue"/>, RunSettings); any kind of the house's table but a
        /// harmless one, coming in through the furthest room of the ground floor from the players.
        /// </summary>
        void UpdateReinforcements()
        {
            var layout = ActiveLevel.Layout;
            if (layout == null || layout.PatrolPoints.Count == 0 || DriveUntil > 0 || !m_HouseEntered) return;
            var s = m_Run.Settings;
            int due = RobotRoster.ReinforcementsDue(HouseTime, s.ReinforceFirst, s.ReinforceSecond, s.ReinforceEvery);
            if (m_Reinforcements >= due) return;
            var rng = new System.Random(LevelRandom.Mix(layout.Seed, Salt.Reinforcements, m_Reinforcements));
            m_Reinforcements++;
            var roster = Tuning.Level.Security.Roster.Robots;
            var table = layout.ClassicEnemies ? roster : RobotRoster.NewEnemies(roster);
            var kind = RobotRoster.PickReinforcement(table, layout.LevelIndex, rng);
            var at = FurthestEntry(layout);
            if (SpawnRobotServer(kind, at, Quaternion.identity) == null) return;
            Banner(Loc.Pack("banner.reinforcement"));
            Debug.Log($"[Game] Reinforcement #{m_Reinforcements}: {kind} at {at}");
        }

        /// <summary>The ground floor's patrol point furthest from every player (on the navmesh): where things come in unseen.</summary>
        Vector3 FurthestEntry(LevelLayout layout)
        {
            Vector3 best = layout.PatrolPoints[0];
            float bestScore = -1f;
            foreach (var p in layout.PatrolPoints)
            {
                if (LevelLayout.FloorOf(p.y) != 0 || layout.IsVault(p)) continue;
                float score = float.MaxValue;
                foreach (var pl in PlayerController.All) score = Mathf.Min(score, Vector3.Distance(p, pl.transform.position));
                if (score > bestScore) { bestScore = score; best = p; }
            }
            if (NavMesh.SamplePosition(best, out var hit, 3f, NavMesh.AllAreas)) best = hit.position;
            return best;
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

        /// <summary>Enough fuel for the way the team is after (<see cref="FuelNeeded"/>).</summary>
        public bool QuotaMet => Collected >= FuelNeeded;

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
            BeginPickingServer();
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
                if (!terminal) return; // (from inside: the exit button, EscapeStorageServer)
            }
            else if (!byHand) return;
            if (m_Opened.Add(OpenKeys.Door(door))) { OpenedVersion++; NoteServer(player, StatKind.Locks); }
        }

        /// <summary>Server: the exit button inside the storage room — the door opens for now, so who is in gets out; it locks again behind them (<see cref="UpdateStorageDoor"/>).</summary>
        public void EscapeStorageServer(PlayerController player, int door)
        {
            if (Phase != RunPhase.Playing || player == null || player.Downed) return;
            var d = AutoDoor.All.FirstOrDefault(a => a.Index == door && a.Storage);
            if (d == null || !AutoDoor.IsInside(d.transform.position, d.Inward, player.transform.position)) return;
            m_EscapeSeen = Time.time;
            if (m_Opened.Add(OpenKeys.Escape(door))) OpenedVersion++;
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
            if (target.Kind == RobotKind.Camera) return; // the cameras are not on the hacking terminal's list
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
        /// Server: a player opens or closes a drawer / locker door (looking right at it). A part with hidden loot opens:
        /// the item appears inside. One drawer of a column is open at a time (an open one covers those under it): opening
        /// one pushes the others back; and what lies in a drawer that shuts goes in with it (<see cref="StowServer"/>). A
        /// drawer pushed back empty stays ajar (<see cref="OpenKeys.Ajar"/>): the team sees it has been searched; one with
        /// something still in it shuts, like one never opened. A drawer a player shuts himself is left the same way.
        /// </summary>
        void ToggleStorageServer(PlayerController player, int prop, int part)
        {
            if (Phase != RunPhase.Playing || player == null || player.Downed) return;
            var storage = Items.Storage.All.FirstOrDefault(s => s.Index == prop);
            if (storage == null || part < 0 || part >= storage.PartCount) return;
            if (Vector3.Distance(storage.transform.position, player.transform.position) > StorageReach) return;
            int key = OpenKeys.Storage(prop, part);
            bool open = !m_Opened.Remove(key);
            if (open)
            {
                m_Opened.Add(key);
                m_Opened.Remove(OpenKeys.Ajar(prop, part));
                foreach (int other in storage.Stacked(part))
                    if (m_Opened.Remove(OpenKeys.Storage(prop, other)) && !StowServer(storage, other)) m_Opened.Add(OpenKeys.Ajar(prop, other));
            }
            else if (storage.Drawers && !StowServer(storage, part)) m_Opened.Add(OpenKeys.Ajar(prop, part));
            OpenedVersion++;
            if (!open || !m_Hidden.Remove(key, out var loot)) return;
            var (pos, rot) = storage.LootPose(part, loot.shape);
            var item = Session.Spawn(ItemPrefab, pos, rot, go => go.GetComponent<WorldItem>().SetLook(loot.shape, loot.color));
            if (item != null) item.GetComponent<WorldItem>().RideServer(storage, part);
        }

        /// <summary>
        /// Server: a drawer shuts — the item lying in it goes back in with it, hidden again until the drawer is opened
        /// (left where the drawer had been, it hung in the air before the cabinet). One item a drawer, as hidden loot is.
        /// True: something is in the drawer now.
        /// </summary>
        bool StowServer(Items.Storage storage, int part)
        {
            int key = OpenKeys.Storage(storage.Index, part);
            if (m_Hidden.ContainsKey(key)) return true;
            var inside = storage.OpenInside(part);
            foreach (var item in WorldItem.All)
            {
                if (item == null || item.Taking || !inside.Contains(item.transform.position)) continue;
                m_Hidden[key] = (item.Shape, item.Color);
                Session.Despawn(item.gameObject);
                return true;
            }
            return false;
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
            NoteServer(player, StatKind.Locks);
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
