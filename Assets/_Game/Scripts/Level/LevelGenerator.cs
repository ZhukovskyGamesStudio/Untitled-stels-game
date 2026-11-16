using System.Collections.Generic;
using UnityEngine;
using static Stealth.Level.LevelMetrics;

namespace Stealth.Level
{
    /// <summary>Relative chance of a room type (LevelConfig).</summary>
    [System.Serializable]
    public struct RoomWeight
    {
        public RoomType Room;
        public int Weight;
        public RoomWeight(RoomType room, int weight) { Room = room; Weight = weight; }
    }

    /// <summary>Tuning of the building (LevelConfig): size by depth, rooms, light, doors and vents.</summary>
    [System.Serializable]
    public class BuildingSettings : System.ICloneable
    {
        [Header("Size by house (0-based); the last entry repeats")]
        public int[] FloorsPerHouse = { 2, 2, 3, 3, 3, 4 };
        [Tooltip("Rooms per floor in the first house (6 = 3 x 2): the first houses are small and easy.")]
        public float BaseRooms = 6f;
        [Tooltip("Every next house has this many more rooms per floor: the longer the run, the bigger the house.")]
        public float RoomsPerHouse = 2.5f;
        [Tooltip("Rooms per floor never go above this (36 = 6 x 6).")]
        public float MaxRooms = 36f;
        [Tooltip("Floor area by team size (1, 2, 3, 4… players; the last repeats): a bigger team gets a bigger house, much softer than ×N.")]
        public float[] AreaByPlayers = { 1f, 1.25f, 1.45f, 1.6f };

        [Header("Rooms")]
        public RoomWeight[] RoomWeights =
        {
            new(RoomType.OpenSpace, 28), new(RoomType.Office, 16), new(RoomType.Meeting, 12), new(RoomType.Kitchen, 10),
            new(RoomType.Server, 12), new(RoomType.Storage, 10), new(RoomType.Restroom, 6), new(RoomType.Lounge, 6),
            // the themed rooms: a few per house, each furnished as what it is
            new(RoomType.Gym, 4), new(RoomType.Library, 4), new(RoomType.CopyRoom, 4), new(RoomType.ControlRoom, 3), new(RoomType.Lab, 4),
            new(RoomType.NapRoom, 3), new(RoomType.Studio, 3), new(RoomType.LockerRoom, 3), new(RoomType.Infirmary, 3), new(RoomType.GameRoom, 4),
        };
        [Tooltip("What a room type allows: size, proportions, an outside wall (windows), loot. The storage room is never drawn " +
                 "by weight: a house has one only by FeatureSettings.StorageChance (StorageRoom).")]
        public RoomRule[] RoomTypeRules = Stealth.Level.RoomRules.Defaults();
        [Tooltip("Which room types belong next door to which, which should be kept apart (Zoning swaps the types of rooms on a floor by it).")]
        public RoomAdjacency[] RoomAdjacency = Stealth.Level.RoomRules.AdjacencyDefaults();

        [Header("Light")]
        [Tooltip("Chance of a dark room in the first house.")]
        public float DarkChance = 0.15f;
        [Tooltip("Added per house, up to MaxDarkHouses houses.")]
        public float DarkChancePerHouse = 0.03f;
        public int MaxDarkHouses = 5;
        [Tooltip("Chance of a flickering lamp in a lit room.")]
        public float FlickerChance = 0.12f;
        [Tooltip("Dark rooms get dim lamps instead of none: every room has a light (the kinds are normal, dim, flickering).")]
        public bool DarkRoomsDim;
        [Tooltip("How bright a dim lamp is, share of a normal one.")]
        public float DimShare = 0.3f;

        [Header("Passages")]
        [Tooltip("Share of the main passages that are open arches instead of doors.")]
        public float OpenPassageChance = 0.3f;
        [Tooltip("Loops: extra ways round a floor gets, per room on it (0.16 = about one per 6 rooms). Every room first gets " +
                 "its way in — off a corridor, a hall or the lobby when it touches one — so the rest are dead ends; these are " +
                 "placed where they make the longest way round, and made of doors or ruins by the weights below.")]
        public float LoopsPerRoom = 0.16f;
        [Tooltip("What the loops are made of (weights): a door…")]
        public float LoopDoorWeight = 2f;
        [Tooltip("…a breach: a stretch of wall knocked out (everyone walks through)…")]
        public float LoopBreachWeight = 3f;
        [Tooltip("…a hole at the floor (players crawl through, robots do not)…")]
        public float LoopHoleWeight = 2f;
        [Tooltip("…a window between the rooms without glass (players climb through, robots only see and hear through).")]
        public float LoopWindowWeight = 3f;
        [Tooltip("Crawl vents a floor gets, per room on it: the hidden ways round, placed where they cut the longest walk short.")]
        public float VentsPerRoom = 0.25f;
        [Tooltip("Share of the vents that sit up under the ceiling, climbed to by crates stacked under them on both sides.")]
        public float HighVentChance = 0.4f;
        [Tooltip("Narrow slots (players squeeze through, robots do not) a floor gets, per room on it.")]
        public float SqueezesPerRoom = 0.1f;
        [Header("Shapes")]
        [Tooltip("Chance per big room (lobby, open space, lounge, storage, meeting) to rise through two or three storeys: an atrium with galleries above.")]
        public float AtriumChance = 0.35f;
        [Tooltip("Chance of a near-square room becoming a rotunda or an octagon (doubled for meeting rooms, lobbies, lounges, halls).")]
        public float RotundaChance = 0.07f;
        [Tooltip("Chance of a room getting some of its corners cut or rounded.")]
        public float CornerCutChance = 0.3f;
        [Tooltip("Chance of a wing getting a corridor spine (otherwise its rooms open into each other).")]
        public float CorridorChance = 0.82f;
        [Tooltip("Chance of a wing getting a corridor across it, between the rooms on its two sides (a short passage joining two rooms).")]
        public float CrossCorridorChance;
        [Tooltip("Chance of a house of separate blocks joined by corridors (decided first: which blocks link and how long each corridor is, then the blocks are placed to fit, then the corridors laid). Otherwise one mass cut into rooms.")]
        public float LinkedChance;
        [Tooltip("In a linked house: share of the links that are corridors (the rest join wall to wall).")]
        public float LinkedCorridorShare = 0.6f;
        [Tooltip("In a linked house: corridor length, metres (min, max).")]
        public Vector2Int LinkCorridorLength = new(3, 9);
        [Tooltip("Every room type at least once in every house (the rooms test).")]
        public bool EveryRoomType;
        [Tooltip("Floor area the corridors take on top of the rooms (0.2 = a fifth more): corridors are never rooms, " +
                 "they do not count towards the room program or the room limits.")]
        public float CorridorShare = 0.2f;
        [Tooltip("Room sizes: 1 = the usual mix of closets, offices and halls; more = bigger rooms.")]
        public float RoomSizeScale = 1f;
        [Tooltip("Weights of the building shapes: bar, L, T, U, courtyard, H (6 numbers; empty = by floor area, the usual mix).")]
        public int[] MassWeights = new int[0];
        [Tooltip("Chance of an annex: a smaller block sticking out of a wing.")]
        public float AnnexChance = 0.45f;
        [Tooltip("Fewest rooms in a house, all floors (corridors and stair halls not counted; 0 = any): a plan with fewer is redrawn.")]
        public int RoomCountMin;
        [Tooltip("Most rooms in a house, all floors (corridors and stair halls not counted; 0 = any): a plan with more is redrawn.")]
        public int RoomCountMax;

