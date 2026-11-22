using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>The furnisher: placing groups and single pieces (see <see cref="Furnisher"/>).</summary>
    public sealed partial class Furnisher
    {
        // ------------------------------------------------------------------ placing

        /// <summary>A piece of a group in world space, not yet committed.</summary>
        struct Piece { public PropKind Kind; public Vector3 Pos; public float Yaw; public bool Solid; }

        /// <summary>A group being laid out: pieces and loot spots, committed only if everything fits.</summary>
        sealed class Batch
        {
            public readonly List<Piece> Pieces = new();
            public readonly List<Vector3> Items = new();
            /// <summary>Where workers leave loot (the storage room's shelves).</summary>
            public readonly List<StorageDrop> Drops = new();
            public void Add(PropKind k, Vector3 p, float yaw, bool solid = true) => Pieces.Add(new Piece { Kind = k, Pos = p, Yaw = yaw, Solid = solid });
            public void Item(Vector3 p) => Items.Add(p);
        }

        /// <summary>Axes of a group: U along the wall (or across an island), N into the room; Yaw turns a model's front to N.</summary>
        readonly struct Frame
        {
            public readonly Vector3 O, U, N;
            public readonly float Yaw;
            public Frame(Vector3 o, Vector3 u, Vector3 n) { O = o; U = u; N = n; Yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg; }
            public Vector3 P(float u, float n, float y = 0f) => O + U * u + N * n + Vector3.up * y;
            public float Face => Yaw;
            public float Back => Yaw + 180f;
            public float AlongU => Mathf.Atan2(U.x, U.z) * Mathf.Rad2Deg;
            public float AgainstU => AlongU + 180f;
        }

        /// <summary>The footprint of a model at a pose, as a world rect (yaws are multiples of 90°, near enough).</summary>
        /// <summary>A placed prop's footprint in the world (x/z, its rotation's bounding box).</summary>
        public static Rect FootAt(PropKind k, Vector3 p, float yaw) =>
            RectAt(Foot.TryGetValue(k, out var f) ? f : Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.3f), p, yaw);

        /// <summary>
        /// A thing hung on a wall without the strip it hangs by (its back 0.3 m): what must stay off the room's other walls —
        /// coat hooks by a doorway ran into the short wall beside it (hung things were only kept off other furniture).
        /// </summary>
        static Rect HungSpan(PropKind k, Vector3 p, float yaw)
        {
            var r = Foot.TryGetValue(k, out var f) ? f : Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.3f);
            return RectAt(Rect.MinMaxRect(r.xMin, Mathf.Min(r.yMin + 0.3f, r.yMax - 0.02f), r.xMax, r.yMax), p, yaw);
        }

        static Rect RectAt(Rect r, Vector3 p, float yaw)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            foreach (var c in new[] { new Vector3(r.xMin, 0, r.yMin), new Vector3(r.xMax, 0, r.yMin), new Vector3(r.xMin, 0, r.yMax), new Vector3(r.xMax, 0, r.yMax) })
            {
                var w = p + q * c;
                min = Vector2.Min(min, new Vector2(w.x, w.z));
                max = Vector2.Max(max, new Vector2(w.x, w.z));
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>The rect lies on the floor of <paramref name="room"/> with a margin from its walls.</summary>
        /// <summary>A standing piece keeps this far from a wall's axis: the wall's face (half its thickness) and 2 cm.</summary>
        static readonly float WallClear = Mathf.Max(0.12f, LevelMetrics.WallThickness / 2f + 0.02f);

        bool OnFloor(int floor, int room, Rect r, float margin)
        {
            var g = Grow(r, margin);
            const float s = HousePlan.Res;
            for (float x = g.xMin + 0.01f; x < g.xMax; x += s * 0.5f)
                for (float z = g.yMin + 0.01f; z < g.yMax; z += s * 0.5f)
                    if (!InRoom(floor, room, x, z)) return false;
            return InRoom(floor, room, g.xMax - 0.01f, g.yMax - 0.01f) && InRoom(floor, room, g.xMax - 0.01f, g.yMin + 0.01f)
                && InRoom(floor, room, g.xMin + 0.01f, g.yMax - 0.01f);
        }

        bool InRoom(int floor, int room, float x, float z)
        {
            var c = HousePlan.CellOf(new Vector2(x, z));
            return m_P.KindAt(floor, c.x, c.y) == CellKind.Room && m_P.IdAt(floor, c.x, c.y) == room;
        }

        bool Clear(int floor, Rect r, float gap)
        {
            var g = Grow(r, gap);
            if (m_Taken[floor].Overlaps(g)) return false;
            if (m_Keep[floor].Overlaps(r)) return false;
            return true;
        }

        /// <summary>Commits a batch if every solid piece stands on the room's floor and clear of everything else.</summary>
        bool TryCommit(PlanRoom room, Batch b, float gap = 0.04f, float aisle = 0f, Vector3? front = null)
        {
            int f = room.Floor;
            if (m_Fridge && b.Pieces.Exists(p => p.Kind == PropKind.Fridge)) return false; // one fridge a kitchen
            var rects = new List<Rect>();
            // windows: a tall piece in front of one fails the group; a thing hung on the wall there is left out of it
            // (the kitchen run stays, its cupboards over the window do not) unless the group is only that
            if (m_Windows != null)
                for (int i = b.Pieces.Count - 1; i >= 0; i--)
                {
                    var p = b.Pieces[i];
                    bool tall = Tall.Contains(p.Kind), hangs = OnWall.Contains(p.Kind);
                    if ((!tall && !hangs) || !m_Windows[f].Overlaps(FootAt(p.Kind, p.Pos, p.Yaw))) continue;
                    if (tall || i == 0) return false;
                    b.Pieces.RemoveAt(i);
                }
            // one water cooler, one clock… per room
            foreach (var p in b.Pieces)
            {
                int cap = CapOf(p.Kind);
                if (cap == int.MaxValue) continue;
                int n = m_InRoom.TryGetValue(p.Kind, out int have) ? have : 0;
                foreach (var q in b.Pieces) if (q.Kind == p.Kind) n++;
                if (n > cap * m_Cap) return false;
            }
            foreach (var p in b.Pieces)
            {
                // a thing hung on the wall (cupboards, a dart board, foam): nothing to walk into, but never over a doorway
                if (!p.Solid)
                {
                    var hung = FootAt(p.Kind, p.Pos, p.Yaw);
                    if (m_Keep[f].Overlaps(Grow(hung, 0.06f)) || m_Taken[f].Overlaps(Grow(hung, -0.02f))) return false; // a thin thing: a margin round it
                    if (!OnFloor(f, room.Id, HungSpan(p.Kind, p.Pos, p.Yaw), WallClear)) return false; // its ends off the side walls
                    continue;
                }
                var r = FootAt(p.Kind, p.Pos, p.Yaw);
                if (!OnFloor(f, room.Id, r, WallClear) || !Clear(f, r, gap)) return false;
                if (front is Vector3 n)
                {
                    // a walkway in front of a group along a wall: nothing placed there already, and still the room's floor
                    var ahead = Rect.MinMaxRect(Mathf.Min(r.xMin, r.xMin + n.x * Aisle), Mathf.Min(r.yMin, r.yMin + n.z * Aisle),
                                                Mathf.Max(r.xMax, r.xMax + n.x * Aisle), Mathf.Max(r.yMax, r.yMax + n.z * Aisle));
                    if (m_Taken[f].Overlaps(ahead)) return false;
                }
                rects.Add(r);
            }
            if (aisle > 0f && rects.Count > 0)
            {
                var all = rects[0];
                foreach (var r in rects) all = Union(all, r);
                // an aisle to the other furniture and most of one to the walls; the kept spots (a doorway's own depth,
                // the square a patrol stands on) only need a step: an aisle more around them kept every table out of
                // a room under 7 × 5 m
                if (m_Taken[f].Overlaps(Grow(all, aisle)) || m_Keep[f].Overlaps(Grow(all, 0.3f)) || !OnFloor(f, room.Id, all, aisle * 0.7f)) return false;
            }
            if (!RoomToOpen(room, b)) return false; // doors and drawers: room to open, nothing put in the way of those already there
            if (rects.Count > 0 && !StaysWalkable(room, rects)) return false;
            // pieces of one group may touch each other, but not other groups
            int group = m_Groups++;
            foreach (var p in b.Pieces)
            {
                m_L.Props.Add(new PropPlacement { Kind = p.Kind, Position = p.Pos, RotationY = p.Yaw });
                m_GroupOf.Add(group);
                m_InRoom[p.Kind] = (m_InRoom.TryGetValue(p.Kind, out int have) ? have : 0) + 1;
                m_Taken[f].Add(FootAt(p.Kind, p.Pos, p.Yaw)); // hung and standing-on things too: nothing else goes under or into them
                NoteOpening(f, p.Kind, p.Pos, p.Yaw);
                if (p.Solid && InTheWay(p.Kind)) m_Solid[f].Add(FootAt(p.Kind, p.Pos, p.Yaw));
            }
            foreach (var i in b.Items) { m_L.ItemSpots.Add(i); m_ItemGroup.Add(group); }
            if (room.Id == m_L.StorageRoom) m_L.StorageDrops.AddRange(b.Drops);
            foreach (var p in b.Pieces)
            {
                m_Fridge |= p.Kind == PropKind.Fridge;
                m_Table |= p.Kind is PropKind.RoundTable or PropKind.BarCounter;
            }
            return true;
        }

        int m_Groups;
        bool[] m_WalkFree;  // the walk grid of the bare building (see Walk), built on the first check
        int[] m_WalkOwner;
        readonly List<int> m_GroupOf = new();    // per prop of the layout: the group it came with (−1: not removable)
        readonly List<int> m_ItemGroup = new();  // per loot spot

        /// <summary>
        /// Under every high vent, on both sides: an ordinary desk against the wall (the players climb it to reach the duct —
        /// furniture that happens to stand there, not a staircase someone built). Never removed by the walkability check;
        /// the space in front of it stays free so nothing blocks the climb.
        /// </summary>
        public void PlaceVentDesks()
        {
            float face = LevelMetrics.WallThickness / 2f;
            float half = Foot[PropKind.Desk].height / 2f;
            foreach (var d in m_L.Doorways)
            {
                if (!d.High) continue;
                var across = d.Rotation * Vector3.forward;
                foreach (float side in new[] { -1f, 1f })
                {
                    var n = across * side;
                    var pos = d.Position + n * (face + WallGap + half);
                    var room = m_P.RoomAt(pos + Vector3.up * 0.1f);
                    if (room == null) continue;
                    float yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg; // its front (+Z) to the room, its back to the wall
                    Put(room, PropKind.Desk, pos, yaw);
                    Keep(d.Floor, pos + n * half - d.Rotation * Vector3.right * 0.9f, pos + n * (half + LevelMetrics.VentDeskAisle) + d.Rotation * Vector3.right * 0.9f);
                }
            }
        }

        public void Put(PlanRoom room, PropKind k, Vector3 p, float yaw)
        {
            m_L.Props.Add(new PropPlacement { Kind = k, Position = p, RotationY = yaw });
            m_GroupOf.Add(-1);
            m_Taken[room.Floor].Add(FootAt(k, p, yaw));
            NoteOpening(room.Floor, k, p, yaw);
            if (InTheWay(k)) m_Solid[room.Floor].Add(FootAt(k, p, yaw));
        }
    }
}
