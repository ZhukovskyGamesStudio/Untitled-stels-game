using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    public static partial class PlanGenerator
    {
        /// <summary>
        /// The narrowest a room of the program is cut (cells, 4 m): its furniture along a wall and a walkway past it still
        /// leave the way through it open.
        /// </summary>
        const int RoomSide = 4 * M;

        /// <summary>A lobby's floor area, about (it is reserved on the ground floor before the rooms), m².</summary>
        const float LobbyArea = 100f;
        /// <summary>A stair hall's floor area, about, m².</summary>
        const float HallArea = 49f;

        /// <summary>
        /// The building's floor area (per storey, m²) the program needs: the fullest floor's rooms with its lobby or stair
        /// halls, plus the corridors' share (<see cref="BuildingSettings.CorridorShare"/>: corridors are not rooms).
        /// </summary>
        public static float FootprintArea(RoomProgram p, BuildingSettings b)
        {
            float most = 0f;
            for (int f = 0; f < p.Floors.Length; f++)
            {
                float fixedArea = (f == 0 ? LobbyArea : 0f) + (p.Floors.Length > 1 ? (f == 0 || f == p.Floors.Length - 1 ? HallArea : 2f * HallArea) : 0f);
                most = Mathf.Max(most, p.AreaOf(f) + fixedArea);
            }
            return most * (1f + Mathf.Max(0f, b.CorridorShare));
        }

        /// <summary>
        /// The program's rooms of a floor (those not placed already) into the free rectangles: the biggest room first into
        /// the rectangle with the most room left; a rectangle that cannot give every room in it at least its smallest size
        /// lets its smallest optional room go; one with much room to spare gets more rooms drawn by the weights. Then each
        /// rectangle is cut into its rooms (<see cref="Treemap"/>), every piece claimed as a room of its type.
        /// </summary>
        static void Fill(Ctx c, int f, List<RectInt> rects, System.Func<RectInt, Region, int> claim, System.Random rng)
        {
            var rules = c.B.RoomTypeRules;
            var rooms = new List<ProgramRoom>();
            if (c.Program != null) foreach (var r in c.Program.Floors[Mathf.Min(f, c.Program.Floors.Length - 1)]) if (r.Block < 0) rooms.Add(r);
            rooms.Sort((a, b) => b.Area.CompareTo(a.Area));
            var bins = new List<ProgramRoom>[rects.Count];
            var cap = new float[rects.Count];
            var used = new float[rects.Count];
            for (int i = 0; i < rects.Count; i++)
            {
                bins[i] = new List<ProgramRoom>();
                cap[i] = rects[i].width * rects[i].height / (float)(M * M);
            }
            foreach (var room in rooms)
            {
                int best = -1;
                float room0 = float.MinValue;
                for (int i = 0; i < rects.Count; i++)
                {
                    if (Mathf.Min(rects[i].width, rects[i].height) < RoomSide) continue;
                    float left = cap[i] - used[i];
                    if (left > room0) { room0 = left; best = i; }
                }
                if (best < 0) break;
                bins[best].Add(room);
                used[best] += room.Area;
            }
            for (int i = 0; i < rects.Count; i++)
            {
                var bin = bins[i];
                // every room at least its smallest size once the rectangle is shared out: else the least needed goes
                for (int guard = 0; guard < 64 && bin.Count > 0; guard++)
                {
                    float sum = 0f;
                    foreach (var r in bin) sum += r.Area;
                    float s = cap[i] / Mathf.Max(1f, sum);
                    bool short_ = false;
                    foreach (var r in bin) short_ |= r.Area * s < RoomRules.MinArea(r.Type, rules) * 1.08f;
                    if (!short_) break;
                    int drop = -1;
                    for (int k = bin.Count - 1; k >= 0 && drop < 0; k--) if (!bin[k].Required) drop = k;
                    bin.RemoveAt(drop >= 0 ? drop : bin.Count - 1);
                }
                // much to spare (the mass is never exactly the program): more rooms, drawn by the weights
                for (int guard = 0; guard < 32; guard++)
                {
                    float sum = 0f;
                    foreach (var r in bin) sum += r.Area;
                    float left = cap[i] - sum;
                    if (bin.Count > 0 && cap[i] / sum < 1.45f) break;
                    var t = RoomProgram.Pick(rng, c.B);
                    float min = RoomRules.MinArea(t, rules) * 1.1f;
                    // the drawn type does not fit what is left: another drawn by the weights may; none: the rooms there
                    // already share the space
                    for (int again = 0; again < 8 && min > left; again++)
                    {
                        t = RoomProgram.Pick(rng, c.B);
                        min = RoomRules.MinArea(t, rules) * 1.1f;
                    }
                    if (min > left) break;
                    bin.Add(new ProgramRoom { Type = t, Area = Mathf.Clamp(RoomProgram.Draw(rng, t, rules), min, left), Block = -1 });
                }
                if (bin.Count == 0)
                {
                    claim(rects[i], new Region { Shape = RoomShape.Rect }); // a scrap: typed later, or joined to a neighbour
                    continue;
                }
                bin.Sort((a, b) => b.Area.CompareTo(a.Area));
                Treemap(rects[i], bin, claim, c.Linked != null ? Joins(c, rects[i]) : null, c.Front?.Invoke(rects[i])); // a linked house: its joins stay whole
            }
        }

        /// <summary>
        /// Cuts a rectangle into rooms of these sizes (scaled to fill it): the list in two halves of about equal area, the
        /// rectangle cut across its longer side in that proportion, each half again. Too narrow to cut: the first (biggest or
        /// required) room takes the whole piece.
        /// </summary>
        static void Treemap(RectInt r, List<ProgramRoom> rooms, System.Func<RectInt, Region, int> claim, List<(bool alongX, int lo, int hi)> keep = null,
            bool[] front = null)
        {
            if (rooms.Count == 1)
            {
                claim(r, new Region { Type = rooms[0].Type, Shape = RoomShape.Rect, Typed = true, Required = rooms[0].Required });
                return;
            }
            bool cutX = r.width >= r.height;
            // a rectangle along a corridor (or a hall, the lobby) is cut across it, so every room keeps a wall on it for its
            // door — unless the rooms would be strips (then a back room opens through the front one)
            if (front != null)
            {
                // a cut across x keeps both halves on the corridor if one runs along the south or north side (or both west
                // and east have one); a cut across z likewise
                bool keepsX = front[2] || front[3] || (front[0] && front[1]);
                bool keepsZ = front[0] || front[1] || (front[2] && front[3]);
                if (keepsX != keepsZ)
                {
                    int along = keepsX ? r.width : r.height, deep = keepsX ? r.height : r.width;
                    if (along >= 2 * RoomSide && deep <= MaxStripAspect * (along / (float)rooms.Count)) cutX = keepsX;
                }
            }
            int side = cutX ? r.width : r.height;
            if (side < 2 * RoomSide && (cutX ? r.height : r.width) >= 2 * RoomSide) { cutX = !cutX; side = cutX ? r.width : r.height; }
            if (side < 2 * RoomSide)
            {
                int must = rooms.FindIndex(x => x.Required);
                claim(r, new Region { Type = rooms[must < 0 ? 0 : must].Type, Shape = RoomShape.Rect, Typed = true, Required = must >= 0 });
                return;
            }
            // balanced halves (the list is biggest first): each room into the lighter half
            var a = new List<ProgramRoom>();
            var b = new List<ProgramRoom>();
            float sa = 0f, sb = 0f;
            foreach (var room in rooms)
            {
                if (sa <= sb) { a.Add(room); sa += room.Area; }
                else { b.Add(room); sb += room.Area; }
            }
            int want = Mathf.Clamp(Mathf.RoundToInt(side * sa / (sa + sb)), RoomSide, side - RoomSide);
            int at = want;
            if (keep != null)
            {
                // a dividing wall never falls where a corridor or a neighbouring block joins this one: the way in stays one
                // room's wall, long enough for its doorway (the nearest cut that misses every such span)
                int origin = cutX ? r.x : r.y;
                bool Clear(int k)
                {
                    foreach (var (alongX, lo, hi) in keep)
                        if (alongX == cutX && origin + k > lo && origin + k < hi) return false;
                    return true;
                }
                for (int d = 0; d <= side; d++)
                {
                    if (want - d >= RoomSide && Clear(want - d)) { at = want - d; break; }
                    if (want + d <= side - RoomSide && Clear(want + d)) { at = want + d; break; }
                }
            }
            // the halves keep the outer sides they have (W, E, S, N); the new wall between them faces no corridor
            bool[] Part(bool low) => front == null ? null
                : cutX ? new[] { low && front[0], !low && front[1], front[2], front[3] }
                       : new[] { front[0], front[1], low && front[2], !low && front[3] };
            if (cutX)
            {
                Treemap(new RectInt(r.x, r.y, at, r.height), a, claim, keep, Part(true));
                Treemap(new RectInt(r.x + at, r.y, r.width - at, r.height), b, claim, keep, Part(false));
            }
            else
            {
                Treemap(new RectInt(r.x, r.y, r.width, at), a, claim, keep, Part(true));
                Treemap(new RectInt(r.x, r.y + at, r.width, r.height - at), b, claim, keep, Part(false));
            }
        }

        /// <summary>Rooms cut across a corridor side at most this many times deeper than wide (else a back room is cut).</summary>
        const float MaxStripAspect = 2.6f;

        /// <summary>
        /// Where other parts join a block (cells): corridors and blocks touching its edges, as spans along the edge they touch
        /// (alongX: a span of x on a south or north edge — a cut across x must miss it).
        /// </summary>
        /// <summary>A join this long (cells, 8 m) has room for a doorway on one side of any cut.</summary>
        const int LongJoin = 8 * M;

        static List<(bool alongX, int lo, int hi)> Joins(Ctx c, RectInt block)
        {
            var list = new List<(bool, int, int)>();
            const int spare = 0; // the join itself inside one room (a 3 m corridor end opens as a whole wall)
            void Add(RectInt o)
            {
                if (o.Equals(block)) return;
                bool sideX = o.xMax == block.xMin || o.xMin == block.xMax;   // touches the west or east edge: a span of z
                bool sideZ = o.yMax == block.yMin || o.yMin == block.yMax;   // the south or north edge: a span of x
                // a short join stays whole (a long one keeps a piece long enough for a door wherever it is cut)
                if (sideX)
                {
                    int lo = Mathf.Max(o.yMin, block.yMin), hi = Mathf.Min(o.yMax, block.yMax);
                    if (hi > lo && hi - lo < LongJoin) list.Add((false, lo - spare, hi + spare));
                }
                if (sideZ)
                {
                    int lo = Mathf.Max(o.xMin, block.xMin), hi = Mathf.Min(o.xMax, block.xMax);
                    if (hi > lo && hi - lo < LongJoin) list.Add((true, lo - spare, hi + spare));
                }
            }
            foreach (var corridor in c.LinkCorridors) Add(corridor);
            foreach (var other in c.LinkBlocks) Add(other);
            return list;
        }

        /// <summary>Rooms in one block of a linked house at most: with three, every room still has the yard on a side (windows).</summary>
        const int RoomsPerBlock = 3;

        /// <summary>
        /// A linked house from its program: block 0 the lobby; then the ground floor's rooms packed into blocks (each the
        /// lightest block with room for one more, up to <see cref="RoomsPerBlock"/>; a block sized for the rooms it holds);
        /// the storage room alone in the last of them (nothing hangs off it: a dead end behind its one door); then the stair
        /// halls.
        /// </summary>
        static (List<Vector2Int> sizes, int storage) LinkedSizes(System.Random rng, Ctx c)
        {
            var sizes = new List<Vector2Int> { BlockSize(rng, c.B, false, true) };
            var ground = c.Program.Floors[0];
            var order = new List<int>();
            float sum = 0f;
            for (int i = 0; i < ground.Count; i++) if (ground[i].Type != RoomType.Storage) { order.Add(i); sum += ground[i].Area; }
            order.Sort((a, b) => ground[b].Area.CompareTo(ground[a].Area));
            int count = Mathf.Max(Mathf.CeilToInt(order.Count / (float)RoomsPerBlock), Mathf.Min(order.Count, Mathf.RoundToInt(sum / 110f)));
            count = Mathf.Max(1, Mathf.Min(count, order.Count));
            var blocks = new List<List<int>>();
            var area = new List<float>();
            for (int k = 0; k < count; k++) { blocks.Add(new List<int>()); area.Add(0f); }
            foreach (int i in order)
            {
                int best = -1;
                for (int k = 0; k < count; k++)
                    if (blocks[k].Count < RoomsPerBlock && (best < 0 || area[k] < area[best])) best = k;
                if (best < 0) best = 0;
                blocks[best].Add(i);
                area[best] += ground[i].Area;
            }
            for (int k = 0; k < count; k++)
            {
                if (blocks[k].Count == 0) continue;
                foreach (int i in blocks[k]) { var r = ground[i]; r.Block = sizes.Count; ground[i] = r; }
                sizes.Add(BlockFor(rng, blocks[k].Count == 1 ? ground[blocks[k][0]] : new ProgramRoom { Type = RoomType.Office, Area = area[k] * 1.06f }, c.B));
            }
            int storage = -1;
            for (int i = 0; i < ground.Count; i++)
            {
                if (ground[i].Type != RoomType.Storage) continue;
                var r = ground[i];
                r.Block = storage = sizes.Count;
                ground[i] = r;
                sizes.Add(BlockFor(rng, r, c.B));
                break; // one storage room
            }
            for (int k = 0; k < c.Floors - 1; k++) sizes.Add(new Vector2Int(7, 7));
            return (sizes, storage);
        }

        /// <summary>A block in whole metres holding at least the room's size, its sides within the type's proportions.</summary>
        static Vector2Int BlockFor(System.Random rng, ProgramRoom room, BuildingSettings b)
        {
            var rule = RoomRules.For(room.Type, b.RoomTypeRules);
            float max = rule.MaxAspect > 0f ? Mathf.Min(1.7f, rule.MaxAspect) : 1.7f;
            float aspect = 1f + (float)rng.NextDouble() * (max - 1f);
            int w = Mathf.Max(5, Mathf.CeilToInt(Mathf.Sqrt(room.Area * aspect)));
            int d = Mathf.Max(5, Mathf.CeilToInt(room.Area / w));
            return new Vector2Int(w, d);
        }
    }
}