        /// <summary>The rooms the count limits look at: everything but corridors and stair halls (the lobby counts).</summary>
        public static int CountRooms(LevelLayout l)
        {
            int n = 0;
            foreach (var r in l.Rooms) if (r.Type != RoomType.Corridor && r.Type != RoomType.Hall) n++;
            return n;
        }

        /// <summary>The house has a room count inside the limits (pure).</summary>
        public bool RoomCountOk(int rooms) => (RoomCountMin <= 0 || rooms >= RoomCountMin) && (RoomCountMax <= 0 || rooms <= RoomCountMax);

        [Tooltip("Smallest room, m² (corridors and stair halls not counted; 0 = any): the splitter aims above it, a plan with a smaller room is redrawn.")]
        public float RoomAreaMin;
        [Tooltip("Biggest room, m² (corridors and stair halls not counted; 0 = any): the splitter aims below it, a plan with a bigger room is redrawn.")]
        public float RoomAreaMax;

        /// <summary>Every room (not the corridors and stair halls) is inside the area limits (pure).</summary>
        public bool RoomAreasOk(LevelLayout l)
        {
            if (RoomAreaMin <= 0f && RoomAreaMax <= 0f) return true;
            foreach (var r in l.Rooms)
            {
                if (r.Type == RoomType.Corridor || r.Type == RoomType.Hall) continue;
                if (RoomAreaMin > 0f && r.Area < RoomAreaMin - 0.01f) return false;
                if (RoomAreaMax > 0f && r.Area > RoomAreaMax + 0.01f) return false;
            }
            return true;
        }

        [Tooltip("Fewest corridors in a house (0 = any): a plan with fewer is redrawn.")]
        public int MinCorridors;

        /// <summary>The house has enough corridors that join rooms: doors from at least two rooms into each (pure).</summary>
        public bool CorridorsOk(LevelLayout l)
        {
            if (MinCorridors <= 0) return true;
            return CountJoiningCorridors(l) >= MinCorridors;
        }

        /// <summary>Corridors with doorways into two or more rooms (a corridor between rooms, not a dead-end alcove).</summary>
        public static int CountJoiningCorridors(LevelLayout l)
        {
            int n = 0;
            foreach (var r in l.Rooms)
            {
                if (r.Type != RoomType.Corridor) continue;
                var rooms = new HashSet<int>(l.Neighbours(r.Id));
                rooms.Remove(r.Id);
                if (rooms.Count >= 2) n++;
            }
            return n;
        }

        /// <summary>Limits that make the generator redraw plans (more attempts are allowed then).</summary>
        public bool HasRoomLimits => RoomCountMin > 0 || RoomCountMax > 0 || RoomAreaMin > 0f || RoomAreaMax > 0f || MinCorridors > 0;

        [Tooltip("Fewest windows on the facade of a house (with WindowsMax ≥ 0).")]
        public int WindowsMin;
        [Tooltip("Most windows on the facade of a house (−1 = a window wherever one fits, every ~5 m of outer wall).")]
        public int WindowsMax = -1;

        /// <summary>How many windows a house may have (−1 = no limit), from its own stream (pure).</summary>
        public int WindowLimit(int seed)
        {
            if (WindowsMax < 0) return -1;
            int lo = Mathf.Clamp(WindowsMin, 0, WindowsMax);
            return new System.Random(LevelRandom.Mix(seed, (int)RandomChannel.Windows, 0)).Next(lo, WindowsMax + 1);
        }

        [Tooltip("Chance of a sliding door in a doorway without a laser.")]
        public float AutoDoorChance = 0.45f;
        [Tooltip("Chance that a sliding door is locked: players must hack it (robots have the key).")]
        public float LockedDoorChance = 0.35f;
        [Tooltip("House (0-based) from which doors can be locked.")]
        public int LockedDoorsFromHouse = 1;

        public object Clone() => MemberwiseClone();

        /// <summary>
        /// The auto repair shop's plan (set by <see cref="HouseKinds.Apply"/> for <see cref="HouseKind.Garage"/>, never saved):
        /// a double-height workshop hall with bays along the street, a two-storey block with the reception, rooms behind
        /// the hall and a gallery over it (<see cref="PlanGenerator"/>, PlanGenerator.Garage.cs).
        /// </summary>
        [System.NonSerialized] public bool Garage;

        public int FloorsFor(int house) => FloorsPerHouse.Length == 0 ? 2 : FloorsPerHouse[Mathf.Clamp(house, 0, FloorsPerHouse.Length - 1)];

        public float AreaFor(int players) =>
            AreaByPlayers == null || AreaByPlayers.Length == 0 ? 1f : AreaByPlayers[Mathf.Clamp(players - 1, 0, AreaByPlayers.Length - 1)];

        /// <summary>Target rooms per floor: grows with every house and (softly) with the team.</summary>
        public float RoomsFor(int house, int players) => Mathf.Min(MaxRooms, (BaseRooms + RoomsPerHouse * Mathf.Max(0, house)) * AreaFor(players));

