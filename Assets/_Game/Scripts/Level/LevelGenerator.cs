using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Room-based multi-storey office generator. Every floor is a grid of rooms; a randomized spanning tree per floor
    /// makes every room reachable, extra doors create loops, staircases in room corners connect the floors.
    /// </summary>
    public static class LevelGenerator
    {
        public const float Cell = 10f;
        public const float WallHeight = 3f;
        public const float FloorHeight = 3.5f; // floor-to-floor: walls + slab
        public const float SlabThickness = FloorHeight - WallHeight;
        public const float WallThickness = 0.2f;
        public const float DoorWidth = 1.8f;
        public const float DoorHeight = 2.3f;
        public const float VentWidth = 1.2f; // wall pieces overlap the opening by 0.1 on each side: 1.0 m clear
        public const float VentHeight = 1.4f; // crouched capsule 1.1 m + PhysX step-up probe (stepOffset 0.35); standing 1.8 m does not fit // crouched player is 1.1 m
        public const int CeilingLayer = 2; // "Ignore Raycast": kept out of the navmesh and vision checks
        public const int StairsArea = 3;   // NavMesh area "Stairs": robots without legs exclude it

        // Staircase geometry in the corner frame (u, v = meters from the corner walls into the room).
        public const int StepsPerFlight = 6;
        public const float StepRun = 0.42f;
        public const float StairSize = 4f;      // the stair + hole occupy [0, 4] x [0, 4] from the corner
        const float FlightStart = 3.8f;         // u where flight A starts / flight B ends
        const float LandingEnd = FlightStart - StepsPerFlight * StepRun; // 1.28

        public static readonly Vector3[] Quadrants =
        {
            new(2.7f, 0, 2.7f), new(-2.7f, 0, 2.7f), new(2.7f, 0, -2.7f), new(-2.7f, 0, -2.7f),
        };

        public static LevelLayout Current { get; private set; }
        public static GameObject CurrentRoot { get; private set; }

        static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        public static int FloorsFor(int levelIndex) => levelIndex < 2 ? 2 : levelIndex < 5 ? 3 : 4;

        // ------------------------------------------------------------------ layout

        public static LevelLayout Generate(int seed, int levelIndex)
        {
            var rng = new System.Random(seed);
            var l = new LevelLayout
            {
                Seed = seed,
                LevelIndex = levelIndex,
                Width = 3 + Mathf.Min(levelIndex, 2),
                Height = 3 + Mathf.Min(levelIndex / 3, 1),
                Floors = FloorsFor(levelIndex),
            };
            int w = l.Width, h = l.Height, floors = l.Floors;
            l.Rooms = new RoomType[floors, w, h];
            l.EdgesX = new EdgeType[floors, w - 1, h];
            l.EdgesZ = new EdgeType[floors, w, h - 1];
            l.StartCell = new Vector2Int(0, rng.Next(h));

            for (int f = 0; f < floors; f++) GenerateFloorPlan(l, f, rng);
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
                        int r = rng.Next(100);
                        l.Rooms[f, x, y] = r < 28 ? RoomType.OpenSpace
                            : r < 44 ? RoomType.Office
                            : r < 56 ? RoomType.Meeting
                            : r < 66 ? RoomType.Kitchen
                            : r < 78 ? RoomType.Server
                            : r < 88 ? RoomType.Storage
                            : r < 94 ? RoomType.Restroom
                            : RoomType.Lounge;
                    }

            l.Dark = new bool[floors, w, h];
            l.Flicker = new bool[floors, w, h];
            for (int f = 0; f < floors; f++)
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        if (l.Rooms[f, x, y] == RoomType.Lobby) continue;
                        double r = rng.NextDouble();
                        l.Dark[f, x, y] = r < 0.15 + 0.03 * Mathf.Min(levelIndex, 5);
                        l.Flicker[f, x, y] = !l.Dark[f, x, y] && r > 0.88;
                    }

            for (int f = 0; f < floors; f++)
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                        FurnishRoom(l, f, x, y, rng);

            PlaceSecurity(l, rng);

            float height = floors * FloorHeight;
            l.BuildingBounds = new Bounds(new Vector3(w * Cell / 2f, height / 2f, h * Cell / 2f), new Vector3(w * Cell, height, h * Cell));
            return l;
        }

        static void GenerateFloorPlan(LevelLayout l, int f, System.Random rng)
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
                SetEdge(l, f, cur, next, rng.NextDouble() < 0.3 ? EdgeType.Open : EdgeType.Door);
                visited[next.x, next.y] = true;
                stack.Push(next);
            }
            for (int x = 0; x < w - 1; x++)
                for (int y = 0; y < h; y++)
                    if (l.EdgesX[f, x, y] == EdgeType.Wall && rng.NextDouble() < 0.2) l.EdgesX[f, x, y] = EdgeType.Door;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h - 1; y++)
                    if (l.EdgesZ[f, x, y] == EdgeType.Wall && rng.NextDouble() < 0.2) l.EdgesZ[f, x, y] = EdgeType.Door;
            // Crawl vents in some of the remaining walls: shortcuts for crouching players only.
            for (int x = 0; x < w - 1; x++)
                for (int y = 0; y < h; y++)
                    if (l.EdgesX[f, x, y] == EdgeType.Wall && rng.NextDouble() < 0.35) l.EdgesX[f, x, y] = EdgeType.Vent;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h - 1; y++)
                    if (l.EdgesZ[f, x, y] == EdgeType.Wall && rng.NextDouble() < 0.35) l.EdgesZ[f, x, y] = EdgeType.Vent;
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

        static void PlaceSecurity(LevelLayout l, System.Random rng)
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

            int robots = 2 + level + (l.Floors - 1);
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
                l.RobotSpawns.Add(new RobotSpawn { Type = rng.NextDouble() < 0.6 ? 0 : 1, Floor = cell.z, Position = pos });
            }

            int cameras = 2 + level;
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
            int lasers = 1 + level;
            while (l.LaserSpots.Count < lasers && doors.Count > 0)
            {
                int idx = rng.Next(doors.Count);
                var spot = doors[idx];
                doors.RemoveAt(idx);
                if ((spot.Position - l.HatchPosition).magnitude < Cell * 0.75f) continue; // keep the lobby doorways free
                spot.Kind = rng.Next(2);
                l.LaserSpots.Add(spot);
            }
            // Sliding doors in some of the remaining doorways (never together with a laser).
            foreach (var d in doors)
                if (rng.NextDouble() < 0.45) l.AutoDoors.Add(new Pose(d.Position, d.Rotation));
        }

        // ------------------------------------------------------------------ geometry

        public static GameObject Build(LevelLayout l, LevelArt art)
        {
            Clear();
            Current = l;
            var root = new GameObject($"Level_{l.Seed}");
            CurrentRoot = root;
            float W = l.Width * Cell, H = l.Height * Cell;
            float top = l.Floors * FloorHeight;

            // Ground, slabs between floors (with stair holes), roof.
            Block(root, "Floor", new Vector3(W / 2, -0.1f, H / 2), new Vector3(W, 0.2f, H), art.Floor);
            for (int f = 1; f < l.Floors; f++) BuildSlab(root, art, l, f);
            var roof = Block(root, "Roof", new Vector3(W / 2, top - SlabThickness / 2f, H / 2), new Vector3(W + 0.4f, SlabThickness, H + 0.4f), art.Ceiling);
            roof.layer = CeilingLayer;
            var ground = Block(root, "OutsideGround", new Vector3(W / 2, -0.07f, H / 2), new Vector3(W + 60f, 0.1f, H + 60f), art.OutsideGround);
            ground.layer = CeilingLayer;
            Fence(root, new Vector3(W / 2, 2.5f, -25f), new Vector3(W + 60f, 5f, 1f));
            Fence(root, new Vector3(W / 2, 2.5f, H + 25f), new Vector3(W + 60f, 5f, 1f));
            Fence(root, new Vector3(-25f, 2.5f, H / 2), new Vector3(1f, 5f, H + 60f));
            Fence(root, new Vector3(W + 25f, 2.5f, H / 2), new Vector3(1f, 5f, H + 60f));

            for (int f = 0; f < l.Floors; f++)
            {
                float fy = LevelLayout.FloorY(f);
                var up = Vector3.up * fy;
                for (int x = 0; x < l.Width - 1; x++)
                    for (int y = 0; y < l.Height; y++)
                        EdgeWall(root, art, new Vector3((x + 1) * Cell, 0, y * Cell) + up, new Vector3((x + 1) * Cell, 0, (y + 1) * Cell) + up, l.EdgesX[f, x, y]);
                for (int x = 0; x < l.Width; x++)
                    for (int y = 0; y < l.Height - 1; y++)
                        EdgeWall(root, art, new Vector3(x * Cell, 0, (y + 1) * Cell) + up, new Vector3((x + 1) * Cell, 0, (y + 1) * Cell) + up, l.EdgesZ[f, x, y]);

                for (int x = 0; x < l.Width; x++)
                {
                    EdgeWall(root, art, new Vector3(x * Cell, 0, 0) + up, new Vector3((x + 1) * Cell, 0, 0) + up, Exterior(l, f, x, 0, Vector2Int.down));
                    EdgeWall(root, art, new Vector3(x * Cell, 0, H) + up, new Vector3((x + 1) * Cell, 0, H) + up, Exterior(l, f, x, l.Height - 1, Vector2Int.up));
                }
                for (int y = 0; y < l.Height; y++)
                {
                    EdgeWall(root, art, new Vector3(0, 0, y * Cell) + up, new Vector3(0, 0, (y + 1) * Cell) + up, Exterior(l, f, 0, y, Vector2Int.left));
                    EdgeWall(root, art, new Vector3(W, 0, y * Cell) + up, new Vector3(W, 0, (y + 1) * Cell) + up, Exterior(l, f, l.Width - 1, y, Vector2Int.right));
                }

                for (int x = 0; x < l.Width; x++)
                    for (int y = 0; y < l.Height; y++)
                    {
                        var go = new GameObject($"Light_{f}_{x}_{y}");
                        go.transform.SetParent(root.transform, false);
                        go.transform.position = l.CellCenter(f, x, y) + Vector3.up * (WallHeight - 0.9f);
                        var light = go.AddComponent<Light>();
                        light.type = LightType.Point;
                        light.range = 10f;
                        light.shadows = LightShadows.None;
                        var room = l.Rooms[f, x, y];
                        bool dark = l.Dark[f, x, y];
                        float intensity = dark ? 0.18f : room == RoomType.Server ? 1f : 1.5f;
                        var color = dark ? new Color(0.5f, 0.6f, 0.9f) : room switch
                        {
                            RoomType.Server => new Color(0.55f, 0.75f, 1f),
                            RoomType.Lobby => new Color(0.75f, 1f, 0.8f),
                            _ => new Color(1f, 0.95f, 0.85f),
                        };
                        go.AddComponent<RoomLight>().Init(color, intensity, l.Flicker[f, x, y], f * 13.1f + x * 3.7f + y * 1.9f);
                    }
            }

            foreach (var s in l.Stairs) BuildStair(root, art, s);
            foreach (var d in l.AutoDoors) BuildAutoDoor(root, art, d);
            BuildFacade(root, art, l);
            if (Application.isPlaying) CombineStatic(root);

            foreach (var p in l.Props)
            {
                var prefab = PropPrefab(art, p.Kind);
                if (prefab == null) continue;
                var go = Object.Instantiate(prefab, p.Position, Quaternion.Euler(0, p.RotationY, 0), root.transform);
                go.name = p.Kind.ToString();
            }
            return root;
        }

        public static void Clear()
        {
            if (CurrentRoot != null)
            {
                if (Application.isPlaying) Object.Destroy(CurrentRoot);
                else Object.DestroyImmediate(CurrentRoot);
            }
            CurrentRoot = null;
            Current = null;
        }

        /// <summary>Slab under floor <paramref name="f"/>, built per cell so that stair cells get a hole.</summary>
        static void BuildSlab(GameObject root, LevelArt art, LevelLayout l, int f)
        {
            float topY = LevelLayout.FloorY(f), y = topY - SlabThickness / 2f;
            for (int x = 0; x < l.Width; x++)
                for (int z = 0; z < l.Height; z++)
                {
                    float x0 = x * Cell, z0 = z * Cell;
                    Stair? hole = null;
                    foreach (var s in l.Stairs) if (s.Floor + 1 == f && s.Cell == new Vector2Int(x, z)) hole = s;
                    if (hole == null)
                    {
                        Block(root, "Slab", new Vector3(x0 + Cell / 2f, y, z0 + Cell / 2f), new Vector3(Cell, SlabThickness, Cell), art.Floor);
                        continue;
                    }
                    // Hole rectangle in world space.
                    var s2 = hole.Value;
                    float cx = s2.Corner.x > 0 ? x0 + Cell : x0, cz = s2.Corner.y > 0 ? z0 + Cell : z0;
                    float hx0 = Mathf.Min(cx, cx - s2.Corner.x * StairSize), hx1 = Mathf.Max(cx, cx - s2.Corner.x * StairSize);
                    float hz0 = Mathf.Min(cz, cz - s2.Corner.y * StairSize), hz1 = Mathf.Max(cz, cz - s2.Corner.y * StairSize);
                    // Cell minus hole = up to 4 rectangles: left/right strips (full depth) + front/back strips (hole width).
                    SlabRect(root, art, x0, hx0, z0, z0 + Cell, y);
                    SlabRect(root, art, hx1, x0 + Cell, z0, z0 + Cell, y);
                    SlabRect(root, art, hx0, hx1, z0, hz0, y);
                    SlabRect(root, art, hx0, hx1, hz1, z0 + Cell, y);
                }
        }

        static void SlabRect(GameObject root, LevelArt art, float x0, float x1, float z0, float z1, float y)
        {
            if (x1 - x0 < 0.01f || z1 - z0 < 0.01f) return;
            Block(root, "Slab", new Vector3((x0 + x1) / 2f, y, (z0 + z1) / 2f), new Vector3(x1 - x0, SlabThickness, z1 - z0), art.Floor);
        }

        /// <summary>
        /// U-shaped staircase: flight A runs from the room towards the corner wall, a landing, flight B runs back up
        /// to the next floor. Steps are solid boxes (proper colliders for players and the navmesh, area "Stairs").
        /// </summary>
        static void BuildStair(GameObject root, LevelArt art, Stair s)
        {
            float fy = LevelLayout.FloorY(s.Floor);
            float half = FloorHeight / 2f, rise = half / StepsPerFlight;
            float cx = (s.Cell.x + (s.Corner.x > 0 ? 1 : 0)) * Cell;
            float cz = (s.Cell.y + (s.Corner.y > 0 ? 1 : 0)) * Cell;
            float ux = -s.Corner.x, vz = -s.Corner.y; // into the room
            var stairRoot = new GameObject($"Stairs_{s.Floor}");
            stairRoot.transform.SetParent(root.transform, false);
            var mod = stairRoot.AddComponent<NavMeshModifier>();
            mod.overrideArea = true;
            mod.area = StairsArea;

            void Box(string name, float u0, float u1, float v0, float v1, float y0, float y1, Material m)
            {
                var a = new Vector3(cx + ux * u0, fy + y0, cz + vz * v0);
                var b = new Vector3(cx + ux * u1, fy + y1, cz + vz * v1);
                Block(stairRoot, name, (a + b) / 2f, new Vector3(Mathf.Abs(b.x - a.x), y1 - y0, Mathf.Abs(b.z - a.z)), m);
            }

            const float aV0 = 0.2f, aV1 = 1.95f, bV0 = 2.05f, bV1 = 3.85f;
            for (int i = 0; i < StepsPerFlight; i++)
                Box("StepA", FlightStart - (i + 1) * StepRun, FlightStart - i * StepRun, aV0, aV1, 0f, (i + 1) * rise, art.DoorFrame);
            Box("Landing", 0.12f, LandingEnd, 0.12f, bV1, 0f, half, art.DoorFrame);
            for (int j = 0; j < StepsPerFlight; j++)
                Box("StepB", LandingEnd + j * StepRun, LandingEnd + (j + 1) * StepRun, bV0, bV1, 0f, half + (j + 1) * rise, art.DoorFrame);
            Box("Exit", FlightStart, StairSize + 0.08f, bV0, bV1, FloorHeight - 0.25f, FloorHeight, art.DoorFrame);

            // Railings around the hole on the floor above (open only where flight B arrives).
            var rail = art.DoorFrame;
            Box("Rail", 0.1f, StairSize, StairSize - 0.03f, StairSize + 0.03f, FloorHeight, FloorHeight + 1.0f, rail);
            Box("Rail", StairSize - 0.03f, StairSize + 0.03f, 0.1f, bV0 - 0.05f, FloorHeight, FloorHeight + 1.0f, rail);
            // Between the flights: a thin wall so nobody walks off flight B onto flight A.
            // Starts two steps away from the landing: a divider right at the landing would leave no room to turn.
            Box("Divider", LandingEnd + 2 * StepRun, FlightStart, aV1, bV0, 0f, FloorHeight - 0.4f, rail);
        }

        /// <summary>Static batching of everything that never moves (walls, slabs, stairs, furniture). Doors stay dynamic.</summary>
        static void CombineStatic(GameObject root)
        {
            var list = new List<GameObject>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                if (r.GetComponentInParent<AutoDoor>() == null) list.Add(r.gameObject);
            StaticBatchingUtility.Combine(list.ToArray(), root);
        }

        /// <summary>Windows on every floor (visual only) and street lamps in the gas around the building.</summary>
        static void BuildFacade(GameObject root, LevelArt art, LevelLayout l)
        {
            float W = l.Width * Cell, H = l.Height * Cell;
            var mat = art.Window != null ? art.Window : art.DoorFrame;
            void Window(Vector3 center, bool alongX)
            {
                // Thin panes on both faces of the wall; no collider.
                var size = alongX ? new Vector3(2.4f, 1.2f, WallThickness + 0.04f) : new Vector3(WallThickness + 0.04f, 1.2f, 2.4f);
                var go = Block(root, "Window", center, size, mat);
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            for (int f = 0; f < l.Floors; f++)
            {
                float y = LevelLayout.FloorY(f) + 1.7f;
                for (int x = 0; x < l.Width; x++)
                    foreach (float side in new[] { -2.6f, 2.6f })
                    {
                        float cx = (x + 0.5f) * Cell + side;
                        Window(new Vector3(cx, y, 0f), true);
                        Window(new Vector3(cx, y, H), true);
                    }
                for (int z = 0; z < l.Height; z++)
                    foreach (float side in new[] { -2.6f, 2.6f })
                    {
                        float cz = (z + 0.5f) * Cell + side;
                        Window(new Vector3(0f, y, cz), false);
                        Window(new Vector3(W, y, cz), false);
                    }
            }

            // Street lamps every ~14 m around the building, 6 m out in the fog.
            const float offset = 6f, step = 14f;
            var spots = new List<Vector3>();
            for (float x = 0; x <= W; x += step) { spots.Add(new Vector3(x, 0, -offset)); spots.Add(new Vector3(x, 0, H + offset)); }
            for (float z = step; z < H; z += step) { spots.Add(new Vector3(-offset, 0, z)); spots.Add(new Vector3(W + offset, 0, z)); }
            foreach (var p in spots)
            {
                var pole = Block(root, "LampPole", p + Vector3.up * 2f, new Vector3(0.15f, 4f, 0.15f), art.DoorFrame);
                pole.layer = CeilingLayer;
                var lamp = new GameObject("StreetLamp");
                lamp.transform.SetParent(root.transform, false);
                lamp.transform.position = p + Vector3.up * 4.1f;
                var light = lamp.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 9f;
                light.intensity = 2.5f;
                light.color = new Color(1f, 0.85f, 0.55f);
                light.shadows = LightShadows.None;
            }
        }

        static void BuildAutoDoor(GameObject root, LevelArt art, Pose pose)
        {
            var go = new GameObject("AutoDoor");
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);
            // Not part of the navmesh: robots plan through doorways and open the doors when they arrive.
            go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            float h = DoorHeight - 0.02f, w = DoorWidth / 2f;
            Transform Panel(string name, float x)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = name;
                Object.DestroyImmediate(p.GetComponent<Collider>());
                p.transform.SetParent(go.transform, false);
                p.transform.localPosition = new Vector3(x, h / 2f, 0);
                p.transform.localScale = new Vector3(w, h, 0.06f);
                p.GetComponent<MeshRenderer>().sharedMaterial = art.Door != null ? art.Door : art.DoorFrame;
                return p.transform;
            }
            var door = go.AddComponent<AutoDoor>();
            door.PanelA = Panel("PanelA", -DoorWidth / 4f);
            door.PanelB = Panel("PanelB", DoorWidth / 4f);
            var blocker = go.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0, h / 2f, 0);
            blocker.size = new Vector3(DoorWidth, h, 0.1f);
            door.Blocker = blocker;
        }

        static EdgeType Exterior(LevelLayout l, int f, int x, int y, Vector2Int dir) =>
            f == 0 && l.ExteriorDoors.Exists(e => e.cell == new Vector2Int(x, y) && e.dir == dir) ? EdgeType.Door : EdgeType.Wall;

        static GameObject PropPrefab(LevelArt art, PropKind kind) => kind switch
        {
            PropKind.Desk => art.Desk,
            PropKind.Chair => art.Chair,
            PropKind.ServerRack => art.ServerRack,
            PropKind.Cabinet => art.Cabinet,
            PropKind.MeetingTable => art.MeetingTable,
            PropKind.Plant => art.Plant,
            PropKind.KitchenCounter => art.KitchenCounter,
            PropKind.Hatch => art.Hatch,
            PropKind.Shelf => art.Shelf,
            PropKind.Boxes => art.Boxes,
            PropKind.WaterCooler => art.WaterCooler,
            PropKind.Whiteboard => art.Whiteboard,
            PropKind.Sofa => art.Sofa,
            PropKind.ToiletStall => art.ToiletStall,
            _ => null,
        };

        static void EdgeWall(GameObject root, LevelArt art, Vector3 a, Vector3 b, EdgeType type)
        {
            if (type == EdgeType.Open) return;
            if (type == EdgeType.Wall)
            {
                WallPiece(root, art.Wall, a, b, 0, WallHeight);
                return;
            }
            var dir = (b - a).normalized;
            var mid = (a + b) / 2f;
            bool vent = type == EdgeType.Vent;
            // Vents sit in the middle of the wall like doors: the centre cross of a room is always kept free of furniture.
            float width = vent ? VentWidth : DoorWidth;
            var d0 = mid - dir * (width / 2f);
            var d1 = mid + dir * (width / 2f);
            WallPiece(root, art.Wall, a, d0, 0, WallHeight);
            WallPiece(root, art.Wall, d1, b, 0, WallHeight);
            WallPiece(root, art.DoorFrame != null ? art.DoorFrame : art.Wall, d0, d1, vent ? VentHeight : DoorHeight, WallHeight);
            if (vent) VentFrame(root, art, d0, d1);
        }

        /// <summary>A dark metal frame around the crawl hole so it reads as a vent from across the room (no collider).</summary>
        static void VentFrame(GameObject root, LevelArt art, Vector3 d0, Vector3 d1)
        {
            var dir = (d1 - d0).normalized;
            var side = new Vector3(-dir.z, 0, dir.x) * (WallThickness / 2f + 0.02f);
            foreach (var s in new[] { side, -side })
            {
                foreach (var (p, size) in new[]
                {
                    ((d0 + d1) / 2f + Vector3.up * VentHeight, new Vector3(VentWidth + 0.16f, 0.08f, 0.04f)),
                    (d0 + Vector3.up * VentHeight / 2f, new Vector3(0.08f, VentHeight, 0.04f)),
                    (d1 + Vector3.up * VentHeight / 2f, new Vector3(0.08f, VentHeight, 0.04f)),
                })
                {
                    var go = Block(root, "VentFrame", p + s, Mathf.Abs(dir.x) > 0.5f ? size : new Vector3(size.z, size.y, size.x), art.DoorFrame);
                    Object.DestroyImmediate(go.GetComponent<Collider>());
                }
            }
        }

        static void WallPiece(GameObject root, Material mat, Vector3 a, Vector3 b, float y0, float y1)
        {
            var len = (b - a).magnitude;
            if (len < 0.01f) return;
            bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
            var size = alongX
                ? new Vector3(len + WallThickness, y1 - y0, WallThickness)
                : new Vector3(WallThickness, y1 - y0, len + WallThickness);
            Block(root, "Wall", (a + b) / 2f + Vector3.up * ((y0 + y1) / 2f), size, mat);
        }

        static GameObject Block(GameObject root, string name, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root.transform, false);
            go.transform.position = center;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static void Fence(GameObject root, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Fence");
            go.transform.SetParent(root.transform, false);
            go.transform.position = center;
            go.layer = CeilingLayer;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
        }
    }
}
