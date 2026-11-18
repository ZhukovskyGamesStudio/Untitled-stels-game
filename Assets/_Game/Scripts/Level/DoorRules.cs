using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// What a door looks like and how it moves. Slide: the office's glass sliding pair (tech rooms); Wood: a thin wooden
    /// door-and-a-half (a leaf for people and a narrow one beside it that opens for a robot); WoodDouble: two glazed wooden
    /// leaves (big rooms, a flat's living room); Entry: a flat's padded steel door; Entrance: the apartment block's thick
    /// street door; Steel: a plain steel door (the plant room, the shop); Clinic: the hospital's laminate door-and-a-half;
    /// ClinicSwing: the hospital's double doors with round windows that swing both ways; Storage, Vehicle, Vault: the
    /// storage room's, the paint booth's and the vault's own doors.
    /// </summary>
    public enum DoorKind : byte { Slide, Wood, WoodDouble, Entry, Entrance, Steel, Clinic, ClinicSwing, Storage, Vehicle, Vault }

    /// <summary>
    /// One door of the house (<see cref="LevelLayout.DoorLooks"/>, parallel to <see cref="LevelLayout.AutoDoors"/>): its kind,
    /// which way it opens (<see cref="Back"/>: towards −Z of its doorway's pose; the builder turns the prefab, whose leaves open
    /// to its own +Z) and which jamb holds the main leaf's hinges in the turned frame (<see cref="HingeRight"/>: +X).
    /// </summary>
    public struct DoorSpec
    {
        public DoorKind Kind;
        public bool Back, HingeRight;
    }

    /// <summary>
    /// Doors (pure): which doorways have one, what kind each is, which room it opens into and where its hinges are — the way a
    /// real building has them. A flat's entry door is always there and opens out onto the landing; a bathroom's opens out of
    /// it; a door off a corridor opens into the room (the corridor stays clear), between two rooms into the deeper one; the
    /// hinges go to the jamb nearer the corner, so the open leaf stands in the dead corner and the light switch is by the
    /// handle. Every swing is checked against the room's walls, stairs, furniture and the other doors' swings; a door that
    /// cannot swing clear anywhere hangs the way that clashes least.
    /// </summary>
    public static class DoorRules
    {
        /// <summary>The frame's lining on each jamb, metres: the leaves span the clear opening less two of these.</summary>
        public const float Lining = 0.05f;
        /// <summary>The main leaf's share of the span of a door-and-a-half (1.5 m span: 0.95 m + 0.55 m).</summary>
        public const float MainShare = 0.63f;
        /// <summary>A hinged leaf's thickness by kind (wood is thin, the street door thick), metres.</summary>
        public static float Thickness(DoorKind k) => k switch
        {
            DoorKind.Wood or DoorKind.WoodDouble => 0.042f,
            DoorKind.Clinic or DoorKind.ClinicSwing => 0.046f,
            DoorKind.Entry => 0.064f,
            DoorKind.Entrance => 0.075f,
            _ => 0.05f,
        };

        public static bool Hinged(DoorKind k) => k is DoorKind.Wood or DoorKind.WoodDouble or DoorKind.Entry or DoorKind.Entrance
            or DoorKind.Steel or DoorKind.Clinic or DoorKind.ClinicSwing;
        /// <summary>Two equal leaves (else a door-and-a-half: a main leaf and a narrow one).</summary>
        public static bool Double(DoorKind k) => k is DoorKind.WoodDouble or DoorKind.ClinicSwing;
        /// <summary>Swings either way, away from whoever comes (the hospital's corridor doors).</summary>
        public static bool BothWays(DoorKind k) => k == DoorKind.ClinicSwing;

        /// <summary>The leaves' widths for a clear opening: (main, second); a double door has two equal leaves.</summary>
        public static (float main, float second) Leaves(DoorKind k, float clear)
        {
            float span = clear - 2f * Lining;
            if (Double(k)) return (span / 2f, span / 2f);
            float main = Mathf.Round(span * MainShare * 100f) / 100f;
            return (main, span - main);
        }

        static bool Circulation(RoomType t) => t is RoomType.Corridor or RoomType.Hall or RoomType.Lobby or RoomType.FlatHall;
        static bool Big(PlanRoom r) => r.Area >= 30f;

        /// <summary>A flat's entry: its hallway onto the landing.</summary>
        public static bool IsFlatEntry(LevelLayout l, Doorway d)
        {
            if (l.Kind != HouseKind.Apartments || d.RoomA < 0 || d.RoomB < 0) return false;
            var a = l.Plan.Rooms[d.RoomA].Type;
            var b = l.Plan.Rooms[d.RoomB].Type;
            return (a == RoomType.FlatHall && b == RoomType.Corridor) || (b == RoomType.FlatHall && a == RoomType.Corridor);
        }

        /// <summary>A doorway that always has its door (a flat's entry, a bathroom, a toilet, a bedroom, the plant room).</summary>
        public static bool Always(LevelLayout l, Doorway d)
        {
            if (d.Kind != EdgeType.Door || d.Exterior || d.RoomA < 0 || d.RoomB < 0) return false;
            if (IsFlatEntry(l, d)) return true;
            foreach (int id in new[] { d.RoomA, d.RoomB })
                if (l.Plan.Rooms[id].Type is RoomType.Bathroom or RoomType.Restroom or RoomType.Bedroom or RoomType.Utility) return true;
            return false;
        }

        /// <summary>The chance a doorway has a door (when it is not one that always has): a flat's rooms mostly have theirs.</summary>
        public static float Chance(LevelLayout l, Doorway d, float chance)
        {
            if (Always(l, d)) return 1f;
            if (l.Kind == HouseKind.Apartments && d.RoomA >= 0 && d.RoomB >= 0
                && (l.Plan.Rooms[d.RoomA].Flat >= 0 || l.Plan.Rooms[d.RoomB].Flat >= 0)) return Mathf.Max(chance, 0.75f);
            if (l.Kind == HouseKind.Hospital) return Mathf.Max(chance, 0.6f); // wards, theatres and the corridor's swing doors
            return chance;
        }

        /// <summary>The kind of the inner door <paramref name="i"/> (index in <see cref="LevelLayout.AutoDoors"/>).</summary>
        public static DoorKind KindOf(LevelLayout l, int i, Doorway d)
        {
            if (i == l.VaultDoor) return DoorKind.Vault;
            if (i == l.StorageDoor && StorageRooms.Locked(l)) return DoorKind.Storage;
            if (i == l.BoothDoor) return DoorKind.Vehicle;
            if (d.RoomA < 0 || d.RoomB < 0) return DoorKind.Slide;
            var a = l.Plan.Rooms[d.RoomA];
            var b = l.Plan.Rooms[d.RoomB];
            bool Either(System.Func<PlanRoom, bool> f) => f(a) || f(b);
            if (Either(r => r.Type is RoomType.Utility)) return DoorKind.Steel;
            switch (l.Kind)
            {
                case HouseKind.Garage:
                    return Either(r => r.Type is RoomType.Workshop or RoomType.PaintBooth or RoomType.Storage) ? DoorKind.Steel : DoorKind.Wood;
                case HouseKind.Hospital:
                    if (Either(r => r.Type is RoomType.Operating)) return DoorKind.ClinicSwing;
                    if (Circulation(a.Type) && Circulation(b.Type)) return DoorKind.ClinicSwing; // the corridor's dividing doors
                    if (Either(r => r.Type is RoomType.Storage)) return DoorKind.Steel;
                    return DoorKind.Clinic;
                case HouseKind.Apartments:
                    if (IsFlatEntry(l, d)) return DoorKind.Entry;
                    if (Either(r => r.Type is RoomType.Living)) return DoorKind.WoodDouble;
                    if (a.Flat < 0 && b.Flat < 0) return DoorKind.Steel; // the ground floor's storeroom, the concierge
                    return DoorKind.Wood;
            }
            // offices: glass sliding doors only where the tech is; steel to storage; double doors to big rooms
            if (Either(r => r.Type is RoomType.Server or RoomType.ControlRoom or RoomType.Lab)) return DoorKind.Slide;
            if (l.Kind == HouseKind.DataCenter && Circulation(a.Type) && Circulation(b.Type)) return DoorKind.Slide;
            if (Either(r => r.Type is RoomType.Storage)) return DoorKind.Steel;
            if (Either(r => r.Type is RoomType.Restroom or RoomType.LockerRoom)) return DoorKind.Wood;
            var room = Circulation(a.Type) ? b : a;
            if (room.Type is RoomType.Meeting or RoomType.Library or RoomType.Lounge or RoomType.Gym or RoomType.OpenSpace or RoomType.GameRoom
                or RoomType.Kitchen && Big(room)) return DoorKind.WoodDouble;
            if (Circulation(a.Type) && Circulation(b.Type)) return DoorKind.WoodDouble; // a lobby onto its corridor
            return DoorKind.Wood;
        }

        /// <summary>The street door's kind: the apartment block's thick steel one, the shop's steel one, else glass leaves (null).</summary>
        public static DoorKind? StreetKind(LevelLayout l) => l.Kind switch
        {
            HouseKind.Apartments => DoorKind.Entrance,
            HouseKind.Garage => DoorKind.Steel,
            _ => null,
        };

        /// <summary>
        /// The room the door should open into, by how buildings are made (−1: no preference): out of a flat onto the landing,
        /// out of a bathroom or a closet, from a corridor into the room, between rooms into the deeper one.
        /// </summary>
        public static int PreferredInto(LevelLayout l, DoorKind kind, int a, int b)
        {
            if (a < 0 || b < 0) return -1;
            var ra = l.Plan.Rooms[a];
            var rb = l.Plan.Rooms[b];
            if (kind == DoorKind.Entry) return ra.Type == RoomType.Corridor ? a : b;   // out onto the landing
            bool SmallA = ra.Type == RoomType.Bathroom || ra.Area < 6f, SmallB = rb.Type == RoomType.Bathroom || rb.Area < 6f;
            if (SmallA != SmallB) return SmallA ? b : a;                                // out of the small room
            bool ca = Circulation(ra.Type), cb = Circulation(rb.Type);
            if (ca != cb) return ca ? b : a;                                            // into the room off the corridor
            int da = l.RoomSteps != null && a < l.RoomSteps.Length ? l.RoomSteps[a] : 0;
            int db = l.RoomSteps != null && b < l.RoomSteps.Length ? l.RoomSteps[b] : 0;
            if (da != db) return da > db ? a : b;                                       // into the deeper room
            return ra.Area <= rb.Area ? a : b;
        }

        /// <summary>A leaf's sweep in the world: the box its quarter circle stays in (x/z), on the floor of the door.</summary>
        public struct Sweep
        {
            public Rect Box;
            public int Floor;
        }

        /// <summary>
        /// The boxes the leaves of a door at <paramref name="pose"/> (its doorway's) sweep for a spec: in the turned frame the
        /// main leaf hinges on the −X jamb (+X with <see cref="DoorSpec.HingeRight"/>) and swings out to +Z.
        /// </summary>
        public static void Sweeps(Pose pose, float clear, DoorSpec spec, List<Rect> into)
        {
            var (main, second) = Leaves(spec.Kind, clear);
            var rot = pose.rotation * (spec.Back ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity);
            var x = rot * Vector3.right;
            var z = rot * Vector3.forward;
            float face = LevelMetrics.WallThickness / 2f, edge = clear / 2f - Lining;
            float s = spec.HingeRight ? -1f : 1f;
            void Box(float x0, float x1, float sign)
            {
                var p0 = pose.position + x * (s * x0) + z * (sign * face);
                var p1 = pose.position + x * (s * x1) + z * (sign * (face + Mathf.Abs(x1 - x0)));
                into.Add(Rect.MinMaxRect(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.z, p1.z), Mathf.Max(p0.x, p1.x), Mathf.Max(p0.z, p1.z)));
            }
            foreach (float sign in BothWays(spec.Kind) ? new[] { 1f, -1f } : new[] { 1f })
            {
                Box(-edge, -edge + main, sign);
                Box(edge - second, edge, sign);
            }
        }

        /// <summary>
        /// Every inner door's spec (<see cref="LevelLayout.DoorLooks"/>): its kind, then of the four ways it can hang (into
        /// either room, hinges on either jamb) the first, in order of preference, whose leaves sweep only its own room's
        /// floor — clear of stairs, furniture and the doors already hung. Called once the house is furnished.
        /// </summary>
        public static void Plan(LevelLayout l)
        {
            l.DoorLooks.Clear();
            var swept = new List<(int floor, Rect box)>();
            var boxes = new List<Rect>();
            var feet = new List<(int floor, Rect box)>();
            foreach (var p in l.Props)
                if (p.Kind != PropKind.Hatch) feet.Add((LevelLayout.FloorOf(p.Position.y), Furnisher.FootAt(p.Kind, p.Position, p.RotationY)));
            for (int i = 0; i < l.AutoDoors.Count; i++)
            {
                var pose = l.AutoDoors[i];
                var d = DoorwayAt(l, pose.position);
                var kind = KindOf(l, i, d);
                if (!Hinged(kind))
                {
                    l.DoorLooks.Add(new DoorSpec { Kind = kind });
                    continue;
                }
                float clear = LevelMetrics.ClearWidth(d.Width > 0.5f && d.Width < LevelMetrics.DoorWidth - 0.01f ? d.Width : LevelMetrics.DoorWidth);
                int floor = LevelLayout.FloorOf(pose.position.y);
                // which room lies on +Z of the pose
                var plus = l.RoomAt(pose.position + pose.rotation * Vector3.forward * 0.6f + Vector3.up * 0.3f);
                var minus = l.RoomAt(pose.position - pose.rotation * Vector3.forward * 0.6f + Vector3.up * 0.3f);
                int into = PreferredInto(l, kind, d.RoomA, d.RoomB);
                bool back = into >= 0 && minus != null && minus.Id == into && (plus == null || plus.Id != into);
                // the four ways it can hang, best first; the first that fits, else the one that clashes least
                DoorSpec chosen = default;
                int least = int.MaxValue;
                foreach (bool side in new[] { back, !back })
                {
                    var target = side ? minus : plus;
                    bool right = CornerIsRight(l, pose, side, clear, target);
                    foreach (bool hinge in new[] { right, !right })
                    {
                        var spec = new DoorSpec { Kind = kind, Back = side, HingeRight = hinge };
                        boxes.Clear();
                        Sweeps(pose, clear, spec, boxes);
                        int clashes = Clashes(l, floor, boxes, spec, plus, minus, side, swept, feet);
                        if (clashes < least) { least = clashes; chosen = spec; }
                        if (least == 0) break;
                    }
                    if (least == 0 || BothWays(kind)) break;
                }
                boxes.Clear();
                Sweeps(pose, clear, chosen, boxes);
                foreach (var b in boxes) swept.Add((floor, b));
                l.DoorLooks.Add(chosen);
            }
        }

        /// <summary>The doorway an auto door stands in.</summary>
        public static Doorway DoorwayAt(LevelLayout l, Vector3 at)
        {
            foreach (var d in l.Doorways)
                if ((d.Position - at).sqrMagnitude < 0.01f) return d;
            return new Doorway { Kind = EdgeType.Door, Position = at, Width = LevelMetrics.DoorWidth, RoomA = -1, RoomB = -1 };
        }

        /// <summary>
        /// The jamb nearer the room's corner on the side the door opens to (in the turned frame: true = +X): the hinges go there.
        /// Measured along the wall a step into the room, until the room ends.
        /// </summary>
        static bool CornerIsRight(LevelLayout l, Pose pose, bool back, float clear, PlanRoom room)
        {
            var rot = pose.rotation * (back ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity);
            var x = rot * Vector3.right;
            var z = rot * Vector3.forward;
            float Run(float s)
            {
                if (room == null) return 0f;
                for (float t = 0f; t < 6f; t += 0.25f)
                {
                    var p = pose.position + x * (s * (clear / 2f + t + 0.1f)) + z * (LevelMetrics.WallThickness / 2f + 0.3f) + Vector3.up * 0.3f;
                    if (l.RoomAt(p) != room) return t;
                }
                return 6f;
            }
            float left = Run(-1f), right = Run(1f);
            if (Mathf.Abs(left - right) < 0.01f) return ((int)(pose.position.x * 7f + pose.position.z * 13f) & 1) == 1;
            return right < left;
        }

        /// <summary>How many of the leaves' boxes leave their room's floor or run into a stair, furniture or another door's swing.</summary>
        static int Clashes(LevelLayout l, int floor, List<Rect> boxes, DoorSpec spec, PlanRoom plus, PlanRoom minus, bool back,
            List<(int floor, Rect box)> swept, List<(int floor, Rect box)> feet)
        {
            float y = LevelLayout.FloorY(floor) + 0.3f;
            int clashes = 0;
            for (int k = 0; k < boxes.Count; k++)
                if (!Clear(k)) clashes++;
            return clashes;

            bool Clear(int k)
            {
                var b = boxes[k];
                // the side this box is on: a both-ways door sweeps both rooms (the first half of its boxes is the opening side)
                bool onBack = BothWays(spec.Kind) && k >= boxes.Count / 2 ? !back : back;
                var room = onBack ? minus : plus;
                if (room == null) return false;
                // the room's own floor all over (inset: the wall face is on the edge)
                for (float u = 0.1f; u <= 0.91f; u += 0.2f)
                    for (float v = 0.1f; v <= 0.91f; v += 0.2f)
                    {
                        var p = new Vector3(b.xMin + b.width * u, y, b.yMin + b.height * v);
                        if (l.RoomAt(p) != room || l.NearStair(floor, p, 0.05f)) return false;
                    }
                var inner = new Rect(b.x + 0.02f, b.y + 0.02f, b.width - 0.04f, b.height - 0.04f);
                foreach (var (f, s) in swept) if (f == floor && s.Overlaps(inner)) return false;
                foreach (var (f, s) in feet) if (f == floor && s.Overlaps(inner)) return false;
                return true;
            }
        }

        /// <summary>How many leaf boxes of the planned doors clash (tests: none should).</summary>
        public static int CountClashes(LevelLayout l)
        {
            var swept = new List<(int floor, Rect box)>();
            var feet = new List<(int floor, Rect box)>();
            foreach (var p in l.Props)
                if (p.Kind != PropKind.Hatch) feet.Add((LevelLayout.FloorOf(p.Position.y), Furnisher.FootAt(p.Kind, p.Position, p.RotationY)));
            var boxes = new List<Rect>();
            int total = 0;
            for (int i = 0; i < l.AutoDoors.Count && i < l.DoorLooks.Count; i++)
            {
                var spec = l.DoorLooks[i];
                if (!Hinged(spec.Kind)) continue;
                var pose = l.AutoDoors[i];
                var d = DoorwayAt(l, pose.position);
                float clear = LevelMetrics.ClearWidth(d.Width > 0.5f && d.Width < LevelMetrics.DoorWidth - 0.01f ? d.Width : LevelMetrics.DoorWidth);
                int floor = LevelLayout.FloorOf(pose.position.y);
                var plus = l.RoomAt(pose.position + pose.rotation * Vector3.forward * 0.6f + Vector3.up * 0.3f);
                var minus = l.RoomAt(pose.position - pose.rotation * Vector3.forward * 0.6f + Vector3.up * 0.3f);
                boxes.Clear();
                Sweeps(pose, clear, spec, boxes);
                total += Clashes(l, floor, boxes, spec, plus, minus, spec.Back, swept, feet);
                foreach (var b in boxes) swept.Add((floor, b));
            }
            return total;
        }

        /// <summary>The spec of the inner door <paramref name="i"/>: planned with the house, else (a hand-made layout) its kind, opening to +Z.</summary>
        public static DoorSpec Of(LevelLayout l, int i)
        {
            if (i >= 0 && i < l.DoorLooks.Count) return l.DoorLooks[i];
            return new DoorSpec { Kind = KindOf(l, i, DoorwayAt(l, l.AutoDoors[i].position)) };
        }

        /// <summary>
        /// Where a door's handle is, from a point beside its doorway (pure): the unit vector along the wall from the hinges to
        /// the handle, or zero when no hinged door stands at <paramref name="center"/> (the light switch goes by the handle).
        /// </summary>
        public static Vector3 HandleWay(LevelLayout l, Vector3 center)
        {
            for (int i = 0; i < l.AutoDoors.Count && i < l.DoorLooks.Count; i++)
            {
                var pose = l.AutoDoors[i];
                var a = pose.position - center;
                a.y = 0f;
                if (a.sqrMagnitude > 0.04f || Mathf.Abs(pose.position.y - center.y) > 1f) continue;
                var spec = l.DoorLooks[i];
                if (!Hinged(spec.Kind) || BothWays(spec.Kind)) return Vector3.zero;
                var rot = pose.rotation * (spec.Back ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity);
                return rot * (spec.HingeRight ? Vector3.left : Vector3.right);
            }
            return Vector3.zero;
        }
    }
}
