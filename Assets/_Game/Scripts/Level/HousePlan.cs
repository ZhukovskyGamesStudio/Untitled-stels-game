using System.Collections.Generic;
using UnityEngine;
using static Stealth.Level.LevelMetrics;

namespace Stealth.Level
{
    /// <summary>What a raster cell of a floor is.</summary>
    public enum CellKind : byte
    {
        /// <summary>Outside the building (the gas, a courtyard).</summary>
        Outside,
        /// <summary>Floor of a room (<see cref="HousePlan.IdAt"/> = the room).</summary>
        Room,
        /// <summary>Sealed space behind a shaped wall (a cut or rounded corner): nobody gets in, nothing is placed.</summary>
        Pocket,
        /// <summary>No floor: the upper part of a room more than one storey high (an atrium) — id = that room.</summary>
        Void,
    }

    public enum RoomShape : byte { Rect, L, T, U, Cross, Corridor, Hall, Rotunda, Octagon }

    /// <summary>How a corner of a room is shaped.</summary>
    public enum CornerCut : byte { None, Chamfer, Round }

    /// <summary>A room of the plan: a region of raster cells on one floor, possibly several storeys high.</summary>
    public sealed class PlanRoom
    {
        public int Id, Floor;
        /// <summary>Storeys of height: 2 or 3 = an atrium (the floors above have a void over it).</summary>
        public int Levels = 1;
        public RoomType Type;
        public RoomShape Shape;
        /// <summary>The rectangles the room was made of (raster cells, before corners were shaped).</summary>
        public readonly List<RectInt> Rects = new();
        /// <summary>Walls that follow cut or rounded corners (world x/z, on this floor): built as they are.</summary>
        public readonly List<(Vector2 a, Vector2 b)> Shaped = new();
        public bool Dark, Flicker;
        /// <summary>Floor area in m² (cells that are this room's floor).</summary>
        public float Area;
        /// <summary>A free point near the middle of the room (world, at its floor's height).</summary>
        public Vector3 Center;
        /// <summary>Bounding box of the room in world x/z.</summary>
        public Rect Bounds;
        /// <summary>How many doorways from the lobby (privacy: loot and guards go deeper).</summary>
        public int Depth;
    }

    /// <summary>
    /// A straight piece of wall between two different things of one floor (two rooms, a room and the outside, a pocket…).
    /// Openings are cut into it (<see cref="HousePlan.Openings"/>).
    /// </summary>
    public sealed class PlanWall
    {
        public int Floor;
        public Vector2 A, B;
        /// <summary>Rooms on either side (−1 = none: outside, a pocket, a void).</summary>
        public int RoomA = -1, RoomB = -1;
        public bool Exterior;
        /// <summary>Follows a cut or rounded corner (not on the raster grid).</summary>
        public bool Shaped;
        /// <summary>Railing instead of a wall: the edge of a gallery over an atrium (see-through, waist high).</summary>
        public bool Railing;
        /// <summary>Goes up to the slab above even where there is no slab (a room under a void continues up).</summary>
        public bool FullHeight;
        public float Length => (B - A).magnitude;
        public Vector2 Dir => (B - A).normalized;
        public Vector2 At(float t) => A + Dir * t;
        public bool Separates(int a, int b) => (RoomA == a && RoomB == b) || (RoomA == b && RoomB == a);
    }

    /// <summary>A doorway, an open passage, a vent or a slot cut into a wall.</summary>
    public struct PlanOpening
    {
        public int Wall;
        public EdgeType Kind;
        /// <summary>Distance from the wall's A to the middle of the opening, metres.</summary>
        public float At;
        /// <summary>Nominal width (the clear width is a wall thickness less); an open passage spans the whole wall.</summary>
        public float Width;
        /// <summary>A vent up under the ceiling (<see cref="LevelMetrics.HighVentSill"/>): reached by the crates stacked under it on both sides.</summary>
        public bool High;
    }

    /// <summary>
    /// The floor plan of a house (pure data, identical on every peer for the same seed): a raster of 0.5 m cells per floor
    /// saying which room every spot belongs to, the rooms, the walls derived from the raster (straight, on the grid) and
    /// from shaped corners (diagonal, arcs), and the openings in them. The raster answers the quick questions
    /// (which room am I in, is this floor free, can I see there), the walls are what gets built.
    /// </summary>
    public sealed class HousePlan
    {
        public const float Res = 0.5f;

