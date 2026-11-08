using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    public enum RoomType { Lobby, OpenSpace, Office, Meeting, Kitchen, Server }
    public enum EdgeType { Wall, Door, Open }
    public enum PropKind { Desk, Chair, ServerRack, Cabinet, MeetingTable, Plant, KitchenCounter, Hatch }

    public struct PropPlacement
    {
        public PropKind Kind;
        public Vector3 Position;
        public float RotationY;
    }

    public struct RobotSpawn
    {
        public int Type; // 0 = guard (eyes), 1 = listener (ears)
        public Vector3 Position;
    }

    public struct LaserSpot
    {
        public int Kind; // 0 = static low beam (crouch under), 1 = blinking gate
        public Vector3 Position;
        public Quaternion Rotation;
        public float Width;
    }

    /// <summary>Pure data describing a generated office floor. Identical on every peer for the same seed.</summary>
    public class LevelLayout
    {
        public int Seed, LevelIndex, Width, Height;
        public RoomType[,] Rooms;
        public EdgeType[,] EdgesX; // between (x,y) and (x+1,y): wall at X = (x+1)*Cell
        public EdgeType[,] EdgesZ; // between (x,y) and (x,y+1): wall at Z = (y+1)*Cell
        public readonly List<(Vector2Int cell, Vector2Int dir)> ExteriorDoors = new();
        public Vector2Int StartCell;
        public Vector3 HatchPosition;
        public readonly List<Vector3> PlayerSpawns = new();
        public readonly List<Vector3> ItemSpots = new();
        public readonly List<Vector3> PatrolPoints = new();
        public readonly List<Pose> CameraSpots = new();
        public readonly List<LaserSpot> LaserSpots = new();
        public readonly List<RobotSpawn> RobotSpawns = new();
        public readonly List<PropPlacement> Props = new();
        public Bounds BuildingBounds;

        public Vector3 CellCenter(int x, int y) =>
            new Vector3((x + 0.5f) * LevelGenerator.Cell, 0f, (y + 0.5f) * LevelGenerator.Cell);

        public int Floors = 1;

        public bool IsInside(Vector3 p) =>
            p.x > 0f && p.z > 0f && p.x < Width * LevelGenerator.Cell && p.z < Height * LevelGenerator.Cell;

        /// <summary>Rooms reachable from a cell in one step (through doors / openings). Result: (x, y, floor).</summary>
        public IEnumerable<Vector3Int> Neighbours(int floor, Vector2Int c)
        {
            if (c.x > 0 && EdgesX[c.x - 1, c.y] != EdgeType.Wall) yield return new Vector3Int(c.x - 1, c.y, floor);
            if (c.x < Width - 1 && EdgesX[c.x, c.y] != EdgeType.Wall) yield return new Vector3Int(c.x + 1, c.y, floor);
            if (c.y > 0 && EdgesZ[c.x, c.y - 1] != EdgeType.Wall) yield return new Vector3Int(c.x, c.y - 1, floor);
            if (c.y < Height - 1 && EdgesZ[c.x, c.y] != EdgeType.Wall) yield return new Vector3Int(c.x, c.y + 1, floor);
        }
    }

    /// <summary>
    /// Room-based office generator. Every cell of a grid is a room; a randomized spanning tree makes sure
    /// that all rooms are reachable, extra doors create loops so that players can sneak around robots.
    /// </summary>
    public static class LevelGenerator
    {
        public const float Cell = 10f;
        public const float WallHeight = 3f;
        public const float WallThickness = 0.2f;
        public const float DoorWidth = 1.8f;
        public const float DoorHeight = 2.3f;
        public const int CeilingLayer = 2; // "Ignore Raycast": kept out of the navmesh and vision checks

        public static LevelLayout Current { get; private set; }
        public static GameObject CurrentRoot { get; private set; }

        static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        // ------------------------------------------------------------------ layout

        public static LevelLayout Generate(int seed, int levelIndex)
        {
            var rng = new System.Random(seed);
            var l = new LevelLayout
            {
                Seed = seed,
                LevelIndex = levelIndex,
                Width = 4 + Mathf.Min(levelIndex, 3),
                Height = 3 + Mathf.Min(levelIndex / 2, 2),
            };
            int w = l.Width, h = l.Height;
            l.Rooms = new RoomType[w, h];
            l.EdgesX = new EdgeType[w - 1, h];
            l.EdgesZ = new EdgeType[w, h - 1];

            // Spanning tree (randomized DFS) from the entrance on the west side.
            l.StartCell = new Vector2Int(0, rng.Next(h));
            var visited = new bool[w, h];
            var stack = new Stack<Vector2Int>();
            stack.Push(l.StartCell);
            visited[l.StartCell.x, l.StartCell.y] = true;
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
                SetEdge(l, cur, next, rng.NextDouble() < 0.3 ? EdgeType.Open : EdgeType.Door);
                visited[next.x, next.y] = true;
                stack.Push(next);
            }

            // Extra doors create loops.
            for (int x = 0; x < w - 1; x++)
                for (int y = 0; y < h; y++)
                    if (l.EdgesX[x, y] == EdgeType.Wall && rng.NextDouble() < 0.2) l.EdgesX[x, y] = EdgeType.Door;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h - 1; y++)
                    if (l.EdgesZ[x, y] == EdgeType.Wall && rng.NextDouble() < 0.2) l.EdgesZ[x, y] = EdgeType.Door;

            // Exterior doors: a way out into the gas, to hide or to walk around the building.
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

            // Room types.
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (new Vector2Int(x, y) == l.StartCell) { l.Rooms[x, y] = RoomType.Lobby; continue; }
                    int r = rng.Next(100);
                    l.Rooms[x, y] = r < 35 ? RoomType.OpenSpace
                        : r < 55 ? RoomType.Office
                        : r < 70 ? RoomType.Meeting
                        : r < 82 ? RoomType.Kitchen
                        : RoomType.Server;
                }

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    FurnishRoom(l, x, y, rng);

            PlaceSecurity(l, rng);

            l.BuildingBounds = new Bounds(
                new Vector3(w * Cell / 2f, WallHeight / 2f, h * Cell / 2f),
                new Vector3(w * Cell, WallHeight, h * Cell));
            return l;
        }

        static void SetEdge(LevelLayout l, Vector2Int a, Vector2Int b, EdgeType type)
        {
            if (a.y == b.y) l.EdgesX[Mathf.Min(a.x, b.x), a.y] = type;
            else l.EdgesZ[a.x, Mathf.Min(a.y, b.y)] = type;
        }

        static void Prop(LevelLayout l, PropKind kind, Vector3 pos, float rotY) =>
            l.Props.Add(new PropPlacement { Kind = kind, Position = pos, RotationY = rotY });

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        static void FurnishRoom(LevelLayout l, int x, int y, System.Random rng)
        {
            var c = l.CellCenter(x, y);
            var quads = new[] { new Vector3(2.7f, 0, 2.7f), new Vector3(-2.7f, 0, 2.7f), new Vector3(2.7f, 0, -2.7f), new Vector3(-2.7f, 0, -2.7f) };
            l.PatrolPoints.Add(c);

            switch (l.Rooms[x, y])
            {
                case RoomType.Lobby:
                    l.HatchPosition = c;
                    Prop(l, PropKind.Hatch, c, 0);
                    foreach (var q in quads)
                    {
                        Prop(l, PropKind.Plant, c + q * 1.45f, 0);
                        l.PlayerSpawns.Add(c + q * 0.45f);
                    }
                    break;

                case RoomType.OpenSpace:
                    foreach (var q in quads)
                    {
                        if (rng.NextDouble() > 0.85) { l.ItemSpots.Add(c + q); continue; }
                        float rot = rng.Next(2) * 90f;
                        var p = c + q;
                        Prop(l, PropKind.Desk, p, rot);
                        Prop(l, PropKind.Chair, p + Quaternion.Euler(0, rot, 0) * new Vector3(0, 0, 0.75f), rot + 180f);
                        l.ItemSpots.Add(p + Quaternion.Euler(0, rot, 0) * new Vector3(Range(rng, -0.5f, 0.5f), 0.77f, 0.1f));
                    }
                    break;

                case RoomType.Office:
                {
                    int deskQ = rng.Next(4);
                    var p = c + quads[deskQ];
                    float rot = rng.Next(4) * 90f;
                    Prop(l, PropKind.Desk, p, rot);
                    Prop(l, PropKind.Chair, p + Quaternion.Euler(0, rot, 0) * new Vector3(0, 0, 0.75f), rot + 180f);
                    l.ItemSpots.Add(p + new Vector3(0, 0.77f, 0));
                    var cab = c + quads[(deskQ + 1) % 4] * 1.4f;
                    Prop(l, PropKind.Cabinet, cab, rng.Next(4) * 90f);
                    l.ItemSpots.Add(cab + new Vector3(0, 1.4f, 0));
                    Prop(l, PropKind.Plant, c + quads[(deskQ + 2) % 4] * 1.5f, 0);
                    l.ItemSpots.Add(c + quads[(deskQ + 3) % 4]);
                    break;
                }

                case RoomType.Meeting:
                    Prop(l, PropKind.MeetingTable, c, 0);
                    for (int i = 0; i < 3; i++)
                    {
                        float px = -1.1f + i * 1.1f;
                        Prop(l, PropKind.Chair, c + new Vector3(px, 0, 1.05f), 180f);
                        Prop(l, PropKind.Chair, c + new Vector3(px, 0, -1.05f), 0f);
                    }
                    Prop(l, PropKind.Plant, c + quads[rng.Next(4)] * 1.5f, 0);
                    l.ItemSpots.Add(c + new Vector3(Range(rng, -1.2f, 1.2f), 0.78f, Range(rng, -0.3f, 0.3f)));
                    l.ItemSpots.Add(c + quads[rng.Next(4)]);
                    break;

                case RoomType.Kitchen:
                {
                    float side = rng.Next(2) == 0 ? 1f : -1f;
                    for (int i = -1; i <= 1; i += 2)
                    {
                        var p = c + new Vector3(i * 2.9f, 0, side * 4.45f);
                        Prop(l, PropKind.KitchenCounter, p, side > 0 ? 180f : 0f);
                        l.ItemSpots.Add(p + new Vector3(Range(rng, -0.8f, 0.4f), 0.93f, 0));
                    }
                    l.ItemSpots.Add(c + quads[rng.Next(4)]);
                    break;
                }

                case RoomType.Server:
                    foreach (var q in quads)
                    {
                        Prop(l, PropKind.ServerRack, c + q + new Vector3(-0.45f, 0, 0), 0);
                        Prop(l, PropKind.ServerRack, c + q + new Vector3(0.45f, 0, 0), 0);
                    }
                    l.ItemSpots.Add(c + new Vector3(Range(rng, -1.5f, 1.5f), 0, Range(rng, -1.5f, 1.5f)));
                    l.ItemSpots.Add(c + quads[rng.Next(4)] * 0.45f);
                    break;
            }
        }

        static void PlaceSecurity(LevelLayout l, System.Random rng)
        {
            int level = l.LevelIndex;
            var candidates = new List<Vector2Int>();
            for (int x = 0; x < l.Width; x++)
                for (int y = 0; y < l.Height; y++)
                {
                    var cell = new Vector2Int(x, y);
                    int dist = Mathf.Abs(x - l.StartCell.x) + Mathf.Abs(y - l.StartCell.y);
                    if (dist >= 2) candidates.Add(cell);
                }
            if (candidates.Count == 0) candidates.Add(new Vector2Int(l.Width - 1, l.Height - 1));

            // Robots.
            int robots = 2 + level;
            for (int i = 0; i < robots; i++)
            {
                var cell = candidates[rng.Next(candidates.Count)];
                var pos = l.CellCenter(cell.x, cell.y) + new Vector3(Range(rng, -1f, 1f), 0, Range(rng, -1f, 1f));
                l.RobotSpawns.Add(new RobotSpawn { Type = rng.NextDouble() < 0.6 ? 0 : 1, Position = pos });
            }

            // Cameras in room corners, looking at the room center.
            int cameras = 2 + level;
            var used = new HashSet<Vector2Int>();
            for (int i = 0; i < cameras && used.Count < candidates.Count; i++)
            {
                var cell = candidates[rng.Next(candidates.Count)];
                if (!used.Add(cell)) { i--; continue; }
                var c = l.CellCenter(cell.x, cell.y);
                var corner = new Vector3(rng.Next(2) == 0 ? -1 : 1, 0, rng.Next(2) == 0 ? -1 : 1);
                var pos = c + corner * (Cell / 2f - 0.45f) + Vector3.up * (WallHeight - 0.3f);
                var look = Quaternion.LookRotation(-corner.normalized); // pan only, tilt is set by the prefab pivot
                l.CameraSpots.Add(new Pose(pos, look));
            }

            // Lasers in interior doorways.
            var doors = new List<LaserSpot>();
            for (int x = 0; x < l.Width - 1; x++)
                for (int y = 0; y < l.Height; y++)
                    if (l.EdgesX[x, y] == EdgeType.Door)
                        doors.Add(new LaserSpot { Position = new Vector3((x + 1) * Cell, 0, (y + 0.5f) * Cell), Rotation = Quaternion.Euler(0, 90, 0), Width = DoorWidth });
            for (int x = 0; x < l.Width; x++)
                for (int y = 0; y < l.Height - 1; y++)
                    if (l.EdgesZ[x, y] == EdgeType.Door)
                        doors.Add(new LaserSpot { Position = new Vector3((x + 0.5f) * Cell, 0, (y + 1) * Cell), Rotation = Quaternion.identity, Width = DoorWidth });
            int lasers = 1 + level;
            while (l.LaserSpots.Count < lasers && doors.Count > 0)
            {
                int idx = rng.Next(doors.Count);
                var spot = doors[idx];
                doors.RemoveAt(idx);
                // Keep the lobby doorways free.
                if ((spot.Position - l.HatchPosition).magnitude < Cell * 0.75f) continue;
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

            // Floor / ceiling / outside ground.
            Block(root, "Floor", new Vector3(W / 2, -0.1f, H / 2), new Vector3(W, 0.2f, H), art.Floor);
            var ceiling = Block(root, "Ceiling", new Vector3(W / 2, WallHeight + 0.1f, H / 2), new Vector3(W + 0.4f, 0.2f, H + 0.4f), art.Ceiling);
            ceiling.layer = CeilingLayer;
            var ground = Block(root, "OutsideGround", new Vector3(W / 2, -0.07f, H / 2), new Vector3(W + 60f, 0.1f, H + 60f), art.OutsideGround);
            ground.layer = CeilingLayer;
            // Invisible fence far in the fog.
            Fence(root, new Vector3(W / 2, 2.5f, -25f), new Vector3(W + 60f, 5f, 1f));
            Fence(root, new Vector3(W / 2, 2.5f, H + 25f), new Vector3(W + 60f, 5f, 1f));
            Fence(root, new Vector3(-25f, 2.5f, H / 2), new Vector3(1f, 5f, H + 60f));
            Fence(root, new Vector3(W + 25f, 2.5f, H / 2), new Vector3(1f, 5f, H + 60f));

            // Interior walls.
            for (int x = 0; x < l.Width - 1; x++)
                for (int y = 0; y < l.Height; y++)
                    EdgeWall(root, art, new Vector3((x + 1) * Cell, 0, y * Cell), new Vector3((x + 1) * Cell, 0, (y + 1) * Cell), l.EdgesX[x, y]);
            for (int x = 0; x < l.Width; x++)
                for (int y = 0; y < l.Height - 1; y++)
                    EdgeWall(root, art, new Vector3(x * Cell, 0, (y + 1) * Cell), new Vector3((x + 1) * Cell, 0, (y + 1) * Cell), l.EdgesZ[x, y]);

            // Perimeter.
            for (int x = 0; x < l.Width; x++)
            {
                EdgeWall(root, art, new Vector3(x * Cell, 0, 0), new Vector3((x + 1) * Cell, 0, 0), Exterior(l, x, 0, Vector2Int.down));
                EdgeWall(root, art, new Vector3(x * Cell, 0, H), new Vector3((x + 1) * Cell, 0, H), Exterior(l, x, l.Height - 1, Vector2Int.up));
            }
            for (int y = 0; y < l.Height; y++)
            {
                EdgeWall(root, art, new Vector3(0, 0, y * Cell), new Vector3(0, 0, (y + 1) * Cell), Exterior(l, 0, y, Vector2Int.left));
                EdgeWall(root, art, new Vector3(W, 0, y * Cell), new Vector3(W, 0, (y + 1) * Cell), Exterior(l, l.Width - 1, y, Vector2Int.right));
            }

            // Lights.
            for (int x = 0; x < l.Width; x++)
                for (int y = 0; y < l.Height; y++)
                {
                    var go = new GameObject($"Light_{x}_{y}");
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = l.CellCenter(x, y) + Vector3.up * (WallHeight - 0.9f);
                    var light = go.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.range = 10f;
                    light.intensity = l.Rooms[x, y] == RoomType.Server ? 1f : 1.5f;
                    light.color = l.Rooms[x, y] switch
                    {
                        RoomType.Server => new Color(0.55f, 0.75f, 1f),
                        RoomType.Lobby => new Color(0.75f, 1f, 0.8f),
                        _ => new Color(1f, 0.95f, 0.85f),
                    };
                    light.shadows = LightShadows.None;
                }

            // Props.
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
            if (CurrentRoot != null) Object.Destroy(CurrentRoot);
            CurrentRoot = null;
            Current = null;
        }

        static EdgeType Exterior(LevelLayout l, int x, int y, Vector2Int dir) =>
            l.ExteriorDoors.Exists(e => e.cell == new Vector2Int(x, y) && e.dir == dir) ? EdgeType.Door : EdgeType.Wall;

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
