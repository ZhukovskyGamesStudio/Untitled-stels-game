using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The furnisher: the ways up to the ceiling grates (<see cref="DuctGrate.ClimbDir"/>), from what stands in such a room
    /// anyway — a pair of filing cabinets and a desk in an office, a card catalogue and a reading table in a library, a low
    /// cube shelf beside a dresser, a dining table, a bench elsewhere; crates only where crates are kept. Floor → the step →
    /// the top under the grate → a crouched jump into the duct.
    /// </summary>
    public sealed partial class Furnisher
    {
        /// <summary>What a room's way up is made of: the piece under the grate (one, or two side by side) and the step beside it.</summary>
        public readonly struct ClimbSet
        {
            public readonly PropKind Top, Step;
            public readonly bool Pair;
            public ClimbSet(PropKind top, PropKind step, bool pair = false) { Top = top; Step = step; Pair = pair; }
        }

        /// <summary>The way up that belongs in a room of this type (pure).</summary>
        public static ClimbSet ClimbOf(RoomType t, HouseKind house = HouseKind.Office) => t switch
        {
            RoomType.Server or RoomType.Lab or RoomType.Office or RoomType.OpenSpace or RoomType.CopyRoom or RoomType.Meeting when house == HouseKind.Hospital
                => new ClimbSet(PropKind.LowShelf, PropKind.DoctorDesk), // (no electronics' parts cabinet, no clerk's desk in a consulting room)
            RoomType.Storage or RoomType.Utility or RoomType.Workshop => new ClimbSet(PropKind.CrateStack, PropKind.WorkTable),
            RoomType.Office or RoomType.OpenSpace or RoomType.CopyRoom or RoomType.Meeting => new ClimbSet(PropKind.FilingCabinet, PropKind.Desk, true),
            RoomType.Server or RoomType.Lab => new ClimbSet(PropKind.PartsCabinet, PropKind.Desk),
            RoomType.Library => new ClimbSet(PropKind.CardCatalog, PropKind.ReadingTable),
            RoomType.Kitchen => new ClimbSet(PropKind.LowShelf, PropKind.DiningTable),
            RoomType.Bedroom => new ClimbSet(PropKind.LowShelf, PropKind.Dresser),
            RoomType.Bathroom => new ClimbSet(PropKind.LowShelf, PropKind.WashingMachine),
            RoomType.FlatHall => new ClimbSet(PropKind.LowShelf, PropKind.ShoeRack),
            RoomType.Gym or RoomType.LockerRoom or RoomType.Restroom => new ClimbSet(PropKind.LowShelf, PropKind.Bench),
            RoomType.Infirmary or RoomType.Ward or RoomType.Operating or RoomType.Imaging or RoomType.NurseStation => new ClimbSet(PropKind.LowShelf, PropKind.DoctorDesk),
            RoomType.Lounge or RoomType.Living or RoomType.GameRoom or RoomType.NapRoom => new ClimbSet(PropKind.LowShelf, PropKind.RoundTable),
            _ when house == HouseKind.Hospital => new ClimbSet(PropKind.LowShelf, PropKind.DoctorDesk), // (no office desk with its PC in a hospital)
            _ => new ClimbSet(PropKind.LowShelf, PropKind.Desk),
        };

        /// <summary>
        /// The pieces of a way up (pure): the top right under the grate, its front (doors, drawers) across the way up — to one
        /// side or, with <paramref name="flip"/>, the other —, the step beside it towards <paramref name="side"/>.
        /// </summary>
        public static List<(PropKind kind, Vector3 pos, float yaw)> ClimbPieces(RoomType type, Vector3 under, Vector3 side, bool flip, HouseKind house = HouseKind.Office)
        {
            var set = ClimbOf(type, house);
            var across = new Vector3(-side.z, 0f, side.x) * (flip ? -1f : 1f);
            float yaw = Mathf.Atan2(side.x, side.z) * Mathf.Rad2Deg, acrossYaw = Mathf.Atan2(across.x, across.z) * Mathf.Rad2Deg;
            float topYaw = set.Top == PropKind.CrateStack ? yaw : acrossYaw; // (a stack is as wide as it is across)
            var list = new List<(PropKind, Vector3, float)>();
            float reach = 0f;
            if (set.Pair)
            {
                float half = FootprintOf(set.Top).width / 2f + 0.005f;
                list.Add((set.Top, under + side * half, topYaw));
                list.Add((set.Top, under - side * half, topYaw));
                reach = half * 2f;
            }
            else
            {
                list.Add((set.Top, under, topYaw));
                reach = Reach(set.Top, topYaw, side);
            }
            // the desk's chair side to the top, as it always stood; anything else with its front (a dresser's drawers) to the way one comes
            float stepYaw = set.Step is PropKind.Desk ? yaw + 180f : yaw;
            list.Add((set.Step, under + side * (reach + 0.02f + Reach(set.Step, stepYaw, -side)), stepYaw));
            return list;
        }

        /// <summary>How far a piece's footprint reaches from its middle that way, metres.</summary>
        static float Reach(PropKind kind, float yaw, Vector3 way)
        {
            var r = FootAt(kind, Vector3.zero, yaw);
            return Mathf.Max(Vector3.Dot(new Vector3(r.xMin, 0f, r.yMin), way), Vector3.Dot(new Vector3(r.xMax, 0f, r.yMax), way));
        }

        /// <summary>The house has the whole way up at this grate (its pieces, whichever side they face).</summary>
        public static bool HasClimb(LevelLayout l, RoomType type, Vector3 under, Vector3 side)
        {
            foreach (bool flip in new[] { false, true })
            {
                bool all = true;
                foreach (var (kind, pos, yaw) in ClimbPieces(type, under, side, flip, l.PlanKind))
                {
                    bool here = false;
                    foreach (var p in l.Props) here |= p.Kind == kind && (p.Position - pos).sqrMagnitude < 0.01f && Mathf.Abs(Mathf.DeltaAngle(p.RotationY, yaw)) < 1f;
                    all &= here;
                }
                if (all) return true;
            }
            return false;
        }

        /// <summary>The ways up to vent grates (room → under the grate, the step's side; <see cref="DuctGrate.ClimbDir"/>).</summary>
        public readonly Dictionary<int, (Vector3 under, Vector3 side)> Climbs = new();

        /// <summary>
        /// The room's way up to its grate, first of all its furniture: the top under the grate, the step along its side, an
        /// aisle in front of the step kept free — set like any group (no overlap, the room stays walkable) or not at all.
        /// </summary>
        void Climb(PlanRoom room)
        {
            if (!Climbs.TryGetValue(room.Id, out var c)) return;
            foreach (bool flip in new[] { false, true })
            {
                var pieces = ClimbPieces(room.Type, c.under, c.side, flip, m_L.PlanKind);
                var b = new Batch();
                foreach (var (kind, pos, yaw) in pieces) b.Add(kind, pos, yaw);
                if (!TryCommit(room, b, 0.0f)) continue;
                foreach (var (_, pos, _) in pieces) m_L.ClimbPieces.Add(pos);
                var step = pieces[pieces.Count - 1].pos;
                Keep(room.Floor, step + c.side * 0.5f - Vector3.one * 0.55f, step + c.side * 1.5f + Vector3.one * 0.55f); // the aisle to climb from
                return;
            }
        }
    }
}