        /// <summary>Rooms along X and Z for a house: the grid closest to <see cref="RoomsFor"/> (never less than 2 x 2, X ≥ Z).</summary>
        public Vector2Int GridFor(int house, int players) => GridOf(RoomsFor(house, players));

        public static Vector2Int GridOf(float rooms)
        {
            int w = 2, h = 2;
            while (w * h < 400)
            {
                int nw = w <= h ? w + 1 : w, nh = w <= h ? h : h + 1;
                if (Mathf.Abs(nw * nh - rooms) > Mathf.Abs(w * h - rooms)) break; // the next size is further from the target
                w = nw;
                h = nh;
            }
            return new Vector2Int(w, h);
        }

        public RoomType PickRoom(System.Random rng)
        {
            int total = 0;
            foreach (var w in RoomWeights) total += Mathf.Max(0, w.Weight);
            int roll = rng.Next(Mathf.Max(1, total));
            foreach (var w in RoomWeights)
            {
                roll -= Mathf.Max(0, w.Weight);
                if (roll < 0) return w.Room;
            }
            return RoomType.OpenSpace;
        }
    }

    /// <summary>Tuning of the guards (LevelConfig): how many robots, devices, cameras and lasers per house.</summary>
    [System.Serializable]
    public class SecuritySettings
    {
        [Tooltip("Guards at all: off = no robots, devices, cameras, lasers or terminals in the house (a quiet test level).")]
        public bool Enemies = true;

        [Header("Counts: base + per house")]
        public int Robots = 1;
        public int RobotsPerHouse = 1;
        [Tooltip("Extra robots per floor above the ground floor.")]
        public int RobotsPerExtraFloor = 1;
        [Tooltip("Robots keep to the rooms deep inside: the deepest room is (1 + this) times as likely as one by the lobby (RoomDepth).")]
        public float DeepRoomWeight = 2f;
        public int Devices = 0;
        public int DevicesPerHouse = 1;
        public int Cameras = 1;
        public int CamerasPerHouse = 1;
        public int Lasers = 0;
        public int LasersPerHouse = 1;
        [Tooltip("A bigger team gets a bigger house: one more robot per this many rooms it adds (all floors).")]
        public int TeamRoomsPerExtraRobot = 10;

        /// <summary>Mobile robots in a house: base, per house, per floor above the ground and per rooms a bigger team adds.</summary>
        public int RobotsFor(int house, int floors, int teamRooms) =>
            Robots + house * RobotsPerHouse + (floors - 1) * RobotsPerExtraFloor + teamRooms / Mathf.Max(1, TeamRoomsPerExtraRobot);
        [Tooltip("…and one more device per this many.")]
        public int TeamRoomsPerExtraDevice = 15;
        [Tooltip("House (0-based) from which lasers can move up and down.")]
        public int MovingLasersFromHouse = 2;

        [Header("Terminals")]
        [Tooltip("Desks turned into a terminal that shows the house's cameras (only in houses with cameras).")]
        public int CameraTerminals = 1;
        [Tooltip("Desks turned into a terminal that hacks robots and devices from afar.")]
        public int HackTerminals = 1;

        [Header("Who")]
        public RosterSettings Roster = new();
    }

    /// <summary>
    /// Multi-storey building generator (pure: seed in, <see cref="LevelLayout"/> out; geometry is built by
    /// <see cref="LevelBuilder"/>). The floor plan comes from <see cref="PlanGenerator"/> (rooms of any size and shape,
    /// corridors, atriums, shaped corners, doorways); this class turns it into a level: the hatch and spawns in the
    /// lobby, light and darkness per room, furniture (<see cref="Furnisher"/>), patrol points, guards, devices,
    /// cameras, lasers, sliding and locked doors, terminals.
    /// </summary>
    public static class LevelGenerator
    {
        /// <summary>Where generation time goes (milliseconds, summed over calls): for tuning, not for the game.</summary>
        public static class Timing { public static long Plan, Check, Furnish, Walk, Grid, Flood, Reach, Barrier, Rounds; public static void Reset() => Plan = Check = Furnish = Walk = Grid = Flood = Reach = Barrier = Rounds = 0; }

        /// <summary>Generates a house. Without settings the defaults are used (tests); the game passes LevelConfig.</summary>
        /// <summary>
        /// The classic generator (the main menu's switch, "-classicrooms"): the plan is cut into rooms first and each room's
        /// type guessed from its size and proportions (<see cref="PlanGenerator.GenerateClassic"/>), instead of the room
        /// program deciding the rooms first. Off by default; set before a game starts — every peer must match.
        /// </summary>
        public static bool Classic;

        public static LevelLayout Generate(int seed, int levelIndex, BuildingSettings building = null, SecuritySettings security = null, int players = 1,
            FeatureSettings features = null) => Generate(seed, levelIndex, building, security, players, features, Classic);

        public static LevelLayout Generate(int seed, int levelIndex, BuildingSettings building, SecuritySettings security, int players,
            FeatureSettings features, bool classic)
        {
            building ??= new BuildingSettings();
            security ??= new SecuritySettings();
            features ??= new FeatureSettings();
            var kind = HouseKinds.Of(seed, levelIndex);
            building = HouseKinds.Apply(kind, building);
            var rng = new System.Random(seed);
            int floors = building.FloorsFor(levelIndex);
            // what the house is made of comes first: its rooms, their types and sizes (corridors not counted); the plan is
            // then laid out to hold them. A storage room (it brings the workers) is decided here, from the house's own stream.
            float roomArea = building.RoomsFor(levelIndex, players) * 100f * PlanGenerator.RoomShareOfArea;
            bool storage = building.EveryRoomType || (features.StorageChance > 0f && levelIndex >= features.StorageFromHouse
                && new System.Random(LevelRandom.Mix(seed, 0x5702, 0)).NextDouble() < features.StorageChance);
            // Generate and check: a plan where some room cannot be walked to (a doorway that did not fit, a sealed
            // corner) is thrown away and the next variant of the same house is drawn (deterministic: attempt by attempt).
            LevelLayout l = null;
            Furnisher checker = null;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int attempts = building.HasRoomLimits ? 64 : 16;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                int planSeed = attempt == 0 ? seed : LevelRandom.Mix(seed, 0xBAD, attempt);
                var o = classic
                    ? PlanGenerator.GenerateClassic(planSeed, levelIndex, floors, building.RoomsFor(levelIndex, players) * 100f, building)
                    : PlanGenerator.Generate(planSeed, levelIndex, floors, RoomProgram.Make(planSeed, floors, roomArea, building, storage), building);
                l = new LevelLayout
                {
                    Seed = seed,
                    LevelIndex = levelIndex,
                    Players = Mathf.Max(1, players),
                    Floors = floors,
                    Plan = o.Plan,
                    LobbyRoom = o.Lobby,
                    Mass = o.Mass,
                    Kind = kind,
                    PlanAttempt = attempt,
                };
                l.Stairs.AddRange(o.Stairs);
                RaiseVents(l, building);
                Doorways(l);
                if (building.Garage) WidenBoothDoorway(l);
                l.HatchPosition = l.Lobby.Center;
                long planMs = watch.ElapsedMilliseconds;
                bool countOk = building.RoomCountOk(BuildingSettings.CountRooms(l)) && building.RoomAreasOk(l) && building.CorridorsOk(l);
                checker = o.Unreached == 0 && countOk ? new Furnisher(l, new System.Random(0)) : null;
                bool ok = checker != null && checker.Unreached().Count == 0;
                Timing.Plan += planMs; Timing.Check += watch.ElapsedMilliseconds - planMs;
                watch.Restart();
                if (ok) break;
            }