        public readonly int Floors, NX, NZ;
        readonly CellKind[] m_Kind;
        readonly int[] m_Id;
        public readonly List<PlanRoom> Rooms = new();
        public readonly List<PlanWall> Walls = new();
        public readonly List<PlanOpening> Openings = new();

        public HousePlan(int floors, int nx, int nz)
        {
            Floors = floors;
            NX = nx;
            NZ = nz;
            m_Kind = new CellKind[floors * nx * nz];
            m_Id = new int[floors * nx * nz];
            for (int i = 0; i < m_Id.Length; i++) m_Id[i] = -1;
        }

        public float SizeX => NX * Res;
        public float SizeZ => NZ * Res;

        int Index(int f, int x, int z) => (f * NZ + z) * NX + x;

        public bool InRange(int f, int x, int z) => f >= 0 && f < Floors && x >= 0 && z >= 0 && x < NX && z < NZ;

        public CellKind KindAt(int f, int x, int z) => InRange(f, x, z) ? m_Kind[Index(f, x, z)] : CellKind.Outside;
        public int IdAt(int f, int x, int z) => InRange(f, x, z) ? m_Id[Index(f, x, z)] : -1;

        public void Set(int f, int x, int z, CellKind kind, int id)
        {
            if (!InRange(f, x, z)) return;
            int i = Index(f, x, z);
            m_Kind[i] = kind;
            m_Id[i] = id;
        }

        public static Vector2Int CellOf(Vector2 p) => new(Mathf.FloorToInt(p.x / Res), Mathf.FloorToInt(p.y / Res));
        public static Vector2 CellCenter(int x, int z) => new((x + 0.5f) * Res, (z + 0.5f) * Res);

        /// <summary>The room whose floor is at <paramref name="p"/> (its floor from p.y), or null.</summary>
        public PlanRoom RoomAt(Vector3 p)
        {
            int f = Mathf.Clamp(LevelLayout.FloorOf(p.y), 0, Floors - 1);
            var c = CellOf(new Vector2(p.x, p.z));
            return KindAt(f, c.x, c.y) == CellKind.Room ? Rooms[IdAt(f, c.x, c.y)] : null;
        }

        /// <summary>The room a point of a floor belongs to, also over the room's void on a floor above (−1 = none).</summary>
        public int SpaceAt(int f, Vector2 p)
        {
            var c = CellOf(p);
            var k = KindAt(f, c.x, c.y);
            return k == CellKind.Room || k == CellKind.Void ? IdAt(f, c.x, c.y) : -1;
        }

        public bool IsFloor(int f, Vector2 p)
        {
            var c = CellOf(p);
            return KindAt(f, c.x, c.y) == CellKind.Room;
        }

        /// <summary>Inside the building on the ground floor (rooms and pockets; not a courtyard).</summary>
        public bool IsInside(Vector2 p)
        {
            var c = CellOf(p);
            var k = KindAt(0, c.x, c.y);
            return k == CellKind.Room || k == CellKind.Pocket;
        }

        public IEnumerable<PlanRoom> RoomsOn(int floor)
        {
            foreach (var r in Rooms) if (r.Floor == floor) yield return r;
        }

        // ------------------------------------------------------------------ walls

