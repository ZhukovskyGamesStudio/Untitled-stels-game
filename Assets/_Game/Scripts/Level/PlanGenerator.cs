using System.Collections.Generic;
using UnityEngine;
using static Stealth.Level.LevelMetrics;

namespace Stealth.Level
{
    /// <summary>
    /// The floor plan of a house (pure: seed in, <see cref="HousePlan"/> out), the way an architect works, top down:
    /// the mass of the building (a bar, L, T, U, a courtyard, H — with annexes), the lobby at the facade and the stair
    /// halls, corridor spines along the wings, rooms cut from what is left (recursive splits to sizes of every kind,
    /// some merged into L/T shapes), rooms that rise through two or three storeys (atriums with galleries), shaped
    /// corners (cut, rounded, rotundas, octagons), then the walls from the raster and the doorways: a spanning tree
    /// from the lobby / the stairs (every room reachable), extra doors for loops, open passages, vents and slots.
    /// Every step draws from its own stream (<see cref="LevelRandom.Mix"/>), so tuning one does not reshuffle the rest.
    /// </summary>
    public static partial class PlanGenerator
    {
        /// <summary>Raster cells per metre.</summary>
        const int M = 2;
        const int MinSide = 3 * M;        // narrowest room
        const int StairHole = 4 * M;      // the stair and its hole (StairSize)
        const float DoorMargin = 0.9f;    // between a doorway and a corner / another opening / where the next wall continues

        public sealed class Output
        {
            public HousePlan Plan;
            public int Lobby;
            public readonly List<Stair> Stairs = new();
            /// <summary>The footprint parts (wings) the building was made of, in metres.</summary>
            public readonly List<RectInt> Wings = new();
            public string Mass;
            /// <summary>Rooms no doorway, open passage or staircase leads to (0 for a good plan).</summary>
            public int Unreached;
        }

        sealed class Ctx
        {
            public int Seed, House, Floors;
            public BuildingSettings B;
            public HousePlan Plan;
            public bool[,] Footprint;                // [x, z] cells inside the building
            public readonly List<RectInt> Wings = new(); // cells
            public RectInt Lobby;                    // cells, ground floor
            public readonly List<(int floor, RectInt hall, Vector2Int corner)> Halls = new(); // stair from floor to floor + 1
            public readonly Dictionary<(int, int, int), bool> Galleries = new();
            public readonly List<int> StairRooms = new();  // per hall: room on the lower floor, then on the upper
            public LinkedMass Linked;                       // a house of blocks joined by corridors (null: one mass)
            public GarageLayout Garage;                     // the auto repair shop's parts (null: any other house)
            public HospitalLayout Hospital;                 // the hospital's corridor and bands (null: any other house)
            public RoomProgram Program;                     // the rooms the house is to have (decided first)
            public readonly List<RectInt> LinkBlocks = new();    // cells
            public readonly List<RectInt> LinkCorridors = new(); // cells
            public System.Random Rng(int channel, int k = 0) => new(LevelRandom.Mix(Seed, channel, k));
            /// <summary>The floor being zoned: which sides of a rectangle (W, E, S, N) lie on a corridor, hall or lobby.</summary>
            public System.Func<RectInt, bool[]> Front;
        }

        /// <summary>A house of about <paramref name="area"/> m² of rooms per floor (its program drawn here: no storage room).</summary>
        public static Output Generate(int seed, int house, int floors, float area, BuildingSettings b) =>
            Generate(seed, house, floors, RoomProgram.Make(seed, floors, area * RoomShareOfArea, b, false), b);

        /// <summary>Of a floor's area, the share that is rooms (the rest: corridors, the lobby, the stair halls).</summary>
        public const float RoomShareOfArea = 0.75f;

        /// <summary>
        /// The plan of a house that has the rooms of <paramref name="program"/>: the building is sized for them, every room
        /// gets a place of at least its type's smallest size and keeps its type.
        /// </summary>
        public static Output Generate(int seed, int house, int floors, RoomProgram program, BuildingSettings b)
        {
            var c = new Ctx { Seed = seed, House = house, Floors = Mathf.Max(1, floors), B = b, Program = program };
            foreach (var list in program.Floors) for (int i = 0; i < list.Count; i++) { var r = list[i]; r.Block = -1; list[i] = r; }
            return Build(c, FootprintArea(program, b));
        }

        /// <summary>
        /// The classic way (the menu's switch, <see cref="LevelGenerator.Classic"/>): no room program — the mass of about
        /// <paramref name="area"/> m² a floor is cut into rooms of every size (BSP, <see cref="Split"/>), then each room's
        /// type is guessed from its size and proportions by the weights (<see cref="AssignTypes"/>, <see cref="Fit"/>).
        /// </summary>
        public static Output GenerateClassic(int seed, int house, int floors, float area, BuildingSettings b) =>
            Build(new Ctx { Seed = seed, House = house, Floors = Mathf.Max(1, floors), B = b }, area);

        static Output Build(Ctx c, float area)
        {
            var b = c.B;
            int house = c.House;
            var program = c.Program;
            var o = new Output();
            // first of all: does this house have corridors between its rooms? Then the links and corridor lengths come
            // first and the blocks are placed to fit them (PlanGenerator.Linked), one block per room; otherwise one mass
            // cut into the rooms
            List<RectInt> wings = null;
            var linkRng = c.Rng(0x11CC);
            if (!b.Garage && !b.Hospital && b.LinkedChance > 0f && linkRng.NextDouble() < b.LinkedChance)
            {
                if (program != null)
                {
                    var (sizes, storage) = LinkedSizes(linkRng, c);
                    c.Linked = Linked(linkRng, sizes, storage, c.Floors, b);
                }
                else c.Linked = Linked(linkRng, LinkedBlocks(linkRng, area, c.Floors, b), c.Floors, b);
                if (c.Linked == null && program != null) foreach (var list in program.Floors) for (int i = 0; i < list.Count; i++) { var r = list[i]; r.Block = -1; list[i] = r; }
                if (c.Linked != null)
                {
                    wings = new List<RectInt>(c.Linked.Blocks);
                    wings.AddRange(c.Linked.Corridors);
                    o.Mass = "linked";
                }
            }
            wings ??= b.Garage ? GarageMass(c, c.Rng(0xA55), out o.Mass)
                : b.Hospital ? HospitalMass(c, c.Rng(0xA55), area, out o.Mass)
                : Mass(c.Rng(0xA55), area, house, b, out o.Mass);
            foreach (var w in wings) o.Wings.Add(w);
            int maxX = 0, maxZ = 0;
            foreach (var w in wings) { maxX = Mathf.Max(maxX, w.xMax); maxZ = Mathf.Max(maxZ, w.yMax); }
            c.Plan = new HousePlan(c.Floors, maxX * M, maxZ * M);
            c.Footprint = new bool[maxX * M, maxZ * M];
            if (c.Linked != null)
            {
                foreach (var r in c.Linked.Blocks) c.LinkBlocks.Add(new RectInt(r.x * M, r.y * M, r.width * M, r.height * M));
                foreach (var r in c.Linked.Corridors) c.LinkCorridors.Add(new RectInt(r.x * M, r.y * M, r.width * M, r.height * M));
            }
            foreach (var w in wings)
            {
                var cw = new RectInt(w.x * M, w.y * M, w.width * M, w.height * M);
                c.Wings.Add(cw);
                for (int x = cw.xMin; x < cw.xMax; x++)
                    for (int z = cw.yMin; z < cw.yMax; z++) c.Footprint[x, z] = true;
            }
            CarveCourtyards(c, wings);

            if (c.Garage != null) ReserveGarage(c);
            else if (c.Hospital != null) ReserveHospital(c);
            else
            {
                ReserveLobby(c);
                ReserveHalls(c);
            }
            for (int f = 0; f < c.Floors; f++) ZoneFloor(c, f);
            var plan = c.Plan;
            SortStairRooms(c);
            Galleries(c);
            if (c.Garage != null) GarageGalleries(c);
            plan.DeriveWalls((f, voidRoom, other) => c.Galleries.TryGetValue((f, voidRoom, other), out var g) && g);
            o.Plan = plan;
            o.Lobby = plan.IdAt(0, c.Lobby.xMin + c.Lobby.width / 2, c.Lobby.yMin + c.Lobby.height / 2);

            for (int i = 0; i < c.Halls.Count; i++)
            {
                var (f, hall, corner) = c.Halls[i];
                var origin = new Vector2((corner.x > 0 ? hall.xMax : hall.xMin) / (float)M, (corner.y > 0 ? hall.yMax : hall.yMin) / (float)M);
                bool flip = c.Rng(0x57F, i).Next(2) == 1; // its own stream: which wall the flights run along
                o.Stairs.Add(new Stair { Floor = f, Origin = origin, Corner = corner, Flip = flip, Room = c.StairRooms[2 * i], RoomAbove = c.StairRooms[2 * i + 1] });
            }
            // rooms with a staircase: their centre (patrols, views, spawns) off the stair and its approach
            var avoid = new List<(int floor, Rect area)>();
            foreach (var s in o.Stairs)
            {
                avoid.Add((s.Floor, StairArea(s, 1.4f)));
                avoid.Add((s.Floor + 1, StairArea(s, 1.4f)));
            }
            foreach (var r in plan.Rooms)
                foreach (var (floor, zone) in avoid)
                    if (floor == r.Floor && zone.Contains(new Vector2(r.Center.x, r.Center.z))) { Summarize(plan, r, avoid); break; }
            for (int attempt = 0; attempt < 4; attempt++)
            {
                Openings(c, o);
                var lost = Unreached(plan, o);
                if (lost.Count == 0) break;
                // rooms sealed off behind pockets: square up the corners of the rooms whose pockets touch them
                var unshape = new HashSet<int>();
                foreach (int r in lost)
                    for (int x = 0; x < plan.NX; x++)
                        for (int z = 0; z < plan.NZ; z++)
                        {
                            int f = plan.Rooms[r].Floor;
                            if (plan.KindAt(f, x, z) != CellKind.Room || plan.IdAt(f, x, z) != r) continue;
                            foreach (var d in Dirs4)
                                if (plan.KindAt(f, x + d.x, z + d.y) == CellKind.Pocket) unshape.Add(plan.IdAt(f, x + d.x, z + d.y));
                        }
                if (unshape.Count == 0) break;
                foreach (int r in unshape) Unshape(plan, plan.Rooms[r]);
                plan.Openings.Clear();
                plan.DeriveWalls((f, voidRoom, other) => c.Galleries.TryGetValue((f, voidRoom, other), out var g) && g);
            }
            Depths(plan, o);
            o.Unreached = Unreached(plan, o).Count;
            return o;
        }

        // ------------------------------------------------------------------ the mass of the building

        /// <summary>
        /// The wings of the building in metres (x, z from 0): a bar, an L, a T, a U, a courtyard or an H, sized to the
        /// floor area, plus sometimes an annex sticking out. Wings overlap where they meet. Public for the tests: the
        /// variety of masses is a property of this function alone.
        /// </summary>
        public static List<RectInt> Mass(System.Random rng, float area, int house, out string kind) => Mass(rng, area, house, new BuildingSettings(), out kind);