            // Sometimes a room hides behind illusion walls (it changes the openings: the doorways are listed again).
            l.HiddenRoom = HouseFeatures.HideRoom(l, features);
            if (l.HiddenRoom >= 0)
            {
                // an illusion wall may now be where a high vent's desks would stand: that vent stays low (no desk in front of the way in)
                for (int i = 0; i < l.Plan.Openings.Count; i++)
                {
                    var o = l.Plan.Openings[i];
                    if (!o.High || DesksFit(l, o)) continue;
                    o.High = false;
                    l.Plan.Openings[i] = o;
                }
                l.Doorways.Clear();
                Doorways(l);
                checker = null; // its walk grid knew the old doors
            }

            // Light: some rooms are dark (robots see worse there), some lamps flicker. Never the lobby.
            foreach (var r in l.Rooms)
            {
                if (r.Id == l.LobbyRoom) continue;
                double roll = rng.NextDouble();
                float dark = building.DarkChance + building.DarkChancePerHouse * Mathf.Min(levelIndex, building.MaxDarkHouses);
                r.Dark = roll < dark * (r.Type == RoomType.Corridor ? 0.6f : 1f);
                r.Flicker = !r.Dark && roll > 1.0 - building.FlickerChance;
            }

            // The hatch in the middle of the lobby, the team around it.
            var lobby = l.Lobby;
            l.HatchPosition = lobby.Center;
            var furnisher = new Furnisher(l, new System.Random(LevelRandom.Mix(seed, 0xF0E, 0)));
            if (checker != null) furnisher.ShareWalk(checker);
            furnisher.Reserve(0, l.HatchPosition, 2.0f);
            furnisher.Put(lobby, PropKind.Hatch, l.HatchPosition, 0f);
            var exit = ExitRoom(l);
            l.ExitHatchPosition = exit.Center;
            furnisher.Reserve(0, l.ExitHatchPosition, 2.0f);
            furnisher.Put(exit, PropKind.Hatch, l.ExitHatchPosition, 0f);
            furnisher.PlaceVentDesks();
            for (int q = 0; q < 4; q++) l.PlayerSpawns.Add(l.HatchPosition + Quadrants[q] * 0.45f);

            // every room as its type allows (the shaping after the types may have taken a kitchen's size or its windows)
            RoomRules.Enforce(l.Plan, building.RoomTypeRules);
            // zoning: themed rooms capped (one kitchen a floor…), work rooms to the facade, service rooms inside, toilets by the stairs
            l.RoomSteps = RoomDepth.Of(l.Plan, l.Stairs, l.LobbyRoom);
            Zoning.Arrange(l.Plan, l.Stairs, building.RoomTypeRules, l.RoomSteps, building.RoomAdjacency);

            // the house's main job: the vault and its lock panels, or the hostage's cage (their places kept free)
            HouseFeatures.PlanObjective(l, features, furnisher);
            // the locked storage room (furnished as storage: shelves and a pile; it brings the workers)
            l.RoomRules = building.RoomTypeRules;
            StorageRooms.Plan(l, features, storage);
            EnsureControlRoom(l, building);

            // patrol points (room centres and the middles of big rooms) stay free of furniture
            Patrols(l);
            foreach (var p in l.PatrolPoints) furnisher.Reserve(LevelLayout.FloorOf(p.y), p, 0.75f);
            l.WindowLimit = building.WindowLimit(seed); // the furniture keeps the windows clear: it must know which ones there are
            watch.Restart();
            foreach (var r in l.Rooms) furnisher.FurnishRoom(r);
            Timing.Furnish += watch.ElapsedMilliseconds;
            watch.Restart();
            furnisher.EnsureWalkable();
            if (furnisher.RestoreFridges()) furnisher.EnsureWalkable(); // every kitchen keeps its one fridge
            Timing.Walk += watch.ElapsedMilliseconds;
            if (security.Enemies)
            {
                PlaceSecurity(l, rng, building, security, features != null ? features.StorageWorkers : 0);
                PlaceTerminals(l, security);
            }
            else IdleConsoles(l);
            StorageRooms.Finish(l, features);
            HouseFeatures.KeyDoors(l, features);
            HouseFeatures.Crates(l, features);
            HouseFeatures.PointOfInterest(l);
            if (security.Enemies) HouseFeatures.Wardens(l, features); // a quiet house (rooms test): no guards with keys either
            HouseFeatures.FinishObjective(l);

            float height = floors * FloorHeight;
            l.BuildingBounds = new Bounds(new Vector3(l.SizeX / 2f, height / 2f, l.SizeZ / 2f), new Vector3(l.SizeX, height, l.SizeZ));
            l.WindowLimit = building.WindowLimit(seed);
            l.DarkRoomsDim = building.DarkRoomsDim;
            l.DimShare = building.DimShare;
            return l;
        }

