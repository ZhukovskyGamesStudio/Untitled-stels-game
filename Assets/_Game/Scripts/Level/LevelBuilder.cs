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
        static readonly List<Mesh> s_Meshes = new();

        /// <summary>Builds <paramref name="layout"/> from the prefabs in <paramref name="art"/>, replacing the previous level.</summary>
        public static GameObject Load(LevelLayout layout, LevelArt art)
        {
            Clear();
            Layout = layout;
            var builder = new LevelBuilder(art, layout);
            Root = builder.Build();
            Shell = builder.Shell;
            s_Props.AddRange(builder.Props);
            s_Meshes.AddRange(builder.Meshes);
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
            foreach (var m in s_Meshes)
                if (m != null)
                {
                    if (Application.isPlaying) Object.Destroy(m);
                    else Object.DestroyImmediate(m);
                }
            s_Meshes.Clear();
        }
    }

    /// <summary>Light of a room by its type (pure, unit tested).</summary>
    public static class RoomLighting
    {
        /// <summary>Ceiling panels sit this far either side of the room centre (along x).</summary>
        public const float LampOffset = 2.5f;

        public static (Color color, float intensity) For(RoomType room, bool dark, float dimShare = 0f)
        {
            if (dark && dimShare <= 0f) return (new Color(1f, 0.95f, 0.85f), 0f); // switched off: exit signs, lamps and the moon only
            if (dark) return (new Color(1f, 0.88f, 0.72f), LightMath.RoomIntensity * dimShare); // a dim lamp: a weak warm glow
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
        /// <summary>Meshes made while building (the city's): destroyed with the level.</summary>
        public List<Mesh> Meshes { get; } = new();

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
            float yard = CityPlan.Yard, far = 2f * CityPlan.Reach;
            // the ground outside is the street's level (a kerb under the house's floor); the pavements stand on it (BuildCity)
            Block(m_Art.OutsideGround, "OutsideGround", new Vector3(W / 2, CityPlan.RoadTop - 0.06f, H / 2), new Vector3(W + far, 0.1f, H + far));
            // the invisible wall round the yard; the street and the city beyond it are scenery (BuildCity)
            Block(m_Art.Fence, "Fence", new Vector3(W / 2, 4f, -yard - 0.5f), new Vector3(W + 2f * yard + 2f, 8f, 1f));
            Block(m_Art.Fence, "Fence", new Vector3(W / 2, 4f, H + yard + 0.5f), new Vector3(W + 2f * yard + 2f, 8f, 1f));
            Block(m_Art.Fence, "Fence", new Vector3(-yard - 0.5f, 4f, H / 2), new Vector3(1f, 8f, H + 2f * yard + 2f));
            Block(m_Art.Fence, "Fence", new Vector3(W + yard + 0.5f, 4f, H / 2), new Vector3(1f, 8f, H + 2f * yard + 2f));

            m_WindowCuts.Clear();
            foreach (var cut in WindowCuts(l))
            {
                if (!m_WindowCuts.TryGetValue(cut.wall, out var at)) m_WindowCuts[cut.wall] = at = new List<float>();
                at.Add(cut.at);
            }
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
                bool vault = i == l.VaultDoor && m_Art.VaultDoor != null;
                var rot = d.rotation;
                if (vault && l.VaultRoom >= 0 && Vector3.Dot(l.Plan.Rooms[l.VaultRoom].Center - d.position, d.rotation * Vector3.forward) < 0f)
                    rot *= Quaternion.Euler(0f, 180f, 0f); // the round door's +Z into the vault
                var door = Place(vault ? m_Art.VaultDoor : m_Art.AutoDoor, vault ? "VaultDoor" : "AutoDoor", d.position, rot).GetComponent<AutoDoor>();
                if (door != null)
                {
                    bool keyed = false;
                    foreach (var k in l.KeyDoors) keyed |= k.Door == i;
                    bool storage = i == l.StorageDoor && StorageRooms.Locked(l);
                    var inward = Vector3.zero;
                    var room = default(Rect);
                    if (storage)
                    {
                        // which way the room is: across the doorway, towards the storage room's middle
                        var across = d.rotation * Vector3.forward;
                        var to = l.Plan.Rooms[l.StorageRoom].Center - d.position;
                        inward = Vector3.Dot(to, across) >= 0f ? across : -across;
                        inward.y = 0f;
                        inward.Normalize();
                        room = l.Plan.Rooms[l.StorageRoom].Bounds;
                    }
                    door.Configure(i, l.LockedDoors.Contains(i), keyed, i == l.VaultDoor, storage, inward, room);
                    if (door.WallParts != null)
                        foreach (var part in door.WallParts)
                            if (part != null) Shell.WallParts.Add(part);
                }
                if (i == l.VaultDoor && m_Art.VaultPanel != null)
                {
                    // the vault's plate with its three lamps over the doorway, on both faces of the wall
                    var across = d.rotation * Vector3.forward;
                    foreach (float side in new[] { 1f, -1f })
                        Place(m_Art.VaultPanel, "VaultPanel", d.position + Vector3.up * (LevelMetrics.DoorHeight + 0.56f) + across * (side * (LevelMetrics.WallThickness / 2f + 0.01f)),
                            Quaternion.LookRotation(across * side));
                }
            }
            if (m_Art.VaultConsole != null)
                for (int i = 0; i < l.VaultConsoles.Count; i++)
                    Place(m_Art.VaultConsole, $"VaultConsole{i}", l.VaultConsoles[i].position, l.VaultConsoles[i].rotation).GetComponent<VaultConsole>()?.Configure(i);
            BuildFacade();
            if (Application.isPlaying) CombineStatic(root);
            BuildCity(W, H); // one mesh of its own: not static-batched

            for (int i = 0; i < l.Props.Count; i++)
            {
                var p = l.Props[i];
                var prefab = m_Art.Prop(p.Kind);
                if (prefab == null) continue;
                var go = Place(prefab, p.Kind.ToString(), p.Position, Quaternion.Euler(0, p.RotationY, 0));
                if (p.Scale > 0f) go.transform.localScale = Vector3.one * p.Scale;
                RecordProp(go, p.Kind, LevelLayout.FloorOf(p.Position.y));
                if (go.TryGetComponent<Items.SafeBox>(out var safe)) safe.Configure(i, l.Seed);
                if (go.TryGetComponent<Interactable>(out var use)) use.Configure(i);
                if (go.TryGetComponent<Items.Storage>(out var storage)) storage.Configure(i);
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

        /// <summary>Posts of the floor being built: by their place rounded to a centimetre (shared ends meet there), the exact place and the height.</summary>
        readonly Dictionary<Vector3, (Vector3 at, float height)> m_Posts = new();
        /// <summary>Where windows cut the outer walls: wall index → positions along it (<see cref="WindowCuts"/>).</summary>
        readonly Dictionary<int, List<float>> m_WindowCuts = new();
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
                // a post between two pieces of a bent wall turns with them: its faces run on with the wall's, no cube sticks out;
                // it stands at the walls' exact end (the rounded key moved it up to 5 mm out of the facade: a seam outside)
                var (p, h) = kv.Value;
                var at = new Vector2(p.x, p.z);
                var rot = SmoothJoint(at, out float yaw) ? Quaternion.Euler(0f, yaw, 0f) : (Quaternion?)null;
                Block(m_Art.WallBlock, "WallPost", p + Vector3.up * (y + h / 2f), new Vector3(t, h, t), rot);
            }
        }

        /// <summary>
        /// Past the post at this end of the wall another wall runs straight on (a T-junction seen from its flat side, a
        /// wall split where the room behind it changes): decoration of the two meets in the middle of the post.
        /// </summary>
        bool RunsOn(int index, Vector2 end)
        {
            if (!m_Ends.TryGetValue(EndKey(end), out var walls)) return false;
            var w = m_Plan.Walls[index];
            foreach (int i in walls)
            {
                if (i == index) continue;
                var o = m_Plan.Walls[i];
                if (o.Railing || Mathf.Abs(Vector2.Dot(o.Dir, w.Dir)) < 0.999f) continue;
                bool open = false;
                foreach (var op in m_Plan.OpeningsOf(i)) if (op.Kind == EdgeType.Open) open = true;
                if (!open) return true; // (an open passage is no wall: the post there is a free end)
            }
            return false;
        }

        void Post(Vector2 p, float height)
        {
            var key = new Vector3(Mathf.Round(p.x * 100f) / 100f, 0f, Mathf.Round(p.y * 100f) / 100f);
            m_Posts[key] = m_Posts.TryGetValue(key, out var was) ? (was.at, Mathf.Max(was.height, height)) : (new Vector3(p.x, 0f, p.y), height);
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
            // an outer wall runs up to the next floor (the roof): the slab's edge between the floors is behind it, the facade is one plane
            float height = w.FullHeight || w.Exterior ? LevelMetrics.FloorHeight : LevelMetrics.WallHeight;
            Post(w.A, height);
            Post(w.B, height);
            const float t = LevelMetrics.WallThickness;
            float len = w.Length;
            if (height < LevelMetrics.FloorHeight)
                foreach (var (u0, u1) in StairBands(w, m_Layout.Stairs))
                {
                    // over the stairwell no slab fills the band up to the next floor: the wall does
                    WallPiece(m_Art.WallBlock, a3 + d3 * u0, a3 + d3 * u1, LevelMetrics.WallHeight, LevelMetrics.FloorHeight);
                    if (u0 <= t) Post(w.A, LevelMetrics.FloorHeight);
                    if (u1 >= len - t) Post(w.B, LevelMetrics.FloorHeight);
                }
            var inner = InnerNormals(w);
            float from = t / 2f;
            bool jointA = SmoothJoint(w.A, out _), jointB = SmoothJoint(w.B, out _);
            bool cornerA = !jointA;
            m_StraightA = RunsOn(index, w.A);
            m_StraightB = RunsOn(index, w.B);
            // the cuts along the wall in order: the plan's openings and the windows (a real hole with glass in it)
            var cuts = new List<(float at, int opening)>();
            for (int k = 0; k < openings.Count; k++) cuts.Add((openings[k].At, k));
            if (m_WindowCuts.TryGetValue(index, out var windows)) foreach (float at in windows) cuts.Add((at, -1));
            cuts.Sort((x, y) => x.at.CompareTo(y.at));
            foreach (var cut in cuts)
            {
                if (cut.opening < 0)
                {
                    // a window: wall up to the sill, the hole (the Window prefab's glass fills it), wall above
                    float w0 = cut.at - LevelMetrics.WindowWidth / 2f, w1 = cut.at + LevelMetrics.WindowWidth / 2f;
                    float sillY = LevelMetrics.WindowCenterY - LevelMetrics.WindowHeight / 2f, topY = sillY + LevelMetrics.WindowHeight;
                    WallPiece(m_Art.WallBlock, a3 + d3 * from, a3 + d3 * w0, 0f, height);
                    RecordFaces(a3 + d3 * from, a3 + d3 * w0, 0f, height, w.Exterior, inner, cornerA, false, jointA && cornerA == false && from <= t / 2f + 0.001f);
                    WallPiece(m_Art.WallBlock, a3 + d3 * w0, a3 + d3 * w1, 0f, sillY);
                    RecordFaces(a3 + d3 * w0, a3 + d3 * w1, 0f, sillY, w.Exterior, inner, false, false);
                    WallPiece(m_Art.WallBlock, a3 + d3 * w0, a3 + d3 * w1, topY, height);
                    RecordFaces(a3 + d3 * w0, a3 + d3 * w1, topY, height, w.Exterior, inner, false, false);
                    from = w1;
                    cornerA = false;
                    continue;
                }
                var o = openings[cut.opening];
                if (o.Kind == EdgeType.Illusion)
                {
                    // the entrance of a hidden room: a wall to look at (with all its decor), nothing to bump into
                    float h0 = o.At - LevelMetrics.ClearWidth(o.Width) / 2f, h1 = o.At + LevelMetrics.ClearWidth(o.Width) / 2f;
                    WallPiece(m_Art.WallBlock, a3 + d3 * from, a3 + d3 * h0, 0f, height);
                    RecordFaces(a3 + d3 * from, a3 + d3 * h0, 0f, height, w.Exterior, inner, cornerA, false, jointA && cornerA == false && from <= t / 2f + 0.001f);
                    WallPiece(m_Art.IllusionBlock != null ? m_Art.IllusionBlock : m_Art.WallBlock, a3 + d3 * h0, a3 + d3 * h1, 0f, height);
                    RecordFaces(a3 + d3 * h0, a3 + d3 * h1, 0f, height, w.Exterior, inner, false, false);
                    from = h1;
                    cornerA = false;
                    continue;
                }
                bool vent = o.Kind == EdgeType.Vent, squeeze = o.Kind == EdgeType.Squeeze;
                bool vault = IsVaultDoorway(index, o);
                float clear = LevelMetrics.ClearWidth(vent ? LevelMetrics.VentWidth : squeeze ? LevelMetrics.SqueezeWidth
                    : vault ? LevelMetrics.VaultDoorWidth : LevelMetrics.DoorWidth);
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
                // over a street door the facade runs on (the door's frame paints the inside): no trim-coloured lintel outside
                WallPiece(w.Exterior ? m_Art.WallBlock : m_Art.TrimBlock, a3 + d3 * o0, a3 + d3 * o1, openH, height);
                RecordFaces(a3 + d3 * o0, a3 + d3 * o1, openH, height, w.Exterior, inner, false, false);
                var mid = a3 + d3 * o.At;
                Shell.Openings.Add(new WallOpening { Center = mid, Along = d3, Width = clear, Height = openH, Vent = vent, Exterior = w.Exterior, Squeeze = squeeze, Vault = vault, Floor = m_Floor });
                // The vent frame and the exit sign are modelled along local X; turn them to the wall direction.
                var along = Quaternion.LookRotation(new Vector3(-d3.z, 0, d3.x));
                if (vent) Place(o.High && m_Art.HighVentFrame != null ? m_Art.HighVentFrame : m_Art.VentFrame, "VentFrame", mid, along);
                else if (w.Exterior)
                {
                    // the street door's +Z out of the house: its leaves swing outwards
                    var outward = inner.Count > 0 ? -inner[0] : along * Vector3.forward;
                    Place(m_Art.ExteriorDoor != null ? m_Art.ExteriorDoor : m_Art.AutoDoor, "ExteriorDoor", mid, Quaternion.LookRotation(outward));
                }
                else if (squeeze) { if (m_Art.SqueezeFrame != null) Place(m_Art.SqueezeFrame, "SqueezeFrame", mid, along); }
                else if (m_Art.ExitSign != null && !vault) Place(m_Art.ExitSign, "ExitSign", mid + Vector3.up * (LevelMetrics.DoorHeight + 0.14f), along);
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

        /// <summary>The vault's doorway (<see cref="LevelLayout.VaultDoorway"/>) — wider, for the round bank door.</summary>
        bool IsVaultDoorway(int wall, PlanOpening o)
        {
            int v = m_Layout.VaultDoorway;
            if (v < 0 || v >= m_Plan.Openings.Count || m_Art.VaultDoor == null) return false;
            var vo = m_Plan.Openings[v];
            return vo.Wall == wall && Mathf.Abs(vo.At - o.At) < 0.01f && o.Kind == EdgeType.Door;
        }

        bool m_StraightA, m_StraightB; // of the wall being built (see RunsOn)

        void RecordFaces(Vector3 a, Vector3 b, float y0, float y1, bool outside, List<Vector3> sides, bool cornerA, bool cornerB,
            bool jointA = false, bool jointB = false)
        {
            if ((b - a).magnitude < 0.05f) return;
            foreach (var side in sides)
                Shell.Walls.Add(new WallFace
                {
                    A = a, B = b, Y0 = y0, Y1 = y1, Normal = side, Floor = m_Floor, Exterior = outside,
                    CornerA = cornerA, CornerB = cornerB, JointA = jointA, JointB = jointB,
                    StraightA = cornerA && m_StraightA, StraightB = cornerB && m_StraightB,
                });
        }

        /// <summary>
        /// Where a wall runs along the hole in the slab above it that the stairs leaving its floor climb through: spans
        /// along the wall (metres from A). Elsewhere the slab fills the band between the wall's top and the next floor; over
        /// the hole nothing did, and by an outer wall a strip of sky showed between the floors.
        /// </summary>
        public static List<(float from, float to)> StairBands(PlanWall w, IReadOnlyList<Stair> stairs)
        {
            var spans = new List<(float, float)>();
            const float step = 0.1f, reach = LevelMetrics.WallThickness / 2f + 0.15f;
            if (w.Railing) return spans;
            foreach (var s in stairs)
            {
                if (s.Floor != w.Floor) continue;
                var hole = PlanGenerator.StairArea(s, reach);
                float from = -1f, to = -1f;
                for (float u = 0f; u <= w.Length + step - 0.001f; u += step)
                {
                    float at = Mathf.Min(u, w.Length);
                    if (!hole.Contains(w.At(at))) continue;
                    if (from < 0f) from = at;
                    to = at;
                }
                if (from >= 0f && to - from > 0.05f) spans.Add((Mathf.Max(0f, from - step), Mathf.Min(w.Length, to + step)));
            }
            return spans;
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
            var (color, intensity) = RoomLighting.For(r.Type, r.Dark, m_Layout.DarkRoomsDim ? m_Layout.DimShare : 0f);
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
            var (mirrored, yaw) = StairGeometry.Placement(s.Corner, s.Flip);
            var go = Place(mirrored ? m_Art.StairsMirrored : m_Art.Stairs, $"Stairs_{s.Floor}", StairGeometry.Origin(s), Quaternion.Euler(0f, yaw, 0f));
            // the look: one per staircase shaft (the same on every floor), from the house seed
            if (go.TryGetComponent<StairLook>(out var look))
                look.Configure(LevelRandom.Mix(m_Layout.Seed, 0x57A1, Mathf.RoundToInt(s.Origin.x * 4f) * 1009 + Mathf.RoundToInt(s.Origin.y * 4f)));
        }

        /// <summary>The city round the yard (<see cref="CityPlan"/>): one mesh, a submesh per material, and the street lamps' lights.</summary>
        void BuildCity(float w, float h)
        {
            var mats = m_Art.CityMaterials;
            if (m_Art.CityPiece == null || mats == null || mats.Length == 0) return;
            var house = new Rect(0f, 0f, w, h);
            var lobby = m_Layout.Lobby.Center;
            var city = CityPlan.Make(house, CityPlan.SideNearest(house, new Vector2(lobby.x, lobby.z)), m_Layout.Floors, m_Layout.Seed);
            // the house's own corners (an L, a courtyard): pavement too
            foreach (var r in Rectangles(m_Plan.NX, m_Plan.NZ, (x, z) => m_Plan.KindAt(0, x, z) == CellKind.Outside))
            {
                var box = new CityBox(new Vector3(r.xMin * HousePlan.Res, -0.3f, r.yMin * HousePlan.Res), new Vector3(r.xMax * HousePlan.Res, CityPlan.WalkTop, r.yMax * HousePlan.Res),
                    CitySlot.Sidewalk, CitySlot.Sidewalk);
                city.Boxes.Add(box);
                city.Ground.Add(box.Bounds);
            }
            // what players reach (inside the invisible wall and a bit): the pavements to stand on, the buildings, cars, posts to bump into
            var reach = new Bounds(new Vector3(w / 2f, 0f, h / 2f), new Vector3(w + 2f * CityPlan.Yard + 2f, 100f, h + 2f * CityPlan.Yard + 2f));
            foreach (var b in city.Ground)
                if (b.Intersects(reach)) Block(m_Art.Fence, "Fence", b.center, b.size);
            foreach (var b in city.Solids)
                if (b.Intersects(reach)) Block(m_Art.Fence, "Fence", b.center, b.size);
            int slots = System.Enum.GetValues(typeof(CitySlot)).Length;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>[slots];
            for (int i = 0; i < slots; i++) tris[i] = new List<int>();
            foreach (var b in city.Boxes) CityPlan.AddFaces(b, verts, normals, uvs, tris);
            var mesh = new Mesh { name = "City", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            var used = new List<Material>();
            var subs = new List<List<int>>();
            for (int i = 0; i < slots; i++)
                if (tris[i].Count > 0 && i < mats.Length && mats[i] != null) { used.Add(mats[i]); subs.Add(tris[i]); }
            mesh.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i, false);
            mesh.RecalculateBounds();
            Meshes.Add(mesh);
            var go = Place(m_Art.CityPiece, "City", Vector3.zero, Quaternion.identity);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = used.ToArray();
            if (m_Art.CityLamp != null)
                foreach (var p in city.Lamps) Place(m_Art.CityLamp, "CityLamp", p, Quaternion.identity);
        }

        /// <summary>
        /// Windows in the outer walls of every floor: the wall has a real hole there (<see cref="Wall"/>), the prefab
        /// frames it with glass you see the yard, the gas and the sky through (it blocks bodies, not sight), blinds inside.
        /// </summary>
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
            foreach (var c in WindowCuts(l)) list.Add((c.pos, c.yaw));
            return list;
        }

        /// <summary>The windows as cuts in the walls (pure): where (centre, yaw) and in which wall, how far along it.</summary>
        public static List<(Vector3 pos, float yaw, int wall, float at)> WindowCuts(LevelLayout l)
        {
            var list = new List<(Vector3, float, int, float)>();
            var roomOf = new List<int>(); // the room each window lights (a kitchen keeps one when windows are few)
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
                    // a window opens into a room (or an atrium), never into a pocket behind a slanted or round wall
                    var inside = HousePlan.CellOf(p + n * (HousePlan.Res * 0.5f));
                    var ik = plan.KindAt(w.Floor, inside.x, inside.y);
                    if (ik != CellKind.Room && ik != CellKind.Void) continue;
                    // nobody looks into a restroom, a changing room or the vault from the street
                    if (ik == CellKind.Room)
                    {
                        int id = plan.IdAt(w.Floor, inside.x, inside.y);
                        if (id == l.VaultRoom || (id >= 0 && id < plan.Rooms.Count && plan.Rooms[id].Type is RoomType.Restroom or RoomType.LockerRoom)) continue;
                    }
                    roomOf.Add(plan.IdAt(w.Floor, inside.x, inside.y));
                    list.Add((new Vector3(p.x, LevelLayout.FloorY(w.Floor) + LevelMetrics.WindowCenterY, p.y), Mathf.Atan2(n.x, n.y) * Mathf.Rad2Deg, i, at));
                }
            }
            if (l.WindowLimit >= 0 && list.Count > l.WindowLimit)
            {
                // a few windows only: which ones from the house's own stream, kept in facade order
                var rng = new System.Random(LevelRandom.Mix(l.Seed, (int)RandomChannel.Windows, 1));
                var keep = new List<int>();
                for (int i = 0; i < list.Count; i++) keep.Add(i);
                for (int i = keep.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (keep[i], keep[j]) = (keep[j], keep[i]); }
                // every kitchen keeps a window: the first of each in the shuffled order goes to the front
                var lit = new HashSet<int>();
                var first = new List<int>();
                foreach (int i in keep)
                {
                    int r = roomOf[i];
                    if (r >= 0 && r < plan.Rooms.Count && plan.Rooms[r].Type == RoomType.Kitchen && lit.Add(r)) first.Add(i);
                }
                keep.RemoveAll(first.Contains);
                keep.InsertRange(0, first);
                keep.RemoveRange(l.WindowLimit, keep.Count - l.WindowLimit);
                keep.Sort();
                var picked = new List<(Vector3, float, int, float)>();
                foreach (int i in keep) picked.Add(list[i]);
                return picked;
            }
            return list;
        }

        /// <summary>Static batching of everything that never moves (walls, slabs, stairs, windows). Doors stay dynamic.</summary>
        static void CombineStatic(GameObject root)
        {
            var list = new List<GameObject>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.GetComponentInParent<AutoDoor>() != null) continue;
                // the unchanging shell is merged per room cell instead (PropBatches): static batching would keep each renderer
                if (PropBatches.IsShell(r, root.transform) && MeshBatcher.CanMerge(r, out _)) continue;
                list.Add(r.gameObject);
            }
            StaticBatchingUtility.Combine(list.ToArray(), root);
        }
    }
}
