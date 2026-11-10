using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>The level currently built on this peer (layout + scene root).</summary>
    public static class ActiveLevel
    {
        public static LevelLayout Layout { get; private set; }
        public static GameObject Root { get; private set; }

        /// <summary>Builds <paramref name="layout"/> from the prefabs in <paramref name="art"/>, replacing the previous level.</summary>
        public static GameObject Load(LevelLayout layout, LevelArt art)
        {
            Clear();
            Layout = layout;
            Root = new LevelBuilder(art, layout).Build();
            if (Application.isPlaying && Root.TryGetComponent<LightBudget>(out var budget)) budget.ApplyNow();
            return Root;
        }

        public static void Clear()
        {
            if (Root != null)
            {
                if (Application.isPlaying) Object.Destroy(Root);
                else Object.DestroyImmediate(Root);
            }
            Root = null;
            Layout = null;
        }
    }

    /// <summary>Light of a room by its type (pure, unit tested).</summary>
    public static class RoomLighting
    {
        /// <summary>Ceiling panels sit this far either side of the room centre (along x).</summary>
        public const float LampOffset = 2.5f;

        public static (Color color, float intensity) For(RoomType room, bool dark)
        {
            if (dark) return (new Color(1f, 0.95f, 0.85f), 0f); // switched off: exit signs, lamps and the moon only
            return room switch
            {
                RoomType.Server => (new Color(0.55f, 0.75f, 1f), LightMath.RoomIntensity * 0.6f),
                RoomType.Storage => (new Color(1f, 0.92f, 0.8f), LightMath.RoomIntensity * 0.75f),
                RoomType.Lobby => (new Color(0.92f, 1f, 0.95f), LightMath.RoomIntensity * 1.15f),
                _ => (new Color(1f, 0.95f, 0.86f), LightMath.RoomIntensity),
            };
        }
    }

    /// <summary>
    /// Turns a <see cref="LevelLayout"/> into scene objects. Everything is instantiated from the prefabs of
    /// <see cref="LevelArt"/>: structure blocks are unit cubes scaled to size, pieces (stairs, doors, vents,
    /// lights, lamps) and furniture are placed as they are.
    /// </summary>
    public sealed class LevelBuilder
    {
        readonly LevelArt m_Art;
        readonly LevelLayout m_Layout;
        Transform m_Root;

        public LevelBuilder(LevelArt art, LevelLayout layout)
        {
            m_Art = art;
            m_Layout = layout;
        }

        public GameObject Build()
        {
            var l = m_Layout;
            var root = Object.Instantiate(m_Art.LevelRoot);
            root.name = $"Level_{l.Seed}";
            m_Root = root.transform;

            float W = l.Width * LevelMetrics.Cell, H = l.Height * LevelMetrics.Cell;
            float top = l.Floors * LevelMetrics.FloorHeight;

            // Ground, slabs between floors (with stair holes), roof, the yard and its invisible fence.
            Block(m_Art.FloorSlab, "Floor", new Vector3(W / 2, -0.1f, H / 2), new Vector3(W, 0.2f, H));
            for (int f = 1; f < l.Floors; f++) BuildSlab(f);
            Block(m_Art.Roof, "Roof", new Vector3(W / 2, top - LevelMetrics.SlabThickness / 2f, H / 2), new Vector3(W + 0.4f, LevelMetrics.SlabThickness, H + 0.4f));
            Block(m_Art.OutsideGround, "OutsideGround", new Vector3(W / 2, -0.07f, H / 2), new Vector3(W + 60f, 0.1f, H + 60f));
            Block(m_Art.Fence, "Fence", new Vector3(W / 2, 2.5f, -25f), new Vector3(W + 60f, 5f, 1f));
            Block(m_Art.Fence, "Fence", new Vector3(W / 2, 2.5f, H + 25f), new Vector3(W + 60f, 5f, 1f));
            Block(m_Art.Fence, "Fence", new Vector3(-25f, 2.5f, H / 2), new Vector3(1f, 5f, H + 60f));
            Block(m_Art.Fence, "Fence", new Vector3(W + 25f, 2.5f, H / 2), new Vector3(1f, 5f, H + 60f));

            for (int f = 0; f < l.Floors; f++)
            {
                BuildWalls(f);
                BuildLights(f);
            }
            foreach (var s in l.Stairs) BuildStair(s);
            foreach (var d in l.AutoDoors) Place(m_Art.AutoDoor, "AutoDoor", d.position, d.rotation);
            BuildFacade();
            if (Application.isPlaying) CombineStatic(root);

            foreach (var p in l.Props)
            {
                var prefab = m_Art.Prop(p.Kind);
                if (prefab != null) Place(prefab, p.Kind.ToString(), p.Position, Quaternion.Euler(0, p.RotationY, 0));
            }
            return root;
        }

        // ------------------------------------------------------------------ helpers

        GameObject Place(GameObject prefab, string name, Vector3 position, Quaternion rotation)
        {
            var go = Object.Instantiate(prefab, position, rotation, m_Root);
            go.name = name;
            return go;
        }

        /// <summary>A unit-cube prefab stretched to <paramref name="size"/>.</summary>
        GameObject Block(GameObject prefab, string name, Vector3 center, Vector3 size)
        {
            var go = Place(prefab, name, center, Quaternion.identity);
            go.transform.localScale = size;
            return go;
        }

        // ------------------------------------------------------------------ structure

        /// <summary>Slab under floor <paramref name="f"/>, built per cell so that stair cells get a hole.</summary>
        void BuildSlab(int f)
        {
            var l = m_Layout;
            const float cell = LevelMetrics.Cell, size = LevelMetrics.StairSize;
            float y = LevelLayout.FloorY(f) - LevelMetrics.SlabThickness / 2f;
            for (int x = 0; x < l.Width; x++)
                for (int z = 0; z < l.Height; z++)
                {
                    float x0 = x * cell, z0 = z * cell;
                    Stair? hole = null;
                    foreach (var s in l.Stairs) if (s.Floor + 1 == f && s.Cell == new Vector2Int(x, z)) hole = s;
                    if (hole == null)
                    {
                        Block(m_Art.FloorSlab, "Slab", new Vector3(x0 + cell / 2f, y, z0 + cell / 2f), new Vector3(cell, LevelMetrics.SlabThickness, cell));
                        continue;
                    }
                    var h = hole.Value;
                    float cx = h.Corner.x > 0 ? x0 + cell : x0, cz = h.Corner.y > 0 ? z0 + cell : z0;
                    float hx0 = Mathf.Min(cx, cx - h.Corner.x * size), hx1 = Mathf.Max(cx, cx - h.Corner.x * size);
                    float hz0 = Mathf.Min(cz, cz - h.Corner.y * size), hz1 = Mathf.Max(cz, cz - h.Corner.y * size);
                    // Cell minus hole = up to 4 rectangles: left/right strips (full depth) + front/back strips (hole width).
                    SlabRect(x0, hx0, z0, z0 + cell, y);
                    SlabRect(hx1, x0 + cell, z0, z0 + cell, y);
                    SlabRect(hx0, hx1, z0, hz0, y);
                    SlabRect(hx0, hx1, hz1, z0 + cell, y);
                }
        }

        void SlabRect(float x0, float x1, float z0, float z1, float y)
        {
            if (x1 - x0 < 0.01f || z1 - z0 < 0.01f) return;
            Block(m_Art.FloorSlab, "Slab", new Vector3((x0 + x1) / 2f, y, (z0 + z1) / 2f), new Vector3(x1 - x0, LevelMetrics.SlabThickness, z1 - z0));
        }

        /// <summary>
        /// Walls of a floor without any overlapping geometry (overlaps z-fight and look like clipping): every wall
        /// segment stops half a wall thickness short of the grid corners, and every corner that walls meet gets one post.
        /// Openings are cut between the segments; the lintel above a doorway fills exactly the gap.
        /// </summary>
        void BuildWalls(int f)
        {
            var l = m_Layout;
            const float cell = LevelMetrics.Cell;
            float W = l.Width * cell, H = l.Height * cell;
            var up = Vector3.up * LevelLayout.FloorY(f);
            m_Corners.Clear();
            for (int x = 0; x < l.Width - 1; x++)
                for (int y = 0; y < l.Height; y++)
                    EdgeWall(new Vector3((x + 1) * cell, 0, y * cell) + up, new Vector3((x + 1) * cell, 0, (y + 1) * cell) + up, l.EdgesX[f, x, y]);
            for (int x = 0; x < l.Width; x++)
                for (int y = 0; y < l.Height - 1; y++)
                    EdgeWall(new Vector3(x * cell, 0, (y + 1) * cell) + up, new Vector3((x + 1) * cell, 0, (y + 1) * cell) + up, l.EdgesZ[f, x, y]);
            for (int x = 0; x < l.Width; x++)
            {
                EdgeWall(new Vector3(x * cell, 0, 0) + up, new Vector3((x + 1) * cell, 0, 0) + up, Exterior(f, x, 0, Vector2Int.down), true);
                EdgeWall(new Vector3(x * cell, 0, H) + up, new Vector3((x + 1) * cell, 0, H) + up, Exterior(f, x, l.Height - 1, Vector2Int.up), true);
            }
            for (int y = 0; y < l.Height; y++)
            {
                EdgeWall(new Vector3(0, 0, y * cell) + up, new Vector3(0, 0, (y + 1) * cell) + up, Exterior(f, 0, y, Vector2Int.left), true);
                EdgeWall(new Vector3(W, 0, y * cell) + up, new Vector3(W, 0, (y + 1) * cell) + up, Exterior(f, l.Width - 1, y, Vector2Int.right), true);
            }
            const float t = LevelMetrics.WallThickness;
            foreach (var c in m_Corners)
                Block(m_Art.WallBlock, "WallPost", c + Vector3.up * (LevelMetrics.WallHeight / 2f), new Vector3(t, LevelMetrics.WallHeight, t));
        }

        readonly HashSet<Vector3> m_Corners = new();

        EdgeType Exterior(int f, int x, int y, Vector2Int dir) =>
            f == 0 && m_Layout.ExteriorDoors.Exists(e => e.cell == new Vector2Int(x, y) && e.dir == dir) ? EdgeType.Door : EdgeType.Wall;

        /// <param name="outside">An outer wall: its doorways get a sliding door (it keeps the gas out of sight).</param>
        void EdgeWall(Vector3 a, Vector3 b, EdgeType type, bool outside = false)
        {
            if (type == EdgeType.Open) return;
            m_Corners.Add(Snap(a));
            m_Corners.Add(Snap(b));
            const float t = LevelMetrics.WallThickness;
            var dir = (b - a).normalized;
            var a1 = a + dir * (t / 2f); // segments end at the corner posts
            var b1 = b - dir * (t / 2f);
            if (type == EdgeType.Wall)
            {
                WallPiece(m_Art.WallBlock, a1, b1, 0, LevelMetrics.WallHeight);
                return;
            }
            var mid = (a + b) / 2f;
            bool vent = type == EdgeType.Vent;
            // Vents sit in the middle of the wall like doors: the centre cross of a room is always kept free of furniture.
            float clear = LevelMetrics.ClearWidth(vent ? LevelMetrics.VentWidth : LevelMetrics.DoorWidth);
            var d0 = mid - dir * (clear / 2f);
            var d1 = mid + dir * (clear / 2f);
            WallPiece(m_Art.WallBlock, a1, d0, 0, LevelMetrics.WallHeight);
            WallPiece(m_Art.WallBlock, d1, b1, 0, LevelMetrics.WallHeight);
            WallPiece(m_Art.TrimBlock, d0, d1, vent ? LevelMetrics.VentHeight : LevelMetrics.DoorHeight, LevelMetrics.WallHeight);
            // The vent frame and the exit sign are modelled along local X; turn them to the wall direction.
            var along = Quaternion.LookRotation(new Vector3(-dir.z, 0, dir.x));
            if (vent) Place(m_Art.VentFrame, "VentFrame", mid, along);
            else if (outside) Place(m_Art.AutoDoor, "ExteriorDoor", mid, along);
            else if (m_Art.ExitSign != null) Place(m_Art.ExitSign, "ExitSign", mid + Vector3.up * (LevelMetrics.DoorHeight + 0.14f), along);
        }

        static Vector3 Snap(Vector3 p) => new(Mathf.Round(p.x * 100f) / 100f, Mathf.Round(p.y * 100f) / 100f, Mathf.Round(p.z * 100f) / 100f);

        /// <summary>A wall block exactly from <paramref name="a"/> to <paramref name="b"/> (no overlap with neighbours).</summary>
        void WallPiece(GameObject prefab, Vector3 a, Vector3 b, float y0, float y1)
        {
            var len = (b - a).magnitude;
            if (len < 0.01f) return;
            bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
            const float t = LevelMetrics.WallThickness;
            var size = alongX ? new Vector3(len, y1 - y0, t) : new Vector3(t, y1 - y0, len);
            Block(prefab, "Wall", (a + b) / 2f + Vector3.up * ((y0 + y1) / 2f), size);
        }

        void BuildLights(int f)
        {
            var l = m_Layout;
            for (int x = 0; x < l.Width; x++)
                for (int y = 0; y < l.Height; y++)
                {
                    var (color, intensity) = RoomLighting.For(l.Rooms[f, x, y], l.Dark[f, x, y]);
                    // Panels ±2.5 m along x: clear of the stair hole (stairs fill a 4 m corner square).
                    var go = Place(m_Art.RoomLight, $"Light_{f}_{x}_{y}", l.CellCenter(f, x, y) + Vector3.up * LevelMetrics.WallHeight, Quaternion.identity);
                    go.GetComponent<RoomLight>().Init(color, intensity, l.Flicker[f, x, y], f * 13.1f + x * 3.7f + y * 1.9f);
                }
        }

        void BuildStair(Stair s)
        {
            var (mirrored, yaw) = StairGeometry.Placement(s.Corner);
            Place(mirrored ? m_Art.StairsMirrored : m_Art.Stairs, $"Stairs_{s.Floor}", StairGeometry.Origin(s), Quaternion.Euler(0f, yaw, 0f));
        }

        /// <summary>Windows on every floor (visual only, blinds facing the room).</summary>
        void BuildFacade()
        {
            var l = m_Layout;
            void Window(Vector3 p, float yaw) => Place(m_Art.Window, "Window", p, Quaternion.Euler(0f, yaw, 0f));
            for (int f = 0; f < l.Floors; f++)
            {
                float y = LevelLayout.FloorY(f) + 1.7f;
                foreach (var (p, yaw) in WindowSpots(l.Width, l.Height, y)) Window(p, yaw);
            }
        }

        /// <summary>
        /// Two windows per outer wall of every cell, at height <paramref name="y"/>; the yaw turns the window's interior
        /// side (local +Z) into the building (pure).
        /// </summary>
        public static List<(Vector3 pos, float yaw)> WindowSpots(int width, int height, float y)
        {
            const float cell = LevelMetrics.Cell;
            float W = width * cell, H = height * cell;
            var list = new List<(Vector3, float)>();
            foreach (float side in new[] { -2.6f, 2.6f })
            {
                for (int x = 0; x < width; x++)
                {
                    float cx = (x + 0.5f) * cell + side;
                    list.Add((new Vector3(cx, y, 0f), 0f));
                    list.Add((new Vector3(cx, y, H), 180f));
                }
                for (int z = 0; z < height; z++)
                {
                    float cz = (z + 0.5f) * cell + side;
                    list.Add((new Vector3(0f, y, cz), 90f));
                    list.Add((new Vector3(W, y, cz), -90f));
                }
            }
            return list;
        }

        /// <summary>Static batching of everything that never moves (walls, slabs, stairs, furniture). Doors stay dynamic.</summary>
        static void CombineStatic(GameObject root)
        {
            var list = new List<GameObject>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                if (r.GetComponentInParent<AutoDoor>() == null) list.Add(r.gameObject);
            StaticBatchingUtility.Combine(list.ToArray(), root);
        }
    }
}
