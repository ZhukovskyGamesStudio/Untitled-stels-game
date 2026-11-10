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
    public class BuildingSettings
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
        public float VentChance = 0.35f;
        [Tooltip("Chance of a sliding door in a doorway without a laser.")]
        public float AutoDoorChance = 0.45f;
        [Tooltip("Chance that a sliding door is locked: players must hack it (robots have the key).")]
        public float LockedDoorChance = 0.35f;
        [Tooltip("House (0-based) from which doors can be locked.")]
        public int LockedDoorsFromHouse = 1;

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

        [Header("Who")]
        public RosterSettings Roster = new();
    }

    /// <summary>
    /// Room-based multi-storey office generator (pure: seed in, <see cref="LevelLayout"/> out; geometry is built by
    /// <see cref="LevelBuilder"/>). Every floor is a grid of rooms; a randomized spanning tree per floor
    /// makes every room reachable, extra doors create loops, staircases in room corners connect the floors.
    /// </summary>
    public static class LevelGenerator
    {
        static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        // ------------------------------------------------------------------ layout

        /// <summary>Generates a house. Without settings the defaults are used (tests); the game passes LevelConfig.</summary>
        public static LevelLayout Generate(int seed, int levelIndex, BuildingSettings building = null, SecuritySettings security = null, int players = 1)
        {
            building ??= new BuildingSettings();
            security ??= new SecuritySettings();
            var rng = new System.Random(seed);
            var grid = building.GridFor(levelIndex, players);
            var l = new LevelLayout
            {
                Seed = seed,
                LevelIndex = levelIndex,
                Players = Mathf.Max(1, players),
                Width = grid.x,
                Height = grid.y,
                Floors = building.FloorsFor(levelIndex),
            };
            int w = l.Width, h = l.Height, floors = l.Floors;
            l.Rooms = new RoomType[floors, w, h];
            l.EdgesX = new EdgeType[floors, w - 1, h];
            l.EdgesZ = new EdgeType[floors, w, h - 1];
            l.StartCell = new Vector2Int(0, rng.Next(h));

            for (int f = 0; f < floors; f++) GenerateFloorPlan(l, f, rng, building);
            PlaceStairs(l, rng);

            // Exterior doors on the ground floor: a way out into the gas, to hide or to walk around the building.
            for (int i = 0; i < 2; i++)
            {
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    int side = rng.Next(4);
                    Vector2Int cell, dir;
                    switch (side)
                    {
                        case 0: cell = new Vector2Int(rng.Next(w), 0); dir = Vector2Int.down; break;
                        case 1: cell = new Vector2Int(rng.Next(w), h - 1); dir = Vector2Int.up; break;
                        case 2: cell = new Vector2Int(w - 1, rng.Next(h)); dir = Vector2Int.right; break;
                        default: cell = new Vector2Int(0, rng.Next(h)); dir = Vector2Int.left; break;
                    }
                    if (cell == l.StartCell || l.ExteriorDoors.Exists(e => e.cell == cell && e.dir == dir)) continue;
                    l.ExteriorDoors.Add((cell, dir));
                    break;
                }
            }

            for (int f = 0; f < floors; f++)
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        if (f == 0 && new Vector2Int(x, y) == l.StartCell) { l.Rooms[f, x, y] = RoomType.Lobby; continue; }
                        l.Rooms[f, x, y] = building.PickRoom(rng);
                    }

            l.Dark = new bool[floors, w, h];
            l.Flicker = new bool[floors, w, h];
            for (int f = 0; f < floors; f++)
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        if (l.Rooms[f, x, y] == RoomType.Lobby) continue;
                        double r = rng.NextDouble();
                        l.Dark[f, x, y] = r < building.DarkChance + building.DarkChancePerHouse * Mathf.Min(levelIndex, building.MaxDarkHouses);
                        l.Flicker[f, x, y] = !l.Dark[f, x, y] && r > 1.0 - building.FlickerChance;
                    }

            for (int f = 0; f < floors; f++)
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                        FurnishRoom(l, f, x, y, rng);

            PlaceSecurity(l, rng, building, security);

            float height = floors * FloorHeight;
            l.BuildingBounds = new Bounds(new Vector3(w * Cell / 2f, height / 2f, h * Cell / 2f), new Vector3(w * Cell, height, h * Cell));
            return l;
        }

        static void GenerateFloorPlan(LevelLayout l, int f, System.Random rng, BuildingSettings b)
        {
            int w = l.Width, h = l.Height;
            var start = f == 0 ? l.StartCell : new Vector2Int(rng.Next(w), rng.Next(h));
            var visited = new bool[w, h];
            var stack = new Stack<Vector2Int>();
            stack.Push(start);
            visited[start.x, start.y] = true;
            while (stack.Count > 0)
            {
                var cur = stack.Peek();
                var options = new List<Vector2Int>();
                foreach (var d in Dirs)
                {
                    var n = cur + d;
                    if (n.x >= 0 && n.y >= 0 && n.x < w && n.y < h && !visited[n.x, n.y]) options.Add(n);
                }
                if (options.Count == 0) { stack.Pop(); continue; }
                var next = options[rng.Next(options.Count)];
                SetEdge(l, f, cur, next, rng.NextDouble() < b.OpenPassageChance ? EdgeType.Open : EdgeType.Door);
                visited[next.x, next.y] = true;
                stack.Push(next);
            }
            for (int x = 0; x < w - 1; x++)
                for (int y = 0; y < h; y++)
                    if (l.EdgesX[f, x, y] == EdgeType.Wall && rng.NextDouble() < b.ExtraDoorChance) l.EdgesX[f, x, y] = EdgeType.Door;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h - 1; y++)
                    if (l.EdgesZ[f, x, y] == EdgeType.Wall && rng.NextDouble() < b.ExtraDoorChance) l.EdgesZ[f, x, y] = EdgeType.Door;
            // Crawl vents in some of the remaining walls: shortcuts for crouching players only.
            for (int x = 0; x < w - 1; x++)
                for (int y = 0; y < h; y++)
                    if (l.EdgesX[f, x, y] == EdgeType.Wall && rng.NextDouble() < b.VentChance) l.EdgesX[f, x, y] = EdgeType.Vent;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h - 1; y++)
                    if (l.EdgesZ[f, x, y] == EdgeType.Wall && rng.NextDouble() < b.VentChance) l.EdgesZ[f, x, y] = EdgeType.Vent;
        }

        /// <summary>One staircase per pair of floors, each in a different cell (never the lobby).</summary>
        static void PlaceStairs(LevelLayout l, System.Random rng)
        {
            var used = new HashSet<Vector2Int> { l.StartCell };
            for (int f = 0; f < l.Floors - 1; f++)
            {
                Vector2Int cell;
                int guard = 0;
                do cell = new Vector2Int(rng.Next(l.Width), rng.Next(l.Height));
                while (used.Contains(cell) && ++guard < 100);
                used.Add(cell);
                var corner = new Vector2Int(rng.Next(2) == 0 ? -1 : 1, rng.Next(2) == 0 ? -1 : 1);
                l.Stairs.Add(new Stair { Floor = f, Cell = cell, Corner = corner });
            }
        }

        static void SetEdge(LevelLayout l, int f, Vector2Int a, Vector2Int b, EdgeType type)
        {
            if (a.y == b.y) l.EdgesX[f, Mathf.Min(a.x, b.x), a.y] = type;
            else l.EdgesZ[f, a.x, Mathf.Min(a.y, b.y)] = type;
        }

        static void Prop(LevelLayout l, PropKind kind, Vector3 pos, float rotY) =>
            l.Props.Add(new PropPlacement { Kind = kind, Position = pos, RotationY = rotY });

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        // ------------------------------------------------------------------ furniture
        // Layouts follow office planning guides: back-to-back desk clusters with privacy screens and chair push-back,
        // storage along the walls, a kitchenette + tables, a lounge corner. Every room keeps its centre cross free
        // (doors and vents sit in the middle of the walls); the furniture fills the four quadrants. A quadrant is
        // addressed in "outward" coordinates: u along x, v along z, both measured from the room centre towards the
        // quadrant's walls (the quadrant spans 1.1 .. 4.9 m on both axes).

        readonly struct Quad
        {
            public readonly Vector3 Center;
            public readonly int Sx, Sz;
            public Quad(Vector3 center, int sx, int sz) { Center = center; Sx = sx; Sz = sz; }

            public Vector3 P(float u, float v, float y = 0f) => Center + new Vector3(Sx * u, y, Sz * v);
            /// <summary>Yaw that turns a model's front (+Z) outwards along v (towards the quadrant's z wall).</summary>
            public float OutV => Sz > 0 ? 0f : 180f;
            public float InV => Sz > 0 ? 180f : 0f;
            public float OutU => Sx > 0 ? 90f : -90f;
            public float InU => Sx > 0 ? -90f : 90f;
            /// <summary>+1 / -1: the direction along u of a model's local +X when the model is turned by <see cref="InV"/>.</summary>
            public int LocalXAlongU => -Sx * Sz;
        }

        static void FurnishRoom(LevelLayout l, int f, int x, int y, System.Random rng)
        {
            var c = l.CellCenter(f, x, y);
            int blocked = l.StairQuadrant(f, new Vector2Int(x, y));
            l.PatrolPoints.Add(c);
            var room = l.Rooms[f, x, y];
            if (room == RoomType.Lobby) l.HatchPosition = c;

            // Rooms built around their centre first.
            if (room == RoomType.Lobby)
            {
                Prop(l, PropKind.Hatch, c, 0);
                for (int q = 0; q < 4; q++) l.PlayerSpawns.Add(c + Quadrants[q] * 0.45f);
            }
            if (room == RoomType.Meeting) MeetingCentre(l, c, blocked, rng);

            for (int q = 0; q < 4; q++)
            {
                if (q == blocked) continue;
                var quad = new Quad(c, Quadrants[q].x > 0 ? 1 : -1, Quadrants[q].z > 0 ? 1 : -1);
                switch (room)
                {
                    case RoomType.Lobby: LobbyQuadrant(l, quad, q, rng); break;
                    case RoomType.OpenSpace: OpenSpaceQuadrant(l, quad, rng); break;
                    case RoomType.Office: OfficeQuadrant(l, quad, q, rng); break;
                    case RoomType.Meeting: MeetingQuadrant(l, quad, q, rng); break;
                    case RoomType.Kitchen: KitchenQuadrant(l, quad, q, rng); break;
                    case RoomType.Server: ServerQuadrant(l, quad, rng); break;
                    case RoomType.Storage: StorageQuadrant(l, quad, rng); break;
                    case RoomType.Restroom: RestroomQuadrant(l, quad, q, rng); break;
                    case RoomType.Lounge: LoungeQuadrant(l, quad, q, rng); break;
                }
            }
        }

        static void Item(LevelLayout l, Vector3 p) => l.ItemSpots.Add(p);

        /// <summary>Height of a desk's top.</summary>
        const float DeskTop = 0.765f;

        /// <summary>A desk lamp left on (in a dark room it is the light to avoid).</summary>
        static void DeskLamp(LevelLayout l, Vector3 p, float yaw) => Prop(l, PropKind.DeskLamp, p, yaw);

        /// <summary>
        /// Back-to-back cluster of four desks with privacy screens, pushed towards the room's centre cross: the aisle
        /// behind the outer chairs stays about a metre wide. Loot lies on the inner desks, within reach from the cross.
        /// </summary>
        static void DeskCluster(LevelLayout l, Quad q, System.Random rng)
        {
            foreach (float u in new[] { 1.9f, 3.5f })
            {
                Prop(l, PropKind.Desk, q.P(u, 2.85f), q.OutV);
                Prop(l, PropKind.Chair, q.P(u + Range(rng, -0.1f, 0.1f), 3.6f), q.InV + Range(rng, -12f, 12f));
                if (rng.NextDouble() < 0.35) DeskLamp(l, q.P(u + (rng.Next(2) == 0 ? -0.6f : 0.6f), 3.05f, DeskTop), q.InV);
                Prop(l, PropKind.Desk, q.P(u, 2.05f), q.InV);
                Prop(l, PropKind.Chair, q.P(u + Range(rng, -0.1f, 0.1f), 1.35f), q.OutV + Range(rng, -12f, 12f));
                Prop(l, PropKind.Partition, q.P(u, 2.45f), 0f);
                if (rng.NextDouble() < 0.6) Item(l, q.P(u + (rng.Next(2) == 0 ? -0.5f : 0.5f), 1.85f, 0.77f));
            }
            if (rng.NextDouble() < 0.5) Prop(l, PropKind.TrashBin, q.P(4.6f, 1.4f), 0f);
            else Prop(l, PropKind.Cabinet, q.P(4.57f, 3.2f), q.InU);
        }

        /// <summary>Copier, cabinets and a cooler: the "services" corner of an open space.</summary>
        static void ServiceCorner(LevelLayout l, Quad q, System.Random rng)
        {
            Prop(l, PropKind.Printer, q.P(2.0f, 4.45f), q.InV);
            Prop(l, PropKind.Cabinet, q.P(3.0f, 4.55f), q.InV);
            Prop(l, PropKind.Cabinet, q.P(4.0f, 4.55f), q.InV);
            Prop(l, PropKind.WaterCooler, q.P(4.55f, 3.4f), q.InU);
            Prop(l, PropKind.Plant, q.P(4.4f, 1.6f), 0f);
            Prop(l, PropKind.TrashBin, q.P(2.0f, 3.6f), 0f);
            Item(l, q.P(3.0f, 4.55f, 1.4f));
            Item(l, q.P(2.6f, 2.4f));
        }

        static void OpenSpaceQuadrant(LevelLayout l, Quad q, System.Random rng)
        {
            if (rng.NextDouble() < 0.8) DeskCluster(l, q, rng);
            else ServiceCorner(l, q, rng);
        }

        static void LobbyQuadrant(LevelLayout l, Quad q, int index, System.Random rng)
        {
            switch (index)
            {
                case 0:
                    Prop(l, PropKind.ReceptionDesk, q.P(3.0f, 3.2f), q.InV);
                    Prop(l, PropKind.Chair, q.P(3.0f, 3.95f), q.InV);
                    Item(l, q.P(2.6f, 2.87f, 1.14f)); // on the high counter facing the room
                    break;
                case 1:
                    Prop(l, PropKind.Sofa, q.P(3.0f, 4.3f), q.InV);
                    Prop(l, PropKind.CoffeeTable, q.P(3.0f, 3.1f), 0f);
                    Prop(l, PropKind.Plant, q.P(4.5f, 1.6f), 0f);
                    Prop(l, PropKind.FloorLamp, q.P(4.45f, 4.45f), 0f);
                    Item(l, q.P(3.0f, 3.1f, 0.42f));
                    break;
                case 2:
                    Prop(l, PropKind.CoatRack, q.P(4.4f, 4.4f), 0f);
                    Prop(l, PropKind.Lockers, q.P(2.4f, 4.55f), q.InV);
                    Prop(l, PropKind.Lockers, q.P(3.35f, 4.55f), q.InV);
                    Prop(l, PropKind.Plant, q.P(1.6f, 1.6f), 0f);
                    break;
                default:
                    Prop(l, PropKind.VendingMachine, q.P(4.4f, 3.0f), q.InU);
                    Prop(l, PropKind.Armchair, q.P(2.6f, 3.6f), q.InV + 20f);
                    Prop(l, PropKind.Plant, q.P(4.4f, 4.4f), 0f);
                    Item(l, q.P(2.0f, 2.2f));
                    break;
            }
        }

        static void OfficeQuadrant(LevelLayout l, Quad q, int index, System.Random rng)
        {
            switch ((index + l.Seed) & 3)
            {
                case 0: // manager's desk facing the room, bookcase behind
                    Prop(l, PropKind.Desk, q.P(2.8f, 3.1f), q.InV);
                    Prop(l, PropKind.Chair, q.P(2.8f, 3.9f), q.InV);
                    DeskLamp(l, q.P(3.42f, 3.25f, DeskTop), q.InV);
                    Prop(l, PropKind.Bookshelf, q.P(4.35f, 4.65f), q.InV);
                    Prop(l, PropKind.Chair, q.P(2.5f, 2.0f), q.OutV);
                    Item(l, q.P(2.5f, 3.2f, 0.77f));
                    break;
                case 1: // bookcases along the wall
                    Prop(l, PropKind.Bookshelf, q.P(1.8f, 4.65f), q.InV);
                    Prop(l, PropKind.Bookshelf, q.P(2.85f, 4.65f), q.InV);
                    Prop(l, PropKind.Safe, q.P(4.0f, 4.4f), q.InV);
                    Prop(l, PropKind.Plant, q.P(4.4f, 2.0f), 0f);
                    Item(l, q.P(4.0f, 4.4f, 1.02f)); // on the safe
                    break;
                case 2: // guest corner
                    Prop(l, PropKind.Armchair, q.P(2.2f, 3.8f), q.InV);
                    Prop(l, PropKind.Armchair, q.P(3.8f, 2.4f), q.InU);
                    Prop(l, PropKind.CoffeeTable, q.P(2.6f, 2.5f), 0f);
                    Prop(l, PropKind.FloorLamp, q.P(1.5f, 4.5f), 0f);
                    Prop(l, PropKind.Plant, q.P(4.4f, 4.4f), 0f);
                    Item(l, q.P(2.6f, 2.5f, 0.42f));
                    break;
                default: // files and the copier
                    Prop(l, PropKind.Cabinet, q.P(4.55f, 2.0f), q.InU);
                    Prop(l, PropKind.Cabinet, q.P(4.55f, 3.0f), q.InU);
                    Prop(l, PropKind.Printer, q.P(4.5f, 4.3f), q.InU);
                    Prop(l, PropKind.CoatRack, q.P(1.6f, 4.4f), 0f);
                    Prop(l, PropKind.TrashBin, q.P(2.6f, 4.55f), 0f);
                    Item(l, q.P(4.55f, 3.0f, 1.4f));
                    break;
            }
        }

        static void MeetingCentre(LevelLayout l, Vector3 c, int blocked, System.Random rng)
        {
            Prop(l, PropKind.MeetingTable, c, 0);
            for (int i = 0; i < 3; i++)
            {
                int sx = i - 1; // -1, 0, 1
                float px = sx * 1.1f;
                // Corner chairs would touch a staircase in that corner.
                if (sx == 0 || LevelLayout.QuadrantOf(new Vector2Int(sx, 1)) != blocked) Prop(l, PropKind.Chair, c + new Vector3(px, 0, 1.05f), 180f);
                if (sx == 0 || LevelLayout.QuadrantOf(new Vector2Int(sx, -1)) != blocked) Prop(l, PropKind.Chair, c + new Vector3(px, 0, -1.05f), 0f);
            }
            l.ItemSpots.Add(c + new Vector3(Range(rng, -1.2f, 1.2f), 0.78f, Range(rng, -0.3f, 0.3f)));
        }

        static void MeetingQuadrant(LevelLayout l, Quad q, int index, System.Random rng)
        {
            switch (index)
            {
                case 0: Prop(l, PropKind.TvStand, q.P(2.6f, 4.55f), q.InV); Prop(l, PropKind.Plant, q.P(4.4f, 4.4f), 0f); break;
                case 1: Prop(l, PropKind.Whiteboard, q.P(2.6f, 4.75f), q.InV); Prop(l, PropKind.Chair, q.P(4.3f, 3.2f), q.InU); break;
                case 2: Prop(l, PropKind.Cabinet, q.P(2.4f, 4.55f), q.InV); Prop(l, PropKind.Cabinet, q.P(3.4f, 4.55f), q.InV); Item(l, q.P(2.4f, 4.55f, 1.4f)); break;
                default: Prop(l, PropKind.WaterCooler, q.P(4.55f, 4.45f), q.InU); Prop(l, PropKind.Plant, q.P(1.6f, 4.4f), 0f); Prop(l, PropKind.Chair, q.P(4.3f, 2.2f), q.InU); break;
            }
        }

        /// <summary>Counters with a sink and a coffee machine along the wall, a fridge; tables with chairs.</summary>
        static void KitchenQuadrant(LevelLayout l, Quad q, int index, System.Random rng)
        {
            if (index < 2)
            {
                Prop(l, PropKind.KitchenCounter, q.P(2.6f, 4.45f), q.InV);
                Prop(l, PropKind.CoffeeMachine, q.P(2.6f + 0.75f * q.LocalXAlongU, 4.5f, 0.93f), q.InV); // the microwave sits at local -X
                Prop(l, PropKind.Fridge, q.P(4.35f, 4.45f), q.InV);
                Prop(l, PropKind.TrashBin, q.P(4.6f, 3.6f), 0f);
                Prop(l, PropKind.RoundTable, q.P(2.8f, 2.4f), 0f);
                Prop(l, PropKind.Chair, q.P(2.8f, 1.55f), q.OutV);
                Prop(l, PropKind.Chair, q.P(3.75f, 2.4f), q.InU);
                Item(l, q.P(2.6f, 4.45f, 0.93f));
                Item(l, q.P(2.8f, 2.4f, 0.76f));
            }
            else
            {
                Prop(l, PropKind.VendingMachine, q.P(2.4f, 4.4f), q.InV);
                Prop(l, PropKind.WaterCooler, q.P(3.6f, 4.6f), q.InV);
                Prop(l, PropKind.RoundTable, q.P(2.8f, 2.6f), 0f);
                Prop(l, PropKind.Chair, q.P(2.8f, 1.7f), q.OutV);
                Prop(l, PropKind.Chair, q.P(3.7f, 2.6f), q.InU);
                Prop(l, PropKind.Chair, q.P(1.9f, 2.6f), q.OutU);
                Item(l, q.P(2.8f, 2.6f, 0.76f));
            }
        }

        /// <summary>Two rows of racks with a cold aisle between them.</summary>
        static void ServerQuadrant(LevelLayout l, Quad q, System.Random rng)
        {
            foreach (float u in new[] { 1.85f, 2.6f, 3.35f })
            {
                Prop(l, PropKind.ServerRack, q.P(u, 4.25f), q.InV);
                Prop(l, PropKind.ServerRack, q.P(u, 1.75f), q.OutV);
            }
            Prop(l, PropKind.Cabinet, q.P(4.55f, 3.0f), q.InU);
            Item(l, q.P(Range(rng, 1.6f, 2.8f), 3.0f));
        }

        /// <summary>Shelving rows, pallets and boxes; loot on the shelves at three heights.</summary>
        static void StorageQuadrant(LevelLayout l, Quad q, System.Random rng)
        {
            var levels = new[] { 0.47f, 1.07f, 1.67f };
            Prop(l, PropKind.Shelf, q.P(2.6f, 4.5f), q.InV);
            if (rng.NextDouble() < 0.7) Item(l, q.P(2.6f + Range(rng, -0.6f, 0.6f), 4.5f, levels[rng.Next(3)]));
            if (rng.NextDouble() < 0.5)
            {
                Prop(l, PropKind.Shelf, q.P(2.25f, 2.4f), q.InV);
                if (rng.NextDouble() < 0.7) Item(l, q.P(2.25f + Range(rng, -0.6f, 0.6f), 2.4f, levels[rng.Next(3)]));
            }
            else
            {
                Prop(l, PropKind.Pallet, q.P(2.4f, 2.4f), rng.Next(2) * 90f);
                Item(l, q.P(3.7f, 3.3f));
            }
            Prop(l, PropKind.Boxes, q.P(4.05f, 1.95f), rng.Next(4) * 90f); // reaches 0.78 m from its origin
            Prop(l, PropKind.Lockers, q.P(4.55f, 3.6f), q.InU);
        }

        static void RestroomQuadrant(LevelLayout l, Quad q, int index, System.Random rng)
        {
            if (index < 2)
            {
                // A row of cubicles along the wall.
                foreach (float u in new[] { 1.65f, 2.7f, 3.75f }) Prop(l, PropKind.ToiletStall, q.P(u, 4.05f), q.InV);
                Item(l, q.P(2.7f, 3.0f));
            }
            else
            {
                Prop(l, PropKind.WashBasin, q.P(2.8f, 4.55f), q.InV);
                Prop(l, PropKind.TrashBin, q.P(4.4f, 4.5f), 0f);
                if (rng.NextDouble() < 0.5) Item(l, q.P(2.4f, 4.5f, 0.88f));
            }
        }

        static void LoungeQuadrant(LevelLayout l, Quad q, int index, System.Random rng)
        {
            switch (index)
            {
                case 0:
                    Prop(l, PropKind.Sofa, q.P(2.9f, 4.3f), q.InV);
                    Prop(l, PropKind.CoffeeTable, q.P(2.9f, 3.1f), 0f);
                    Prop(l, PropKind.Armchair, q.P(4.3f, 2.4f), q.InU);
                    Prop(l, PropKind.FloorLamp, q.P(4.45f, 4.45f), 0f);
                    Item(l, q.P(2.9f, 4.3f, 0.46f));
                    Item(l, q.P(2.9f, 3.1f, 0.42f));
                    break;
                case 1:
                    Prop(l, PropKind.TvStand, q.P(2.8f, 4.55f), q.InV);
                    Prop(l, PropKind.Armchair, q.P(2.2f, 2.6f), q.OutV);
                    Prop(l, PropKind.Armchair, q.P(3.4f, 2.6f), q.OutV);
                    Prop(l, PropKind.Plant, q.P(4.4f, 4.4f), 0f);
                    break;
                case 2: // games corner: table tennis (you can stand on it) and a bean bag
                    Prop(l, PropKind.PingPongTable, q.P(3.0f, 3.3f), 0f);
                    Prop(l, PropKind.BeanBag, q.P(1.7f, 1.7f), 0f);
                    Item(l, q.P(3.6f, 3.3f, 0.78f));
                    break;
                default:
                    Prop(l, PropKind.VendingMachine, q.P(4.4f, 3.0f), q.InU);
                    Prop(l, PropKind.WaterCooler, q.P(4.6f, 4.4f), q.InU);
                    Prop(l, PropKind.RoundTable, q.P(2.5f, 2.6f), 0f);
                    Prop(l, PropKind.Chair, q.P(2.5f, 3.5f), q.InV);
                    Prop(l, PropKind.ArcadeMachine, q.P(2.2f, 4.35f), q.InV);
                    Item(l, q.P(2.5f, 2.6f, 0.76f));
                    break;
            }
        }

        /// <summary>Fixed devices keep this far from the hatch on the ground floor (players start there).</summary>
        const float MinDeviceDistanceToHatch = 12.5f;

        /// <summary>Where a fixed device goes: plates in a room's walkway, motion sensors high in a corner, lidar poles in the middle.</summary>
        static (Vector3Int cell, Vector3 pos, Quaternion rot) DeviceSpot(LevelLayout l, List<Vector3Int> candidates, RobotKind kind, System.Random rng)
        {
            var cell = candidates[rng.Next(candidates.Count)];
            var c = l.CellCenter(cell.z, cell.x, cell.y);
            Vector3 pos;
            Quaternion rot;
            switch (kind)
            {
                case RobotKind.MotionSensor:
                {
                    var corner = new Vector3(rng.Next(2) == 0 ? -1 : 1, 0, rng.Next(2) == 0 ? -1 : 1);
                    pos = c + corner * (Cell / 2f - 0.2f) + Vector3.up * 2.2f;
                    rot = Quaternion.LookRotation(-corner.normalized);
                    break;
                }
                case RobotKind.PressurePlate:
                {
                    var dirs = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
                    pos = c + dirs[rng.Next(4)] * Range(rng, 2f, 3.5f);
                    rot = Quaternion.identity;
                    break;
                }
                default:
                    pos = c;
                    rot = Quaternion.identity;
                    break;
            }
            return (cell, pos, rot);
        }

        static void PlaceSecurity(LevelLayout l, System.Random rng, BuildingSettings b, SecuritySettings s)
        {
            int level = l.LevelIndex;
            var candidates = new List<Vector3Int>(); // x, y, floor
            for (int f = 0; f < l.Floors; f++)
                for (int x = 0; x < l.Width; x++)
                    for (int y = 0; y < l.Height; y++)
                    {
                        int dist = Mathf.Abs(x - l.StartCell.x) + Mathf.Abs(y - l.StartCell.y) + f * 2;
                        if (dist >= 2) candidates.Add(new Vector3Int(x, y, f));
                    }

            // A bigger team gets a bigger house — and the guards to go with it.
            var solo = b.GridFor(level, 1);
            int teamRooms = Mathf.Max(0, l.Width * l.Height - solo.x * solo.y) * l.Floors;
            int robots = s.Robots + level * s.RobotsPerHouse + (l.Floors - 1) * s.RobotsPerExtraFloor + teamRooms / Mathf.Max(1, s.TeamRoomsPerExtraRobot);
            var freshRobots = RobotRoster.Newcomers(s.Roster.Robots, level);
            var freshDevices = RobotRoster.Newcomers(s.Roster.Devices, level);
            for (int i = 0; i < robots; i++)
            {
                // Make sure every floor gets at least one robot, then spread randomly.
                Vector3Int cell;
                if (i < l.Floors)
                {
                    var onFloor = candidates.FindAll(c => c.z == i);
                    cell = onFloor.Count > 0 ? onFloor[rng.Next(onFloor.Count)] : candidates[rng.Next(candidates.Count)];
                }
                else cell = candidates[rng.Next(candidates.Count)];
                var pos = l.CellCenter(cell.z, cell.x, cell.y) + new Vector3(Range(rng, -1f, 1f), 0, Range(rng, -1f, 1f));
                var kind = RobotRoster.PickRobot(level, rng, s.Roster);
                if (i < freshRobots.Count) kind = freshRobots[i]; // the house that introduces a kind shows it
                l.RobotSpawns.Add(new RobotSpawn { Kind = kind, Floor = cell.z, Position = pos, Rotation = Quaternion.Euler(0, rng.Next(360), 0) });
            }

            // Fixed security devices: pressure plates in the room's walkways, motion sensors on walls, lidar poles.
            int devices = s.Devices + level * s.DevicesPerHouse + teamRooms / Mathf.Max(1, s.TeamRoomsPerExtraDevice);
            var hatch = l.CellCenter(0, l.StartCell.x, l.StartCell.y);
            for (int i = 0; i < devices; i++)
            {
                var kind = RobotRoster.PickDevice(level, rng, s.Roster);
                if (i < freshDevices.Count) kind = freshDevices[i];
                Vector3Int cell;
                Vector3 pos;
                Quaternion rot;
                int tries = 0;
                // A device in the corner of a room diagonal to the lobby could end up right next to the hatch: try again.
                do (cell, pos, rot) = DeviceSpot(l, candidates, kind, rng);
                while (cell.z == 0 && Vector3.Distance(pos, hatch) < MinDeviceDistanceToHatch && ++tries < 20);
                l.RobotSpawns.Add(new RobotSpawn { Kind = kind, Floor = cell.z, Position = pos, Rotation = rot });
            }

            int cameras = s.Cameras + level * s.CamerasPerHouse;
            var used = new HashSet<Vector3Int>();
            for (int i = 0; i < cameras && used.Count < candidates.Count; i++)
            {
                var cell = candidates[rng.Next(candidates.Count)];
                if (!used.Add(cell)) { i--; continue; }
                var c = l.CellCenter(cell.z, cell.x, cell.y);
                var corner = new Vector3(rng.Next(2) == 0 ? -1 : 1, 0, rng.Next(2) == 0 ? -1 : 1);
                var pos = c + corner * (Cell / 2f - 0.45f) + Vector3.up * (WallHeight - 0.3f);
                l.CameraSpots.Add(new Pose(pos, Quaternion.LookRotation(-corner.normalized)));
            }

            var doors = new List<LaserSpot>();
            for (int f = 0; f < l.Floors; f++)
            {
                float fy = LevelLayout.FloorY(f);
                for (int x = 0; x < l.Width - 1; x++)
                    for (int y = 0; y < l.Height; y++)
                        if (l.EdgesX[f, x, y] == EdgeType.Door)
                            doors.Add(new LaserSpot { Position = new Vector3((x + 1) * Cell, fy, (y + 0.5f) * Cell), Rotation = Quaternion.Euler(0, 90, 0), Width = ClearWidth(DoorWidth) });
                for (int x = 0; x < l.Width; x++)
                    for (int y = 0; y < l.Height - 1; y++)
                        if (l.EdgesZ[f, x, y] == EdgeType.Door)
                            doors.Add(new LaserSpot { Position = new Vector3((x + 0.5f) * Cell, fy, (y + 1) * Cell), Rotation = Quaternion.identity, Width = ClearWidth(DoorWidth) });
            }
            int lasers = s.Lasers + level * s.LasersPerHouse;
            while (l.LaserSpots.Count < lasers && doors.Count > 0)
            {
                int idx = rng.Next(doors.Count);
                var spot = doors[idx];
                doors.RemoveAt(idx);
                if ((spot.Position - l.HatchPosition).magnitude < Cell * 0.75f) continue; // keep the lobby doorways free
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
                bool lobby = (l.AutoDoors[i].position - l.HatchPosition).magnitude < Cell * 0.75f;
                if (locks.NextDouble() < b.LockedDoorChance && !lobby) l.LockedDoors.Add(i);
            }
        }
    }
}