        /// <summary>
        /// Some vents go up under the ceiling (own stream: the rest of the house does not shift) — only where the crates
        /// under them fit on both sides: inside the rooms, clear of stairs.
        /// </summary>
        static void RaiseVents(LevelLayout l, BuildingSettings b)
        {
            var plan = l.Plan;
            var rng = new System.Random(LevelRandom.Mix(l.Seed, 0x4856, l.PlanAttempt));
            for (int i = 0; i < plan.Openings.Count; i++)
            {
                var o = plan.Openings[i];
                bool roll = rng.NextDouble() < b.HighVentChance; // one roll per opening, used or not
                if (o.Kind != EdgeType.Vent || !roll || !DesksFit(l, o)) continue;
                o.High = true;
                plan.Openings[i] = o;
            }
        }

        /// <summary>
        /// A desk can stand under a high vent on both sides, with room in front of it to climb on: inside one room that is
        /// not a corridor (they stay bare), clear of stairs and of every other opening's doorway space.
        /// </summary>
        public static bool DesksFit(LevelLayout l, PlanOpening o)
        {
            var plan = l.Plan;
            var w = plan.Walls[o.Wall];
            if (w.Exterior || w.Railing || w.Shaped) return false;
            var mid = w.At(o.At);
            var n = new Vector2(-w.Dir.y, w.Dir.x);
            float face = LevelMetrics.WallThickness / 2f, reach = LevelMetrics.VentDeskReach + LevelMetrics.VentDeskAisle;
            foreach (float side in new[] { -1f, 1f })
            {
                PlanRoom room = null;
                for (float depth = 0.1f; depth <= reach + 0.01f; depth += 0.25f)
                    foreach (float along in new[] { -0.85f, -0.4f, 0f, 0.4f, 0.85f })
                    {
                        var p = mid + n * (side * (face + depth)) + w.Dir * along;
                        var p3 = new Vector3(p.x, LevelLayout.FloorY(w.Floor) + 0.1f, p.y);
                        var here = plan.RoomAt(p3);
                        if (here == null || here.Type == RoomType.Corridor || (room != null && here != room) || l.NearStair(w.Floor, p3, 0.3f)) return false;
                        room = here;
                        // the space a doorway keeps free (Furnisher): a desk there would block the door
                        foreach (var other in plan.Openings)
                        {
                            if (other.Wall == o.Wall && Mathf.Abs(other.At - o.At) < 0.01f) continue;
                            var ow = plan.Walls[other.Wall];
                            if (ow.Floor != w.Floor) continue;
                            var oc = ow.At(other.At);
                            var od = p - oc;
                            float oAlong = Mathf.Abs(Vector2.Dot(od, ow.Dir)), oAcross = Mathf.Abs(Vector2.Dot(od, new Vector2(-ow.Dir.y, ow.Dir.x)));
                            float half = (other.Kind == EdgeType.Open ? ow.Length : other.Width) / 2f + 0.35f;
                            if (oAlong < half && oAcross < 1.35f) return false;
                        }
                    }
            }
            return true;
        }

        /// <summary>Every opening of the plan as a pose on its wall.</summary>
        static void Doorways(LevelLayout l)
        {
            var plan = l.Plan;
            foreach (var op in plan.Openings)
            {
                var w = plan.Walls[op.Wall];
                var p = w.At(op.At);
                var d = w.Dir;
                l.Doorways.Add(new Doorway
                {
                    Kind = op.Kind,
                    Position = new Vector3(p.x, LevelLayout.FloorY(w.Floor), p.y),
                    Rotation = Quaternion.LookRotation(new Vector3(-d.y, 0f, d.x)),
                    Width = op.Width,
                    Floor = w.Floor,
                    RoomA = w.RoomA,
                    RoomB = w.RoomB,
                    Wall = op.Wall,
                    Exterior = w.Exterior,
                    High = op.High,
                });
            }
        }

        /// <summary>
        /// The room of the exit hatch: on the ground floor, not the lobby, not the hidden room, not a corridor, as far from the
        /// entry as possible (the team has to cross the house).
        /// </summary>
        static PlanRoom ExitRoom(LevelLayout l)
        {
            PlanRoom best = null;
            float far = -1f;
            foreach (var r in l.Rooms)
            {
                if (r.Floor != 0 || r.Id == l.LobbyRoom || r.Id == l.HiddenRoom || r.Type is RoomType.Corridor or RoomType.Storage or RoomType.PaintBooth || r.Area < 10f) continue; // (the booth: its car)
                if (l.NearStair(0, r.Center, 1.4f)) continue;
                float d = (r.Center - l.HatchPosition).magnitude;
                if (d > far) { far = d; best = r; }
            }
            return best ?? l.Lobby;
        }

        /// <summary>Patrol points: the middle of every room, and in big rooms the middles of the parts they were made of.</summary>
        static void Patrols(LevelLayout l)
        {
            foreach (var r in l.Rooms)
            {
                l.PatrolPoints.Add(PatrolSpot(l, r));
                if (r.Area < 60f) continue;
                foreach (var rect in r.Rects)
                {
                    var c = new Vector2((rect.xMin + rect.xMax) * HousePlan.Res / 2f, (rect.yMin + rect.yMax) * HousePlan.Res / 2f);
                    var p = new Vector3(c.x, LevelLayout.FloorY(r.Floor), c.y);
                    if ((p - r.Center).magnitude > 4f && l.Plan.RoomAt(p) == r && !l.NearStair(r.Floor, p, 0.8f)) l.PatrolPoints.Add(p);
                }
            }
        }

        /// <summary>
        /// Where a patrol stands in a room: its middle — except the rooms whose middle belongs to their furniture (the
        /// table of a meeting room, the stalls of a small restroom reach it): there a step inside the first doorway; and a
        /// kitchen's, whose table stands in the middle: beside it, along the room's long side (kept free of furniture).
        /// </summary>
        public static Vector3 PatrolSpot(LevelLayout l, PlanRoom r)
        {
            if (r.Type == RoomType.Kitchen)
            {
                var along = r.Bounds.width >= r.Bounds.height ? Vector3.right : Vector3.forward;
                foreach (float off in new[] { 2.0f, 1.6f })
                    foreach (float side in new[] { 1f, -1f })
                    {
                        var p = r.Center + along * (side * off);
                        if (l.Plan.RoomAt(p + Vector3.up * 0.3f) == r && !l.NearStair(r.Floor, p, 0.8f)) return p;
                    }
                return r.Center;
            }
            if (!Furnisher.MiddleIsFurniture(r)) return r.Center;
            foreach (var d in l.Doorways)
            {
                if (d.Floor != r.Floor || d.Kind is not (EdgeType.Door or EdgeType.Open) || d.Exterior) continue;
                var across = d.Rotation * Vector3.forward;
                foreach (float side in new[] { -1f, 1f })
                {
                    var p = d.Position + across * side;
                    p.y = r.Center.y;
                    if (l.Plan.RoomAt(p) == r && !l.NearStair(r.Floor, p, 0.8f)) return p;
                }
            }
            return r.Center;
        }

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>Fixed devices keep this far from the hatch on the ground floor (players start there).</summary>
        const float MinDeviceDistanceToHatch = 12.5f;