        /// <summary>The mass with the weights of kinds and the annex chance of these settings (the rooms test: a bar only).</summary>
        public static List<RectInt> Mass(System.Random rng, float area, int house, BuildingSettings b, out string kind)
        {
            var list = new List<RectInt>();
            int Even(float v) => Mathf.Max(2, Mathf.RoundToInt(v / 2f) * 2);
            int ww = Even(Mathf.Clamp(Mathf.Sqrt(area) / 2.1f, 12f, 22f) + (float)rng.NextDouble() * 4f - 2f); // wing depth
            string[] kinds = { "bar", "L", "T", "U", "courtyard", "H" };
            int[] weights = b.MassWeights is { Length: 6 } && System.Array.Exists(b.MassWeights, x => x > 0) ? b.MassWeights
                          : area < 1100f ? new[] { 35, 35, 25, 5, 0, 0 } : area < 1800f ? new[] { 18, 26, 22, 18, 8, 8 } : new[] { 10, 18, 18, 20, 18, 16 };
            int total = 0;
            foreach (var w in weights) total += w;
            int roll = rng.Next(total), pick = 0;
            while (roll >= weights[pick]) roll -= weights[pick++];
            kind = kinds[pick];
            switch (pick)
            {
                case 0:
                {
                    float aspect = 1f + (float)rng.NextDouble() * 0.9f;
                    int w = Even(Mathf.Sqrt(area * aspect)), d = Even(area / w);
                    list.Add(new RectInt(0, 0, w, Mathf.Max(12, d)));
                    break;
                }
                case 1: // L: a long bar and an arm at one end
                {
                    float k = 0.55f + (float)rng.NextDouble() * 0.35f; // arm length / bar length
                    int len = Even(Mathf.Max(ww * 1.5f, (area / ww + ww) / (1f + k)));
                    int arm = Even(Mathf.Max(ww + 6, len * k));
                    list.Add(new RectInt(0, 0, len, ww));
                    list.Add(new RectInt(rng.Next(2) == 0 ? 0 : len - ww, 0, ww, arm));
                    break;
                }
                case 2: // T: a bar and a stem from its middle
                {
                    int len = Even(Mathf.Max(ww * 2f, area / ww * 0.6f));
                    int stem = Even(Mathf.Max(ww, (area - len * ww) / ww));
                    int x = Even((len - ww) / 2f);
                    list.Add(new RectInt(0, stem, len, ww));
                    list.Add(new RectInt(x, 0, ww, stem + 2));
                    break;
                }
                case 3: // U: a bar and two arms
                {
                    int len = Even(Mathf.Max(ww * 2.6f, area / ww * 0.5f));
                    int arm = Even(Mathf.Max(ww + 4, (area - len * ww) / (2f * ww) + ww));
                    list.Add(new RectInt(0, 0, len, ww));
                    list.Add(new RectInt(0, 0, ww, arm));
                    list.Add(new RectInt(len - ww, 0, ww, arm));
                    break;
                }
                case 4: // courtyard: a ring of four wings around an open yard
                {
                    int side = Even(area / (4f * ww) + ww);
                    int depth = Even(side * (0.8f + (float)rng.NextDouble() * 0.4f));
                    side = Mathf.Max(side, 2 * ww + 8);
                    depth = Mathf.Max(depth, 2 * ww + 8);
                    list.Add(new RectInt(0, 0, side, ww));
                    list.Add(new RectInt(0, depth - ww, side, ww));
                    list.Add(new RectInt(0, 0, ww, depth));
                    list.Add(new RectInt(side - ww, 0, ww, depth));
                    break;
                }
                default: // H: two bars and a bridge
                {
                    int len = Even(Mathf.Max(ww * 2f, area / (2.4f * ww)));
                    int gap = Even(Mathf.Max(8f, ww * 0.8f));
                    list.Add(new RectInt(0, 0, ww, len));
                    list.Add(new RectInt(ww + gap, 0, ww, len));
                    int bz = Even((len - ww) / 2f);
                    list.Add(new RectInt(ww - 2, bz, gap + 4, ww));
                    break;
                }
            }
            // An annex: a smaller block sticking out of a wing (a bay, a pavilion).
            if (rng.NextDouble() < b.AnnexChance)
            {
                var host = list[rng.Next(list.Count)];
                int w = Even(8 + rng.Next(8)), d = Even(6 + rng.Next(6));
                bool alongX = rng.Next(2) == 0;
                int pos = alongX ? host.x + rng.Next(Mathf.Max(1, host.width - w)) : host.y + rng.Next(Mathf.Max(1, host.height - w));
                pos = Mathf.RoundToInt(pos / 2f) * 2;
                list.Add(alongX
                    ? new RectInt(pos, host.yMax - 2, w, d + 2)
                    : new RectInt(host.xMax - 2, pos, d + 2, w));
            }
            // Shift to x, z ≥ 0.
            int minX = int.MaxValue, minZ = int.MaxValue;
            foreach (var r in list) { minX = Mathf.Min(minX, r.xMin); minZ = Mathf.Min(minZ, r.yMin); }
            for (int i = 0; i < list.Count; i++) list[i] = new RectInt(list[i].x - minX, list[i].y - minZ, list[i].width, list[i].height);
            return list;
        }

        /// <summary>The yard of a courtyard building stays outside even where wings overlap around it (nothing to do: wings never cover it).</summary>
        static void CarveCourtyards(Ctx c, List<RectInt> wings) { }

        // ------------------------------------------------------------------ reserved rooms

        /// <summary>The lobby: a room at the facade on the ground floor, the hatch in its middle.</summary>
        static void ReserveLobby(Ctx c)
        {
            if (c.Linked != null) { c.Lobby = c.LinkBlocks[0]; return; } // a linked house: the first block is the lobby
            var rng = c.Rng(0x10B);
            for (int attempt = 0; attempt < 200; attempt++)
            {
                int w = (8 + rng.Next(5)) * M, d = (8 + rng.Next(4)) * M;
                var wing = c.Wings[rng.Next(c.Wings.Count)];
                if (wing.width < w || wing.height < d) continue;
                int x = wing.xMin + rng.Next(wing.width - w + 1), z = wing.yMin + rng.Next(wing.height - d + 1);
                // pushed to a facade side of its wing
                switch (rng.Next(4))
                {
                    case 0: x = wing.xMin; break;
                    case 1: x = wing.xMax - w; break;
                    case 2: z = wing.yMin; break;
                    default: z = wing.yMax - d; break;
                }
                var r = new RectInt(x / M * M, z / M * M, w, d);
                if (!Inside(c, r) || !TouchesOutside(c, r)) continue;
                c.Lobby = Snug(c, r);
                return;
            }
            c.Lobby = new RectInt(0, 0, 8 * M, 8 * M);
        }

