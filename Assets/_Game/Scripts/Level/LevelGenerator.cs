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
        [Tooltip("Rooms per floor in the first house (9 = 3 x 3).")]
        public float BaseRooms = 9f;
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
        };

        [Header("Light")]
        [Tooltip("Chance of a dark room in the first house.")]
        public float DarkChance = 0.15f;
        [Tooltip("Added per house, up to MaxDarkHouses houses.")]
        public float DarkChancePerHouse = 0.03f;
        public int MaxDarkHouses = 5;
        [Tooltip("Chance of a flickering lamp in a lit room.")]
        public float FlickerChance = 0.12f;

        [Header("Passages")]
        [Tooltip("Share of the main passages that are open arches instead of doors.")]
        public float OpenPassageChance = 0.3f;
        [Tooltip("Chance of an extra door in a remaining wall (loops).")]
        public float ExtraDoorChance = 0.2f;
        [Tooltip("Chance of a crawl vent in a remaining wall.")]
        public float VentChance = 0.45f;
        [Tooltip("Share of the vents that sit up under the ceiling, climbed to by crates stacked under them on both sides.")]
        public float HighVentChance = 0.4f;
        [Tooltip("Chance of a narrow slot (players squeeze through, robots do not) in a wall still left.")]
        public float SqueezeChance = 0.3f;
        [Header("Shapes")]
        [Tooltip("Chance per big room (lobby, open space, lounge, storage, meeting) to rise through two or three storeys: an atrium with galleries above.")]
        public float AtriumChance = 0.35f;
        [Tooltip("Chance of a near-square room becoming a rotunda or an octagon (doubled for meeting rooms, lobbies, lounges, halls).")]
        public float RotundaChance = 0.07f;
        [Tooltip("Chance of a room getting some of its corners cut or rounded.")]
        public float CornerCutChance = 0.3f;
        [Tooltip("Chance of a wing getting a corridor spine (otherwise its rooms open into each other).")]
        public float CorridorChance = 0.82f;
        [Tooltip("Room sizes: 1 = the usual mix of closets, offices and halls; more = bigger rooms.")]
        public float RoomSizeScale = 1f;

        [Tooltip("Chance of a sliding door in a doorway without a laser.")]
        public float AutoDoorChance = 0.45f;
        [Tooltip("Chance that a sliding door is locked: players must hack it (robots have the key).")]
        public float LockedDoorChance = 0.35f;
        [Tooltip("House (0-based) from which doors can be locked.")]
        public int LockedDoorsFromHouse = 1;

        public object Clone() => MemberwiseClone();

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
        [Header("Counts: base + per house")]
        public int Robots = 2;
        public int RobotsPerHouse = 1;
        [Tooltip("Extra robots per floor above the ground floor.")]
        public int RobotsPerExtraFloor = 1;
        public int Devices = 1;
        public int DevicesPerHouse = 1;
        public int Cameras = 2;
        public int CamerasPerHouse = 1;
        public int Lasers = 1;
        public int LasersPerHouse = 1;
        [Tooltip("A bigger team gets a bigger house: one more robot per this many rooms it adds (all floors).")]
        public int TeamRoomsPerExtraRobot = 10;
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
        public static LevelLayout Generate(int seed, int levelIndex, BuildingSettings building = null, SecuritySettings security = null, int players = 1)
        {
            building ??= new BuildingSettings();
            security ??= new SecuritySettings();
            var kind = HouseKinds.Of(seed, levelIndex);
            building = HouseKinds.Apply(kind, building);
            var rng = new System.Random(seed);
            int floors = building.FloorsFor(levelIndex);
            float area = building.RoomsFor(levelIndex, players) * 100f;
            // Generate and check: a plan where some room cannot be walked to (a doorway that did not fit, a sealed
            // corner) is thrown away and the next variant of the same house is drawn (deterministic: attempt by attempt).
            LevelLayout l = null;
            Furnisher checker = null;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int attempt = 0; attempt < 16; attempt++)
            {
                int planSeed = attempt == 0 ? seed : LevelRandom.Mix(seed, 0xBAD, attempt);
                var o = PlanGenerator.Generate(planSeed, levelIndex, floors, area, building);
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
                l.HatchPosition = l.Lobby.Center;
                long planMs = watch.ElapsedMilliseconds;
                checker = o.Unreached == 0 ? new Furnisher(l, new System.Random(0)) : null;
                bool ok = checker != null && checker.Unreached().Count == 0;
                Timing.Plan += planMs; Timing.Check += watch.ElapsedMilliseconds - planMs;
                watch.Restart();
                if (ok) break;
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
            furnisher.PlaceVentDesks();
            for (int q = 0; q < 4; q++) l.PlayerSpawns.Add(l.HatchPosition + Quadrants[q] * 0.45f);

            // patrol points (room centres and the middles of big rooms) stay free of furniture
            Patrols(l);
            foreach (var p in l.PatrolPoints) furnisher.Reserve(LevelLayout.FloorOf(p.y), p, 0.75f);
            watch.Restart();
            foreach (var r in l.Rooms) furnisher.FurnishRoom(r);
            Timing.Furnish += watch.ElapsedMilliseconds;
            watch.Restart();
            furnisher.EnsureWalkable();
            Timing.Walk += watch.ElapsedMilliseconds;
            PlaceSecurity(l, rng, building, security);
            PlaceTerminals(l, security);

            float height = floors * FloorHeight;
            l.BuildingBounds = new Bounds(new Vector3(l.SizeX / 2f, height / 2f, l.SizeZ / 2f), new Vector3(l.SizeX, height, l.SizeZ));
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

        /// <summary>Patrol points: the middle of every room, and in big rooms the middles of the parts they were made of.</summary>
        static void Patrols(LevelLayout l)
        {
            foreach (var r in l.Rooms)
            {
                l.PatrolPoints.Add(r.Center);
                if (r.Area < 60f) continue;
                foreach (var rect in r.Rects)
                {
                    var c = new Vector2((rect.xMin + rect.xMax) * HousePlan.Res / 2f, (rect.yMin + rect.yMax) * HousePlan.Res / 2f);
                    var p = new Vector3(c.x, LevelLayout.FloorY(r.Floor), c.y);
                    if ((p - r.Center).magnitude > 4f && l.Plan.RoomAt(p) == r && !l.NearStair(r.Floor, p, 0.8f)) l.PatrolPoints.Add(p);
                }
            }
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
        static void PlaceTerminals(LevelLayout l, SecuritySettings s)
        {
            var desks = new List<int>();
            for (int i = 0; i < l.Props.Count; i++)
                if (l.Props[i].Kind == PropKind.Desk && (l.Props[i].Position - l.HatchPosition).magnitude > 7.5f) desks.Add(i);
            var rng = new System.Random(LevelRandom.Mix(l.Seed, 0x7E41, 0));
            var want = new List<TerminalKind>();
            for (int i = 0; i < (l.CameraSpots.Count > 0 ? s.CameraTerminals : 0); i++) want.Add(TerminalKind.Cameras);
            for (int i = 0; i < s.HackTerminals; i++) want.Add(TerminalKind.Hacking);
            foreach (var kind in want)
            {
                if (desks.Count == 0) return;
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
        }

        static void PlaceSecurity(LevelLayout l, System.Random rng, BuildingSettings b, SecuritySettings s)
        {
            int level = l.LevelIndex;
            // Rooms for guards and devices: not the lobby nor right next to it.
            var candidates = new List<PlanRoom>();
            foreach (var r in l.Rooms)
                if (r.Id != l.LobbyRoom && (r.Floor > 0 || (r.Depth >= 2 && Vector3.Distance(r.Center, l.HatchPosition) > MinDeviceDistanceToHatch + 1f)))
                    candidates.Add(r);
            if (candidates.Count == 0) foreach (var r in l.Rooms) if (r.Id != l.LobbyRoom) candidates.Add(r);

            // A bigger team gets a bigger house — and the guards to go with it.
            float solo = b.RoomsFor(level, 1) * l.Floors;
            int teamRooms = Mathf.Max(0, Mathf.RoundToInt(l.RoomUnits - solo));
            int robots = s.Robots + level * s.RobotsPerHouse + (l.Floors - 1) * s.RobotsPerExtraFloor + teamRooms / Mathf.Max(1, s.TeamRoomsPerExtraRobot);
            var freshRobots = RobotRoster.Newcomers(s.Roster.Robots, level);
            var freshDevices = RobotRoster.Newcomers(s.Roster.Devices, level);
            for (int i = 0; i < robots; i++)
            {
                // Every floor gets at least one robot, then they spread (bigger rooms more likely).
                PlanRoom room;
                if (i < l.Floors)
                {
                    var onFloor = candidates.FindAll(c => c.Floor == i);
                    room = onFloor.Count > 0 ? onFloor[rng.Next(onFloor.Count)] : candidates[rng.Next(candidates.Count)];
                }
                else room = Weighted(candidates, rng);
                var pos = SpotIn(l, room, rng, 1.2f);
                if (room.Floor == 0 && Vector3.Distance(pos, l.HatchPosition) < MinDeviceDistanceToHatch) pos = room.Center;
                var kind = RobotRoster.PickRobot(level, rng, s.Roster);
                if (i < freshRobots.Count) kind = freshRobots[i]; // the house that introduces a kind shows it
                l.RobotSpawns.Add(new RobotSpawn { Kind = kind, Floor = room.Floor, Position = pos, Rotation = Quaternion.Euler(0, rng.Next(360), 0) });
            }

            // Doorways between rooms: pressure plates lie in some of them, lasers guard others.
            var doors = new List<LaserSpot>();
            foreach (var d in l.Doorways)
                if (d.Kind == EdgeType.Door && !d.Exterior)
                    doors.Add(new LaserSpot { Position = d.Position, Rotation = d.Rotation, Width = ClearWidth(DoorWidth) });
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

            // Some of them are locked (own stream: the rest of the house does not shift). Never next to the lobby.
            if (level < b.LockedDoorsFromHouse) return;
            var locks = new System.Random(LevelRandom.Mix(l.Seed, 0x10C4, 0));
            for (int i = 0; i < l.AutoDoors.Count; i++)
            {
                bool lobby = (l.AutoDoors[i].position - l.HatchPosition).magnitude < 7.5f;
                if (locks.NextDouble() < b.LockedDoorChance && !lobby) l.LockedDoors.Add(i);
            }
        }

        /// <summary>A room picked with a chance growing with its area (a hall holds more guards than a closet).</summary>
        static PlanRoom Weighted(List<PlanRoom> rooms, System.Random rng)
        {
            float total = 0f;
            foreach (var r in rooms) total += Mathf.Sqrt(Mathf.Max(1f, r.Area));
            float roll = (float)rng.NextDouble() * total;
            foreach (var r in rooms)
            {
                roll -= Mathf.Sqrt(Mathf.Max(1f, r.Area));
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