        /// <summary>A random free point of a room near its middle (on its floor, not on a staircase).</summary>
        static Vector3 SpotIn(LevelLayout l, PlanRoom r, System.Random rng, float spread)
        {
            for (int i = 0; i < 12; i++)
            {
                var p = r.Center + new Vector3(Range(rng, -spread, spread), 0, Range(rng, -spread, spread));
                if (l.Plan.RoomAt(p) == r && !l.NearStair(r.Floor, p, 0.6f)) return p;
            }
            return r.Center;
        }

        /// <summary>A corner of a room (for things high on its walls): where it is, and which way is into the room.</summary>
        static (Vector3 pos, Vector3 inward) CornerOf(LevelLayout l, PlanRoom r, System.Random rng, float inset)
        {
            var b = r.Bounds;
            var corners = new List<(Vector3, Vector3)>();
            foreach (var (x, z, sx, sz) in new[] { (b.xMin, b.yMin, 1, 1), (b.xMax, b.yMin, -1, 1), (b.xMin, b.yMax, 1, -1), (b.xMax, b.yMax, -1, -1) })
            {
                var p = new Vector3(x + sx * inset, LevelLayout.FloorY(r.Floor), z + sz * inset);
                if (l.Plan.RoomAt(p) == r && !l.NearStair(r.Floor, p, 0.3f)) corners.Add((p, new Vector3(sx, 0, sz).normalized));
            }
            if (corners.Count == 0) return (r.Center, Vector3.forward);
            return corners[rng.Next(corners.Count)];
        }

        /// <summary>
        /// Some office desks become security terminals (own stream: the rest of the house does not shift). Not in the lobby,
        /// and as far from each other as the desks allow, so the two kinds are not side by side.
        /// </summary>
        /// <summary>
        /// A house with a storage room has its workers: at least <paramref name="want"/> (once workers are on the roster),
        /// and all of them on the storage room's floor — wheels do not climb stairs, and their work is there.
        /// </summary>
        static void StorageWorkers(LevelLayout l, System.Random rng, SecuritySettings s, List<PlanRoom> candidates, int want, int fresh)
        {
            if (l.StorageRoom < 0) return;
            bool onRoster = false;
            foreach (var e in s.Roster.Robots) onRoster |= e.Kind == RobotKind.Worker && e.Weight > 0f && e.MinLevel <= l.LevelIndex;
            int workers = 0;
            foreach (var r in l.RobotSpawns) if (r.Kind == RobotKind.Worker) workers++;
            for (int i = l.RobotSpawns.Count - 1; onRoster && workers < want && i >= fresh; i--)
            {
                var r = l.RobotSpawns[i];
                if (r.Kind == RobotKind.Worker) continue;
                r.Kind = RobotKind.Worker;
                l.RobotSpawns[i] = r;
                workers++;
            }
            int floor = l.Plan.Rooms[l.StorageRoom].Floor;
            var rooms = candidates.FindAll(c => c.Floor == floor && c.Id != l.StorageRoom);
            if (rooms.Count == 0) return;
            for (int i = 0; i < l.RobotSpawns.Count; i++)
            {
                var r = l.RobotSpawns[i];
                if (r.Kind != RobotKind.Worker || r.Floor == floor) continue;
                var room = Weighted(rooms, rng);
                r.Floor = floor;
                r.Position = SpotIn(l, room, rng, 1.2f);
                l.RobotSpawns[i] = r;
            }
        }

        /// <summary>
        /// The robots' control room, where the camera and hacking terminals stand (<see cref="PlaceTerminals"/>): a house
        /// whose program could have one but drew none turns an office of a control room's size into it (the plan is laid
        /// out already — only the room's type, and so its furniture, changes). Not the lobby, the vault, the hidden room,
        /// the storage room nor the exit's room.
        /// </summary>
        static void EnsureControlRoom(LevelLayout l, BuildingSettings b)
        {
            bool wanted = false;
            foreach (var w in b.RoomWeights) wanted |= w.Room == RoomType.ControlRoom && w.Weight > 0;
            if (!wanted) return;
            foreach (var r in l.Rooms) if (r.Type == RoomType.ControlRoom) return;
            int exit = l.Plan.RoomAt(l.ExitHatchPosition + Vector3.up * 0.3f)?.Id ?? -1;
            PlanRoom best = null;
            foreach (var r in l.Rooms)
            {
                if (r.Type != RoomType.Office || r.Levels != 1 || r.Area < 16f || r.Area > 60f) continue;
                if (r.Id == l.LobbyRoom || r.Id == l.VaultRoom || r.Id == l.HiddenRoom || r.Id == l.StorageRoom || r.Id == exit) continue;
                if (best == null || Mathf.Abs(r.Area - 32f) < Mathf.Abs(best.Area - 32f)) best = r;
            }
            if (best != null) best.Type = RoomType.ControlRoom;
        }

        /// <summary>No security: the control room's consoles are plain desks.</summary>
        static void IdleConsoles(LevelLayout l)
        {
            for (int i = 0; i < l.Props.Count; i++)
                if (l.Props[i].Kind == PropKind.Terminal && !l.Terminals.Exists(t => t.prop == i))
                {
                    var p = l.Props[i];
                    p.Kind = PropKind.Desk;
                    l.Props[i] = p;
                }
        }