        /// <summary>One stair hall per pair of floors: the same rectangle on both, the stair in one of its corners.</summary>
        static void ReserveHalls(Ctx c)
        {
            var rng = c.Rng(0x57A);
            if (c.Linked != null)
            {
                // a linked house: the stair halls are blocks of their own, the stair in a corner on the yard
                for (int f = 0; f < c.Floors - 1 && f < c.Linked.Halls.Count; f++)
                {
                    var r = c.LinkBlocks[c.Linked.Halls[f]];
                    // the corner farthest from what joins the hall (a corridor or a block): the stair must not stand in the way in
                    var corner = new Vector2Int(1, 1);
                    int best = int.MaxValue;
                    foreach (var cand in new[] { new Vector2Int(-1, -1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(1, 1) })
                    {
                        int joined = JoinedNear(c, r, cand, StairHole + M);
                        if (joined < best || (joined == best && rng.Next(2) == 0)) { best = joined; corner = cand; }
                    }
                    c.Halls.Add((f, r, corner));
                }
                return;
            }
            // the stairs make a core, as in a real building: each next stair hall as close to the one below as the halls allow,
            // the first one near the lobby (a few valid places drawn, the nearest taken)
            for (int f = 0; f < c.Floors - 1; f++)
            {
                var found = new List<(RectInt hall, Vector2Int corner)>();
                for (int attempt = 0; attempt < 400 && found.Count < CoreCandidates; attempt++)
                {
                    int w = (6 + rng.Next(3)) * M, d = (6 + rng.Next(3)) * M;
                    var wing = c.Wings[rng.Next(c.Wings.Count)];
                    if (wing.width < w || wing.height < d) continue;
                    var r = new RectInt(wing.xMin + rng.Next(wing.width - w + 1) / M * M, wing.yMin + rng.Next(wing.height - d + 1) / M * M, w, d);
                    if (!Inside(c, r)) continue;
                    if (f == 0 && Grow(c.Lobby, 2 * M).Overlaps(r)) continue;
                    if (HallClash(c, f, r)) continue;
                    r = Snug(c, r);
                    // the stair goes into the corner whose walls face the outside (or are the fewest doors lost)
                    var corner = new Vector2Int(rng.Next(2) == 0 ? -1 : 1, rng.Next(2) == 0 ? -1 : 1);
                    int bestOut = -1;
                    foreach (var cand in new[] { new Vector2Int(-1, -1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(1, 1) })
                    {
                        int outside = (SideOutside(c, r, cand.x, 0) ? 1 : 0) + (SideOutside(c, r, 0, cand.y) ? 1 : 0);
                        if (outside > bestOut || (outside == bestOut && rng.Next(2) == 0)) { bestOut = outside; corner = cand; }
                    }
                    if ((f == 0 && Grow(c.Lobby, 2 * M).Overlaps(r)) || HallClash(c, f, r)) continue;
                    found.Add((r, corner));
                }
                if (found.Count == 0) continue;
                var anchor = f == 0 ? c.Lobby.center : c.Halls.Count > 0 ? c.Halls[c.Halls.Count - 1].hall.center : c.Lobby.center;
                int pick = 0;
                float best = float.MaxValue;
                for (int i = 0; i < found.Count; i++)
                {
                    float d = (found[i].hall.center - anchor).magnitude + (float)rng.NextDouble() * 2f * M; // a little play
                    if (d < best) { best = d; pick = i; }
                }
                c.Halls.Add((f, found[pick].hall, found[pick].corner));
            }
        }

        /// <summary>
        /// A stair hall here would clash with the halls of the floors next to it: overlap one, or leave a strip narrower than a
        /// room between them (it could only become a sliver of a room). Touching is fine: the stairs make one core.
        /// </summary>
        static bool HallClash(Ctx c, int f, RectInt r)
        {
            foreach (var h in c.Halls)
            {
                if (Mathf.Abs(h.floor - f) > 1) continue;
                if (h.hall.Overlaps(r)) return true;
                int gapX = Mathf.Max(r.xMin - h.hall.xMax, h.hall.xMin - r.xMax), gapZ = Mathf.Max(r.yMin - h.hall.yMax, h.hall.yMin - r.yMax);
                bool sideBySide = (gapX >= 0 && gapZ < 0) || (gapZ >= 0 && gapX < 0);
                int gap = Mathf.Max(gapX, gapZ);
                if (gap > 0 && gap < (sideBySide ? RoomSide : 2 * M)) return true;
            }
            return false;
        }

        /// <summary>Valid places drawn for a stair hall before the one nearest the core is taken.</summary>
        const int CoreCandidates = 12;

        static RectInt Grow(RectInt r, int k) => new(r.x - k, r.y - k, r.width + 2 * k, r.height + 2 * k);

        /// <summary>Building cells just outside a rectangle along the two sides of one corner, within <paramref name="reach"/> of it.</summary>
        static int JoinedNear(Ctx c, RectInt r, Vector2Int corner, int reach)
        {
            int n = 0;
            bool In(int x, int z) => x >= 0 && z >= 0 && x < c.Plan.NX && z < c.Plan.NZ && c.Footprint[x, z];
            int sx = corner.x > 0 ? r.xMax : r.xMin - 1, sz = corner.y > 0 ? r.yMax : r.yMin - 1;
            for (int k = 0; k < reach; k++)
            {
                int z = corner.y > 0 ? r.yMax - 1 - k : r.yMin + k;
                int x = corner.x > 0 ? r.xMax - 1 - k : r.xMin + k;
                if (z >= r.yMin && z < r.yMax && In(sx, z)) n++;
                if (x >= r.xMin && x < r.xMax && In(x, sz)) n++;
            }
            return n;
        }

        /// <summary>The side of a rectangle (dx or dz = ±1) lies on the facade along all of its length (a step beyond it too).</summary>
        static bool AllOutside(Ctx c, RectInt r, int dx, int dz)
        {
            bool Out(int x, int z) => x < 0 || z < 0 || x >= c.Plan.NX || z >= c.Plan.NZ || !c.Footprint[x, z];
            if (dx != 0)
            {
                int x = dx > 0 ? r.xMax : r.xMin - 1;
                for (int z = r.yMin - 1; z <= r.yMax; z++) if (!Out(x, z)) return false;
            }
            else
            {
                int z = dz > 0 ? r.yMax : r.yMin - 1;
                for (int x = r.xMin - 1; x <= r.xMax; x++) if (!Out(x, z)) return false;
            }
            return true;
        }

        /// <summary>The side of a rectangle (dx or dz = ±1) lies on the facade along most of its length.</summary>
        static bool SideOutside(Ctx c, RectInt r, int dx, int dz)
        {
            int n = 0, total = 0;
            if (dx != 0)
            {
                int x = dx > 0 ? r.xMax : r.xMin - 1;
                for (int z = r.yMin; z < r.yMax; z++) { total++; if (x < 0 || x >= c.Plan.NX || !c.Footprint[x, z]) n++; }
            }
            else
            {
                int z = dz > 0 ? r.yMax : r.yMin - 1;
                for (int x = r.xMin; x < r.xMax; x++) { total++; if (z < 0 || z >= c.Plan.NZ || !c.Footprint[x, z]) n++; }
            }
            return n * 2 > total;
        }

        /// <summary>
        /// A reserved room within 3 m of the facade reaches it: no strip too narrow for a room is left between them.
        /// </summary>
        static RectInt Snug(Ctx c, RectInt r)
        {
            int Gap(int dx, int dz)
            {
                // cells from the side of r outwards (along its middle) until the footprint ends
                int x = dx > 0 ? r.xMax : dx < 0 ? r.xMin - 1 : r.xMin + r.width / 2;
                int z = dz > 0 ? r.yMax : dz < 0 ? r.yMin - 1 : r.yMin + r.height / 2;
                int n = 0;
                while (x >= 0 && z >= 0 && x < c.Plan.NX && z < c.Plan.NZ && c.Footprint[x, z] && n < 7) { n++; x += dx; z += dz; }
                return n;
            }
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int g = Gap(dx, dz);
                if (g == 0 || g >= 6) continue;
                var grown = dx > 0 ? new RectInt(r.x, r.y, r.width + g, r.height) : dx < 0 ? new RectInt(r.x - g, r.y, r.width + g, r.height)
                          : dz > 0 ? new RectInt(r.x, r.y, r.width, r.height + g) : new RectInt(r.x, r.y - g, r.width, r.height + g);
                if (Inside(c, grown)) r = grown;
            }
            return r;
        }

        static bool Inside(Ctx c, RectInt r)
        {
            for (int x = r.xMin; x < r.xMax; x++)
                for (int z = r.yMin; z < r.yMax; z++)
                    if (x < 0 || z < 0 || x >= c.Plan.NX || z >= c.Plan.NZ || !c.Footprint[x, z]) return false;
            return true;
        }

        static bool TouchesOutside(Ctx c, RectInt r)
        {
            bool Out(int x, int z) => x < 0 || z < 0 || x >= c.Plan.NX || z >= c.Plan.NZ || !c.Footprint[x, z];
            for (int x = r.xMin; x < r.xMax; x++) if (Out(x, r.yMin - 1) || Out(x, r.yMax)) return true;
            for (int z = r.yMin; z < r.yMax; z++) if (Out(r.xMin - 1, z) || Out(r.xMax, z)) return true;
            return false;
        }

        // ------------------------------------------------------------------ zoning one floor

        sealed class Region
        {
            public RoomType Type;
            public RoomShape Shape;
            public bool Fixed;           // lobby, stair hall: never merged or shaped away
            public bool Typed;           // a room of the program: its type is decided (AssignTypes leaves it)
            public bool Required;        // the program must have it
            public int Hall = -1;        // index of the stair hall on this floor
            public readonly List<Vector2Int> Cells = new();
            public readonly List<RectInt> Rects = new();
            public int Levels = 1;
        }

        static void ZoneFloor(Ctx c, int f)
        {
            var plan = c.Plan;
            var rng = c.Rng(0x20E, f);
            int nx = plan.NX, nz = plan.NZ;
            var owner = new int[nx, nz];
            for (int x = 0; x < nx; x++)
                for (int z = 0; z < nz; z++)
                    owner[x, z] = c.Footprint[x, z] && plan.KindAt(f, x, z) == CellKind.Outside ? -1 : -2; // −1 free, −2 taken
            var regions = new List<Region>();

            int Claim(RectInt r, Region reg)
            {
                int n = 0;
                for (int x = r.xMin; x < r.xMax; x++)
                    for (int z = r.yMin; z < r.yMax; z++)
                    {
                        if (x < 0 || z < 0 || x >= nx || z >= nz || owner[x, z] != -1) continue;
                        owner[x, z] = regions.Count;
                        reg.Cells.Add(new Vector2Int(x, z));
                        n++;
                    }
                if (n > 0) { reg.Rects.Add(r); regions.Add(reg); }
                return n;
            }

            // Reserved rooms first: the plant room in the lobby's outer corner, the lobby, the stair halls (arriving from below, going up).
            if (f == 0)
            {
                if (UtilityCorner(c, c.Lobby) is RectInt plant)
                {
                    Claim(plant, new Region { Type = RoomType.Utility, Shape = RoomShape.Rect, Fixed = true, Typed = true });
                    // the lobby is the rest: an L of two rectangles
                    var lobby = new Region { Type = RoomType.Lobby, Shape = RoomShape.Rect, Fixed = true };
                    int id = regions.Count;
                    var lb = c.Lobby;
                    var parts = new List<RectInt>();
                    bool left = plant.xMin == lb.xMin, low = plant.yMin == lb.yMin;
                    parts.Add(left ? new RectInt(plant.xMax, lb.yMin, lb.xMax - plant.xMax, lb.height) : new RectInt(lb.xMin, lb.yMin, plant.xMin - lb.xMin, lb.height));
                    parts.Add(low ? new RectInt(plant.xMin, plant.yMax, plant.width, lb.yMax - plant.yMax) : new RectInt(plant.xMin, lb.yMin, plant.width, plant.yMin - lb.yMin));
                    foreach (var r in parts)
                    {
                        for (int x = r.xMin; x < r.xMax; x++)
                            for (int z = r.yMin; z < r.yMax; z++)
                            {
                                if (x < 0 || z < 0 || x >= nx || z >= nz || owner[x, z] != -1) continue;
                                owner[x, z] = id;
                                lobby.Cells.Add(new Vector2Int(x, z));
                            }
                        lobby.Rects.Add(r);
                    }
                    regions.Add(lobby);
                }
                else Claim(c.Lobby, new Region { Type = RoomType.Lobby, Shape = RoomShape.Rect, Fixed = true });
            }
            for (int i = 0; i < c.Halls.Count; i++)
            {
                var h = c.Halls[i];
                if (h.floor == f || h.floor + 1 == f) Claim(h.hall, new Region { Type = RoomType.Hall, Shape = RoomShape.Hall, Fixed = true, Hall = i });
            }

            if (c.Linked != null)
            {
                ZoneLinked(c, f, regions, owner, Claim);
                return;
            }
            if (c.Garage != null)
            {
                ZoneGarage(c, f, regions, owner, Claim);
                return;
            }
            if (c.Hospital != null)
            {
                ZoneHospital(c, f, regions, owner, Claim);
                return;
            }

            // Corridor spines along the wings (some floors / wings are open plan).
            var corridorRng = c.Rng(0xC0D, f);
            var spine = new bool[nx, nz];
            foreach (var wing in c.Wings)
            {
                int across = Mathf.Min(wing.width, wing.height), along = Mathf.Max(wing.width, wing.height);
                if (across < SingleLoaded * M || along < 10 * M || corridorRng.NextDouble() > c.B.CorridorChance) continue;
                int cw = (corridorRng.NextDouble() < 0.5 ? 5 : 6);          // 2.5 or 3 m
                bool alongX = wing.width >= wing.height;
                if (across < DoubleLoaded * M)
                {
                    // a narrow wing: one row of rooms with the corridor along its inner side (the rooms keep the windows);
                    // both sides on the facade: either
                    bool lowOut = alongX ? SideOutside(c, wing, 0, -1) : SideOutside(c, wing, -1, 0);
                    bool highOut = alongX ? SideOutside(c, wing, 0, 1) : SideOutside(c, wing, 1, 0);
                    bool atLow = lowOut != highOut ? !lowOut : corridorRng.Next(2) == 0;
                    MarkSpine(spine, wing, alongX, atLow ? 0 : across - cw, cw);
                    continue;
                }
                // Double-loaded: as in a real office, no room deeper than about 8 m from its corridor — a deep wing gets
                // several corridors, evenly spaced (a band of rooms between two of them opens onto either)
                int count = Mathf.Clamp(Mathf.CeilToInt(across / (float)(ModuleDepth * M)), 1, Mathf.Max(1, across / (2 * MinRoomDepth * M + cw)));
                float module = across / (float)count;
                for (int k = 0; k < count; k++)
                {
                    float jitter = ((float)corridorRng.NextDouble() - 0.5f) * M; // up to half a metre either way
                    int off = Mathf.Clamp(Mathf.RoundToInt(module * (k + 0.5f) - cw / 2f + jitter) / M * M, MinRoomDepth * M, across - cw - MinRoomDepth * M);
                    MarkSpine(spine, wing, alongX, off, cw);
                }
            }
            // Cross corridors: a passage across a wing, between the rooms on its two sides (the rooms test: a corridor
            // joins two rooms instead of a spine lined with many).
            var crossRng = c.Rng(0xC105, f);
            foreach (var wing in c.Wings)
            {
                int across = Mathf.Min(wing.width, wing.height), along = Mathf.Max(wing.width, wing.height);
                if (along < 18 * M || crossRng.NextDouble() >= c.B.CrossCorridorChance) continue;
                int cw = crossRng.NextDouble() < 0.5 ? 5 : 6;                   // 2.5 or 3 m
                bool alongX = wing.width >= wing.height;                        // the wing's long side
                // where it may go: rooms of a decent size on both sides (not at the very ends), and clear of the lobby and
                // the stair halls — a reserved room across it would join the two sides itself and cut it into a stub
                var spots = new List<int>();
                for (int off = Mathf.Max(MinSide * 2, along / 5) / M * M; off + cw <= along - Mathf.Max(MinSide * 2, along / 5); off += M)
                {
                    var strip = alongX ? new RectInt(wing.xMin + off, wing.yMin, cw, wing.height) : new RectInt(wing.xMin, wing.yMin + off, wing.width, cw);
                    bool clear = f != 0 || !strip.Overlaps(c.Lobby);
                    foreach (var h in c.Halls) if ((h.floor == f || h.floor + 1 == f) && strip.Overlaps(h.hall)) clear = false;
                    if (clear) spots.Add(off);
                }
                if (spots.Count == 0) continue;
                MarkSpine(spine, wing, !alongX, spots[crossRng.Next(spots.Count)], cw);
            }
            // Which sides of a rectangle face circulation (rooms are then cut to keep their door onto it, Treemap)
            c.Front = r => FrontOf(r, owner, regions, nx, nz);

            // Corridor cells → corridor regions (connected pieces).
            var seen = new bool[nx, nz];
            for (int x = 0; x < nx; x++)
                for (int z = 0; z < nz; z++)
                {
                    if (!spine[x, z] || seen[x, z] || owner[x, z] != -1) continue;
                    var reg = new Region { Type = RoomType.Corridor, Shape = RoomShape.Corridor };
                    var q = new Queue<Vector2Int>();
                    q.Enqueue(new Vector2Int(x, z));
                    seen[x, z] = true;
                    int id = regions.Count;
                    while (q.Count > 0)
                    {
                        var p = q.Dequeue();
                        owner[p.x, p.y] = id;
                        reg.Cells.Add(p);
                        foreach (var d in Dirs4)
                        {
                            var n = p + d;
                            if (n.x < 0 || n.y < 0 || n.x >= nx || n.y >= nz || seen[n.x, n.y] || !spine[n.x, n.y] || owner[n.x, n.y] != -1) continue;
                            seen[n.x, n.y] = true;
                            q.Enqueue(n);
                        }
                    }
                    var cb = BoundsOf(reg.Cells);
                    if (reg.Cells.Count < 6 * M * M || Mathf.Min(cb.width, cb.height) < 5) { foreach (var p in reg.Cells) owner[p.x, p.y] = -1; continue; }
                    regions.Add(reg);
                }

            // What is left: rectangles, cut into the program's rooms of this floor (the classic way: into rooms of any size).
            Cut(c, f, Rectangles(owner, nx, nz), Claim, c.Rng(0x5D1, f));

            Cleanup(regions, owner, nx, nz);
            Merge(regions, owner, nx, nz, c.Rng(0x3E6, f));
            // slivers out, pieces apart, closets joined — until it settles (joining can leave a new neck)
            for (int settle = 0; settle < 4; settle++)
            {
                Slim(regions, owner, nx, nz);
                SplitDisconnected(regions, owner, nx, nz);
                Cleanup(regions, owner, nx, nz);
            }
            Slim(regions, owner, nx, nz);
            SplitDisconnected(regions, owner, nx, nz);
            AssignTypes(c, f, regions);
            Atriums(c, f, regions, owner);
            Commit(c, f, regions, owner);
        }

        static readonly Vector2Int[] Dirs4 = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        /// <summary>The plant room's size, cells (4 × 3.5 m: its door needs 3.6 m of wall): a drain stack, the cleanout, the hatch down to the street sewer.</summary>
        const int PlantW = 8, PlantD = 7;

        /// <summary>
        /// The plant room's place in the lobby's corner (cells), where the building's drain leaves for the street: the corner
        /// with the most outside walls (both, at a building's corner; else the facade side). Null when the lobby is too small
        /// to give it up.
        /// </summary>
        static RectInt? UtilityCorner(Ctx c, RectInt lobby)
        {
            if (lobby.width < PlantW + 4 * M || lobby.height < PlantD + 3 * M) return null; // (the rest of the lobby at least 3 m across)
            RectInt? best = null;
            int bestOut = 0;
            foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            {
                var r = new RectInt(sx < 0 ? lobby.xMin : lobby.xMax - PlantW, sz < 0 ? lobby.yMin : lobby.yMax - PlantD, PlantW, PlantD);
                int outside = (SideOutside(c, r, sx, 0) ? 1 : 0) + (SideOutside(c, r, 0, sz) ? 1 : 0);
                // a block of a linked house: its corridors come in at its sides — the plant room only where nothing touches it
                if (c.Linked != null && (!AllOutside(c, r, sx, 0) || !AllOutside(c, r, 0, sz))) continue;
                if (outside > bestOut) { bestOut = outside; best = r; }
            }
            return best;
        }

        /// <summary>
        /// One floor of a linked house: every corridor is a corridor, every block a room (whole when it is inside the room
        /// area limits — the rooms test — otherwise cut into rooms like any wing); no spines, no merging across blocks.
        /// </summary>
        static void ZoneLinked(Ctx c, int f, List<Region> regions, int[,] owner, System.Func<RectInt, Region, int> claim)
        {
            int nx = c.Plan.NX, nz = c.Plan.NZ;
            foreach (var r in c.LinkCorridors) claim(r, new Region { Type = RoomType.Corridor, Shape = RoomShape.Corridor });
            c.Front = r => FrontOf(r, owner, regions, nx, nz);
            // the ground floor: every block holds the rooms it was sized for. The floors above are cut the same way (the same
            // walls, so the same ways between blocks), their rooms of that floor's program put into the ground floor's places
            if (c.Program != null)
            {
                var pool = f == 0 ? null : new List<ProgramRoom>(c.Program.Floors[Mathf.Min(f, c.Program.Floors.Length - 1)]);
                pool?.Sort((a, b) => a.Required != b.Required ? (a.Required ? -1 : 1) : b.Area.CompareTo(a.Area));
                var rng = c.Rng(0x5D2, f);
                for (int k = 0; k < c.LinkBlocks.Count; k++)
                {
                    var rooms = c.Program.Floors[0].FindAll(r => r.Block == k);
                    if (rooms.Count == 0)
                    {
                        // the lobby's block (or a stair hall's on a floor it does not reach) above the ground: one room, as
                        // it is one room below
                        var blk = c.LinkBlocks[k];
                        if (f == 0 || owner[blk.x + blk.width / 2, blk.y + blk.height / 2] != -1) continue;
                        rooms.Add(new ProgramRoom { Type = RoomType.Office, Area = blk.width * blk.height / (float)(M * M), Block = k });
                    }
                    rooms.Sort((a, b) => b.Area.CompareTo(a.Area));
                    if (pool != null)
                        for (int i = 0; i < rooms.Count; i++)
                        {
                            var slot = rooms[i];
                            slot.Type = TakeFitting(pool, slot.Area, c.B, rng, out slot.Required);
                            rooms[i] = slot;
                        }
                    Treemap(c.LinkBlocks[k], rooms, claim, Joins(c, c.LinkBlocks[k]), c.Front?.Invoke(c.LinkBlocks[k]));
                }
            }
            // the floors above (and anything the ground floor left): the blocks hold that floor's rooms
            var rects = new List<RectInt>();
            foreach (var block in c.LinkBlocks)
            {
                // what the lobby / a stair hall / a room left of the block
                var mask = new int[nx, nz];
                for (int x = 0; x < nx; x++)
                    for (int z = 0; z < nz; z++) mask[x, z] = block.Contains(new Vector2Int(x, z)) && owner[x, z] == -1 ? -1 : -2;
                rects.AddRange(Rectangles(mask, nx, nz));
            }
            Cut(c, f, rects, claim, c.Rng(0x5D1, f));
            Cleanup(regions, owner, nx, nz);
            for (int settle = 0; settle < 2; settle++)
            {
                Slim(regions, owner, nx, nz);
                SplitDisconnected(regions, owner, nx, nz);
                Cleanup(regions, owner, nx, nz);
            }
            AssignTypes(c, f, regions);
            Atriums(c, f, regions, owner);
            Commit(c, f, regions, owner);
        }

        /// <summary>
        /// A place of this size on a floor above: the first room of the floor's program it holds (required ones first, then
        /// the biggest), taken out of the pool; none fits: a type drawn by the weights that does, else an office.
        /// </summary>
        static RoomType TakeFitting(List<ProgramRoom> pool, float area, BuildingSettings b, System.Random rng, out bool required)
        {
            required = false;
            for (int i = 0; i < pool.Count; i++)
            {
                if (RoomRules.MinArea(pool[i].Type, b.RoomTypeRules) > area) continue;
                var t = pool[i].Type;
                required = pool[i].Required;
                pool.RemoveAt(i);
                return t;
            }
            for (int tries = 0; tries < 8; tries++)
            {
                var t = RoomProgram.Pick(rng, b);
                if (RoomRules.MinArea(t, b.RoomTypeRules) <= area) return t;
            }
            return RoomType.Office;
        }

        /// <summary>
        /// Free rectangles into rooms: the program's rooms of the floor (<see cref="Fill"/>), or the classic way — split
        /// until room-sized, the types guessed afterwards (<see cref="Split"/>, <see cref="AssignTypes"/>).
        /// </summary>
        static void Cut(Ctx c, int f, List<RectInt> rects, System.Func<RectInt, Region, int> claim, System.Random rng)
        {
            if (c.Program != null) { Fill(c, f, rects, claim, rng); return; }
            foreach (var rect in rects) Split(rect, rng, c.House, c.B, r => claim(r, new Region { Shape = RoomShape.Rect }));
        }

        /// <summary>Width of a wing per corridor (metres): rooms on both sides about 8 m deep.</summary>
        const int ModuleDepth = 19;
        /// <summary>Rooms between a corridor and the facade or the next corridor at least this deep (metres).</summary>
        const int MinRoomDepth = 4;

        /// <summary>A wing at least this wide (metres) gets a corridor with rooms on both sides; narrower, one row of rooms.</summary>
        const int DoubleLoaded = 11;
        /// <summary>A wing narrower than this (metres) has no corridor: its rooms open onto each other.</summary>
        const int SingleLoaded = 8;

        /// <summary>Sides (W, E, S, N) of a rectangle along which at least 2 m touch a corridor, a stair hall or the lobby.</summary>
        static bool[] FrontOf(RectInt r, int[,] owner, List<Region> regions, int nx, int nz)
        {
            bool Circ(int x, int z)
            {
                if (x < 0 || z < 0 || x >= nx || z >= nz) return false;
                int o = owner[x, z];
                return o >= 0 && o < regions.Count && regions[o].Type is RoomType.Corridor or RoomType.Hall or RoomType.Lobby;
            }
            int w = 0, e = 0, s = 0, n = 0;
            for (int z = r.yMin; z < r.yMax; z++) { if (Circ(r.xMin - 1, z)) w++; if (Circ(r.xMax, z)) e++; }
            for (int x = r.xMin; x < r.xMax; x++) { if (Circ(x, r.yMin - 1)) s++; if (Circ(x, r.yMax)) n++; }
            return new[] { w >= 2 * M, e >= 2 * M, s >= 2 * M, n >= 2 * M };
        }

        static void MarkSpine(bool[,] spine, RectInt wing, bool alongX, int off, int width)
        {
            for (int i = 0; i < (alongX ? wing.width : wing.height); i++)
                for (int k = 0; k < width; k++)
                {
                    int x = alongX ? wing.xMin + i : wing.xMin + off + k;
                    int z = alongX ? wing.yMin + off + k : wing.yMin + i;
                    if (x >= 0 && z >= 0 && x < spine.GetLength(0) && z < spine.GetLength(1)) spine[x, z] = true;
                }
        }

        /// <summary>Free cells as rectangles (greedy: widest run, then as tall as it stays free).</summary>
        static List<RectInt> Rectangles(int[,] owner, int nx, int nz)
        {
            var used = new bool[nx, nz];
            var list = new List<RectInt>();
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    if (owner[x, z] != -1 || used[x, z]) continue;
                    int w = 0;
                    while (x + w < nx && owner[x + w, z] == -1 && !used[x + w, z]) w++;
                    int h = 1;
                    while (z + h < nz)
                    {
                        bool row = true;
                        for (int i = 0; i < w && row; i++) row = owner[x + i, z + h] == -1 && !used[x + i, z + h];
                        if (!row) break;
                        h++;
                    }
                    for (int i = 0; i < w; i++)
                        for (int j = 0; j < h; j++) used[x + i, z + j] = true;
                    list.Add(new RectInt(x, z, w, h));
                }
            return list;
        }

