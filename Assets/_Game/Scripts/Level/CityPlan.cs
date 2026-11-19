using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>What a box of the city is drawn with (an index into <see cref="LevelArt.CityMaterials"/>). Append only.</summary>
    public enum CitySlot { Road, Sidewalk, Paint, Roof, FacadeA, FacadeB, FacadeC, Metal, LampGlow, Leaves, Bark, CarRed, CarBlue, CarSilver, Glass, Tyre,
        Stone, Plinth, TailLight, Soil }

    /// <summary>One box of the city: its sides in one material, its top in another (no bottom face).</summary>
    public struct CityBox
    {
        public Vector3 Center, Size;
        public CitySlot Sides, Top;

        public CityBox(Vector3 min, Vector3 max, CitySlot sides, CitySlot top)
        {
            Center = (min + max) / 2f;
            Size = max - min;
            Sides = sides;
            Top = top;
        }

        public Bounds Bounds => new(Center, Size);
    }

    /// <summary>
    /// The city around a house: boxes to draw, where the street lamps shine from (each looks down), what is solid (the
    /// buildings, parked cars, lamp posts and tree trunks) and the pavement players walk on (its top at the house's floor).
    /// </summary>
    public sealed class City
    {
        public readonly List<CityBox> Boxes = new();
        public readonly List<Vector3> Lamps = new();
        public readonly List<Bounds> Solids = new();
        public readonly List<Bounds> Ground = new();
    }

    /// <summary>
    /// The street the house stands on (pure, from the house's seed; scenery past the yard's invisible wall): the house is one
    /// building of a block like any other. Its front (the lobby's side) is on the pavement of a two-lane street, neighbours of
    /// about its height stand three to six metres to its sides along the same building line, a back alley runs behind the
    /// block, cross streets close it at both ends; across the street and beyond the alley stand more rows of buildings,
    /// towers behind them. Street lamps and trees on the pavements, cars parked along the kerbs, a dashed centre line and
    /// zebra crossings at the junctions. The pavement is level with the house's floor, the roads a kerb lower. Everything is
    /// a box (<see cref="CityBox"/>); the level builder merges them into one mesh per material and makes the near ones solid.
    /// </summary>
    public static class CityPlan
    {
        /// <summary>The invisible wall round the yard, metres from the house.</summary>
        public const float Yard = 10f;
        /// <summary>The pavement in front of the house, the street, the pavement across it (metres from the front wall).</summary>
        public const float Walk = 3.5f, RoadWidth = 8f, FarWalk = 3.5f;
        /// <summary>Tops of the pavement (the house's floor) and of the roads (a kerb lower), metres.</summary>
        public const float WalkTop = 0f, RoadTop = -0.14f;
        const float Bottom = -0.3f;
        /// <summary>A cross street: kerb-to-kerb width and the pavement on each side.</summary>
        const float CrossRoad = 8f, CrossWalk = 1.5f;
        const float AlleyWidth = 6f;
        /// <summary>How far the city goes along and away from the street, metres from the house.</summary>
        public const float Reach = 90f;
        /// <summary>A storey of the facade textures (Tools/Art/generate_city.py: 4 bays x 4 storeys per tile) — the house's own.</summary>
        public const float Storey = LevelMetrics.FloorHeight;

        /// <summary>Metres one repeat of a slot's texture covers.</summary>
        public static float Tile(CitySlot s) => s switch
        {
            CitySlot.FacadeA or CitySlot.FacadeB or CitySlot.FacadeC => 4f * Storey,
            CitySlot.Road => 4f,
            CitySlot.Sidewalk => 2f,
            _ => 1f,
        };

        /// <summary>The side of the house's rectangle nearest to a point (0 south −z, 1 east +x, 2 north +z, 3 west −x): its front.</summary>
        public static int SideNearest(Rect house, Vector2 p)
        {
            float s = p.y - house.yMin, e = house.xMax - p.x, n = house.yMax - p.y, w = p.x - house.xMin;
            float best = Mathf.Min(Mathf.Min(s, e), Mathf.Min(n, w));
            return best == s ? 0 : best == e ? 1 : best == n ? 2 : 3;
        }

        public static City Make(Rect house, int front, int floors, int seed)
        {
            var c = new City();
            var rng = new System.Random(LevelRandom.Mix(seed, Salt.City, 0));
            var e = new Edge(house, front);
            float L = e.Length, D = front % 2 == 0 ? house.height : house.width;
            float B = D + 2f;                                   // the block's depth behind the building line
            float roadOut = Walk + RoadWidth, opposite = roadOut + FarWalk;

            // our block: the house, then neighbours on both sides along the same building line
            float gapL = Range(rng, 3f, 6f), gapR = Range(rng, 3f, 6f);
            var lots = new List<(float u0, float u1)>();
            float uMin = -gapL;
            for (int i = 0, n = rng.Next(2, 4); i < n; i++)
            {
                float w = Range(rng, 10f, 20f);
                lots.Add((uMin - w, uMin));
                uMin -= w + Range(rng, 0f, 1.5f);
            }
            float uMax = L + gapR;
            for (int i = 0, n = rng.Next(2, 4); i < n; i++)
            {
                float w = Range(rng, 10f, 20f);
                lots.Add((uMax, uMax + w));
                uMax += w + Range(rng, 0f, 1.5f);
            }
            // the cross streets that close the block, the blocks beyond them
            var crossL = (lo: uMin - CrossWalk - CrossRoad, hi: uMin - CrossWalk);
            var crossR = (lo: uMax + CrossWalk, hi: uMax + CrossWalk + CrossRoad);
            var blocks = new[] { (lo: -Reach, hi: crossL.lo - CrossWalk), (lo: uMin - CrossWalk, hi: uMax + CrossWalk), (lo: crossR.hi + CrossWalk, hi: L + Reach) };
            bool InCross(float u, float margin) => (u > crossL.lo - margin && u < crossL.hi + margin) || (u > crossR.lo - margin && u < crossR.hi + margin);

            // the asphalt under everything; the pavements of every block a kerb higher (ours round the house, not under it)
            Box(c, e, -Reach, L + Reach, -Reach, Reach, Bottom, RoadTop, CitySlot.Road, CitySlot.Road);
            float alley0 = -B - AlleyWidth, backRow = alley0 - 1.5f, deep = 22f;
            foreach (var (lo, hi) in blocks)
            {
                if (hi - lo < 1f) continue;
                bool ours = lo < 0f && hi > L;
                if (ours)
                {
                    Pave(c, e, lo, hi, 0f, Walk);          // the pavement in front of the row
                    Pave(c, e, lo, hi, -B, -D);             // behind the house, up to the alley
                    Pave(c, e, lo, 0f, -D, 0f);             // the sides of the house
                    Pave(c, e, L, hi, -D, 0f);
                }
                else Pave(c, e, lo, hi, -B, Walk);
                Pave(c, e, lo, hi, roadOut, Mathf.Min(Reach, opposite + deep));
                Pave(c, e, lo, hi, Mathf.Max(-Reach, backRow - deep), alley0);
            }

            // the buildings: our neighbours, the rest of our side, across the street, beyond the alley; towers behind
            int Storeys() => Mathf.Max(2, floors + rng.Next(-1, 3));
            foreach (var (u0, u1) in lots) Building(c, rng, e, u0, u1, 0f, -Range(rng, Mathf.Max(10f, B - 8f), B), Storeys());
            foreach (var (lo, hi) in blocks)
            {
                bool ours = lo < 0f && hi > L;
                if (!ours) Row(c, rng, e, lo, hi, 0f, -1f, () => Range(rng, Mathf.Max(10f, B - 8f), B), Storeys);
                Row(c, rng, e, lo, hi, opposite + 0.5f, 1f, () => Range(rng, 12f, 20f), Storeys);
                Row(c, rng, e, lo, hi, backRow, -1f, () => Range(rng, 12f, 20f), Storeys);
            }
            for (float u = -Reach + Range(rng, 0f, 20f); u < L + Reach - 20f; u += Range(rng, 26f, 42f))
            {
                float w = Range(rng, 14f, 22f);
                Building(c, rng, e, u, u + w, opposite + deep + Range(rng, 8f, 20f), 0f, Mathf.Max(floors + 3, rng.Next(7, 15)), Range(rng, 12f, 18f));
                Building(c, rng, e, u + 10f, u + 10f + w, backRow - deep - Range(rng, 8f, 20f), 0f, Mathf.Max(floors + 3, rng.Next(7, 15)), -Range(rng, 12f, 18f));
            }

            // the street's paint: the centre line, zebras over the street beside the junctions and over the cross streets' mouths
            const float paintTop = RoadTop + 0.012f;
            float mid = Walk + RoadWidth / 2f;
            for (float u = -Reach + 2f; u + 3f < L + Reach - 2f; u += 6f)
                if (!InCross(u, 5.5f) && !InCross(u + 3f, 5.5f)) Box(c, e, u, u + 3f, mid - 0.075f, mid + 0.075f, RoadTop, paintTop, CitySlot.Paint, CitySlot.Paint);
            foreach (var cross in new[] { crossL, crossR })
            {
                foreach (float u0 in new[] { cross.lo - CrossWalk - 4f, cross.hi + CrossWalk + 0.5f })
                    for (float v = Walk + 0.5f; v + 0.5f <= roadOut - 0.25f; v += 1f)
                        Box(c, e, u0, u0 + 3.5f, v, v + 0.5f, RoadTop, paintTop, CitySlot.Paint, CitySlot.Paint);
                foreach (var (v0, v1) in new[] { (-4f, -0.5f), (roadOut + 0.5f, roadOut + 4f) })
                    for (float u = cross.lo + 0.5f; u + 0.5f <= cross.hi - 0.25f; u += 1f)
                        Box(c, e, u, u + 0.5f, v0, v1, RoadTop, paintTop, CitySlot.Paint, CitySlot.Paint);
                float cm = (cross.lo + cross.hi) / 2f;
                foreach (var (from, to) in new[] { (-Reach, -6f), (roadOut + 6f, Reach) })
                    for (float v = from; v + 3f <= to; v += 6f)
                        if (v + 3f < alley0 - 1f || v > -B + 1f) Box(c, e, cm - 0.075f, cm + 0.075f, v, v + 3f, RoadTop, paintTop, CitySlot.Paint, CitySlot.Paint);
            }

            // lamps by both kerbs (their arms over the street), trees between them, cars parked along the kerbs
            for (float u = -Reach + 6f; u < L + Reach - 6f; u += 16f)
            {
                if (InCross(u, 2.5f)) continue;
                Lamp(c, e, u, Walk - 0.5f, 1f);
                if (!InCross(u + 8f, 2.5f)) Lamp(c, e, u + 8f, roadOut + 0.5f, -1f);
                if (!InCross(u + 8f, 3f)) Tree(c, rng, e, u + 8f, Walk - 1.1f);
                if (!InCross(u, 3f)) Tree(c, rng, e, u, roadOut + 1.1f);
            }
            foreach (var (kerb, side) in new[] { (Walk + 1.1f, 0), (roadOut - 1.1f, 1) })
                for (float u = -Reach + Range(rng, 2f, 8f); u < L + Reach - 6f; u += Range(rng, 5.5f, 14f))
                    if (!InCross(u, 6f) && !InCross(u + 4.4f, 6f)) Car(c, rng, e, u, kerb, side);

            // the street's furniture: benches and bins on the pavements, bollards at the crossings, hydrants, manholes,
            // traffic lights at the junctions
            for (float u = -Reach + 10f; u < L + Reach - 10f; u += 16f)
            {
                if (InCross(u + 4f, 3f)) continue;
                if (rng.NextDouble() < 0.5) Bench(c, e, u + 4f, Walk - 0.9f, 1f);
                else Bin(c, e, u + 4f, Walk - 0.45f);
                if (rng.NextDouble() < 0.5) Bench(c, e, u + 12f, roadOut + 0.9f, -1f);
                else Bin(c, e, u + 12f, roadOut + 0.45f);
                if (rng.NextDouble() < 0.25) Hydrant(c, e, u + 1.5f, Walk - 0.4f);
                if (rng.NextDouble() < 0.4) Box(c, e, u + 6f, u + 6.7f, mid + 1.4f, mid + 2.1f, RoadTop, RoadTop + 0.015f, CitySlot.Tyre, CitySlot.Tyre); // a manhole
            }
            foreach (var cross in new[] { crossL, crossR })
            {
                foreach (float cu in new[] { cross.lo - CrossWalk - 4.5f, cross.hi + CrossWalk + 4.5f })
                    foreach (float bv in new[] { Walk - 0.3f, roadOut + 0.3f })
                        for (int k = 0; k < 3; k++) Bollard(c, e, cu + (k - 1) * 1.4f, bv);
                TrafficLight(c, e, cross.lo - 0.6f, Walk - 0.4f, 1f);
                TrafficLight(c, e, cross.hi + 0.6f, roadOut + 0.4f, -1f);
            }
            return c;
        }

        static float Range(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// <summary>
        /// A frame along one side of a rectangle: <paramref name="side"/> 0 south (−z), 1 east (+x), 2 north (+z), 3 west
        /// (−x); u runs along the side (from its start corner), v out of the rectangle.
        /// </summary>
        readonly struct Edge
        {
            public readonly Vector2 Start, U, V;
            public readonly float Length;
            public Edge(Rect r, int side)
            {
                (Start, U, V, Length) = side switch
                {
                    0 => (new Vector2(r.xMin, r.yMin), Vector2.right, Vector2.down, r.width),
                    1 => (new Vector2(r.xMax, r.yMin), Vector2.up, Vector2.right, r.height),
                    2 => (new Vector2(r.xMax, r.yMax), Vector2.left, Vector2.up, r.width),
                    _ => (new Vector2(r.xMin, r.yMax), Vector2.down, Vector2.left, r.height),
                };
            }
            public Vector2 At(float u, float v) => Start + U * u + V * v;
        }

        /// <summary>A box in an edge's frame: u0..u1 along, v0..v1 out (either order).</summary>
        static CityBox Box(City c, Edge e, float u0, float u1, float v0, float v1, float y0, float y1, CitySlot sides, CitySlot top)
        {
            var a = e.At(u0, v0);
            var b = e.At(u1, v1);
            var box = new CityBox(new Vector3(Mathf.Min(a.x, b.x), y0, Mathf.Min(a.y, b.y)), new Vector3(Mathf.Max(a.x, b.x), y1, Mathf.Max(a.y, b.y)), sides, top);
            c.Boxes.Add(box);
            return box;
        }

        /// <summary>A pavement slab (walked on: its top is the house's floor level).</summary>
        static void Pave(City c, Edge e, float u0, float u1, float v0, float v1)
        {
            if (Mathf.Abs(u1 - u0) < 0.05f || Mathf.Abs(v1 - v0) < 0.05f) return;
            c.Ground.Add(Box(c, e, u0, u1, v0, v1, Bottom, WalkTop, CitySlot.Sidewalk, CitySlot.Sidewalk).Bounds);
        }

        /// <summary>
        /// A building on u0..u1 from the building line <paramref name="v0"/> back to <paramref name="v1"/> (or, with
        /// <paramref name="depth"/> ≠ 0, from v0 that deep: the sign is the direction), so many storeys tall.
        /// </summary>
        static void Building(City c, System.Random rng, Edge e, float u0, float u1, float v0, float v1, int storeys, float depth = 0f)
        {
            if (depth != 0f) v1 = v0 + depth;
            var facade = (CitySlot)((int)CitySlot.FacadeA + rng.Next(3));
            float h = storeys * Storey + Range(rng, 0.3f, 1.0f); // a parapet over the top storey
            var body = Box(c, e, u0, u1, v0, v1, WalkTop, h, facade, CitySlot.Roof);
            float f = Mathf.Sign(v0 - v1);                         // the street side: v grows out of the front along f
            var trim = facade == CitySlot.FacadeC ? CitySlot.Metal : CitySlot.Stone;
            CityBox Grown(float o, float y0, float y1, CitySlot sides, CitySlot top) => Box(c, e, u0 - o, u1 + o, v0 + f * o, v1 - f * o, y0, y1, sides, top);
            // a stone plinth over the ground storey, bands between the storeys, a cornice round the top
            var plinth = Grown(0.08f, WalkTop, Storey, CitySlot.Plinth, trim);
            c.Solids.Add(plinth.Bounds);
            for (int k = 2; k < storeys; k++) Grown(0.05f, k * Storey - 0.06f, k * Storey + 0.06f, trim, trim);
            Grown(0.22f, h - 0.14f, h + 0.1f, trim, trim);
            // the way in: a glass door in the middle of the front, a canopy over it
            float um = (u0 + u1) / 2f;
            Box(c, e, um - 0.8f, um + 0.8f, v0 + f * 0.08f, v0 + f * 0.1f, WalkTop, 2.4f, CitySlot.Glass, CitySlot.Glass);
            Box(c, e, um - 1.3f, um + 1.3f, v0, v0 + f * 1.1f, 2.7f, 2.82f, CitySlot.Metal, CitySlot.Metal);
            // balconies on the plain fronts now and then; air conditioners hung outside the windows
            if (facade == CitySlot.FacadeA && storeys >= 3 && rng.NextDouble() < 0.6)
                for (int k = 1; k < storeys; k++)
                    for (float u = u0 + 1.4f; u + 2.4f <= u1 - 1.4f; u += 4.2f)
                    {
                        float y = k * Storey + 0.02f;
                        Box(c, e, u, u + 2.4f, v0, v0 + f * 1.0f, y, y + 0.14f, trim, trim);
                        Box(c, e, u, u + 2.4f, v0 + f * 0.95f, v0 + f * 1.0f, y + 0.14f, y + 1.05f, CitySlot.Metal, CitySlot.Metal);
                        foreach (float su in new[] { u, u + 2.35f })
                            Box(c, e, su, su + 0.05f, v0, v0 + f * 1.0f, y + 0.14f, y + 1.05f, CitySlot.Metal, CitySlot.Metal);
                    }
            int units = rng.NextDouble() < 0.5 ? rng.Next(2, 6) : 0;
            for (int i = 0; i < units && storeys > 1; i++)
            {
                float u = Range(rng, u0 + 0.6f, u1 - 1.4f), y = rng.Next(1, storeys) * Storey + 0.7f;
                Box(c, e, u, u + 0.8f, v0, v0 + f * 0.35f, y, y + 0.55f, CitySlot.Metal, CitySlot.Metal);
            }
            // something on the roof now and then: a machine room, a water tank
            var b = body.Bounds;
            if (rng.NextDouble() < 0.6 && b.size.x > 7f && b.size.z > 7f)
            {
                float w = Range(rng, 2.5f, 4.5f), d = Range(rng, 2.5f, 4.5f);
                float x = Range(rng, b.min.x + 1f, b.max.x - 1f - w), z = Range(rng, b.min.z + 1f, b.max.z - 1f - d);
                c.Boxes.Add(new CityBox(new Vector3(x, h, z), new Vector3(x + w, h + Range(rng, 1.8f, 3f), z + d), CitySlot.Roof, CitySlot.Roof));
            }
            Details(c, rng, e, b, f, facade, trim, storeys, h);
        }

        /// <summary>Where the windows of a facade texture are in its storey-square cell: x0, y0, x1, y1 in 0..1 (Tools/Art/generate_city.py).</summary>
        static Vector4 WindowOf(CitySlot facade) => facade switch
        {
            CitySlot.FacadeA => new Vector4(0.18f, 0.3f, 0.82f, 0.82f),
            CitySlot.FacadeB => new Vector4(0.25f, 0.28f, 0.75f, 0.8f),
            _ => new Vector4(0.04f, 0.12f, 0.96f, 0.94f),
        };

        /// <summary>
        /// The relief a flat textured box lacked (seen from the house's windows it read as low poly): on the windows the
        /// texture draws (they line up: both count from the pavement and along the world's axes, AddFaces) frames, sills and
        /// hoods on the plain and brick fronts, mullions on the glass ones; drainpipes at the front corners, a fire escape
        /// on a brick side, shop awnings over the ground floor, machines, vents, masts and a water tank on the roof.
        /// </summary>
        static void Details(City c, System.Random rng, Edge e, Bounds b, float f, CitySlot facade, CitySlot trim, int storeys, float h)
        {
            var win = WindowOf(facade);
            bool glass = facade == CitySlot.FacadeC;
            var front = new Vector3(e.V.x, 0f, e.V.y) * f; // out of the street front
            foreach (var n in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                bool alongX = Mathf.Abs(n.z) > 0.5f;
                var right = Vector3.Cross(Vector3.up, -n);
                float s = alongX ? right.x : right.z;                  // the texture's u = s * the world coordinate along the face
                float lo = alongX ? b.min.x : b.min.z, hi = alongX ? b.max.x : b.max.z;
                float nSign = alongX ? n.z : n.x;
                float plane = nSign > 0 ? (alongX ? b.max.z : b.max.x) : (alongX ? b.min.z : b.min.x);
                // a box on the face: along a0..a1 (world), o0..o1 out of it, y0..y1
                void On(float a0, float a1, float o0, float o1, float y0, float y1, CitySlot slot)
                {
                    float p0 = plane + nSign * o0, p1 = plane + nSign * o1;
                    var min = alongX ? new Vector3(a0, y0, Mathf.Min(p0, p1)) : new Vector3(Mathf.Min(p0, p1), y0, a0);
                    var max = alongX ? new Vector3(a1, y1, Mathf.Max(p0, p1)) : new Vector3(Mathf.Max(p0, p1), y1, a1);
                    c.Boxes.Add(new CityBox(min, max, slot, slot));
                }
                float tlo = Mathf.Min(s * lo, s * hi), thi = Mathf.Max(s * lo, s * hi);
                for (int k = Mathf.CeilToInt(tlo / Storey); (k + 1) * Storey <= thi + 0.01f; k++)
                {
                    float t0 = (k + win.x) * Storey, t1 = (k + win.z) * Storey;
                    float a0 = Mathf.Min(s * t0, s * t1), a1 = Mathf.Max(s * t0, s * t1);
                    if (a0 < lo + 0.15f || a1 > hi - 0.15f) continue;
                    for (int y = 1; y < storeys; y++)
                    {
                        float sill = (y + win.y) * Storey, top = (y + win.w) * Storey;
                        if (glass)
                        {
                            // the curtain wall's mullions between the panes (the bands are its transoms)
                            float m = s > 0 ? a0 : a1;
                            On(m - 0.06f, m + 0.06f, 0f, 0.1f, y * Storey, (y + 1) * Storey, CitySlot.Metal);
                            continue;
                        }
                        On(a0 - 0.08f, a1 + 0.08f, 0f, 0.14f, sill - 0.07f, sill, trim);              // the sill
                        On(a0 - 0.06f, a0, 0f, 0.09f, sill, top, trim);                               // the reveal's jambs
                        On(a1, a1 + 0.06f, 0f, 0.09f, sill, top, trim);
                        On((a0 + a1) / 2f - 0.03f, (a0 + a1) / 2f + 0.03f, 0f, 0.04f, sill, top, CitySlot.Metal); // the casement's middle bar
                        if (facade == CitySlot.FacadeB) On(a0 - 0.12f, a1 + 0.12f, 0f, 0.12f, top, top + 0.16f, trim); // a brick hood over it
                        else On(a0 - 0.04f, a1 + 0.04f, 0f, 0.09f, top, top + 0.06f, trim);
                    }
                }
                bool isFront = Vector3.Dot(n, front) > 0.5f;
                // the wall itself in relief: pilasters between the bays of a plain front, quoins up the corners of a brick
                // one, spandrel panels under the glass of a curtain wall; the parapet's coping along the top
                for (int k = Mathf.CeilToInt(tlo / Storey); k * Storey <= thi + 0.01f; k++)
                {
                    float at = s * k * Storey;
                    if (at < lo + 0.3f || at > hi - 0.3f || glass) continue;
                    if (facade == CitySlot.FacadeA) On(at - 0.14f, at + 0.14f, 0f, 0.1f, Storey, h - 0.2f, trim);
                }
                if (facade == CitySlot.FacadeB)
                    foreach (float corner in new[] { lo, hi })
                        for (float y = Storey; y < h - 0.6f; y += 0.6f)
                        {
                            float w = ((int)(y / 0.6f) % 2 == 0) ? 0.5f : 0.32f;
                            On(corner == lo ? lo : hi - w, corner == lo ? lo + w : hi, 0f, 0.05f, y, y + 0.5f, trim);
                        }
                if (glass)
                    for (int y = 1; y < storeys; y++)
                        On(lo + 0.1f, hi - 0.1f, 0f, 0.06f, (y + win.w) * Storey, (y + 1 + win.y) * Storey, CitySlot.Metal);
                On(lo - 0.05f, hi + 0.05f, -0.02f, 0.18f, h - 0.04f, h + 0.06f, trim);
                // a little railing in front of some windows of a brick front (a French balcony), satellite dishes, air conditioners
                if (facade == CitySlot.FacadeB && !glass)
                    for (int k = Mathf.CeilToInt(tlo / Storey); (k + 1) * Storey <= thi + 0.01f; k++)
                        for (int y = 1; y < storeys; y++)
                        {
                            if (rng.NextDouble() > 0.25) continue;
                            float t0 = (k + win.x) * Storey, t1 = (k + win.z) * Storey;
                            float a0 = Mathf.Min(s * t0, s * t1), a1 = Mathf.Max(s * t0, s * t1);
                            if (a0 < lo + 0.15f || a1 > hi - 0.15f) continue;
                            float sill = (y + win.y) * Storey;
                            On(a0, a1, 0.22f, 0.25f, sill + 0.85f, sill + 0.9f, CitySlot.Metal);
                            for (float r = a0; r <= a1 + 0.01f; r += 0.12f) On(r - 0.01f, r + 0.01f, 0.22f, 0.25f, sill, sill + 0.88f, CitySlot.Metal);
                        }
                for (int d = 0, dishes = storeys >= 3 && !glass ? rng.Next(0, 3) : 0; d < dishes; d++)
                {
                    float at = Range(rng, lo + 0.8f, hi - 0.8f), y = rng.Next(1, storeys) * Storey + Range(rng, 1.4f, 2.6f);
                    On(at - 0.03f, at + 0.03f, 0f, 0.35f, y - 0.03f, y + 0.03f, CitySlot.Metal);      // the arm
                    On(at - 0.3f, at + 0.3f, 0.35f, 0.42f, y - 0.3f, y + 0.3f, CitySlot.Roof);          // the dish
                }
                for (int u = 0, units = !glass ? rng.Next(0, 4) : 0; u < units; u++)
                {
                    float at = Range(rng, lo + 0.8f, hi - 1.6f), y = rng.Next(1, storeys) * Storey + 0.25f;
                    On(at, at + 0.8f, 0f, 0.32f, y, y + 0.55f, CitySlot.Metal);
                    On(at + 0.1f, at + 0.7f, 0.32f, 0.34f, y + 0.08f, y + 0.47f, CitySlot.Tyre);       // its grille
                }
                // the ground floor of a front: a shop window or two, framed, with a door between
                if (isFront && !glass && storeys >= 2 && hi - lo > 6f && rng.NextDouble() < 0.75)
                {
                    float m = (lo + hi) / 2f;
                    foreach (var (s0, s1) in new[] { (lo + 0.8f, m - 1.2f), (m + 1.2f, hi - 0.8f) })
                    {
                        if (s1 - s0 < 1.2f) continue;
                        On(s0, s1, 0.08f, 0.1f, 0.5f, 2.6f, CitySlot.Glass);                             // the glass
                        On(s0 - 0.08f, s1 + 0.08f, 0.08f, 0.16f, 2.6f, 2.72f, CitySlot.Metal);           // its frame
                        On(s0 - 0.08f, s1 + 0.08f, 0.08f, 0.2f, 0.38f, 0.5f, CitySlot.Metal);
                        On(s0 - 0.08f, s0, 0.08f, 0.16f, 0.5f, 2.6f, CitySlot.Metal);
                        On(s1, s1 + 0.08f, 0.08f, 0.16f, 0.5f, 2.6f, CitySlot.Metal);
                        for (float r = s0 + 1.2f; r < s1 - 0.4f; r += 1.2f) On(r - 0.03f, r + 0.03f, 0.08f, 0.14f, 0.5f, 2.6f, CitySlot.Metal);
                    }
                }
                if (isFront && !glass)
                    foreach (float a in new[] { lo + 0.12f, hi - 0.12f })                                // drainpipes down the front's corners
                    {
                        On(a - 0.06f, a + 0.06f, 0.1f, 0.22f, WalkTop, h - 0.15f, CitySlot.Metal);
                        On(a - 0.1f, a + 0.1f, 0.05f, 0.26f, h - 0.35f, h - 0.15f, CitySlot.Metal);     // the hopper under the gutter
                    }
                if (isFront && storeys >= 2 && rng.NextDouble() < (glass ? 0.7 : 0.45))
                {
                    // shops: an awning over the ground floor's windows, a sign board over it
                    var paint = (CitySlot)((int)CitySlot.CarRed + rng.Next(3));
                    float span = Mathf.Min(hi - lo - 4f, Range(rng, 4f, 9f));
                    if (span > 2f)
                    {
                        float a = Range(rng, lo + 0.6f, hi - 0.6f - span);
                        On(a, a + span, 0.1f, 1.3f, 2.95f, 3.08f, paint);
                        On(a, a + span, 1.22f, 1.3f, 2.65f, 3.08f, paint);
                        On(a + 0.4f, a + span - 0.4f, 0.1f, 0.2f, 3.25f, 3.85f, rng.NextDouble() < 0.5 ? CitySlot.LampGlow : CitySlot.Plinth);
                    }
                }
                bool side = Mathf.Abs(Vector3.Dot(n, front)) < 0.5f;
                if (side && facade == CitySlot.FacadeB && storeys >= 3 && hi - lo > 6f && rng.NextDouble() < 0.5)
                {
                    // a fire escape: a landing per storey with a rail, ladders between them (from the first floor up: none at the ground)
                    float a = Range(rng, lo + 1f, hi - 4f);
                    for (int y = 1; y < storeys; y++)
                    {
                        float fy = y * Storey + 0.05f;
                        On(a, a + 3f, 0f, 1.1f, fy, fy + 0.06f, CitySlot.Metal);
                        On(a, a + 3f, 1.05f, 1.1f, fy + 0.06f, fy + 1f, CitySlot.Metal);
                        On(a, a + 3f, 0.6f, 1.1f, fy + 0.94f, fy + 1f, CitySlot.Metal);
                        if (y + 1 < storeys)
                            foreach (float r in new[] { a + 2.2f, a + 2.85f })
                                On(r - 0.025f, r + 0.025f, 0.3f, 0.36f, fy, fy + Storey, CitySlot.Metal);
                    }
                }
            }
            // the roof: machines, vents, a mast
            int things = rng.Next(2, 6);
            for (int i = 0; i < things && b.size.x > 4f && b.size.z > 4f; i++)
            {
                float x = Range(rng, b.min.x + 0.8f, b.max.x - 1.8f), z = Range(rng, b.min.z + 0.8f, b.max.z - 1.8f);
                switch (rng.Next(3))
                {
                    case 0: // an air-conditioning unit, a fan's grille on top
                        c.Boxes.Add(new CityBox(new Vector3(x, h, z), new Vector3(x + 1.2f, h + 0.9f, z + 0.8f), CitySlot.Metal, CitySlot.Metal));
                        c.Boxes.Add(new CityBox(new Vector3(x + 0.2f, h + 0.9f, z + 0.15f), new Vector3(x + 0.7f, h + 0.95f, z + 0.65f), CitySlot.Tyre, CitySlot.Tyre));
                        break;
                    case 1: // a vent stack with a cap
                        c.Boxes.Add(new CityBox(new Vector3(x, h, z), new Vector3(x + 0.35f, h + 1.3f, z + 0.35f), CitySlot.Metal, CitySlot.Metal));
                        c.Boxes.Add(new CityBox(new Vector3(x - 0.08f, h + 1.3f, z - 0.08f), new Vector3(x + 0.43f, h + 1.38f, z + 0.43f), CitySlot.Metal, CitySlot.Metal));
                        break;
                    default: // a mast with two arms
                        c.Boxes.Add(new CityBox(new Vector3(x, h, z), new Vector3(x + 0.07f, h + Range(rng, 2.5f, 4.5f), z + 0.07f), CitySlot.Metal, CitySlot.Metal));
                        c.Boxes.Add(new CityBox(new Vector3(x - 0.5f, h + 2f, z + 0.02f), new Vector3(x + 0.57f, h + 2.04f, z + 0.05f), CitySlot.Metal, CitySlot.Metal));
                        c.Boxes.Add(new CityBox(new Vector3(x + 0.02f, h + 1.6f, z - 0.4f), new Vector3(x + 0.05f, h + 1.64f, z + 0.47f), CitySlot.Metal, CitySlot.Metal));
                        break;
                }
            }
            if (storeys >= 6 && b.size.x > 7f && b.size.z > 7f && rng.NextDouble() < 0.5)
            {
                // a water tank on a frame of legs
                float x = Range(rng, b.min.x + 1.5f, b.max.x - 4f), z = Range(rng, b.min.z + 1.5f, b.max.z - 4f);
                foreach (var (dx, dz) in new[] { (0f, 0f), (2.2f, 0f), (0f, 2.2f), (2.2f, 2.2f) })
                    c.Boxes.Add(new CityBox(new Vector3(x + dx, h, z + dz), new Vector3(x + dx + 0.15f, h + 1.6f, z + dz + 0.15f), CitySlot.Metal, CitySlot.Metal));
                c.Boxes.Add(new CityBox(new Vector3(x - 0.1f, h + 1.6f, z - 0.1f), new Vector3(x + 2.45f, h + 3.8f, z + 2.45f), CitySlot.Bark, CitySlot.Roof));
                c.Boxes.Add(new CityBox(new Vector3(x + 0.2f, h + 3.8f, z + 0.2f), new Vector3(x + 2.15f, h + 4.1f, z + 2.15f), CitySlot.Roof, CitySlot.Roof));
            }
        }

        /// <summary>Buildings side by side along u0..u1 from the building line <paramref name="v0"/>, deeper along <paramref name="dir"/>.</summary>
        static void Row(City c, System.Random rng, Edge e, float u0, float u1, float v0, float dir, System.Func<float> depth, System.Func<int> storeys)
        {
            float u = u0 + 0.5f;
            while (u < u1 - 6f)
            {
                float w = Mathf.Min(u1 - 0.5f - u, Range(rng, 10f, 20f));
                if (u1 - 0.5f - (u + w) < 6f) w = u1 - 0.5f - u;
                Building(c, rng, e, u, u + w, v0, 0f, storeys(), dir * depth());
                u += w + Range(rng, 0f, 1.5f);
            }
        }

        /// <summary>A street lamp at (u, v): a post, an arm along <paramref name="dir"/>·v over the street, a glowing head.</summary>
        static void Lamp(City c, Edge e, float u, float v, float dir)
        {
            const float pole = 0.14f, height = 6f, arm = 1.6f;
            var p = e.At(u, v);
            var post = new CityBox(new Vector3(p.x - pole / 2f, WalkTop, p.y - pole / 2f), new Vector3(p.x + pole / 2f, height, p.y + pole / 2f), CitySlot.Metal, CitySlot.Metal);
            c.Boxes.Add(post);
            c.Solids.Add(post.Bounds);
            var tip = e.At(u, v + dir * arm);
            var a = Vector2.Min(p, tip) - Vector2.one * 0.05f;
            var b = Vector2.Max(p, tip) + Vector2.one * 0.05f;
            c.Boxes.Add(new CityBox(new Vector3(a.x, height - 0.12f, a.y), new Vector3(b.x, height - 0.02f, b.y), CitySlot.Metal, CitySlot.Metal));
            c.Boxes.Add(new CityBox(new Vector3(tip.x - 0.25f, height - 0.22f, tip.y - 0.25f), new Vector3(tip.x + 0.25f, height - 0.12f, tip.y + 0.25f), CitySlot.LampGlow, CitySlot.Metal));
            c.Lamps.Add(new Vector3(tip.x, height - 0.25f, tip.y));
        }

        /// <summary>A small street tree: a trunk and a blocky crown well over head height.</summary>
        static void Tree(City c, System.Random rng, Edge e, float u, float v)
        {
            var p = e.At(u, v);
            float h = Range(rng, 2.4f, 3f), s = Range(rng, 1.8f, 2.4f);
            CityBox At(Vector3 lo, Vector3 hi, CitySlot slot) { var b = new CityBox(lo, hi, slot, slot); c.Boxes.Add(b); return b; }
            // the tree pit in the paving, its iron grid's frame, the trunk, a crown of three tiers
            At(new Vector3(p.x - 0.6f, WalkTop, p.y - 0.6f), new Vector3(p.x + 0.6f, WalkTop + 0.006f, p.y + 0.6f), CitySlot.Soil);
            At(new Vector3(p.x - 0.62f, WalkTop, p.y - 0.62f), new Vector3(p.x + 0.62f, WalkTop + 0.012f, p.y - 0.56f), CitySlot.Metal);
            At(new Vector3(p.x - 0.62f, WalkTop, p.y + 0.56f), new Vector3(p.x + 0.62f, WalkTop + 0.012f, p.y + 0.62f), CitySlot.Metal);
            var trunk = At(new Vector3(p.x - 0.11f, WalkTop, p.y - 0.11f), new Vector3(p.x + 0.11f, h + 0.4f, p.y + 0.11f), CitySlot.Bark);
            c.Solids.Add(trunk.Bounds);
            float y = h - 0.2f;
            foreach (var (k, dx, dz) in new[] { (1f, 0f, 0f), (0.72f, 0.18f, -0.12f), (0.45f, -0.08f, 0.1f) })
            {
                float w = s * k, t = s * k * 0.62f;
                At(new Vector3(p.x + dx * s - w / 2f, y, p.y + dz * s - w / 2f), new Vector3(p.x + dx * s + w / 2f, y + t, p.y + dz * s + w / 2f), CitySlot.Leaves);
                y += t * 0.7f;
            }
        }

        /// <summary>A bench along the kerb, facing <paramref name="dir"/>·v (to the street): a wooden seat and back on iron legs.</summary>
        static void Bench(City c, Edge e, float u, float v, float dir)
        {
            Box(c, e, u - 0.9f, u + 0.9f, v - 0.22f, v + 0.22f, WalkTop + 0.42f, WalkTop + 0.47f, CitySlot.Bark, CitySlot.Bark);
            Box(c, e, u - 0.9f, u + 0.9f, v - dir * 0.24f, v - dir * 0.2f, WalkTop + 0.55f, WalkTop + 0.9f, CitySlot.Bark, CitySlot.Bark);
            foreach (float lu in new[] { u - 0.75f, u + 0.75f })
                Box(c, e, lu - 0.03f, lu + 0.03f, v - 0.24f, v + 0.2f, WalkTop, WalkTop + 0.9f, CitySlot.Metal, CitySlot.Metal);
            var a = e.At(u - 0.9f, v - 0.24f);
            var b = e.At(u + 0.9f, v + 0.24f);
            c.Solids.Add(new Bounds(new Vector3((a.x + b.x) / 2f, WalkTop + 0.45f, (a.y + b.y) / 2f), new Vector3(Mathf.Abs(a.x - b.x), 0.9f, Mathf.Abs(a.y - b.y))));
        }

        /// <summary>A litter bin on a post.</summary>
        static void Bin(City c, Edge e, float u, float v)
        {
            var bin = Box(c, e, u - 0.22f, u + 0.22f, v - 0.22f, v + 0.22f, WalkTop + 0.25f, WalkTop + 0.95f, CitySlot.Metal, CitySlot.Tyre);
            Box(c, e, u - 0.04f, u + 0.04f, v - 0.04f, v + 0.04f, WalkTop, WalkTop + 0.25f, CitySlot.Metal, CitySlot.Metal);
            c.Solids.Add(new Bounds(bin.Center - Vector3.up * 0.12f, bin.Size + Vector3.up * 0.25f));
        }

        /// <summary>A short iron post by a crossing.</summary>
        static void Bollard(City c, Edge e, float u, float v)
        {
            var b = Box(c, e, u - 0.07f, u + 0.07f, v - 0.07f, v + 0.07f, WalkTop, WalkTop + 0.85f, CitySlot.Metal, CitySlot.Stone);
            c.Solids.Add(b.Bounds);
        }

        /// <summary>A red fire hydrant.</summary>
        static void Hydrant(City c, Edge e, float u, float v)
        {
            var b = Box(c, e, u - 0.12f, u + 0.12f, v - 0.12f, v + 0.12f, WalkTop, WalkTop + 0.6f, CitySlot.CarRed, CitySlot.CarRed);
            Box(c, e, u - 0.2f, u + 0.2f, v - 0.05f, v + 0.05f, WalkTop + 0.35f, WalkTop + 0.45f, CitySlot.CarRed, CitySlot.CarRed);
            c.Solids.Add(b.Bounds);
        }

        /// <summary>Traffic lights at a corner: a post and a box of lamps facing the street (red, amber, green from the top).</summary>
        static void TrafficLight(City c, Edge e, float u, float v, float dir)
        {
            var post = Box(c, e, u - 0.06f, u + 0.06f, v - 0.06f, v + 0.06f, WalkTop, WalkTop + 3.1f, CitySlot.Metal, CitySlot.Metal);
            c.Solids.Add(post.Bounds);
            Box(c, e, u - 0.16f, u + 0.16f, v - 0.12f, v + 0.12f, WalkTop + 2.2f, WalkTop + 3.05f, CitySlot.Tyre, CitySlot.Tyre);
            float face = v + dir * 0.125f;
            Box(c, e, u - 0.09f, u + 0.09f, face, face + dir * 0.02f, WalkTop + 2.82f, WalkTop + 2.98f, CitySlot.TailLight, CitySlot.TailLight);
            Box(c, e, u - 0.09f, u + 0.09f, face, face + dir * 0.02f, WalkTop + 2.27f, WalkTop + 2.43f, CitySlot.LampGlow, CitySlot.LampGlow);
        }

        /// <summary>A parked car along the kerb at <paramref name="v"/>, 4.4 m from u: body, cabin with glass, four wheels.</summary>
        static void Car(City c, System.Random rng, Edge e, float u, float v, int side)
        {
            var paint = (CitySlot)((int)CitySlot.CarRed + rng.Next(3));
            const float len = 4.4f, wide = 1.78f, g = RoadTop;
            float hw = wide / 2f;
            // it faces the way its lane drives: +u by the near kerb, −u by the far one
            float front = side == 0 ? u + len : u, back = side == 0 ? u : u + len, fd = side == 0 ? 1f : -1f;
            var body = Box(c, e, u + 0.05f, u + len - 0.05f, v - hw, v + hw, g + 0.32f, g + 0.82f, paint, paint);
            c.Solids.Add(new Bounds(body.Center - Vector3.up * 0.16f, body.Size + Vector3.up * 0.32f));
            // the bonnet a step lower than the boot line, the cabin set back with glass round it and a painted roof
            Box(c, e, front - fd * 1.15f, front - fd * 0.05f, v - hw + 0.02f, v + hw - 0.02f, g + 0.82f, g + 0.86f, paint, paint);
            float cab0 = front - fd * 1.25f, cab1 = back + fd * 0.85f;
            Box(c, e, cab0, cab1, v - hw + 0.1f, v + hw - 0.1f, g + 0.82f, g + 1.3f, CitySlot.Glass, CitySlot.Glass);
            Box(c, e, cab0 - fd * 0.12f, cab1 + fd * 0.08f, v - hw + 0.12f, v + hw - 0.12f, g + 1.3f, g + 1.38f, paint, paint);
            // bumpers, lamps, mirrors
            Box(c, e, front - fd * 0.06f, front + fd * 0.06f, v - hw, v + hw, g + 0.3f, g + 0.5f, CitySlot.Tyre, CitySlot.Tyre);
            Box(c, e, back - fd * 0.06f, back + fd * 0.06f, v - hw, v + hw, g + 0.3f, g + 0.5f, CitySlot.Tyre, CitySlot.Tyre);
            foreach (float s in new[] { -1f, 1f })
            {
                Box(c, e, front - fd * 0.06f, front + fd * 0.01f, v + s * (hw - 0.38f), v + s * (hw - 0.08f), g + 0.58f, g + 0.72f, CitySlot.LampGlow, CitySlot.LampGlow);
                Box(c, e, back - fd * 0.01f, back + fd * 0.06f, v + s * (hw - 0.32f), v + s * (hw - 0.08f), g + 0.6f, g + 0.72f, CitySlot.TailLight, CitySlot.TailLight);
                Box(c, e, cab0 - fd * 0.05f, cab0 + fd * 0.1f, v + s * hw, v + s * (hw + 0.16f), g + 0.88f, g + 0.98f, paint, paint);
            }
            foreach (float wu in new[] { u + 0.8f, u + len - 0.8f })
                foreach (float s in new[] { -1f, 1f })
                {
                    float wv = v + s * (hw - 0.09f);
                    Box(c, e, wu - 0.33f, wu + 0.33f, wv - 0.11f, wv + 0.11f, g, g + 0.66f, CitySlot.Tyre, CitySlot.Tyre);
                    Box(c, e, wu - 0.18f, wu + 0.18f, wv + s * 0.11f, wv + s * 0.115f, g + 0.15f, g + 0.51f, CitySlot.CarSilver, CitySlot.CarSilver); // the hub
                }
        }

        /// <summary>
        /// Appends a box's faces (sides into the triangles of <see cref="CityBox.Sides"/>, the top into those of
        /// <see cref="CityBox.Top"/>; no bottom) to shared vertex lists. Texture coordinates are world metres over the slot's
        /// <see cref="Tile"/>, so neighbouring faces continue each other and facades start at the ground.
        /// </summary>
        /// <summary>
        /// Cuts <paramref name="hole"/> (on the ground) out of every box under it: the shaft down to the basement goes through
        /// the asphalt laid under the whole block (its top seen from the lobby was a black plate over the shaft).
        /// </summary>
        public static void CutHole(City c, Rect hole)
        {
            for (int i = c.Boxes.Count - 1; i >= 0; i--)
            {
                var b = c.Boxes[i];
                var min = b.Center - b.Size / 2f;
                var max = b.Center + b.Size / 2f;
                if (max.x <= hole.xMin || min.x >= hole.xMax || max.z <= hole.yMin || min.z >= hole.yMax) continue;
                c.Boxes.RemoveAt(i);
                foreach (var (a, z) in ArchitecturePlanner.CutHoles(new Vector2(min.x, min.z), new Vector2(max.x, max.z), new List<Rect> { hole }))
                    c.Boxes.Add(new CityBox(new Vector3(a.x, min.y, a.y), new Vector3(z.x, max.y, z.y), b.Sides, b.Top));
            }
        }

        public static void AddFaces(CityBox b, List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int>[] tris)
        {
            var min = b.Center - b.Size / 2f;
            var max = b.Center + b.Size / 2f;
            void Quad(Vector3 a, Vector3 c1, Vector3 c2, Vector3 d, Vector3 n, CitySlot slot, System.Func<Vector3, Vector2> uv)
            {
                // a → c1 → c2 → d clockwise seen from the front
                int i = verts.Count;
                foreach (var p in new[] { a, c1, c2, d })
                {
                    verts.Add(p);
                    normals.Add(n);
                    uvs.Add(uv(p));
                }
                var t = tris[(int)slot];
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
                t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }
            float top = Tile(b.Top);
            Quad(new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z),
                Vector3.up, b.Top, p => new Vector2(p.x / top, p.z / top));
            if (b.Size.y < 0.05f) return; // paint on the road: its edges are not worth drawing
            float side = Tile(b.Sides);
            foreach (var n in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                var right = Vector3.Cross(Vector3.up, -n); // the viewer's right, looking at the face from outside
                float half = Mathf.Abs(Vector3.Dot(b.Size, right)) / 2f, out_ = Mathf.Abs(Vector3.Dot(b.Size, n)) / 2f;
                var c = new Vector3(b.Center.x, 0f, b.Center.z) + n * out_;
                var bl = c - right * half + Vector3.up * min.y;
                var br = c + right * half + Vector3.up * min.y;
                // facades count their storeys from the pavement (the house's floor), not from the box's bottom
                Quad(bl, bl + Vector3.up * b.Size.y, br + Vector3.up * b.Size.y, br, n, b.Sides,
                    p => new Vector2(Vector3.Dot(p, right) / side, (p.y - WalkTop) / side));
            }
        }
    }
}
