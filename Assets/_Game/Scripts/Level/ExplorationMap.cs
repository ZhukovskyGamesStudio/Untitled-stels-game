using UnityEngine;
using static Stealth.Level.LevelMetrics;

namespace Stealth.Level
{
    /// <summary>
    /// What the team has seen of a house (pure, unit tested): every floor is a grid of 2.5 m tiles (4 x 4 per room).
    /// A tile is revealed when a player sees its centre: close enough and not behind a wall — doors and vents let the
    /// view through their opening, open passages along the whole wall. The server reveals around every player and
    /// replicates the bits (<see cref="Chunk"/> / <see cref="Merge"/>); the minimap draws revealed tiles, walls and furniture.
    /// </summary>
    public sealed class ExplorationMap
    {
        public const int TilesPerCell = 4;
        public const float Tile = Cell / TilesPerCell;

        readonly LevelLayout m_Layout;
        readonly ulong[] m_Bits;
        readonly int[] m_RevealedPerFloor;

        /// <summary>Tiles along X and Z of one floor, and floors.</summary>
        public readonly int Width, Height, Floors;

        /// <summary>Grows with every change: viewers redraw, the network twin sends.</summary>
        public int Version { get; private set; }

        public int Seed => m_Layout.Seed;
        public LevelLayout Layout => m_Layout;
        public int ChunkCount => m_Bits.Length;

        public ExplorationMap(LevelLayout layout)
        {
            m_Layout = layout;
            Width = layout.Width * TilesPerCell;
            Height = layout.Height * TilesPerCell;
            Floors = layout.Floors;
            m_Bits = new ulong[(Width * Height * Floors + 63) / 64];
            m_RevealedPerFloor = new int[Floors];
        }

        int Index(int floor, int tx, int ty) => (floor * Height + ty) * Width + tx;

        public bool InRange(int floor, int tx, int ty) => floor >= 0 && floor < Floors && tx >= 0 && ty >= 0 && tx < Width && ty < Height;

        public bool IsRevealed(int floor, int tx, int ty)
        {
            if (!InRange(floor, tx, ty)) return false;
            int i = Index(floor, tx, ty);
            return (m_Bits[i >> 6] & (1UL << (i & 63))) != 0;
        }

        /// <summary>Share of a floor the team has seen (0..1).</summary>
        public float RevealedShare(int floor) => floor >= 0 && floor < Floors ? m_RevealedPerFloor[floor] / (float)(Width * Height) : 0f;

        public static Vector2Int TileOf(Vector3 p) => new(Mathf.FloorToInt(p.x / Tile), Mathf.FloorToInt(p.z / Tile));

        public static Vector2 TileCenter(int tx, int ty) => new((tx + 0.5f) * Tile, (ty + 0.5f) * Tile);

        bool Set(int floor, int tx, int ty)
        {
            int i = Index(floor, tx, ty);
            ulong bit = 1UL << (i & 63);
            if ((m_Bits[i >> 6] & bit) != 0) return false;
            m_Bits[i >> 6] |= bit;
            m_RevealedPerFloor[floor]++;
            return true;
        }

        /// <summary>
        /// Reveals what is seen from <paramref name="eye"/> (its floor only) within <paramref name="radius"/> metres.
        /// Returns how many tiles were new.
        /// </summary>
        public int RevealAround(Vector3 eye, float radius)
        {
            if (!m_Layout.IsInside(eye)) return 0;
            int floor = Mathf.Clamp(LevelLayout.FloorOf(eye.y), 0, Floors - 1);
            var from = new Vector2(eye.x, eye.z);
            int r = Mathf.CeilToInt(radius / Tile);
            var c = TileOf(eye);
            int added = 0;
            for (int ty = c.y - r; ty <= c.y + r; ty++)
                for (int tx = c.x - r; tx <= c.x + r; tx++)
                {
                    if (!InRange(floor, tx, ty) || IsRevealed(floor, tx, ty)) continue;
                    var to = TileCenter(tx, ty);
                    if ((to - from).sqrMagnitude > radius * radius) continue;
                    if (!CanSee(m_Layout, floor, from, to)) continue;
                    if (Set(floor, tx, ty)) added++;
                }
            if (added > 0) Version++;
            return added;
        }

        /// <summary>
        /// The straight line between two points of a floor crosses no wall: at every room boundary it passes through an
        /// open passage or the opening of a door / vent. Leaving the building blocks the view.
        /// </summary>
        public static bool CanSee(LevelLayout l, int floor, Vector2 from, Vector2 to)
        {
            var d = to - from;
            // Boundaries between rooms along X (walls at x = k * Cell).
            int x0 = Mathf.FloorToInt(from.x / Cell), x1 = Mathf.FloorToInt(to.x / Cell);
            for (int k = Mathf.Min(x0, x1) + 1; k <= Mathf.Max(x0, x1); k++)
            {
                float t = (k * Cell - from.x) / d.x;
                float z = from.y + d.y * t;
                int row = Mathf.FloorToInt(z / Cell);
                if (k <= 0 || k >= l.Width || row < 0 || row >= l.Height) return false;
                if (!Passes(l.EdgesX[floor, k - 1, row], z - (row + 0.5f) * Cell)) return false;
            }
            // Boundaries along Z (walls at z = k * Cell).
            int y0 = Mathf.FloorToInt(from.y / Cell), y1 = Mathf.FloorToInt(to.y / Cell);
            for (int k = Mathf.Min(y0, y1) + 1; k <= Mathf.Max(y0, y1); k++)
            {
                float t = (k * Cell - from.y) / d.y;
                float x = from.x + d.x * t;
                int col = Mathf.FloorToInt(x / Cell);
                if (k <= 0 || k >= l.Height || col < 0 || col >= l.Width) return false;
                if (!Passes(l.EdgesZ[floor, col, k - 1], x - (col + 0.5f) * Cell)) return false;
            }
            return true;
        }

        /// <summary>Does a line cross this wall <paramref name="offset"/> metres from its middle?</summary>
        static bool Passes(EdgeType edge, float offset) => edge switch
        {
            EdgeType.Open => true,
            EdgeType.Door => Mathf.Abs(offset) <= ClearWidth(DoorWidth) / 2f,
            EdgeType.Vent => Mathf.Abs(offset) <= ClearWidth(VentWidth) / 2f,
            _ => false,
        };

        // ------------------------------------------------------------------ replication

        public ulong Chunk(int i) => m_Bits[i];

        /// <summary>Adds what another peer has seen (bits are only ever added). Returns true if anything was new.</summary>
        public bool Merge(int i, ulong bits)
        {
            if (i < 0 || i >= m_Bits.Length) return false;
            ulong fresh = bits & ~m_Bits[i];
            if (fresh == 0) return false;
            m_Bits[i] |= fresh;
            for (int b = 0; b < 64; b++)
            {
                if ((fresh & (1UL << b)) == 0) continue;
                int index = i * 64 + b;
                int floor = index / (Width * Height);
                if (floor < Floors) m_RevealedPerFloor[floor]++;
            }
            Version++;
            return true;
        }
    }
}