        /// <summary>Splits a rectangle in two until the pieces are room-sized; sizes of every kind, from closets to halls.</summary>
        static void Split(RectInt r, System.Random rng, int house, BuildingSettings b, System.Action<RectInt> room)
        {
            float scale = b.RoomSizeScale;
            float area = r.width * r.height / (float)(M * M);
            double roll = rng.NextDouble();
            float want = roll < 0.28 ? 9f + (float)rng.NextDouble() * 10f        // small: closets, offices, toilets
                       : roll < 0.7 ? 20f + (float)rng.NextDouble() * 25f        // medium
                       : 45f + (float)rng.NextDouble() * (60f + house * 8f);    // large: open spaces, halls
            want *= scale;
            // room area limits (the rooms test: 30..200 m²): aim inside them, never cut into pieces below the smallest
            if (b.RoomAreaMax > 0f) want = Mathf.Min(want, b.RoomAreaMax / 1.3f);
            if (b.RoomAreaMin > 0f) want = Mathf.Max(want, b.RoomAreaMin * 1.4f);
            bool canX = r.width >= 2 * MinSide, canZ = r.height >= 2 * MinSide;
            if (b.RoomAreaMin > 0f && area < b.RoomAreaMin * 2f && (b.RoomAreaMax <= 0f || area <= b.RoomAreaMax)) { room(r); return; }
            if (area <= want * 1.25f || (!canX && !canZ)) { room(r); return; }
            bool cutX = canX && (!canZ || (r.width > r.height * 1.25f || (r.height <= r.width * 1.25f && rng.Next(2) == 0)));
            int side = cutX ? r.width : r.height;
            float k = 0.3f + (float)rng.NextDouble() * 0.4f;
            int at = Mathf.Clamp(Mathf.RoundToInt(side * k / M) * M, MinSide, side - MinSide);
            if (cutX)
            {
                Split(new RectInt(r.x, r.y, at, r.height), rng, house, b, room);
                Split(new RectInt(r.x + at, r.y, r.width - at, r.height), rng, house, b, room);
            }
            else
            {
                Split(new RectInt(r.x, r.y, r.width, at), rng, house, b, room);
                Split(new RectInt(r.x, r.y + at, r.width, r.height - at), rng, house, b, room);
            }
        }

