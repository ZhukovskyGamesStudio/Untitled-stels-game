using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    public static partial class PlanGenerator
    {
        /// <summary>
        /// A house of separate blocks joined by corridors (the rooms test, and some houses of the game: see
        /// <see cref="BuildingSettings.LinkedChance"/>). Made in the order an architect sketches it:
        /// first the links — which blocks join, and whether through a corridor or wall to wall; then the length of every
        /// corridor; then the blocks are placed so that every link holds (a corridor's blocks exactly its length apart,
        /// facing each other across it); last the corridors themselves, in the gaps. Outside the blocks and corridors is
        /// the yard, so a corridor reads as a corridor: a narrow passage with outside on both sides. Metres, x/z ≥ 0.
        /// </summary>
        public sealed class LinkedMass
        {
            public readonly List<RectInt> Blocks = new();
            public readonly List<RectInt> Corridors = new();
            /// <summary>Block a, block b, the corridor between them (−1: wall to wall).</summary>
            public readonly List<(int a, int b, int corridor)> Links = new();
            /// <summary>Block 0 is the lobby; these are the stair halls (one per pair of floors).</summary>
            public readonly List<int> Halls = new();
        }

        /// <summary>Width of a corridor between blocks, metres (whole metres: the mass is in metres).</summary>
        public const int LinkCorridorWidth = 3;
        /// <summary>Gap kept between blocks and corridors that are not linked (metres): the yard runs between them.</summary>
        const int LinkClearance = 2;

        /// <summary>How many blocks a house of this floor area gets (the room limits, if any, decide for the rooms test).</summary>
        public static int LinkedBlocks(System.Random rng, float area, int floors, BuildingSettings b)
        {
            int halls = Mathf.Max(0, floors - 1);
            if (b.RoomCountMax > 0)
            {
                int lo = Mathf.Max(2, b.RoomCountMin), hi = Mathf.Max(lo, b.RoomCountMax);
                return rng.Next(lo, hi + 1) + halls;
            }
            return Mathf.Clamp(Mathf.RoundToInt(area * 0.85f / 110f), 3, 14) + halls;
        }

        /// <summary>The links, corridor lengths and placed blocks of a linked house, or null if none could be laid out (pure).</summary>
        public static LinkedMass Linked(System.Random rng, int blocks, int floors, BuildingSettings b)
        {
            blocks = Mathf.Max(2, blocks);
            var halls = new HashSet<int>();
            for (int k = 0; k < floors - 1 && k + 1 < blocks; k++) halls.Add(blocks - 1 - k);
            var sizes = new List<Vector2Int>();
            for (int i = 0; i < blocks; i++) sizes.Add(BlockSize(rng, b, halls.Contains(i), i == 0));
            return Linked(rng, sizes, -1, floors, b);
        }

        /// <summary>
        /// The same with the blocks' sizes given (block 0 the lobby, the last ones the stair halls); <paramref name="leaf"/>:
        /// a block nothing else hangs off (the storage room: one door), −1 none.
        /// </summary>
        public static LinkedMass Linked(System.Random rng, List<Vector2Int> sizes, int leaf, int floors, BuildingSettings b)
        {
            for (int attempt = 0; attempt < 120; attempt++)
            {
                var m = TryLinked(rng, sizes, leaf, floors, b);
                if (m != null) return m;
            }
            return null;
        }

        static LinkedMass TryLinked(System.Random rng, List<Vector2Int> sizes, int leaf, int floors, BuildingSettings b)
        {
            int n = Mathf.Max(2, sizes.Count);
            var m = new LinkedMass();
            // 1. the links: a tree (every block reached), a block joins at most three others; each link a corridor or a wall
            var parent = new int[n];
            var degree = new int[n];
            parent[0] = -1;
            for (int i = 1; i < n; i++)
            {
                int p;
                int guard = 0;
                do p = rng.Next(i); while ((degree[p] >= 3 || p == leaf) && ++guard < 40);
                if (p == leaf) p = 0; // never off the leaf
                parent[i] = p;
                degree[p]++;
                degree[i]++;
            }
            var corridor = new bool[n];
            int corridors = 0;
            for (int i = 1; i < n; i++) if (corridor[i] = rng.NextDouble() < b.LinkedCorridorShare) corridors++;
            // at least the corridors asked for (MinCorridors, at least one: this is a house with corridors)
            int want = Mathf.Min(n - 1, Mathf.Max(1, b.MinCorridors));
            while (corridors < want)
            {
                int i = 1 + rng.Next(n - 1);
                if (!corridor[i]) { corridor[i] = true; corridors++; }
            }
            // 2. the length of every corridor
            var length = new int[n];
            for (int i = 1; i < n; i++)
                if (corridor[i]) length[i] = rng.Next(Mathf.Max(3, b.LinkCorridorLength.x), Mathf.Max(3, b.LinkCorridorLength.y) + 1);
            // 3. the blocks: their sizes, then placed parent first so every link holds
            var halls = new HashSet<int>();
            for (int k = 0; k < floors - 1 && k + 1 < n; k++) halls.Add(n - 1 - k);
            var size = new Vector2Int[n];
            for (int i = 0; i < n; i++) size[i] = i < sizes.Count ? sizes[i] : new Vector2Int(8, 7);
            var rects = new RectInt[n];
            var corridorRect = new RectInt?[n];
            var placed = new List<RectInt>();
            rects[0] = new RectInt(0, 0, size[0].x, size[0].y);
            placed.Add(rects[0]);
            for (int i = 1; i < n; i++)
            {
                var p = rects[parent[i]];
                bool ok = false;
                for (int tries = 0; tries < 60 && !ok; tries++)
                {
                    int dir = rng.Next(4);                       // 0 +x, 1 −x, 2 +z, 3 −z of the parent
                    var s = size[i];
                    if (rng.Next(2) == 0 && !halls.Contains(i)) s = new Vector2Int(s.y, s.x);
                    int gap = corridor[i] ? length[i] : 0;
                    bool alongZ = dir < 2;                       // the shared span runs along z when the child is beside in x
                    int pLo = alongZ ? p.yMin : p.xMin, pHi = alongZ ? p.yMax : p.xMax, cLen = alongZ ? s.y : s.x;
                    int need = corridor[i] ? LinkCorridorWidth + 2 : 4;     // a corridor with a metre each side; a 3.6 m door
                    int lo = pLo - cLen + need, hi = pHi - need;
                    if (hi < lo) continue;
                    int off = lo + rng.Next(hi - lo + 1);
                    RectInt child = dir switch
                    {
                        0 => new RectInt(p.xMax + gap, off, s.x, s.y),
                        1 => new RectInt(p.xMin - gap - s.x, off, s.x, s.y),
                        2 => new RectInt(off, p.yMax + gap, s.x, s.y),
                        _ => new RectInt(off, p.yMin - gap - s.y, s.x, s.y),
                    };
                    RectInt? link = null;
                    if (corridor[i])
                    {
                        int sLo = Mathf.Max(pLo, alongZ ? child.yMin : child.xMin) + 1, sHi = Mathf.Min(pHi, alongZ ? child.yMax : child.xMax) - 1 - LinkCorridorWidth;
                        if (sHi < sLo) continue;
                        int at = sLo + rng.Next(sHi - sLo + 1);
                        link = dir switch
                        {
                            0 => new RectInt(p.xMax, at, gap, LinkCorridorWidth),
                            1 => new RectInt(p.xMin - gap, at, gap, LinkCorridorWidth),
                            2 => new RectInt(at, p.yMax, LinkCorridorWidth, gap),
                            _ => new RectInt(at, p.yMin - gap, LinkCorridorWidth, gap),
                        };
                    }
                    // clear of everything else: the yard between blocks that do not link
                    bool clear = true;
                    for (int j = 0; j < i && clear; j++)
                    {
                        if (j == parent[i]) continue;
                        if (Grow(rects[j], LinkClearance).Overlaps(child)) clear = false;
                        if (link.HasValue && Grow(rects[j], LinkClearance).Overlaps(link.Value)) clear = false;
                        if (corridorRect[j].HasValue && Grow(corridorRect[j].Value, LinkClearance).Overlaps(child)) clear = false;
                        if (corridorRect[j].HasValue && link.HasValue && Grow(corridorRect[j].Value, LinkClearance).Overlaps(link.Value)) clear = false;
                    }
                    // the parent's own corridors to its other children stay clear of this one too
                    if (clear && corridorRect[parent[i]].HasValue)
                    {
                        var up = Grow(corridorRect[parent[i]].Value, LinkClearance);
                        if (up.Overlaps(child) || (link.HasValue && up.Overlaps(link.Value))) clear = false;
                    }
                    if (!clear) continue;
                    rects[i] = child;
                    corridorRect[i] = link;
                    ok = true;
                }
                if (!ok) return null;
            }
            // 4. the corridors, and everything shifted to x, z ≥ 0
            int minX = int.MaxValue, minZ = int.MaxValue;
            for (int i = 0; i < n; i++)
            {
                minX = Mathf.Min(minX, rects[i].xMin); minZ = Mathf.Min(minZ, rects[i].yMin);
                if (corridorRect[i].HasValue) { minX = Mathf.Min(minX, corridorRect[i].Value.xMin); minZ = Mathf.Min(minZ, corridorRect[i].Value.yMin); }
            }
            RectInt Shift(RectInt r) => new(r.x - minX, r.y - minZ, r.width, r.height);
            for (int i = 0; i < n; i++) m.Blocks.Add(Shift(rects[i]));
            for (int i = 1; i < n; i++)
            {
                int c = -1;
                if (corridorRect[i].HasValue) { c = m.Corridors.Count; m.Corridors.Add(Shift(corridorRect[i].Value)); }
                m.Links.Add((parent[i], i, c));
            }
            foreach (int h in halls) m.Halls.Add(h);
            m.Halls.Sort();
            return m;
        }

        /// <summary>A block's size in metres: a room inside the area limits (whole, not split), a stair hall 7 x 7, the lobby ≥ 7 m.</summary>
        static Vector2Int BlockSize(System.Random rng, BuildingSettings b, bool hall, bool lobby)
        {
            if (hall) return new Vector2Int(7, 7);
            float lo = b.RoomAreaMin > 0f ? b.RoomAreaMin + 3f : 45f * b.RoomSizeScale;
            float hi = b.RoomAreaMax > 0f ? Mathf.Min(b.RoomAreaMax - 8f, lo + 80f) : 140f * b.RoomSizeScale;
            hi = Mathf.Max(lo + 1f, hi);
            for (int tries = 0; tries < 20; tries++)
            {
                float area = lo + (float)rng.NextDouble() * (hi - lo);
                float aspect = 1f + (float)rng.NextDouble() * 0.7f;
                int w = Mathf.RoundToInt(Mathf.Sqrt(area * aspect)), d = Mathf.RoundToInt(area / Mathf.Max(1, w));
                int min = lobby ? 7 : 5;
                if (w < min || d < min) continue;
                float got = w * d;
                if ((b.RoomAreaMin > 0f && got < b.RoomAreaMin + 1f) || (b.RoomAreaMax > 0f && got > b.RoomAreaMax - 1f)) continue;
                return new Vector2Int(w, d);
            }
            return new Vector2Int(8, 7);
        }
    }
}