        static void PlaceTerminals(LevelLayout l, SecuritySettings s)
        {
            // the control room's consoles first (Furnisher puts them there); what is left of them becomes a plain desk
            var consoles = new List<int>();
            for (int i = 0; i < l.Props.Count; i++)
                if (l.Props[i].Kind == PropKind.Terminal && !l.Terminals.Exists(t => t.prop == i)
                    && l.Plan.RoomAt(l.Props[i].Position + Vector3.up * 0.3f)?.Id != l.LobbyRoom) consoles.Add(i); // like any terminal: not in the lobby
            var leftover = new List<int>();
            for (int i = 0; i < l.Props.Count; i++)
                if (l.Props[i].Kind == PropKind.Terminal && !l.Terminals.Exists(t => t.prop == i) && !consoles.Contains(i)) leftover.Add(i);
            var desks = new List<int>();
            for (int i = 0; i < l.Props.Count; i++)
                if (l.Props[i].Kind == PropKind.Desk && (l.Props[i].Position - l.HatchPosition).magnitude > 7.5f && !LampOn(l, i)) desks.Add(i);
            var rng = new System.Random(LevelRandom.Mix(l.Seed, 0x7E41, 0));
            var want = new List<TerminalKind>();
            // the hacking terminal first: with a single console in the control room, it is the one there
            for (int i = 0; i < s.HackTerminals; i++) want.Add(TerminalKind.Hacking);
            for (int i = 0; i < (l.CameraSpots.Count > 0 ? s.CameraTerminals : 0); i++) want.Add(TerminalKind.Cameras);
            foreach (var kind in want)
            {
                if (consoles.Count > 0)
                {
                    l.Terminals.Add((consoles[0], kind));
                    consoles.RemoveAt(0);
                    continue;
                }
                if (desks.Count == 0) break;
                int pick = desks[rng.Next(desks.Count)];
                if (l.Terminals.Count > 0)
                {
                    // the desk furthest from the terminals placed so far
                    float best = -1f;
                    foreach (var d in desks)
                    {
                        float near = float.MaxValue;
                        foreach (var t in l.Terminals) near = Mathf.Min(near, (l.Props[d].Position - l.Props[t.prop].Position).sqrMagnitude);
                        if (near > best) { best = near; pick = d; }
                    }
                }
                desks.Remove(pick);
                var p = l.Props[pick];
                p.Kind = PropKind.Terminal;
                l.Props[pick] = p;
                l.Terminals.Add((pick, kind));
            }
            consoles.AddRange(leftover);
            foreach (int i in consoles)
            {
                var p = l.Props[i];
                p.Kind = PropKind.Desk; // a console with nothing to do: a desk like the others
                l.Props[i] = p;
            }
        }

        /// <summary>The shop's paint booth: its one door widened for a car (it is driven in), remembered for its big sliding door.</summary>
        static void WidenBoothDoorway(LevelLayout l)
        {
            int booth = -1;
            foreach (var r in l.Rooms) if (r.Type == RoomType.PaintBooth) booth = r.Id;
            if (booth < 0) return;
            for (int i = 0; i < l.Doorways.Count; i++)
            {
                var d = l.Doorways[i];
                if (d.Kind != EdgeType.Door || (d.RoomA != booth && d.RoomB != booth)) continue;
                if (HouseFeatures.WidenDoorway(l, i, VehicleDoorWidth)) l.BoothDoorway = i;
                return;
            }
        }

        /// <summary>A desk lamp stands on this desk (it would go through a terminal's monitors: such a desk stays a desk).</summary>
        public static bool LampOn(LevelLayout l, int desk)
        {
            var d = l.Props[desk].Position;
            foreach (var p in l.Props)
                if (p.Kind == PropKind.DeskLamp && p.Position.y > d.y + 0.4f && p.Position.y < d.y + 1.5f
                    && new Vector2(p.Position.x - d.x, p.Position.z - d.z).magnitude < 1.0f) return true;
            return false;
        }

