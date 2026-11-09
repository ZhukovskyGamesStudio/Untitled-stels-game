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
        public int BaseWidth = 3;
        [Tooltip("+1 room in width per house, up to this.")]
        public int MaxExtraWidth = 2;
        public int BaseDepth = 3;
        [Tooltip("+1 room in depth every this many houses…")]
        public int HousesPerExtraDepth = 3;
        [Tooltip("…up to this.")]
        public int MaxExtraDepth = 1;

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

        public int FloorsFor(int house) => FloorsPerHouse.Length == 0 ? 2 : FloorsPerHouse[Mathf.Clamp(house, 0, FloorsPerHouse.Length - 1)];
        public int WidthFor(int house) => BaseWidth + Mathf.Min(house, MaxExtraWidth);
        public int DepthFor(int house) => BaseDepth + Mathf.Min(house / Mathf.Max(1, HousesPerExtraDepth), MaxExtraDepth);

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
        public static LevelLayout Generate(int seed, int levelIndex, BuildingSettings building = null, SecuritySettings security = null)
        {
            building ??= new BuildingSettings();
            security ??= new SecuritySettings();
            var rng = new System.Random(seed);
            var l = new LevelLayout
            {
                Seed = seed,
                LevelIndex = levelIndex,
                Width = building.WidthFor(levelIndex),
                Height = building.DepthFor(levelIndex),
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

        static void FurnishRoom(LevelLayout l, int f, int x, int y, System.Random rng)
        {
            var c = l.CellCenter(f, x, y);
            var quads = Quadrants;
            int blocked = l.StairQuadrant(f, new Vector2Int(x, y));
            bool Free(int q) => q != blocked;
            l.PatrolPoints.Add(c);

            switch (l.Rooms[f, x, y])
            {
                case RoomType.Lobby:
                    l.HatchPosition = c;
                    Prop(l, PropKind.Hatch, c, 0);
                    for (int q = 0; q < 4; q++)
                    {
                        Prop(l, PropKind.Plant, c + quads[q] * 1.45f, 0);
                        l.PlayerSpawns.Add(c + quads[q] * 0.45f);
                    }
                    break;

                case RoomType.OpenSpace:
                    for (int q = 0; q < 4; q++)
                    {
                        if (!Free(q)) continue;
                        if (rng.NextDouble() > 0.85) { l.ItemSpots.Add(c + quads[q]); continue; }
                        float rot = rng.Next(2) * 90f;
                        var p = c + quads[q];
                        Prop(l, PropKind.Desk, p, rot);
                        Prop(l, PropKind.Chair, p + Quaternion.Euler(0, rot, 0) * new Vector3(0, 0, 0.75f), rot + 180f);
                        l.ItemSpots.Add(p + Quaternion.Euler(0, rot, 0) * new Vector3(Range(rng, -0.5f, 0.5f), 0.77f, 0.1f));
                    }
                    break;

                case RoomType.Office:
                {
                    int deskQ = rng.Next(4);
                    if (!Free(deskQ)) deskQ = (deskQ + 1) % 4;
                    var p = c + quads[deskQ];
                    float rot = rng.Next(4) * 90f;
                    Prop(l, PropKind.Desk, p, rot);
                    Prop(l, PropKind.Chair, p + Quaternion.Euler(0, rot, 0) * new Vector3(0, 0, 0.75f), rot + 180f);
                    l.ItemSpots.Add(p + new Vector3(0, 0.77f, 0));
                    if (Free((deskQ + 1) % 4))
                    {
                        var cab = c + quads[(deskQ + 1) % 4] * 1.4f;
                        Prop(l, PropKind.Cabinet, cab, rng.Next(4) * 90f);
                        l.ItemSpots.Add(cab + new Vector3(0, 1.4f, 0));
                    }
                    if (Free((deskQ + 2) % 4)) Prop(l, PropKind.Plant, c + quads[(deskQ + 2) % 4] * 1.5f, 0);
                    if (Free((deskQ + 3) % 4)) l.ItemSpots.Add(c + quads[(deskQ + 3) % 4]);
                    break;
                }

                case RoomType.Meeting:
                {
                    Prop(l, PropKind.MeetingTable, c, 0);
                    for (int i = 0; i < 3; i++)
                    {
                        int sx = i - 1; // -1, 0, 1
                        float px = sx * 1.1f;
                        // Corner chairs would touch a staircase in that corner.
                        if (sx == 0 || Free(LevelLayout.QuadrantOf(new Vector2Int(sx, 1)))) Prop(l, PropKind.Chair, c + new Vector3(px, 0, 1.05f), 180f);
                        if (sx == 0 || Free(LevelLayout.QuadrantOf(new Vector2Int(sx, -1)))) Prop(l, PropKind.Chair, c + new Vector3(px, 0, -1.05f), 0f);
                    }
                    int pq = rng.Next(4);
                    if (Free(pq)) Prop(l, PropKind.Plant, c + quads[pq] * 1.5f, 0);
                    // Whiteboard on the wall beside the doorway (never in the door, never at the stairs).
                    float wbSide = rng.Next(2) == 0 ? 1f : -1f;
                    if (Free(LevelLayout.QuadrantOf(new Vector2Int(wbSide > 0 ? 1 : -1, 1))))
                        Prop(l, PropKind.Whiteboard, c + new Vector3(wbSide * 2.6f, 0, 4.82f), 180f);
                    l.ItemSpots.Add(c + new Vector3(Range(rng, -1.2f, 1.2f), 0.78f, Range(rng, -0.3f, 0.3f)));
                    int iq = rng.Next(4);
                    if (Free(iq)) l.ItemSpots.Add(c + quads[iq]);
                    break;
                }

                case RoomType.Kitchen:
                {
                    float side = rng.Next(2) == 0 ? 1f : -1f;
                    for (int i = -1; i <= 1; i += 2)
                    {
                        int q = LevelLayout.QuadrantOf(new Vector2Int(i, (int)side));
                        if (!Free(q)) continue;
                        var p = c + new Vector3(i * 2.9f, 0, side * 4.45f);
                        Prop(l, PropKind.KitchenCounter, p, side > 0 ? 180f : 0f);
                        l.ItemSpots.Add(p + new Vector3(Range(rng, -0.8f, 0.4f), 0.93f, 0));
                    }
                    int iq = rng.Next(4);
                    if (Free(iq)) l.ItemSpots.Add(c + quads[iq]);
                    else l.ItemSpots.Add(c + new Vector3(Range(rng, -0.9f, 0.9f), 0, Range(rng, -0.9f, 0.9f)));
                    break;
                }

                case RoomType.Storage:
                    // Racks with goods: loot sits on the shelves at three heights.
                    for (int q = 0; q < 4; q++)
                    {
                        if (!Free(q)) continue;
                        var p = c + quads[q] * 1.25f;
                        Prop(l, PropKind.Shelf, p, 0f);
                        if (rng.NextDouble() < 0.7)
                            l.ItemSpots.Add(p + new Vector3(Range(rng, -0.6f, 0.6f), new[] { 0.47f, 1.07f, 1.67f }[rng.Next(3)], 0f));
                        Prop(l, PropKind.Boxes, c + quads[q] * 0.55f, rng.Next(4) * 90f);
                    }
                    break;

                case RoomType.Restroom:
                {
                    // A row of cubicles along one wall, on a side without stairs.
                    float side = Free(LevelLayout.QuadrantOf(new Vector2Int(1, 1))) && Free(LevelLayout.QuadrantOf(new Vector2Int(-1, 1))) ? 1f : -1f;
                    float zWall = side * 4.1f;
                    for (int i = 0; i < 3; i++)
                    {
                        float px = 1.6f + i * 1.05f;
                        float sx = rng.Next(2) == 0 ? 1f : -1f;
                        if (!Free(LevelLayout.QuadrantOf(new Vector2Int((int)sx, (int)side)))) sx = -sx;
                        if (!Free(LevelLayout.QuadrantOf(new Vector2Int((int)sx, (int)side)))) continue;
                        Prop(l, PropKind.ToiletStall, c + new Vector3(sx * px, 0, zWall), side > 0 ? 180f : 0f);
                        if (i == 1) l.ItemSpots.Add(c + new Vector3(sx * px, 0, zWall - side * 0.4f));
                    }
                    break;
                }

                case RoomType.Lounge:
                {
                    int sofaQ = rng.Next(4);
                    if (!Free(sofaQ)) sofaQ = (sofaQ + 2) % 4;
                    var sp = c + quads[sofaQ] * 1.2f;
                    Prop(l, PropKind.Sofa, sp, quads[sofaQ].z > 0 ? 180f : 0f);
                    l.ItemSpots.Add(sp + new Vector3(Range(rng, -0.6f, 0.6f), 0.46f, 0f));
                    int coolerQ = (sofaQ + 1) % 4;
                    if (Free(coolerQ)) Prop(l, PropKind.WaterCooler, c + quads[coolerQ] * 1.55f, 0f);
                    int plantQ = (sofaQ + 3) % 4;
                    if (Free(plantQ)) Prop(l, PropKind.Plant, c + quads[plantQ] * 1.5f, 0f);
                    break;
                }

                case RoomType.Server:
                    for (int q = 0; q < 4; q++)
                    {
                        if (!Free(q)) continue;
                        Prop(l, PropKind.ServerRack, c + quads[q] + new Vector3(-0.45f, 0, 0), 0);
                        Prop(l, PropKind.ServerRack, c + quads[q] + new Vector3(0.45f, 0, 0), 0);
                    }
                    l.ItemSpots.Add(c + new Vector3(Range(rng, -0.9f, 0.9f), 0, Range(rng, -0.9f, 0.9f))); // the centre cross is always free
                    int sq = rng.Next(4);
                    if (Free(sq)) l.ItemSpots.Add(c + quads[sq] * 0.45f);
                    break;
            }
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

            int robots = s.Robots + level * s.RobotsPerHouse + (l.Floors - 1) * s.RobotsPerExtraFloor;
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
                l.RobotSpawns.Add(new RobotSpawn { Kind = kind, Floor = cell.z, Position = pos, Rotation = Quaternion.Euler(0, rng.Next(360), 0) });
            }

            // Fixed security devices: pressure plates in the room's walkways, motion sensors on walls, lidar poles.
            int devices = s.Devices + level * s.DevicesPerHouse;
            for (int i = 0; i < devices; i++)
            {
                var cell = candidates[rng.Next(candidates.Count)];
                var c = l.CellCenter(cell.z, cell.x, cell.y);
                var kind = RobotRoster.PickDevice(level, rng, s.Roster);
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
                            doors.Add(new LaserSpot { Position = new Vector3((x + 1) * Cell, fy, (y + 0.5f) * Cell), Rotation = Quaternion.Euler(0, 90, 0), Width = DoorWidth });
                for (int x = 0; x < l.Width; x++)
                    for (int y = 0; y < l.Height - 1; y++)
                        if (l.EdgesZ[f, x, y] == EdgeType.Door)
                            doors.Add(new LaserSpot { Position = new Vector3((x + 0.5f) * Cell, fy, (y + 1) * Cell), Rotation = Quaternion.identity, Width = DoorWidth });
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
        }
    }
}