        /// <summary>Shared border length (cells) between every pair of regions.</summary>
        static Dictionary<(int, int), int> Borders(int[,] owner, int nx, int nz)
        {
            var d = new Dictionary<(int, int), int>();
            void Add(int a, int b)
            {
                if (a < 0 || b < 0 || a == b) return;
                var key = a < b ? (a, b) : (b, a);
                d[key] = d.TryGetValue(key, out var n) ? n + 1 : 1;
            }
            for (int x = 0; x < nx; x++)
                for (int z = 0; z < nz; z++)
                {
                    if (x + 1 < nx) Add(owner[x, z], owner[x + 1, z]);
                    if (z + 1 < nz) Add(owner[x, z], owner[x, z + 1]);
                }
            return d;
        }

        static void Absorb(List<Region> regions, int[,] owner, int from, int into)
        {
            foreach (var p in regions[from].Cells) owner[p.x, p.y] = into;
            regions[into].Cells.AddRange(regions[from].Cells);
            regions[into].Rects.AddRange(regions[from].Rects);
            regions[from].Cells.Clear();
            regions[from].Rects.Clear();
        }

        /// <summary>Slivers and closets too small to use join the neighbour they share most wall with.</summary>
        static void Cleanup(List<Region> regions, int[,] owner, int nx, int nz)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                var borders = Borders(owner, nx, nz);
                bool changed = false;
                for (int i = 0; i < regions.Count; i++)
                {
                    var r = regions[i];
                    if (r.Fixed || r.Cells.Count == 0) continue;
                    var b = BoundsOf(r.Cells);
                    bool tiny = r.Cells.Count < 7 * M * M || Mathf.Min(b.width, b.height) < 5 /* 2.5 m */ && r.Type != RoomType.Corridor;
                    if (!tiny) continue;
                    int best = -1, len = 0;
                    foreach (var kv in borders)
                    {
                        int other = kv.Key.Item1 == i ? kv.Key.Item2 : kv.Key.Item2 == i ? kv.Key.Item1 : -1;
                        if (other < 0 || regions[other].Fixed || regions[other].Cells.Count == 0) continue;
                        if (kv.Value > len) { len = kv.Value; best = other; }
                    }
                    if (best < 0) continue;
                    Absorb(regions, owner, i, best);
                    changed = true;
                }
                if (!changed) break;
            }
        }

        /// <summary>
        /// No sliver of a room narrower than 2 m: a cell whose room runs less than 4 cells through it both across and
        /// along (a strip left between a corridor and a facade, the arm of a merged room) goes to the neighbour it touches
        /// most. Repeated until nothing moves; reserved rooms (lobby, stair halls) never give or take cells.
        /// </summary>
        static void Slim(List<Region> regions, int[,] owner, int nx, int nz)
        {
            var movedOnce = new HashSet<Vector2Int>();
            int Run(int x, int z, int id, int dx, int dz, int limit)
            {
                int n = 1;
                for (int k = 1; k < limit; k++) { int px = x + dx * k, pz = z + dz * k; if (px < 0 || pz < 0 || px >= nx || pz >= nz || owner[px, pz] != id) break; n++; }
                for (int k = 1; k < limit; k++) { int px = x - dx * k, pz = z - dz * k; if (px < 0 || pz < 0 || px >= nx || pz >= nz || owner[px, pz] != id) break; n++; }
                return n;
            }
            for (int pass = 0; pass < 12; pass++)
            {
                bool moved = false;
                for (int x = 0; x < nx; x++)
                    for (int z = 0; z < nz; z++)
                    {
                        int id = owner[x, z];
                        if (id < 0 || regions[id].Fixed) continue;
                        var cell = new Vector2Int(x, z);
                        if (movedOnce.Contains(cell)) continue;
                        int min = regions[id].Type == RoomType.Corridor ? 5 : 4;
                        if (Run(x, z, id, 1, 0, min) >= min && Run(x, z, id, 0, 1, min) >= min) continue;
                        // the neighbour this cell would be widest in (as if it were already theirs)
                        int best = -1, bestScore = -1;
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dz = -1; dz <= 1; dz++)
                            {
                                int px = x + dx, pz = z + dz;
                                if (px < 0 || pz < 0 || px >= nx || pz >= nz) continue;
                                int o = owner[px, pz];
                                if (o < 0 || o == id || regions[o].Fixed) continue;
                                owner[x, z] = o;
                                int score = Mathf.Min(Run(x, z, o, 1, 0, 8), Run(x, z, o, 0, 1, 8));
                                owner[x, z] = id;
                                if (score > bestScore) { bestScore = score; best = o; }
                            }
                        if (best < 0) continue;
                        regions[id].Cells.Remove(cell);
                        regions[best].Cells.Add(cell);
                        owner[x, z] = best;
                        movedOnce.Add(cell);
                        moved = true;
                    }
                if (!moved) break;
            }
        }

        /// <summary>A region that slimming or merging left in several pieces becomes one region per piece.</summary>
        static void SplitDisconnected(List<Region> regions, int[,] owner, int nx, int nz)
        {
            int count = regions.Count;
            for (int i = 0; i < count; i++)
            {
                var r = regions[i];
                if (r.Cells.Count == 0) continue;
                var set = new HashSet<Vector2Int>(r.Cells);
                var pieces = new List<List<Vector2Int>>();
                while (set.Count > 0)
                {
                    var start = default(Vector2Int);
                    foreach (var p in set) { start = p; break; }
                    var piece = new List<Vector2Int>();
                    var q = new Queue<Vector2Int>();
                    q.Enqueue(start);
                    set.Remove(start);
                    while (q.Count > 0)
                    {
                        var p = q.Dequeue();
                        piece.Add(p);
                        foreach (var d in Dirs4)
                            if (set.Remove(p + d)) q.Enqueue(p + d);
                    }
                    pieces.Add(piece);
                }
                if (pieces.Count < 2) continue;
                pieces.Sort((a, b) => b.Count.CompareTo(a.Count));
                r.Cells.Clear();
                r.Cells.AddRange(pieces[0]);
                for (int k = 1; k < pieces.Count; k++)
                {
                    var extra = new Region { Type = r.Type, Shape = r.Shape };
                    extra.Cells.AddRange(pieces[k]);
                    extra.Rects.Add(BoundsOf(pieces[k]));
                    foreach (var p in pieces[k]) owner[p.x, p.y] = regions.Count;
                    regions.Add(extra);
                }
            }
        }

        /// <summary>Some neighbouring rooms merge into L, T and U shapes.</summary>
        static void Merge(List<Region> regions, int[,] owner, int nx, int nz, System.Random rng)
        {
            var borders = Borders(owner, nx, nz);
            var keys = new List<(int, int)>(borders.Keys);
            keys.Sort();
            foreach (var (a, b) in keys)
            {
                var ra = regions[a];
                var rb = regions[b];
                if (ra.Cells.Count == 0 || rb.Cells.Count == 0 || ra.Fixed || rb.Fixed) continue;
                if (ra.Type == RoomType.Corridor || rb.Type == RoomType.Corridor) continue;
                if (rb.Typed) continue; // a room of the program is never swallowed (a scrap may join one)
                float area = (ra.Cells.Count + rb.Cells.Count) / (float)(M * M);
                if (area > 130f || borders[(a, b)] < 3 * M || rng.NextDouble() > 0.28) continue;
                Absorb(regions, owner, b, a);
            }
        }

        static RectInt BoundsOf(List<Vector2Int> cells)
        {
            int x0 = int.MaxValue, z0 = int.MaxValue, x1 = int.MinValue, z1 = int.MinValue;
            foreach (var p in cells) { x0 = Mathf.Min(x0, p.x); z0 = Mathf.Min(z0, p.y); x1 = Mathf.Max(x1, p.x); z1 = Mathf.Max(z1, p.y); }
            return new RectInt(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
        }

        /// <summary>Room types by size and shape: long narrow rooms make server rooms and storage, small ones toilets and offices, big ones open spaces and lounges.</summary>
        static void AssignTypes(Ctx c, int f, List<Region> regions)
        {
            var rng = c.Rng(0x7E9, f);
            foreach (var r in regions)
            {
                if (r.Cells.Count == 0 || r.Fixed || r.Type == RoomType.Corridor) continue;
                if (r.Typed) { r.Shape = r.Cells.Count == BoundsOf(r.Cells).width * BoundsOf(r.Cells).height ? RoomShape.Rect : r.Rects.Count >= 3 ? RoomShape.U : RoomShape.L; continue; }
                var b = BoundsOf(r.Cells);
                float area = r.Cells.Count / (float)(M * M);
                float aspect = Mathf.Max(b.width, b.height) / (float)Mathf.Max(1, Mathf.Min(b.width, b.height));
                int total = 0;
                var weights = new List<(RoomType, int)>();
                float outside = -1f;
                foreach (var w0 in c.B.RoomWeights)
                {
                    // the locked storage room is chosen on its own (StorageRooms): the back rooms its weight stood for are offices
                    var w = w0.Room == RoomType.Storage ? new RoomWeight(RoomType.Office, w0.Weight) : w0;
                    var rule = RoomRules.For(w.Room, c.B.RoomTypeRules);
                    if (rule.OutsideWall > 0f && outside < 0f) outside = OutsideWall(c, r);
                    if (!RoomRules.Fits(rule, area, aspect, Mathf.Max(0f, outside), r.Cells.Count == b.width * b.height)) continue;
                    float fit = Fit(w.Room, area, aspect);
                    int k = w.Weight > 0 && fit > 0f ? Mathf.Max(1, Mathf.RoundToInt(w.Weight * fit * 10f)) : 0;
                    if (k > 0) { weights.Add((w.Room, k)); total += k; }
                }
                if (total == 0) { r.Type = area > 40f ? RoomType.OpenSpace : RoomType.Office; continue; }
                int roll = rng.Next(total);
                foreach (var (t, k) in weights)
                {
                    if (roll < k) { r.Type = t; break; }
                    roll -= k;
                }
                bool rect = r.Cells.Count == b.width * b.height;
                r.Shape = rect ? RoomShape.Rect : r.Rects.Count >= 3 ? RoomShape.U : RoomShape.L;
            }
        }

        /// <summary>How much outside wall a region has, metres (cell edges on the building's outline: where its windows go).</summary>
        static float OutsideWall(Ctx c, Region r)
        {
            var own = new HashSet<Vector2Int>(r.Cells);
            int edges = 0;
            foreach (var p in r.Cells)
                foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    var q = p + d;
                    if (own.Contains(q)) continue;
                    if (q.x < 0 || q.y < 0 || q.x >= c.Plan.NX || q.y >= c.Plan.NZ || !c.Footprint[q.x, q.y]) edges++;
                }
            return edges * HousePlan.Res;
        }

        static float Fit(RoomType t, float area, float aspect) => t switch
        {
            RoomType.Restroom => area < 26f ? 1.6f : area < 45f ? 0.4f : 0f,
            RoomType.Office => area < 40f ? 1.3f : 0.3f,
            RoomType.Meeting => area is > 14f and < 70f ? 1.2f : 0.2f,
            RoomType.Kitchen => area is > 14f and < 70f ? 1f : 0.2f,
            RoomType.Server => (aspect > 1.7f ? 1.8f : 0.7f) * (area < 90f ? 1f : 0.4f),
            RoomType.Storage => aspect > 1.6f ? 1.4f : 0.9f,
            RoomType.OpenSpace => area > 45f ? 2f : area > 28f ? 0.6f : 0f,
            RoomType.Lounge => area > 30f ? 1.3f : 0.3f,
            RoomType.Gym => area is > 22f and < 80f ? 1.3f : 0.15f,
            RoomType.Library => area > 22f ? 1.2f : 0.2f,
            RoomType.CopyRoom => area < 26f ? 1.5f : area < 40f ? 0.4f : 0f,
            RoomType.ControlRoom => area is > 24f and < 60f ? 1.1f : 0f,
            RoomType.Lab => area is > 16f and < 70f ? 1.1f : 0.2f,
            RoomType.NapRoom => area is > 14f and < 36f ? 1.3f : 0f,
            RoomType.Studio => area is > 16f and < 40f ? 1.2f : 0f,
            RoomType.LockerRoom => (area is > 16f and < 50f ? 1.1f : 0f) * (aspect > 1.4f ? 1.4f : 1f),
            RoomType.Infirmary => area is > 18f and < 50f ? 1.1f : 0f,
            RoomType.GameRoom => area is > 26f and < 100f ? 1.3f : 0.1f,
            _ => 1f,
        };

        /// <summary>
        /// Some big rooms rise through two (rarely three) storeys: the floors above get a void over them, galleries with
        /// railings look down into them. Only where the floor above stays connected around the void.
        /// </summary>
        static void Atriums(Ctx c, int f, List<Region> regions, int[,] owner)
        {
            if (f >= c.Floors - 1) return;
            if (c.Linked != null) return; // a linked house: every block may be the way between others upstairs — none becomes a hole
            var rng = c.Rng(0xA7E, f);
            var plan = c.Plan;
            int made = 0;
            var order = new List<int>();
            for (int i = 0; i < regions.Count; i++) order.Add(i);
            order.Sort((a, b) => regions[b].Cells.Count.CompareTo(regions[a].Cells.Count));
            foreach (int i in order)
            {
                var r = regions[i];
                float area = r.Cells.Count / (float)(M * M);
                bool kind = r.Type is RoomType.Lobby or RoomType.OpenSpace or RoomType.Lounge or RoomType.Meeting
                    or RoomType.Library or RoomType.GameRoom or RoomType.Gym;
                if (!kind || r.Hall >= 0 || area < 45f || area > 260f || made >= 1 + c.House / 3) continue;
                if (rng.NextDouble() > c.B.AtriumChance) continue;
                int levels = f + 2 < c.Floors && rng.NextDouble() < 0.3 ? 3 : 2;
                // no stair hall above, and the floors above stay in one piece around the void
                bool ok = true;
                for (int up = 1; up < levels && ok; up++)
                {
                    foreach (var h in c.Halls)
                        if ((h.floor == f + up || h.floor + 1 == f + up))
                            foreach (var p in r.Cells) if (h.hall.Contains(p)) { ok = false; break; }
                    if (ok) ok = StaysConnected(c, f + up, r.Cells);
                }
                if (!ok) continue;
                r.Levels = levels;
                made++;
            }
        }

        static bool StaysConnected(Ctx c, int f, List<Vector2Int> hole)
        {
            var plan = c.Plan;
            int nx = plan.NX, nz = plan.NZ;
            var blocked = new bool[nx, nz];
            foreach (var p in hole) blocked[p.x, p.y] = true;
            int total = 0;
            Vector2Int start = new(-1, -1);
            for (int x = 0; x < nx; x++)
                for (int z = 0; z < nz; z++)
                    if (c.Footprint[x, z] && !blocked[x, z] && plan.KindAt(f, x, z) == CellKind.Outside)
                    {
                        total++;
                        if (start.x < 0) start = new Vector2Int(x, z);
                    }
            if (total == 0) return false;
            var seen = new bool[nx, nz];
            var q = new Queue<Vector2Int>();
            q.Enqueue(start);
            seen[start.x, start.y] = true;
            int n = 0;
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                n++;
                foreach (var d in Dirs4)
                {
                    var o = p + d;
                    if (o.x < 0 || o.y < 0 || o.x >= nx || o.y >= nz || seen[o.x, o.y] || blocked[o.x, o.y]) continue;
                    if (!c.Footprint[o.x, o.y] || plan.KindAt(f, o.x, o.y) != CellKind.Outside) continue;
                    seen[o.x, o.y] = true;
                    q.Enqueue(o);
                }
            }
            return n == total && total > hole.Count / 2;
        }

        /// <summary>Writes the regions into the plan as rooms, with shaped corners; voids over atriums on the floors above.</summary>
        static void Commit(Ctx c, int f, List<Region> regions, int[,] owner)
        {
            var plan = c.Plan;
            var shapeRng = c.Rng(0x5A9, f);
            for (int i = 0; i < regions.Count; i++)
            {
                var reg = regions[i];
                if (reg.Cells.Count == 0) continue;
                var room = new PlanRoom { Id = plan.Rooms.Count, Floor = f, Type = reg.Type, Shape = reg.Shape, Levels = reg.Levels };
                room.Rects.AddRange(reg.Rects);
                plan.Rooms.Add(room);
                for (int lv = 0; lv < reg.Levels; lv++)
                    foreach (var p in reg.Cells) plan.Set(f + lv, p.x, p.y, lv == 0 ? CellKind.Room : CellKind.Void, room.Id);
                if (reg.Type != RoomType.Lobby) Shape(c, room, reg, shapeRng);
                Summarize(plan, room);
            }
        }

        /// <summary>
        /// Which rooms of the floors above an atrium look down into it over a railing (a gallery) and which wall it off:
        /// corridors, halls, lounges and open spaces mostly open up.
        /// </summary>
        static void Galleries(Ctx c)
        {
            var plan = c.Plan;
            var rng = c.Rng(0x6A1);
            foreach (var r in plan.Rooms)
                for (int lv = 1; lv < r.Levels; lv++)
                    foreach (var o in plan.Rooms)
                    {
                        if (o.Floor != r.Floor + lv) continue;
                        double chance = o.Type is RoomType.Corridor or RoomType.Hall or RoomType.Lounge or RoomType.OpenSpace ? 0.85 : 0.35;
                        c.Galleries[(o.Floor, r.Id, o.Id)] = rng.NextDouble() < chance;
                    }
        }

        static void SortStairRooms(Ctx c)
        {
            // Rebuild from the halls: for each hall, the room containing its rectangle on its floor and on the floor above.
            var list = new List<int>();
            foreach (var (floor, hall, _) in c.Halls)
            {
                var p = new Vector2Int(hall.xMin + 1, hall.yMin + 1);
                list.Add(c.Plan.KindAt(floor, p.x, p.y) == CellKind.Room ? c.Plan.IdAt(floor, p.x, p.y) : -1);
                list.Add(c.Plan.KindAt(floor + 1, p.x, p.y) == CellKind.Room ? c.Plan.IdAt(floor + 1, p.x, p.y) : -1);
            }
            c.StairRooms.Clear();
            c.StairRooms.AddRange(list);
        }

        // ------------------------------------------------------------------ shaped corners

        /// <summary>
        /// Cuts or rounds some convex corners of a room; near-square rooms may become a rotunda or an octagon (all four
        /// corners, leaving straight middles for doorways). The space behind a shaped wall becomes a sealed pocket.
        /// Never the corner of a staircase or the lobby's hatch.
        /// </summary>
        static void Shape(Ctx c, PlanRoom room, Region reg, System.Random rng)
        {
            if (reg.Type is RoomType.Corridor or RoomType.Utility) return;
            // a cut corner or a rotunda takes up to a fifth of the floor: never below the type's smallest size
            if (reg.Cells.Count / (float)(M * M) * 0.78f < RoomRules.MinArea(reg.Type, c.B.RoomTypeRules)) return;
            var b = BoundsOf(reg.Cells);
            bool rect = reg.Cells.Count == b.width * b.height;
            float minSide = Mathf.Min(b.width, b.height) / (float)M;
            double roll = rng.NextDouble();
            double rotundaChance = reg.Type is RoomType.Meeting or RoomType.Lobby or RoomType.Lounge or RoomType.Hall ? c.B.RotundaChance * 2.0 : c.B.RotundaChance;
            if (rect && reg.Hall < 0 && minSide >= 7f && Mathf.Max(b.width, b.height) <= Mathf.Min(b.width, b.height) * 1.3f && roll < rotundaChance)
            {
                bool round = rng.Next(2) == 0;
                float cut = (minSide - 3.2f) / 2f;
                foreach (var corner in Corners(b))
                    CutCorner(c, room, corner.p, corner.e1, corner.e2, cut, round);
                room.Shape = round ? RoomShape.Rotunda : RoomShape.Octagon;
                return;
            }
            if (roll > c.B.CornerCutChance + rotundaChance || reg.Cells.Count < 20 * M * M) return;
            foreach (var corner in ConvexCorners(c.Plan, room.Floor, room.Id, reg.Cells))
            {
                if (rng.NextDouble() > 0.5) continue;
                float cut = Mathf.Min(1.5f + (float)rng.NextDouble() * 2.5f, minSide * 0.4f);
                if (cut < 1f) continue;
                CutCorner(c, room, corner.p, corner.e1, corner.e2, cut, rng.Next(3) == 0);
            }
        }

        struct Corner { public Vector2Int p; public Vector2Int e1, e2; }

        static IEnumerable<Corner> Corners(RectInt b)
        {
            yield return new Corner { p = new Vector2Int(b.xMin, b.yMin), e1 = Vector2Int.right, e2 = Vector2Int.up };
            yield return new Corner { p = new Vector2Int(b.xMax, b.yMin), e1 = Vector2Int.left, e2 = Vector2Int.up };
            yield return new Corner { p = new Vector2Int(b.xMin, b.yMax), e1 = Vector2Int.right, e2 = Vector2Int.down };
            yield return new Corner { p = new Vector2Int(b.xMax, b.yMax), e1 = Vector2Int.left, e2 = Vector2Int.down };
        }

        /// <summary>Grid points where only one of the four surrounding cells is the room: its convex corners.</summary>
        static List<Corner> ConvexCorners(HousePlan plan, int f, int id, List<Vector2Int> cells)
        {
            var list = new List<Corner>();
            var set = new HashSet<Vector2Int>(cells);
            foreach (var p in cells)
                for (int sx = 0; sx <= 1; sx++)
                    for (int sz = 0; sz <= 1; sz++)
                    {
                        var g = new Vector2Int(p.x + sx, p.y + sz); // grid point at a corner of cell p
                        int inside = 0;
                        for (int dx = -1; dx <= 0; dx++)
                            for (int dz = -1; dz <= 0; dz++) if (set.Contains(new Vector2Int(g.x + dx, g.y + dz))) inside++;
                        if (inside != 1) continue;
                        var e1 = new Vector2Int(sx == 0 ? 1 : -1, 0);
                        var e2 = new Vector2Int(0, sz == 0 ? 1 : -1);
                        list.Add(new Corner { p = g, e1 = e1, e2 = e2 });
                    }
            return list;
        }

        static void CutCorner(Ctx c, PlanRoom room, Vector2Int p, Vector2Int e1, Vector2Int e2, float cut, bool round)
        {
            var plan = c.Plan;
            int n = Mathf.CeilToInt(cut * M);
            // The whole corner square must be this room (on every storey it spans) and free of stairs and the hatch.
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    var cell = p + e1 * i + e2 * j + new Vector2Int(e1.x < 0 ? -1 : 0, e2.y < 0 ? -1 : 0);
                    if (plan.KindAt(room.Floor, cell.x, cell.y) != CellKind.Room || plan.IdAt(room.Floor, cell.x, cell.y) != room.Id) return;
                }
            var origin = new Vector2(p.x / (float)M, p.y / (float)M);
            var u = new Vector2(e1.x, e1.y);
            var v = new Vector2(e2.x, e2.y);
            var square = Rect.MinMaxRect(Mathf.Min(origin.x, origin.x + (u.x + v.x) * cut), Mathf.Min(origin.y, origin.y + (u.y + v.y) * cut),
                                         Mathf.Max(origin.x, origin.x + (u.x + v.x) * cut), Mathf.Max(origin.y, origin.y + (u.y + v.y) * cut));
            foreach (var h in c.Halls)
            {
                if (h.floor != room.Floor && h.floor + 1 != room.Floor) continue;
                var hr = new Rect(h.hall.x / (float)M, h.hall.y / (float)M, h.hall.width / (float)M, h.hall.height / (float)M);
                if (hr.Overlaps(square)) return;
            }
            if (room.Floor == 0)
            {
                var lobby = new Rect(c.Lobby.x / (float)M, c.Lobby.y / (float)M, c.Lobby.width / (float)M, c.Lobby.height / (float)M);
                var hatch = new Rect(lobby.center - Vector2.one * 1.5f, Vector2.one * 3f);
                if (hatch.Overlaps(square)) return;
            }
            // Pocket: cells whose centre is behind the shaped wall (with a margin, so the room's cells are all usable).
            const float margin = 0.3f;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    var cell = p + e1 * i + e2 * j + new Vector2Int(e1.x < 0 ? -1 : 0, e2.y < 0 ? -1 : 0);
                    var centre = HousePlan.CellCenter(cell.x, cell.y) - origin;
                    float a = Vector2.Dot(centre, u), b = Vector2.Dot(centre, v);
                    bool behind = round
                        ? (a - cut) * (a - cut) + (b - cut) * (b - cut) > (cut - margin) * (cut - margin) && a < cut && b < cut
                        : a + b < cut + margin * 1.41f;
                    if (!behind) continue;
                    for (int lv = 0; lv < room.Levels; lv++) plan.Set(room.Floor + lv, cell.x, cell.y, CellKind.Pocket, room.Id);
                }
            if (!round) { room.Shaped.Add((origin + u * cut, origin + v * cut)); return; }
            var centreArc = origin + (u + v) * cut;
            int steps = Mathf.Max(3, Mathf.CeilToInt(cut * Mathf.PI / 2f / 0.8f));
            Vector2 Point(int k)
            {
                float t = Mathf.PI + k / (float)steps * Mathf.PI / 2f;
                return centreArc + cut * (Mathf.Cos(t) * u + Mathf.Sin(t) * v);
            }
            for (int k = 0; k < steps; k++) room.Shaped.Add((Point(k), Point(k + 1)));
        }

        /// <summary>Area, bounds and a centre point (the room cell furthest from its walls).</summary>
        public static void Summarize(HousePlan plan, PlanRoom room, List<(int floor, Rect area)> avoid = null)
        {
            int f = room.Floor;
            var cells = new List<Vector2Int>();
            int x0 = int.MaxValue, z0 = int.MaxValue, x1 = int.MinValue, z1 = int.MinValue;
            for (int x = 0; x < plan.NX; x++)
                for (int z = 0; z < plan.NZ; z++)
                    if (plan.KindAt(f, x, z) == CellKind.Room && plan.IdAt(f, x, z) == room.Id)
                    {
                        cells.Add(new Vector2Int(x, z));
                        x0 = Mathf.Min(x0, x); z0 = Mathf.Min(z0, z); x1 = Mathf.Max(x1, x); z1 = Mathf.Max(z1, z);
                    }
            room.Area = cells.Count * HousePlan.Res * HousePlan.Res;
            if (cells.Count == 0) return;
            room.Bounds = Rect.MinMaxRect(x0 * HousePlan.Res, z0 * HousePlan.Res, (x1 + 1) * HousePlan.Res, (z1 + 1) * HousePlan.Res);
            // distance to the nearest non-room cell (BFS from the room's border)
            var dist = new Dictionary<Vector2Int, int>();
            var q = new Queue<Vector2Int>();
            var set = new HashSet<Vector2Int>(cells);
            foreach (var p in cells)
                foreach (var d in Dirs4)
                    if (!set.Contains(p + d)) { dist[p] = 0; q.Enqueue(p); break; }
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                foreach (var d in Dirs4)
                {
                    var n = p + d;
                    if (!set.Contains(n) || dist.ContainsKey(n)) continue;
                    dist[n] = dist[p] + 1;
                    q.Enqueue(n);
                }
            }
            var mid = new Vector2((x0 + x1) / 2f, (z0 + z1) / 2f);
            Vector2Int best = cells[0];
            float score = float.MinValue;
            foreach (var p in cells)
            {
                if (avoid != null)
                {
                    var cp = HousePlan.CellCenter(p.x, p.y);
                    bool skip = false;
                    foreach (var (floor, area) in avoid) if (floor == f && area.Contains(cp)) skip = true;
                    if (skip) continue;
                }
                float s = dist[p] * 4f - (p - mid).magnitude;
                if (s > score) { score = s; best = p; }
            }
            var cc = HousePlan.CellCenter(best.x, best.y);
            room.Center = new Vector3(cc.x, LevelLayout.FloorY(f), cc.y);
        }

        /// <summary>Gives a room its square corners back (its pockets become its floor again).</summary>
        static void Unshape(HousePlan plan, PlanRoom room)
        {
            for (int lv = 0; lv < room.Levels; lv++)
                for (int x = 0; x < plan.NX; x++)
                    for (int z = 0; z < plan.NZ; z++)
                        if (plan.KindAt(room.Floor + lv, x, z) == CellKind.Pocket && plan.IdAt(room.Floor + lv, x, z) == room.Id)
                            plan.Set(room.Floor + lv, x, z, lv == 0 ? CellKind.Room : CellKind.Void, room.Id);
            room.Shaped.Clear();
            if (room.Shape is RoomShape.Rotunda or RoomShape.Octagon) room.Shape = RoomShape.Rect;
            Summarize(plan, room);
        }

        /// <summary>Rooms no doorway, open passage or staircase leads to from the lobby.</summary>
        static List<int> Unreached(HousePlan plan, Output o)
        {
            var adj = new Dictionary<int, List<int>>();
            void Link(int a, int b)
            {
                if (a < 0 || b < 0) return;
                if (!adj.TryGetValue(a, out var la)) adj[a] = la = new List<int>();
                if (!adj.TryGetValue(b, out var lb)) adj[b] = lb = new List<int>();
                la.Add(b);
                lb.Add(a);
            }
            foreach (var op in plan.Openings)
                if (op.Kind is EdgeType.Door or EdgeType.Open) Link(plan.Walls[op.Wall].RoomA, plan.Walls[op.Wall].RoomB);
            foreach (var s in o.Stairs) Link(s.Room, s.RoomAbove);
            var seen = new HashSet<int> { o.Lobby };
            var q = new Queue<int>();
            q.Enqueue(o.Lobby);
            while (q.Count > 0)
            {
                int r = q.Dequeue();
                if (!adj.TryGetValue(r, out var list)) continue;
                foreach (int n in list) if (seen.Add(n)) q.Enqueue(n);
            }
            var lost = new List<int>();
            foreach (var r in plan.Rooms) if (!seen.Contains(r.Id)) lost.Add(r.Id);
            return lost;
        }

        // ------------------------------------------------------------------ openings

        /// <summary>
        /// Doorways: per floor a random spanning tree over the rooms (from the lobby or the stair hall arriving from
        /// below) — corridors first — so every room is reachable; extra doors make loops; some passages are open; vents
        /// and slots in some walls still closed. Exterior doors on the ground floor. Doorways keep clear of stairs.
        /// </summary>
        static void Openings(Ctx c, Output o)
        {
            var plan = c.Plan;
            var b = c.B;
            var stairZones = new List<(int floor, Rect area)>();
            foreach (var s in o.Stairs)
            {
                var area = StairArea(s, 0.35f); // a doorway beside the stair is fine, not one onto its flights
                stairZones.Add((s.Floor, area));
                stairZones.Add((s.Floor + 1, area));
            }

            for (int f = 0; f < plan.Floors; f++)
            {
                var rng = c.Rng(0xD00, f);
                // interior walls per pair of rooms
                var pairs = new Dictionary<(int, int), List<int>>();
                for (int i = 0; i < plan.Walls.Count; i++)
                {
                    var w = plan.Walls[i];
                    if (w.Floor != f || w.RoomA < 0 || w.RoomB < 0 || w.Railing || w.Shaped) continue;
                    var key = w.RoomA < w.RoomB ? (w.RoomA, w.RoomB) : (w.RoomB, w.RoomA);
                    if (!pairs.TryGetValue(key, out var list)) pairs[key] = list = new List<int>();
                    list.Add(i);
                }
                var rooms = new List<int>();
                foreach (var r in plan.Rooms) if (r.Floor == f) rooms.Add(r.Id);
                if (rooms.Count == 0) continue;
                int start = f == 0 ? o.Lobby : StairRoomAbove(o, f) ?? rooms[0];
                var reached = new HashSet<int> { start };
                var done = new HashSet<(int, int)>();

                bool TryOpen((int, int) key, EdgeType kind)
                {
                    var walls = new List<int>(pairs[key]);
                    walls.Sort((p, q) => plan.Walls[q].Length.CompareTo(plan.Walls[p].Length));
                    float width = kind == EdgeType.Vent ? VentWidth : kind == EdgeType.Squeeze ? SqueezeWidth : DoorWidth;
                    foreach (int wi in walls)
                    {
                        var w = plan.Walls[wi];
                        if (kind == EdgeType.Open)
                        {
                            if (w.Length > 12f || w.Length < 1.8f || NearStair(stairZones, f, w, 0f, w.Length) || plan.OpeningsOf(wi).Count > 0) continue;
                            plan.Openings.Add(new PlanOpening { Wall = wi, Kind = EdgeType.Open, At = w.Length / 2f, Width = w.Length });
                            return true;
                        }
                        // a few candidate spots, the middle first
                        for (int k = 0; k < 7; k++)
                        {
                            float t = k == 0 ? 0.5f : (float)rng.NextDouble();
                            float at = Mathf.Lerp(width / 2f + DoorMargin, w.Length - width / 2f - DoorMargin, t);
                            at = Mathf.Round(at * 2f) / 2f;
                            if (!plan.FreeOn(wi, at, width, DoorMargin)) continue;
                            if (NearStair(stairZones, f, w, at - width / 2f - 0.3f, at + width / 2f + 0.3f)) continue;
                            plan.Openings.Add(new PlanOpening { Wall = wi, Kind = kind, At = at, Width = width });
                            return true;
                        }
                    }
                    return false;
                }

                // Connect the floor: every pair of rooms that can share a doorway, in random order (corridors first), joins
                // two groups of rooms if they are still apart (Kruskal: no loops yet, every room in reach that can be).
                var groupOf = new Dictionary<int, int>();
                foreach (int r in rooms) groupOf[r] = r;
                int Find(int r) { while (groupOf[r] != r) r = groupOf[r] = groupOf[groupOf[r]]; return r; }
                var order = new List<((int, int) key, float weight)>();
                foreach (var kv in pairs)
                {
                    var (a, bb) = kv.Key;
                    // circulation first (a room that touches a corridor, a hall or the lobby opens onto it, like an office
                    // off a corridor), rooms through rooms only where there is no other way; the storage room last: everything
                    // else joins without it, then it gets its one door (a dead end)
                    bool circulation = IsCirculation(plan, a) || IsCirculation(plan, bb);
                    // (the shop's paint booth too: sealed, its one door onto the workshop when it can)
                    float w = IsStorage(plan, kv.Key) ? (OwnDoor(plan, kv.Key) ? -0.5f : -1f) : (circulation ? 10f : 0f) + (float)rng.NextDouble();
                    order.Add((kv.Key, w));
                }
                order.Sort((p, q) => q.weight.CompareTo(p.weight));
                var storageDoor = new HashSet<int>();
                foreach (var (key, _) in order)
                {
                    if (Find(key.Item1) == Find(key.Item2)) continue;
                    if (IsStorage(plan, key))
                    {
                        int s = OneDoor(plan.Rooms[key.Item1].Type) ? key.Item1 : key.Item2;
                        if (storageDoor.Contains(s) || (OneDoor(plan.Rooms[key.Item1].Type) && OneDoor(plan.Rooms[key.Item2].Type))) continue;
                        // the nurse station is open to its corridor (the whole wall), every other one-door room has a door
                        var way = Touches(plan, key, RoomType.NurseStation) && Touches(plan, key, RoomType.Corridor) ? EdgeType.Open : EdgeType.Door;
                        if (TryOpen(key, way)) { storageDoor.Add(s); done.Add(key); groupOf[Find(key.Item1)] = Find(key.Item2); }
                        continue;
                    }
                    bool open = rng.NextDouble() < b.OpenPassageChance && !IsHall(plan, key);
                    if ((open && TryOpen(key, EdgeType.Open)) || TryOpen(key, EdgeType.Door))
                    {
                        done.Add(key);
                        groupOf[Find(key.Item1)] = Find(key.Item2);
                    }
                }
                // groups still apart (only short walls between them): an open passage on any walkable wall between them
                foreach (var (key, _) in order)
                {
                    if (Find(key.Item1) == Find(key.Item2) || IsStorage(plan, key)) continue;
                    foreach (int wi in pairs[key])
                    {
                        var w = plan.Walls[wi];
                        if (w.Length < 1.8f || plan.OpeningsOf(wi).Count > 0 || NearStair(stairZones, f, w, 0f, w.Length)) continue;
                        plan.Openings.Add(new PlanOpening { Wall = wi, Kind = EdgeType.Open, At = w.Length / 2f, Width = w.Length });
                        done.Add(key);
                        groupOf[Find(key.Item1)] = Find(key.Item2);
                        break;
                    }
                }
                foreach (int r in rooms) if (Find(r) == Find(start)) reached.Add(r);

                // loops, vents, slots: a budget a floor, each where it makes the longest way round (the walk between the
                // two rooms before it, a little shuffled) — planned loops instead of a door in every other wall
                var slots = c.Rng(0x5A0E, f);
                int floorRooms = 0;
                foreach (int r in rooms) if (!IsCirculation(plan, r)) floorRooms++;
                var links = new Dictionary<int, List<int>>();
                foreach (var key in done) { Link(links, key.Item1, key.Item2); }
                void Place(EdgeType kind, float perRoom, System.Random pick)
                {
                    int want = Mathf.RoundToInt(floorRooms * perRoom + (float)pick.NextDouble() * 0.5f);
                    for (int n = 0; n < want; n++)
                    {
                        (int, int) best = default;
                        float bestScore = 0f;
                        foreach (var key in pairs.Keys)
                        {
                            if (IsStorage(plan, key) || links.TryGetValue(key.Item1, out var l1) && l1.Contains(key.Item2)) continue;
                            if (kind == EdgeType.Door && IsCirculation(plan, key.Item1) == IsCirculation(plan, key.Item2) && IsCirculation(plan, key.Item1)) continue;
                            int d = Walk(links, key.Item1, key.Item2);
                            if (d < (kind == EdgeType.Door ? 3 : 2)) continue; // a door loop round one wall is no loop; a vent may short-cut one
                            float score = d + (float)pick.NextDouble() * 3f;
                            if (score > bestScore) { bestScore = score; best = key; }
                        }
                        if (bestScore <= 0f) break;
                        if (TryOpen(best, kind)) Link(links, best.Item1, best.Item2);
                        else Link(links, best.Item1, best.Item2); // no room for it there: never tried again
                    }
                }
                Place(EdgeType.Door, b.LoopsPerRoom, rng);
                Place(EdgeType.Vent, b.VentsPerRoom, rng);
                Place(EdgeType.Squeeze, b.SqueezesPerRoom, slots);
            }

            // Exterior doors: two ways out into the gas on the ground floor (not from the lobby).
            var ext = c.Rng(0xE87);
            int doors = 0;
            var candidates = new List<int>();
            for (int i = 0; i < plan.Walls.Count; i++)
            {
                var w = plan.Walls[i];
                if (w.Floor != 0 || !w.Exterior || w.Shaped || w.Length < DoorWidth + 2f * DoorMargin) continue;
                int room = Mathf.Max(w.RoomA, w.RoomB);
                if (room < 0 || room == o.Lobby || OneDoor(plan.Rooms[room].Type) || plan.Rooms[room].Type is RoomType.Workshop or RoomType.Utility) continue; // (the hall's street wall is its bays' doors; the plant room is the drain's)
                candidates.Add(i);
            }
            while (doors < 2 && candidates.Count > 0)
            {
                int k = ext.Next(candidates.Count);
                int wi = candidates[k];
                candidates.RemoveAt(k);
                var w = plan.Walls[wi];
                float at = Mathf.Round(w.Length * 2f) / 4f;
                if (!plan.FreeOn(wi, at, DoorWidth, DoorMargin) || NearStair(stairZones, 0, w, at - 1.2f, at + 1.2f)) continue;
                plan.Openings.Add(new PlanOpening { Wall = wi, Kind = EdgeType.Door, At = at, Width = DoorWidth });
                doors++;
            }
        }

        static bool IsCirculation(HousePlan plan, int room) => plan.Rooms[room].Type is RoomType.Corridor or RoomType.Hall or RoomType.Lobby;

        static void Link(Dictionary<int, List<int>> links, int a, int b)
        {
            if (!links.TryGetValue(a, out var la)) links[a] = la = new List<int>();
            if (!links.TryGetValue(b, out var lb)) links[b] = lb = new List<int>();
            la.Add(b);
            lb.Add(a);
        }

        /// <summary>Steps from room a to room b over these links (breadth first); not joined: a long way (99).</summary>
        static int Walk(Dictionary<int, List<int>> links, int a, int b)
        {
            var dist = new Dictionary<int, int> { [a] = 0 };
            var q = new Queue<int>();
            q.Enqueue(a);
            while (q.Count > 0)
            {
                int r = q.Dequeue();
                if (r == b) return dist[r];
                if (!links.TryGetValue(r, out var next)) continue;
                foreach (int n in next)
                    if (!dist.ContainsKey(n)) { dist[n] = dist[r] + 1; q.Enqueue(n); }
            }
            return 99;
        }

        static bool IsStorage(HousePlan plan, (int, int) key) => OneDoor(plan.Rooms[key.Item1].Type) || OneDoor(plan.Rooms[key.Item2].Type);

        /// <summary>A room with a single door, a dead end: the storage room (it is locked), the shop's paint booth (sealed).</summary>
        static bool OneDoor(RoomType t) => t is RoomType.Storage or RoomType.PaintBooth or RoomType.Utility
            or RoomType.Ward or RoomType.NurseStation or RoomType.Operating or RoomType.Imaging; // (the hospital's: one way in, off the corridor)

        /// <summary>The door a one-door room should have: the paint booth's into the workshop, the plant room's into the lobby.</summary>
        static bool OwnDoor(HousePlan plan, (int, int) key) =>
            (Touches(plan, key, RoomType.PaintBooth) && Touches(plan, key, RoomType.Workshop)) || (Touches(plan, key, RoomType.Utility) && Touches(plan, key, RoomType.Lobby))
            || (Touches(plan, key, RoomType.Corridor) && (Touches(plan, key, RoomType.Ward) || Touches(plan, key, RoomType.NurseStation)
                || Touches(plan, key, RoomType.Operating) || Touches(plan, key, RoomType.Imaging)));

        static bool Touches(HousePlan plan, (int, int) key, RoomType t) => plan.Rooms[key.Item1].Type == t || plan.Rooms[key.Item2].Type == t;

        static bool IsHall(HousePlan plan, (int, int) key) => plan.Rooms[key.Item1].Type == RoomType.Hall || plan.Rooms[key.Item2].Type == RoomType.Hall;

        static bool HasPassage(HousePlan plan, List<int> walls)
        {
            foreach (var o in plan.Openings)
                if (walls.Contains(o.Wall) && o.Kind is EdgeType.Door or EdgeType.Open) return true;
            return false;
        }

        static int? StairRoomAbove(Output o, int f)
        {
            foreach (var s in o.Stairs) if (s.Floor + 1 == f) return s.RoomAbove;
            return null;
        }

        /// <summary>The square of a staircase and its approach, grown by <paramref name="margin"/>.</summary>
        public static Rect StairArea(Stair s, float margin)
        {
            float x = s.Corner.x > 0 ? s.Origin.x - StairSize : s.Origin.x;
            float z = s.Corner.y > 0 ? s.Origin.y - StairSize : s.Origin.y;
            return new Rect(x - margin, z - margin, StairSize + 2 * margin, StairSize + 2 * margin);
        }

        static bool NearStair(List<(int floor, Rect area)> zones, int f, PlanWall w, float t0, float t1)
        {
            var a = w.At(Mathf.Max(0f, t0));
            var b = w.At(Mathf.Min(w.Length, t1));
            var seg = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - 0.15f, Mathf.Min(a.y, b.y) - 0.15f, Mathf.Max(a.x, b.x) + 0.15f, Mathf.Max(a.y, b.y) + 0.15f);
            foreach (var (floor, area) in zones)
                if (floor == f && area.Overlaps(seg)) return true;
            return false;
        }

        /// <summary>Doorways from the lobby to every room (stairs count as one step): privacy depth.</summary>
        static void Depths(HousePlan plan, Output o)
        {
            foreach (var r in plan.Rooms) r.Depth = int.MaxValue;
            var adj = new Dictionary<int, List<int>>();
            void Link(int a, int b)
            {
                if (a < 0 || b < 0) return;
                if (!adj.TryGetValue(a, out var la)) adj[a] = la = new List<int>();
                if (!adj.TryGetValue(b, out var lb)) adj[b] = lb = new List<int>();
                la.Add(b);
                lb.Add(a);
            }
            foreach (var op in plan.Openings)
            {
                if (op.Kind is not (EdgeType.Door or EdgeType.Open)) continue;
                var w = plan.Walls[op.Wall];
                Link(w.RoomA, w.RoomB);
            }
            foreach (var s in o.Stairs) Link(s.Room, s.RoomAbove);
            var q = new Queue<int>();
            plan.Rooms[o.Lobby].Depth = 0;
            q.Enqueue(o.Lobby);
            while (q.Count > 0)
            {
                int r = q.Dequeue();
                if (!adj.TryGetValue(r, out var list)) continue;
                foreach (int n in list)
                {
                    if (plan.Rooms[n].Depth <= plan.Rooms[r].Depth + 1) continue;
                    plan.Rooms[n].Depth = plan.Rooms[r].Depth + 1;
                    q.Enqueue(n);
                }
            }
        }
    }
}
