using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The breaches a driller knocks in the walls between rooms. The rule (pure, unit tested): a breach is a doorway-high
    /// gap in a straight inner wall between two rooms of a floor, clear of the wall's ends and its other openings, of the
    /// stairs and the lift, of furniture on both sides — never an outer wall, never into the vault, the hidden room, the
    /// storage room or a room behind a key door (no way round a lock). The cut (every peer, from the replicated key
    /// <see cref="Game.OpenKeys.WallBreak"/>): the wall's blocks are split round the gap with a lintel left over it, the
    /// shell's faces too (the style's finish goes round it once the style is put on again), a ruin frame
    /// (<see cref="LevelArt.BreachFrame"/>) is put in it, and the plan gets the opening (the map, the rooms' graph, the
    /// robots' doorways know the new way).
    /// </summary>
    public static class WallBreaks
    {
        /// <summary>Where along a wall a breach may be: every tenth of a metre (the key holds it in decimetres).</summary>
        public const float Step = 0.1f;

        /// <summary>The clear width of a breach, metres (a ruin's, as the generator's own breaches).</summary>
        public static float Width => LevelMetrics.ClearWidth(LevelMetrics.OpeningWidth(EdgeType.Breach));

        /// <summary>A spot on a wall snapped to the breach grid.</summary>
        public static float Snap(float at) => Mathf.Round(at / Step) * Step;

        static bool Straight(PlanWall w) => Mathf.Abs(w.Dir.x) > 0.999f || Mathf.Abs(w.Dir.y) > 0.999f;

        /// <summary>Whether a breach may be knocked in wall <paramref name="wall"/> at <paramref name="at"/> metres from its A end.</summary>
        public static bool CanBreak(LevelLayout l, int wall, float at, IEnumerable<(int wall, float at)> made = null)
        {
            if (l == null || l.Plan == null || wall < 0 || wall >= l.Plan.Walls.Count) return false;
            var plan = l.Plan;
            var w = plan.Walls[wall];
            if (w.Exterior || w.Railing || w.Shaped || !Straight(w) || w.RoomA < 0 || w.RoomB < 0 || w.RoomA == w.RoomB) return false;
            if (!plan.FreeOn(wall, at, LevelMetrics.OpeningWidth(EdgeType.Breach), 0.6f)) return false;
            // not a way round a lock: the vault, the hidden room, the storage room, a room behind a key door
            foreach (int room in new[] { w.RoomA, w.RoomB })
            {
                if (room == l.VaultRoom || room == l.HiddenRoom || room == l.StorageRoom) return false;
                foreach (var k in l.KeyDoors) if (k.Rooms != null && k.Rooms.Contains(room)) return false;
            }
            float width = Width;
            if (made != null)
                foreach (var (mw, ma) in made)
                    if (mw == wall && Mathf.Abs(ma - at) < width + 1.2f) return false;
            var mid2 = w.At(at);
            var area = new Rect(mid2.x - width / 2f - 0.8f, mid2.y - width / 2f - 0.8f, width + 1.6f, width + 1.6f);
            foreach (var s in l.Stairs)
                if ((s.Floor == w.Floor || s.Floor + 1 == w.Floor) && PlanGenerator.StairArea(s, 0.3f).Overlaps(area)) return false;
            if (LevelBuilder.StairBands(w, l.Stairs).Exists(b => at + width / 2f > b.from - 0.2f && at - width / 2f < b.to + 0.2f)) return false;
            if (l.HasLift && l.LiftWell.Overlaps(area)) return false;
            // nothing standing against the wall there on either side, nothing mounted on it
            float y = LevelLayout.FloorY(w.Floor);
            var mid = new Vector3(mid2.x, y, mid2.y);
            var normal = new Vector3(-w.Dir.y, 0f, w.Dir.x);
            foreach (float side in new[] { -1f, 1f })
                for (float u = -width / 2f; u <= width / 2f + 0.01f; u += width / 2f)
                {
                    var p = mid + new Vector3(w.Dir.x, 0f, w.Dir.y) * u + normal * (side * 0.65f);
                    if (!Items.LootPlanner.ClearOfFurniture(l, p, w.Floor, 0.4f)) return false;
                }
            bool Near(Vector3 q) => Mathf.Abs(q.y - y - 1.5f) < 1.7f && new Vector2(q.x - mid.x, q.z - mid.z).magnitude < width / 2f + 0.7f;
            foreach (var c in l.CameraSpots) if (Near(c.position)) return false;
            foreach (var r in l.RobotSpawns) if (RobotRoster.IsDevice(r.Kind) && Near(r.Position)) return false;
            foreach (var c in l.VaultConsoles) if (Near(c.position)) return false;
            foreach (var d in l.ChargingDocks) if (Near(d.position)) return false;
            return true;
        }

        /// <summary>
        /// The wall a walker at <paramref name="from"/> heading for <paramref name="to"/> meets first within
        /// <paramref name="reach"/> metres, and where on it (snapped): what a driller breaks to go straight at its prey.
        /// </summary>
        public static bool Ahead(LevelLayout l, Vector3 from, Vector3 to, float reach, out int wall, out float at)
        {
            wall = -1;
            at = 0f;
            if (l == null || l.Plan == null) return false;
            int floor = LevelLayout.FloorOf(from.y);
            var p = new Vector2(from.x, from.z);
            var q = new Vector2(to.x, to.z);
            var dir = q - p;
            float length = dir.magnitude;
            if (length < 0.5f) return false;
            dir /= length;
            float best = Mathf.Min(reach, length);
            var walls = l.Plan.Walls;
            for (int i = 0; i < walls.Count; i++)
            {
                var w = walls[i];
                if (w.Floor != floor) continue;
                // the crossing of the walker's line and the wall's segment
                var e = w.B - w.A;
                float det = dir.x * e.y - dir.y * e.x;
                if (Mathf.Abs(det) < 1e-4f) continue;
                var ap = w.A - p;
                float t = (ap.x * e.y - ap.y * e.x) / det;   // along the walker's line, metres
                float u = (ap.x * dir.y - ap.y * dir.x) / det; // along the wall, 0..1
                if (t < 0f || t > best || u < 0f || u > 1f) continue;
                best = t;
                wall = i;
                at = Snap(u * w.Length);
            }
            return wall >= 0;
        }

        /// <summary>Whether the wall's line from <paramref name="at"/> is already open there (a doorway, a made breach): nothing to break.</summary>
        public static bool OpenAt(HousePlan plan, int wall, float at)
        {
            foreach (var o in plan.Openings)
            {
                if (o.Wall != wall) continue;
                if (o.Kind == EdgeType.Open) return true;
                if (o.Kind is EdgeType.Door or EdgeType.Breach && Mathf.Abs(o.At - at) < o.Width / 2f) return true;
            }
            return false;
        }

        /// <summary>A span (metres along the wall) less the breach: what is left of it on each side.</summary>
        public static List<(float from, float to)> Around(float from, float to, float gap0, float gap1)
        {
            var list = new List<(float, float)>();
            if (to <= gap0 || from >= gap1) { list.Add((from, to)); return list; }
            if (gap0 - from > 0.01f) list.Add((from, gap0));
            if (to - gap1 > 0.01f) list.Add((gap1, to));
            return list;
        }

        /// <summary>
        /// Every peer: cuts the breach into the built house — the wall's blocks, the shell's faces, the ruin frame — and
        /// into the plan. The style is put on again afterwards by the caller.
        /// </summary>
        public static void Cut(LevelLayout l, int wall, float at, int seed)
        {
            var root = ActiveLevel.Root;
            var shell = ActiveLevel.Shell;
            var art = ActiveLevel.Art;
            if (root == null || l == null || l.Plan == null || wall < 0 || wall >= l.Plan.Walls.Count) return;
            var w = l.Plan.Walls[wall];
            float y = LevelLayout.FloorY(w.Floor), width = Width, top = LevelMetrics.DoorHeight;
            float gap0 = at - width / 2f, gap1 = at + width / 2f;
            var a3 = new Vector3(w.A.x, y, w.A.y);
            var d3 = new Vector3(w.Dir.x, 0f, w.Dir.y);
            bool alongX = Mathf.Abs(d3.x) > 0.999f;
            const float t = LevelMetrics.WallThickness;

            // the blocks of this wall the gap runs through: split along the wall, a lintel left over the gap
            var pieces = new List<Transform>();
            foreach (Transform piece in root.transform)
            {
                if (piece.name != "Wall" || Quaternion.Angle(piece.rotation, Quaternion.identity) > 0.5f) continue;
                var p = piece.position;
                var s = piece.localScale;
                float across = alongX ? Mathf.Abs(p.z - a3.z) : Mathf.Abs(p.x - a3.x);
                if (across > 0.02f || Mathf.Abs((alongX ? s.z : s.x) - t) > 0.02f) continue;
                if (p.y - s.y / 2f > y + top - 0.01f || p.y + s.y / 2f < y) continue; // (a lintel, a band over the stairs: above the gap)
                float mid = Vector3.Dot(p - a3, d3), half = (alongX ? s.x : s.z) / 2f;
                if (mid + half <= gap0 + 0.001f || mid - half >= gap1 - 0.001f) continue;
                pieces.Add(piece);
            }
            foreach (var piece in pieces)
            {
                var p = piece.position;
                var s = piece.localScale;
                float mid = Vector3.Dot(p - a3, d3), half = (alongX ? s.x : s.z) / 2f;
                float y0 = p.y - s.y / 2f, y1 = p.y + s.y / 2f;
                void Part(float from, float to, float low, float high)
                {
                    if (to - from < 0.01f || high - low < 0.01f) return;
                    var go = Object.Instantiate(piece.gameObject, root.transform);
                    go.name = piece.name;
                    var c = a3 + d3 * ((from + to) / 2f);
                    go.transform.SetPositionAndRotation(new Vector3(c.x, (low + high) / 2f, c.z), piece.rotation);
                    go.transform.localScale = alongX ? new Vector3(to - from, high - low, s.z) : new Vector3(s.x, high - low, to - from);
                }
                foreach (var (from, to) in Around(mid - half, mid + half, gap0, gap1)) Part(from, to, y0, y1);
                Part(Mathf.Max(mid - half, gap0), Mathf.Min(mid + half, gap1), Mathf.Max(y0, y + top), y1);
                piece.gameObject.SetActive(false); // (out of this frame's merge and physics at once)
                Object.Destroy(piece.gameObject);
            }

            if (shell != null)
            {
                for (int i = shell.Walls.Count - 1; i >= 0; i--)
                {
                    var f = shell.Walls[i];
                    if (f.Floor != w.Floor || f.Y0 > top - 0.01f) continue;
                    float off = alongX ? Mathf.Abs(f.A.z - a3.z) + Mathf.Abs(f.B.z - a3.z) : Mathf.Abs(f.A.x - a3.x) + Mathf.Abs(f.B.x - a3.x);
                    if (off > 0.04f) continue;
                    float ua = Vector3.Dot(f.A - a3, d3), ub = Vector3.Dot(f.B - a3, d3);
                    bool flip = ub < ua;
                    float lo = Mathf.Min(ua, ub), hi = Mathf.Max(ua, ub);
                    if (hi <= gap0 + 0.001f || lo >= gap1 - 0.001f) continue;
                    shell.Walls.RemoveAt(i);
                    WallFace Piece(float from, float to, float low, float high, bool keepLo, bool keepHi)
                    {
                        var g = f;
                        var pa = a3 + d3 * from;
                        var pb = a3 + d3 * to;
                        g.A = flip ? pb : pa;
                        g.B = flip ? pa : pb;
                        g.Y0 = low;
                        g.Y1 = high;
                        bool endLo = keepLo, endHi = keepHi; // a cut end is no corner and no joint
                        if (flip) { g.CornerB &= endLo; g.JointB &= endLo; g.StraightB &= endLo; g.CornerA &= endHi; g.JointA &= endHi; g.StraightA &= endHi; }
                        else { g.CornerA &= endLo; g.JointA &= endLo; g.StraightA &= endLo; g.CornerB &= endHi; g.JointB &= endHi; g.StraightB &= endHi; }
                        return g;
                    }
                    if (gap0 - lo > 0.05f) shell.Walls.Add(Piece(lo, gap0, f.Y0, f.Y1, true, false));
                    if (hi - gap1 > 0.05f) shell.Walls.Add(Piece(gap1, hi, f.Y0, f.Y1, false, true));
                    if (f.Y1 - top > 0.05f) shell.Walls.Add(Piece(Mathf.Max(lo, gap0), Mathf.Min(hi, gap1), top, f.Y1, false, false));
                }
                var centre = a3 + d3 * at;
                shell.Openings.Add(new WallOpening { Center = centre, Along = d3, Width = width, Height = top, Squeeze = true, Floor = w.Floor });
                if (art != null && art.BreachFrame != null)
                {
                    var frame = Object.Instantiate(art.BreachFrame, centre, Quaternion.LookRotation(new Vector3(-d3.z, 0f, d3.x)), root.transform);
                    frame.name = "Breach";
                    if (frame.TryGetComponent<RuinFrame>(out var ruin)) ruin.Show(seed, shell.WallParts);
                }
            }

            // the plan and the layout know the new way (the map, the rooms' graph, the robots' doorways)
            l.Plan.Openings.Add(new PlanOpening { Wall = wall, Kind = EdgeType.Breach, At = at, Width = LevelMetrics.OpeningWidth(EdgeType.Breach) });
            var at2 = w.At(at);
            l.Doorways.Add(new Doorway
            {
                Kind = EdgeType.Breach,
                Position = new Vector3(at2.x, y, at2.y),
                Rotation = Quaternion.LookRotation(new Vector3(-w.Dir.y, 0f, w.Dir.x)),
                Width = LevelMetrics.OpeningWidth(EdgeType.Breach),
                Floor = w.Floor,
                RoomA = w.RoomA,
                RoomB = w.RoomB,
                Wall = wall,
            });
        }
    }
}
