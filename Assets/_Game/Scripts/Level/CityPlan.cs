using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>What a box of the city is drawn with (an index into <see cref="LevelArt.CityMaterials"/>). Append only.</summary>
    public enum CitySlot { Road, Sidewalk, Paint, Roof, FacadeA, FacadeB, FacadeC, Metal, LampGlow, Leaves, Bark, CarRed, CarBlue, CarSilver, Glass, Tyre }

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
            var rng = new System.Random(LevelRandom.Mix(seed, 0xC17E, 0));
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
            c.Solids.Add(body.Bounds);
            // something on the roof now and then: a machine room, a water tank
            var b = body.Bounds;
            if (rng.NextDouble() < 0.6 && b.size.x > 7f && b.size.z > 7f)
            {
                float w = Range(rng, 2.5f, 4.5f), d = Range(rng, 2.5f, 4.5f);
                float x = Range(rng, b.min.x + 1f, b.max.x - 1f - w), z = Range(rng, b.min.z + 1f, b.max.z - 1f - d);
                c.Boxes.Add(new CityBox(new Vector3(x, h, z), new Vector3(x + w, h + Range(rng, 1.8f, 3f), z + d), CitySlot.Roof, CitySlot.Roof));
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
            float h = Range(rng, 2.4f, 3f), s = Range(rng, 1.6f, 2.2f);
            var trunk = new CityBox(new Vector3(p.x - 0.13f, WalkTop, p.y - 0.13f), new Vector3(p.x + 0.13f, h, p.y + 0.13f), CitySlot.Bark, CitySlot.Bark);
            c.Boxes.Add(trunk);
            c.Solids.Add(trunk.Bounds);
            c.Boxes.Add(new CityBox(new Vector3(p.x - s / 2f, h - 0.3f, p.y - s / 2f), new Vector3(p.x + s / 2f, h - 0.3f + s * 0.9f, p.y + s / 2f), CitySlot.Leaves, CitySlot.Leaves));
        }

        /// <summary>A parked car along the kerb at <paramref name="v"/>, 4.4 m from u: body, cabin with glass, four wheels.</summary>
        static void Car(City c, System.Random rng, Edge e, float u, float v, int side)
        {
            var paint = (CitySlot)((int)CitySlot.CarRed + rng.Next(3));
            const float len = 4.4f, wide = 1.8f, ground = RoadTop;
            var body = Box(c, e, u, u + len, v - wide / 2f, v + wide / 2f, ground + 0.28f, ground + 0.85f, paint, paint);
            c.Solids.Add(new Bounds(body.Center - Vector3.up * 0.14f, body.Size + Vector3.up * 0.28f));
            float cab0 = side == 0 ? u + 1.1f : u + 1.0f;
            Box(c, e, cab0, cab0 + 2.2f, v - wide / 2f + 0.12f, v + wide / 2f - 0.12f, ground + 0.85f, ground + 1.38f, CitySlot.Glass, paint);
            foreach (float wu in new[] { u + 0.75f, u + len - 0.75f })
                foreach (float s in new[] { -1f, 1f })
                {
                    float wv = v + s * (wide / 2f - 0.08f);
                    Box(c, e, wu - 0.32f, wu + 0.32f, wv - 0.12f, wv + 0.12f, ground, ground + 0.64f, CitySlot.Tyre, CitySlot.Tyre);
                }
        }

        /// <summary>
        /// Appends a box's faces (sides into the triangles of <see cref="CityBox.Sides"/>, the top into those of
        /// <see cref="CityBox.Top"/>; no bottom) to shared vertex lists. Texture coordinates are world metres over the slot's
        /// <see cref="Tile"/>, so neighbouring faces continue each other and facades start at the ground.
        /// </summary>
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