        /// <summary>
        /// Walls from the raster: wherever two neighbouring cells differ (another room, outside, a pocket of another
        /// room, a void) a piece of wall runs along their border; equal neighbours along a line merge into one wall.
        /// A room and its own pocket get no grid wall (the shaped wall stands there); a room and the void of the room
        /// below get a railing where <paramref name="gallery"/> says so (the edge of a gallery), a wall otherwise.
        /// </summary>
        public void DeriveWalls(System.Func<int, int, int, bool> gallery = null)
        {
            Walls.Clear();
            for (int f = 0; f < Floors; f++)
            {
                // Lines x = k (between columns k-1 and k), running along z.
                for (int k = 0; k <= NX; k++)
                {
                    int start = -1;
                    (CellKind, int, CellKind, int) run = default;
                    for (int z = 0; z <= NZ; z++)
                    {
                        bool has = false;
                        (CellKind, int, CellKind, int) key = default;
                        if (z < NZ)
                        {
                            var a = (KindAt(f, k - 1, z), IdAt(f, k - 1, z));
                            var b = (KindAt(f, k, z), IdAt(f, k, z));
                            has = NeedsWall(a, b);
                            key = (a.Item1, a.Item2, b.Item1, b.Item2);
                        }
                        if (start >= 0 && (!has || !key.Equals(run)))
                        {
                            AddGridWall(f, new Vector2(k * Res, start * Res), new Vector2(k * Res, z * Res), run, gallery);
                            start = -1;
                        }
                        if (has && start < 0) { start = z; run = key; }
                    }
                }
                // Lines z = k, running along x.
                for (int k = 0; k <= NZ; k++)
                {
                    int start = -1;
                    (CellKind, int, CellKind, int) run = default;
                    for (int x = 0; x <= NX; x++)
                    {
                        bool has = false;
                        (CellKind, int, CellKind, int) key = default;
                        if (x < NX)
                        {
                            var a = (KindAt(f, x, k - 1), IdAt(f, x, k - 1));
                            var b = (KindAt(f, x, k), IdAt(f, x, k));
                            has = NeedsWall(a, b);
                            key = (a.Item1, a.Item2, b.Item1, b.Item2);
                        }
                        if (start >= 0 && (!has || !key.Equals(run)))
                        {
                            AddGridWall(f, new Vector2(start * Res, k * Res), new Vector2(x * Res, k * Res), run, gallery);
                            start = -1;
                        }
                        if (has && start < 0) { start = x; run = key; }
                    }
                }
            }
            foreach (var r in Rooms)
                for (int lv = 0; lv < r.Levels; lv++)
                    foreach (var (a, b) in r.Shaped)
                        Walls.Add(new PlanWall { Floor = r.Floor + lv, A = a, B = b, RoomA = lv == 0 ? r.Id : -1, Shaped = true, FullHeight = lv + 1 < r.Levels });
            // A room more than one storey high: its walls go up to the slab (there is none over it).
            foreach (var w in Walls)
            {
                if (w.Railing) continue;
                if (Tall(w.RoomA, w.Floor) || Tall(w.RoomB, w.Floor)) w.FullHeight = true;
            }
        }

        bool Tall(int room, int floor) => room >= 0 && Rooms[room].Floor == floor && Rooms[room].Levels > 1
                                           || room >= 0 && Rooms[room].Floor < floor && Rooms[room].Floor + Rooms[room].Levels - 1 > floor;

        static bool NeedsWall((CellKind kind, int id) a, (CellKind kind, int id) b)
        {
            if (a.kind == CellKind.Outside && b.kind == CellKind.Outside) return false;
            if (a.kind == b.kind && a.id == b.id) return false;
            // a room and its own pocket: the shaped wall stands there instead
            if (a.kind == CellKind.Pocket && b.kind == CellKind.Room && a.id == b.id) return false;
            if (b.kind == CellKind.Pocket && a.kind == CellKind.Room && a.id == b.id) return false;
            // two voids of different rooms side by side: open air between atriums would need a wall; keep it
            return true;
        }

        void AddGridWall(int f, Vector2 a, Vector2 b, (CellKind ka, int ia, CellKind kb, int ib) key, System.Func<int, int, int, bool> gallery)
        {
            int roomA = key.ka == CellKind.Room ? key.ia : -1, roomB = key.kb == CellKind.Room ? key.ib : -1;
            bool exterior = key.ka == CellKind.Outside || key.kb == CellKind.Outside;
            bool voidEdge = (key.ka == CellKind.Void) != (key.kb == CellKind.Void) && !exterior;
            int voidRoom = key.ka == CellKind.Void ? key.ia : key.kb == CellKind.Void ? key.ib : -1;
            int other = key.ka == CellKind.Void ? roomB : roomA;
            bool railing = voidEdge && other >= 0 && gallery != null && gallery(f, voidRoom, other);
            // the wall around a void that goes on up (a three-storey atrium) reaches the next floor
            bool tallVoid = voidRoom >= 0 && Rooms[voidRoom].Floor + Rooms[voidRoom].Levels - 1 > f;
            Walls.Add(new PlanWall { Floor = f, A = a, B = b, RoomA = roomA, RoomB = roomB, Exterior = exterior, Railing = railing, FullHeight = tallVoid && !railing });
        }