        static void PlaceSecurity(LevelLayout l, System.Random rng, BuildingSettings b, SecuritySettings s, int storageWorkers = 0)
        {
            int level = l.LevelIndex;
            // Rooms for guards and devices: not the lobby nor right next to it.
            var candidates = new List<PlanRoom>();
            foreach (var r in l.Rooms)
                if (r.Id != l.LobbyRoom && (r.Floor > 0 || (r.Depth >= 2 && Vector3.Distance(r.Center, l.HatchPosition) > MinDeviceDistanceToHatch + 1f)))
                    candidates.Add(r);
            if (candidates.Count == 0) foreach (var r in l.Rooms) if (r.Id != l.LobbyRoom) candidates.Add(r);

            // A bigger team gets a bigger house — and the guards to go with it.
            float solo = b.RoomsFor(level, 1) * l.Floors * PlanGenerator.RoomShareOfArea; // rooms only, like RoomUnits
            int teamRooms = Mathf.Max(0, Mathf.RoundToInt(l.RoomUnits - solo));
            int robots = s.RobotsFor(level, l.Floors, teamRooms);
            var freshRobots = RobotRoster.Newcomers(s.Roster.Robots, level);
            var freshDevices = RobotRoster.Newcomers(s.Roster.Devices, level);
            for (int i = 0; i < robots; i++)
            {
                // Every floor gets at least one robot, then they spread (bigger rooms more likely).
                PlanRoom room;
                if (i < l.Floors)
                {
                    var onFloor = candidates.FindAll(c => c.Floor == i);
                    room = onFloor.Count > 0 ? Weighted(onFloor, rng, l, s.DeepRoomWeight) : candidates[rng.Next(candidates.Count)];
                }
                else room = Weighted(candidates, rng, l, s.DeepRoomWeight);
                var pos = SpotIn(l, room, rng, 1.2f);
                if (room.Floor == 0 && Vector3.Distance(pos, l.HatchPosition) < MinDeviceDistanceToHatch) pos = room.Center;
                var kind = RobotRoster.PickRobot(level, rng, s.Roster);
                if (i < freshRobots.Count) kind = freshRobots[i]; // the house that introduces a kind shows it
                // workers haul loot to the storage room: no storage room, no workers
                for (int k = 0; k < 6 && kind == RobotKind.Worker && l.StorageRoom < 0; k++) kind = RobotRoster.PickRobot(level, rng, s.Roster);
                if (kind == RobotKind.Worker && l.StorageRoom < 0) kind = RobotKind.Guard;
                l.RobotSpawns.Add(new RobotSpawn { Kind = kind, Floor = room.Floor, Position = pos, Rotation = Quaternion.Euler(0, rng.Next(360), 0) });
            }
            StorageWorkers(l, rng, s, candidates, storageWorkers, freshRobots.Count);

            // Doorways between rooms: pressure plates lie in some of them, lasers guard others.
            var doors = new List<LaserSpot>();
            for (int i = 0; i < l.Doorways.Count; i++)
            {
                var d = l.Doorways[i];
                if (d.Kind == EdgeType.Door && !d.Exterior && i != l.BoothDoorway) // (the paint booth's: its big door, nothing in the way)
                    doors.Add(new LaserSpot { Position = d.Position, Rotation = d.Rotation, Width = ClearWidth(DoorWidth) });
            }
            // Fixed security devices: pressure plates in doorways, motion sensors on walls, lidar poles.
            int devices = s.Devices + level * s.DevicesPerHouse + teamRooms / Mathf.Max(1, s.TeamRoomsPerExtraDevice);
            var hatch = l.HatchPosition;
            for (int i = 0; i < devices; i++)
            {
                var kind = RobotRoster.PickDevice(level, rng, s.Roster);
                if (i < freshDevices.Count) kind = freshDevices[i];
                // A pressure plate lies across a doorway: whoever goes through jumps over it, hacks it, or trips it.
                if (kind == RobotKind.PressurePlate && TakeDoorway(doors, rng, hatch, out var doorway))
                {
                    l.RobotSpawns.Add(new RobotSpawn { Kind = kind, Floor = LevelLayout.FloorOf(doorway.Position.y), Position = doorway.Position, Rotation = doorway.Rotation });
                    continue;
                }
                PlanRoom room = null;
                Vector3 pos = default;
                Quaternion rot = Quaternion.identity;
                for (int tries = 0; tries < 20; tries++)
                {
                    room = Weighted(candidates, rng);
                    switch (kind)
                    {
                        case RobotKind.MotionSensor:
                        {
                            var (c, inward) = CornerOf(l, room, rng, 0.2f);
                            pos = c + Vector3.up * 2.2f;
                            rot = Quaternion.LookRotation(inward);
                            break;
                        }
                        case RobotKind.PressurePlate:
                            pos = SpotIn(l, room, rng, 3f);
                            rot = Quaternion.identity;
                            break;
                        default:
                            pos = SpotIn(l, room, rng, 0.5f);
                            rot = Quaternion.identity;
                            break;
                    }
                    if (room.Floor != 0 || Vector3.Distance(pos, hatch) >= MinDeviceDistanceToHatch) break;
                }
                l.RobotSpawns.Add(new RobotSpawn { Kind = kind, Floor = room.Floor, Position = pos, Rotation = rot });
            }

            int cameras = s.Cameras + level * s.CamerasPerHouse;
            var used = new HashSet<int>();
            for (int i = 0; i < cameras && used.Count < candidates.Count; i++)
            {
                var room = candidates[rng.Next(candidates.Count)];
                if (!used.Add(room.Id)) { i--; continue; }
                var (c, inward) = CornerOf(l, room, rng, 0.45f);
                if (room.Floor == 0 && Vector3.Distance(c, l.HatchPosition) < 9f) continue; // not over the team's heads
                var pos = c + Vector3.up * (WallHeight - 0.3f);
                l.CameraSpots.Add(new Pose(pos, Quaternion.LookRotation(inward)));
            }

            int lasers = s.Lasers + level * s.LasersPerHouse;
            while (l.LaserSpots.Count < lasers && doors.Count > 0)
            {
                int idx = rng.Next(doors.Count);
                var spot = doors[idx];
                doors.RemoveAt(idx);
                if ((spot.Position - l.HatchPosition).magnitude < 7.5f) continue; // keep the lobby doorways free
                spot.Kind = (LaserPattern)rng.Next(level >= s.MovingLasersFromHouse ? 3 : 2);
                l.LaserSpots.Add(spot);
            }
            // Sliding doors in some of the remaining doorways (never together with a laser).
            foreach (var d in doors)
                if (rng.NextDouble() < b.AutoDoorChance) l.AutoDoors.Add(new Pose(d.Position, d.Rotation));
            // the paint booth's sliding door (wide enough for a car), never locked
            if (l.BoothDoorway >= 0)
            {
                var bd = l.Doorways[l.BoothDoorway];
                l.BoothDoor = l.AutoDoors.Count;
                l.AutoDoors.Add(new Pose(bd.Position, bd.Rotation));
            }

            // Some of them are locked (own stream: the rest of the house does not shift). Never next to the lobby.
            if (level < b.LockedDoorsFromHouse) return;
            var locks = new System.Random(LevelRandom.Mix(l.Seed, 0x10C4, 0));
            for (int i = 0; i < l.AutoDoors.Count; i++)
            {
                bool lobby = (l.AutoDoors[i].position - l.HatchPosition).magnitude < 7.5f;
                if (locks.NextDouble() < b.LockedDoorChance && !lobby && i != l.BoothDoor) l.LockedDoors.Add(i);
            }
        }

        /// <summary>A room picked with a chance growing with its area (a hall holds more guards than a closet).</summary>
        /// <summary>A room by size (bigger: likelier); with <paramref name="deep"/> &gt; 0 the rooms deep inside count more too.</summary>
        static PlanRoom Weighted(List<PlanRoom> rooms, System.Random rng, LevelLayout l = null, float deep = 0f)
        {
            float Weight(PlanRoom r) => Mathf.Sqrt(Mathf.Max(1f, r.Area)) * (1f + (l != null ? deep * l.DepthShare(r.Id) : 0f));
            float total = 0f;
            foreach (var r in rooms) total += Weight(r);
            float roll = (float)rng.NextDouble() * total;
            foreach (var r in rooms)
            {
                roll -= Weight(r);
                if (roll <= 0f) return r;
            }
            return rooms[rooms.Count - 1];
        }

        /// <summary>A random doorway for a pressure plate (not next to the hatch); it is removed from the list. False if none.</summary>
        static bool TakeDoorway(List<LaserSpot> doors, System.Random rng, Vector3 hatch, out LaserSpot spot)
        {
            spot = default;
            var free = new List<int>();
            for (int i = 0; i < doors.Count; i++)
            {
                var p = doors[i].Position;
                if (LevelLayout.FloorOf(p.y) == 0 && Vector3.Distance(p, hatch) < MinDeviceDistanceToHatch) continue;
                free.Add(i);
            }
            if (free.Count == 0) return false;
            int k = free[rng.Next(free.Count)];
            spot = doors[k];
            doors.RemoveAt(k);
            return true;
        }
    }
}
