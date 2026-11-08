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
                        l.Rooms[f, x, y] = r < 35 ? RoomType.OpenSpace
                            : r < 55 ? RoomType.Office
                            : r < 70 ? RoomType.Meeting
                            : r < 82 ? RoomType.Kitchen
                            : RoomType.Server;
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
                    else l.ItemSpots.Add(c + new Vector3(Range(rng, -1f, 1f), 0, Range(rng, -1f, 1f)));
                    break;
                }

                case RoomType.Server:
                    for (int q = 0; q < 4; q++)
                    {
                        if (!Free(q)) continue;
                        Prop(l, PropKind.ServerRack, c + quads[q] + new Vector3(-0.45f, 0, 0), 0);
                        Prop(l, PropKind.ServerRack, c + quads[q] + new Vector3(0.45f, 0, 0), 0);
                    }
                    l.ItemSpots.Add(c + new Vector3(Range(rng, -1.5f, 1.5f), 0, Range(rng, -1.5f, 1.5f)));
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
                        var room = l.Rooms[f, x, y];
                        light.intensity = room == RoomType.Server ? 1f : 1.5f;
                        light.color = room switch
                        {
                            RoomType.Server => new Color(0.55f, 0.75f, 1f),
                            RoomType.Lobby => new Color(0.75f, 1f, 0.8f),
                            _ => new Color(1f, 0.95f, 0.85f),
                        };
                        light.shadows = LightShadows.None;
                    }
            }

            foreach (var s in l.Stairs) BuildStair(root, art, s);

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
            var d0 = mid - dir * (DoorWidth / 2f);
            var d1 = mid + dir * (DoorWidth / 2f);
            WallPiece(root, art.Wall, a, d0, 0, WallHeight);
            WallPiece(root, art.Wall, d1, b, 0, WallHeight);
            WallPiece(root, art.DoorFrame != null ? art.DoorFrame : art.Wall, d0, d1, DoorHeight, WallHeight);
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
