using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Which rooms of a house an eye can see into (pure, unit tested; reads the plan only, so it may run off the main
    /// thread — one call at a time). A fan of rays on the eye's floor, as the team's map is revealed
    /// (<see cref="HousePlan.SightShare"/>): a ray stops at a solid wall and goes through doorways, vents, slots and open
    /// passages. A doorway whose door is shut stops it too (<see cref="Shut"/>); a shut door with a vision slot or a
    /// porthole only from farther than <see cref="SlotDistance"/> — up close one looks through it. An outer wall counts
    /// as a window: the ray crosses the yard and marks the room it enters across it. Stairs and atria show the storey
    /// above and below, from the middle of the room they arrive in; so do the floors right under and over the eye (a hole
    /// broken through a slab). Erring towards seeing: the furniture of a room not seen is not drawn (<see cref="RoomCulling"/>).
    /// </summary>
    public sealed class RoomSight
    {
        /// <summary>What stands in an opening (per plan opening): nothing or an open door; a shut door with glass to peep through; a shut blind door.</summary>
        public const byte Open = 0, ShutSlot = 1, Shut = 2;

        /// <summary>How far an eye sees along its floor, metres (a house is not longer).</summary>
        public const float Reach = 90f;
        /// <summary>How far it sees on the storey a stair or an atrium opens, from that room's middle.</summary>
        public const float StairReach = 30f;
        /// <summary>A shut door's slot or porthole shows the room behind it only this near, metres (farther it is a few pixels).</summary>
        public const float SlotDistance = 8f;
        /// <summary>The rooms behind the doorways this near the eye are seen whatever the rays make of them (a door beside the eye, at a glancing angle), metres.</summary>
        public const float NearDoor = 6f;
        /// <summary>The floors under and over the eye are seen this far around it, metres (a hole broken through the slab).</summary>
        const float ThroughSlab = 6f;
        /// <summary>How deep into a wing across the yard a ray marks rooms (through its window).</summary>
        const float AcrossYard = 4f;
        /// <summary>Rays of a fan: 0.4° apart — half a metre at 70 m, less than a doorway seen square on.</summary>
        const int Rays = 900;
        const float Step = HousePlan.Res * 0.5f;

        struct Gap
        {
            /// <summary>The middle along the wall and half the clear width (a whole open wall: an endless half).</summary>
            public float At, Half;
            public int Opening;
            public Vector2 Middle;
        }

        readonly LevelLayout m_L;
        readonly HousePlan m_P;
        readonly List<int>[] m_Walls;
        readonly Vector2[] m_Dirs = new Vector2[Rays];
        readonly float[] m_Open = new float[Rays];
        readonly List<Gap>[] m_Gaps;
        readonly int[] m_DoorwayOpening;

        /// <summary>Rooms of the plan (the length of the array <see cref="See"/> fills).</summary>
        public int Rooms => m_P.Rooms.Count;
        /// <summary>Openings of the plan (the length of the doors' array <see cref="See"/> takes).</summary>
        public int Openings => m_P.Openings.Count;

        /// <summary>What a shut door of this kind is to the eye (pure): glazed leaves hide nothing, a slot or a porthole only from afar.</summary>
        public static byte ShutState(DoorKind kind) => kind switch
        {
            DoorKind.Slide or DoorKind.WoodDouble => Open,
            DoorKind.Clinic or DoorKind.ClinicSwing or DoorKind.Steel or DoorKind.Entrance => ShutSlot,
            _ => Shut,
        };

        public RoomSight(LevelLayout l)
        {
            m_L = l;
            m_P = l.Plan;
            m_Walls = new List<int>[m_P.Floors];
            for (int f = 0; f < m_P.Floors; f++) m_Walls[f] = new List<int>();
            for (int i = 0; i < m_P.Walls.Count; i++)
            {
                var w = m_P.Walls[i];
                if (!w.Railing && w.Floor >= 0 && w.Floor < m_P.Floors) m_Walls[w.Floor].Add(i);
            }
            m_Gaps = new List<Gap>[m_P.Walls.Count];
            for (int i = 0; i < m_P.Openings.Count; i++)
            {
                var o = m_P.Openings[i];
                if (o.Kind == EdgeType.Illusion || o.Wall < 0 || o.Wall >= m_Gaps.Length) continue; // (an illusion looks like a wall)
                var w = m_P.Walls[o.Wall];
                (m_Gaps[o.Wall] ??= new List<Gap>()).Add(new Gap
                {
                    At = o.At, Half = o.Kind == EdgeType.Open ? float.PositiveInfinity : LevelMetrics.ClearWidth(o.Width) / 2f,
                    Opening = i, Middle = w.A + w.Dir * o.At,
                });
            }
            m_DoorwayOpening = new int[l.Doorways.Count];
            for (int i = 0; i < m_DoorwayOpening.Length; i++) m_DoorwayOpening[i] = OpeningAt(l.Doorways[i].Position);
            for (int k = 0; k < Rays; k++)
            {
                float a = k * (2f * Mathf.PI / Rays);
                m_Dirs[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
        }

        /// <summary>The plan's opening a door stands in (by its place on its floor), −1: none there.</summary>
        public int OpeningAt(Vector3 position)
        {
            int floor = LevelLayout.FloorOf(position.y + 0.3f);
            var p = new Vector2(position.x, position.z);
            int best = -1;
            float near = 0.45f * 0.45f;
            for (int i = 0; i < m_P.Openings.Count; i++)
            {
                var o = m_P.Openings[i];
                if (o.Wall < 0 || o.Wall >= m_P.Walls.Count) continue;
                var w = m_P.Walls[o.Wall];
                if (w.Floor != floor) continue;
                float d = (w.A + w.Dir * o.At - p).sqrMagnitude;
                if (d < near) { near = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Marks the rooms seen from <paramref name="eye"/> (by plan id); <paramref name="doors"/>: what stands in each
        /// opening of the plan (<see cref="Open"/>, <see cref="ShutSlot"/>, <see cref="Shut"/>; null: every door open).
        /// False: the eye is not inside the house (in the yard, on the street) — from there everything shows.
        /// </summary>
        public bool See(Vector3 eye, bool[] seen, byte[] doors = null)
        {
            System.Array.Clear(seen, 0, seen.Length);
            int floor = Mathf.Clamp(LevelLayout.FloorOf(eye.y), 0, m_P.Floors - 1);
            var from = new Vector2(eye.x, eye.z);
            if (!m_P.IsInside(floor, from)) return false;
            Fan(floor, from, Reach, seen, doors);
            // the eye's own room, and the rooms behind the doorways right by the eye
            Mark(seen, m_P.SpaceAt(floor, from));
            for (int i = 0; i < m_L.Doorways.Count; i++)
            {
                var d = m_L.Doorways[i];
                if (d.Floor != floor || (new Vector2(d.Position.x, d.Position.z) - from).sqrMagnitude > NearDoor * NearDoor) continue;
                int o = m_DoorwayOpening[i];
                if (doors != null && o >= 0 && doors[o] == Shut) continue;
                Mark(seen, d.RoomA);
                Mark(seen, d.RoomB);
            }
            // right under and over the eye: a hole broken through the slab shows the room there
            for (float x = -ThroughSlab; x <= ThroughSlab; x += 1f)
                for (float z = -ThroughSlab; z <= ThroughSlab; z += 1f)
                {
                    if (x * x + z * z > ThroughSlab * ThroughSlab) continue;
                    var c = HousePlan.CellOf(from + new Vector2(x, z));
                    for (int f = floor - 1; f <= floor + 1; f += 2)
                        if (m_P.KindAt(f, c.x, c.y) == CellKind.Room) Mark(seen, m_P.IdAt(f, c.x, c.y));
                }
            // a storey up and down: through the stairs seen…
            foreach (var s in m_L.Stairs)
            {
                if (s.Floor == floor && Is(seen, s.Room)) Storey(s.RoomAbove, floor + 1, seen, doors);
                else if (s.Floor + 1 == floor && Is(seen, s.RoomAbove)) Storey(s.Room, floor - 1, seen, doors);
            }
            // …and every storey of an atrium seen (its galleries look into it)
            for (int i = 0; i < m_P.Rooms.Count; i++)
            {
                var r = m_P.Rooms[i];
                if (r.Levels <= 1 || !seen[i]) continue;
                var middle = new Vector2(r.Center.x, r.Center.z);
                for (int f = r.Floor; f < r.Floor + r.Levels && f < m_P.Floors; f++)
                    if (f != floor) Fan(f, middle, StairReach, seen, doors);
            }
            return true;
        }

        /// <summary>The storey a stair opens: the room it arrives in and what is seen from that room's middle.</summary>
        void Storey(int room, int floor, bool[] seen, byte[] doors)
        {
            if (room < 0 || room >= m_P.Rooms.Count) return;
            Mark(seen, room);
            var c = m_P.Rooms[room].Center;
            Fan(floor, new Vector2(c.x, c.z), StairReach, seen, doors);
        }

        static void Mark(bool[] seen, int room)
        {
            if (room >= 0 && room < seen.Length) seen[room] = true;
        }

        static bool Is(bool[] seen, int room) => room >= 0 && room < seen.Length && seen[room];

        void Fan(int f, Vector2 from, float reach, bool[] seen, byte[] doors)
        {
            if (f < 0 || f >= m_P.Floors || !m_P.IsInside(f, from)) return;
            // how far each ray goes before a solid wall: every wall against the rays that point at it only (a wall spans
            // a few of the nine hundred; all of them against every wall was 70 ms a look)
            for (int k = 0; k < Rays; k++) m_Open[k] = reach;
            const float perRay = Rays / (2f * Mathf.PI);
            foreach (int i in m_Walls[f])
            {
                var w = m_P.Walls[i];
                var a = w.A - from;
                var b = w.B - from;
                float cross = Mathf.Abs(a.x * b.y - a.y * b.x), length = Mathf.Max(1e-4f, (b - a).magnitude);
                if (cross / length > reach) continue; // (its line is farther than the rays go)
                int first = 0, count = Rays;
                // (the eye on the wall's own line, at a doorway: the angles say nothing — every ray)
                if (cross > 0.02f * length)
                {
                    float a0 = Mathf.Atan2(a.y, a.x), a1 = Mathf.Atan2(b.y, b.x);
                    float turn = Mathf.Repeat(a1 - a0 + Mathf.PI, 2f * Mathf.PI) - Mathf.PI; // the short way round from A to B
                    float start = turn >= 0f ? a0 : a1;
                    first = Mathf.FloorToInt(Mathf.Repeat(start, 2f * Mathf.PI) * perRay) - 1;
                    count = Mathf.CeilToInt(Mathf.Abs(turn) * perRay) + 3;
                }
                var gaps = m_Gaps[i];
                for (int n = 0; n < count; n++)
                {
                    int k = ((first + n) % Rays + Rays) % Rays;
                    if (!HousePlan.Intersects(from, from + m_Dirs[k] * reach, w.A, w.B, out float along)) continue;
                    bool through = false;
                    if (gaps != null)
                        foreach (var g in gaps)
                        {
                            if (Mathf.Abs(along - g.At) > g.Half) continue;
                            byte door = doors != null ? doors[g.Opening] : Open;
                            through |= door == Open || (door == ShutSlot && (g.Middle - from).sqrMagnitude <= SlotDistance * SlotDistance);
                        }
                    if (through) continue;
                    float hit = (w.A + w.Dir * along - from).magnitude;
                    if (hit < m_Open[k]) m_Open[k] = hit;
                }
            }
            for (int k = 0; k < Rays; k++)
            {
                var dir = m_Dirs[k];
                float open = m_Open[k];
                bool outside = false;
                float s = 0f;
                // the open stretch: every room (and atrium void) the ray passes over
                for (; s <= open; s += Step)
                {
                    var c = HousePlan.CellOf(from + dir * s);
                    var kind = m_P.KindAt(f, c.x, c.y);
                    outside = kind == CellKind.Outside;
                    if (kind == CellKind.Room || kind == CellKind.Void) Mark(seen, m_P.IdAt(f, c.x, c.y));
                }
                // past the wall that stopped it: an outer wall is a window — across the yard, into the wing beyond
                float inside = -1f;
                for (; s <= reach; s += Step)
                {
                    var c = HousePlan.CellOf(from + dir * s);
                    var kind = m_P.KindAt(f, c.x, c.y);
                    if (kind == CellKind.Outside)
                    {
                        if (inside >= 0f) break; // (out of that wing again)
                        outside = true;
                        continue;
                    }
                    if (kind != CellKind.Room && kind != CellKind.Void) break;
                    if (!outside) break; // an inner wall: nothing is seen through it
                    Mark(seen, m_P.IdAt(f, c.x, c.y));
                    if (inside < 0f) inside = s;
                    if (s - inside > AcrossYard) break;
                }
            }
        }
    }
}