        /// <summary>Openings of a wall, sorted by position.</summary>
        public List<PlanOpening> OpeningsOf(int wall)
        {
            var list = new List<PlanOpening>();
            foreach (var o in Openings) if (o.Wall == wall) list.Add(o);
            list.Sort((p, q) => p.At.CompareTo(q.At));
            return list;
        }

        /// <summary>Can an opening of <paramref name="width"/> sit on a wall at <paramref name="at"/>: clear of its ends and other openings.</summary>
        public bool FreeOn(int wall, float at, float width, float margin = 0.45f)
        {
            var w = Walls[wall];
            if (w.Railing || w.Shaped) return false;
            if (at - width / 2f < margin || at + width / 2f > w.Length - margin) return false;
            foreach (var o in Openings)
            {
                if (o.Wall != wall) continue;
                if (o.Kind == EdgeType.Open) return false;
                if (Mathf.Abs(o.At - at) < (o.Width + width) / 2f + margin) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ sight

        /// <summary>
        /// The straight line between two points of a floor crosses no solid wall (doorways, vents, slots, open passages
        /// and railings let the view through) and never leaves the building.
        /// </summary>
        public bool CanSee(int f, Vector2 from, Vector2 to)
        {
            if (!InsideAt(f, from) || !InsideAt(f, to)) return false;
            for (int i = 0; i < Walls.Count; i++)
            {
                var w = Walls[i];
                if (w.Floor != f || w.Railing) continue;
                if (!Intersects(from, to, w.A, w.B, out float t)) continue;
                if (!ThroughOpening(i, t)) return false;
            }
            return true;
        }

        /// <summary>
        /// How far (0..1 of the segment) the view goes from <paramref name="from"/> towards <paramref name="to"/> before a
        /// solid wall; 1 = nothing in the way. <paramref name="walls"/> — the walls worth testing (null = every wall of the floor).
        /// </summary>
        public float SightShare(int f, Vector2 from, Vector2 to, List<int> walls = null)
        {
            float best = 1f;
            int count = walls?.Count ?? Walls.Count;
            float len = (to - from).magnitude;
            for (int k = 0; k < count; k++)
            {
                int i = walls != null ? walls[k] : k;
                var w = Walls[i];
                if (w.Floor != f || w.Railing) continue;
                if (!Intersects(from, to, w.A, w.B, out float along)) continue;
                if (ThroughOpening(i, along)) continue;
                // where along the ray: the crossing point projected on the ray
                var hit = w.A + w.Dir * along;
                float share = len > 1e-4f ? (hit - from).magnitude / len : 0f;
                if (share < best) best = share;
            }
            return best;
        }

        /// <summary>The walls of a floor that come within <paramref name="radius"/> of a point (for many rays from it).</summary>
        public void WallsNear(int f, Vector2 p, float radius, List<int> output)
        {
            output.Clear();
            for (int i = 0; i < Walls.Count; i++)
            {
                var w = Walls[i];
                if (w.Floor != f || w.Railing) continue;
                var min = Vector2.Min(w.A, w.B) - Vector2.one * radius;
                var max = Vector2.Max(w.A, w.B) + Vector2.one * radius;
                if (p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y) output.Add(i);
            }
        }

        /// <summary>A point of a floor is inside the building (a room or the void of an atrium).</summary>
        public bool IsInside(int f, Vector2 p) => InsideAt(f, p);

        bool InsideAt(int f, Vector2 p)
        {
            var c = CellOf(p);
            var k = KindAt(f, c.x, c.y);
            return k == CellKind.Room || k == CellKind.Void;
        }

        bool ThroughOpening(int wall, float t)
        {
            foreach (var o in Openings)
            {
                if (o.Wall != wall) continue;
                if (o.Kind == EdgeType.Open) return true;
                if (Mathf.Abs(t - o.At) <= ClearWidth(o.Width) / 2f) return true;
            }
            return false;
        }

        /// <summary>Segment p0–p1 crosses segment a–b; <paramref name="along"/> = distance from a to the crossing.</summary>
        public static bool Intersects(Vector2 p0, Vector2 p1, Vector2 a, Vector2 b, out float along)
        {
            along = 0f;
            var r = p1 - p0;
            var s = b - a;
            float den = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(den) < 1e-6f) return false;
            var q = a - p0;
            float t = (q.x * s.y - q.y * s.x) / den;
            float u = (q.x * r.y - q.y * r.x) / den;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
            along = u * s.magnitude;
            return true;
        }
    }
}
