using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>The furnisher: walkability: the walk grid, what cuts a room off, what is taken out (see <see cref="Furnisher"/>).</summary>
    public sealed partial class Furnisher
    {
        // ------------------------------------------------------------------ walkability

        /// <summary>
        /// The middle of this room is for its furniture (a meeting room's table, a restroom's stalls): the patrol stands
        /// by the door instead (<see cref="LevelGenerator.PatrolSpot"/>) and "reached" means any free spot near the middle.
        /// </summary>
        public static bool MiddleIsFurniture(PlanRoom r) => RoomTraits.MiddleIsFurniture(r.Type);

        /// <summary>A walker has to go around this piece (it is not on a top, up on a wall or flat on the floor).</summary>
        static bool InTheWay(PropKind k) =>
            k is not (PropKind.Partition or PropKind.DeskLamp or PropKind.CoffeeMachine or PropKind.Hatch or PropKind.ShaftHatch or PropKind.Microwave
                or PropKind.WallCabinet or PropKind.HandDryer or PropKind.AcousticPanel or PropKind.DartBoard or PropKind.AreaRug
                or PropKind.BayLines or PropKind.BedCurtain or PropKind.SurgicalLights) && !Hung.Contains(k);

        // The room being furnished, as the walkability check will see it (same grid, same margins): its free cells
        // without furniture, and the spots a walker must still get between: its middle, the inner side of each of its
        // doorways and open passages, the ends of its stairs.
        PlanRoom m_WalkRoom;
        int m_WX, m_WZ, m_WNX, m_WNZ;
        bool[] m_WBare, m_WFree, m_WSeen;
        readonly List<int> m_WTargets = new();
        readonly List<int> m_WStack = new();
        readonly List<Rect> m_WRects = new();
        const float WalkCell = 0.25f, WalkAgent = 0.4f, WalkClutter = 0.55f; // those of Walk

        void BeginRoomWalk(PlanRoom room)
        {
            m_WalkRoom = room;
            var b = room.Bounds;
            m_WX = Mathf.FloorToInt(b.xMin / WalkCell);
            m_WZ = Mathf.FloorToInt(b.yMin / WalkCell);
            m_WNX = Mathf.CeilToInt(b.xMax / WalkCell) - m_WX;
            m_WNZ = Mathf.CeilToInt(b.yMax / WalkCell) - m_WZ;
            int n = m_WNX * m_WNZ;
            if (m_WBare == null || m_WBare.Length < n) { m_WBare = new bool[n]; m_WFree = new bool[n]; m_WSeen = new bool[n]; }
            float reach = LevelMetrics.WallThickness / 2f + WalkAgent - 0.01f;
            for (int z = 0; z < m_WNZ; z++)
                for (int x = 0; x < m_WNX; x++)
                {
                    float cx = (m_WX + x + 0.5f) * WalkCell, cz = (m_WZ + z + 0.5f) * WalkCell;
                    // this room's floor, a walker's half width off everything that is not (walls; other rooms count too:
                    // the way through a doorway is checked by the whole-house pass, here only the way to it)
                    m_WBare[z * m_WNX + x] = InRoom(room.Floor, room.Id, cx, cz)
                        && InRoom(room.Floor, room.Id, cx - reach, cz) && InRoom(room.Floor, room.Id, cx + reach, cz)
                        && InRoom(room.Floor, room.Id, cx, cz - reach) && InRoom(room.Floor, room.Id, cx, cz + reach);
                }
            // a staircase in the room is solid, as in the whole-house pass (Walk): else a piece beside it could close the way
            // round it and only the final check would see it (taking half the house's furniture with it)
            foreach (var st in m_L.Stairs)
            {
                if (st.Floor != room.Floor && st.Floor + 1 != room.Floor) continue;
                var sa = PlanGenerator.StairArea(st, WalkAgent + 0.05f);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(sa.xMin / WalkCell) - m_WX), x1 = Mathf.Min(m_WNX - 1, Mathf.FloorToInt(sa.xMax / WalkCell) - m_WX);
                int z0 = Mathf.Max(0, Mathf.FloorToInt(sa.yMin / WalkCell) - m_WZ), z1 = Mathf.Min(m_WNZ - 1, Mathf.FloorToInt(sa.yMax / WalkCell) - m_WZ);
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                        m_WBare[z * m_WNX + x] = false;
            }
            m_WTargets.Clear();
            Stamp(null); // the targets are cells a walker can stand on now (with the hatch, the vents' desks, the cage)
            void Target(Vector3 p, int reach = 4)
            {
                // the free cell nearest to the spot, within 1 m (a stair's end and the middle: 1.5 m, as the whole-house pass has them)
                int best = -1;
                float bestD = reach * WalkCell;
                int px = Mathf.FloorToInt(p.x / WalkCell) - m_WX, pz = Mathf.FloorToInt(p.z / WalkCell) - m_WZ;
                for (int dz = -reach; dz <= reach; dz++)
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        int x = px + dx, z = pz + dz;
                        if (x < 0 || z < 0 || x >= m_WNX || z >= m_WNZ || !m_WFree[z * m_WNX + x]) continue;
                        float d = new Vector2((m_WX + x + 0.5f) * WalkCell - p.x, (m_WZ + z + 0.5f) * WalkCell - p.z).magnitude;
                        if (d < bestD) { bestD = d; best = z * m_WNX + x; }
                    }
                if (best >= 0 && !m_WTargets.Contains(best)) m_WTargets.Add(best);
            }
            if (!MiddleIsFurniture(room)) Target(room.Center, 6); // as Walk.Reached: the free cell nearest the middle within 1.5 m
            foreach (var d in m_L.Doorways)
            {
                if (d.Floor != room.Floor || d.Kind is not (EdgeType.Door or EdgeType.Open or EdgeType.Breach)) continue;
                var across = d.Rotation * Vector3.forward;
                foreach (float side in new[] { -0.75f, 0.75f })
                {
                    var p = d.Position + across * side;
                    if (InRoom(room.Floor, room.Id, p.x, p.z)) Target(p);
                }
            }
            foreach (var s in m_L.Stairs)
            {
                var (bottom, top) = LevelLayout.StairEnds(s);
                if (s.Floor == room.Floor && InRoom(room.Floor, room.Id, bottom.x, bottom.z)) Target(bottom, 6);
                if (s.Floor + 1 == room.Floor && InRoom(room.Floor, room.Id, top.x, top.z)) Target(top, 6);
            }
            Baseline();
        }

        /// <summary>The room's free cells around the furniture already placed (plus <paramref name="more"/>), into m_WFree.</summary>
        void Stamp(List<Rect> more)
        {
            System.Array.Copy(m_WBare, m_WFree, m_WNX * m_WNZ);
            m_WRects.Clear();
            m_Solid[m_WalkRoom.Floor].Query(Grow(m_WalkRoom.Bounds, 1f), m_WRects);
            if (more != null) m_WRects.AddRange(more);
            foreach (var r in m_WRects)
            {
                var g = Grow(r, WalkClutter);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(g.xMin / WalkCell) - m_WX), x1 = Mathf.Min(m_WNX - 1, Mathf.FloorToInt(g.xMax / WalkCell) - m_WX);
                int z0 = Mathf.Max(0, Mathf.FloorToInt(g.yMin / WalkCell) - m_WZ), z1 = Mathf.Min(m_WNZ - 1, Mathf.FloorToInt(g.yMax / WalkCell) - m_WZ);
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                        m_WFree[z * m_WNX + x] = false;
            }
        }

        /// <summary>The free cells of m_WFree reached from <paramref name="start"/>, into m_WSeen.</summary>
        void Flood(int start)
        {
            System.Array.Clear(m_WSeen, 0, m_WNX * m_WNZ);
            if (!m_WFree[start]) return;
            m_WStack.Clear();
            m_WStack.Add(start);
            m_WSeen[start] = true;
            while (m_WStack.Count > 0)
            {
                int i = m_WStack[m_WStack.Count - 1];
                m_WStack.RemoveAt(m_WStack.Count - 1);
                int x = i % m_WNX, z = i / m_WNX;
                if (x > 0 && m_WFree[i - 1] && !m_WSeen[i - 1]) { m_WSeen[i - 1] = true; m_WStack.Add(i - 1); }
                if (x < m_WNX - 1 && m_WFree[i + 1] && !m_WSeen[i + 1]) { m_WSeen[i + 1] = true; m_WStack.Add(i + 1); }
                if (z > 0 && m_WFree[i - m_WNX] && !m_WSeen[i - m_WNX]) { m_WSeen[i - m_WNX] = true; m_WStack.Add(i - m_WNX); }
                if (z < m_WNZ - 1 && m_WFree[i + m_WNX] && !m_WSeen[i + m_WNX]) { m_WSeen[i + m_WNX] = true; m_WStack.Add(i + m_WNX); }
            }
        }

        /// <summary>
        /// The baseline: of the targets, the biggest set the furniture already there (the hatch, desks under vents, the
        /// hostage's cage) leaves linked is what every group must keep linked; the others are dropped. Flooding only from the
        /// first target let a cage over the room's middle drop every doorway: then nothing in that room was checked at all
        /// and the whole-house pass took out a dozen groups round it.
        /// </summary>
        void Baseline()
        {
            Stamp(null);
            m_WTargets.RemoveAll(t => !m_WFree[t]);
            if (m_WTargets.Count < 2) return;
            int best = 0, bestCount = 0;
            for (int k = 0; k < m_WTargets.Count && bestCount < m_WTargets.Count; k++)
            {
                Flood(m_WTargets[k]);
                int count = 0;
                foreach (int t in m_WTargets) if (m_WSeen[t]) count++;
                if (count > bestCount) { bestCount = count; best = k; }
            }
            Flood(m_WTargets[best]);
            int first = m_WTargets[best];
            m_WTargets.RemoveAll(t => !m_WSeen[t]);
            m_WTargets.Remove(first);
            m_WTargets.Insert(0, first);
        }

        /// <summary>With <paramref name="more"/> footprints added, every target is still reached from the first.</summary>
        bool Linked(List<Rect> more, List<int> keep)
        {
            if (keep.Count < 2) return true;
            Stamp(more);
            Flood(keep[0]);
            foreach (int t in keep) if (!m_WSeen[t]) return false;
            return true;
        }

        /// <summary>
        /// With these footprints added, a walker still gets from the middle of the room to each of its doorways and
        /// stairs: checked when a group is placed, so a group that would wall something off is never put there (the
        /// whole-house check after the furnishing used to take it out again, with its neighbours: rooms went bare).
        /// </summary>
        bool StaysWalkable(PlanRoom room, List<Rect> rects)
        {
            if (m_WalkRoom != room) return true; // pieces put outside FurnishRoom (tests, the features)
            return Linked(rects, m_WTargets);
        }

        /// <summary>
        /// The safety net under all the rules above: a walker 0.8 m wide (a robot) must get from the hatch to the middle of
        /// every room. Checked on a 0.25 m grid of each floor: the rooms' floors minus a band along the solid walls and
        /// around the furniture, linked through doorways and open passages and between floors by the stairs. Where rooms
        /// stay out of reach, the furniture groups that wall them off go (with their loot), until everything is reachable.
        /// </summary>
        /// <summary>Furniture groups the last walkability check took out (for tests and logs).</summary>
        /// (Per thread: the next house is generated in the background during the drive while the main thread may generate a preview.)
        public static int LastRemoved { get => s_LastRemoved; private set => s_LastRemoved = value; }
        public static int LastDroppedItems { get => s_LastDropped; private set => s_LastDropped = value; }
        public static List<string> LastNotes => s_LastNotes ??= new List<string>();
        /// <summary>The pieces the last walkability check took out (for the space audit and tests).</summary>
        public static List<(PropKind kind, Vector3 pos)> LastCut => s_LastCut ??= new List<(PropKind, Vector3)>();
        [System.ThreadStatic] static int s_LastRemoved, s_LastDropped;
        [System.ThreadStatic] static List<string> s_LastNotes;
        [System.ThreadStatic] static List<(PropKind, Vector3)> s_LastCut;

        /// <summary>Rooms whose middle cannot be walked to from the hatch (walls, doorways, stairs, furniture placed so far).</summary>
        public List<PlanRoom> Unreached()
        {
            var grid = new Walk(m_L, this);
            var reached = grid.Flood();
            var list = new List<PlanRoom>();
            foreach (var r in m_L.Rooms) if (r.Id != m_L.HiddenRoom && !grid.Reached(reached, r.Center)) list.Add(r);
            return list;
        }

        /// <summary>Takes the walk grid of the bare building from another furnisher of the same layout (it is built only once).</summary>
        public void ShareWalk(Furnisher other)
        {
            if (other.m_L != m_L) return;
            m_WalkFree = other.m_WalkFree;
            m_WalkOwner = other.m_WalkOwner;
        }

        public void EnsureWalkable()
        {
            LastRemoved = 0;
            LastDroppedItems = 0;
            LastNotes.Clear();
            LastCut.Clear();
            for (int round = 0; round < 40; round++)
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                var grid = new Walk(m_L, this);
                long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                var reached = grid.Flood();
                long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
                var lostRooms = new List<PlanRoom>();
                // the walker is a robot: it never gets into the hidden room (players do, through its illusion walls)
                foreach (var r in m_L.Rooms) if (r.Id != m_L.HiddenRoom && !grid.Reached(reached, r)) lostRooms.Add(r);
                long t3 = System.Diagnostics.Stopwatch.GetTimestamp();
                var cut = lostRooms.Count > 0 ? grid.Barrier(reached, lostRooms) : new HashSet<int>();
                long t4 = System.Diagnostics.Stopwatch.GetTimestamp();
                long tick = System.Diagnostics.Stopwatch.Frequency / 1000;
                LevelGenerator.Timing.Grid += (t1 - t0) / tick; LevelGenerator.Timing.Flood += (t2 - t1) / tick;
                LevelGenerator.Timing.Reach += (t3 - t2) / tick; LevelGenerator.Timing.Barrier += (t4 - t3) / tick;
                LevelGenerator.Timing.Rounds++;
                if (cut.Count == 0)
                {
                    foreach (var s in m_L.Stairs)
                        LastNotes.Add($"stair from {s.Floor} room {s.Room} reached {grid.Reached(reached, m_L.Rooms[s.Room].Center)} -> room {s.RoomAbove} reached {grid.Reached(reached, m_L.Rooms[s.RoomAbove].Center)}");
                    foreach (var r in m_L.Rooms)
                        if (!grid.Reached(reached, r)) LastNotes.Add($"lost #{r.Id} {r.Type} f{r.Floor} {r.Center}");
                    // loot nobody can reach (in a niche walled off by furniture) is not left there
                    for (int i = m_L.ItemSpots.Count - 1; i >= 0; i--)
                    {
                        var p = m_L.ItemSpots[i];
                        var floor = new Vector3(p.x, LevelLayout.FloorY(LevelLayout.FloorOf(p.y)), p.z);
                        bool hidden = m_L.HiddenRoom >= 0 && m_L.Plan.RoomAt(floor + Vector3.up * 0.3f)?.Id == m_L.HiddenRoom;
                        if (!hidden && !grid.Reached(reached, floor)) { m_L.ItemSpots.RemoveAt(i); m_ItemGroup.RemoveAt(i); LastDroppedItems++; }
                    }
                    return;
                }
                LastRemoved += cut.Count;
                RemoveGroups(cut);
            }
        }

        /// <summary>
        /// After <see cref="EnsureWalkable"/>: a kitchen whose run (with its fridge) had to go, because it stood in the way
        /// between doors, gets a fridge of its own back — against any wall, with a walkway in front of it. True if one was
        /// put back (then check the walking again).
        /// </summary>
        public bool RestoreFridges()
        {
            bool any = false;
            foreach (var room in m_L.Rooms)
            {
                if (room.Type != RoomType.Kitchen) continue;
                bool has = false;
                foreach (var p in m_L.Props) has |= p.Kind == PropKind.Fridge && m_P.RoomAt(p.Position + Vector3.up * 0.3f) == room;
                if (has) continue;
                m_Fridge = false;
                int before = m_L.Props.Count;
                AgainstAnyWall(room, PropKind.Fridge, walkway: true);
                any |= m_L.Props.Count > before;
            }
            return any;
        }

        void RemoveGroups(HashSet<int> groups)
        {
            for (int i = m_L.Props.Count - 1; i >= 0; i--)
                if (m_GroupOf[i] >= 0 && groups.Contains(m_GroupOf[i]))
                {
                    LastCut.Add((m_L.Props[i].Kind, m_L.Props[i].Position));
                    m_L.Props.RemoveAt(i);
                    m_GroupOf.RemoveAt(i);
                }
            for (int i = m_L.ItemSpots.Count - 1; i >= 0; i--)
                if (groups.Contains(m_ItemGroup[i])) { m_L.ItemSpots.RemoveAt(i); m_ItemGroup.RemoveAt(i); }
            for (int f = 0; f < m_Taken.Length; f++) { m_Taken[f].Clear(); m_Solid[f].Clear(); }
            foreach (var p in m_L.Props)
            {
                if (p.Kind is not (PropKind.Partition or PropKind.DeskLamp or PropKind.CoffeeMachine))
                    m_Taken[LevelLayout.FloorOf(p.Position.y)].Add(FootAt(p.Kind, p.Position, p.RotationY));
                if (InTheWay(p.Kind)) m_Solid[LevelLayout.FloorOf(p.Position.y)].Add(FootAt(p.Kind, p.Position, p.RotationY));
            }
        }

        /// <summary>The walkable grid of the house for one check.</summary>
        sealed class Walk
        {
            const float S = 0.25f, Agent = 0.4f, Clutter = 0.55f; // a robot's half width; around furniture a little more (the baked navmesh erodes more there)
            readonly LevelLayout m_L;
            readonly int m_NX, m_NZ, m_F;
            readonly bool[] m_Free;
            readonly int[] m_Owner; // the furniture group blocking a cell (−1 none, −2 wall)

            public Walk(LevelLayout l, Furnisher fur)
            {
                m_L = l;
                m_F = l.Floors;
                m_NX = Mathf.CeilToInt(l.SizeX / S);
                m_NZ = Mathf.CeilToInt(l.SizeZ / S);
                // the building itself (floors, walls, stairs) does not change while furniture is taken out: built once per
                // furnisher, copied for every check, and only the furniture is stamped on the copy
                if (fur.m_WalkFree != null)
                {
                    m_Free = (bool[])fur.m_WalkFree.Clone();
                    m_Owner = (int[])fur.m_WalkOwner.Clone();
                    Furniture(l, fur);
                    return;
                }
                m_Free = new bool[m_F * m_NX * m_NZ];
                m_Owner = new int[m_F * m_NX * m_NZ];
                for (int f = 0; f < m_F; f++)
                    for (int x = 0; x < m_NX; x++)
                        for (int z = 0; z < m_NZ; z++)
                        {
                            int i = Index(f, x, z);
                            m_Owner[i] = -1;
                            var c = Center(x, z);
                            m_Free[i] = l.Plan.IsFloor(f, c);
                        }
                // solid walls (openings cut out) with the walker's half width around them
                for (int w = 0; w < l.Plan.Walls.Count; w++)
                {
                    var wall = l.Plan.Walls[w];
                    var cuts = new List<(float a, float b)>();
                    bool open = false;
                    foreach (var o in l.Plan.OpeningsOf(w))
                    {
                        if (o.Kind == EdgeType.Open) open = true;
                        else if (o.Kind is EdgeType.Door or EdgeType.Breach) cuts.Add((o.At - LevelMetrics.ClearWidth(o.Width) / 2f, o.At + LevelMetrics.ClearWidth(o.Width) / 2f));
                    }
                    if (open) continue;
                    cuts.Sort();
                    float from = 0f;
                    foreach (var (a, b) in cuts) { Band(wall.Floor, wall.At(from), wall.At(a), LevelMetrics.WallThickness / 2f + Agent, -2); from = b; }
                    Band(wall.Floor, wall.At(from), wall.At(wall.Length), LevelMetrics.WallThickness / 2f + Agent, -2);
                }
                // the stairs' squares (their flights are walked through the links below)
                foreach (var s in l.Stairs)
                {
                    var a = PlanGenerator.StairArea(s, Agent + 0.05f); // the flights and rails, with a robot's half width
                    Box(s.Floor, a, -2);
                    if (s.Floor + 1 < m_F) Box(s.Floor + 1, a, -2);
                }
                fur.m_WalkFree = (bool[])m_Free.Clone();
                fur.m_WalkOwner = (int[])m_Owner.Clone();
                Furniture(l, fur);
            }

            void Furniture(LevelLayout l, Furnisher fur)
            {
                for (int i = 0; i < l.Props.Count; i++)
                {
                    var p = l.Props[i];
                    if (!InTheWay(p.Kind)) continue; // on a top or up on a wall, or flat on the floor
                    var r = FootAt(p.Kind, p.Position, p.RotationY);
                    Box(LevelLayout.FloorOf(p.Position.y), Grow(r, Clutter), fur.m_GroupOf[i] >= 0 ? fur.m_GroupOf[i] : -2);
                }
            }

            int Index(int f, int x, int z) => (f * m_NZ + z) * m_NX + x;
            static Vector2 Center(int x, int z) => new((x + 0.5f) * S, (z + 0.5f) * S);
            Vector2Int CellOf(Vector2 p) => new(Mathf.FloorToInt(p.x / S), Mathf.FloorToInt(p.y / S));
            bool In(int f, int x, int z) => f >= 0 && f < m_F && x >= 0 && z >= 0 && x < m_NX && z < m_NZ;

            void Block(int f, int x, int z, int owner)
            {
                if (!In(f, x, z)) return;
                int i = Index(f, x, z);
                if (!m_Free[i] && m_Owner[i] == -2) return;
                m_Free[i] = false;
                if (m_Owner[i] != -2) m_Owner[i] = owner == -2 ? -2 : (m_Owner[i] == -1 ? owner : m_Owner[i]);
            }

            void Box(int f, Rect r, int owner)
            {
                var a = CellOf(r.min);
                var b = CellOf(r.max);
                for (int x = a.x; x <= b.x; x++)
                    for (int z = a.y; z <= b.y; z++)
                        if (r.Overlaps(new Rect(x * S, z * S, S, S))) Block(f, x, z, owner);
            }

            void Band(int f, Vector2 a, Vector2 b, float half, int owner)
            {
                float len = (b - a).magnitude;
                var box = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - half, Mathf.Min(a.y, b.y) - half, Mathf.Max(a.x, b.x) + half, Mathf.Max(a.y, b.y) + half);
                var lo = CellOf(box.min);
                var hi = CellOf(box.max);
                for (int x = lo.x; x <= hi.x; x++)
                    for (int z = lo.y; z <= hi.y; z++)
                    {
                        var c = Center(x, z);
                        float t = len > 1e-4f ? Mathf.Clamp(Vector2.Dot(c - a, (b - a) / len), 0f, len) : 0f;
                        var near = len > 1e-4f ? a + (b - a) / len * t : a;
                        if ((c - near).magnitude < half) Block(f, x, z, owner);
                    }
            }

            /// <summary>Cells reached from the hatch (stairs link their ends on two floors).</summary>
            public bool[] Flood()
            {
                var seen = new bool[m_Free.Length];
                var q = new Queue<int>();
                void Seed(Vector3 p)
                {
                    int f = LevelLayout.FloorOf(p.y);
                    var c = Nearest(f, new Vector2(p.x, p.z));
                    if (c < 0 || seen[c]) return;
                    seen[c] = true;
                    q.Enqueue(c);
                }
                Seed(m_L.HatchPosition + new Vector3(1.6f, 0f, 0f));
                Seed(m_L.HatchPosition - new Vector3(1.6f, 0f, 0f));
                Seed(m_L.HatchPosition + new Vector3(0f, 0f, 1.6f));
                Seed(m_L.HatchPosition - new Vector3(0f, 0f, 1.6f));
                void Visit(int j)
                {
                    if (seen[j] || !m_Free[j]) return;
                    seen[j] = true;
                    q.Enqueue(j);
                }
                bool more = true;
                var linked = new HashSet<int>();
                while (more)
                {
                    while (q.Count > 0)
                    {
                        int i = q.Dequeue();
                        int x = i % m_NX, z = i / m_NX % m_NZ;
                        if (x > 0) Visit(i - 1);
                        if (x < m_NX - 1) Visit(i + 1);
                        if (z > 0) Visit(i - m_NX);
                        if (z < m_NZ - 1) Visit(i + m_NX);
                    }
                    more = false;
                    for (int k = 0; k < m_L.Stairs.Count; k++)
                    {
                        if (linked.Contains(k)) continue;
                        var s = m_L.Stairs[k];
                        var (bottom, top) = LevelLayout.StairEnds(s);
                        int bi = Nearest(s.Floor, new Vector2(bottom.x, bottom.z), s.Room);
                        int ti = Nearest(s.Floor + 1, new Vector2(top.x, top.z), s.RoomAbove);
                        if (bi < 0 || ti < 0) { LastNotes.Add($"stair {k} floor {s.Floor}: bottom {bottom} cell {bi} room {s.Room} space {m_L.Plan.SpaceAt(s.Floor, new Vector2(bottom.x, bottom.z))}, top {top} cell {ti} room {s.RoomAbove} space {m_L.Plan.SpaceAt(s.Floor + 1, new Vector2(top.x, top.z))}"); continue; }
                        if (seen[bi] && !seen[ti]) { seen[ti] = true; q.Enqueue(ti); linked.Add(k); more = true; }
                        else if (seen[ti] && !seen[bi]) { seen[bi] = true; q.Enqueue(bi); linked.Add(k); more = true; }
                    }
                }
                return seen;
            }

            /// <summary>The free cell nearest to a point of a floor (within 1.5 m, or <paramref name="reach"/> cells; of that room when given), or −1.</summary>
            int Nearest(int f, Vector2 p, int room = -1, int reach = 6)
            {
                var c = CellOf(p);
                int best = -1;
                float bestD = reach * S;
                for (int dx = -reach; dx <= reach; dx++)
                    for (int dz = -reach; dz <= reach; dz++)
                    {
                        if (!In(f, c.x + dx, c.y + dz)) continue;
                        int i = Index(f, c.x + dx, c.y + dz);
                        if (!m_Free[i]) continue;
                        if (room >= 0 && m_L.Plan.SpaceAt(f, Center(c.x + dx, c.y + dz)) != room) continue;
                        float d = (Center(c.x + dx, c.y + dz) - p).magnitude;
                        if (d < bestD) { bestD = d; best = i; }
                    }
                return best;
            }

            public bool Reached(bool[] seen, Vector3 p, int reach = 6)
            {
                int f = LevelLayout.FloorOf(p.y);
                int i = Nearest(f, new Vector2(p.x, p.z), m_L.Plan.SpaceAt(f, new Vector2(p.x, p.z)), reach);
                return i >= 0 && seen[i];
            }

            /// <summary>
            /// A walker gets into the room: to its middle, or, where the middle is furniture (a meeting room's table, a
            /// restroom's stalls, the hostage's cage) and no free floor lies near it, to any of the room's free floor —
            /// as the check while furnishing has it (StaysWalkable: the doors, not the middle). Asking for a spot within
            /// 3.5 m of the middle stripped long meeting rooms with their door at the short end of the table and chairs.
            /// </summary>
            public bool Reached(bool[] seen, PlanRoom r)
            {
                if (!MiddleIsFurniture(r))
                {
                    int i = Nearest(r.Floor, new Vector2(r.Center.x, r.Center.z), r.Id);
                    if (i >= 0) return seen[i];
                }
                return AnyReached(seen, r);
            }

            /// <summary>
            /// At least half of the room's free floor is reached (a room whose middle is furniture: the table must not cut it in two —
            /// with the thick walls a meeting room 4 m wide has no way round its table left); a room with almost no free floor:
            /// any of it.
            /// </summary>
            bool AnyReached(bool[] seen, PlanRoom r)
            {
                var a = CellOf(r.Bounds.min);
                var b = CellOf(r.Bounds.max);
                int free = 0, reached = 0;
                for (int x = a.x; x <= b.x; x++)
                    for (int z = a.y; z <= b.y; z++)
                    {
                        if (!In(r.Floor, x, z)) continue;
                        int i = Index(r.Floor, x, z);
                        if (!m_Free[i] || m_L.Plan.SpaceAt(r.Floor, Center(x, z)) != r.Id) continue;
                        free++;
                        if (seen[i]) reached++;
                    }
                return reached > 0 && (free < 12 || reached >= free * 0.5f);
            }

            const int Reach = 6; // 1.5 m in cells: how close a blocking group has to be to both sides

            /// <summary>
            /// The furniture between the reached part of the house and the rooms out of reach: groups owning blocked cells
            /// within 1.5 m (Chebyshev, same floor) of both a reached cell and a free cell on the lost side (flooded from
            /// the lost rooms' middles). One flood of the lost side, two summed-area tables, then one look per blocked cell.
            /// </summary>
            public HashSet<int> Barrier(bool[] seen, List<PlanRoom> rooms)
            {
                var found = new HashSet<int>();
                int n = m_Free.Length, plane = m_NX * m_NZ;
                // the lost side: free unreached cells connected to a lost room's middle
                var lost = new bool[n];
                var stack = new Stack<int>();
                foreach (var room in rooms)
                {
                    int start = Nearest(room.Floor, new Vector2(room.Center.x, room.Center.z));
                    if (start < 0 || seen[start] || lost[start]) continue;
                    lost[start] = true;
                    stack.Push(start);
                }
                // and the unreached ends of stairs: furniture can wall off the approach to a flight inside a room that is
                // reached itself (a narrow strip beside the stair), and then the whole floor beyond is lost
                foreach (var s in m_L.Stairs)
                {
                    var (bottom, top) = LevelLayout.StairEnds(s);
                    foreach (int end in new[] { Nearest(s.Floor, new Vector2(bottom.x, bottom.z), s.Room), Nearest(s.Floor + 1, new Vector2(top.x, top.z), s.RoomAbove) })
                    {
                        if (end < 0 || seen[end] || lost[end]) continue;
                        lost[end] = true;
                        stack.Push(end);
                    }
                }
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    int x = i % m_NX, z = i / m_NX % m_NZ;
                    if (x > 0) Visit(i - 1);
                    if (x < m_NX - 1) Visit(i + 1);
                    if (z > 0) Visit(i - m_NX);
                    if (z < m_NZ - 1) Visit(i + m_NX);
                }
                void Visit(int j)
                {
                    if (!m_Free[j] || seen[j] || lost[j]) return;
                    lost[j] = true;
                    stack.Push(j);
                }
                var near = new List<int>();
                var nearSeen = Table(seen);
                var nearLost = Table(lost);
                int w = m_NX + 1;
                for (int f = 0; f < m_F; f++)
                {
                    int tb = f * w * (m_NZ + 1);
                    for (int z = 0; z < m_NZ; z++)
                    {
                        int z0 = Mathf.Max(0, z - Reach), z1 = Mathf.Min(m_NZ, z + Reach + 1);
                        for (int x = 0; x < m_NX; x++)
                        {
                            int i = f * plane + z * m_NX + x;
                            if (m_Free[i] || m_Owner[i] < 0 || found.Contains(m_Owner[i])) continue;
                            int x0 = Mathf.Max(0, x - Reach), x1 = Mathf.Min(m_NX, x + Reach + 1);
                            int a = tb + z0 * w + x0, b = tb + z0 * w + x1, c = tb + z1 * w + x0, d = tb + z1 * w + x1;
                            if (nearSeen[d] - nearSeen[b] - nearSeen[c] + nearSeen[a] > 0 &&
                                nearLost[d] - nearLost[b] - nearLost[c] + nearLost[a] > 0) { found.Add(m_Owner[i]); near.Add(i); }
                        }
                    }
                }
                // ...and of those, the groups that stand between the two sides in their own room: the reached and the lost
                // floor near them on their side of the walls. Counted through walls, a wing out of reach took with it every
                // piece standing against the far side of a wall (toilet stalls, a hospital bed, lab benches), not just the
                // one in the way.
                var mine = new HashSet<int>();
                foreach (int i in near)
                {
                    if (mine.Contains(m_Owner[i])) continue;
                    int f = i / plane, x = i % m_NX, z = i / m_NX % m_NZ;
                    int space = m_L.Plan.SpaceAt(f, Center(x, z));
                    bool r = false, l = false;
                    for (int dz = -Reach; dz <= Reach && !(r && l); dz++)
                        for (int dx = -Reach; dx <= Reach && !(r && l); dx++)
                        {
                            if (!In(f, x + dx, z + dz)) continue;
                            int j = Index(f, x + dx, z + dz);
                            if (!m_Free[j] || !(seen[j] || lost[j])) continue;
                            if (m_L.Plan.SpaceAt(f, Center(x + dx, z + dz)) != space) continue;
                            if (seen[j]) r = true; else l = true;
                        }
                    if (r && l) mine.Add(m_Owner[i]);
                }
                return mine.Count > 0 ? mine : found;
            }

            /// <summary>Summed-area table of the marked free cells, per floor ((NX + 1) × (NZ + 1) each, zero first row and column).</summary>
            int[] Table(bool[] marked)
            {
                int w = m_NX + 1, h = m_NZ + 1, plane = m_NX * m_NZ;
                var sum = new int[m_F * w * h];
                for (int f = 0; f < m_F; f++)
                {
                    int tb = f * w * h;
                    for (int z = 0; z < m_NZ; z++)
                    {
                        int row = 0;
                        for (int x = 0; x < m_NX; x++)
                        {
                            int i = f * plane + z * m_NX + x;
                            if (marked[i] && m_Free[i]) row++;
                            sum[tb + (z + 1) * w + x + 1] = sum[tb + z * w + x + 1] + row;
                        }
                    }
                }
                return sum;
            }
        }

        /// <summary>
        /// How far from the wall's face a piece's origin goes so its back touches the wall: standing furniture keeps
        /// <see cref="WallGap"/> (skirting, panels), what hangs on the wall itself sits flat against it (<see cref="HungGap"/>).
        /// </summary>
        float BackOf(PropKind k) => -(Foot.TryGetValue(k, out var f) ? f.yMin : -0.3f) + (OnWall.Contains(k) ? HungGap : WallGap);
        float R(float a, float b) => a + (float)m_Rng.NextDouble() * (b - a);
        bool Chance(double p) => m_Rng.NextDouble() < p;
    }
}
