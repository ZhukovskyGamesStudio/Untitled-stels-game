using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// What a box or a model part of the city is drawn with (an index into <see cref="LevelArt.CityMaterials"/>). Append
    /// only. <see cref="Livery"/> and <see cref="Pane"/> are never drawn: a model's part named so takes its copy's
    /// <see cref="CityProp.Paint"/> / <see cref="CityProp.Pane"/>.
    /// </summary>
    public enum CitySlot { Road, Sidewalk, Paint, Roof, FacadeA, FacadeB, FacadeC, Metal, LampGlow, Leaves, Bark, CarRed, CarBlue, CarSilver, Glass, Tyre,
        Stone, Plinth, TailLight, Soil, Livery, Pane, WindowLit, WindowDim, WindowDark, CarWhite, CarBlack, CarGreen, Wood, Concrete, Chrome, Frame }

    /// <summary>
    /// A model of the city (Tools/Blender/street.py → Art/Models/City/<see cref="CityPlan.ModelFile"/>): street furniture,
    /// parked cars, trees, the facades' kit and the roofs' machines. Append only.
    /// </summary>
    public enum CityPropKind
    {
        Lamp, TreeA, TreeB, TreeC, Bench, Bin, Hydrant, Bollard, TrafficLight, BusStop, Manhole, Planter,
        Sedan, Hatchback, Van,
        WindowA, WindowB, CurtainBay, Shopfront, Entrance, Cornice, Band, Coping, Kerb, Pilaster, Quoin,
        Drainpipe, DrainHead, PipeClip, FireLanding, FireLadder, Balcony, Juliet, WallAc, Dish, Awning, Sign,
        RoofAc, RoofVent, RoofMast, WaterTank, RoofHut,
    }

    /// <summary>One box of the city: its sides in one material, its top in another (a bottom only when it floats).</summary>
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
    /// A copy of a city model: where it stands, which way its front (+z) looks, how it is stretched (x along its front,
    /// y up, z out of it: the facade's profiles and pipes are stretched to their wall), and what its Livery / Pane parts
    /// are drawn with (a car's paint, a building's trim; a window lit, dim or dark).
    /// </summary>
    public struct CityProp
    {
        public CityPropKind Kind;
        public Vector3 Position;
        public float Yaw;
        public Vector3 Scale;
        public CitySlot Paint, Pane;

        public CityProp(CityPropKind kind, Vector3 position, float yaw, Vector3 scale, CitySlot paint = CitySlot.Stone, CitySlot pane = CitySlot.WindowDark)
        {
            Kind = kind;
            Position = position;
            Yaw = yaw;
            Scale = scale;
            Paint = paint;
            Pane = pane;
        }

        public Vector3 Forward => Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;
        public Matrix4x4 Pose => Matrix4x4.TRS(Position, Quaternion.Euler(0f, Yaw, 0f), Scale);
    }

    /// <summary>
    /// The city around a house: boxes to draw (the ground, the buildings' textured bodies), the models placed on and among
    /// them, where the street lamps shine from (each looks down), what is solid (the buildings, parked cars, lamp posts,
    /// tree trunks, benches…) and the pavement players walk on (its top at the house's floor).
    /// </summary>
    public sealed class City
    {
        public readonly List<CityBox> Boxes = new();
        public readonly List<CityProp> Props = new();
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
    /// zebra crossings at the junctions. The pavement is level with the house's floor, the roads a kerb lower.
    /// The ground and the buildings' bodies are boxes (<see cref="CityBox"/>, facades textured with their windows);
    /// everything else is a model (<see cref="CityProp"/>): the street furniture, the cars, the trees, and on the buildings
    /// seen from the yard the facade kit — bands, cornices, copings everywhere, and on those within <see cref="DetailReach"/>
    /// real windows over the texture's (<see cref="WindowOf"/>), shop fronts and entrances, balconies, fire escapes,
    /// pipes and machines. The level builder merges boxes and models into one mesh per material.
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
        /// <summary>How far along the street from the house cars are parked (further they are lost in the dark), metres.</summary>
        public const float CarReach = 60f;
        /// <summary>Buildings closer than this to the house get the facade's windows, shop fronts, balconies and pipes, metres.</summary>
        public const float DetailReach = 40f;
        /// <summary>A storey of the facade textures (Tools/Art/generate_city.py: 4 bays x 4 storeys per tile) — the house's own.</summary>
        public const float Storey = LevelMetrics.FloorHeight;
        /// <summary>A storey's top, the cornice under the parapet starts this far below it, metres (street.py: the windows' heads end under it).</summary>
        const float CorniceDrop = 0.4f;

        static readonly CitySlot[] CarPaints = { CitySlot.CarRed, CitySlot.CarBlue, CitySlot.CarSilver, CitySlot.CarWhite, CitySlot.CarBlack, CitySlot.CarGreen, CitySlot.CarSilver, CitySlot.CarWhite };
        static readonly CitySlot[] ShopPaints = { CitySlot.CarRed, CitySlot.CarBlue, CitySlot.CarGreen, CitySlot.CarBlack, CitySlot.Wood };

        /// <summary>Metres one repeat of a slot's texture covers.</summary>
        public static float Tile(CitySlot s) => s switch
        {
            CitySlot.FacadeA or CitySlot.FacadeB or CitySlot.FacadeC => 4f * Storey,
            CitySlot.Road => 4f,
            CitySlot.Sidewalk => 2f,
            _ => 1f,
        };

        /// <summary>The model file of a kind: city_ and its name in snake case (CurtainBay → city_curtain_bay).</summary>
        public static string ModelFile(CityPropKind kind)
        {
            var name = kind.ToString();
            var sb = new System.Text.StringBuilder("city_");
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i])) sb.Append('_');
                sb.Append(char.ToLowerInvariant(name[i]));
            }
            return sb.ToString();
        }

        /// <summary>How long a parked car of a kind is (its model), metres.</summary>
        public static float CarLength(CityPropKind kind) => kind switch { CityPropKind.Van => 4.8f, CityPropKind.Hatchback => 4.0f, _ => 4.55f };

        public static bool IsCar(CityPropKind k) => k is CityPropKind.Sedan or CityPropKind.Hatchback or CityPropKind.Van;

        /// <summary>Furniture standing on the pavements (not the cars, the manholes in the road or the facades' kit).</summary>
        public static bool OnPavement(CityPropKind k) => k is CityPropKind.Lamp or CityPropKind.TreeA or CityPropKind.TreeB or CityPropKind.TreeC
            or CityPropKind.Bench or CityPropKind.Bin or CityPropKind.Hydrant or CityPropKind.Bollard or CityPropKind.TrafficLight
            or CityPropKind.BusStop or CityPropKind.Planter;

        /// <summary>The slot a model's part is drawn with on this copy: Livery and Pane are filled in per copy.</summary>
        public static CitySlot Resolve(CitySlot part, in CityProp p) => part == CitySlot.Livery ? p.Paint : part == CitySlot.Pane ? p.Pane : part;

        /// <summary>The side of the house's rectangle nearest to a point (0 south −z, 1 east +x, 2 north +z, 3 west −x): its front.</summary>
        public static int SideNearest(Rect house, Vector2 p)
        {
            float s = p.y - house.yMin, e = house.xMax - p.x, n = house.yMax - p.y, w = p.x - house.xMin;
            float best = Mathf.Min(Mathf.Min(s, e), Mathf.Min(n, w));
            return best == s ? 0 : best == e ? 1 : best == n ? 2 : 3;
        }

        /// <summary>The gap between two rectangles (0 when they touch or overlap).</summary>
        public static float Gap(Rect a, Rect b) =>
            new Vector2(Mathf.Max(0f, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax)), Mathf.Max(0f, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax))).magnitude;

        public static City Make(Rect house, int front, int floors, int seed)
        {
            var c = new City();
            var rng = new System.Random(LevelRandom.Mix(seed, Salt.City, 0));
            var e = new Edge(house, front);
            var site = new Site { C = c, E = e, House = house, Seed = seed };
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

            // the asphalt under everything; the pavements of every block a kerb higher (ours round the house, not under it),
            // granite kerbs along their edges on the street and the alley
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
                Kerb(c, e, lo, hi, Walk, 1f);
                Kerb(c, e, lo, hi, roadOut, -1f);
                Kerb(c, e, lo, hi, -B, -1f);
                Kerb(c, e, lo, hi, alley0, 1f);
            }

            // the buildings: our neighbours, the rest of our side, across the street, beyond the alley; towers behind
            int Storeys() => Mathf.Max(2, floors + rng.Next(-1, 3));
            foreach (var (u0, u1) in lots) Building(site, rng, u0, u1, 0f, -Range(rng, Mathf.Max(10f, B - 8f), B), Storeys());
            foreach (var (lo, hi) in blocks)
            {
                bool ours = lo < 0f && hi > L;
                if (!ours) Row(site, rng, lo, hi, 0f, -1f, () => Range(rng, Mathf.Max(10f, B - 8f), B), Storeys);
                Row(site, rng, lo, hi, opposite + 0.5f, 1f, () => Range(rng, 12f, 20f), Storeys);
                Row(site, rng, lo, hi, backRow, -1f, () => Range(rng, 12f, 20f), Storeys);
            }
            for (float u = -Reach + Range(rng, 0f, 20f); u < L + Reach - 20f; u += Range(rng, 26f, 42f))
            {
                float w = Range(rng, 14f, 22f);
                Building(site, rng, u, u + w, opposite + deep + Range(rng, 8f, 20f), 0f, Mathf.Max(floors + 3, rng.Next(7, 15)), Range(rng, 12f, 18f));
                Building(site, rng, u + 10f, u + 10f + w, backRow - deep - Range(rng, 8f, 20f), 0f, Mathf.Max(floors + 3, rng.Next(7, 15)), -Range(rng, 12f, 18f));
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

            // along both kerbs, every 16 m: a lamp (its arm over the street), a tree half way to the next, and between them
            // a bench, a bin or a planter; across the street the same shifted by half; a hydrant now and then, a bus stop
            // across from the house, manholes in the road
            bool busStop = false;
            for (float u = -Reach + 6f; u < L + Reach - 6f; u += 16f)
            {
                if (!InCross(u, 2.5f)) Lamp(c, e, u, Walk - 0.5f, 1f);
                if (!InCross(u + 8f, 2.5f)) Lamp(c, e, u + 8f, roadOut + 0.5f, -1f);
                if (!InCross(u + 8f, 3f)) Tree(c, rng, e, u + 8f, Walk - 1.1f);
                if (!InCross(u, 3f)) Tree(c, rng, e, u, roadOut + 1.1f);
                if (!InCross(u + 4f, 3f)) Furniture(c, rng, e, u + 4f, Walk, 1f);
                if (!busStop && u + 4f > L / 2f - 8f && !InCross(u + 4f, 4f))
                {
                    busStop = true;
                    var at = P(e, u + 4f, roadOut + 2.3f, WalkTop);
                    Put(c, CityPropKind.BusStop, at, -Dir(e.V));
                    c.Solids.Add(Flat(e, u + 4f - 1.9f, u + 4f + 1.9f, roadOut + 1.5f, roadOut + 3.1f, 2.6f));
                }
                else if (!InCross(u + 12f, 3f)) Furniture(c, rng, e, u + 12f, roadOut, -1f);
                if (!InCross(u + 1.5f, 3f) && rng.NextDouble() < 0.25) Hydrant(c, e, u + 1.5f, Walk - 0.5f);
                if (rng.NextDouble() < 0.4) Put(c, CityPropKind.Manhole, P(e, u + 6.35f, mid + 1.75f, RoadTop), Dir(e.U));
            }
            // cars parked along both kerbs near the house, facing the way their lane drives
            foreach (var (kerb, side) in new[] { (Walk + 1.1f, 0), (roadOut - 1.1f, 1) })
                for (float u = -CarReach + Range(rng, 2f, 8f); u < L + CarReach - 6f;)
                {
                    int r = rng.Next(10);
                    var kind = r < 5 ? CityPropKind.Sedan : r < 8 ? CityPropKind.Hatchback : CityPropKind.Van;
                    float len = CarLength(kind);
                    if (!InCross(u, 6f) && !InCross(u + len, 6f)) Car(c, rng, e, kind, u, len, kerb, side);
                    u += len + Range(rng, 1.4f, 9f);
                }
            // bollards at the crossings, traffic lights at the junctions
            foreach (var cross in new[] { crossL, crossR })
            {
                foreach (float cu in new[] { cross.lo - CrossWalk - 4.5f, cross.hi + CrossWalk + 4.5f })
                    foreach (float bv in new[] { Walk - 0.32f, roadOut + 0.32f })
                        for (int k = 0; k < 3; k++) Bollard(c, e, cu + (k - 1) * 1.4f, bv);
                // on the corners of the blocks' pavements (the cross street's mouth is road)
                TrafficLight(c, e, cross.lo - CrossWalk - 0.6f, Walk - 0.4f, 1f);
                TrafficLight(c, e, cross.hi + CrossWalk + 0.6f, roadOut + 0.4f, -1f);
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

        /// <summary>What every building of a city shares: where it goes, the house to keep the detail near, the seed of the detail streams.</summary>
        sealed class Site
        {
            public City C;
            public Edge E;
            public Rect House;
            public int Seed, Count;
        }

        static Vector3 P(Edge e, float u, float v, float y)
        {
            var p = e.At(u, v);
            return new Vector3(p.x, y, p.y);
        }

        static Vector3 Dir(Vector2 d) => new(d.x, 0f, d.y);

        static float YawOf(Vector3 forward) => Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        static void Put(City c, CityPropKind kind, Vector3 at, Vector3 forward, Vector3? scale = null, CitySlot paint = CitySlot.Stone, CitySlot pane = CitySlot.WindowDark) =>
            c.Props.Add(new CityProp(kind, at, YawOf(forward), scale ?? Vector3.one, paint, pane));

        /// <summary>A solid in an edge's frame: u0..u1 along, v0..v1 out (either order), from the pavement up.</summary>
        static Bounds Flat(Edge e, float u0, float u1, float v0, float v1, float height, float y0 = WalkTop)
        {
            var a = e.At(u0, v0);
            var b = e.At(u1, v1);
            return new Bounds(new Vector3((a.x + b.x) / 2f, y0 + height / 2f, (a.y + b.y) / 2f), new Vector3(Mathf.Abs(a.x - b.x), height, Mathf.Abs(a.y - b.y)));
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

        /// <summary>Kerb stones along a pavement's edge at v from u0 to u1, the road on the side of <paramref name="road"/>·v.</summary>
        static void Kerb(City c, Edge e, float u0, float u1, float v, float road)
        {
            if (u1 - u0 < 1f) return;
            Put(c, CityPropKind.Kerb, P(e, (u0 + u1) / 2f, v, WalkTop), Dir(e.V) * road, new Vector3(u1 - u0, 1f, 1f));
        }

        /// <summary>
        /// A building on u0..u1 from the building line <paramref name="v0"/> back to <paramref name="v1"/> (or, with
        /// <paramref name="depth"/> ≠ 0, from v0 that deep: the sign is the direction), so many storeys tall: its textured
        /// body and a stone plinth round the ground storey, then the kit (<see cref="Details"/>) from its own stream.
        /// </summary>
        static void Building(Site st, System.Random rng, float u0, float u1, float v0, float v1, int storeys, float depth = 0f)
        {
            var c = st.C;
            var e = st.E;
            if (depth != 0f) v1 = v0 + depth;
            var facade = (CitySlot)((int)CitySlot.FacadeA + rng.Next(3));
            float h = storeys * Storey + Range(rng, 0.3f, 1.0f); // a parapet over the top storey
            var body = Box(c, e, u0, u1, v0, v1, WalkTop, h, facade, CitySlot.Roof);
            float f = Mathf.Sign(v0 - v1);                         // the street side: v grows out of the front along f
            var trim = facade == CitySlot.FacadeC ? CitySlot.Metal : CitySlot.Stone;
            var plinth = Box(c, e, u0 - 0.08f, u1 + 0.08f, v0 + f * 0.08f, v1 - f * 0.08f, WalkTop, Storey, CitySlot.Plinth, trim);
            c.Solids.Add(plinth.Bounds);
            var detail = new System.Random(LevelRandom.Mix(st.Seed, Salt.CityDetail, st.Count++));
            Details(st, detail, body.Bounds, Dir(e.V) * f, facade, trim, storeys, h);
        }

        /// <summary>Where the windows of a facade texture are in its storey-square cell: x0, y0, x1, y1 in 0..1 (Tools/Art/generate_city.py, street.py).</summary>
        public static Vector4 WindowOf(CitySlot facade) => facade switch
        {
            CitySlot.FacadeA => new Vector4(0.18f, 0.3f, 0.82f, 0.82f),
            CitySlot.FacadeB => new Vector4(0.25f, 0.28f, 0.75f, 0.8f),
            _ => new Vector4(0.04f, 0.12f, 0.96f, 0.94f),
        };

        /// <summary>A window's glass: mostly dark, some lit warm, a few in a television's blue.</summary>
        static CitySlot PaneOf(System.Random rng)
        {
            double r = rng.NextDouble();
            return r < 0.3 ? CitySlot.WindowLit : r < 0.4 ? CitySlot.WindowDim : CitySlot.WindowDark;
        }

        /// <summary>
        /// The facade kit on every face of a building seen from the yard: bands at the floor lines, the cornice under the
        /// parapet and its coping (the curtain wall only the coping). Within <see cref="DetailReach"/> of the house also
        /// the windows over the texture's (they line up: both count from the pavement and along the world's axes, AddFaces),
        /// pilasters between the bays of a plain front, quoins up a brick one's corners, the curtain wall's bays, on the
        /// street front the ground floor's shops (glass, fascia, sign, awning) and the entrance, drainpipes at its corners;
        /// balconies, French balconies, a fire escape on a brick side, air conditioners and dishes. On every roof:
        /// a machine room, condensers, vents, masts, a water tank on the tall ones.
        /// </summary>
        static void Details(Site st, System.Random rng, Bounds b, Vector3 front, CitySlot facade, CitySlot trim, int storeys, float h)
        {
            var c = st.C;
            var win = WindowOf(facade);
            bool glass = facade == CitySlot.FacadeC, brick = facade == CitySlot.FacadeB, plain = facade == CitySlot.FacadeA;
            var footprint = new Rect(b.min.x, b.min.z, b.size.x, b.size.z);
            bool near = Gap(footprint, st.House) < DetailReach;
            var yard = new Rect(st.House.xMin - Yard, st.House.yMin - Yard, st.House.width + 2f * Yard, st.House.height + 2f * Yard);
            float top = storeys * Storey;
            var shop = ShopPaints[rng.Next(ShopPaints.Length)];
            bool shops = !glass && storeys >= 2 && rng.NextDouble() < 0.75, balconies = plain && storeys >= 3 && rng.NextDouble() < 0.6;
            foreach (var n in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                bool alongX = Mathf.Abs(n.z) > 0.5f;
                var right = Vector3.Cross(Vector3.up, -n);
                float s = alongX ? right.x : right.z;                  // the texture's u = s * the world coordinate along the face
                float lo = alongX ? b.min.x : b.min.z, hi = alongX ? b.max.x : b.max.z;
                float nSign = alongX ? n.z : n.x;
                float plane = nSign > 0 ? (alongX ? b.max.z : b.max.x) : (alongX ? b.min.z : b.min.x);
                // a face nobody in the yard stands in front of is never seen: no kit on it
                bool seen = nSign > 0 ? (alongX ? yard.yMax > plane : yard.xMax > plane) : (alongX ? yard.yMin < plane : yard.xMin < plane);
                if (!seen) continue;
                bool isFront = Vector3.Dot(n, front) > 0.5f, side = Mathf.Abs(Vector3.Dot(n, front)) < 0.5f;
                float yaw = YawOf(n);
                // a piece on the face: at a along it (world), o out of it, its origin at height y
                void Piece(CityPropKind k, float a, float o, float y, Vector3 scale, CitySlot paint = CitySlot.Stone, CitySlot pane = CitySlot.WindowDark)
                {
                    var at = alongX ? new Vector3(a, y, plane + nSign * o) : new Vector3(plane + nSign * o, y, a);
                    c.Props.Add(new CityProp(k, at, yaw, scale, paint, pane));
                }
                // a profile the length of the face: faces along x run past the corners by its projection, the others stop
                // short by its back, so no corner holds two copies in one plane
                void Run(CityPropKind k, float y, float proj, float back)
                {
                    float a0 = alongX ? lo - proj : lo + back, a1 = alongX ? hi + proj : hi - back;
                    Piece(k, (a0 + a1) / 2f, 0f, y, new Vector3(a1 - a0, 1f, 1f), trim);
                }
                if (!glass)
                {
                    for (int k = 1; k < storeys; k++) Run(CityPropKind.Band, k * Storey - 0.1f, 0.14f, 0.03f);
                    Run(CityPropKind.Cornice, top - CorniceDrop, 0.42f, 0.03f);
                }
                Run(CityPropKind.Coping, h, 0.07f, 0.35f);
                if (!near) continue;

                float tlo = Mathf.Min(s * lo, s * hi), thi = Mathf.Max(s * lo, s * hi);
                int k0 = Mathf.CeilToInt(tlo / Storey);
                // the bays of the texture that lie whole on the face: (middle of the bay, middle of its window)
                var bays = new List<(float mid, float win)>();
                for (int k = k0; (k + 1) * Storey <= thi + 0.01f; k++)
                {
                    float a0 = s * k * Storey, a1 = s * (k + 1) * Storey;
                    if (Mathf.Min(a0, a1) < lo + 0.05f || Mathf.Max(a0, a1) > hi - 0.05f) continue;
                    bays.Add(((a0 + a1) / 2f, s * (k + (win.x + win.z) / 2f) * Storey));
                }
                float centre = (lo + hi) / 2f;
                int door = -1;
                for (int i = 0; i < bays.Count; i++)
                    if (door < 0 || Mathf.Abs(bays[i].mid - centre) < Mathf.Abs(bays[door].mid - centre)) door = i;
                int escape = brick && side && storeys >= 3 && bays.Count > 0 && rng.NextDouble() < 0.5 ? rng.Next(bays.Count) : -1;
                var window = plain ? CityPropKind.WindowA : CityPropKind.WindowB;
                for (int i = 0; i < bays.Count; i++)
                {
                    var (mid, w) = bays[i];
                    bool balcony = balconies && isFront && i % 2 == 1;
                    for (int y = 0; y < storeys; y++)
                    {
                        float o = y == 0 ? 0.08f : 0f; // the ground storey's kit stands on the plinth's face
                        if (y == 0 && isFront && i == door)
                        {
                            Piece(CityPropKind.Entrance, mid, o, 0f, Vector3.one, trim);
                            continue;
                        }
                        if (glass)
                        {
                            Piece(CityPropKind.CurtainBay, mid, o, y * Storey, Vector3.one, trim, PaneOf(rng));
                            continue;
                        }
                        if (y == 0 && isFront && shops)
                        {
                            Piece(CityPropKind.Shopfront, mid, o, 0f, Vector3.one, shop, PaneOf(rng));
                            if (rng.NextDouble() < 0.5) Piece(CityPropKind.Sign, mid, o + 0.105f, 3.1f, Vector3.one);
                            if (rng.NextDouble() < 0.5) Piece(CityPropKind.Awning, mid, o, 2.4f, new Vector3(Storey - 0.5f, 1f, 1f), ShopPaints[rng.Next(ShopPaints.Length)]);
                            continue;
                        }
                        Piece(window, w, o, (y + win.y) * Storey, Vector3.one, trim, PaneOf(rng));
                        if (y == 0) continue;
                        if (balcony) Piece(CityPropKind.Balcony, w, 0f, y * Storey + 0.02f, Vector3.one);
                        if (i == escape)
                        {
                            Piece(CityPropKind.FireLanding, w, 0f, y * Storey + 0.05f, Vector3.one);
                            if (y + 1 < storeys) Piece(CityPropKind.FireLadder, w, 0f, y * Storey + 0.05f, Vector3.one);
                        }
                        else if (brick && rng.NextDouble() < 0.2) Piece(CityPropKind.Juliet, w, 0f, (y + win.y) * Storey, Vector3.one);
                        if (!balcony && i != escape && rng.NextDouble() < 0.12)
                            Piece(CityPropKind.WallAc, w + (rng.NextDouble() < 0.5 ? -0.7f : 0.7f), 0f, y * Storey + 0.3f, Vector3.one);
                    }
                }
                // pilasters between the bays of a plain front, quoins up the corners of a brick one
                if (plain)
                    for (int k = k0; k * Storey <= thi + 0.01f; k++)
                    {
                        float at = s * k * Storey;
                        if (at < lo + 0.3f || at > hi - 0.3f) continue;
                        float y0 = Storey + 0.12f, y1 = top - CorniceDrop;
                        if (y1 - y0 > 0.5f) Piece(CityPropKind.Pilaster, at, 0f, y0, new Vector3(1f, y1 - y0, 1f), trim);
                    }
                if (brick)
                    foreach (bool atLo in new[] { true, false })
                    {
                        float edge = atLo ? lo : hi, inward = atLo ? 1f : -1f;
                        float start = alongX ? edge - inward * 0.05f : edge + inward * 0.03f;
                        for (int i = 0; Storey + 0.12f + (i + 1) * 0.45f < top - CorniceDrop; i++)
                        {
                            float w = i % 2 == 0 ? 0.5f : 0.32f;
                            Piece(CityPropKind.Quoin, start + inward * w / 2f, 0f, Storey + 0.12f + i * 0.45f, new Vector3(w / 0.5f, 1f, 1f), trim);
                        }
                    }
                // drainpipes down the street front's corners, a hopper under the cornice, clips every storey
                if (isFront && !glass)
                    foreach (float a in new[] { lo + 0.14f, hi - 0.14f })
                    {
                        float pipe = top - CorniceDrop - 0.34f;
                        Piece(CityPropKind.Drainpipe, a, 0.03f, 0f, new Vector3(1f, pipe, 1f));
                        Piece(CityPropKind.DrainHead, a, 0.03f, pipe - 0.04f, Vector3.one);
                        for (float y = 1.5f; y < pipe - 0.5f; y += Storey) Piece(CityPropKind.PipeClip, a, 0.03f, y, Vector3.one);
                    }
                // satellite dishes on the piers of a brick front
                for (int d = 0, dishes = brick && storeys >= 3 ? rng.Next(0, 3) : 0; d < dishes; d++)
                {
                    float at = s * (k0 + rng.Next(Mathf.Max(1, Mathf.FloorToInt((thi - k0 * Storey) / Storey) + 1))) * Storey;
                    if (at < lo + 0.6f || at > hi - 0.6f) continue;
                    Piece(CityPropKind.Dish, at, 0f, rng.Next(1, storeys) * Storey + 2.0f, Vector3.one);
                }
            }
            Roof(c, rng, b, front, storeys, h, near);
        }

        /// <summary>The machines on a roof: a machine room now and then, condensers, vent stacks, masts (fewer far away), a water tank on a tall one.</summary>
        static void Roof(City c, System.Random rng, Bounds b, Vector3 front, int storeys, float h, bool near)
        {
            var taken = new List<Rect>();
            float margin = 0.8f; // clear of the coping round the parapet
            bool Spot(float w, float d, out Vector2 at)
            {
                at = default;
                if (b.size.x < w + 2f * margin || b.size.z < d + 2f * margin) return false;
                for (int t = 0; t < 4; t++)
                {
                    var r = new Rect(Range(rng, b.min.x + margin, b.max.x - margin - w), Range(rng, b.min.z + margin, b.max.z - margin - d), w, d);
                    bool free = true;
                    foreach (var o in taken)
                        if (Gap(o, r) < 0.4f) { free = false; break; }
                    if (!free) continue;
                    taken.Add(r);
                    at = r.center;
                    return true;
                }
                return false;
            }
            bool alongZ = Mathf.Abs(front.z) > 0.5f; // the front looks along z: a model's x runs along the world's x
            if (rng.NextDouble() < 0.6)
            {
                float w = Range(rng, 2.5f, 4.5f), d = Range(rng, 2.5f, 4.5f), hut = Range(rng, 2.2f, 3f);
                if (Spot(w, d, out var at))
                    Put(c, CityPropKind.RoofHut, new Vector3(at.x, h, at.y), front, new Vector3((alongZ ? w : d) / 3.24f, hut / 2.55f, (alongZ ? d : w) / 3.24f));
            }
            if (storeys >= 6 && rng.NextDouble() < 0.5 && Spot(2.5f, 2.5f, out var tank))
                Put(c, CityPropKind.WaterTank, new Vector3(tank.x, h, tank.y), front);
            for (int i = 0, things = near ? rng.Next(2, 6) : rng.Next(0, 3); i < things; i++)
            {
                int kind = rng.Next(3);
                var k = kind == 0 ? CityPropKind.RoofAc : kind == 1 ? CityPropKind.RoofVent : CityPropKind.RoofMast;
                float size = k == CityPropKind.RoofAc ? 1.6f : k == CityPropKind.RoofVent ? 0.7f : 2.9f;
                if (Spot(size, size, out var at))
                    Put(c, k, new Vector3(at.x, h, at.y), Quaternion.Euler(0f, rng.Next(4) * 90f, 0f) * front);
            }
        }

        /// <summary>Buildings side by side along u0..u1 from the building line <paramref name="v0"/>, deeper along <paramref name="dir"/>.</summary>
        static void Row(Site st, System.Random rng, float u0, float u1, float v0, float dir, System.Func<float> depth, System.Func<int> storeys)
        {
            float u = u0 + 0.5f;
            while (u < u1 - 6f)
            {
                float w = Mathf.Min(u1 - 0.5f - u, Range(rng, 10f, 20f));
                if (u1 - 0.5f - (u + w) < 6f) w = u1 - 0.5f - u;
                Building(st, rng, u, u + w, v0, 0f, storeys(), dir * depth());
                u += w + Range(rng, 0f, 1.5f);
            }
        }

        /// <summary>A street lamp at (u, v), its arm along <paramref name="dir"/>·v over the street; its light hangs under the head.</summary>
        static void Lamp(City c, Edge e, float u, float v, float dir)
        {
            const float arm = 1.62f, light = 5.75f; // street.py: the luminaire's lens
            var p = P(e, u, v, WalkTop);
            Put(c, CityPropKind.Lamp, p, Dir(e.V) * dir);
            c.Solids.Add(new Bounds(p + Vector3.up * 3f, new Vector3(0.3f, 6f, 0.3f)));
            c.Lamps.Add(P(e, u, v + dir * arm, light));
        }

        /// <summary>A small street tree in its pit (one of three, turned by a quarter so the grate stays square to the paving).</summary>
        static void Tree(City c, System.Random rng, Edge e, float u, float v)
        {
            var kind = (CityPropKind)((int)CityPropKind.TreeA + rng.Next(3));
            var p = P(e, u, v, WalkTop);
            c.Props.Add(new CityProp(kind, p, rng.Next(4) * 90f, Vector3.one));
            c.Solids.Add(new Bounds(p + Vector3.up * 1.4f, new Vector3(0.32f, 2.8f, 0.32f)));
        }

        /// <summary>A bench facing the street, a bin or a planter on the pavement whose edge is at <paramref name="edge"/> (the road along <paramref name="dir"/>·v).</summary>
        static void Furniture(City c, System.Random rng, Edge e, float u, float edge, float dir)
        {
            double r = rng.NextDouble();
            if (r < 0.45)
            {
                float v = edge - dir * 0.9f;
                Put(c, CityPropKind.Bench, P(e, u, v, WalkTop), Dir(e.V) * dir);
                c.Solids.Add(Flat(e, u - 0.9f, u + 0.9f, v - 0.3f, v + 0.3f, 0.9f));
            }
            else if (r < 0.8)
            {
                float v = edge - dir * 0.5f;
                Put(c, CityPropKind.Bin, P(e, u, v, WalkTop), Dir(e.V) * dir);
                c.Solids.Add(Flat(e, u - 0.25f, u + 0.25f, v - 0.25f, v + 0.25f, 1.05f));
            }
            else
            {
                float v = edge - dir * 0.75f;
                Put(c, CityPropKind.Planter, P(e, u, v, WalkTop), Dir(e.V));
                c.Solids.Add(Flat(e, u - 0.62f, u + 0.62f, v - 0.32f, v + 0.32f, 0.5f));
            }
        }

        /// <summary>A short iron post by a crossing.</summary>
        static void Bollard(City c, Edge e, float u, float v)
        {
            var p = P(e, u, v, WalkTop);
            Put(c, CityPropKind.Bollard, p, Dir(e.V));
            c.Solids.Add(new Bounds(p + Vector3.up * 0.475f, new Vector3(0.22f, 0.95f, 0.22f)));
        }

        /// <summary>A red fire hydrant, its big outlet to the street.</summary>
        static void Hydrant(City c, Edge e, float u, float v)
        {
            var p = P(e, u, v, WalkTop);
            Put(c, CityPropKind.Hydrant, p, Dir(e.V));
            c.Solids.Add(new Bounds(p + Vector3.up * 0.35f, new Vector3(0.34f, 0.7f, 0.34f)));
        }

        /// <summary>Traffic lights at a corner, the signal facing the street along <paramref name="dir"/>·v.</summary>
        static void TrafficLight(City c, Edge e, float u, float v, float dir)
        {
            var p = P(e, u, v, WalkTop);
            Put(c, CityPropKind.TrafficLight, p, Dir(e.V) * dir);
            c.Solids.Add(new Bounds(p + Vector3.up * 1.7f, new Vector3(0.24f, 3.4f, 0.24f)));
        }

        /// <summary>A parked car of a kind along the kerb at <paramref name="v"/>, from u that long, facing the way its lane drives (+u by the near kerb, −u by the far one).</summary>
        static void Car(City c, System.Random rng, Edge e, CityPropKind kind, float u, float len, float v, int side)
        {
            var paint = CarPaints[rng.Next(CarPaints.Length)];
            Put(c, kind, P(e, u + len / 2f, v, RoadTop), Dir(e.U) * (side == 0 ? 1f : -1f), null, paint);
            c.Solids.Add(Flat(e, u, u + len, v - 0.95f, v + 0.95f, kind == CityPropKind.Van ? 1.95f : 1.45f, RoadTop));
        }

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

        /// <summary>
        /// Appends a box's faces (sides into the triangles of <see cref="CityBox.Sides"/>, the top into those of
        /// <see cref="CityBox.Top"/>; a bottom only when the box floats over the pavement, or it would be hollow seen from
        /// under it) to shared vertex lists. Texture coordinates are world metres over the slot's <see cref="Tile"/>, so
        /// neighbouring faces continue each other and facades start at the ground.
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
            if (min.y > WalkTop + 0.05f)
                Quad(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z),
                    Vector3.down, b.Sides, p => new Vector2(p.x / side, p.z / side));
        }

        /// <summary>
        /// Appends a placed copy of a model part (its vertices, normals and triangles; <paramref name="pose"/> takes it into
        /// the world) to shared vertex lists, its triangles into <paramref name="slot"/>'s. Texture coordinates are world
        /// metres over the slot's <see cref="Tile"/>, projected along each vertex's main axis, so a stretched profile keeps
        /// its texture's scale. A mirroring pose turns the triangles round so they still face out.
        /// </summary>
        public static void AddModel(Vector3[] v, Vector3[] n, int[] triangles, Matrix4x4 pose, CitySlot slot, List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int>[] tris)
        {
            int start = verts.Count;
            var turn = pose.inverse.transpose;
            float tile = Tile(slot);
            bool hasNormals = n != null && n.Length == v.Length;
            for (int i = 0; i < v.Length; i++)
            {
                var p = pose.MultiplyPoint3x4(v[i]);
                var nn = hasNormals ? turn.MultiplyVector(n[i]) : Vector3.up;
                nn = nn.sqrMagnitude > 1e-12f ? nn.normalized : Vector3.up;
                verts.Add(p);
                normals.Add(nn);
                float ax = Mathf.Abs(nn.x), ay = Mathf.Abs(nn.y), az = Mathf.Abs(nn.z);
                var uv = ay >= ax && ay >= az ? new Vector2(p.x, p.z) : ax >= az ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
                uvs.Add(uv / tile);
            }
            var t = tris[(int)slot];
            bool mirrored = pose.determinant < 0f;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                t.Add(start + triangles[i]);
                t.Add(start + (mirrored ? triangles[i + 2] : triangles[i + 1]));
                t.Add(start + (mirrored ? triangles[i + 1] : triangles[i + 2]));
            }
        }
    }
}
