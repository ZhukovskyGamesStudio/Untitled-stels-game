using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The soft light a room's own lamps leave in it after bouncing off its walls (pure): one level per room from the
    /// lamps that burn there and the room's size, laid out on the plan's raster so that it never crosses a wall — only an
    /// open passage, where it fades over a couple of metres. The picture adds it where nothing else lights a surface
    /// (Art/Shaders/ScreenBounce.shader); robots and the HUD's eye never read it. A room without burning lamps and
    /// without an open way to one gets nothing: dark stays dark.
    /// </summary>
    public static class RoomFill
    {
        /// <summary>Steps of spreading through open passages (a cell a step: the fade is about this many half metres).</summary>
        public const int Spread = 6;
        /// <summary>The brightest level a cell can hold.</summary>
        public const float MaxLevel = 2f;
        /// <summary>A surface is asked this far off itself, along its normal: a wall's face answers for the room it looks into.</summary>
        public const float NormalOffset = 0.15f;
        /// <summary>What a lamp's light leaves after its bounces, per unit of its intensity over a square metre of the room's surfaces.</summary>
        public const float Gain = 3f;

        public const uint BlockX = 1u << 31, BlockZ = 1u << 30, OpenUp = 1u << 29;

        /// <summary>
        /// The level of a room: its burning lamps (linear colour × intensity, summed) over everything they have to light —
        /// floor, ceiling and walls. A big hall under one lamp is dimmer than a closet under the same one.
        /// </summary>
        public static Color Level(Color lamps, float floorArea, float strength)
        {
            float area = Mathf.Max(floorArea, 1f);
            float surface = 2f * area + 4.6f * Mathf.Sqrt(area) * LevelMetrics.WallHeight;
            float k = Mathf.Max(0f, strength) * Gain / surface;
            return new Color(Mathf.Min(lamps.r * k, MaxLevel), Mathf.Min(lamps.g * k, MaxLevel), Mathf.Min(lamps.b * k, MaxLevel), 1f);
        }

        /// <summary>A cell of the buffer: its level (10, 10 and 9 bits, square-rooted) and what stands on its +x and +z sides.</summary>
        public static uint Pack(Vector3 level, uint flags)
        {
            uint r = (uint)Mathf.RoundToInt(Mathf.Sqrt(Mathf.Clamp01(level.x / MaxLevel)) * 1023f);
            uint g = (uint)Mathf.RoundToInt(Mathf.Sqrt(Mathf.Clamp01(level.y / MaxLevel)) * 1023f);
            uint b = (uint)Mathf.RoundToInt(Mathf.Sqrt(Mathf.Clamp01(level.z / MaxLevel)) * 511f);
            return flags | (r << 19) | (g << 9) | b;
        }

        public static Vector3 Unpack(uint cell)
        {
            float r = ((cell >> 19) & 1023u) / 1023f, g = ((cell >> 9) & 1023u) / 1023f, b = (cell & 511u) / 511f;
            return new Vector3(r * r, g * g, b * b) * MaxLevel;
        }

        /// <summary>The plan as the fill sees it: whose every cell is, which of its sides are shut, where no slab lies over it.</summary>
        public sealed class Grid
        {
            public readonly int Floors, NX, NZ;
            readonly int[] m_Room;
            readonly uint[] m_Flags;
            Vector3[] m_A, m_B;

            public int Count => m_Room.Length;
            int Index(int f, int x, int z) => (f * NZ + z) * NX + x;

            /// <summary>The room a cell's light belongs to (−1: outside, a sealed shaft).</summary>
            public int RoomAt(int f, int x, int z) => f < 0 || f >= Floors || x < 0 || z < 0 || x >= NX || z >= NZ ? -1 : m_Room[Index(f, x, z)];

            public Grid(HousePlan plan, IEnumerable<(int floor, Rect hole)> holes = null)
            {
                Floors = plan.Floors;
                NX = plan.NX;
                NZ = plan.NZ;
                m_Room = new int[Floors * NX * NZ];
                m_Flags = new uint[m_Room.Length];
                for (int f = 0; f < Floors; f++)
                    for (int z = 0; z < NZ; z++)
                        for (int x = 0; x < NX; x++)
                            // a room's floor, the air of an atrium over it, the pocket behind its own slanted wall (the wall's
                            // face is asked a little off itself, and that may fall there)
                            m_Room[Index(f, x, z)] = plan.KindAt(f, x, z) == CellKind.Outside ? -1 : plan.IdAt(f, x, z);
                for (int f = 0; f < Floors; f++)
                    for (int z = 0; z < NZ; z++)
                        for (int x = 0; x < NX; x++)
                        {
                            int i = Index(f, x, z), room = m_Room[i];
                            if (room < 0 || RoomAt(f, x + 1, z) != room) m_Flags[i] |= BlockX;
                            if (room < 0 || RoomAt(f, x, z + 1) != room) m_Flags[i] |= BlockZ;
                            if (plan.KindAt(f + 1, x, z) == CellKind.Void) m_Flags[i] |= OpenUp;
                        }
                if (holes != null)
                    foreach (var (floor, hole) in holes)
                    {
                        if (floor < 0 || floor >= Floors) continue;
                        for (int z = Mathf.FloorToInt(hole.yMin / HousePlan.Res); z <= Mathf.FloorToInt((hole.yMax - 0.01f) / HousePlan.Res); z++)
                            for (int x = Mathf.FloorToInt(hole.xMin / HousePlan.Res); x <= Mathf.FloorToInt((hole.xMax - 0.01f) / HousePlan.Res); x++)
                                if (RoomAt(floor, x, z) >= 0) m_Flags[Index(floor, x, z)] |= OpenUp;
                    }
                // what stands open in a wall: a passage without a door, a hole broken through, the railing of a gallery
                foreach (var w in plan.Walls)
                    if (w.Railing && !w.Shaped) Unblock(w, w.Length / 2f, w.Length / 2f);
                foreach (var o in plan.Openings)
                {
                    if (o.Kind is not (EdgeType.Open or EdgeType.Breach) || o.Wall < 0 || o.Wall >= plan.Walls.Count) continue;
                    var w = plan.Walls[o.Wall];
                    if (!w.Shaped) Unblock(w, o.At, Mathf.Min(LevelMetrics.ClearWidth(o.Width), w.Length) / 2f);
                }
            }

            /// <summary>Opens the cells' sides that lie on a grid wall within <paramref name="half"/> of a point of it.</summary>
            void Unblock(PlanWall w, float at, float half)
            {
                var mid = w.At(at);
                bool alongZ = Mathf.Abs(w.A.x - w.B.x) < 0.01f;
                int line = Mathf.RoundToInt((alongZ ? w.A.x : w.A.y) / HousePlan.Res);
                float centre = alongZ ? mid.y : mid.x;
                for (int k = Mathf.FloorToInt((centre - half) / HousePlan.Res); k <= Mathf.FloorToInt((centre + half) / HousePlan.Res); k++)
                {
                    float c = (k + 0.5f) * HousePlan.Res;
                    if (c < centre - half || c > centre + half) continue;
                    int x = alongZ ? line - 1 : k, z = alongZ ? k : line - 1;
                    if (RoomAt(w.Floor, x, z) < 0 || RoomAt(w.Floor, alongZ ? x + 1 : x, alongZ ? z : z + 1) < 0) continue;
                    m_Flags[Index(w.Floor, x, z)] &= ~(alongZ ? BlockX : BlockZ);
                }
            }

            /// <summary>
            /// The cells of the house for the picture: every cell its room's level, then spread through what stands open
            /// (<see cref="Spread"/> steps) — inside a room nothing changes, across a wall nothing passes.
            /// </summary>
            public void Bake(IReadOnlyList<Color> rooms, uint[] into)
            {
                m_A ??= new Vector3[Count];
                m_B ??= new Vector3[Count];
                for (int i = 0; i < Count; i++)
                {
                    int room = m_Room[i];
                    m_A[i] = room >= 0 && room < rooms.Count ? new Vector3(rooms[room].r, rooms[room].g, rooms[room].b) : Vector3.zero;
                }
                for (int step = 0; step < Spread; step++)
                {
                    for (int f = 0; f < Floors; f++)
                        for (int z = 0; z < NZ; z++)
                            for (int x = 0; x < NX; x++)
                            {
                                int i = Index(f, x, z);
                                if (m_Room[i] < 0) { m_B[i] = Vector3.zero; continue; }
                                var sum = m_A[i] * 2f;
                                float n = 2f;
                                if ((m_Flags[i] & BlockX) == 0) { sum += m_A[i + 1]; n++; }
                                if ((m_Flags[i] & BlockZ) == 0) { sum += m_A[i + NX]; n++; }
                                if (x > 0 && (m_Flags[i - 1] & BlockX) == 0) { sum += m_A[i - 1]; n++; }
                                if (z > 0 && (m_Flags[i - NX] & BlockZ) == 0) { sum += m_A[i - NX]; n++; }
                                m_B[i] = sum / n;
                            }
                    (m_A, m_B) = (m_B, m_A);
                }
                for (int i = 0; i < Count; i++) into[i] = Pack(m_A[i], m_Flags[i]);
            }

            /// <summary>
            /// What the picture reads at a point of a surface facing <paramref name="normal"/> — the shader's own steps
            /// (ScreenBounce.shader, FillAt): the cell the point is in and its three neighbours towards the point, each
            /// only if nothing stands between. Tests hold the shader's rule to this.
            /// </summary>
            public Vector3 Sample(uint[] cells, Vector3 point, Vector3 normal)
            {
                var q = point + normal * NormalOffset;
                int f = Mathf.FloorToInt(q.y / LevelMetrics.FloorHeight);
                float gx = q.x / HousePlan.Res, gz = q.z / HousePlan.Res;
                int hx = Mathf.FloorToInt(gx), hz = Mathf.FloorToInt(gz);
                if (RoomAt(f, hx, hz) < 0) return Vector3.zero;
                uint home = cells[Index(f, hx, hz)];
                if (q.y - f * LevelMetrics.FloorHeight > LevelMetrics.WallHeight + 0.05f && (home & OpenUp) == 0) return Vector3.zero; // in the slab: the ducts
                float fx = gx - hx - 0.5f, fz = gz - hz - 0.5f;
                int sx = fx >= 0f ? 1 : -1, sz = fz >= 0f ? 1 : -1;
                float tx = Mathf.Abs(fx), tz = Mathf.Abs(fz);
                bool openX = Open(cells, f, hx, hz, sx, 0), openZ = Open(cells, f, hx, hz, 0, sz);
                bool openD = (openX && Open(cells, f, hx + sx, hz, 0, sz)) || (openZ && Open(cells, f, hx, hz + sz, sx, 0));
                var c = Unpack(home);
                var cx = openX ? Unpack(cells[Index(f, hx + sx, hz)]) : c;
                var cz = openZ ? Unpack(cells[Index(f, hx, hz + sz)]) : c;
                var cd = openD ? Unpack(cells[Index(f, hx + sx, hz + sz)]) : (cx + cz) * 0.5f;
                return Vector3.Lerp(Vector3.Lerp(c, cx, tx), Vector3.Lerp(cz, cd, tx), tz);
            }

            /// <summary>Nothing stands between a cell and its neighbour one step along x or z.</summary>
            bool Open(uint[] cells, int f, int x, int z, int dx, int dz)
            {
                int lx = dx < 0 ? x - 1 : x, lz = dz < 0 ? z - 1 : z; // the cell whose +side that is
                if (RoomAt(f, x, z) < 0 || RoomAt(f, x + dx, z + dz) < 0) return false;
                return (cells[Index(f, lx, lz)] & (dx != 0 ? BlockX : BlockZ)) == 0;
            }
        }
    }
}
