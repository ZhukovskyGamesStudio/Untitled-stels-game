using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The furnisher: furniture that opens (see <see cref="Furnisher"/>). A cupboard's doors and a chest's drawers need the
    /// strip of floor in front of them (<see cref="SwingAt"/>): it stays the room's own floor — such a piece never stands
    /// with its front to a wall — and free of other furniture, and nothing is put there later; two such pieces never open
    /// into each other. Chairs and bins do not count: one shoves them aside. The same strip is kept before what is looked
    /// at from its front (<see cref="Faced"/>: a logo totem, a kiosk, a board on a stand): a totem at the end of one wall
    /// stood with its screen a hand's breadth from the side of the lockers on the next.
    /// </summary>
    public sealed partial class Furnisher
    {
        /// <summary>
        /// Furniture whose doors or drawers open for real (Items.Storage on its prefab: checked by a test), at its front
        /// (the model's +Z).
        /// </summary>
        public static readonly HashSet<PropKind> Opens = new()
        {
            PropKind.Cabinet, PropKind.Lockers, PropKind.KitchenCounter, PropKind.Fridge, PropKind.TvStand,
            PropKind.WallCabinet, PropKind.MedicineCabinet, PropKind.PartsCabinet, PropKind.MailShelf, PropKind.PantryCabinet,
            PropKind.Wardrobe, PropKind.Dresser, PropKind.ShoeCabinet,
            PropKind.KitchenSink, PropKind.BathVanity, PropKind.MirrorSink, PropKind.Dishwasher, PropKind.DoubleSink, PropKind.Credenza, PropKind.AvCredenza,
            PropKind.FilingCabinet, PropKind.FilePedestal, PropKind.ExecutiveDesk, PropKind.NightTable,
        };

        /// <summary>Standing pieces with a face to be seen (their front, the model's +Z): it keeps a strip of floor clear before it, like a cupboard's doors.</summary>
        public static readonly HashSet<PropKind> Faced = new()
        {
            PropKind.LogoTotem, PropKind.InfoKiosk, PropKind.DirectoryBoard, PropKind.Flipchart, PropKind.MobileWhiteboard, PropKind.MapBoard,
            PropKind.VendingMachine, PropKind.Jukebox, PropKind.DisplayCase, PropKind.MagazineRack,
        };

        /// <summary>The piece keeps the floor before its front clear: it opens there, or it is looked at from there.</summary>
        public static bool NeedsFront(PropKind k) => Opens.Contains(k) || Faced.Contains(k);

        /// <summary>Things one shoves aside to get at a door or a drawer: they may stand in front of it (not an armchair: a
        /// row of them in front of a cupboard read as the cupboard opening into them).</summary>
        static readonly HashSet<PropKind> Shoved = new()
        {
            PropKind.Chair, PropKind.MeetingChair, PropKind.OperatorChair, PropKind.BarStool, PropKind.Wheelchair,
            PropKind.TrashBin, PropKind.PaperBin, PropKind.SanitaryBin,
        };

        /// <summary>
        /// How far a piece opens in front of itself, metres: a drawer's travel (Items.StorageMath.DrawerTravel 0.38) and a
        /// narrow door's leaf with a little to spare; the wide leaves of a wardrobe, a fridge, a pantry and lockers reach further.
        /// </summary>
        public static float OpenReach(PropKind k) =>
            k is PropKind.Wardrobe or PropKind.Fridge or PropKind.PantryCabinet or PropKind.Lockers ? 0.55f : Faced.Contains(k) ? 0.7f : 0.45f;

        /// <summary>The strip of floor a placed piece opens into (world x/z): as wide as the piece, <see cref="OpenReach"/> deep in front of it.</summary>
        public static Rect SwingAt(PropKind k, Vector3 p, float yaw)
        {
            var r = FootprintOf(k);
            const float side = 0.03f; // (neighbours along one wall touch: their strips do not overlap)
            return RectAt(Rect.MinMaxRect(r.xMin + side, r.yMax + 0.01f, r.xMax - side, r.yMax + OpenReach(k)), p, yaw);
        }

        /// <summary>The piece hangs on the wall over what stands under it: its doors swing over counters, into tall things only.</summary>
        static bool OpensHigh(PropKind k) => OnWall.Contains(k) || Hung.Contains(k);

        /// <summary>
        /// A piece of kind <paramref name="other"/> standing in the strip of <paramref name="opener"/> blocks it (pure): not
        /// what is shoved aside, lies flat or sits on a top; a cupboard up on the wall is blocked only by tall things and by
        /// what hangs there too.
        /// </summary>
        public static bool BlocksOpening(PropKind opener, PropKind other)
        {
            if (Shoved.Contains(other)) return false;
            if (other is PropKind.AreaRug or PropKind.BayLines or PropKind.DeskLamp or PropKind.CoffeeMachine or PropKind.Microwave or PropKind.Hatch
                or PropKind.ShaftHatch or PropKind.BedCurtain or PropKind.SurgicalLights) return false;
            bool otherHigh = OpensHigh(other) || OnWall.Contains(other) || Hung.Contains(other);
            if (OpensHigh(opener)) return otherHigh || Tall.Contains(other);
            return !otherHigh;
        }

        /// <summary>The rect is the floor of <paramref name="room"/> on storey <paramref name="floor"/>, <paramref name="margin"/> off its walls' axes (pure).</summary>
        public static bool OnRoomFloor(HousePlan plan, int floor, int room, Rect r, float margin)
        {
            var g = Grow(r, margin);
            const float s = HousePlan.Res * 0.5f;
            bool In(float x, float z)
            {
                var c = HousePlan.CellOf(new Vector2(x, z));
                return plan.KindAt(floor, c.x, c.y) == CellKind.Room && plan.IdAt(floor, c.x, c.y) == room;
            }
            for (float x = g.xMin + 0.01f; x < g.xMax; x += s)
                for (float z = g.yMin + 0.01f; z < g.yMax; z += s)
                    if (!In(x, z)) return false;
            return In(g.xMax - 0.01f, g.yMax - 0.01f) && In(g.xMax - 0.01f, g.yMin + 0.01f) && In(g.xMin + 0.01f, g.yMax - 0.01f);
        }

        /// <summary>
        /// What is wrong with the furniture that opens in a finished house (pure; empty: nothing): a piece whose front is
        /// to a wall (<c>other</c> −1) or whose strip another piece stands in (<c>other</c>: its index in the layout's props).
        /// </summary>
        public static List<(int prop, int other)> OpeningClashes(LevelLayout l)
        {
            var bad = new List<(int, int)>();
            var props = l.Props;
            var floors = new int[props.Count];
            var feet = new Rect[props.Count];
            for (int i = 0; i < props.Count; i++)
            {
                floors[i] = LevelLayout.FloorOf(props[i].Position.y + 0.1f);
                feet[i] = l.KioskTerminals.Contains(i) || l.StorageTerminal == i // a map kiosk or the storage door's access stand in place of a cupboard: a stand, not the desk a terminal usually is
                    ? new Rect(props[i].Position.x - 0.3f, props[i].Position.z - 0.3f, 0.6f, 0.6f)
                    : Scaled(FootAt(props[i].Kind, props[i].Position, props[i].RotationY), props[i]);
            }
            for (int i = 0; i < props.Count; i++)
            {
                var p = props[i];
                if (!Opens.Contains(p.Kind) || floors[i] < 0) continue;
                var room = l.Plan.RoomAt(new Vector3(p.Position.x, LevelLayout.FloorY(floors[i]) + 0.1f, p.Position.z));
                if (room == null) continue;
                var swing = SwingAt(p.Kind, p.Position, p.RotationY);
                if (!OnRoomFloor(l.Plan, floors[i], room.Id, swing, WallClear)) { bad.Add((i, -1)); continue; }
                for (int j = 0; j < props.Count; j++)
                {
                    if (j == i || floors[j] != floors[i] || !BlocksOpening(p.Kind, props[j].Kind)) continue;
                    if (feet[j].Overlaps(swing)) { bad.Add((i, j)); break; }
                    // two pieces opening into each other: their strips cross (not two side by side along one wall)
                    if (j > i && Opens.Contains(props[j].Kind) && BlocksOpening(props[j].Kind, p.Kind)
                        && SwingAt(props[j].Kind, props[j].Position, props[j].RotationY).Overlaps(swing)) { bad.Add((i, j)); break; }
                }
            }
            return bad;
        }

        // ------------------------------------------------------------------ while furnishing

        // per storey: the footprints that stand in the way of a low piece's doors and drawers / of a wall cupboard's doors,
        // and the strips the placed pieces open into (low and up on the wall)
        RectGrid[] m_LowAt, m_HighAt, m_SwingLow, m_SwingHigh;

        static bool BlocksLow(PropKind k) => BlocksOpening(PropKind.Cabinet, k);
        static bool BlocksHigh(PropKind k) => BlocksOpening(PropKind.WallCabinet, k);

        void InitOpening(int floors)
        {
            m_LowAt = new RectGrid[floors];
            m_HighAt = new RectGrid[floors];
            m_SwingLow = new RectGrid[floors];
            m_SwingHigh = new RectGrid[floors];
            for (int f = 0; f < floors; f++)
            {
                m_LowAt[f] = new RectGrid();
                m_HighAt[f] = new RectGrid();
                m_SwingLow[f] = new RectGrid();
                m_SwingHigh[f] = new RectGrid();
            }
        }

        /// <summary>A piece now stands here: what it is in the way of, and the strip it opens into.</summary>
        void NoteOpening(int f, PropKind k, Vector3 p, float yaw)
        {
            if (f < 0 || f >= m_LowAt.Length) return;
            var r = FootAt(k, p, yaw);
            if (BlocksLow(k)) m_LowAt[f].Add(r);
            if (BlocksHigh(k)) m_HighAt[f].Add(r);
            if (NeedsFront(k)) (OpensHigh(k) ? m_SwingHigh : m_SwingLow)[f].Add(SwingAt(k, p, yaw));
        }

        /// <summary>After groups were taken out: the same from the furniture that is left.</summary>
        void RenoteOpening()
        {
            for (int f = 0; f < m_LowAt.Length; f++)
            {
                m_LowAt[f].Clear();
                m_HighAt[f].Clear();
                m_SwingLow[f].Clear();
                m_SwingHigh[f].Clear();
            }
            foreach (var p in m_L.Props) NoteOpening(LevelLayout.FloorOf(p.Position.y + 0.1f), p.Kind, p.Position, p.RotationY);
        }

        /// <summary>
        /// A group may go in: none of its pieces stands in the strip a piece already there opens into, and each of its own
        /// pieces that opens has its strip on the room's floor (its front is not to a wall), free of the furniture there and
        /// of the group's other pieces, and crossing no other piece's strip (two never open into each other).
        /// </summary>
        bool RoomToOpen(PlanRoom room, Batch b)
        {
            int f = room.Floor;
            for (int i = 0; i < b.Pieces.Count; i++)
            {
                var p = b.Pieces[i];
                var r = FootAt(p.Kind, p.Pos, p.Yaw);
                if (BlocksLow(p.Kind) && m_SwingLow[f].Overlaps(r)) return false;
                if (BlocksHigh(p.Kind) && m_SwingHigh[f].Overlaps(r)) return false;
                if (!NeedsFront(p.Kind)) continue;
                bool high = OpensHigh(p.Kind);
                var s = SwingAt(p.Kind, p.Pos, p.Yaw);
                if (!OnFloor(f, room.Id, s, WallClear)) return false;
                if ((high ? m_HighAt : m_LowAt)[f].Overlaps(s) || (high ? m_SwingHigh : m_SwingLow)[f].Overlaps(s)) return false;
                for (int j = 0; j < b.Pieces.Count; j++)
                {
                    if (j == i) continue;
                    var q = b.Pieces[j];
                    if (!BlocksOpening(p.Kind, q.Kind)) continue;
                    if (FootAt(q.Kind, q.Pos, q.Yaw).Overlaps(s)) return false;
                    if (j > i && NeedsFront(q.Kind) && BlocksOpening(q.Kind, p.Kind) && SwingAt(q.Kind, q.Pos, q.Yaw).Overlaps(s)) return false;
                }
            }
            return true;
        }

        /// <summary>A prop's footprint with its own scale (a point of interest shrunk to the piece it replaced).</summary>
        static Rect Scaled(Rect r, PropPlacement p)
        {
            float k = p.Scale > 0f ? p.Scale : 1f;
            if (Mathf.Approximately(k, 1f)) return r;
            var c = r.center;
            return new Rect(c.x - r.width * k / 2f, c.y - r.height * k / 2f, r.width * k, r.height * k);
        }
    }
}
