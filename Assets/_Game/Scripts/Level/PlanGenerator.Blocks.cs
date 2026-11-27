using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// How an office of blocks is laid out: each kind of office house a plan of its own (<see cref="HouseKinds.Apply"/>;
    /// the schemes themselves — PlanGenerator.Blocks). Racetrack: the office. Court: the business centre. Grid: the data
    /// centre. Sheds: the warehouse. Axis: the headquarters. Append only (the value is in the level settings).
    /// </summary>
    public enum OfficeLayout : byte { Racetrack, Court, Grid, Sheds, Axis }

    /// <summary>
    /// Blocks first, corridors between them (Docs/LevelRefinementPlan.md, section 4; prototype Tools/Prototypes/layouts.py):
    /// the mass of a building is a set of wings that overlap, step and turn; it is cut into blocks of 8–15 m by a staggered
    /// partition; the corridors then grow from the entrance along the edges between blocks — the cheapest way to the next
    /// block nobody reaches yet, turns cheap, long straights and the facade dear — and a few shortcuts close loops. Every
    /// block then becomes one room, a row, two rows back to back, a cluster (a private hall with rooms off it), a pinwheel
    /// (rooms round a small hall) or open plan with pods. The partition and the corridors are the building's (they stack, as
    /// a structure does); what a block becomes is drawn per floor.
    /// </summary>
    public static partial class PlanGenerator
    {
        /// <summary>
        /// How a building of blocks is laid out. Every kind of house has a scheme of its own — its own masses, its own way
        /// of cutting blocks, its own corridors — so two kinds are never one plan with other numbers.
        /// Streets (the hospital): corridors that wander between the blocks, cross and loop; rows of rooms, departments.
        /// Hub (the abandoned station): a hall in the middle, a straight spoke down every arm, hardly a loop.
        /// Racetrack (the office): a ring corridor a room's depth from the facade, cellular rooms outside it, a core inside.
        /// Court (the business centre): an atrium through the floors, a gallery round it, suites between it and the facade.
        /// Grid (the data centre): halls as islands, an aisle on every side of each.
        /// Sheds (the warehouse): a strip of rooms along the front, one corridor behind it, big halls opening into each other.
        /// Axis (the headquarters): a gallery straight on from the lobby, a cross gallery behind it, the plan mirrored.
        /// (The repair shop and the apartment block have plans of their own: PlanGenerator.Garage, PlanGenerator.Apartments.)
        /// </summary>
        enum Scheme : byte { Streets, Hub, Racetrack, Court, Grid, Sheds, Axis }

        /// <summary>The building's blocks and corridors (metres for the blocks, plan cells for the corridors).</summary>
        sealed class BlockPlan
        {
            public int W, D;
            public bool Station;
            /// <summary>An office of any kind: open plans with pods, departments, rows of offices; its rooms' types by the kind's weights.</summary>
            public bool Office;
            /// <summary>How it is laid out: its kind of house's own scheme.</summary>
            public Scheme Scheme;
            /// <summary>Court: the atriums, metres (the building's: every floor keeps them whole, a gallery round each).</summary>
            public List<RectInt> Courts = new();
            public bool[,] Ring;        // [x, z] metre cell: the ring corridor runs round these (a racetrack's core, a court) a metre out
            public bool[,] Front;       // [x, z] metre cell of the front of house (Sheds: the strip by the lobby; Grid: by the lobby and the stairs)
            public bool HasLine, HasAxis;
            /// <summary>Grid points, stretch by stretch: the corridor behind the lobby (Sheds, Axis) and the gallery straight on from it (Axis).</summary>
            public readonly List<(Vector2Int a, Vector2Int b)> FrontLines = new(), AxisLines = new();
            public int AxisMid;
            public bool MirrorInX;      // the axis runs along z: the plan is mirrored in x
            /// <summary>The biggest block that stays one room, m² (a lab hall at the station, a store at the warehouse; a hospital's rooms are small).</summary>
            public int Hall = 75;
            /// <summary>What a block becomes, as weights (one room, back to back, a cluster, a pinwheel, open plan); null: the hospital's or the station's.</summary>
            public float[] Weights;
            /// <summary>The width of a room in a row, metres: one through the building, its rhythm (a double room is two).</summary>
            public int Module = 5;
            /// <summary>How often a row has a double room (the top floor of a hospital more: its theatres).</summary>
            public double Doubles = 0.12;
            public bool[,] In;          // [x, z] metre cell inside the building
            public int[,] Block;        // [x, z] metre cell -> block (−1 outside)
            public readonly List<bool> Reserved = new(); // the lobby's and the stair halls' blocks: never cut, never crossed
            public int LobbyBlock = -1;
            public bool[,] Corridor;    // [x, z] plan cell
            public bool[,] Main;        // [x, z] plan cell of the main way (3 m)
            public int Loops, Halls, Unreached;
            /// <summary>Each upper floor's own blocks and corridors (index = floor; [0] is this one): only the stair halls stack.</summary>
            public BlockPlan[] Floors;
            /// <summary>What an upper floor is mostly made of (<see cref="Character"/>): rows, clusters, or as it comes.</summary>
            public Character Kind;

            /// <summary>The plan of a floor.</summary>
            public BlockPlan On(int floor) => Floors != null && floor < Floors.Length && Floors[floor] != null ? Floors[floor] : this;
        }

        /// <summary>Corridors: the main way and the rest, plan cells (3 m and 2 m), on whole metres (no strip of half a metre is left beside one).</summary>
        const int MainCells = 6, LaneCells = 4;

        /// <summary>A floor's character: mostly rows of rooms along the corridors, mostly clusters (departments), or mixed.</summary>
        enum Character : byte { Mixed, Rows, Clusters }

        // ------------------------------------------------------------------ the mass

        /// <summary>The masses a building of blocks may have (metres, x and z from 0), by name.</summary>
        static List<RectInt> BlockShape(string kind, System.Random r, float s)
        {
            int S(float v) => Mathf.Max(8, Mathf.RoundToInt(v * s));
            var l = new List<RectInt>();
            switch (kind)
            {
                case "figure8":
                {
                    int w = S(64), d = S(46), k = r.Next(15, 19);
                    int mid = Mathf.RoundToInt(w * Range(r, 0.42f, 0.58f)) - k / 2;
                    l.Add(new RectInt(0, 0, w, k)); l.Add(new RectInt(0, d - k, w, k)); l.Add(new RectInt(0, 0, k, d)); l.Add(new RectInt(w - k, 0, k, d));
                    l.Add(new RectInt(mid, 0, k - 2, d));
                    break;
                }
                case "comb":
                {
                    int L = S(64), k = r.Next(19, 25);
                    l.Add(new RectInt(0, 0, L, k));
                    int x = r.Next(0, 6), n = r.Next(2, 4);
                    for (int i = 0; i < n && x + 15 <= L; i++)
                    {
                        int w = r.Next(16, 21);
                        l.Add(new RectInt(x, k - 2, w, S(r.Next(16, 27))));
                        x += w + r.Next(9, 15);
                    }
                    if (r.NextDouble() < 0.6) { int h = S(13); l.Add(new RectInt(r.Next(18, 35), -h, r.Next(16, 25), h + 2)); }
                    break;
                }
                case "deep":
                {
                    int w = S(r.Next(46, 55)), d = S(r.Next(38, 45));
                    int cw = r.Next(8, 13), cd = r.Next(8, 12), cx = Mathf.RoundToInt(w * Range(r, 0.3f, 0.6f)), cz = Mathf.RoundToInt(d * Range(r, 0.3f, 0.5f));
                    l.Add(new RectInt(0, 0, w, cz)); l.Add(new RectInt(0, cz + cd, w, d - cz - cd));
                    l.Add(new RectInt(0, cz, cx, cd)); l.Add(new RectInt(cx + cw, cz, w - cx - cw, cd));
                    break;
                }
                case "pinwheel":
                {
                    int c = S(22), a = S(24), k = r.Next(18, 23);
                    l.Add(new RectInt(a, a, c, c));
                    l.Add(new RectInt(a - r.Next(2, 7), 0, k, a + 2));
                    l.Add(new RectInt(a + c - 2, a - r.Next(2, 7), a, k));
                    l.Add(new RectInt(a + c - k + r.Next(2, 7), a + c - 2, k, a));
                    l.Add(new RectInt(0, a + c - k + r.Next(2, 7), a + 2, k));
                    break;
                }
                case "campus":
                {
                    int cx = 16, cz = 14, w = S(26), d = S(22);
                    l.Add(new RectInt(cx, cz, w, d));
                    l.Add(new RectInt(cx - r.Next(14, 19), cz + r.Next(-6, 5), 19, r.Next(14, 19)));
                    l.Add(new RectInt(cx + w - 2, cz + r.Next(4, 10), r.Next(18, 23), r.Next(15, 21)));
                    l.Add(new RectInt(cx + r.Next(2, 9), cz + d - 2, r.Next(16, 23), r.Next(14, 19)));
                    l.Add(new RectInt(cx + r.Next(-4, 6), cz - r.Next(11, 15), r.Next(17, 23), 16));
                    break;
                }
                case "cross":
                {
                    int a = S(60), b = S(48), k = r.Next(20, 26);
                    int z = Mathf.RoundToInt(b * Range(r, 0.3f, 0.55f)), x = Mathf.RoundToInt(a * Range(r, 0.28f, 0.5f));
                    l.Add(new RectInt(0, z, a, k)); l.Add(new RectInt(x, 0, k, b)); l.Add(new RectInt(x - r.Next(4, 9), z - r.Next(2, 7), k + 12, k + 8));
                    break;
                }
                case "spokes":
                {
                    // a hub and an arm straight out of each side (one of them missing in some houses)
                    int c = S(20), k = r.Next(15, 19), drop = r.Next(6);
                    int Off() => (c - k) / 2 + r.Next(-2, 3);
                    l.Add(new RectInt(0, 0, c, c));
                    if (drop != 0) l.Add(new RectInt(Off(), c - 2, k, S(r.Next(18, 31)) + 2));
                    if (drop != 1) l.Add(new RectInt(c - 2, Off(), S(r.Next(18, 31)) + 2, k));
                    if (drop != 2) { int n = S(r.Next(18, 31)); l.Add(new RectInt(Off(), -n, k, n + 2)); }
                    if (drop != 3) { int n = S(r.Next(18, 31)); l.Add(new RectInt(-n, Off(), n + 2, k)); }
                    break;
                }
                case "dumbbell":
                {
                    // two hubs and the bridge between them, an arm out of each (and some off the ends)
                    int c = S(20), k = r.Next(15, 19), link = S(r.Next(16, 25)), x2 = c + link;
                    int Off() => (c - k) / 2 + r.Next(-1, 2);
                    l.Add(new RectInt(0, 0, c, c));
                    l.Add(new RectInt(c - 2, Off(), link + 4, k));
                    l.Add(new RectInt(x2, 0, c, c));
                    foreach (int x in new[] { 0, x2 })
                    {
                        int n = S(r.Next(16, 27));
                        l.Add(r.Next(2) == 0 ? new RectInt(x + Off(), c - 2, k, n + 2) : new RectInt(x + Off(), -n, k, n + 2));
                    }
                    if (r.Next(3) > 0) { int n = S(r.Next(14, 23)); l.Add(new RectInt(x2 + c - 2, Off(), n + 2, k)); }
                    if (r.Next(3) > 0) { int n = S(r.Next(14, 23)); l.Add(new RectInt(-n, Off(), n + 2, k)); }
                    break;
                }
                case "z":
                {
                    int k = r.Next(20, 25), L = S(34), mid = S(30);
                    l.Add(new RectInt(0, 0, L, k)); l.Add(new RectInt(L - k, 0, k, mid + k)); l.Add(new RectInt(L - k, mid, L, k));
                    l.Add(new RectInt(L - k - r.Next(6, 11), k + r.Next(2, 6), k + 14, r.Next(14, 19)));
                    break;
                }
                default: // "stagger": bars stepping along
                {
                    int x = 0, z = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        int w = S(r.Next(26, 35)), d = S(r.Next(22, 31));
                        l.Add(new RectInt(x, z, w, d));
                        x += Mathf.RoundToInt(w * Range(r, 0.62f, 0.85f));
                        z += Mathf.RoundToInt(d * Range(r, 0.45f, 0.75f)) * (i % 2 == 0 ? 1 : (r.Next(2) == 0 ? 1 : -1));
                    }
                    break;
                }
            }
            int mx = int.MaxValue, mz = int.MaxValue;
            foreach (var q in l) { mx = Mathf.Min(mx, q.xMin); mz = Mathf.Min(mz, q.yMin); }
            for (int i = 0; i < l.Count; i++) l[i] = new RectInt(l[i].x - mx, l[i].y - mz, l[i].width, l[i].height);
            return l;
        }

        static float Range(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        /// <summary>Union area of rectangles, m².</summary>
        static int UnionArea(List<RectInt> l)
        {
            int w = 0, d = 0;
            foreach (var q in l) { w = Mathf.Max(w, q.xMax); d = Mathf.Max(d, q.yMax); }
            var g = new bool[w, d];
            int n = 0;
            foreach (var q in l)
                for (int x = q.xMin; x < q.xMax; x++)
                    for (int z = q.yMin; z < q.yMax; z++) if (!g[x, z]) { g[x, z] = true; n++; }
            return n;
        }

        static readonly string[] HospitalMasses = { "figure8", "comb", "deep", "pinwheel", "campus", "cross", "stagger" };
        static readonly string[] StationMasses = { "spokes", "dumbbell" }; // (a middle and arms: its hub and spokes; none of them the hospital's)

        /// <summary>The wings of a building of blocks (metres), sized so a floor is about <paramref name="area"/> m².</summary>
        static List<RectInt> BlockMass(Ctx c, System.Random rng, float area, bool station, out string kind)
        {
            var names = station ? StationMasses : HospitalMasses;
            kind = names[rng.Next(names.Length)];
            int seed = rng.Next();
            var first = BlockShape(kind, new System.Random(seed), 1f);
            float s = Mathf.Clamp(Mathf.Sqrt(area / Mathf.Max(1f, UnionArea(first))), 0.55f, 2.4f);
            c.Blocks = new BlockPlan { Station = station, Scheme = station ? Scheme.Hub : Scheme.Streets, Hall = station ? 170 : 75, Module = 4 + new System.Random(seed ^ 0x40D).Next(2) };
            return BlockShape(kind, new System.Random(seed), s);
        }

        /// <summary>
        /// The wings of an office of blocks (metres) and its scheme: the kind of house's own (<see cref="OfficeLayout"/>) —
        /// its masses, what its blocks become, how big a room stays whole. A kind without open space has no open plans.
        /// </summary>
        static List<RectInt> OfficeMass(Ctx c, System.Random rng, float area, BuildingSettings b, out string kind)
        {
            bool open = false;
            foreach (var w in b.RoomWeights) open |= w.Room == RoomType.OpenSpace && w.Weight > 0;
            float o = open ? 1f : 0f;
            var scheme = b.Layout switch
            {
                OfficeLayout.Court => Scheme.Court, OfficeLayout.Grid => Scheme.Grid, OfficeLayout.Sheds => Scheme.Sheds, OfficeLayout.Axis => Scheme.Axis,
                _ => Scheme.Racetrack,
            };
            float scale = Mathf.Clamp(b.RoomSizeScale, 0.7f, 1.8f);
            c.Blocks = new BlockPlan
            {
                Office = true,
                Scheme = scheme,
                Hall = scheme == Scheme.Grid ? 210 : scheme == Scheme.Sheds ? 280 : Mathf.Clamp(Mathf.RoundToInt(90f * scale * scale), 60, 220),
                // (one room, back to back, a cluster, a pinwheel, open plan)
                Weights = scheme switch
                {
                    Scheme.Court => new[] { 0.5f, 0.5f, 4f, 3f, 3f * o },    // suites: a reception and its rooms, open floors
                    Scheme.Grid => new[] { 5f, 2f, 0f, 0f, 0f },             // halls
                    Scheme.Sheds => new[] { 1f, 0f, 0f, 0f, 0f },
                    Scheme.Axis => new[] { 0.6f, 4f, 2.5f, 0.4f, 0.6f * o }, // rows of offices, departments
                    _ => new[] { 2.5f, 2f, 1f, 0.3f, 3f * o },               // the core: service rooms, meeting rooms, open floors
                },
            };
            int seed = rng.Next();
            c.Blocks.Module = 4 + new System.Random(seed ^ 0x40D).Next(2);
            return SchemeShape(c, scheme, new System.Random(seed), area, out kind);
        }

        /// <summary>The masses of the offices' schemes (metres, x and z from 0), sized to <paramref name="area"/> m² a floor.</summary>
        static List<RectInt> SchemeShape(Ctx c, Scheme scheme, System.Random r, float area, out string kind)
        {
            var l = new List<RectInt>();
            int Even(float v) => Mathf.Max(2, Mathf.RoundToInt(v / 2f) * 2);
            RectInt Cells(RectInt m) => new(m.x * M, m.y * M, m.width * M, m.height * M);
            // a house too small for its scheme's masses: one block (its corridors and rooms the scheme's all the same)
            if (area < (scheme == Scheme.Racetrack ? 800f : scheme == Scheme.Axis ? 1000f : scheme == Scheme.Sheds ? 500f : 0f))
            {
                kind = "block";
                int d = Even(Mathf.Sqrt(area / Range(r, 1.2f, 1.7f)));
                l.Add(new RectInt(0, 0, Even(area / d), d));
                return l;
            }
            switch (scheme)
            {
                case Scheme.Court:
                {
                    // one deep block (two courts in a long one): the court, the gallery round it, 8 m and more of rooms behind
                    bool two = area > 2400f && r.NextDouble() < 0.6;
                    float aspect = two ? Range(r, 1.7f, 2f) : Range(r, 1f, 1.25f);
                    int d = Even(Mathf.Sqrt(area / aspect)), w = Even(area / d), n = two ? 2 : 1;
                    kind = two ? "courts" : "court";
                    l.Add(new RectInt(0, 0, w, d));
                    for (int i = 0; i < n; i++)
                    {
                        int a = Mathf.Clamp(Even(w / n * 0.3f), 10, 16), b = Mathf.Clamp(Even(d * 0.3f), 10, 16);
                        if (w / n < a + 20 || d < b + 20) continue; // (too small a house for a court)
                        int cx = w * (2 * i + 1) / (2 * n) + r.Next(-2, 3), cz = d / 2 + r.Next(-2, 3);
                        var court = new RectInt(cx - a / 2, cz - b / 2, a, b);
                        c.Blocks.Courts.Add(court);
                        c.KeepClear.Add(Cells(new RectInt(court.x - 2, court.y - 2, a + 4, b + 4)));
                        // the entrance: straight in front of the first court, the lobby as deep as the rooms there
                        int depth = court.yMin - 2;
                        if (i == 0 && depth >= 7 && depth <= 12) c.LobbyWant = Cells(new RectInt(cx - 5, 0, 10, depth));
                    }
                    break;
                }
                case Scheme.Grid:
                {
                    // a box, or two boxes side by side
                    if (area > 1500f && r.NextDouble() < 0.5)
                    {
                        kind = "boxes";
                        float share = Range(r, 0.55f, 0.65f);
                        int d1 = Even(Mathf.Sqrt(area * share / Range(r, 1.1f, 1.4f))), w1 = Even(area * share / d1);
                        int d2 = Even(d1 * Range(r, 0.6f, 0.8f)), w2 = Even(area * (1f - share) / d2);
                        l.Add(new RectInt(0, 0, w1, d1));
                        l.Add(new RectInt(w1 - 2, Even(r.Next(0, d1 - d2 + 1)), w2 + 2, d2));
                    }
                    else
                    {
                        kind = "box";
                        int d = Even(Mathf.Sqrt(area / Range(r, 1.2f, 1.6f)));
                        l.Add(new RectInt(0, 0, Even(area / d), d));
                    }
                    break;
                }
                case Scheme.Sheds:
                {
                    // one deep shed, or a row of them of different depths
                    int n = area > 2200f ? r.Next(2, 4) : area > 1300f ? r.Next(1, 3) : 1;
                    kind = n == 1 ? "shed" : "sheds";
                    float left = area;
                    int x = 0;
                    for (int i = 0; i < n; i++)
                    {
                        float a = i == n - 1 ? left : area / n * Range(r, 0.8f, 1.2f);
                        left -= a;
                        int d = Even(Mathf.Clamp(Mathf.Sqrt(a / Range(r, 0.9f, 1.4f)), 28f, 44f)), w = Even(Mathf.Max(16f, a / d));
                        l.Add(new RectInt(x, 0, w + (i < n - 1 ? 2 : 0), d));
                        x += w;
                    }
                    break;
                }
                case Scheme.Axis:
                {
                    // mirrored about the entrance: a T, a bar with an entrance pavilion, a trident, an H — the lobby in the middle of the front
                    int k = Even(r.Next(20, 25)), s = Even(r.Next(20, 25)), L, front = 0;
                    var names = area < 1400f ? new[] { "tee", "pavilion" } : new[] { "tee", "pavilion", "trident", "aitch" };
                    kind = names[r.Next(names.Length)];
                    if (kind == "tee")
                    {
                        L = Even(Mathf.Max(s + 16, area * Range(r, 0.5f, 0.62f) / k));
                        l.Add(new RectInt(0, 0, L, k));
                        l.Add(new RectInt(L / 2 - s / 2, k - 2, s, Even(Mathf.Max(12f, (area - L * k) / s)) + 2));
                    }
                    else if (kind == "pavilion")
                    {
                        // the stem runs through: an entrance pavilion in front of the bar
                        int f = Even(r.Next(8, 13));
                        L = Even(Mathf.Max(s + 16, area * 0.5f / k));
                        l.Add(new RectInt(0, f, L, k));
                        l.Add(new RectInt(L / 2 - s / 2, 0, s, f + k + Even(Mathf.Max(12f, (area - L * k - s * f) / s))));
                    }
                    else if (kind == "trident")
                    {
                        int w = Even(r.Next(16, 21));
                        L = Even(Mathf.Max(3 * w + 20, area * 0.42f / k));
                        float rest = area - L * k;
                        int side = Even(Mathf.Max(10f, rest * 0.29f / w));
                        l.Add(new RectInt(0, 0, L, k));
                        l.Add(new RectInt(L / 2 - s / 2, k - 2, s, Even(Mathf.Max(14f, rest * 0.42f / s)) + 2));
                        l.Add(new RectInt(0, k - 2, w, side + 2));
                        l.Add(new RectInt(L - w, k - 2, w, side + 2));
                    }
                    else
                    {
                        // two wings and the bar between them: the entrance in its middle, on the forecourt
                        int gap = Even(Mathf.Max(18f, Mathf.Sqrt(area) * 0.45f));
                        int D = Even(Mathf.Max(k + 12, (area - gap * k) / (2f * k)));
                        front = Even((D - k) * Range(r, 0.3f, 0.5f));
                        L = 2 * k + gap;
                        l.Add(new RectInt(0, 0, k, D));
                        l.Add(new RectInt(k + gap, 0, k, D));
                        l.Add(new RectInt(k - 2, front, gap + 4, k));
                    }
                    int lw = 10 + 2 * r.Next(2), ld = 8 + r.Next(2), far = 0;
                    foreach (var q in l) far = Mathf.Max(far, q.yMax);
                    c.LobbyWant = Cells(new RectInt(L / 2 - lw / 2, front, lw, ld));
                    // neither the gallery on the axis nor the one behind the lobby under a stair
                    c.KeepClear.Add(Cells(new RectInt(L / 2 - 5, front + ld, 10, far)));
                    c.KeepClear.Add(Cells(new RectInt(0, front + ld, L, 3)));
                    break;
                }
                default:
                {
                    // wings deep enough for the ring: rooms, the corridor, a core, the corridor, rooms
                    int k = Even(r.Next(24, 31));
                    float total = area / k; // the wings' length, about
                    var names = new List<string>();
                    if (total <= 76f) names.Add("slab");
                    if (total >= 60f) { names.Add("ell"); names.Add("zed"); }
                    if (total >= 90f) names.Add("horseshoe");
                    kind = names[r.Next(names.Count)];
                    if (kind == "slab") l.Add(new RectInt(0, 0, Even(Mathf.Max(k + 8, total)), k));
                    else if (kind == "ell")
                    {
                        int sum = Even(total + k), a = Even(sum * Range(r, 0.5f, 0.62f)), b = Mathf.Max(k + 8, sum - a);
                        l.Add(new RectInt(0, 0, a, k));
                        l.Add(new RectInt(r.Next(2) == 0 ? 0 : a - k, 0, k, b));
                    }
                    else if (kind == "zed")
                    {
                        int sum = Even(total + 2 * k);
                        int a = Mathf.Max(k + 6, Even(sum * Range(r, 0.27f, 0.35f))), e = Mathf.Max(k + 6, Even(sum * Range(r, 0.27f, 0.35f)));
                        int m = Mathf.Max(2 * k + 4, sum - a - e);
                        l.Add(new RectInt(0, 0, a, k));
                        l.Add(new RectInt(a - k, 0, k, m));
                        l.Add(new RectInt(a - k, m - k, e, k));
                    }
                    else
                    {
                        int sum = Even(total + 2 * k), a = Even(Mathf.Max(2 * k + 12, sum * Range(r, 0.36f, 0.44f))), b = Even(Mathf.Max(k + 8, (sum - a) / 2f));
                        l.Add(new RectInt(0, 0, a, k));
                        l.Add(new RectInt(0, 0, k, b));
                        l.Add(new RectInt(a - k, 0, k, b));
                    }
                    break;
                }
            }
            return l;
        }

        // ------------------------------------------------------------------ blocks and corridors (once a building)

        static void PlanBlocks(Ctx c)
        {
            var ground = c.Blocks;
            ground.Floors = new BlockPlan[c.Floors];
            ground.Floors[0] = ground;
            PlanFloor(c, ground, -1, c.Rng(0xB10C));
            // the floors above: the same stair core, their own blocks and corridors (a building's partitions are not its
            // structure: the same plan on every floor read as one floor four times)
            var characters = new List<Character> { Character.Rows, Character.Clusters, Character.Mixed };
            var pick = c.Rng(0xB10E);
            for (int f = 1; f < c.Floors; f++)
            {
                var up = new BlockPlan { Station = ground.Station, Office = ground.Office, Scheme = ground.Scheme, Courts = ground.Courts, Hall = ground.Hall, Weights = ground.Weights, Module = ground.Module, Doubles = ground.Doubles };
                if (characters.Count == 0) characters.AddRange(new[] { Character.Rows, Character.Clusters, Character.Mixed });
                int k = pick.Next(characters.Count);
                up.Kind = characters[k];
                characters.RemoveAt(k);
                PlanFloor(c, up, f, c.Rng(0xB10C, f));
                ground.Floors[f] = up;
                ground.Unreached += up.Unreached;
            }
        }

        /// <summary>
        /// One floor's blocks and corridors (<paramref name="floor"/> −1: the ground floor, from the lobby; above it from the
        /// stair hall it arrives in, the halls of that floor kept whole).
        /// </summary>
        static void PlanFloor(Ctx c, BlockPlan bp, int floor, System.Random rng)
        {
            int W = bp.W = c.Plan.NX / M, D = bp.D = c.Plan.NZ / M;
            bp.In = new bool[W, D];
            bp.Block = new int[W, D];
            for (int x = 0; x < W; x++)
                for (int z = 0; z < D; z++)
                {
                    bp.Block[x, z] = -1;
                    bp.In[x, z] = c.Footprint[x * M, z * M] && c.Footprint[x * M + 1, z * M] && c.Footprint[x * M, z * M + 1] && c.Footprint[x * M + 1, z * M + 1];
                }
            // the lobby and the stair halls: blocks of their own
            void Reserve(RectInt cells)
            {
                int id = bp.Reserved.Count;
                bp.Reserved.Add(true);
                for (int x = cells.xMin / M; x < Mathf.CeilToInt(cells.xMax / (float)M); x++)
                    for (int z = cells.yMin / M; z < Mathf.CeilToInt(cells.yMax / (float)M); z++)
                        if (x >= 0 && z >= 0 && x < W && z < D && bp.In[x, z] && bp.Block[x, z] < 0) bp.Block[x, z] = id;
            }
            if (floor < 0)
            {
                Reserve(c.Lobby);
                bp.LobbyBlock = 0;
                foreach (var h in c.Halls) Reserve(h.hall);
            }
            else
            {
                // the halls of this floor: the one from below first (the corridors start there)
                foreach (var h in c.Halls) if (h.floor + 1 == floor) { bp.LobbyBlock = bp.Reserved.Count; Reserve(h.hall); }
                foreach (var h in c.Halls) if (h.floor == floor) { if (bp.LobbyBlock < 0) bp.LobbyBlock = bp.Reserved.Count; Reserve(h.hall); }
            }
            // the scheme's own parts first: a racetrack's core, the courts, the front of house, the halves of a mirrored plan
            int lo = 8, hi = bp.Station ? 16 : 15;
            if (bp.Scheme >= Scheme.Racetrack) Lay(c, bp, rng, ref lo, ref hi);
            // the rest: the biggest rectangles, each cut on its own (so the cut lines do not run through)
            CutZone(bp, null, rng, lo, hi);
            MergeSlivers(bp);
            var net = new Network(bp, rng);
            switch (bp.Scheme)
            {
                case Scheme.Hub:
                    // from the middle outwards, straight down every arm; one shortcut at most
                    net.Hub();
                    net.Turn = 3f;
                    net.Straight = 26;
                    net.Grow(c);
                    net.Loops(1);
                    break;
                case Scheme.Racetrack:
                case Scheme.Court:
                    // the ring (round the core, round each court), a way from the lobby to it, then whatever it leaves out
                    net.Ring(bp.Ring);
                    net.Join();
                    net.Grow(c);
                    net.Loops(2);
                    break;
                case Scheme.Grid:
                    // an aisle between every two halls
                    net.AllEdges(3);
                    net.BreakStraights(StraightRun);
                    net.Join();
                    net.Grow(c);
                    break;
                case Scheme.Sheds:
                    // the one corridor behind the front of house (the halls behind it open into each other)
                    foreach (var (a, b) in bp.FrontLines) net.Line(a, b, true);
                    net.Join();
                    net.Grow(c, bp.HasLine);
                    break;
                case Scheme.Axis:
                    // the gallery on the axis and the one across it behind the lobby; the rest keeps straight
                    foreach (var (a, b) in bp.FrontLines) net.Line(a, b, false);
                    foreach (var (a, b) in bp.AxisLines) net.Line(a, b, true);
                    net.Turn = 3f;
                    net.Straight = 24;
                    net.Join();
                    net.Grow(c);
                    net.Loops(3);
                    break;
                default:
                    net.Grow(c);
                    net.Loops(4);
                    break;
            }
            net.Carve(c);
        }

        /// <summary>A racetrack's rooms on the facade are this deep, metres: then the ring corridor, then the core.</summary>
        const int BandDepth = 6;

        /// <summary>Blocks cut from what is free (of <paramref name="zone"/>, if given): the biggest rectangle first, each cut on its own.</summary>
        static void CutZone(BlockPlan bp, bool[,] zone, System.Random rng, int lo, int hi)
        {
            while (true)
            {
                var q = BiggestFree(bp, zone);
                if (q.width == 0) break;
                Cut(bp, q, rng, lo, hi);
            }
        }

        /// <summary>
        /// The scheme's own parts of a floor, before the rest is cut into blocks: a racetrack's core, the courts, the front of
        /// house and the corridor behind the lobby, the two halves of a mirrored plan; and how big its blocks are.
        /// </summary>
        static void Lay(Ctx c, BlockPlan bp, System.Random rng, ref int lo, ref int hi)
        {
            int W = bp.W, D = bp.D;
            switch (bp.Scheme)
            {
                case Scheme.Racetrack:
                {
                    var core = CoreOf(bp, BandDepth);
                    CutZone(bp, core, rng, 8, 15);
                    bp.Ring = Round(bp, core);
                    break;
                }
                case Scheme.Court:
                {
                    // a court is a block nothing cuts and no corridor crosses, on every floor
                    var courts = new bool[W, D];
                    foreach (var q in bp.Courts)
                    {
                        bool free = true;
                        for (int x = q.xMin; x < q.xMax && free; x++)
                            for (int z = q.yMin; z < q.yMax && free; z++)
                                free = x >= 0 && z >= 0 && x < W && z < D && bp.In[x, z] && bp.Block[x, z] < 0;
                        if (!free) continue;
                        int id = bp.Reserved.Count;
                        bp.Reserved.Add(true);
                        for (int x = q.xMin; x < q.xMax; x++)
                            for (int z = q.yMin; z < q.yMax; z++) { bp.Block[x, z] = id; courts[x, z] = true; }
                    }
                    bp.Ring = Round(bp, courts);
                    lo = 9;
                    break;
                }
                case Scheme.Grid:
                    lo = 11;
                    bp.Front = NearReserved(bp, 8);
                    break;
                case Scheme.Sheds:
                    FrontLine(c, bp);
                    if (bp.Front != null) CutZone(bp, bp.Front, rng, 8, 14);
                    lo = 13;
                    hi = 19;
                    break;
                case Scheme.Axis:
                    FrontLine(c, bp);
                    bp.Front = null; // (its front is rooms like the rest of it)
                    if (bp.HasAxis) Mirror(bp, rng, lo, hi);
                    break;
            }
        }

        /// <summary>Metres from each metre cell to the outside (Chebyshev: a cell on the facade 1; outside the building int.MaxValue).</summary>
        static int[,] Distances(BlockPlan b)
        {
            int W = b.W, D = b.D;
            bool InCell(int x, int z) => x >= 0 && z >= 0 && x < W && z < D && b.In[x, z];
            var dist = new int[W, D];
            var q = new Queue<Vector2Int>();
            for (int x = 0; x < W; x++)
                for (int z = 0; z < D; z++)
                {
                    dist[x, z] = int.MaxValue;
                    if (!b.In[x, z]) continue;
                    bool edge = false;
                    for (int dx = -1; dx <= 1 && !edge; dx++)
                        for (int dz = -1; dz <= 1 && !edge; dz++) edge = !InCell(x + dx, z + dz);
                    if (edge) { dist[x, z] = 1; q.Enqueue(new Vector2Int(x, z)); }
                }
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int x = p.x + dx, z = p.y + dz;
                        if (!InCell(x, z) || dist[x, z] <= dist[p.x, p.y] + 1) continue;
                        dist[x, z] = dist[p.x, p.y] + 1;
                        q.Enqueue(new Vector2Int(x, z));
                    }
            }
            return dist;
        }

        /// <summary>
        /// A racetrack's core: what lies <paramref name="band"/> + 3 m and more from the facade (the rooms, the corridor) and
        /// 3 m from the lobby and the stair halls (the corridor passes between), nowhere thinner than 4 m, no piece under 48 m².
        /// Wings too narrow for it have none: the corridors grow down them as they come.
        /// </summary>
        static bool[,] CoreOf(BlockPlan bp, int band)
        {
            int W = bp.W, D = bp.D;
            var dist = Distances(bp);
            var raw = new bool[W, D];
            for (int x = 0; x < W; x++)
                for (int z = 0; z < D; z++)
                {
                    // (the rooms on the facade 2 m deeper in every other square of StraightRun m: the ring steps in and out, no
                    // side of it one long straight)
                    int deep = band + 3 + ((x / StraightRun + z / StraightRun) % 2 == 0 ? 0 : 2);
                    if (!bp.In[x, z] || bp.Block[x, z] >= 0 || dist[x, z] < deep) continue;
                    bool near = false;
                    for (int dx = -2; dx <= 2 && !near; dx++)
                        for (int dz = -2; dz <= 2 && !near; dz++)
                        {
                            int px = x + dx, pz = z + dz;
                            near = px >= 0 && pz >= 0 && px < W && pz < D && bp.Block[px, pz] >= 0;
                        }
                    raw[x, z] = !near;
                }
            var core = new bool[W, D];
            for (int x = 0; x + 4 <= W; x++)
                for (int z = 0; z + 4 <= D; z++)
                {
                    bool all = true;
                    for (int i = 0; i < 4 && all; i++)
                        for (int j = 0; j < 4 && all; j++) all = raw[x + i, z + j];
                    if (!all) continue;
                    for (int i = 0; i < 4; i++)
                        for (int j = 0; j < 4; j++) core[x + i, z + j] = true;
                }
            var seen = new bool[W, D];
            for (int x = 0; x < W; x++)
                for (int z = 0; z < D; z++)
                {
                    if (!core[x, z] || seen[x, z]) continue;
                    var piece = new List<Vector2Int> { new(x, z) };
                    seen[x, z] = true;
                    for (int i = 0; i < piece.Count; i++)
                        foreach (var d in Dirs4)
                        {
                            var n = piece[i] + d;
                            if (n.x < 0 || n.y < 0 || n.x >= W || n.y >= D || !core[n.x, n.y] || seen[n.x, n.y]) continue;
                            seen[n.x, n.y] = true;
                            piece.Add(n);
                        }
                    if (piece.Count < 48) foreach (var q in piece) core[q.x, q.y] = false;
                }
            return core;
        }

        /// <summary>The cells and a metre round them (not into the lobby or a stair hall): the ring corridor follows this outline.</summary>
        static bool[,] Round(BlockPlan bp, bool[,] mask)
        {
            int W = bp.W, D = bp.D;
            var k = new bool[W, D];
            for (int x = 0; x < W; x++)
                for (int z = 0; z < D; z++)
                {
                    if (!bp.In[x, z]) continue;
                    if (mask[x, z]) { k[x, z] = true; continue; }
                    if (bp.Block[x, z] >= 0 && bp.Reserved[bp.Block[x, z]]) continue;
                    for (int dx = -1; dx <= 1 && !k[x, z]; dx++)
                        for (int dz = -1; dz <= 1 && !k[x, z]; dz++)
                        {
                            int px = x + dx, pz = z + dz;
                            k[x, z] = px >= 0 && pz >= 0 && px < W && pz < D && mask[px, pz];
                        }
                }
            return k;
        }

        /// <summary>The free cells within <paramref name="reach"/> m of the lobby or a stair hall (called before any block is cut).</summary>
        static bool[,] NearReserved(BlockPlan bp, int reach)
        {
            int W = bp.W, D = bp.D;
            var near = new bool[W, D];
            for (int x = 0; x < W; x++)
                for (int z = 0; z < D; z++)
                {
                    if (bp.Block[x, z] < 0) continue;
                    for (int dx = -reach; dx <= reach; dx++)
                        for (int dz = -reach; dz <= reach; dz++)
                        {
                            int px = x + dx, pz = z + dz;
                            if (px >= 0 && pz >= 0 && px < W && pz < D && bp.In[px, pz] && bp.Block[px, pz] < 0) near[px, pz] = true;
                        }
                }
            return near;
        }

        /// <summary>No corridor laid as a line runs straight for longer than this, metres: past it, it steps aside.</summary>
        const int StraightRun = 28;

        /// <summary>How far such a corridor steps aside, metres (its own width: one does not see down both stretches).</summary>
        const int Jog = 3;

        /// <summary>
        /// The corridor behind the lobby: a line a metre in from the lobby's inner side, as far both ways as the building
        /// goes (behind the facade side where it runs the longest); what lies between it and that facade is the front of
        /// house. And the axis: straight on from the lobby's middle, away from the facade. Neither runs straight for more
        /// than <see cref="StraightRun"/>: every other stretch steps <see cref="Jog"/> m aside (the lobby's own stays).
        /// Called before any block is cut.
        /// </summary>
        static void FrontLine(Ctx c, BlockPlan bp)
        {
            int W = bp.W, D = bp.D;
            int x0 = c.Lobby.xMin / M, z0 = c.Lobby.yMin / M, x1 = Mathf.CeilToInt(c.Lobby.xMax / (float)M), z1 = Mathf.CeilToInt(c.Lobby.yMax / (float)M);
            bool Free(int x, int z) => x >= 0 && z >= 0 && x < W && z < D && bp.In[x, z] && bp.Block[x, z] < 0;
            int best = 0;
            for (int side = 0; side < 4; side++)
            {
                int dx = side == 2 ? -1 : side == 3 ? 1 : 0, dz = side == 0 ? -1 : side == 1 ? 1 : 0; // towards the facade
                if (!SideOutside(c, c.Lobby, dx, dz)) continue;
                bool alongX = side < 2, low = side is 0 or 2;
                int at = side switch { 0 => z1 + 1, 1 => z0 - 1, 2 => x1 + 1, _ => x0 - 1 }, inward = low ? 1 : -1;
                int mid = alongX ? (x0 + x1) / 2 : (z0 + z1) / 2;
                // a step along a line (row) from point u to u + 1 has the cells u on both sides of it
                bool Open(int u, int row) => alongX ? Free(u, row - 1) && Free(u, row) : Free(row - 1, u) && Free(row, u);
                Vector2Int P(int u, int row) => alongX ? new Vector2Int(u, row) : new Vector2Int(row, u);
                if (!Open(mid, at) || !Open(mid - 1, at)) continue;
                int a = mid, b = mid;
                while (Open(a - 1, at)) a--;
                while (Open(b, at)) b++;
                if (b - a <= best) continue;
                best = b - a;
                bp.HasLine = true;
                bp.MirrorInX = alongX;
                bp.FrontLines.Clear();
                bp.AxisLines.Clear();
                bp.Front = new bool[W, D];
                // the stretches: the lobby's own in the middle of its run, then one every StraightRun both ways
                var cuts = new List<int>();
                if (b - a > StraightRun + 8)
                {
                    for (int u = Mathf.Max(a, mid - StraightRun / 2); u - a >= 10; u -= StraightRun) cuts.Add(u);
                    cuts.Reverse();
                    for (int u = Mathf.Min(b, mid + StraightRun / 2); b - u >= 10; u += StraightRun) cuts.Add(u);
                }
                cuts.Insert(0, a);
                cuts.Add(b);
                int home = 0;
                for (int i = 0; i + 1 < cuts.Count; i++) if (mid >= cuts[i] && mid < cuts[i + 1]) home = i;
                int last = at;
                for (int i = 0; i + 1 < cuts.Count; i++)
                {
                    int u0 = cuts[i], u1 = cuts[i + 1], row = at + ((i - home) % 2 != 0 ? Jog * inward : 0);
                    for (int u = u0; u < u1 && row != at; u++) if (!Open(u, row)) row = at; // (no room for the step: straight on)
                    bp.FrontLines.Add((P(u0, row), P(u1, row)));
                    if (i > 0 && row != last) bp.FrontLines.Add((P(u0, last), P(u0, row)));
                    last = row;
                    // the front of house: from the corridor to the facade
                    for (int u = u0; u < u1; u++)
                        for (int v = low ? row - 2 : row + 1; ; v -= inward)
                        {
                            int x = alongX ? u : v, z = alongX ? v : u;
                            if (!Free(x, z)) break;
                            bp.Front[x, z] = true;
                        }
                }
                // the axis: a step along it from row v to v + inward has the cells of the row it enters on both sides
                bool Ahead(int col, int v)
                {
                    int r = inward > 0 ? v : v - 1;
                    return alongX ? Free(col - 1, r) && Free(col, r) : Free(r, col - 1) && Free(r, col);
                }
                Vector2Int Q(int col, int v) => alongX ? new Vector2Int(col, v) : new Vector2Int(v, col);
                int e = at;
                while (Ahead(mid, e)) e += inward;
                int len = Mathf.Abs(e - at);
                bp.HasAxis = len >= 8;
                bp.AxisMid = mid;
                if (!bp.HasAxis) continue;
                int n = len > StraightRun + 8 ? Mathf.CeilToInt(len / (float)StraightRun) : 1, lastCol = mid;
                for (int i = 0; i < n; i++)
                {
                    int v0 = at + inward * (len * i / n), v1 = at + inward * (len * (i + 1) / n);
                    int col = mid + (i % 2 == 1 ? (i % 4 == 1 ? Jog : -Jog) : 0);
                    for (int v = v0; v != v1 && col != mid; v += inward) if (!Ahead(col, v)) col = mid;
                    bp.AxisLines.Add((Q(col, v0), Q(col, v1)));
                    if (i > 0 && col != lastCol) bp.AxisLines.Add((Q(lastCol, v0), Q(col, v0)));
                    lastCol = col;
                }
            }
        }

        /// <summary>A mirrored plan: the half on the low side of the axis is cut into blocks, the other half gets the same blocks mirrored.</summary>
        static void Mirror(BlockPlan bp, System.Random rng, int lo, int hi)
        {
            int W = bp.W, D = bp.D, mid = bp.AxisMid;
            bool inX = bp.MirrorInX;
            var half = new bool[W, D];
            for (int x = 0; x < W; x++)
                for (int z = 0; z < D; z++) half[x, z] = (inX ? x : z) < mid;
            int first = bp.Reserved.Count;
            CutZone(bp, half, rng, lo, hi);
            int n = bp.Reserved.Count;
            var twin = new int[n];
            for (int i = 0; i < n; i++) twin[i] = -1;
            for (int x = 0; x < W; x++)
                for (int z = 0; z < D; z++)
                {
                    int b = bp.Block[x, z];
                    if (b < first || b >= n || !half[x, z]) continue;
                    int mx = inX ? 2 * mid - 1 - x : x, mz = inX ? z : 2 * mid - 1 - z;
                    if (mx < 0 || mz < 0 || mx >= W || mz >= D || !bp.In[mx, mz] || bp.Block[mx, mz] >= 0) continue;
                    if (twin[b] < 0) { twin[b] = bp.Reserved.Count; bp.Reserved.Add(false); }
                    bp.Block[mx, mz] = twin[b];
                }
        }

        static RectInt BiggestFree(BlockPlan bp, bool[,] zone = null)
        {
            int W = bp.W, D = bp.D;
            var h = new int[D + 1];
            RectInt best = default;
            int bestArea = 0;
            for (int x = 0; x < W; x++)
            {
                for (int z = 0; z < D; z++) h[z] = bp.In[x, z] && bp.Block[x, z] < 0 && (zone == null || zone[x, z]) ? h[z] + 1 : 0;
                h[D] = 0;
                var st = new Stack<(int start, int height)>();
                for (int z = 0; z <= D; z++)
                {
                    int start = z;
                    while (st.Count > 0 && st.Peek().height >= h[z])
                    {
                        var (j, hh) = st.Pop();
                        int area = hh * (z - j);
                        float score = area * (Mathf.Min(hh, z - j) >= 4 ? 1f : 0.3f);
                        if (score > bestArea) { bestArea = Mathf.RoundToInt(score); best = new RectInt(x - hh + 1, j, hh, z - j); }
                        start = j;
                    }
                    st.Push((start, h[z]));
                }
            }
            return best;
        }

        static void Cut(BlockPlan bp, RectInt q, System.Random r, int lo, int hi)
        {
            int lim = r.Next(lo, hi + 1);
            int w = q.width, d = q.height;
            bool done = Mathf.Max(w, d) <= Mathf.Max(lim, 10) || (Mathf.Max(w, d) <= hi && r.NextDouble() < 0.45);
            bool along = r.NextDouble() < 0.85 || Mathf.Max(w, d) > 1.7f * Mathf.Min(w, d) ? w >= d : w < d;
            int L = along ? w : d;
            if (!done && L >= 14)
            {
                int at = r.Next(Mathf.Max(7, Mathf.RoundToInt(L * 0.3f)), Mathf.Min(L - 7, Mathf.RoundToInt(L * 0.7f)) + 1);
                if (along) { Cut(bp, new RectInt(q.x, q.y, at, d), r, lo, hi); Cut(bp, new RectInt(q.x + at, q.y, w - at, d), r, lo, hi); }
                else { Cut(bp, new RectInt(q.x, q.y, w, at), r, lo, hi); Cut(bp, new RectInt(q.x, q.y + at, w, d - at), r, lo, hi); }
                return;
            }
            int id = bp.Reserved.Count;
            bp.Reserved.Add(false);
            for (int x = q.xMin; x < q.xMax; x++)
                for (int z = q.yMin; z < q.yMax; z++) bp.Block[x, z] = id;
        }

        /// <summary>A block thinner than 5 m or smaller than 45 m² joins the neighbour it shares the most with.</summary>
        static void MergeSlivers(BlockPlan bp)
        {
            int n = bp.Reserved.Count;
            var cells = new List<Vector2Int>[n];
            for (int i = 0; i < n; i++) cells[i] = new List<Vector2Int>();
            for (int x = 0; x < bp.W; x++)
                for (int z = 0; z < bp.D; z++) if (bp.Block[x, z] >= 0) cells[bp.Block[x, z]].Add(new Vector2Int(x, z));
            for (int i = 0; i < n; i++)
            {
                if (bp.Reserved[i] || cells[i].Count == 0) continue;
                var b = MetreBounds(cells[i]);
                if (Mathf.Min(b.width, b.height) >= 5 && cells[i].Count >= 45) continue;
                var shared = new Dictionary<int, int>();
                foreach (var p in cells[i])
                    foreach (var dd in Dirs4)
                    {
                        var q = p + dd;
                        if (q.x < 0 || q.y < 0 || q.x >= bp.W || q.y >= bp.D) continue;
                        int j = bp.Block[q.x, q.y];
                        if (j >= 0 && j != i && !bp.Reserved[j]) shared[j] = shared.TryGetValue(j, out var k) ? k + 1 : 1;
                    }
                int best = -1, most = 0;
                foreach (var kv in shared) if (kv.Value > most || kv.Value == most && kv.Key < best) { most = kv.Value; best = kv.Key; }
                if (best < 0) continue;
                foreach (var p in cells[i]) bp.Block[p.x, p.y] = best;
                cells[best].AddRange(cells[i]);
                cells[i].Clear();
            }
        }

        static RectInt MetreBounds(List<Vector2Int> cells)
        {
            int x0 = int.MaxValue, z0 = int.MaxValue, x1 = int.MinValue, z1 = int.MinValue;
            foreach (var p in cells) { x0 = Mathf.Min(x0, p.x); z0 = Mathf.Min(z0, p.y); x1 = Mathf.Max(x1, p.x); z1 = Mathf.Max(z1, p.y); }
            return new RectInt(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
        }

        /// <summary>
        /// The corridors as a network on the grid points between metre cells: grown from the lobby to every block along
        /// their edges, then a few loops; carved into plan cells at the end.
        /// </summary>
        sealed class Network
        {
            readonly BlockPlan m_B;
            readonly System.Random m_Rng;
            readonly int m_W, m_D;
            readonly int[,] m_Depth;                 // [x, z] grid point: metres to the facade, up to 5 (−1: not a point)
            readonly HashSet<Vector2Int> m_Net = new();
            readonly HashSet<(Vector2Int, Vector2Int)> m_Edges = new();
            readonly int[] m_Size, m_Touch;
            readonly HashSet<(Vector2Int, Vector2Int)> m_MainEdges = new(); // steps that are the main way whatever the rest (a gallery)
            readonly bool[,] m_At;      // [x, z] grid point is on the network (m_Net, for the search)
            readonly bool[,,] m_Has;    // [x, z, 0 / 1] the step from the point towards +x / +z is a corridor (m_Edges, for the search)
            Vector2Int m_Start;
            bool m_Hub;
            /// <summary>What a turn costs and how long a straight may run before every step of it costs more (metres).</summary>
            public float Turn = 1.6f;
            public int Straight = 15;
            static readonly Vector2Int[] Steps = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

            public Network(BlockPlan b, System.Random rng)
            {
                m_B = b;
                m_Rng = rng;
                m_W = b.W;
                m_D = b.D;
                int n = b.Reserved.Count;
                m_Size = new int[n];
                m_Touch = new int[n];
                m_At = new bool[m_W + 2, m_D + 2];
                m_Has = new bool[m_W + 2, m_D + 2, 2];
                for (int x = 0; x < m_W; x++)
                    for (int z = 0; z < m_D; z++) if (b.Block[x, z] >= 0) m_Size[b.Block[x, z]]++;
                // metres from each cell to the outside (Chebyshev), then each grid point's least of its four cells
                var dist = Distances(b);
                m_Depth = new int[m_W + 1, m_D + 1];
                for (int x = 0; x <= m_W; x++)
                    for (int z = 0; z <= m_D; z++)
                    {
                        m_Depth[x, z] = -1;
                        if (!InCell(x - 1, z - 1) || !InCell(x, z - 1) || !InCell(x - 1, z) || !InCell(x, z)) continue;
                        m_Depth[x, z] = Mathf.Min(5, Mathf.Min(Mathf.Min(dist[x - 1, z - 1], dist[x, z - 1]), Mathf.Min(dist[x - 1, z], dist[x, z])));
                    }
                // the start: the lobby's edge point deepest in the building
                int bestDepth = -1;
                for (int x = 0; x <= m_W; x++)
                    for (int z = 0; z <= m_D; z++)
                    {
                        var p = new Vector2Int(x, z);
                        if (!Point(p) || m_Depth[x, z] <= bestDepth) continue;
                        bool byLobby = false;
                        foreach (var s in Steps)
                        {
                            var q2 = p + s;
                            if (!Point(q2)) continue;
                            var (i, j) = Sides(p, q2);
                            if (i != j && (i == m_B.LobbyBlock || j == m_B.LobbyBlock)) byLobby = true;
                        }
                        if (!byLobby) continue;
                        bestDepth = m_Depth[x, z];
                        m_Start = p;
                    }
            }

            /// <summary>
            /// Hub and spokes: the corridors start in the middle of the building — the point deep inside it nearest its
            /// centre, no lobby or stair hall beside it — where a hall is carved.
            /// </summary>
            public void Hub()
            {
                var mean = Vector2.zero;
                int n = 0;
                for (int x = 0; x < m_W; x++)
                    for (int z = 0; z < m_D; z++) if (m_B.In[x, z]) { mean += new Vector2(x + 0.5f, z + 0.5f); n++; }
                mean /= Mathf.Max(1, n);
                float best = float.MaxValue;
                for (int x = 1; x < m_W; x++)
                    for (int z = 1; z < m_D; z++)
                    {
                        var p = new Vector2Int(x, z);
                        if (!Point(p)) continue;
                        bool reserved = false;
                        for (int dx = -1; dx <= 0; dx++)
                            for (int dz = -1; dz <= 0; dz++) { int b = m_B.Block[x + dx, z + dz]; reserved |= b >= 0 && m_B.Reserved[b]; }
                        if (reserved) continue;
                        float score = (new Vector2(x, z) - mean).magnitude - 4f * m_Depth[x, z];
                        if (score < best) { best = score; m_Start = p; m_Hub = true; }
                    }
            }

            /// <summary>A ring: every step along the outline of these cells (between a cell of them and a cell that is not).</summary>
            public void Ring(bool[,] k)
            {
                if (k == null) return;
                for (int x = 0; x <= m_W; x++)
                    for (int z = 0; z <= m_D; z++)
                        for (int e = 0; e < 2; e++)
                        {
                            var a = new Vector2Int(x, z);
                            var b = a + Steps[e];
                            if (!Point(a) || !Point(b)) continue;
                            bool one = e == 0 ? k[x, z - 1] : k[x - 1, z], other = k[x, z];
                            if (one == other || Cost(a, b) < 0f) continue;
                            Add(new List<Vector2Int> { a, b });
                        }
            }

            /// <summary>A straight corridor from grid point to grid point, wherever a step of it may run (the main way, if so).</summary>
            public void Line(Vector2Int a, Vector2Int b, bool main)
            {
                var d = new Vector2Int(System.Math.Sign(b.x - a.x), System.Math.Sign(b.y - a.y));
                if (d == Vector2Int.zero) return;
                for (var p = a; p != b; p += d)
                {
                    var q = p + d;
                    if (!Point(p) || !Point(q) || Cost(p, q) < 0f) continue;
                    Add(new List<Vector2Int> { p, q });
                    if (main) m_MainEdges.Add(Key(p, q));
                }
            }

            /// <summary>An aisle between every two blocks (not the lobby's and the stair halls' sides), <paramref name="depth"/> m and more from the facade.</summary>
            public void AllEdges(int depth)
            {
                for (int x = 0; x <= m_W; x++)
                    for (int z = 0; z <= m_D; z++)
                        for (int e = 0; e < 2; e++)
                        {
                            var a = new Vector2Int(x, z);
                            var b = a + Steps[e];
                            if (!Point(a) || !Point(b) || m_Depth[a.x, a.y] < depth || m_Depth[b.x, b.y] < depth) continue;
                            var (i, j) = Sides(a, b);
                            if (i < 0 || j < 0 || i == j || m_B.Reserved[i] || m_B.Reserved[j]) continue;
                            Add(new List<Vector2Int> { a, b });
                        }
            }

            /// <summary>One network: every part of it not joined to the start gets the cheapest way to what is.</summary>
            public void Join()
            {
                m_Net.Add(m_Start);
                m_At[m_Start.x, m_Start.y] = true;
                for (int guard = 0; guard < 80; guard++)
                {
                    var seen = Along(m_Start);
                    if (seen.Count >= m_Net.Count) return;
                    var (cost, from) = Paths(seen.Keys, float.MaxValue, false);
                    int pick = -1;
                    float best = float.MaxValue;
                    for (int k = 0; k < cost.Length; k++)
                    {
                        if (cost[k] >= best || k % 5 == 4 || from[k] < 0) continue;
                        int cell = k / 5;
                        var a = new Vector2Int(cell % (m_W + 1), cell / (m_W + 1));
                        if (!At(a) || seen.ContainsKey(a)) continue;
                        best = cost[k];
                        pick = k;
                    }
                    if (pick < 0) return;
                    Add(Back(from, pick));
                }
            }

            bool InCell(int x, int z) => x >= 0 && z >= 0 && x < m_W && z < m_D && m_B.In[x, z];
            bool Point(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x <= m_W && p.y <= m_D && m_Depth[p.x, p.y] >= 0;

            /// <summary>The two blocks either side of the step from <paramref name="a"/> (−1: none, not a step).</summary>
            (int, int) Sides(Vector2Int a, Vector2Int b)
            {
                if (a.y == b.y) { int x = Mathf.Min(a.x, b.x); return (m_B.Block[x, a.y - 1], m_B.Block[x, a.y]); }
                int z = Mathf.Min(a.y, b.y);
                return (m_B.Block[a.x - 1, z], m_B.Block[a.x, z]);
            }

            static (Vector2Int, Vector2Int) Key(Vector2Int a, Vector2Int b) => a.x < b.x || a.x == b.x && a.y < b.y ? (a, b) : (b, a);

            bool At(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x <= m_W && p.y <= m_D && m_At[p.x, p.y];

            /// <summary>The step between two neighbouring grid points is a corridor.</summary>
            bool Edge(Vector2Int a, Vector2Int b) => a.y == b.y ? m_Has[Mathf.Min(a.x, b.x), a.y, 0] : m_Has[a.x, Mathf.Min(a.y, b.y), 1];

            void Mark(Vector2Int a, Vector2Int b, bool on)
            {
                if (a.y == b.y) m_Has[Mathf.Min(a.x, b.x), a.y, 0] = on; else m_Has[a.x, Mathf.Min(a.y, b.y), 1] = on;
            }

            /// <summary>A step of corridor taken out again (a point no step is left at leaves the network).</summary>
            void Remove(Vector2Int a, Vector2Int b)
            {
                if (!m_Edges.Remove(Key(a, b))) return;
                Mark(a, b, false);
                m_MainEdges.Remove(Key(a, b));
                var (i, j) = Sides(a, b);
                if (i >= 0) m_Touch[i]--;
                if (j >= 0 && j != i) m_Touch[j]--;
                foreach (var p in new[] { a, b })
                {
                    bool any = false;
                    foreach (var s in Steps) { var q = p + s; any |= q.x >= 0 && q.y >= 0 && q.x <= m_W && q.y <= m_D && Edge(p, q); }
                    if (!any && p != m_Start) { m_Net.Remove(p); m_At[p.x, p.y] = false; }
                }
            }

            /// <summary>
            /// No aisle straight for more than <paramref name="run"/> m: of a longer one the stretch between two crossings
            /// nearest its middle is taken out (the halls either side of it stand wall to wall there).
            /// </summary>
            public void BreakStraights(int run)
            {
                for (int pass = 0; pass < 60; pass++)
                {
                    bool cut = false;
                    for (int dir = 0; dir < 2 && !cut; dir++) // along x, along z
                        for (int line = 0; line <= (dir == 0 ? m_D : m_W) && !cut; line++)
                        {
                            int start = -1, n = dir == 0 ? m_W : m_D;
                            Vector2Int P(int u) => dir == 0 ? new Vector2Int(u, line) : new Vector2Int(line, u);
                            for (int u = 0; u <= n && !cut; u++)
                            {
                                if (u < n && Edge(P(u), P(u + 1))) { if (start < 0) start = u; continue; }
                                if (start >= 0 && u - start > run)
                                {
                                    // crossings along it: the points an aisle leaves it sideways
                                    var side = Steps[1 - dir];
                                    bool Crossing(int k) => At(P(k) + side) && Edge(P(k), P(k) + side) || At(P(k) - side) && Edge(P(k) - side, P(k));
                                    int mid = (start + u) / 2, a = mid, b = mid + 1;
                                    while (a > start && !Crossing(a)) a--;
                                    while (b < u && !Crossing(b)) b++;
                                    if ((a > start || b < u) && b - a <= 20)
                                    {
                                        for (int k = a; k < b; k++) Remove(P(k), P(k + 1));
                                        cut = true;
                                    }
                                }
                                start = -1;
                            }
                        }
                    if (!cut) return;
                }
            }

            float Cost(Vector2Int a, Vector2Int b)
            {
                var (i, j) = Sides(a, b);
                if (i < 0 || j < 0) return -1f;
                if (i == j && m_B.Reserved[i]) return -1f; // never through the lobby or a stair hall
                float k = i != j ? 1f : 3.5f;
                int m = Mathf.Min(m_Depth[a.x, a.y], m_Depth[b.x, b.y]);
                if (m < 4) k += (4 - m) * 1.1f;
                return k;
            }

            int Index(Vector2Int p, int dir) => (p.y * (m_W + 1) + p.x) * 5 + dir;

            /// <summary>
            /// Cheapest ways from <paramref name="src"/> (Dijkstra over point and heading): along the network almost free,
            /// along a block edge 1, through a block more, near the facade much more, a turn <see cref="Turn"/>, a straight
            /// past <see cref="Straight"/> m 2.5 a step more.
            /// </summary>
            (float[] cost, int[] from) Paths(IEnumerable<Vector2Int> src, float limit, bool fresh, ICollection<Vector2Int> avoid = null)
            {
                int n = (m_W + 1) * (m_D + 1) * 5;
                var cost = new float[n];
                var from = new int[n];
                var run = new int[n];
                for (int i = 0; i < n; i++) { cost[i] = float.MaxValue; from[i] = -1; }
                var heap = new MinHeap();
                var start = new HashSet<Vector2Int>(src);
                foreach (var p in start) { int k = Index(p, 4); cost[k] = 0f; heap.Push(0f, k); }
                var done = new bool[n];
                while (heap.Count > 0)
                {
                    var (c, k) = heap.Pop();
                    if (done[k]) continue;
                    done[k] = true;
                    if (c > limit) break;
                    int cell = k / 5, dir = k % 5;
                    var a = new Vector2Int(cell % (m_W + 1), cell / (m_W + 1));
                    if (avoid != null && avoid.Contains(a) && !start.Contains(a)) continue;
                    for (int e = 0; e < 4; e++)
                    {
                        if (dir < 4 && e == (dir + 2) % 4) continue;
                        var b = a + Steps[e];
                        if (!Point(b)) continue;
                        bool inNet = Edge(a, b);
                        if (fresh && inNet) continue;
                        float step = inNet ? 0.3f : Cost(a, b);
                        if (step < 0f) continue;
                        if (!inNet && Beside(a, b)) step += 3f;
                        int nr = dir == e ? run[k] + 1 : 1;
                        float nc = c + step + (dir < 4 && dir != e ? Turn : 0f) + (nr > Straight ? 2.5f : 0f);
                        int nk = Index(b, e);
                        if (nc >= cost[nk]) continue;
                        cost[nk] = nc;
                        from[nk] = k;
                        run[nk] = nr;
                        heap.Push(nc, nk);
                    }
                }
                return (cost, from);
            }

            /// <summary>A new step running alongside the network 1–2 m off it (a corridor beside a corridor: one wide blob).</summary>
            bool Beside(Vector2Int a, Vector2Int b)
            {
                var d = b - a;
                var side = new Vector2Int(d.y, d.x);
                for (int k = 1; k <= 2; k++)
                    if (At(b + side * k) && At(a + side * k) || At(b - side * k) && At(a - side * k)) return true;
                return false;
            }

            List<Vector2Int> Back(int[] from, int k)
            {
                var p = new List<Vector2Int>();
                for (; k >= 0; k = from[k]) { int cell = k / 5; p.Add(new Vector2Int(cell % (m_W + 1), cell / (m_W + 1))); }
                p.Reverse();
                return p;
            }

            void Add(List<Vector2Int> path)
            {
                for (int i = 0; i + 1 < path.Count; i++)
                {
                    var key = Key(path[i], path[i + 1]);
                    m_Net.Add(path[i]);
                    m_Net.Add(path[i + 1]);
                    m_At[path[i].x, path[i].y] = m_At[path[i + 1].x, path[i + 1].y] = true;
                    if (!m_Edges.Add(key)) continue;
                    Mark(path[i], path[i + 1], true);
                    var (a, b) = Sides(path[i], path[i + 1]);
                    m_Touch[a]++;
                    if (b != a) m_Touch[b]++;
                }
            }

            /// <summary>From the start (the lobby's inner side), the cheapest way to the next block not met yet, until every block is met along 3 m.</summary>
            public void Grow(Ctx c, bool reservedOnly = false)
            {
                m_Net.Add(m_Start);
                m_At[m_Start.x, m_Start.y] = true;
                for (int guard = 0; guard < 400; guard++)
                {
                    // (the lobby and the stair halls only: the halls of a shed open into each other)
                    bool Wanted(int i) => m_Touch[i] < 3 && (m_Size[i] >= 30 && !reservedOnly || m_B.Reserved[i] && m_Size[i] > 0);
                    bool any = false;
                    for (int i = 0; i < m_Size.Length && !any; i++) any = Wanted(i);
                    if (!any) break;
                    var (cost, from) = Paths(m_Net, float.MaxValue, false);
                    int pick = -1;
                    float best = float.MaxValue;
                    for (int k = 0; k < cost.Length; k++)
                    {
                        if (cost[k] == float.MaxValue || k % 5 == 4 || from[k] < 0) continue;
                        int cell = k / 5;
                        var a = new Vector2Int(cell % (m_W + 1), cell / (m_W + 1));
                        var prev = a - Steps[k % 5];
                        if (Edge(prev, a)) continue;
                        var (i, j) = Sides(prev, a);
                        float gain = (Wanted(i) ? m_Size[i] + (m_B.Reserved[i] ? 400 : 0) : 0) + (j != i && Wanted(j) ? m_Size[j] + (m_B.Reserved[j] ? 400 : 0) : 0);
                        if (gain <= 0f || m_Depth[a.x, a.y] < 3 && !(m_B.Reserved[i] || m_B.Reserved[j])) continue;
                        float score = (cost[k] + 3f) / Mathf.Sqrt(gain) * Range(m_Rng, 0.8f, 1.25f);
                        if (score < best) { best = score; pick = k; }
                    }
                    if (pick < 0) { m_B.Unreached++; break; }
                    Add(Back(from, pick));
                    // on a little: the block is met along at least 3 m
                    int pc = pick / 5, dir = pick % 5;
                    var at = new Vector2Int(pc % (m_W + 1), pc / (m_W + 1));
                    for (int s = 0; s < 2; s++)
                    {
                        var next = at + Steps[dir];
                        if (!Point(next) || m_Depth[next.x, next.y] < 4 || Cost(at, next) < 0f) break;
                        Add(new List<Vector2Int> { at, next });
                        at = next;
                    }
                }
            }

            Dictionary<Vector2Int, int> Along(Vector2Int a)
            {
                var links = new Dictionary<Vector2Int, List<Vector2Int>>();
                foreach (var (p, q) in m_Edges)
                {
                    if (!links.TryGetValue(p, out var lp)) links[p] = lp = new List<Vector2Int>();
                    if (!links.TryGetValue(q, out var lq)) links[q] = lq = new List<Vector2Int>();
                    lp.Add(q);
                    lq.Add(p);
                }
                var d = new Dictionary<Vector2Int, int> { [a] = 0 };
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(a);
                while (queue.Count > 0)
                {
                    var p = queue.Dequeue();
                    if (!links.TryGetValue(p, out var next)) continue;
                    foreach (var q in next) if (!d.ContainsKey(q)) { d[q] = d[p] + 1; queue.Enqueue(q); }
                }
                return d;
            }

            /// <summary>Shortcuts between points far apart along the network and close across it: loops (dead ends first).</summary>
            public void Loops(int count)
            {
                for (int n = 0; n < count; n++)
                {
                    var degree = new Dictionary<Vector2Int, int>();
                    foreach (var (p, q) in m_Edges)
                    {
                        degree[p] = degree.TryGetValue(p, out var dp) ? dp + 1 : 1;
                        degree[q] = degree.TryGetValue(q, out var dq) ? dq + 1 : 1;
                    }
                    var ends = new List<Vector2Int>();
                    var rest = new List<Vector2Int>();
                    foreach (var p in m_Net) (degree.TryGetValue(p, out var d) && d == 1 ? ends : rest).Add(p);
                    ends.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
                    rest.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
                    Shuffle(ends);
                    Shuffle(rest);
                    ends.AddRange(rest);
                    float bestScore = 9f;
                    List<Vector2Int> best = null;
                    for (int t = 0; t < Mathf.Min(9, ends.Count); t++)
                    {
                        var a = ends[t];
                        var along = Along(a);
                        var (cost, from) = Paths(new[] { a }, 62f, true, m_Net);
                        for (int k = 0; k < cost.Length; k++)
                        {
                            if (cost[k] == float.MaxValue || k % 5 == 4) continue;
                            int cell = k / 5;
                            var y = new Vector2Int(cell % (m_W + 1), cell / (m_W + 1));
                            if (!m_Net.Contains(y) || !along.TryGetValue(y, out int far) || far < 9) continue;
                            var path = Back(from, k);
                            float score = far - 1.1f * path.Count - 0.25f * cost[k];
                            if (score <= bestScore || !Apart(path)) continue;
                            bestScore = score;
                            best = path;
                        }
                    }
                    if (best == null) break;
                    Add(best);
                    m_B.Loops++;
                }
            }

            /// <summary>A shortcut's middle keeps 3 m from the network it does not join (no corridor right beside a corridor).</summary>
            bool Apart(List<Vector2Int> path)
            {
                for (int i = 3; i < path.Count - 3; i++)
                    for (int dx = -2; dx <= 2; dx++)
                        for (int dz = -2; dz <= 2; dz++)
                            if (m_Net.Contains(path[i] + new Vector2Int(dx, dz)) && Mathf.Abs(dx) + Mathf.Abs(dz) <= 2) return false;
                return true;
            }

            void Shuffle(List<Vector2Int> l)
            {
                for (int i = l.Count - 1; i > 0; i--) { int j = m_Rng.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); }
            }

            /// <summary>The network into plan cells: 2.5 m corridors, 3 m along the main way, a hall at some crossings.</summary>
            public void Carve(Ctx c)
            {
                int nx = c.Plan.NX, nz = c.Plan.NZ;
                m_B.Corridor = new bool[nx, nz];
                m_B.Main = new bool[nx, nz];
                var along = Along(m_Start);
                var far = m_Start;
                foreach (var kv in along) if (kv.Value > along[far]) far = kv.Key;
                // the main way: back from the farthest point along the network
                var main = new HashSet<Vector2Int> { far };
                var links = new Dictionary<Vector2Int, List<Vector2Int>>();
                foreach (var (p, q) in m_Edges)
                {
                    if (!links.TryGetValue(p, out var lp)) links[p] = lp = new List<Vector2Int>();
                    if (!links.TryGetValue(q, out var lq)) links[q] = lq = new List<Vector2Int>();
                    lp.Add(q);
                    lq.Add(p);
                }
                for (var p = far; p != m_Start && along.ContainsKey(p);)
                {
                    var next = p;
                    foreach (var q in links[p]) if (along.TryGetValue(q, out var d) && d < along[next]) next = q;
                    if (next == p) break;
                    p = next;
                    main.Add(p);
                }
                if (m_MainEdges.Count > 0) main.Clear(); // (the plan has its main way: a gallery)
                void Mark(int x0, int z0, int size, bool isMain)
                {
                    for (int x = x0; x < x0 + size; x++)
                        for (int z = z0; z < z0 + size; z++)
                        {
                            if (x < 0 || z < 0 || x >= nx || z >= nz || !c.Footprint[x, z]) continue;
                            int b = m_B.Block[x / M, z / M];
                            if (b >= 0 && m_B.Reserved[b]) continue;
                            m_B.Corridor[x, z] = true;
                            if (isMain) m_B.Main[x, z] = true;
                        }
                }
                foreach (var (p, q) in m_Edges)
                {
                    bool isMain = main.Contains(p) && main.Contains(q) || m_MainEdges.Contains(Key(p, q));
                    int size = isMain ? MainCells : LaneCells;
                    // a 2 / 3 m square at every point of the edge (two points a metre apart: the squares join into a band)
                    foreach (var pt in new[] { p, q }) Mark(pt.x * M - 2, pt.y * M - 2, size, isMain);
                }
                // a hall at some crossings: waiting areas, the nurse station's lobby, a lift lobby
                float hallChance = m_B.Scheme switch { Scheme.Streets => 0.15f, Scheme.Hub => 0.08f, Scheme.Racetrack => 0.06f, Scheme.Axis => 0.25f, _ => 0f };
                if (m_Hub && m_Depth[m_Start.x, m_Start.y] >= 4) { Mark2(m_Start.x * M - 7, m_Start.y * M - 7, 14, 14); m_B.Halls++; } // the hub
                var pts = new List<Vector2Int>(m_Net);
                pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
                foreach (var p in pts)
                {
                    if (!links.TryGetValue(p, out var l) || l.Count < 3 || m_Rng.NextDouble() >= hallChance) continue;
                    int w = m_Rng.Next(5, 8), d = m_Rng.Next(5, 8);
                    if (!Point(p) || m_Depth[p.x, p.y] < 4) continue;
                    Mark2(p.x * M - w * M / 2, p.y * M - d * M / 2, w * M, d * M);
                    m_B.Halls++;
                }
                void Mark2(int x0, int z0, int w, int d)
                {
                    for (int x = x0; x < x0 + w; x++)
                        for (int z = z0; z < z0 + d; z++)
                        {
                            if (x < 0 || z < 0 || x >= nx || z >= nz || !c.Footprint[x, z]) continue;
                            int b = m_B.Block[x / M, z / M];
                            if (b >= 0 && m_B.Reserved[b]) continue;
                            m_B.Corridor[x, z] = true;
                        }
                }
            }
        }

        /// <summary>A binary heap of (cost, key), the least first.</summary>
        sealed class MinHeap
        {
            readonly List<(float c, int k)> m_A = new();
            public int Count => m_A.Count;
            public void Push(float c, int k)
            {
                m_A.Add((c, k));
                int i = m_A.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (m_A[p].c <= c) break;
                    m_A[i] = m_A[p];
                    i = p;
                }
                m_A[i] = (c, k);
            }
            public (float c, int k) Pop()
            {
                var top = m_A[0];
                var last = m_A[m_A.Count - 1];
                m_A.RemoveAt(m_A.Count - 1);
                if (m_A.Count == 0) return top;
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1;
                    if (l >= m_A.Count) break;
                    int r = l + 1 < m_A.Count && m_A[l + 1].c < m_A[l].c ? l + 1 : l;
                    if (m_A[r].c >= last.c) break;
                    m_A[i] = m_A[r];
                    i = r;
                }
                m_A[i] = last;
                return top;
            }
        }

        // ------------------------------------------------------------------ one floor

        /// <summary>What a piece of a block became (it decides the room type later).</summary>
        enum Piece : byte { Single, Row, Open, Pod, ClusterRoom, ClusterHall, Anteroom, Atrium }

        static void ZoneBlocks(Ctx c, int f, List<Region> regions, int[,] owner, System.Func<RectInt, Region, int> claim)
        {
            var bp = c.Blocks.On(f);
            int nx = c.Plan.NX, nz = c.Plan.NZ;
            var rng = c.Rng(0xB10D, f);
            // the corridors (on a floor they reach into what is free: the halls of other floors are rooms here)
            var seen = new bool[nx, nz];
            for (int x = 0; x < nx; x++)
                for (int z = 0; z < nz; z++)
                {
                    if (!bp.Corridor[x, z] || seen[x, z] || owner[x, z] != -1) continue;
                    var reg = new Region { Type = RoomType.Corridor, Shape = RoomShape.Corridor, Typed = true };
                    int id = regions.Count;
                    var q = new Queue<Vector2Int>();
                    q.Enqueue(new Vector2Int(x, z));
                    seen[x, z] = true;
                    while (q.Count > 0)
                    {
                        var p = q.Dequeue();
                        owner[p.x, p.y] = id;
                        reg.Cells.Add(p);
                        foreach (var d in Dirs4)
                        {
                            var n = p + d;
                            if (n.x < 0 || n.y < 0 || n.x >= nx || n.y >= nz || seen[n.x, n.y] || !bp.Corridor[n.x, n.y] || owner[n.x, n.y] != -1) continue;
                            seen[n.x, n.y] = true;
                            q.Enqueue(n);
                        }
                    }
                    reg.Rects.AddRange(RectsOf(reg.Cells, LaneCells, LaneCells * LaneCells)); // its straight runs (the ducts run along them)
                    regions.Add(reg);
                }
            // every block: what it becomes on this floor
            int cluster = 0;
            var blockCells = new Dictionary<int, List<Vector2Int>>();
            for (int x = 0; x < bp.W; x++)
                for (int z = 0; z < bp.D; z++)
                {
                    int b = bp.Block[x, z];
                    if (b < 0) continue;
                    // a metre whose four plan cells are all free
                    if (owner[x * M, z * M] != -1 || owner[x * M + 1, z * M] != -1 || owner[x * M, z * M + 1] != -1 || owner[x * M + 1, z * M + 1] != -1) continue;
                    if (!blockCells.TryGetValue(b, out var l)) blockCells[b] = l = new List<Vector2Int>();
                    l.Add(new Vector2Int(x, z));
                }
            var keys = new List<int>(blockCells.Keys);
            keys.Sort();
            foreach (int b in keys)
                foreach (var comp in Pieces(blockCells[b]))
                foreach (var q in RectsOf(comp))
                {
                    var mine = new HashSet<Vector2Int>();
                    for (int x = q.xMin; x < q.xMax; x++) for (int z = q.yMin; z < q.yMax; z++) mine.Add(new Vector2Int(x, z));
                    var sides = Contact(bp, mine, q);
                    bool hospital = !bp.Station && !bp.Office;
                    bp.Doubles = hospital && f == c.Floors - 1 && f > 0 ? 0.35 : 0.12; // (a hospital's top floor: room for two theatres)
                    foreach (var (r, piece, cl) in Treat(bp, q, mine, sides, rng, ref cluster, hospital && f > 0))
                    {
                        var reg = new Region
                        {
                            Type = piece == Piece.ClusterHall ? RoomType.Corridor : RoomType.Office,
                            Shape = piece == Piece.ClusterHall ? RoomShape.Corridor : RoomShape.Rect,
                            Typed = piece == Piece.ClusterHall,
                            Cluster = cl,
                            Piece = piece,
                            Court = piece == Piece.Atrium,
                        };
                        ClaimIn(regions, owner, reg, r, mine);
                    }
                }
            // what is left (crumbs of the blocks, the cells under a corridor's corner)
            foreach (var r in Rectangles(owner, nx, nz)) claim(r, new Region { Type = RoomType.Office, Shape = RoomShape.Rect, Piece = Piece.Single });
            Cleanup(regions, owner, nx, nz);
            for (int settle = 0; settle < 4; settle++)
            {
                Slim(regions, owner, nx, nz, LaneCells);
                SplitDisconnected(regions, owner, nx, nz);
                Cleanup(regions, owner, nx, nz);
            }
                        // scraps too small or too thin for a room's furniture join the room beside them (of their cluster first)
            for (int pass = 0; pass < 3; pass++)
            {
                var near = Borders(owner, nx, nz);
                bool moved = false;
                for (int i = 0; i < regions.Count; i++)
                {
                    var r = regions[i];
                    if (r.Cells.Count == 0 || r.Fixed || r.Type == RoomType.Corridor) continue;
                    if (AreaOf(r) >= 14f && Thin(r) >= 7) continue;
                    int best = -1;
                    float most = 0f;
                    foreach (var kv in near)
                    {
                        int o = kv.Key.Item1 == i ? kv.Key.Item2 : kv.Key.Item2 == i ? kv.Key.Item1 : -1;
                        if (o < 0 || regions[o].Fixed || regions[o].Type == RoomType.Corridor || regions[o].Cells.Count == 0) continue;
                        float score = kv.Value + (regions[o].Cluster == r.Cluster ? 100f : 0f);
                        if (score > most) { most = score; best = o; }
                    }
                    if (best < 0 || most < 2 * M) continue;
                    Absorb(regions, owner, i, best);
                    moved = true;
                }
                if (!moved) break;
            }
            // which rooms share a wall of 2 m with a corridor (a cluster's hall too): the ones a one-door room may be
            var borders = Borders(owner, nx, nz);
            var onCorridor = new HashSet<Region>();
            var nearCorridor = new HashSet<Region>(); // (3 m: a theatre's one door with its margins, where 4 m is not to be had)
            foreach (var kv in borders)
            {
                if (kv.Value < 3 * M) continue;
                var (a, b) = kv.Key;
                var into = kv.Value >= 4 * M ? onCorridor : null; // (4 m of wall: a door and its margins, whatever the wall's corners)
                if (regions[a].Type == RoomType.Corridor && regions[b].Type != RoomType.Corridor) { nearCorridor.Add(regions[b]); into?.Add(regions[b]); }
                if (regions[b].Type == RoomType.Corridor && regions[a].Type != RoomType.Corridor) { nearCorridor.Add(regions[a]); into?.Add(regions[a]); }
            }
            // a hospital's top floor: two theatres want two plain rooms 5.5 m across on a corridor — two rooms of a row join
            // into one where there are not enough
            if (!bp.Station && !bp.Office && f == c.Floors - 1 && f > 0)
            {
                bool Theatre(Region r) => r.Cells.Count > 0 && r.Type != RoomType.Corridor && !r.Fixed && Square(r) && Thin(r) >= 11 && AreaOf(r) >= 26f && AreaOf(r) <= 80f && nearCorridor.Contains(r);
                int have = 0;
                foreach (var r in regions) if (Theatre(r)) have++;
                var near = Borders(owner, nx, nz);
                foreach (var kv in near)
                {
                    if (have >= 2) break;
                    var (i, j) = kv.Key;
                    var a = regions[i]; var b2 = regions[j];
                    if (a.Cells.Count == 0 || b2.Cells.Count == 0 || a.Fixed || b2.Fixed || a.Type == RoomType.Corridor || b2.Type == RoomType.Corridor) continue;
                    if (!nearCorridor.Contains(a) && !nearCorridor.Contains(b2) || Theatre(a) || Theatre(b2)) continue;
                    var ba = BoundsOf(a.Cells); var bb = BoundsOf(b2.Cells);
                    var u = new RectInt(Mathf.Min(ba.xMin, bb.xMin), Mathf.Min(ba.yMin, bb.yMin), 0, 0);
                    u.xMax = Mathf.Max(ba.xMax, bb.xMax); u.yMax = Mathf.Max(ba.yMax, bb.yMax);
                    float sum = AreaOf(a) + AreaOf(b2);
                    // together a plain rectangle, wide enough for the table and its machines
                    if (u.width * u.height != a.Cells.Count + b2.Cells.Count || Mathf.Min(u.width, u.height) < 11 || sum < 26f || sum > 80f) continue;
                    if (!nearCorridor.Contains(a)) { (i, j) = (j, i); (a, b2) = (b2, a); }
                    Absorb(regions, owner, j, i);
                    if (Theatre(a)) have++;
                }
                borders = Borders(owner, nx, nz);
            }
            if (bp.Station) TypeStation(c, f, regions, rng);
            else if (bp.Office) TypeOffice(c, f, regions, rng);
            else TypeHospital(c, f, regions, rng, onCorridor, nearCorridor);
            KeepReachable(regions, borders);
            // a region's rectangles are its cells' (absorbed scraps brought rectangles bigger than what they gave)
            foreach (var reg in regions)
            {
                if (reg.Cells.Count == 0 || reg.Fixed) continue;
                var rects = RectsOf(reg.Cells, 2, 4);
                if (rects.Count == 0) continue;
                reg.Rects.Clear();
                reg.Rects.AddRange(rects);
            }
            AssignTypes(c, f, regions);
            Atriums(c, f, regions, owner); // (an office's: the hospital and the station have none)
            Commit(c, f, regions, owner);
        }

        /// <summary>The block's own free metres in a rectangle (metres) claimed as a region (plan cells).</summary>
        static void ClaimIn(List<Region> regions, int[,] owner, Region reg, RectInt metres, HashSet<Vector2Int> mine)
        {
            int id = regions.Count;
            for (int x = metres.xMin; x < metres.xMax; x++)
                for (int z = metres.yMin; z < metres.yMax; z++)
                {
                    if (!mine.Contains(new Vector2Int(x, z))) continue;
                    for (int i = 0; i < M; i++)
                        for (int j = 0; j < M; j++)
                        {
                            int px = x * M + i, pz = z * M + j;
                            if (owner[px, pz] != -1) continue;
                            owner[px, pz] = id;
                            reg.Cells.Add(new Vector2Int(px, pz));
                        }
                }
            if (reg.Cells.Count == 0) return;
            reg.Rects.Add(new RectInt(metres.x * M, metres.y * M, metres.width * M, metres.height * M));
            regions.Add(reg);
        }

        /// <summary>
        /// A piece of a block as rectangles, the biggest first (a corridor through a block leaves it an L or a U: rooms are
        /// cut from straight walls); what is too thin for a room is left for the tidying (it joins a neighbour).
        /// </summary>
        static List<RectInt> RectsOf(List<Vector2Int> cells, int minSide = 4, int minArea = 16)
        {
            var left = new HashSet<Vector2Int>(cells);
            var b = MetreBounds(cells);
            var list = new List<RectInt>();
            while (left.Count > 0)
            {
                var h = new int[b.height + 1];
                RectInt best = default;
                int bestArea = 0;
                for (int x = b.xMin; x < b.xMax; x++)
                {
                    for (int z = 0; z < b.height; z++) h[z] = left.Contains(new Vector2Int(x, b.yMin + z)) ? h[z] + 1 : 0;
                    h[b.height] = 0;
                    var st = new Stack<(int start, int height)>();
                    for (int z = 0; z <= b.height; z++)
                    {
                        int start = z;
                        while (st.Count > 0 && st.Peek().height >= h[z])
                        {
                            var (j, hh) = st.Pop();
                            int area = hh * (z - j);
                            if (Mathf.Min(hh, z - j) >= minSide && area > bestArea) { bestArea = area; best = new RectInt(x - hh + 1, b.yMin + j, hh, z - j); }
                            start = j;
                        }
                        st.Push((start, h[z]));
                    }
                }
                if (bestArea < minArea) break;
                list.Add(best);
                for (int x = best.xMin; x < best.xMax; x++) for (int z = best.yMin; z < best.yMax; z++) left.Remove(new Vector2Int(x, z));
            }
            return list;
        }

        static List<List<Vector2Int>> Pieces(List<Vector2Int> cells)
        {
            var left = new HashSet<Vector2Int>(cells);
            var all = new List<List<Vector2Int>>();
            cells.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            foreach (var s in cells)
            {
                if (!left.Remove(s)) continue;
                var piece = new List<Vector2Int> { s };
                var q = new Queue<Vector2Int>();
                q.Enqueue(s);
                while (q.Count > 0)
                {
                    var p = q.Dequeue();
                    foreach (var d in Dirs4) if (left.Remove(p + d)) { piece.Add(p + d); q.Enqueue(p + d); }
                }
                all.Add(piece);
            }
            return all;
        }

        /// <summary>How long each side of a piece (W, E, S, N) lies on a corridor, metres.</summary>
        static int[] Contact(BlockPlan bp, HashSet<Vector2Int> mine, RectInt q)
        {
            var on = new int[4];
            bool Corr(int x, int z) => x >= 0 && z >= 0 && x < bp.W && z < bp.D && (bp.Corridor[x * M, z * M] || bp.Corridor[x * M + 1, z * M + 1]);
            foreach (var p in mine)
            {
                if (p.x < q.xMin + 2 && Corr(p.x - 1, p.y)) on[0]++;
                if (p.x >= q.xMax - 2 && Corr(p.x + 1, p.y)) on[1]++;
                if (p.y < q.yMin + 2 && Corr(p.x, p.y - 1)) on[2]++;
                if (p.y >= q.yMax - 2 && Corr(p.x, p.y + 1)) on[3]++;
            }
            return on;
        }

        /// <summary>What a piece of a block becomes: rectangles (metres) with what they are and their cluster (−1 none).</summary>
        static List<(RectInt r, Piece piece, int cluster)> Treat(BlockPlan bp, RectInt q, HashSet<Vector2Int> mine, int[] on, System.Random rng, ref int cluster, bool wards)
        {
            var o = new List<(RectInt, Piece, int)>();
            int area = mine.Count, m = Mathf.Min(q.width, q.height);
            int sides = 0, front = 0;
            for (int s = 0; s < 4; s++) { if (on[s] >= 2) sides |= 1 << s; if (on[s] > on[front]) front = s; }
            bool opposite = (sides & 3) == 3 && q.width >= 9 || (sides & 12) == 12 && q.height >= 9;
            var middle = new Vector2Int(q.x + q.width / 2, q.y + q.height / 2);
            foreach (var court in bp.Courts) if (court.Contains(middle)) { o.Add((q, Piece.Atrium, -1)); return o; }
            bool frontOfHouse = bp.Front != null && bp.Front[middle.x, middle.y];
            if (bp.Scheme == Scheme.Sheds)
            {
                // the front of house: rooms side by side between the corridor and the facade
                if (frontOfHouse) { Slices(o, q, front >= 2, rng, module: bp.Module + 1, doubles: 0.2); return o; }
                // a bay: one hall, or halls one behind the other (the far ones through the near ones)
                int n = Mathf.Max(1, Mathf.CeilToInt(area / (float)bp.Hall));
                var (W, D, f) = Orient(q, front);
                bool deep = D >= W;
                int L = deep ? D : W;
                for (int i = 0; i < n; i++)
                {
                    int a = L * i / n, b = L * (i + 1) / n;
                    o.Add((deep ? f(0, a, W, b - a) : f(a, 0, b - a, D), Piece.Single, -1));
                }
                return o;
            }
            if (bp.Scheme == Scheme.Grid && !frontOfHouse)
            {
                // a hall: an island between its aisles, whole or in two back to back
                if (m < 9 || area <= bp.Hall && rng.NextDouble() < 0.6) o.Add((q, Piece.Single, -1));
                else if (q.width >= q.height)
                {
                    int k = q.width / 2;
                    o.Add((new RectInt(q.x, q.y, k, q.height), Piece.Single, -1));
                    o.Add((new RectInt(q.x + k, q.y, q.width - k, q.height), Piece.Single, -1));
                }
                else
                {
                    int k = q.height / 2;
                    o.Add((new RectInt(q.x, q.y, q.width, k), Piece.Single, -1));
                    o.Add((new RectInt(q.x, q.y + k, q.width, q.height - k), Piece.Single, -1));
                }
                return o;
            }
            // weights by the kind of building
            float single = bp.Station ? 3.5f : 0.4f, rows = 0f, back = bp.Station ? 1f : 5f, clu = bp.Station ? 3f : 2.5f, pin = bp.Station ? 3f : 1f, open = bp.Station ? 1f : 0.5f;
            if (bp.Weights != null) { single = bp.Weights[0]; back = bp.Weights[1]; clu = bp.Weights[2]; pin = bp.Weights[3]; open = bp.Weights[4]; }
            if (bp.Scheme == Scheme.Grid) { single = 0.3f; back = 4f; clu = 2f; pin = 0f; open = 0f; } // (its front of house: small rooms)
            if (area < 64 || m < 4) { o.Add((q, Piece.Single, -1)); return o; }
            if (m <= 8 && area > bp.Hall) single = 0f;
            if (m <= 8) { rows = 4f; single = bp.Scheme == Scheme.Racetrack ? 0.4f : area < 110 || bp.Station ? 1f : 0.15f; back = clu = pin = open = 0f; }
            else
            {
                if (area < 100 || sides == 0) clu = 0f;
                if (m < 11 || area < 130 || sides == 0) pin = 0f;
                if (area < 120) open = 0f;
                if (!opposite) back = 0f;
                if (area > 150 && bp.Hall < 150) single = Mathf.Min(single, 0.25f);
            }
            if (wards) { back *= 2f; pin *= 0.5f; open *= 0.3f; }
            if (bp.Scheme >= Scheme.Racetrack) { } // (an office's floors keep their scheme)
            else if (bp.Kind == Character.Rows) { back *= 2f; rows *= 2f; clu *= 0.3f; pin *= 0.3f; }
            else if (bp.Kind == Character.Clusters) { clu *= wards ? 1.8f : 3f; pin *= 1.5f; back *= wards ? 0.8f : 0.5f; } // (a ward floor keeps its rows on the facade)
            float total = single + rows + back + clu + pin + open, roll = (float)rng.NextDouble() * total;
            int id = cluster++;
            if ((roll -= single) < 0f)
            {
                // one room, as big as the kind of building has them (a lab hall at the station; a hospital's rooms are small)
                if (area <= bp.Hall) o.Add((q, Piece.Single, -1));
                else
                {
                    var (W, D, f) = Orient(q, front);
                    int k = D / 2 + rng.Next(-1, 2);
                    Slices(o, f(0, 0, W, k), front >= 2, rng, Piece.Single, module: bp.Module, doubles: bp.Doubles);
                    Slices(o, f(0, k, W, D - k), front >= 2, rng, Piece.Single, module: bp.Module, doubles: bp.Doubles);
                }
            }
            else if ((roll -= rows) < 0f) Slices(o, q, front >= 2, rng, module: bp.Module, doubles: bp.Doubles);
            else if ((roll -= back) < 0f)
            {
                if ((sides & 3) == 3 && q.width >= 9 && !((sides & 12) == 12 && q.height >= 9 && rng.Next(2) == 0))
                {
                    int k = q.width / 2; // (back to back down the middle: both rows as deep)
                    Slices(o, new RectInt(q.x, q.y, k, q.height), false, rng, module: bp.Module, doubles: bp.Doubles);
                    Slices(o, new RectInt(q.x + k, q.y, q.width - k, q.height), false, rng, module: bp.Module, doubles: bp.Doubles);
                }
                else
                {
                    int k = q.height / 2;
                    Slices(o, new RectInt(q.x, q.y, q.width, k), true, rng, module: bp.Module, doubles: bp.Doubles);
                    Slices(o, new RectInt(q.x, q.y + k, q.width, q.height - k), true, rng, module: bp.Module, doubles: bp.Doubles);
                }
            }
            else if ((roll -= clu) < 0f) Cluster(bp, o, q, front, rng, id);
            else if ((roll -= pin) < 0f) Pinwheel(bp, o, q, front, rng, id);
            else
            {
                // open plan with closed pods in some corners
                int pods = rng.Next(1, 4);
                var corners = new List<int> { 0, 1, 2, 3 };
                for (int i = 0; i < pods; i++)
                {
                    int k = corners[rng.Next(corners.Count)];
                    corners.Remove(k);
                    int pw = rng.Next(4, Mathf.Max(5, q.width / 3 + 1)), pd = rng.Next(4, Mathf.Max(5, q.height / 3 + 1));
                    o.Add((new RectInt((k & 1) != 0 ? q.xMax - pw : q.xMin, (k & 2) != 0 ? q.yMax - pd : q.yMin, pw, pd), Piece.Pod, -1));
                }
                o.Add((q, Piece.Open, -1));
            }
            return o;
        }

        /// <summary>Rooms side by side across a strip: along x when <paramref name="alongX"/>.</summary>
        static void Slices(List<(RectInt, Piece, int)> o, RectInt q, bool alongX, System.Random rng, Piece piece = Piece.Row, int cluster = -1, int module = 0, double doubles = 0.12)
        {
            int L = alongX ? q.width : q.height, at = 0;
            while (at < L)
            {
                // the building's rhythm: rooms of one width, now and then a double one (calmer than every room its own width)
                int t = module > 0 ? (rng.NextDouble() < doubles ? 2 * module : module) : rng.Next(4, 7);
                if (module <= 0 && rng.NextDouble() < 0.18) t = Mathf.RoundToInt(t * 1.8f);
                if (L - at - t < (module > 0 ? module : 3)) t = L - at;
                o.Add((alongX ? new RectInt(q.x + at, q.y, t, q.height) : new RectInt(q.x, q.y + at, q.width, t), piece, cluster));
                at += t;
            }
        }

        /// <summary>A block seen from its corridor side: local (u along the corridor, v into the block) → metres.</summary>
        static (int W, int D, System.Func<int, int, int, int, RectInt> f) Orient(RectInt q, int side) => side switch
        {
            2 => (q.width, q.height, (u, v, a, b) => new RectInt(q.x + u, q.y + v, a, b)),
            3 => (q.width, q.height, (u, v, a, b) => new RectInt(q.x + u, q.yMax - v - b, a, b)),
            0 => (q.height, q.width, (u, v, a, b) => new RectInt(q.x + v, q.y + u, b, a)),
            _ => (q.height, q.width, (u, v, a, b) => new RectInt(q.xMax - v - b, q.y + u, b, a)),
        };

        /// <summary>Where along the side the corridor touches the block (local u).</summary>
        static List<int> Span(BlockPlan bp, RectInt q, int side)
        {
            var l = new List<int>();
            bool Corr(int x, int z) => x >= 0 && z >= 0 && x < bp.W && z < bp.D && (bp.Corridor[x * M, z * M] || bp.Corridor[x * M + 1, z * M + 1]);
            int n = side >= 2 ? q.width : q.height;
            for (int i = 0; i < n; i++)
            {
                bool hit = side switch
                {
                    2 => Corr(q.x + i, q.y - 1) || Corr(q.x + i, q.y),
                    3 => Corr(q.x + i, q.yMax) || Corr(q.x + i, q.yMax - 1),
                    0 => Corr(q.x - 1, q.y + i) || Corr(q.x, q.y + i),
                    _ => Corr(q.xMax, q.y + i) || Corr(q.xMax - 1, q.y + i),
                };
                if (hit) l.Add(i);
            }
            return l;
        }

        /// <summary>A cluster: a private hall from the corridor into the block, rooms on both sides of it, rooms at its end.</summary>
        static void Cluster(BlockPlan bp, List<(RectInt, Piece, int)> o, RectInt q, int side, System.Random rng, int id)
        {
            var (W, D, f) = Orient(q, side);
            var span = Span(bp, q, side);
            int u = -1;
            float best = float.MaxValue;
            foreach (int a in span)
            {
                if (!span.Contains(a + 1) || !span.Contains(a + 2) || a < 3 || a > W - 6) continue;
                float score = Mathf.Abs(a + 1.5f - W / 2f) + (float)rng.NextDouble() * 4f;
                if (score < best) { best = score; u = a; }
            }
            if (u < 0 || W < 9 || D < 8)
            {
                if (D > 9) { int k = D / 2; Slices(o, f(0, 0, W, k), side >= 2, rng, module: bp.Module, doubles: bp.Doubles); Slices(o, f(0, k, W, D - k), side >= 2, rng, module: bp.Module, doubles: bp.Doubles); }
                else Slices(o, q, side >= 2, rng, module: bp.Module, doubles: bp.Doubles);
                return;
            }
            const int hw = 3; // the private hall, 3 m wide (2.5 m once the walls are in)
            int hall = rng.Next(Mathf.Max(4, D - 6), D - 3);
            void Side(int u0, int w)
            {
                if (w <= 0) return;
                int at = 0;
                while (at < hall)
                {
                    int t = rng.Next(3, 7);
                    if (hall - at - t < 3) t = hall - at;
                    o.Add((f(u0, at, w, t), Piece.ClusterRoom, id));
                    at += t;
                }
            }
            Side(0, u);
            Side(u + hw, W - u - hw);
            o.Add((f(u, 0, hw, hall), Piece.ClusterHall, id));
            if (D - hall >= 4)
            {
                if (W < 12) o.Add((f(0, hall, W, D - hall), Piece.ClusterRoom, id));
                else
                {
                    int k = Mathf.Clamp(u + 1 + rng.Next(-1, 2), 4, W - 4);
                    o.Add((f(0, hall, k, D - hall), Piece.ClusterRoom, id));
                    o.Add((f(k, hall, W - k, D - hall), Piece.ClusterRoom, id));
                }
            }
        }

        /// <summary>A pinwheel: four rooms round a small hall, the one on the corridor's side the anteroom into it.</summary>
        static void Pinwheel(BlockPlan bp, List<(RectInt, Piece, int)> o, RectInt q, int side, System.Random rng, int id)
        {
            var (W, D, f) = Orient(q, side);
            if (W < 11 || D < 11) { Cluster(bp, o, q, side, rng, id); return; }
            int hw = Mathf.Min(6, rng.Next(3, Mathf.Max(4, W - 8))), hd = Mathf.Min(6, rng.Next(3, Mathf.Max(4, D - 8)));
            int x1 = rng.Next(4, Mathf.Max(5, W - hw - 3)), z1 = rng.Next(4, Mathf.Max(5, D - hd - 3));
            int x2 = x1 + hw, z2 = z1 + hd;
            o.Add((f(x1, z1, hw, hd), Piece.ClusterHall, id));
            o.Add((f(0, 0, x2, z1), Piece.Anteroom, id));
            void Arm(RectInt r)
            {
                // a big arm in two
                if (r.width * r.height >= 52 && Mathf.Max(r.width, r.height) >= 9)
                {
                    if (r.width >= r.height) { int c = r.width / 2 + rng.Next(-1, 2); o.Add((new RectInt(r.x, r.y, c, r.height), Piece.ClusterRoom, id)); o.Add((new RectInt(r.x + c, r.y, r.width - c, r.height), Piece.ClusterRoom, id)); }
                    else { int c = r.height / 2 + rng.Next(-1, 2); o.Add((new RectInt(r.x, r.y, r.width, c), Piece.ClusterRoom, id)); o.Add((new RectInt(r.x, r.y + c, r.width, r.height - c), Piece.ClusterRoom, id)); }
                }
                else o.Add((r, Piece.ClusterRoom, id));
            }
            Arm(f(x2, 0, W - x2, z2));
            Arm(f(x1, z2, W - x1, D - z2));
            Arm(f(0, z1, x1, D - z1));
        }

        // ------------------------------------------------------------------ room types

        /// <summary>The share of a room's wall on the facade (its windows), metres.</summary>
        static float Facade(Ctx c, Region r) => OutsideWall(c, r);

        static float AreaOf(Region r) => r.Cells.Count / (float)(M * M);

        /// <summary>A plain rectangle (its cells fill its bounds).</summary>
        static bool Square(Region r) { var b = BoundsOf(r.Cells); return b.width * b.height == r.Cells.Count; }

        /// <summary>The narrow side of a room's bounds, plan cells.</summary>
        static int Thin(Region r) { var b = BoundsOf(r.Cells); return Mathf.Min(b.width, b.height); }

        /// <summary>
        /// The hospital: the ground floor its admissions (examination rooms, imaging, a lab, offices, the staff's rooms), the
        /// floors above its wards (patient rooms on the facade, a nurse station on a corridor in the middle), the top floor
        /// its theatres; toilets by the stairs on every floor.
        /// </summary>
        static void TypeHospital(Ctx c, int f, List<Region> regions, System.Random rng, HashSet<Region> onCorridor, HashSet<Region> nearCorridor)
        {
            var rooms = Typeable(regions);
            bool top = f == c.Floors - 1 && f > 0;
            var core = HallCentres(c, f);
            void Take(RoomType t, int n, System.Func<Region, float> score)
            {
                for (int k = 0; k < n; k++)
                {
                    Region best = null;
                    float bs = float.MinValue;
                    foreach (var r in rooms)
                    {
                        if (r.Typed) continue;
                        float s = score(r);
                        if (s > bs) { bs = s; best = r; }
                    }
                    if (best == null || bs == float.MinValue) return;
                    best.Type = t;
                    best.Typed = true;
                }
            }
            float Size(Region r, float lo, float hi) { float a = AreaOf(r); return a < lo || a > hi ? float.MinValue : 0f; }
            float NearCore(Region r) => -Near(core, r);
            float Door(Region r) => onCorridor.Contains(r) ? 0f : float.MinValue; // a one-door room: its door onto a corridor
            bool storage = false;
            if (f == 0 && c.Program != null) foreach (var p in c.Program.Floors[0]) storage |= p.Type == RoomType.Storage && p.Required;
            Take(RoomType.Restroom, f == 0 ? 2 : 1, r => Size(r, 8f, 26f) + NearCore(r) - Facade(c, r));
            Take(RoomType.Kitchen, 1, r => Size(r, 24f, 50f) + NearCore(r) * 0.3f);
            if (f == 0)
            {
                if (storage) Take(RoomType.Storage, 1, r => Size(r, 12f, 40f) + (Square(r) ? 0f : float.MinValue) - Facade(c, r) * 2f + (r.Piece == Piece.ClusterRoom ? 4f : 0f) + (float)rng.NextDouble());
                Take(RoomType.Imaging, 2 + rng.Next(2), r => Size(r, 22f, 70f) + Door(r) + (Thin(r) < 9 || !Square(r) ? float.MinValue : Thin(r) < 13 ? -50f : 0f) - Facade(c, r) + AreaOf(r) * 0.2f);
                Take(RoomType.LockerRoom, 1, r => Size(r, 14f, 45f) - Facade(c, r));
                Take(RoomType.Lab, 1, r => Size(r, 16f, 60f) + (float)rng.NextDouble());
                Take(RoomType.Infirmary, 3 + rng.Next(3), r => Size(r, 12f, 32f) + (r.Piece is Piece.ClusterRoom or Piece.Row ? 3f : 0f) + (float)rng.NextDouble());
                Take(RoomType.Lounge, 1, r => r.Piece == Piece.Open ? 10f : Size(r, 40f, 200f));
            }
            else
            {
                if (top) Take(RoomType.Operating, 2, r => Size(r, 26f, 80f) + (nearCorridor.Contains(r) ? 0f : float.MinValue) + (Thin(r) < 11 || !Square(r) ? float.MinValue : 0f) - Facade(c, r) * 0.5f + (r.Cluster >= 0 ? 6f : 0f) + AreaOf(r) * 0.1f);
                if (top) Take(RoomType.Imaging, 1, r => Size(r, 22f, 70f) + Door(r) + (Thin(r) < 9 || !Square(r) ? float.MinValue : Thin(r) < 13 ? -50f : 0f) - Facade(c, r));
                // the nurse station: on a corridor, in the middle of the floor
                var mid = Middle(rooms);
                Take(RoomType.NurseStation, 1, r => Size(r, 10f, 34f) + Door(r) + (Thin(r) >= 10 ? 0f : -15f) /* (5 m: the counter and the chairs behind it) */ + (r.Cluster >= 0 ? -20f : 0f) + (r.Piece is Piece.Row or Piece.Single ? 0f : -20f) - (new Vector2(Mean(r).x, Mean(r).y) - mid).magnitude * 0.05f);
                // patient rooms: on the facade (their windows), as many as hold beds
                foreach (var r in rooms)
                    if (!r.Typed && AreaOf(r) >= 12f && AreaOf(r) <= 60f && Facade(c, r) >= 2f && r.Piece != Piece.Open && onCorridor.Contains(r)) { r.Type = RoomType.Ward; r.Typed = true; }
                Take(RoomType.Lounge, 1, r => r.Piece == Piece.Open ? 10f : Size(r, 30f, 200f));
                Take(RoomType.Infirmary, 2, r => Size(r, 12f, 32f) - Facade(c, r));
            }
            // the rest: the doctors' rooms (an Office here is a consulting room, the records office or the on-call room:
            // Furnisher.Practice) and treatment rooms; of the big ones one lab a floor, then a staff lounge or a treatment
            // hall (no meeting rooms, no second and third lab: a floor of them read as an office with a workshop)
            int labs = 0;
            foreach (var r in rooms) if (r.Typed && r.Type == RoomType.Lab) labs++;
            foreach (var r in rooms)
            {
                if (r.Typed) continue;
                float area = AreaOf(r);
                if (area <= 40f) r.Type = area >= 14f && rng.Next(4) == 0 ? RoomType.Infirmary : RoomType.Office;
                else if (labs == 0 && (area > 60f || rng.Next(3) == 0)) { r.Type = RoomType.Lab; labs++; }
                else r.Type = area > 60f && rng.Next(2) == 0 ? RoomType.Lounge : RoomType.Infirmary;
                r.Typed = true;
            }
        }

        /// <summary>The station: labs, control and servers below, the staff's quarters above, the computing floor on top.</summary>
        /// <summary>
        /// An office's rooms follow its room program (<see cref="RoomProgram"/>: what the kind of house has on this floor
        /// and how big): every room of the program, the ones the house must have first, then the biggest, takes the room
        /// that fits its type's rule and is nearest its size — the open space an open plan, the toilets by the stairs and off
        /// the facade, a control room, a kitchen or a meeting room a plain rectangle (their consoles, run and table want a
        /// wall), no anteroom anything but an office or a lounge. What the program did not ask for gets its type from the
        /// kind's weights (<see cref="AssignTypes"/>), as in an office of wings.
        /// </summary>
        static void TypeOffice(Ctx c, int f, List<Region> regions, System.Random rng)
        {
            var rooms = Typeable(regions);
            foreach (var r in rooms) if (r.Piece == Piece.Atrium) { r.Type = RoomType.Lounge; r.Typed = true; } // the court
            var core = HallCentres(c, f);
            var wanted = new List<ProgramRoom>();
            if (c.Program != null && f < c.Program.Floors.Length) wanted.AddRange(c.Program.Floors[f]);
            // (a stable order: the must-haves, then by size, then as the program had them)
            var order = new List<int>();
            for (int k = 0; k < wanted.Count; k++) order.Add(k);
            order.Sort((a, b) => wanted[a].Required != wanted[b].Required ? (wanted[a].Required ? -1 : 1)
                : wanted[a].Area != wanted[b].Area ? wanted[b].Area.CompareTo(wanted[a].Area) : a.CompareTo(b));
            bool openSpace = false, lounge = false, office = false;
            foreach (var w in c.B.RoomWeights)
            {
                openSpace |= w.Room == RoomType.OpenSpace && w.Weight > 0;
                lounge |= w.Room == RoomType.Lounge && w.Weight > 0;
                office |= w.Room == RoomType.Office && w.Weight > 0;
            }
            foreach (int k in order)
            {
                var want = wanted[k];
                // the locked storage room is the house's own (StorageRooms): the back rooms its weight stood for are offices
                var type = want.Type == RoomType.Storage && !want.Required ? RoomType.Office : want.Type;
                var rule = RoomRules.For(type, c.B.RoomTypeRules);
                Region best = null;
                float bs = float.MinValue;
                foreach (var r in rooms)
                {
                    if (r.Typed) continue;
                    var b = BoundsOf(r.Cells);
                    float area = AreaOf(r), aspect = Mathf.Max(b.width, b.height) / (float)Mathf.Max(1, Mathf.Min(b.width, b.height));
                    bool rect = r.Cells.Count == b.width * b.height;
                    if (!RoomRules.Fits(rule, area, aspect, rule.OutsideWall > 0f ? OutsideWall(c, r) : 0f, rect)) continue;
                    float s = -10f * Mathf.Abs(area - want.Area) / Mathf.Max(8f, want.Area) + (float)rng.NextDouble();
                    if (type == RoomType.OpenSpace) s += r.Piece == Piece.Open ? 8f : 0f;
                    else if (r.Piece == Piece.Open) s -= 6f;
                    if (r.Piece == Piece.Anteroom && type is not (RoomType.Office or RoomType.Lounge)) s -= 12f;
                    if (type is RoomType.Restroom or RoomType.LockerRoom or RoomType.Storage or RoomType.Server or RoomType.CopyRoom) s -= Facade(c, r) * 0.5f;
                    if (type == RoomType.Restroom) s -= Near(core, r) * 0.5f + (r.Cluster >= 0 ? 10f : 0f);
                    if (type is RoomType.ControlRoom or RoomType.Kitchen or RoomType.Meeting) s += rect && Thin(r) >= 8 ? 4f : -4f;
                    // private rooms lie away from the lobby and the stairs, public ones by them (RoomRule.Privacy: +1 deep, −1 near)
                    s += rule.Privacy * Mathf.Min(Near(core, r), 30f) * 0.25f;
                    if (s > bs) { bs = s; best = r; }
                }
                if (best == null) continue;
                best.Type = type;
                best.Typed = true;
                best.Required = want.Required;
            }
            foreach (var r in rooms)
            {
                if (r.Typed) continue;
                // an open plan nobody asked for is open space all the same; an anteroom is the way into its suite
                if (r.Piece == Piece.Open && openSpace && AreaOf(r) >= 28f) { r.Type = RoomType.OpenSpace; r.Typed = true; }
                else if (r.Piece == Piece.Anteroom && lounge && AreaOf(r) >= 30f) { r.Type = RoomType.Lounge; r.Typed = true; }
                else if (r.Piece == Piece.Anteroom && office) { r.Type = RoomType.Office; r.Typed = true; }
            }
        }

        static void TypeStation(Ctx c, int f, List<Region> regions, System.Random rng)
        {
            var rooms = Typeable(regions);
            bool top = f == c.Floors - 1 && f > 0;
            var core = HallCentres(c, f);
            void Take(RoomType t, int n, System.Func<Region, float> score)
            {
                for (int k = 0; k < n; k++)
                {
                    Region best = null;
                    float bs = float.MinValue;
                    foreach (var r in rooms) { if (r.Typed) continue; float s = score(r); if (s > bs) { bs = s; best = r; } }
                    if (best == null || bs == float.MinValue) return;
                    best.Type = t;
                    best.Typed = true;
                }
            }
            float Size(Region r, float lo, float hi) { float a = AreaOf(r); return a < lo || a > hi ? float.MinValue : 0f; }
            bool storage = false;
            if (f == 0 && c.Program != null) foreach (var p in c.Program.Floors[0]) storage |= p.Type == RoomType.Storage && p.Required;
            Take(RoomType.Restroom, 1, r => Size(r, 8f, 26f) - Near(core, r) - Facade(c, r));
            if (f == 0)
            {
                if (storage) Take(RoomType.Storage, 1, r => Size(r, 12f, 40f) + (Square(r) ? 0f : float.MinValue) - Facade(c, r) * 2f + (float)rng.NextDouble());
                Take(RoomType.ControlRoom, 1, r => Size(r, 24f, 60f) + (r.Piece == Piece.ClusterHall ? -99f : 0f) + (float)rng.NextDouble());
                Take(RoomType.Server, 2 + rng.Next(2), r => Size(r, 16f, 90f) - Facade(c, r));
                Take(RoomType.Kitchen, 1, r => Size(r, 24f, 64f));
                Take(RoomType.LockerRoom, 1, r => Size(r, 14f, 45f) - Facade(c, r));
                Take(RoomType.Infirmary, 1, r => Size(r, 14f, 35f));
                foreach (var r in rooms) if (!r.Typed && AreaOf(r) >= 20f) { r.Type = RoomType.Lab; r.Typed = true; }
            }
            else if (top)
            {
                Take(RoomType.ControlRoom, 1, r => Size(r, 24f, 60f));
                foreach (var r in rooms) if (!r.Typed && AreaOf(r) >= 14f && Facade(c, r) < 3f) { r.Type = RoomType.Server; r.Typed = true; }
            }
            else
            {
                Take(RoomType.Kitchen, 1, r => Size(r, 24f, 64f));
                Take(RoomType.Lounge, 1, r => r.Piece == Piece.Open ? 10f : Size(r, 30f, 200f));
                Take(RoomType.Library, 1, r => Size(r, 22f, 80f) + (float)rng.NextDouble());
                // the quarters: the clusters' rooms and the rows on the facade
                foreach (var r in rooms)
                    if (!r.Typed && AreaOf(r) >= 9f && AreaOf(r) <= 36f && (r.Cluster >= 0 || Facade(c, r) >= 2.5f)) { r.Type = RoomType.Bedroom; r.Typed = true; }
                foreach (var r in rooms) if (!r.Typed && AreaOf(r) >= 24f) { r.Type = RoomType.Lab; r.Typed = true; }
            }
            foreach (var r in rooms) { if (r.Typed) continue; r.Type = RoomType.Office; r.Typed = true; }
        }

        /// <summary>
        /// A one-door room (a patient room, a theatre, imaging, the nurse station) opens only onto a corridor: a room behind it
        /// that has no other way to a corridor would be shut in — that one-door room becomes an office (pure, on the floor's regions).
        /// </summary>
        static void KeepReachable(List<Region> regions, Dictionary<(int, int), int> borders)
        {
            var near = new Dictionary<int, List<int>>();
            foreach (var kv in borders)
            {
                if (kv.Value < 3 * M) continue; // (a door's wall)
                var (a, b) = kv.Key;
                if (!near.TryGetValue(a, out var la)) near[a] = la = new List<int>();
                if (!near.TryGetValue(b, out var lb)) near[b] = lb = new List<int>();
                la.Add(b);
                lb.Add(a);
            }
            bool Way(Region r) => r.Type == RoomType.Corridor || r.Fixed;
            for (int round = 0; round < 40; round++)
            {
                var reached = new HashSet<int>();
                var q = new Queue<int>();
                for (int i = 0; i < regions.Count; i++) if (regions[i].Cells.Count > 0 && Way(regions[i])) { reached.Add(i); q.Enqueue(i); }
                while (q.Count > 0)
                {
                    int i = q.Dequeue();
                    if (!near.TryGetValue(i, out var l)) continue;
                    foreach (int j in l)
                    {
                        if (reached.Contains(j) || regions[j].Cells.Count == 0) continue;
                        // a one-door room is reached from a corridor only, and leads nowhere
                        if (RoomTraits.OneDoor(regions[j].Type) && !Way(regions[i])) continue;
                        reached.Add(j);
                        if (!RoomTraits.OneDoor(regions[j].Type)) q.Enqueue(j);
                    }
                }
                int fix = -1;
                for (int i = 0; i < regions.Count && fix < 0; i++)
                {
                    if (regions[i].Cells.Count == 0 || reached.Contains(i) || !near.TryGetValue(i, out var l)) continue;
                    foreach (int j in l) if (RoomTraits.OneDoor(regions[j].Type) && reached.Contains(j) && regions[j].Type != RoomType.Storage) { fix = j; break; }
                }
                if (fix < 0) return;
                regions[fix].Type = RoomType.Office;
            }
        }

        static List<Region> Typeable(List<Region> regions)
        {
            var l = new List<Region>();
            foreach (var r in regions) if (r.Cells.Count > 0 && !r.Fixed && r.Type != RoomType.Corridor) { r.Typed = false; l.Add(r); }
            return l;
        }

        static List<Vector2> HallCentres(Ctx c, int f)
        {
            var l = new List<Vector2>();
            foreach (var h in c.Halls) if (h.floor == f || h.floor + 1 == f) l.Add(h.hall.center);
            if (f == 0) l.Add(c.Lobby.center);
            return l;
        }

        /// <summary>Metres from a room's middle to the nearest of these points (plan cells).</summary>
        static float Near(List<Vector2> points, Region r)
        {
            var m = Mean(r);
            float best = 99f;
            foreach (var p in points) best = Mathf.Min(best, (p - m).magnitude / M);
            return best;
        }

        static Vector2 Mean(Region r)
        {
            var s = Vector2.zero;
            foreach (var p in r.Cells) s += p;
            return s / Mathf.Max(1, r.Cells.Count);
        }

        static Vector2 Middle(List<Region> rooms)
        {
            var s = Vector2.zero;
            int n = 0;
            foreach (var r in rooms) { s += Mean(r) * r.Cells.Count; n += r.Cells.Count; }
            return s / Mathf.Max(1, n);
        }

        static bool SameCluster(HousePlan plan, (int, int) key) =>
            plan.Rooms[key.Item1].Cluster >= 0 && plan.Rooms[key.Item1].Cluster == plan.Rooms[key.Item2].Cluster && (plan.Rooms[key.Item1].ClusterDoor || plan.Rooms[key.Item2].ClusterDoor);

        /// <summary>A cluster's rooms open only into their own hall or anteroom; its hall onto the corridors (pure).</summary>
        static bool ClusterAllows(HousePlan plan, (int, int) key)
        {
            var a = plan.Rooms[key.Item1];
            var b = plan.Rooms[key.Item2];
            if (a.Cluster < 0 && b.Cluster < 0) return true;
            if (a.Cluster == b.Cluster) return true;
            // one of them is in a cluster, the other not (or in another): only its hall, onto circulation
            bool Hall(PlanRoom r) => r.ClusterDoor;
            bool Circ(PlanRoom r) => r.Cluster < 0 && RoomTraits.Circulation(r.Type);
            return a.Cluster >= 0 ? Hall(a) && Circ(b) : Hall(b) && Circ(a);
        }
    }
}
