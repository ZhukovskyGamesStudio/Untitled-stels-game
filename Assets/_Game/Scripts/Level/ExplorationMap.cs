using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// What the team has seen of a house (pure, unit tested): every floor is a grid of 0.5 m tiles (the cells of the
    /// plan). The view is a fan of rays from the eyes that stop at solid walls — doors and vents let it through their
    /// opening, open passages and railings along their whole length (<see cref="HousePlan.SightShare"/>) — so walking past
    /// a door reveals the wedge of the room seen through it, not whole squares. The server reveals around every player and
    /// replicates the bits (<see cref="Chunk"/> / <see cref="Merge"/>); the minimap draws revealed tiles, walls and furniture.
    /// </summary>
    public sealed class ExplorationMap
    {
        public const float Tile = HousePlan.Res;
        /// <summary>Gap between neighbouring rays at the edge of the view (smaller than a tile: no tile slips between them).</summary>
        const float RaySpacing = Tile * 0.7f;

        readonly LevelLayout m_Layout;
        readonly ulong[] m_Bits;
        readonly int[] m_RevealedPerFloor;
        readonly List<int> m_Walls = new();

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
            Width = Mathf.CeilToInt(layout.SizeX / Tile);
            Height = Mathf.CeilToInt(layout.SizeZ / Tile);
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
        /// Reveals what is seen from <paramref name="eye"/> (its floor only) within <paramref name="radius"/> metres:
        /// a fan of rays, each one marching until a solid wall or the outside. Returns how many tiles were new.
        /// </summary>
        public int RevealAround(Vector3 eye, float radius)
        {
            var plan = m_Layout.Plan;
            if (plan == null || !m_Layout.IsInside(eye)) return 0;
            int floor = Mathf.Clamp(LevelLayout.FloorOf(eye.y), 0, Floors - 1);
            var from = new Vector2(eye.x, eye.z);
            if (!plan.IsInside(floor, from)) return 0;
            plan.WallsNear(floor, from, radius, m_Walls);
            int rays = Mathf.Max(64, Mathf.CeilToInt(2f * Mathf.PI * radius / RaySpacing));
            int added = 0;
            var c = TileOf(eye);
            if (InRange(floor, c.x, c.y) && Set(floor, c.x, c.y)) added++;
            for (int k = 0; k < rays; k++)
            {
                float a = k * (2f * Mathf.PI / rays);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float reach = radius * plan.SightShare(floor, from, from + dir * radius, m_Walls);
                for (float s = Tile * 0.5f; s <= reach; s += Tile * 0.5f)
                {
                    var p = from + dir * s;
                    if (!plan.IsInside(floor, p)) break;
                    var t = TileOf(new Vector3(p.x, 0f, p.y));
                    if (InRange(floor, t.x, t.y) && Set(floor, t.x, t.y)) added++;
                }
            }
            if (added > 0) Version++;
            return added;
        }

        /// <summary>The straight line between two points of a floor crosses no solid wall and stays inside the building.</summary>
        public static bool CanSee(LevelLayout l, int floor, Vector2 from, Vector2 to) => l.Plan != null && l.Plan.CanSee(floor, from, to);

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
