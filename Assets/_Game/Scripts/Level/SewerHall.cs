using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>What stands in a side of a hall's cell on the hall's boundary.</summary>
    public enum HallSide { Wall, Entry, Mouth }

    /// <summary>The outline the hall's footprint started from (for logs and tests: the cells say what it is).</summary>
    public enum HallForm { Box, L, T, Cross, Ragged }

    /// <summary>One side of the hall: a wall, the way in from the sewer's tunnel, or a mouth of a tunnel leading on.</summary>
    public struct HallEdge
    {
        public Vector2Int Cell;
        /// <summary>Out of the hall: (1, 0) +X, (0, 1) +Z, (0, −1) −Z, (−1, 0) −X.</summary>
        public Vector2Int Out;
        public HallSide Kind;
        /// <summary>The mouth's index in <see cref="SewerHall.Mouths"/> (<see cref="HallSide.Mouth"/>), else −1.</summary>
        public int Mouth;
        /// <summary>
        /// Hall-local: the middle of the wall's outer face at road level, +X out of the hall — the frame of a branch
        /// tunnel, of a plug and of every wall piece of the kit (sewer_hall_wall / _mouth / _entry).
        /// </summary>
        public Pose Frame;
    }

    /// <summary>
    /// The sewer's hall where the tunnels lead on to the next houses (pure, unit tested), different every time: a
    /// footprint of <see cref="Cell"/> m cells (a box, an L, a T, a cross or a ragged one with bays, up to 5 cells long
    /// and 7 wide) under brick groin vaults on columns, low or high (<see cref="Springing"/>). The sewer's tunnel comes in
    /// at hall-local (0, 0, 0) heading +X into cell (0, 0); the hall's cells start behind the entry's wall
    /// (<see cref="Wall"/> thick), cell (i, k) is x ∈ <see cref="Wall"/> + 8i .. +8, z ∈ 8k ± 4. The ways on leave by
    /// mouths in boundary sides facing +X, +Z or −Z (never back): 3–5 of them, three take the tunnels (left to right as
    /// the car comes in), the others are bricked up. Every mouth has a straight run of a cell inside behind it, no
    /// tunnel runs through the hall, the incoming tunnel or another tunnel, and a car gets from the way in to every
    /// mouth (cells connect side by side, columns only stand at their corners). Rolls from its own stream.
    /// </summary>
    public sealed class SewerHall
    {
        /// <summary>A cell's side, metres (the kit's tiles: Tools/Blender/sewer.py HALL_CELL).</summary>
        public const float Cell = 8f;
        /// <summary>The hall's walls are this thick; their inner faces stand on the cells' sides.</summary>
        public const float Wall = 0.6f;
        /// <summary>The kit's height variants: where the vaults spring from the columns' capitals (sewer.py HALL_HEIGHTS).</summary>
        public const int Heights = 2;
        public static readonly float[] Springing = { 3.4f, 4.2f };
        /// <summary>Half the widest part of a column at the road (its base), round a corner of the cells.</summary>
        public const float ColumnHalf = 0.38f;
        /// <summary>Half the shaft of a column (lamps hang on it).</summary>
        public const float ShaftHalf = 0.34f;
        /// <summary>Half the width of a branch tunnel with its ribs (sewer_branch: 2.3 + 0.1), with a margin.</summary>
        public const float TunnelHalf = 2.7f;
        /// <summary>Half the width of the incoming tunnel with its vault (the kit's segment: 3.6 + 0.35).</summary>
        public const float IncomingHalf = 4f;
        public const int TunnelCount = 3;
        const float Far = 1000f;
        const int Attempts = 24;
        const int MaxLamps = 9;

        static readonly Vector2Int[] Sides = { new(1, 0), new(0, 1), new(0, -1), new(-1, 0) };
        /// <summary>Where a way on may lead: ahead or to a side, never back along the incoming tunnel.</summary>
        static readonly Vector2Int[] Ways = { new(1, 0), new(0, 1), new(0, -1) };
        public static readonly Vector2Int EntryCell = Vector2Int.zero;
        /// <summary>The cell ahead of the way in: always part of the hall (the car drives in straight).</summary>
        static readonly Vector2Int Ahead = new(1, 0);
        static readonly Vector2Int Back = new(-1, 0);

        public readonly HashSet<Vector2Int> Cells = new();
        /// <summary>The cells in a fixed order (x, then z): build and name in this order on every peer.</summary>
        public readonly List<Vector2Int> Ordered = new();
        public readonly List<HallEdge> Edges = new();
        /// <summary>The mouths (hall-local frames, +X into the tunnel): the first <see cref="TunnelCount"/> listed in <see cref="Tunnels"/>.</summary>
        public Pose[] Mouths = Array.Empty<Pose>();
        /// <summary>Which mouth each of the three tunnels (left, middle, right as the car comes in) leaves by.</summary>
        public readonly int[] Tunnels = { 0, 1, 2 };
        /// <summary>The corners of the cells a column stands at (free-standing inside, half in the wall on the boundary).</summary>
        public readonly List<Vector2Int> Columns = new();
        /// <summary>The stone arches between neighbouring cells, column to column (hall-local; the arch spans its local Z).</summary>
        public readonly List<Pose> Arches = new();
        /// <summary>Lamps on the walls and on a few columns (hall-local; the lamp's +X looks into the hall).</summary>
        public readonly List<Pose> Lamps = new();
        public int Height;
        public HallForm Form;
        /// <summary>The hall's extent with its walls (hall-local; it starts at x = 0, the entry wall's outer face).</summary>
        public float MaxX, MinZ, MaxZ;

        readonly List<(Vector2Int cell, Vector2Int side)> m_Mouths = new();

        // ------------------------------------------------------------------ geometry

        public static Vector3 CellCentre(Vector2Int c) => new(Wall + Cell * c.x + Cell / 2f, 0f, Cell * c.y);

        /// <summary>A corner of the cells: corner (a, b) is the one between cells (a − 1 .. a, b − 1 .. b).</summary>
        public static Vector3 Corner(Vector2Int v) => new(Wall + Cell * v.x, 0f, Cell * v.y - Cell / 2f);

        /// <summary>The frame of a side of a cell: the middle of the wall's outer face, +X out of the hall.</summary>
        public static Pose Frame(Vector2Int cell, Vector2Int side)
        {
            var o = new Vector3(side.x, 0f, side.y);
            float yaw = -Mathf.Atan2(side.y, side.x) * Mathf.Rad2Deg;
            return new Pose(CellCentre(cell) + o * (Cell / 2f + Wall), Quaternion.Euler(0f, yaw, 0f));
        }

        /// <summary>The ground a tunnel from this side covers (x → Rect.x, z → Rect.y), on into the distance.</summary>
        public static Rect TunnelRect(Vector2Int cell, Vector2Int side)
        {
            var p = Frame(cell, side).position;
            if (side.x != 0)
                return Rect.MinMaxRect(side.x > 0 ? p.x : p.x - Far, p.z - TunnelHalf, side.x > 0 ? p.x + Far : p.x, p.z + TunnelHalf);
            return Rect.MinMaxRect(p.x - TunnelHalf, side.y > 0 ? p.z : p.z - Far, p.x + TunnelHalf, side.y > 0 ? p.z + Far : p.z);
        }

        /// <summary>A cell with its walls (x → Rect.x, z → Rect.y).</summary>
        public static Rect CellRect(Vector2Int c)
        {
            var m = CellCentre(c);
            const float h = Cell / 2f + Wall;
            return Rect.MinMaxRect(m.x - h, m.z - h, m.x + h, m.z + h);
        }

        /// <summary>The incoming tunnel behind the way in (it runs back along −X from x = 0).</summary>
        public static readonly Rect IncomingRect = Rect.MinMaxRect(-Far, -IncomingHalf, 0f, IncomingHalf);

        /// <summary>Overlap by more than a centimetre (touching sides do not count).</summary>
        public static bool Overlap(Rect a, Rect b) =>
            a.xMin < b.xMax - 0.01f && b.xMin < a.xMax - 0.01f && a.yMin < b.yMax - 0.01f && b.yMin < a.yMax - 0.01f;

        /// <summary>The side of the hall a mouth is in (its cell and the way out).</summary>
        public (Vector2Int cell, Vector2Int side) MouthSide(int mouth) => m_Mouths[mouth];

        // ------------------------------------------------------------------ the plan

        /// <summary>The hall from its own stream (<see cref="SewerPlan.Make"/> seeds it from the run's seed and the depth).</summary>
        public static SewerHall Make(System.Random rng)
        {
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                var h = new SewerHall();
                if (h.TryBuild(rng)) return h;
            }
            // a plain box always has its ways on (its far wall at least)
            var box = new SewerHall { Height = rng.Next(Heights), Form = HallForm.Box };
            box.Fill(3, 3, -1);
            box.PlaceMouths(rng);
            box.Finish(rng);
            return box;
        }

        bool TryBuild(System.Random rng)
        {
            Height = rng.Next(Heights);
            Form = (HallForm)rng.Next(5);
            int w = 2 + rng.Next(3), d = 2 + rng.Next(3), k0 = -rng.Next(d);
            Fill(w, d, k0);
            switch (Form)
            {
                case HallForm.L:
                {
                    // a block cut out of one of the far corners
                    int a = 1 + rng.Next(w - 1), b = 1 + rng.Next(d - 1);
                    bool low = rng.Next(2) == 0;
                    for (int i = w - a; i < w; i++)
                        for (int j = 0; j < b; j++) Cells.Remove(new Vector2Int(i, low ? k0 + j : k0 + d - 1 - j));
                    break;
                }
                case HallForm.T:
                {
                    // a stem from the way in (its rows hold row 0), a bar across the far end
                    int bar = w >= 4 ? 1 + rng.Next(2) : 1;
                    int stem = 1 + rng.Next(Mathf.Max(1, d - 2));
                    int lo = Mathf.Max(k0, 1 - stem), hi = Mathf.Min(0, k0 + d - stem);
                    int s0 = lo + rng.Next(hi - lo + 1);
                    for (int i = 0; i < w - bar; i++)
                        for (int k = k0; k < k0 + d; k++)
                            if (k < s0 || k >= s0 + stem) Cells.Remove(new Vector2Int(i, k));
                    break;
                }
                case HallForm.Cross:
                {
                    // the corners cut away (the way in keeps its cell)
                    if (w < 3 || d < 3) { Cells.Remove(new Vector2Int(w - 1, rng.Next(2) == 0 ? k0 : k0 + d - 1)); break; }
                    foreach (var c in new[] { new Vector2Int(0, k0), new Vector2Int(0, k0 + d - 1), new Vector2Int(w - 1, k0), new Vector2Int(w - 1, k0 + d - 1) })
                        if (c != EntryCell) Cells.Remove(c);
                    break;
                }
                case HallForm.Ragged:
                {
                    int bays = rng.Next(3);
                    for (int n = 0; n < bays; n++) Grow(rng);
                    int cuts = 1 + rng.Next(Mathf.Max(1, Cells.Count / 3));
                    for (int n = 0; n < cuts; n++) Cut(rng);
                    break;
                }
            }
            return Valid() && PlaceMouths(rng) && Finish(rng);
        }

        void Fill(int w, int d, int k0)
        {
            Cells.Clear();
            for (int i = 0; i < w; i++)
                for (int k = k0; k < k0 + d; k++) Cells.Add(new Vector2Int(i, k));
        }

        bool Valid() => Cells.Count >= 3 && Cells.Contains(EntryCell) && Cells.Contains(Ahead) && Connected() && !Pinched();

        /// <summary>Every cell reached from the way in's, side by side.</summary>
        public bool Connected()
        {
            if (!Cells.Contains(EntryCell)) return false;
            var seen = new HashSet<Vector2Int> { EntryCell };
            var open = new Queue<Vector2Int>();
            open.Enqueue(EntryCell);
            while (open.Count > 0)
            {
                var c = open.Dequeue();
                foreach (var s in Sides)
                    if (Cells.Contains(c + s) && seen.Add(c + s)) open.Enqueue(c + s);
            }
            return seen.Count == Cells.Count;
        }

        /// <summary>Two cells touching only at a corner (the walls would cross there).</summary>
        public bool Pinched()
        {
            foreach (var c in Cells)
            {
                // c as the lower left of a block of four, and as its lower right
                if (!Cells.Contains(c + new Vector2Int(1, 0)) && !Cells.Contains(c + new Vector2Int(0, 1)) && Cells.Contains(c + new Vector2Int(1, 1))) return true;
                if (!Cells.Contains(c + new Vector2Int(-1, 0)) && !Cells.Contains(c + new Vector2Int(0, 1)) && Cells.Contains(c + new Vector2Int(-1, 1))) return true;
            }
            return false;
        }

        static List<Vector2Int> Sorted(IEnumerable<Vector2Int> cells) => cells.OrderBy(c => c.x).ThenBy(c => c.y).ToList();

        static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>A bay: a cell added beside the hall (not beside the incoming tunnel).</summary>
        void Grow(System.Random rng)
        {
            var can = new List<Vector2Int>();
            var seen = new HashSet<Vector2Int>();
            foreach (var c in Sorted(Cells))
                foreach (var s in Sides)
                {
                    var n = c + s;
                    if (Cells.Contains(n) || n.x < 1 || n.x > 4 || Mathf.Abs(n.y) > 3 || !seen.Add(n)) continue;
                    Cells.Add(n);
                    if (!Pinched()) can.Add(n);
                    Cells.Remove(n);
                }
            if (can.Count > 0) Cells.Add(can[rng.Next(can.Count)]);
        }

        /// <summary>A cell of the boundary taken away, the hall staying in one piece.</summary>
        void Cut(System.Random rng)
        {
            if (Cells.Count <= 4) return;
            var can = new List<Vector2Int>();
            foreach (var c in Sorted(Cells))
            {
                if (c == EntryCell || c == Ahead || Sides.All(s => Cells.Contains(c + s))) continue;
                Cells.Remove(c);
                if (Connected() && !Pinched()) can.Add(c);
                Cells.Add(c);
            }
            if (can.Count > 0) Cells.Remove(can[rng.Next(can.Count)]);
        }

        /// <summary>The ways on: free sides (a straight cell behind them, nothing in the tunnel's way), 3–5 not crossing.</summary>
        bool PlaceMouths(System.Random rng)
        {
            var free = new List<(Vector2Int cell, Vector2Int side)>();
            foreach (var c in Sorted(Cells))
                foreach (var s in Ways)
                {
                    if (Cells.Contains(c + s)) continue;
                    var behind = c - s;
                    if (!Cells.Contains(behind) && behind != Back) continue; // the car lines up over a whole cell
                    var r = TunnelRect(c, s);
                    if (Overlap(r, IncomingRect) || Cells.Any(o => Overlap(r, CellRect(o)))) continue;
                    free.Add((c, s));
                }
            Shuffle(free, rng);
            int want = TunnelCount + rng.Next(3);
            m_Mouths.Clear();
            foreach (var m in free)
            {
                if (m_Mouths.Count == want) break;
                var r = TunnelRect(m.cell, m.side);
                if (m_Mouths.Any(o => Overlap(r, TunnelRect(o.cell, o.side)))) continue;
                m_Mouths.Add(m);
            }
            if (m_Mouths.Count < TunnelCount) return false;
            Mouths = m_Mouths.Select(m => Frame(m.cell, m.side)).ToArray();
            // which take the tunnels: three of them, left to right as the car comes in (bearing from the way in)
            var order = Enumerable.Range(0, Mouths.Length).ToList();
            Shuffle(order, rng);
            var three = order.Take(TunnelCount).OrderByDescending(i => Mathf.Atan2(Mouths[i].position.z, Mouths[i].position.x)).ToArray();
            for (int i = 0; i < TunnelCount; i++) Tunnels[i] = three[i];
            return true;
        }

        /// <summary>The walls, columns, arches and lamps of the footprint.</summary>
        bool Finish(System.Random rng)
        {
            Ordered.Clear();
            Ordered.AddRange(Sorted(Cells));
            Edges.Clear();
            foreach (var c in Ordered)
                foreach (var s in Sides)
                {
                    if (Cells.Contains(c + s)) continue;
                    int mouth = m_Mouths.FindIndex(m => m.cell == c && m.side == s);
                    var kind = mouth >= 0 ? HallSide.Mouth : c == EntryCell && s == Back ? HallSide.Entry : HallSide.Wall;
                    Edges.Add(new HallEdge { Cell = c, Out = s, Kind = kind, Mouth = mouth, Frame = Frame(c, s) });
                }

            var corners = new HashSet<Vector2Int>();
            foreach (var c in Ordered)
                for (int dx = 0; dx <= 1; dx++)
                    for (int dz = 0; dz <= 1; dz++) corners.Add(c + new Vector2Int(dx, dz));
            Columns.Clear();
            Columns.AddRange(Sorted(corners));

            Arches.Clear();
            foreach (var c in Ordered)
            {
                if (Cells.Contains(c + new Vector2Int(1, 0))) Arches.Add(new Pose(new Vector3(Wall + Cell * (c.x + 1), 0f, Cell * c.y), Quaternion.identity));
                if (Cells.Contains(c + new Vector2Int(0, 1))) Arches.Add(new Pose(new Vector3(CellCentre(c).x, 0f, Cell * c.y + Cell / 2f), Quaternion.Euler(0f, 90f, 0f)));
            }

            // lamps on about half the plain walls, and on a few columns standing free
            Lamps.Clear();
            var walls = Edges.Where(e => e.Kind == HallSide.Wall).ToList();
            Shuffle(walls, rng);
            int lit = Mathf.Clamp(Mathf.RoundToInt(walls.Count * (0.35f + 0.25f * (float)rng.NextDouble())), Mathf.Min(2, walls.Count), MaxLamps);
            float y = Springing[Height] - 0.3f;
            foreach (var e in walls.Take(lit))
                Lamps.Add(new Pose(e.Frame.position + e.Frame.rotation * new Vector3(-Wall - 0.1f, y, 0f), e.Frame.rotation * Quaternion.Euler(0f, 180f, 0f)));
            foreach (var v in Columns)
            {
                bool free = Cells.Contains(v) && Cells.Contains(v - new Vector2Int(1, 0)) && Cells.Contains(v - new Vector2Int(0, 1)) && Cells.Contains(v - new Vector2Int(1, 1));
                if (!free || rng.NextDouble() > 0.3) continue;
                var s = Sides[rng.Next(Sides.Length)];
                var o = new Vector3(s.x, 0f, s.y);
                Lamps.Add(new Pose(Corner(v) + o * (ShaftHalf + 0.1f) + Vector3.up * (Springing[Height] - 0.8f),
                    Quaternion.Euler(0f, -Mathf.Atan2(s.y, s.x) * Mathf.Rad2Deg, 0f)));
            }

            MaxX = Ordered.Max(c => Frame(c, Sides[0]).position.x);
            MinZ = Ordered.Min(c => Frame(c, Sides[2]).position.z);
            MaxZ = Ordered.Max(c => Frame(c, Sides[1]).position.z);
            return true;
        }
    }
}
