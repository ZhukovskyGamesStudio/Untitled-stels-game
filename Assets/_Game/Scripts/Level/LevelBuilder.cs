using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>The level currently built on this peer (layout + scene root).</summary>
    public static class ActiveLevel
    {
        public static LevelLayout Layout { get; private set; }
        public static GameObject Root { get; private set; }
        /// <summary>Surfaces of the house (walls, floors, openings) that visual styles dress.</summary>
        public static LevelShell Shell { get; private set; }
        /// <summary>The furniture placed in the house (one object per prop).</summary>
        public static IReadOnlyList<GameObject> Props => s_Props;
        static readonly List<GameObject> s_Props = new();

        /// <summary>Builds <paramref name="layout"/> from the prefabs in <paramref name="art"/>, replacing the previous level.</summary>
        public static GameObject Load(LevelLayout layout, LevelArt art)
        {
            Clear();
            Layout = layout;
            var builder = new LevelBuilder(art, layout);
            Root = builder.Build();
            Shell = builder.Shell;
            s_Props.AddRange(builder.Props);
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
            Shell = null;
            s_Props.Clear();
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
    /// <see cref="LevelArt"/>: structure blocks are unit cubes scaled (and turned) to size, pieces (stairs, doors, vents,
    /// lights, lamps) and furniture are placed as they are. Walls follow the plan (<see cref="HousePlan.Walls"/>):
    /// straight, diagonal and arcs of short pieces, railings on the edges of galleries; slabs follow its raster.
    /// </summary>
    public sealed class LevelBuilder
    {
        readonly LevelArt m_Art;
        readonly LevelLayout m_Layout;
        readonly HousePlan m_Plan;
        Transform m_Root;
        int m_Floor;

        const float RailingHeight = 1.05f, RailingThickness = 0.08f;

        /// <summary>Surfaces recorded while building.</summary>
        public LevelShell Shell { get; } = new();
        /// <summary>The furniture placed (not static-batched: styles swap its meshes; <see cref="PropBatches"/> merges it).</summary>
        public List<GameObject> Props { get; } = new();

        public LevelBuilder(LevelArt art, LevelLayout layout)
        {
            m_Art = art;
            m_Layout = layout;
            m_Plan = layout.Plan;
        }

        public GameObject Build()
        {
            var l = m_Layout;
            var root = Object.Instantiate(m_Art.LevelRoot);
            root.name = $"Level_{l.Seed}";
            m_Root = root.transform;

            float W = l.SizeX, H = l.SizeZ;
            // Ground under the building, slabs between floors (holes for stairs and atriums), the roof, the yard and its fence.
            for (int f = 0; f < l.Floors; f++) BuildSlab(f);
            BuildRoof();
            Block(m_Art.OutsideGround, "OutsideGround", new Vector3(W / 2, -0.07f, H / 2), new Vector3(W + 60f, 0.1f, H + 60f));
            Block(m_Art.Fence, "Fence", new Vector3(W / 2, 2.5f, -25f), new Vector3(W + 60f, 5f, 1f));
            Block(m_Art.Fence, "Fence", new Vector3(W / 2, 2.5f, H + 25f), new Vector3(W + 60f, 5f, 1f));
            Block(m_Art.Fence, "Fence", new Vector3(-25f, 2.5f, H / 2), new Vector3(1f, 5f, H + 60f));
            Block(m_Art.Fence, "Fence", new Vector3(W + 25f, 2.5f, H / 2), new Vector3(1f, 5f, H + 60f));

            for (int f = 0; f < l.Floors; f++)
            {
                m_Floor = f;
                BuildWalls(f);
            }
            foreach (var r in l.Rooms) BuildLights(r);
            foreach (var s in l.Stairs) BuildStair(s);
            for (int i = 0; i < l.AutoDoors.Count; i++)
            {
                var d = l.AutoDoors[i];
                var door = Place(m_Art.AutoDoor, "AutoDoor", d.position, d.rotation).GetComponent<AutoDoor>();
                if (door != null) door.Configure(i, l.LockedDoors.Contains(i));
            }
            BuildFacade();
            if (Application.isPlaying) CombineStatic(root);

            for (int i = 0; i < l.Props.Count; i++)
            {
                var p = l.Props[i];
                var prefab = m_Art.Prop(p.Kind);
                if (prefab == null) continue;
                var go = Place(prefab, p.Kind.ToString(), p.Position, Quaternion.Euler(0, p.RotationY, 0));
                RecordProp(go, p.Kind, LevelLayout.FloorOf(p.Position.y));
                if (go.TryGetComponent<Items.SafeBox>(out var safe)) safe.Configure(i, l.Seed);
                if (go.TryGetComponent<SecurityTerminal>(out var terminal))
                    foreach (var t in l.Terminals)
                        if (t.prop == i) terminal.Configure(i, t.kind);
                Props.Add(go);
            }
            return root;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>The footprint of the placed model on the map (glowing bits such as lamp shades and screens are left out).</summary>
        void RecordProp(GameObject go, PropKind kind, int floor)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.name.StartsWith("Eye")) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any || kind == PropKind.Hatch) return;
            Shell.Props.Add(new MapProp { Floor = floor, Kind = kind, Area = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z) });
        }

        GameObject Place(GameObject prefab, string name, Vector3 position, Quaternion rotation)
        {
            var go = Object.Instantiate(prefab, position, rotation, m_Root);
            go.name = name;
            return go;
        }

        /// <summary>A unit-cube prefab stretched to <paramref name="size"/> (and turned by <paramref name="rotation"/>).</summary>
        GameObject Block(GameObject prefab, string name, Vector3 center, Vector3 size, Quaternion? rotation = null)
        {
            var go = Place(prefab, name, center, rotation ?? Quaternion.identity);
            go.transform.localScale = size;
            return go;
        }

        // ------------------------------------------------------------------ slabs

        /// <summary>
        /// The slab under floor <paramref name="f"/> (the ground slab for floor 0): everywhere the floor has rooms or
        /// pockets, minus the holes of stairs arriving from below; atriums (voids) have none. Cells merge into rectangles.
        /// </summary>
        void BuildSlab(int f)
        {
            var plan = m_Plan;
            var holes = new List<Rect>();
            foreach (var s in m_Layout.Stairs) if (s.Floor + 1 == f) holes.Add(PlanGenerator.StairArea(s, 0f));
            bool Solid(int x, int z)
            {
                var k = plan.KindAt(f, x, z);
                if (k != CellKind.Room && k != CellKind.Pocket) return false;
                var c = HousePlan.CellCenter(x, z);
                foreach (var h in holes) if (h.Contains(c)) return false;
                return true;
            }
            float thick = f == 0 ? 0.2f : LevelMetrics.SlabThickness;
            float y = f == 0 ? -0.1f : LevelLayout.FloorY(f) - LevelMetrics.SlabThickness / 2f;
            foreach (var r in Rectangles(plan.NX, plan.NZ, Solid))
            {
                float x0 = r.xMin * HousePlan.Res, x1 = r.xMax * HousePlan.Res, z0 = r.yMin * HousePlan.Res, z1 = r.yMax * HousePlan.Res;
                Block(m_Art.FloorSlab, f == 0 ? "Floor" : "Slab", new Vector3((x0 + x1) / 2f, y, (z0 + z1) / 2f), new Vector3(x1 - x0, thick, z1 - z0));
                Shell.Floors.Add(new FloorRect { Min = new Vector2(x0, z0), Max = new Vector2(x1, z1), Y = y + thick / 2f, Floor = f });
            }
        }

        /// <summary>The roof over the top floor (over its rooms, pockets and the top of atriums).</summary>
        void BuildRoof()
        {
            int top = m_Layout.Floors - 1;
            float y = LevelLayout.FloorY(top + 1) - LevelMetrics.SlabThickness / 2f;
            foreach (var r in Rectangles(m_Plan.NX, m_Plan.NZ, (x, z) => m_Plan.KindAt(top, x, z) != CellKind.Outside))
            {
                float x0 = r.xMin * HousePlan.Res - 0.2f, x1 = r.xMax * HousePlan.Res + 0.2f, z0 = r.yMin * HousePlan.Res - 0.2f, z1 = r.yMax * HousePlan.Res + 0.2f;
                Block(m_Art.Roof, "Roof", new Vector3((x0 + x1) / 2f, y, (z0 + z1) / 2f), new Vector3(x1 - x0, LevelMetrics.SlabThickness, z1 - z0));
            }
        }

        /// <summary>Cells where <paramref name="solid"/> holds, merged into rectangles (widest run, then as tall as it stays).</summary>
        public static List<RectInt> Rectangles(int nx, int nz, System.Func<int, int, bool> solid)
        {
            var used = new bool[nx, nz];
            var list = new List<RectInt>();
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    if (used[x, z] || !solid(x, z)) continue;
                    int w = 0;
                    while (x + w < nx && !used[x + w, z] && solid(x + w, z)) w++;
                    int h = 1;
                    while (z + h < nz)
                    {
                        bool row = true;
                        for (int i = 0; i < w && row; i++) row = !used[x + i, z + h] && solid(x + i, z + h);
                        if (!row) break;
                        h++;
                    }
                    for (int i = 0; i < w; i++)
                        for (int j = 0; j < h; j++) used[x + i, z + j] = true;
                    list.Add(new RectInt(x, z, w, h));
                }
            return list;
        }

        // ------------------------------------------------------------------ walls

        readonly Dictionary<Vector3, float> m_Posts = new();
        /// <summary>Walls of the floor by their end points (cm keys): to find bent walls whose pieces meet at a shallow angle.</summary>
        readonly Dictionary<Vector3, List<int>> m_Ends = new();

        static Vector3 EndKey(Vector2 p) => new(Mathf.Round(p.x * 100f) / 100f, 0f, Mathf.Round(p.y * 100f) / 100f);

        /// <summary>
        /// The two walls meeting at <paramref name="p"/> continue each other at a shallow angle (the pieces of a chamfer or an
        /// arc, not a corner): their joint gets a post turned between them and decoration that meets at the joint.
        /// </summary>
        bool SmoothJoint(Vector2 p, out float yaw)
        {
            yaw = 0f;
            if (!m_Ends.TryGetValue(EndKey(p), out var walls) || walls.Count != 2) return false;
            var a = m_Plan.Walls[walls[0]];
            var b = m_Plan.Walls[walls[1]];
            if (a.Railing || b.Railing) return false;
            // directions pointing away from the joint
            var da = (EndKey(a.A) == EndKey(p) ? a.Dir : -a.Dir);
            var db = (EndKey(b.A) == EndKey(p) ? b.Dir : -b.Dir);
            float turn = 180f - Vector2.Angle(da, db); // 0 = straight on
            if (turn > 60f || turn < 1f) return false;
            var mean = (da - db).normalized; // along the wall through the joint
            yaw = Mathf.Atan2(mean.x, mean.y) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>
        /// Walls of a floor without overlapping geometry (overlaps z-fight and look like clipping): every piece stops half a
        /// wall thickness short of its ends, and every end gets one post. Openings are cut between the pieces; the lintel
        /// above a doorway fills exactly the gap. Railings edge the galleries over atriums.
        /// </summary>
        void BuildWalls(int f)
        {
            m_Posts.Clear();
            m_Ends.Clear();
            for (int i = 0; i < m_Plan.Walls.Count; i++)
            {
                var w = m_Plan.Walls[i];
                if (w.Floor != f || w.Railing) continue;
                foreach (var e in new[] { EndKey(w.A), EndKey(w.B) })
                {
                    if (!m_Ends.TryGetValue(e, out var l)) m_Ends[e] = l = new List<int>();
                    l.Add(i);
                }
            }
            for (int i = 0; i < m_Plan.Walls.Count; i++)
                if (m_Plan.Walls[i].Floor == f) Wall(i);
            const float t = LevelMetrics.WallThickness;
            float y = LevelLayout.FloorY(f);
            foreach (var kv in m_Posts)
            {
                // a post between two pieces of a bent wall turns with them: its faces run on with the wall's, no cube sticks out
                var at = new Vector2(kv.Key.x, kv.Key.z);
                var rot = SmoothJoint(at, out float yaw) ? Quaternion.Euler(0f, yaw, 0f) : (Quaternion?)null;
                Block(m_Art.WallBlock, "WallPost", kv.Key + Vector3.up * (y + kv.Value / 2f), new Vector3(t, kv.Value, t), rot);
            }
        }

        void Post(Vector2 p, float height)
        {
            var key = new Vector3(Mathf.Round(p.x * 100f) / 100f, 0f, Mathf.Round(p.y * 100f) / 100f);
            m_Posts[key] = m_Posts.TryGetValue(key, out var h) ? Mathf.Max(h, height) : height;
        }

        void Wall(int index)
        {
            var w = m_Plan.Walls[index];
            var openings = m_Plan.OpeningsOf(index);
            foreach (var o in openings) if (o.Kind == EdgeType.Open) return; // an open passage: no wall at all
            float y = LevelLayout.FloorY(w.Floor);
            var dir = w.Dir;
            var a3 = new Vector3(w.A.x, y, w.A.y);
            var b3 = new Vector3(w.B.x, y, w.B.y);
            var d3 = new Vector3(dir.x, 0f, dir.y);
            if (w.Railing)
            {
                RailingPiece(a3, b3);
                return;
            }
            float height = w.FullHeight ? LevelMetrics.FloorHeight : LevelMetrics.WallHeight;
            Post(w.A, height);
            Post(w.B, height);
            const float t = LevelMetrics.WallThickness;
            float len = w.Length;
            var inner = InnerNormals(w);
            float from = t / 2f;
            bool jointA = SmoothJoint(w.A, out _), jointB = SmoothJoint(w.B, out _);
            bool cornerA = !jointA;
            foreach (var o in openings)
            {
                bool vent = o.Kind == EdgeType.Vent, squeeze = o.Kind == EdgeType.Squeeze;
                float clear = LevelMetrics.ClearWidth(vent ? LevelMetrics.VentWidth : squeeze ? LevelMetrics.SqueezeWidth : LevelMetrics.DoorWidth);
                float o0 = o.At - clear / 2f, o1 = o.At + clear / 2f;
                float sill = o.High ? LevelMetrics.HighVentSill : LevelMetrics.VentSill;
                float openH = vent ? sill + LevelMetrics.VentHeight : LevelMetrics.DoorHeight;
                WallPiece(m_Art.WallBlock, a3 + d3 * from, a3 + d3 * o0, 0f, height);
                RecordFaces(a3 + d3 * from, a3 + d3 * o0, 0f, height, w.Exterior, inner, cornerA, false, jointA && cornerA == false && from <= t / 2f + 0.001f);
                if (vent)
                {
                    // the duct's floor: a sill of wall under the hole
                    WallPiece(m_Art.WallBlock, a3 + d3 * o0, a3 + d3 * o1, 0f, sill);
                    RecordFaces(a3 + d3 * o0, a3 + d3 * o1, 0f, sill, w.Exterior, inner, false, false);
                }
                WallPiece(m_Art.TrimBlock, a3 + d3 * o0, a3 + d3 * o1, openH, height);
                RecordFaces(a3 + d3 * o0, a3 + d3 * o1, openH, height, w.Exterior, inner, false, false);
                var mid = a3 + d3 * o.At;
                Shell.Openings.Add(new WallOpening { Center = mid, Along = d3, Width = clear, Height = openH, Vent = vent, Exterior = w.Exterior, Floor = m_Floor });
                // The vent frame and the exit sign are modelled along local X; turn them to the wall direction.
                var along = Quaternion.LookRotation(new Vector3(-d3.z, 0, d3.x));
                if (vent) Place(o.High && m_Art.HighVentFrame != null ? m_Art.HighVentFrame : m_Art.VentFrame, "VentFrame", mid, along);
                else if (w.Exterior) Place(m_Art.AutoDoor, "ExteriorDoor", mid, along);
                else if (squeeze) { if (m_Art.SqueezeFrame != null) Place(m_Art.SqueezeFrame, "SqueezeFrame", mid, along); }
                else if (m_Art.ExitSign != null) Place(m_Art.ExitSign, "ExitSign", mid + Vector3.up * (LevelMetrics.DoorHeight + 0.14f), along);
                from = o1;
                cornerA = false;
            }
            WallPiece(m_Art.WallBlock, a3 + d3 * from, a3 + d3 * (len - t / 2f), 0f, height);
            RecordFaces(a3 + d3 * from, a3 + d3 * (len - t / 2f), 0f, height, w.Exterior, inner, cornerA, !jointB,
                jointA && from <= t / 2f + 0.001f, jointB);
        }

        /// <summary>
        /// The sides of a wall a room sees: both for a wall between rooms, only the room's side otherwise. A bent wall (a
        /// chamfer, an arc) has the sealed pocket on one side, and the room's cells next to it are pocket too (a margin):
        /// look a little further out before deciding.
        /// </summary>
        List<Vector3> InnerNormals(PlanWall w)
        {
            var list = new List<Vector3>();
            var d = w.Dir;
            var n = new Vector2(-d.y, d.x);
            var mid = w.At(w.Length / 2f);
            foreach (var side in new[] { n, -n })
            {
                var k = CellKind.Pocket;
                for (float s = HousePlan.Res * 0.5f; s <= 0.8f && k == CellKind.Pocket; s += HousePlan.Res * 0.5f)
                {
                    var c = HousePlan.CellOf(mid + side * s);
                    k = m_Plan.KindAt(w.Floor, c.x, c.y);
                }
                if (k == CellKind.Room || k == CellKind.Void) list.Add(new Vector3(side.x, 0f, side.y));
            }
            return list;
        }

        void RecordFaces(Vector3 a, Vector3 b, float y0, float y1, bool outside, List<Vector3> sides, bool cornerA, bool cornerB,
            bool jointA = false, bool jointB = false)
        {
            if ((b - a).magnitude < 0.05f) return;
            foreach (var side in sides)
                Shell.Walls.Add(new WallFace
                {
                    A = a, B = b, Y0 = y0, Y1 = y1, Normal = side, Floor = m_Floor, Exterior = outside,
                    CornerA = cornerA, CornerB = cornerB, JointA = jointA, JointB = jointB,
                });
        }

        /// <summary>A wall block exactly from <paramref name="a"/> to <paramref name="b"/> (no overlap with neighbours), any direction.</summary>
        void WallPiece(GameObject prefab, Vector3 a, Vector3 b, float y0, float y1)
        {
            var len = (b - a).magnitude;
            if (len < 0.01f) return;
            const float t = LevelMetrics.WallThickness;
            var d = (b - a) / len;
            var centre = (a + b) / 2f + Vector3.up * ((y0 + y1) / 2f);
            if (Mathf.Abs(d.x) > 0.999f) Block(prefab, "Wall", centre, new Vector3(len, y1 - y0, t));
            else if (Mathf.Abs(d.z) > 0.999f) Block(prefab, "Wall", centre, new Vector3(t, y1 - y0, len));
            else Block(prefab, "Wall", centre, new Vector3(t, y1 - y0, len), Quaternion.LookRotation(d));
        }

        /// <summary>
        /// A railing on the edge of a gallery: an invisible waist-high collider the view and the light pass over, and bays of
        /// the house's railing look strung along it (posts and balusters, tubes, glass or a parapet — <see cref="StairLook"/>).
        /// </summary>
        void RailingPiece(Vector3 a, Vector3 b)
        {
            var len = (b - a).magnitude;
            if (len < 0.01f) return;
            var d = (b - a) / len;
            var centre = (a + b) / 2f + Vector3.up * (RailingHeight / 2f);
            var rot = Mathf.Abs(d.x) > 0.999f ? Quaternion.LookRotation(Vector3.right) : Mathf.Abs(d.z) > 0.999f ? Quaternion.identity : Quaternion.LookRotation(d);
            if (m_Art.RailingBay == null || m_Art.RailingBlock == null)
            {
                Block(m_Art.TrimBlock, "Railing", centre, new Vector3(RailingThickness, RailingHeight, len), rot);
                return;
            }
            Block(m_Art.RailingBlock, "Railing", centre, new Vector3(RailingThickness, RailingHeight, len), rot);
            int bays = Mathf.Max(1, Mathf.CeilToInt(len / 1.25f));
            var bayRot = Quaternion.LookRotation(Vector3.Cross(d, Vector3.up)); // local X along the railing
            int seed = LevelRandom.Mix(m_Layout.Seed, 0x57A1, 7); // one look for every railing of the house
            for (int i = 0; i < bays; i++)
            {
                var go = Place(m_Art.RailingBay, "RailingBay", a + d * (len * (i + 0.5f) / bays), bayRot);
                go.transform.localScale = new Vector3(len / bays, 1f, 1f);
                if (go.TryGetComponent<StairLook>(out var look)) look.Configure(seed);
            }
        }

        // ------------------------------------------------------------------ light, stairs, facade

        /// <summary>Ceiling lights of a room: about one per 70 m², spread over its floor (clear of stair holes), under its own ceiling.</summary>
        void BuildLights(PlanRoom r)
        {
            var (color, intensity) = RoomLighting.For(r.Type, r.Dark);
            float ceiling = LevelLayout.FloorY(r.Floor + r.Levels - 1) + LevelMetrics.WallHeight;
            int k = 0;
            foreach (var p in LightSpots(m_Layout, r))
            {
                var go = Place(m_Art.RoomLight, $"Light_{r.Id}_{k}", new Vector3(p.x, ceiling, p.y), Quaternion.identity);
                go.GetComponent<RoomLight>().Init(color, intensity, r.Flicker, r.Id * 3.7f + k * 1.9f);
                k++;
            }
        }

        /// <summary>Where the ceiling lights of a room hang (pure): a grid of spots on its floor, at least one.</summary>
        public static List<Vector2> LightSpots(LevelLayout l, PlanRoom r)
        {
            var list = new List<Vector2>();
            int n = Mathf.Max(1, Mathf.RoundToInt(r.Area / 70f));
            var b = r.Bounds;
            int cols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(n * b.width / Mathf.Max(1f, b.height))));
            int rows = Mathf.Max(1, Mathf.CeilToInt(n / (float)cols));
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < rows; j++)
                {
                    var p = new Vector2(b.xMin + b.width * (i + 0.5f) / cols, b.yMin + b.height * (j + 0.5f) / rows);
                    var p3 = new Vector3(p.x, LevelLayout.FloorY(r.Floor), p.y);
                    if (l.Plan.RoomAt(p3) != r) continue;
                    bool hole = false;
                    for (int f = r.Floor; f < r.Floor + r.Levels; f++) hole |= l.NearStair(f, p3, 0.8f);
                    if (!hole) list.Add(p);
                }
            if (list.Count == 0) list.Add(new Vector2(r.Center.x, r.Center.z));
            return list;
        }

        void BuildStair(Stair s)
        {
            var (mirrored, yaw) = StairGeometry.Placement(s.Corner);
            var go = Place(mirrored ? m_Art.StairsMirrored : m_Art.Stairs, $"Stairs_{s.Floor}", StairGeometry.Origin(s), Quaternion.Euler(0f, yaw, 0f));
            // the look: one per staircase shaft (the same on every floor), from the house seed
            if (go.TryGetComponent<StairLook>(out var look))
                look.Configure(LevelRandom.Mix(m_Layout.Seed, 0x57A1, Mathf.RoundToInt(s.Origin.x * 4f) * 1009 + Mathf.RoundToInt(s.Origin.y * 4f)));
        }

        /// <summary>Windows on the outer walls of every floor (visual only, blinds facing the room).</summary>
        void BuildFacade()
        {
            foreach (var (p, yaw) in WindowSpots(m_Layout))
            {
                Place(m_Art.Window, "Window", p, Quaternion.Euler(0f, yaw, 0f));
                Shell.Windows.Add((p, yaw));
            }
        }

        /// <summary>
        /// Windows along the straight outer walls (pure): every ~5 m where a window fits clear of the wall's ends and its
        /// openings; the yaw turns the window's interior side (local +Z) into the building.
        /// </summary>
        public static List<(Vector3 pos, float yaw)> WindowSpots(LevelLayout l)
        {
            var list = new List<(Vector3, float)>();
            var plan = l.Plan;
            const float spacing = 5f, clear = LevelMetrics.WindowWidth / 2f + 0.9f;
            for (int i = 0; i < plan.Walls.Count; i++)
            {
                var w = plan.Walls[i];
                if (!w.Exterior || w.Shaped || w.Railing || w.Length < LevelMetrics.WindowWidth + 1.8f) continue;
                var d = w.Dir;
                var n = new Vector2(-d.y, d.x);
                var mid = w.At(w.Length / 2f);
                var c = HousePlan.CellOf(mid + n * (HousePlan.Res * 0.5f));
                var k = plan.KindAt(w.Floor, c.x, c.y);
                if (k != CellKind.Room && k != CellKind.Void) n = -n;
                int count = Mathf.Max(1, Mathf.FloorToInt((w.Length - 1.8f) / spacing));
                var openings = plan.OpeningsOf(i);
                for (int j = 0; j < count; j++)
                {
                    float at = w.Length * (j + 0.5f) / count;
                    bool free = at > clear && at < w.Length - clear;
                    foreach (var o in openings) if (Mathf.Abs(o.At - at) < o.Width / 2f + clear) free = false;
                    if (!free) continue;
                    var p = w.At(at);
                    list.Add((new Vector3(p.x, LevelLayout.FloorY(w.Floor) + LevelMetrics.WindowCenterY, p.y), Mathf.Atan2(n.x, n.y) * Mathf.Rad2Deg));
                }
            }
            return list;
        }

        /// <summary>Static batching of everything that never moves (walls, slabs, stairs, windows). Doors stay dynamic.</summary>
        static void CombineStatic(GameObject root)
        {
            var list = new List<GameObject>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                if (r.GetComponentInParent<AutoDoor>() == null) list.Add(r.gameObject);
            StaticBatchingUtility.Combine(list.ToArray(), root);
        }
    }
}
