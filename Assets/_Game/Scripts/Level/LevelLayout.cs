using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    public enum RoomType { Lobby, OpenSpace, Office, Meeting, Kitchen, Server, Storage, Restroom, Lounge, Corridor, Hall,
        // themed rooms (append only): each furnished as what it is (Furnisher), some with a floor of their own (StyleGeometry.RoomFloors)
        Gym, Library, CopyRoom, ControlRoom, Lab, NapRoom, Studio, LockerRoom, Infirmary, GameRoom,
        // the auto repair shop (HouseKind.Garage, PlanGenerator.Garage): the double-height workshop hall with its bays, the paint booth
        Workshop, PaintBooth,
        // the plant room off the lobby at the outer wall: where the building's drain leaves for the street sewer — the entry hatch (PlanGenerator.UtilityCorner)
        Utility,
        // the hospital (HouseKind.Hospital, PlanGenerator.Hospital): patient rooms, the nurse station open to the corridor, theatres, X-ray and CT rooms
        Ward, NurseStation, Operating, Imaging,
        // the apartment block (HouseKind.Apartments, PlanGenerator.Apartments): a flat's hallway, living room, bedroom and bathroom (its kitchen is a Kitchen)
        FlatHall, Living, Bedroom, Bathroom }
    /// <summary>
    /// Vent = a wall with a low crawl hole: crouching players fit, robots (2 m) do not. Squeeze = a narrow slot at full
    /// height: a player squeezes through sideways, robots (navmesh agents 1 m wide) do not fit.
    /// </summary>
    /// Illusion = the entrance of a hidden room: looks like a wall, players walk through, robots and the map do not.
    /// <summary>
    /// What a wall opening is. Breach: a ruined stretch of wall, everyone walks through; Hole: a low hole broken through at
    /// the floor, players crawl through (robots do not fit); InnerWindow: a window between rooms without glass, players
    /// climb through (robots do not), everyone sees and hears through it.
    /// </summary>
    /// Lift = the doorway of the apartment block's lift on a landing (its sliding doors open only where the cab stands).
    public enum EdgeType { Wall, Door, Open, Vent, Squeeze, Illusion, Breach, Hole, InnerWindow, Lift }
    public enum PropKind { Desk, Chair, ServerRack, Cabinet, MeetingTable, Plant, KitchenCounter, Hatch, Shelf, Boxes, WaterCooler, Whiteboard, Sofa, ToiletStall,
        Partition, Bookshelf, Printer, Fridge, CoffeeMachine, RoundTable, Armchair, CoffeeTable, TvStand, ReceptionDesk,
        TrashBin, CoatRack, Lockers, Pallet, WashBasin, VendingMachine, DeskLamp, FloorLamp,
        PingPongTable, BeanBag, ArcadeMachine, Safe, Terminal,
        // searchable crate, and the points of interest of the three locations (HouseFeatures)
        Crate, MedTable, Workbench, Programmer,
        // the house's main job (HouseFeatures.Objective): the hostage's cage
        Cage,
        // the themed rooms' furniture (Tools/Blender/rooms_*.py, AssetBuilder.Rooms): kitchen and restrooms
        KitchenSink, Stove, Microwave, WallCabinet, Urinal, MirrorSink, HandDryer, Bench, ShowerStall,
        // gym, infirmary, nap room
        Treadmill, ExerciseBike, WeightBench, DumbbellRack, PunchingBag, HospitalBed, MedicineCabinet, PrivacyScreen, SleepPod, Hammock,
        // library, copy room, control room
        TallBookcase, ReadingTable, Globe, Copier, PaperStack, MailShelf, Shredder, MonitorWall, ConsoleDesk,
        // studio, game room, lab
        MixingDesk, MicStand, AcousticPanel, DrumKit, PoolTable, Foosball, DartBoard, BarCounter, BarStool, LabBench, PartsCabinet, RobotArm,
        // round 2: open space
        StandingDesk, PlanterDivider, MobileWhiteboard, FilePedestal, SofaPod,
        // round 2: office
        ExecutiveDesk, FilingCabinet, Credenza, GuestChair, WallClock,
        // round 2: meeting
        ConferenceTable, ProjectorScreen, Flipchart, AvCredenza, MeetingChair,
        // round 2: lobby
        InfoKiosk, SecurityTurnstile, RoundBenchPlanter, LogoTotem, QueueBarrier,
        // round 2: kitchen
        DiningTable, KitchenIsland, Dishwasher, RecyclingBins, PantryCabinet,
        // round 2: restroom
        DoubleSink, JanitorCart, ChangingTable, TowelDispenser, SanitaryBin,
        // round 2: lounge
        SectionalSofa, EggChair, SideTable, MagazineRack, AreaRug,
        // round 2: corridor
        CorridorBench, FireCabinet, NoticeBoard, BinPair, UmbrellaStand,
        // round 2: server
        NetworkCabinet, UpsUnit, CrashCart, CracUnit, TapeLibrary,
        // round 2: storage
        MetalShelving, DrumPair, HandTruck, CrateStack, RollingCage,
        // round 2: copy room
        Plotter, WorkTable, SupplyShelf, MailCart, PaperBin,
        // round 2: control room
        MapBoard, RadioRack, OperatorChair, SupervisorDesk, AlarmPanel,
        // round 2: hall
        DisplayCase, PlanterLarge, HallBench, Sculpture, DirectoryBoard,
        // round 2: gym
        RowingMachine, PowerCage, KettlebellRack, MatRack, CableMachine,
        // round 2: locker room
        TowelShelf, LaundryCart, BenchIsland, WeighingScale, ShoeRack,
        // round 2: infirmary
        ExamTable, Wheelchair, IvStand, DoctorDesk, DefibCabinet,
        // round 2: nap room
        BunkBed, Daybed, CurtainDivider, FloorCushions, NightTable,
        // round 2: library
        CardCatalog, StudyCarrel, Lectern, WingbackChair, BookCart,
        // round 2: lab
        FumeHood, Printer3d, ComponentShelf, RobotTable, ScopeCart,
        // round 2: studio
        GuitarStand, SynthStand, VocalBooth, AmpCabinet, StudioCouch,
        // round 2: game room
        AirHockey, PokerTable, Jukebox, Pinball, GamingCorner,
        // round 2: the vault
        DepositWall, GoldPallet, MoneyCart, CashShelf,
        // the auto repair shop (Tools/Blender/garage.py): customers' cars, the lifts, the shop's equipment, the bays' doors and lines
        CarParked, CarHoodOpen, LiftCar, LiftEmpty, CarOnStands, ToolChest, TireChanger, WheelBalancer, TireRack, TireStack,
        EngineStand, EngineHoist, AirCompressor, OilDrain, HoseReel, FloorJack, OilDrums, WeldingCart, BayLines, GarageDoor, PaintRack,
        // the hospital (Tools/Blender/hospital.py): the wards, the nurse station, the theatres, imaging, the corridors
        BedHeadwall, BedCurtain, NurseStationDesk, Gurney, MedicationCart, LinenCart, SurgicalTable, SurgicalLights, AnesthesiaMachine,
        InstrumentTable, ScrubSink, XrayMachine, CtScanner, WaitingSeats, VitalsMonitor, HandSanitizer,
        // the storage room's empty pallet rack (Tools/Blender/storage.py): the workers shelve loot on it
        StorageRack,
        // the apartment block (Tools/Blender/apartments.py): the flats' furniture, the lobby's mailboxes, the landing's lift doors
        DoubleBed, Wardrobe, Dresser, Bathtub, HomeToilet, BathVanity, WashingMachine, ShoeCabinet, HallHooks, Mailboxes, ElevatorDoors }

    /// <summary>The house's main job, optional, known from the sewer already: a vault behind three locks, or a hostage in a cage.</summary>
    public enum ObjectiveKind : byte { None, Vault, Hostage }

    /// <summary>The vault's three locks, one console each (in the order of <see cref="LevelLayout.VaultConsoles"/>).</summary>
    public enum VaultGame : byte { Dial, Switches, Sequence }

    /// <summary>What a security terminal does: show the house's cameras, hack robots and devices from afar, or unlock the storage room's door.</summary>
    public enum TerminalKind : byte { Cameras, Hacking, Doors }

    /// <summary>
    /// Where a worker leaves loot in the storage room (<see cref="LevelLayout.StorageDrops"/>): on a shelf's bottom board
    /// (faced square on: <see cref="Facing"/> points from the robot at the shelf) or on the floor of the pile (any side).
    /// </summary>
    public struct StorageDrop
    {
        public Vector3 Spot;
        public Vector3 Facing;
        public bool Shelf;
    }

    public struct PropPlacement
    {
        public PropKind Kind;
        public Vector3 Position;
        public float RotationY;
        /// <summary>Uniform scale of the model (0 = 1): a point of interest shrinks to the table it stands in for.</summary>
        public float Scale;
    }

    public struct RobotSpawn
    {
        public RobotKind Kind;
        public int Floor;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    /// <summary>Laser gate patterns: one low beam, three blinking beams, one moving beam (from the third house on).</summary>
    public enum LaserPattern : byte { Low, Blinking, Moving }

    public struct LaserSpot
    {
        public LaserPattern Kind;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Width;
    }

    /// <summary>
    /// A staircase in the corner of a room: it starts on <see cref="Floor"/> and arrives on Floor + 1 in the same corner
    /// (where the slab has a hole). <see cref="Origin"/> is the corner point (world x/z), <see cref="Corner"/> (±1, ±1)
    /// which corner of the room it is: the stair lies towards −Corner from the origin.
    /// </summary>
    public struct Stair
    {
        public int Floor;
        public Vector2 Origin;
        public Vector2Int Corner;
        /// <summary>The flights run along the other wall of the corner (the mirror image of the usual staircase).</summary>
        public bool Flip;
        /// <summary>
        /// One of a stairwell's flights stacked floor over floor (the apartment block): thin flights over a half landing, the
        /// slab above cut only over them (<see cref="PlanGenerator.HoleOf"/>), its floor landing where the next one starts.
        /// </summary>
        public bool Stacked;
        /// <summary>The room it starts in and the room it arrives in (plan ids).</summary>
        public int Room, RoomAbove;
    }

    /// <summary>An opening in a wall: a doorway, a vent, a slot, an open passage.</summary>
    public struct Doorway
    {
        public EdgeType Kind;
        /// <summary>Bottom centre on the wall's centre line; the rotation's local X runs along the wall, Z across it.</summary>
        public Vector3 Position;
        public Quaternion Rotation;
        public float Width;
        public int Floor, RoomA, RoomB, Wall;
        public bool Exterior;
        /// <summary>A vent under the ceiling, climbed to by the crates under it (<see cref="PlanOpening.High"/>).</summary>
        public bool High;
    }

    /// <summary>Pure data describing a generated building. Identical on every peer for the same seed.</summary>
    public class LevelLayout
    {
        public int Seed, LevelIndex, Floors;
        /// <summary>Team size the house was generated for (a bigger team gets a bigger house).</summary>
        public int Players = 1;
        /// <summary>The floor plan: rooms of any size and shape, walls, openings, atriums.</summary>
        public HousePlan Plan;
        /// <summary>What the mass of the building is (bar, L, T, U, courtyard, H): for logs and the route panel.</summary>
        public string Mass = "";
        /// <summary>Which variant of the plan was taken (0 = the first; more when earlier ones failed the walk check).</summary>
        public int PlanAttempt;
        /// <summary>What kind of building this is (office, business centre, data centre, warehouse, headquarters).</summary>
        public HouseKind Kind;
        /// <summary>The lobby (plan room id): the hatch is in it.</summary>
        public int LobbyRoom;
        public readonly List<Stair> Stairs = new();
        /// <summary>Every opening of the plan as a pose (doors between rooms, exterior doors, vents, slots, open passages).</summary>
        public readonly List<Doorway> Doorways = new();
        /// <summary>The entry hatch (in the lobby): the team comes up here from the car.</summary>
        public Vector3 HatchPosition;
        /// <summary>The exit hatch (another room of the ground floor, far from the lobby): the team leaves by it.</summary>
        public Vector3 ExitHatchPosition;
        public readonly List<Vector3> PlayerSpawns = new();
        public readonly List<Vector3> ItemSpots = new();
        public readonly List<Vector3> PatrolPoints = new();
        public readonly List<Pose> CameraSpots = new();
        public readonly List<LaserSpot> LaserSpots = new();
        public readonly List<Pose> AutoDoors = new(); // doors in doorways (local X along the wall)
        /// <summary>Each door's kind, the way it opens and its hinges (<see cref="DoorRules.Plan"/>; parallel to <see cref="AutoDoors"/>).</summary>
        public readonly List<DoorSpec> DoorLooks = new();
        /// <summary>Indices into <see cref="AutoDoors"/> that are locked: players hack them open, robots pass.</summary>
        public readonly HashSet<int> LockedDoors = new();
        /// <summary>Security terminals: index in <see cref="Props"/> (a desk turned into a console) and what it does.</summary>
        public readonly List<(int prop, TerminalKind kind)> Terminals = new();
        /// <summary>
        /// The storage room (plan id, -1 if none: <see cref="StorageRoom"/>): its only doorway (index in <see cref="Doorways"/>),
        /// its door (in <see cref="AutoDoors"/>; locked for players, robots pass), the door terminal (in <see cref="Props"/>,
        /// -1: the door is open from the start).
        /// </summary>
        public int StorageRoom = -1, StorageDoorway = -1, StorageDoor = -1, StorageTerminal = -1;
        /// <summary>The auto repair shop's paint booth doorway (index in <see cref="Doorways"/>, widened for a car) and its big sliding door (in <see cref="AutoDoors"/>).</summary>
        public int BoothDoorway = -1, BoothDoor = -1;
        /// <summary>Where the workers leave loot in the storage room: shelves and the pile.</summary>
        public readonly List<StorageDrop> StorageDrops = new();
        /// <summary>What each room type allows (<see cref="BuildingSettings.RoomTypeRules"/>): loot sizes and bonuses read it.</summary>
        public RoomRule[] RoomRules;
        /// <summary>The hidden room (plan id; its entrances are <see cref="EdgeType.Illusion"/>), -1 if none.</summary>
        public int HiddenRoom = -1;
        /// <summary>
        /// The apartment block: the flat the shelter map is hidden in (−1: no map in this house), the room of it to look in
        /// first and a spot on its floor where the map lies when no cupboard of the flat can hold it (HouseFeatures.PlanShelterMap).
        /// </summary>
        public int MapFlat = -1, MapRoom = -1;
        public Vector3 MapSpot;

        /// <summary>The nominal width of the doorway the auto door <paramref name="i"/> stands in (a flat's bathroom: a narrow one).</summary>
        public float DoorWidthOf(int i)
        {
            if (i < 0 || i >= AutoDoors.Count) return LevelMetrics.DoorWidth;
            var at = AutoDoors[i].position;
            foreach (var d in Doorways)
                if ((d.Position - at).sqrMagnitude < 0.01f) return d.Width > 0.5f ? d.Width : LevelMetrics.DoorWidth;
            return LevelMetrics.DoorWidth;
        }

        /// <summary>The auto door <paramref name="i"/> is a flat's entry door (its hallway onto the apartment block's landing).</summary>
        public bool FlatEntry(int i)
        {
            if (i < 0 || i >= AutoDoors.Count || Kind != HouseKind.Apartments) return false;
            var at = AutoDoors[i].position;
            foreach (var d in Doorways)
            {
                if ((d.Position - at).sqrMagnitude > 0.01f || d.RoomA < 0 || d.RoomB < 0) continue;
                var a = Plan.Rooms[d.RoomA].Type;
                var b = Plan.Rooms[d.RoomB].Type;
                return (a == RoomType.FlatHall && b == RoomType.Corridor) || (b == RoomType.FlatHall && a == RoomType.Corridor);
            }
            return false;
        }
        /// <summary>Steps from the lobby per room id over the building's own ways (<see cref="RoomDepth"/>; −1 not reached).</summary>
        public int[] RoomSteps;
        int m_DeepestSteps = -1;

        /// <summary>How deep a room lies: 0 the lobby … 1 the deepest room (<see cref="RoomDepth.Share"/>).</summary>
        public float DepthShare(int room)
        {
            if (RoomSteps == null || room < 0 || room >= RoomSteps.Length) return 0f;
            if (m_DeepestSteps < 0) { m_DeepestSteps = 1; foreach (int s in RoomSteps) m_DeepestSteps = Mathf.Max(m_DeepestSteps, s); }
            return RoomSteps[room] < 0 ? 1f : RoomSteps[room] / (float)m_DeepestSteps;
        }
        /// <summary>Doors locked with a key block, and where each key lies (<see cref="HouseFeatures.KeyDoors"/>).</summary>
        public readonly List<KeyDoor> KeyDoors = new();
        /// <summary>Searchable crates: indices in <see cref="Props"/>.</summary>
        public readonly List<int> Crates = new();
        /// <summary>Wardens' posts (their robots are in <see cref="RobotSpawns"/> too).</summary>
        public readonly List<WardenPost> Wardens = new();
        /// <summary>Where lure speakers lie when the house starts.</summary>
        public readonly List<Vector3> LureSpots = new();
        /// <summary>The location's point of interest: index in <see cref="Props"/>, -1 if none.</summary>
        public int PointOfInterest = -1;
        /// <summary>The house's main job (<see cref="HouseFeatures.Objective"/>).</summary>
        public ObjectiveKind Objective;
        /// <summary>Vault: its room (plan id), its only door (index in <see cref="AutoDoors"/>), where its treasure lies.</summary>
        public int VaultRoom = -1, VaultDoor = -1;
        /// <summary>Vault: its doorway (index in <see cref="Doorways"/> and the plan's openings).</summary>
        public int VaultDoorway = -1;
        public readonly List<Vector3> VaultTreasure = new();
        /// <summary>
        /// Vault: its lock panels on the walls of the rooms next to it (the face of the wall at hand height, facing into
        /// the room); the n-th is <see cref="VaultGame"/> n.
        /// </summary>
        public readonly List<Pose> VaultConsoles = new();
        /// <summary>Hostage: the cage (index in <see cref="Props"/>) and the guard carrying its key (index in <see cref="RobotSpawns"/>).</summary>
        public int Cage = -1, KeyCarrier = -1;
        /// <summary>Hostage, and no robot to carry the key: where it lies instead.</summary>
        public Vector3 CageKeySpot;
        public Location Location => HouseFeatures.LocationOf(Kind);
        public readonly List<RobotSpawn> RobotSpawns = new();
        public readonly List<PropPlacement> Props = new();
        public Bounds BuildingBounds;
        /// <summary>Most windows on the facade (<see cref="LevelBuilder.WindowSpots"/>), −1 = wherever one fits.</summary>
        public int WindowLimit = -1;
        /// <summary>Dark rooms have dim lamps (<see cref="DimShare"/> of the usual brightness) instead of none.</summary>
        public bool DarkRoomsDim;
        public float DimShare = 0.3f;

        /// <summary>Size of the building's bounding box, metres (x, z).</summary>
        public float SizeX => Plan != null ? Plan.SizeX : 0f;
        public float SizeZ => Plan != null ? Plan.SizeZ : 0f;

        public IReadOnlyList<PlanRoom> Rooms => Plan.Rooms;
        public PlanRoom Lobby => Plan.Rooms[LobbyRoom];

        /// <summary>Floor area of the whole house in "old rooms" (100 m² each): counts of guards and loot scale with it.</summary>
        public float RoomUnits
        {
            get
            {
                // rooms only: corridors and stair halls never count
                float a = 0f;
                foreach (var r in Plan.Rooms) if (r.Type is not (RoomType.Corridor or RoomType.Hall)) a += r.Area;
                return a / 100f;
            }
        }

        public static float FloorY(int floor) => floor * LevelMetrics.FloorHeight;

        public static int FloorOf(float y) => Mathf.Max(0, Mathf.FloorToInt((y + 0.75f) / LevelMetrics.FloorHeight));

        /// <summary>The apartment block has a lift (<see cref="HousePlan.LiftWell"/>): its cab, a doorway on every landing.</summary>
        public bool HasLift => Plan != null && Plan.HasLift;

        /// <summary>The lift's well in the world (x, z), the same on every floor.</summary>
        public Rect LiftWell => HasLift
            ? new Rect(Plan.LiftWell.xMin * HousePlan.Res, Plan.LiftWell.yMin * HousePlan.Res, Plan.LiftWell.width * HousePlan.Res, Plan.LiftWell.height * HousePlan.Res)
            : default;

        /// <summary>The room at <paramref name="p"/> (its floor from p.y), or null between rooms / outside.</summary>
        public PlanRoom RoomAt(Vector3 p) => Plan?.RoomAt(p);

        /// <summary>Center of the room containing <paramref name="p"/> (at the height of p's floor); p itself outside rooms.</summary>
        public Vector3 RoomCenterAt(Vector3 p)
        {
            var r = RoomAt(p);
            return r != null ? r.Center : new Vector3(p.x, FloorY(FloorOf(p.y)), p.z);
        }

        /// <summary>
        /// The rectangular part of the room at <paramref name="p"/> it stands in (world x/z): what a wall crawler walks
        /// around and a ceiling vacuum sweeps. A 10 × 10 m square around p outside rooms.
        /// </summary>
        public Rect RoomRectAt(Vector3 p)
        {
            var r = RoomAt(p);
            if (r == null) return new Rect(p.x - 5f, p.z - 5f, 10f, 10f);
            var q = new Vector2(p.x, p.z);
            foreach (var c in r.Rects)
            {
                var w = new Rect(c.x * HousePlan.Res, c.y * HousePlan.Res, c.width * HousePlan.Res, c.height * HousePlan.Res);
                if (w.Contains(q)) return w;
            }
            return r.Bounds;
        }

        public bool IsDarkAt(Vector3 p)
        {
            var r = RoomAt(p);
            return r != null && r.Dark;
        }

        /// <summary>Inside the building footprint (gas is everywhere else, a courtyard too).</summary>
        public bool IsInside(Vector3 p)
        {
            if (Plan == null) return false;
            if (Plan.IsInside(new Vector2(p.x, p.z))) return true;
            // a balcony is the house's too: no gas up there
            foreach (var b in Plan.Balconies)
                if (b.Area.Contains(new Vector2(p.x, p.z)) && Mathf.Abs(p.y - FloorY(b.Floor) - 1f) < 1.6f) return true;
            return false;
        }

        /// <summary>
        /// Two points see and hear each other across floors only through a void (an atrium): every floor boundary the
        /// straight line crosses has no slab there. On one floor: true.
        /// </summary>
        public bool OpenBetweenFloors(Vector3 a, Vector3 b)
        {
            int fa = FloorOf(a.y), fb = FloorOf(b.y);
            if (fa == fb) return true;
            if (Plan == null) return false;
            int lo = Mathf.Min(fa, fb), hi = Mathf.Max(fa, fb);
            for (int f = lo + 1; f <= hi; f++)
            {
                float y = FloorY(f) - LevelMetrics.SlabThickness / 2f;
                float t = Mathf.Abs(b.y - a.y) < 1e-4f ? 0f : (y - a.y) / (b.y - a.y);
                var p = Vector3.Lerp(a, b, Mathf.Clamp01(t));
                var c = HousePlan.CellOf(new Vector2(p.x, p.z));
                if (Plan.KindAt(f, c.x, c.y) != CellKind.Void) return false;
            }
            return true;
        }

        /// <summary>Rooms reachable from a room in one step (doors, open passages, stairs). Vents are for crouching players only.</summary>
        public IEnumerable<int> Neighbours(int room)
        {
            foreach (var d in Doorways)
            {
                if (d.Kind is not (EdgeType.Door or EdgeType.Open or EdgeType.Breach)) continue;
                if (d.RoomA == room && d.RoomB >= 0) yield return d.RoomB;
                else if (d.RoomB == room && d.RoomA >= 0) yield return d.RoomA;
            }
            foreach (var s in Stairs)
            {
                if (s.Room == room) yield return s.RoomAbove;
                else if (s.RoomAbove == room) yield return s.Room;
            }
        }

        /// <summary>Where to stand to walk up a staircase (in front of its first step) and where it arrives on the floor above.</summary>
        public static (Vector3 bottom, Vector3 top) StairEnds(Stair s) =>
            (StairGeometry.ToWorld(s, new Vector3(LevelMetrics.StairSize + 0.6f, 0.05f, (StairGeometry.LaneA0 + StairGeometry.LaneA1) / 2f)),
             StairGeometry.ToWorld(s, new Vector3(LevelMetrics.StairSize + 0.6f, LevelMetrics.FloorHeight + 0.05f, (StairGeometry.LaneB0 + StairGeometry.LaneB1) / 2f)));

        /// <summary>Is a point within <paramref name="margin"/> of a staircase going up from or arriving on <paramref name="floor"/>?</summary>
        public bool NearStair(int floor, Vector3 p, float margin)
        {
            foreach (var s in Stairs)
            {
                if (s.Floor != floor && s.Floor + 1 != floor) continue;
                if (PlanGenerator.StairArea(s, margin).Contains(new Vector2(p.x, p.z))) return true;
            }
            return false;
        }
    }
}
