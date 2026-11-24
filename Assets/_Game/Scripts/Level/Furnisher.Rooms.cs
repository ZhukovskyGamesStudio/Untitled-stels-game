using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>The furnisher: room by room: anchors, islands and the groups of each type (see <see cref="Furnisher"/>).</summary>
    public sealed partial class Furnisher
    {
        // ------------------------------------------------------------------ rooms

        /// <summary>How far in front of a window (from the wall's axis) a tall piece still covers it.</summary>
        const float WindowDepth = 1.0f;

        /// <summary>The strip of floor in front of every window of the house, per floor (<see cref="LevelBuilder.WindowCuts"/>).</summary>
        RectGrid[] WindowStrips()
        {
            var grids = new RectGrid[m_P.Floors];
            for (int f = 0; f < grids.Length; f++) grids[f] = new RectGrid();
            foreach (var (pos, yaw, _, _) in LevelBuilder.WindowCuts(m_L))
            {
                var q = Quaternion.Euler(0f, yaw, 0f);
                Vector3 inward = q * Vector3.forward, along = q * Vector3.right * (LevelMetrics.WindowWidth / 2f + 0.1f);
                Vector3 a = pos - along, c = pos + along + inward * WindowDepth;
                int floor = LevelLayout.FloorOf(pos.y - LevelMetrics.WindowCenterY + 0.3f);
                if (floor < 0 || floor >= grids.Length) continue;
                grids[floor].Add(Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.z, c.z), Mathf.Max(a.x, c.x), Mathf.Max(a.z, c.z)));
            }
            return grids;
        }

        /// <summary>Furnishes one room: islands first in big rooms, then the walls, then the corners.</summary>
        /// <summary>A printer stands in this room already: at most one per room (they looked like a row of kiosks).</summary>
        bool m_Printer;
        /// <summary>A fridge stands in this room already: every kitchen gets one, first thing along its walls.</summary>
        bool m_Fridge;
        /// <summary>A table stands in this room already (a kitchen gets one: a small one if the big one did not fit).</summary>
        bool m_Table;

        public void FurnishRoom(PlanRoom room)
        {
            if (room.Area < 4f) return;
            m_Printer = false;
            m_Fridge = false;
            m_Table = false;
            m_InRoom.Clear();
            m_Cap = room.Area > 70f ? 2 : 1;
            m_Windows ??= WindowStrips();
            BeginRoomWalk(room);
            if (room.Id == m_L.VaultRoom) { VaultRoom(room); return; }
            // the way up to the grate first — but after the hospital rooms' own pieces (a ward's beds, a theatre's table): they need those walls
            bool climbLate = RoomTraits.Equipped(room.Type);
            if (!climbLate) Climb(room);
            // islands in the middle: about one and a half times what an office planner would call "enough" — a house to search
            int islands = room.Type switch
            {
                RoomType.OpenSpace => Mathf.RoundToInt(room.Area / 15f),
                RoomType.Server => Mathf.RoundToInt(room.Area / 11f),
                RoomType.Storage => room.Area > 24f ? 1 : 0, // a pallet or so: the middle is the pile's floor (the workers stack loot there)
                RoomType.Meeting => room.Area > 60f ? 3 : room.Area > 30f ? 2 : 1,
                RoomType.Kitchen => Mathf.Max(1, Mathf.RoundToInt(room.Area / 17f)),
                RoomType.Lounge => Mathf.Max(1, Mathf.RoundToInt(room.Area / 19f)),
                RoomType.Lobby when m_L.PlanKind == HouseKind.Apartments => 0,
                RoomType.Lobby => 3,
                RoomType.Living => room.Area > 14f ? 1 : 0,
                RoomType.Office => room.Area > 22f ? Mathf.Max(1, Mathf.RoundToInt(room.Area / 28f)) : 0,
                RoomType.Hall => room.Area > 30f ? 1 : 0,
                RoomType.Gym => Mathf.Max(1, Mathf.RoundToInt(room.Area / 16f)),
                RoomType.Library => Mathf.Max(1, Mathf.RoundToInt(room.Area / 14f)),
                RoomType.CopyRoom => room.Area > 14f ? 1 : 0,
                RoomType.ControlRoom => room.Area > 20f ? 1 : 0,
                RoomType.Lab => Mathf.Max(1, Mathf.RoundToInt(room.Area / 16f)),
                RoomType.NapRoom => room.Area > 16f ? 1 : 0,
                RoomType.Studio => room.Area > 14f ? 1 : 0,
                RoomType.LockerRoom => Mathf.Max(1, Mathf.RoundToInt(room.Area / 18f)),
                RoomType.Infirmary => room.Area > 16f ? 1 : 0,
                RoomType.GameRoom => Mathf.Max(1, Mathf.RoundToInt(room.Area / 15f)),
                RoomType.Workshop => Mathf.RoundToInt(room.Area / 70f), // tyres, an engine, the drain cart: the walkway behind the bays stays open
                RoomType.ChargingStation => room.Area > 34f ? 1 : 0, // battery stacks back to back (the generator came with the anchors)
                _ => 0,
            };
            Anchor(room);
            if (climbLate) Climb(room);
            Islands(room, islands);
            if (room.Type == RoomType.Meeting) MeetingTable(room);
            if (room.Type == RoomType.Kitchen && !m_Table) SmallTable(room);
            Signature(room);
            Walls(room);
            if (room.Type != RoomType.Corridor) Walls(room); // a second pass fills the gaps the first left
            Corners(room);
            Sparse(room);
        }

        /// <summary>
        /// The vault behind the bank door: deposit boxes and cash shelves along every wall, gold on pallets and carts of
        /// money around the treasure in the middle (kept free by <see cref="HouseFeatures.PlanObjective"/>); the quarter
        /// circle the round door swings through stays clear.
        /// </summary>
        void VaultRoom(PlanRoom room)
        {
            if (m_L.VaultDoorway >= 0 && m_L.VaultDoorway < m_L.Doorways.Count)
            {
                var d = m_L.Doorways[m_L.VaultDoorway];
                var along = d.Rotation * Vector3.right;
                var across = d.Rotation * Vector3.forward;
                if (Vector3.Dot(room.Center - d.Position, across) < 0f) across = -across;
                Keep(room.Floor, d.Position - along * 1.4f, d.Position + along * 1.4f + across * 2.6f);
            }
            var faces = FacesOf(room);
            float y = LevelLayout.FloorY(room.Floor);
            foreach (var (w, n) in faces)
            {
                var u = new Vector3(w.Dir.x, 0, w.Dir.y);
                for (float t = 0.1f; t < w.Length - 0.3f;)
                {
                    var face = new Vector3(w.A.x, y, w.A.y) + u * t + n * (LevelMetrics.WallThickness / 2f);
                    var f = new Frame(face, u, n);
                    var batch = new Batch();
                    bool boxes = Chance(0.7);
                    var kind = boxes ? PropKind.DepositWall : PropKind.CashShelf;
                    float width = boxes ? 2.05f : 1.65f;
                    batch.Add(kind, f.P(width / 2f, BackOf(kind)), f.Face);
                    if (!boxes) batch.Item(f.P(width / 2f + R(-0.5f, 0.5f), BackOf(kind), 1.06f));
                    if (t + width <= w.Length - 0.05f && Deep(room, face, n, batch, 0.9f) && TryCommit(room, batch, 0.02f)) t += width + 0.02f;
                    else t += 0.25f;
                }
            }
            // gold and money between the walls and the treasure
            var b = room.Bounds;
            var spots = new List<Vector3>();
            for (float x = b.xMin + 1.0f; x < b.xMax - 0.9f; x += 0.5f)
                for (float z = b.yMin + 1.0f; z < b.yMax - 0.9f; z += 0.5f)
                    if (InRoom(room.Floor, room.Id, x, z)) spots.Add(new Vector3(x, y, z));
            Shuffle(spots);
            int placed = 0, max = Mathf.Clamp(Mathf.RoundToInt(room.Area / 6f), 2, 8);
            foreach (var s in spots)
            {
                if (placed >= max) break;
                var batch = new Batch();
                var kind = Chance(0.6) ? PropKind.GoldPallet : PropKind.MoneyCart;
                batch.Add(kind, s, m_Rng.Next(4) * 90f);
                if (kind == PropKind.GoldPallet && Chance(0.5)) batch.Item(s + Vector3.up * 0.6f);
                if (TryCommit(room, batch, 0.05f, 0.75f)) placed++;
            }
        }

        /// <summary>
        /// A meeting room is its table. Where the islands found no place for one (doors on every wall leave little of a
        /// small room), a smaller set gets every spot of the room and a narrower way round: the long table with six chairs,
        /// the round one with four, with two, alone.
        /// </summary>
        void MeetingTable(PlanRoom room)
        {
            if (m_InRoom.ContainsKey(PropKind.ConferenceTable) || m_InRoom.ContainsKey(PropKind.MeetingTable) || m_InRoom.ContainsKey(PropKind.RoundTable)) return;
            var b = room.Bounds;
            var spots = new List<Vector3>();
            float y = LevelLayout.FloorY(room.Floor);
            for (float x = b.xMin + 1.0f; x < b.xMax - 0.9f; x += 0.5f)
                for (float z = b.yMin + 1.0f; z < b.yMax - 0.9f; z += 0.5f)
                    if (InRoom(room.Floor, room.Id, x, z)) spots.Add(new Vector3(x, y, z));
            var c = room.Center;
            spots.Sort((p, q) => (p - c).sqrMagnitude.CompareTo((q - c).sqrMagnitude));
            bool wide = b.width >= b.height;
            for (int option = room.Area > 18f ? 0 : 1; option < 4; option++)
                foreach (var s in spots)
                    for (int turn = 0; turn < (option == 3 ? 1 : 2); turn++)
                    {
                        // the table along the room first
                        bool alongX = wide == (turn == 0);
                        var f = new Frame(s, alongX ? Vector3.right : Vector3.forward, alongX ? Vector3.forward : Vector3.left);
                        var batch = new Batch();
                        if (option == 0)
                        {
                            batch.Add(PropKind.MeetingTable, f.P(0, 0), f.Face);
                            for (int i = -1; i <= 1; i++)
                            {
                                batch.Add(PropKind.MeetingChair, f.P(i * 1.1f, 1.0f), f.Back);
                                batch.Add(PropKind.MeetingChair, f.P(i * 1.1f, -1.0f), f.Face);
                            }
                            batch.Item(f.P(R(-1.2f, 1.2f), R(-0.3f, 0.3f), 0.78f));
                        }
                        else
                        {
                            batch.Add(PropKind.RoundTable, f.P(0, 0), 0);
                            if (option == 1) Chairs(f, batch, 4);
                            if (option == 2)
                            {
                                batch.Add(PropKind.Chair, f.P(0, 1.0f), f.Back);
                                batch.Add(PropKind.Chair, f.P(0, -1.0f), f.Face);
                            }
                            batch.Item(f.P(0, 0, 0.76f));
                        }
                        if (TryCommit(room, batch, 0.05f, 0.6f)) return;
                    }
        }

        /// <summary>
        /// The pieces that make a room what it is: an office without a desk, a server room without a rack, a meeting room
        /// without a table reads as a mistake (measured over 72 houses: 61 % of offices had no desk — the wall groups gave
        /// them guest chairs, a safe and shelves —, 20 % of server rooms no rack, 13 % of meeting rooms no table).
        /// </summary>
        public static readonly Dictionary<RoomType, PropKind[]> Signatures = new()
        {
            [RoomType.Office] = new[] { PropKind.Desk, PropKind.ExecutiveDesk, PropKind.StandingDesk, PropKind.Terminal, PropKind.ConsoleDesk, PropKind.SupervisorDesk },
            [RoomType.OpenSpace] = new[] { PropKind.Desk, PropKind.StandingDesk, PropKind.Terminal },
            [RoomType.Server] = new[] { PropKind.ServerRack, PropKind.NetworkCabinet, PropKind.TapeLibrary },
            [RoomType.Meeting] = new[] { PropKind.MeetingTable, PropKind.RoundTable, PropKind.ConferenceTable },
            [RoomType.Lab] = new[] { PropKind.LabBench, PropKind.RobotTable, PropKind.WorkTable },
            [RoomType.Infirmary] = new[] { PropKind.HospitalBed, PropKind.ExamTable },
        };

        /// <summary>
        /// After the room's own groups and islands: a room still without its signature piece (<see cref="Signatures"/>)
        /// gets a compact one against a wall — a desk and its chair, a row of racks, a small table with two chairs, a work
        /// table, an examination couch — before the walls fill up with the rest.
        /// </summary>
        void Signature(PlanRoom room)
        {
            if (!Signatures.TryGetValue(room.Type, out var kinds)) return;
            foreach (var k in kinds) if (m_InRoom.ContainsKey(k)) return;
            Anchor(room, Option.Signature, 3, false, 0.7f); // a small room with doors on every wall: a narrower way past it
        }

        /// <summary>
        /// The options of <see cref="AnchorGroup"/> by name: the one contract between the callers of <see cref="Anchor(PlanRoom, int, int, bool, float)"/>
        /// and its cases. Per room type, a run of options from the biggest to ever smaller ones; a room with several groups
        /// (a restroom's stalls, basins, urinals; a charging room's docks and generator) has a run for each.
        /// </summary>
        static class Option
        {
            /// <summary>Any themed room's own piece: the biggest, then smaller ones (<see cref="Count"/> of them).</summary>
            public const int Biggest = 0, Smaller = 1, Smallest = 2, Count = 3;
            /// <summary>A restroom: three runs of three — the stalls, the basins under their mirrors, the urinals.</summary>
            public const int Stalls = 0, Basins = 3, Urinals = 6;
            /// <summary>The robots' charging room: a pair of docks, one dock, the generator, the remnant the humans left.</summary>
            public const int DockPair = 0, Dock = 1, Generator = 2, Remnant = 3;
            /// <summary>The control room: two terminals side by side, one, the video wall.</summary>
            public const int TwoTerminals = 0, OneTerminal = 1, VideoWall = 2;
            /// <summary>A lobby: the waiting seats (two rows, one), the mailboxes on the wall.</summary>
            public const int Seats = 0, Mailboxes = 2;
            /// <summary>A flat's bathroom: the bath, then the toilet with the basin.</summary>
            public const int Bath = 0, ToiletBasin = 1;
            /// <summary>The first of the signature pieces (<see cref="Signature"/>; after every themed room's own runs).</summary>
            public const int Signature = 10;
        }

        void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = m_Rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// A themed room's own piece first, compact enough for a small room (a bed lengthwise along the wall, one pod, the
        /// video wall without its console…), so the room always reads as what it is; the rest comes after it. Tried along
        /// every wall, longest first, with the usual walkway in front, then with a narrower one.
        /// </summary>
        void Anchor(PlanRoom room)
        {
            if (room.Type == RoomType.Lobby && m_L.PlanKind == HouseKind.Hospital) { Anchor(room, Option.Seats, 2, false); return; } // the waiting area's rows of seats
            if (room.Type == RoomType.Lobby && m_L.PlanKind == HouseKind.Apartments) { Anchor(room, Option.Mailboxes, 1, false, 0.6f); return; } // the entrance hall's mailboxes
            if (room.Type < RoomType.Gym && room.Type is not (RoomType.Kitchen or RoomType.Restroom)) return;
            if (room.Type == RoomType.Restroom)
            {
                // a restroom is its stalls and its basins: both, each as big as the room takes; where no stall fits
                // (they are 1.5 m deep: a narrow room with doors at both ends has no place for one) urinals on a wall
                if (!Anchor(room, Option.Stalls, 3, false)) Anchor(room, Option.Urinals, 3, false);
                Anchor(room, Option.Basins, 3, false);
                return;
            }
            if (room.Type == RoomType.Workshop) { WorkshopBays(room); return; }
            if (room.Type == RoomType.Ward) { if (!Anchor(room, Option.Biggest, 1, false)) Anchor(room, Option.Smaller, 1, false); return; } // two beds, or one
            // the flats: the bed with its night tables (or the bed alone), the sofa (with its coffee table), the bath, then the
            // toilet and the basin, the hallway's shoe cabinet under the coat hooks
            if (room.Type is RoomType.Bedroom or RoomType.Living) { if (!Anchor(room, Option.Biggest, 1, false, 0.8f)) Anchor(room, Option.Smaller, 1, false, 0.7f); return; }
            if (room.Type == RoomType.Bathroom) { Anchor(room, Option.Bath, 1, false, 0.6f); Anchor(room, Option.ToiletBasin, 1, false, 0.6f); return; }
            if (room.Type == RoomType.FlatHall) { Anchor(room, Option.Biggest, 2, false, 0.7f); return; }
            if (room.Type == RoomType.NurseStation && StationCounter(room)) return;
            if (room.Type is RoomType.NurseStation or RoomType.Operating or RoomType.Imaging) // in the middle
            {
                AnchorFree(room);
                if (room.Type == RoomType.Operating)
                    for (int i = m_L.Props.Count - 1; i >= 0; i--)
                        if (m_L.Props[i].Kind == PropKind.SurgicalTable && m_P.RoomAt(m_L.Props[i].Position + Vector3.up * 0.3f) == room)
                        {
                            // the lamps hang from the ceiling over the table (nothing to walk into: no check against what stands under them)
                            Put(room, PropKind.SurgicalLights, m_L.Props[i].Position, m_L.Props[i].RotationY);
                            break;
                        }
                return;
            }
            if (room.Type == RoomType.ChargingStation) { ChargingStation(room); return; }
            if (room.Type == RoomType.PaintBooth) { AnchorFree(room); return; } // the car in the middle of the booth
            if (room.Type == RoomType.ControlRoom)
            {
                // the house's two robot terminals: side by side, or each against its own wall (the video wall if neither fits)
                // (a narrower way past them where the walls are short: the second terminal belongs here too)
                if (Anchor(room, Option.TwoTerminals, 1, false, 0.7f)) return;
                if (Anchor(room, Option.OneTerminal, 1, false, 0.7f)) { Anchor(room, Option.OneTerminal, 1, false, 0.7f); return; }
                Anchor(room, Option.VideoWall, 1, true);
                return;
            }
            Anchor(room, Option.Biggest, Option.Count, true);
        }

        /// <summary>How many guards' docks the charging room tries for (pairs while the walls take them, then singles).</summary>
        const int ChargingDocksWanted = 4;
        bool m_GeneratorPlaced;

        /// <summary>
        /// The robots' charging room (<see cref="ChargingRooms"/>): their docks along the walls first — where the guards
        /// rest —, then the generator (free in the middle of a big room, else against a wall), and the one piece the humans
        /// left, pushed askew against a wall. The walls then fill with battery stacks, scrap shrines, conduit growths and
        /// cable masses (<see cref="WallGroup"/>).
        /// </summary>
        void ChargingStation(PlanRoom room)
        {
            m_GeneratorPlaced = false;
            int docks = 0;
            for (int k = 0; k < 2 && docks + 2 <= ChargingDocksWanted; k++)
                if (Anchor(room, Option.DockPair, 1, false, 0.8f)) docks += 2;
                else break;
            while (docks < ChargingDocksWanted && Anchor(room, Option.Dock, 1, false, 0.8f)) docks++;
            if (!(room.Area >= 24f && AnchorFree(room))) Anchor(room, Option.Generator, 1, false, 0.8f);
            m_GeneratorPlaced = true;
            Anchor(room, Option.Remnant, 1, false, 0.8f);
        }

        /// <summary>
        /// The workshop hall's service bays along the street wall, one per 4 m: a closed sectional door against the wall, the
        /// painted lines of the bay and in it a car up on a lift, a car with its hood up, one parked, one on jack stands or
        /// a lift waiting empty — nose in, as they drive in. Where a doorway keeps the floor clear, the bay is left out.
        /// </summary>
        void WorkshopBays(PlanRoom room)
        {
            (PlanWall w, Vector3 n) street = default;
            bool found = false;
            foreach (var face in FacesOf(room))
            {
                if (face.n.z < 0.9f) continue;                     // the street is at z = 0: its wall's inner side looks +z
                if (!found || face.w.A.y < street.w.A.y - 0.01f || (Mathf.Abs(face.w.A.y - street.w.A.y) < 0.01f && face.w.Length > street.w.Length))
                {
                    street = face;
                    found = true;
                }
            }
            if (!found) return;
            var (wall, n) = street;
            var u = new Vector3(wall.Dir.x, 0, wall.Dir.y);
            float y = LevelLayout.FloorY(room.Floor);
            var origin = new Vector3(wall.A.x, y, wall.A.y) + n * (LevelMetrics.WallThickness / 2f);
            float bay = PlanGenerator.BayWidth;
            int count = Mathf.FloorToInt((wall.Length - 0.6f) / bay);
            float t0 = (wall.Length - count * bay) / 2f;
            var cars = new[] { PropKind.LiftCar, PropKind.CarHoodOpen, PropKind.CarParked, PropKind.CarOnStands, PropKind.LiftEmpty };
            var odds = new[] { 0.35, 0.2, 0.15, 0.15, 0.15 };
            for (int k = 0; k < count; k++)
            {
                var f = new Frame(origin + u * (t0 + k * bay + bay / 2f), u, n);
                double roll = m_Rng.NextDouble();
                int pick = 0;
                for (double acc = odds[0]; pick < odds.Length - 1 && roll > acc; acc += odds[++pick]) { }
                var car = cars[pick];
                // the whole bay first; where a doorway or a patrol keeps some of it free: without the door, without the lines,
                // a narrower car
                foreach (var (door, lines, kind) in new[] { (true, true, car), (false, true, car), (false, false, car), (false, false, PropKind.CarParked) })
                {
                    var b = new Batch();
                    if (door) b.Add(PropKind.GarageDoor, f.P(0, 0.01f), f.Face, solid: false);
                    if (lines) b.Add(PropKind.BayLines, f.P(0, 3.3f, 0.002f), f.Face, solid: false);
                    b.Add(kind, f.P(0, 3.4f), f.Face);
                    // a part left on the trunk lid of a car waiting its turn
                    if (kind is PropKind.CarParked or PropKind.CarHoodOpen && Chance(0.5)) b.Item(f.P(R(-0.3f, 0.3f), 1.7f, 0.9f));
                    if (TryCommit(room, b, 0.05f)) break;
                }
            }
        }

        /// <summary>
        /// The nurse station's counter: parallel to its open side, facing the corridor a little way in (the nurses see
        /// the corridor and whoever comes up to the ledge), their chairs behind it.
        /// </summary>
        bool StationCounter(PlanRoom room)
        {
            foreach (var d in m_L.Doorways)
            {
                if (d.Kind != EdgeType.Open || (d.RoomA != room.Id && d.RoomB != room.Id)) continue;
                var across = d.Rotation * Vector3.forward;
                across.y = 0f;
                if (Vector3.Dot(room.Center - d.Position, across) < 0f) across = -across;
                across.Normalize();
                var along = Vector3.Cross(Vector3.up, across);
                float yaw = Mathf.Atan2(-across.x, -across.z) * Mathf.Rad2Deg; // its front to the corridor
                foreach (float depth in new[] { 4.6f, 4.0f, 3.2f, 2.6f }) // past the room's middle: the floor before it stays open to the corridor
                {
                    var p = d.Position + across * depth;
                    p.y = LevelLayout.FloorY(room.Floor);
                    var b = new Batch();
                    b.Add(PropKind.NurseStationDesk, p, yaw);
                    b.Add(PropKind.Chair, p + across * 0.95f - along * 0.7f, yaw);
                    b.Add(PropKind.Chair, p + across * 0.95f + along * 0.7f, yaw);
                    b.Item(p + across * 0.1f + along * R(-1f, 1f) + Vector3.up * 0.78f);
                    if (TryCommit(room, b, 0.05f)) return true;
                }
            }
            return false;
        }

        /// <summary>The room's own free-standing group in the middle (no option against a wall tried).</summary>
        bool AnchorFree(PlanRoom room) => Anchor(room, 0, 0, true);

        /// <summary>
        /// The first of <paramref name="count"/> options of <see cref="AnchorGroup"/> from <paramref name="first"/> (an <see cref="Option"/>)
        /// that fits along a wall (or, when <paramref name="free"/>, in the middle).
        /// </summary>
        bool Anchor(PlanRoom room, int first, int count, bool free, float narrowest = 1.0f)
        {
            var faces = FacesOf(room);
            // stalls are deep (1.5 m): at the end wall of a narrow restroom they leave the whole length to walk, along
            // its side they would leave a slot
            if (room.Type == RoomType.Restroom && first == Option.Stalls) faces.Reverse();
            float y = LevelLayout.FloorY(room.Floor);
            // the best option first (the biggest), smaller ones where doors leave too little wall
            for (int option = first; option < first + count; option++)
                foreach (float walk in narrowest < 1.0f ? new[] { 1.7f, 1.0f, narrowest } : new[] { 1.7f, 1.0f })
                    foreach (var (w, n) in faces)
                    {
                        var u = new Vector3(w.Dir.x, 0, w.Dir.y);
                        // (from the faces of the walls at its ends: the wall's line runs into them by half their thickness)
                        float end0 = LevelMetrics.WallThickness / 2f + 0.05f, end1 = w.Length - LevelMetrics.WallThickness / 2f - 0.05f;
                        for (float t = end0; t < end1 - 0.1f; t += 0.25f)
                        {
                            var face = new Vector3(w.A.x, y, w.A.y) + u * t + n * (LevelMetrics.WallThickness / 2f);
                            var batch = new Batch();
                            float width = AnchorGroup(room, batch, new Frame(face, u, n), option);
                            if (width <= 0f || t + width > end1) break;
                            var shift = u * (width / 2f);
                            for (int i = 0; i < batch.Pieces.Count; i++) { var q = batch.Pieces[i]; q.Pos += shift; batch.Pieces[i] = q; }
                            for (int i = 0; i < batch.Items.Count; i++) batch.Items[i] += shift;
                            if (Deep(room, face + shift, n, batch, walk) && TryCommit(room, batch, front: n)) return true;
                        }
                    }
            if (!free) return false;
            // doors everywhere along the walls: the piece stands free in the room, an aisle round it
            var b = room.Bounds;
            var spots = new List<Vector3>();
            for (float x = b.xMin + 1.2f; x < b.xMax - 1.1f; x += 0.5f)
                for (float z = b.yMin + 1.2f; z < b.yMax - 1.1f; z += 0.5f)
                    if (InRoom(room.Floor, room.Id, x, z)) spots.Add(new Vector3(x, y, z));
            var c = room.Center;
            spots.Sort((p, q) => (p - c).sqrMagnitude.CompareTo((q - c).sqrMagnitude));
            bool wide = b.width >= b.height;
            foreach (var s in spots)
            {
                // the room's own free-standing group (a table, a bed, a pool table — never a thing that hangs on a wall)
                var f = new Frame(s, wide ? Vector3.right : Vector3.forward, wide ? Vector3.forward : Vector3.left);
                var batch = new Batch();
                Island(room, f, batch);
                if (batch.Pieces.Count > 0 && batch.Pieces.TrueForAll(p => p.Solid) && TryCommit(room, batch, 0.05f, 0.9f)) return true;
            }
            return false;
        }

        /// <summary>
        /// A themed room's group against a wall (centred on the frame, u along the wall) for an <see cref="Option"/>;
        /// its width, 0 = no such option.
        /// </summary>
        float AnchorGroup(PlanRoom room, Batch b, Frame f, int option)
        {
            float Single(PropKind k, float w) { b.Add(k, f.P(0, BackOf(k)), f.Face); return w; }
            float Out(PropKind k, float v, float w) { b.Add(k, f.P(0, v), f.Face); return w; }
            // lying along the wall (its side to it): beds, pods
            float Along(PropKind k)
            {
                var r = FootprintOf(k);
                b.Add(k, f.P(0, r.width / 2f + WallGap), f.Face + 90f);
                return r.height + 0.05f;
            }
            switch (room.Type, option)
            {
                // the robots' charging room: docks in pairs or alone, the generator against a wall, the humans' remnant askew
                case (RoomType.ChargingStation, Option.DockPair):
                    foreach (float u in new[] { -0.62f, 0.62f }) b.Add(PropKind.ChargingDock, f.P(u, BackOf(PropKind.ChargingDock)), f.Face);
                    return 2.45f;
                case (RoomType.ChargingStation, Option.Dock): return Single(PropKind.ChargingDock, 1.2f);
                case (RoomType.ChargingStation, Option.Generator): return Single(PropKind.Generator, 1.9f);
                case (RoomType.ChargingStation, Option.Remnant):
                {
                    var k = ChargingRooms.Remnant(m_L.ChargingFrom);
                    var r = FootprintOf(k);
                    b.Add(k, f.P(0f, BackOf(k) + 0.15f), f.Face + (Chance(0.5) ? 14f : -14f));
                    return r.width + 0.5f;
                }
                case (RoomType.Kitchen, Option.Biggest):
                case (RoomType.Kitchen, Option.Smaller):
                {
                    // the kitchen run (or its shorter half): sink, cooker, worktop with its microwave and the coffee machine, cupboards
                    // over it, and the fridge at its end (every kitchen has one); the whole run shifted to centre it
                    bool full = option == Option.Biggest;
                    float shift = full ? -0.36f : -0.355f;
                    float sink = (full ? -1.52f : -1.22f) + shift, counter = (full ? 0.92f : 0.62f) + shift;
                    b.Add(PropKind.KitchenSink, f.P(sink, BackOf(PropKind.KitchenSink)), f.Face);
                    if (full) b.Add(PropKind.Stove, f.P(-0.62f + shift, BackOf(PropKind.Stove)), f.Face);
                    b.Add(PropKind.Fridge, f.P(counter + 1.22f + 0.37f, BackOf(PropKind.Fridge)), f.Face);
                    b.Add(PropKind.KitchenCounter, f.P(counter, BackOf(PropKind.KitchenCounter)), f.Face);
                    var cx = Quaternion.Euler(0, f.Face, 0) * Vector3.right;
                    b.Add(PropKind.CoffeeMachine, f.P(counter, BackOf(PropKind.KitchenCounter) + 0.05f, 0.93f) + cx * 0.75f, f.Face, solid: false);
                    b.Add(PropKind.WallCabinet, f.P(sink, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                    if (full) b.Add(PropKind.WallCabinet, f.P(counter, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                    b.Item(f.P(counter, BackOf(PropKind.KitchenCounter), 0.93f));
                    return full ? 5.02f : 4.41f;
                }
                case (RoomType.Kitchen, Option.Smallest):
                    // the smallest: the sink and the fridge beside it
                    b.Add(PropKind.KitchenSink, f.P(-0.36f, BackOf(PropKind.KitchenSink)), f.Face);
                    b.Add(PropKind.WallCabinet, f.P(-0.36f, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                    b.Add(PropKind.Fridge, f.P(0.61f, BackOf(PropKind.Fridge)), f.Face);
                    return 1.96f;
                // the stalls: three, two, one
                case (RoomType.Restroom, Option.Stalls): return Row(f, b, PropKind.ToiletStall, room.Area > 14f ? 3 : 2, 1.06f);
                case (RoomType.Restroom, Option.Stalls + 1): return Row(f, b, PropKind.ToiletStall, room.Area > 14f ? 2 : 1, 1.06f);
                case (RoomType.Restroom, Option.Stalls + 2): return Row(f, b, PropKind.ToiletStall, 1, 1.06f);
                // the basins under their mirrors, the hand dryer beside them
                case (RoomType.Restroom, Option.Basins):
                    b.Add(PropKind.MirrorSink, f.P(-0.62f, BackOf(PropKind.MirrorSink)), f.Face);
                    b.Add(PropKind.MirrorSink, f.P(0.18f, BackOf(PropKind.MirrorSink)), f.Face);
                    b.Add(PropKind.HandDryer, f.P(0.82f, BackOf(PropKind.HandDryer)), f.Face, solid: false);
                    return 2.0f;
                case (RoomType.Restroom, Option.Basins + 1):
                    b.Add(PropKind.MirrorSink, f.P(-0.22f, BackOf(PropKind.MirrorSink)), f.Face);
                    b.Add(PropKind.HandDryer, f.P(0.42f, BackOf(PropKind.HandDryer)), f.Face, solid: false);
                    return 1.2f;
                case (RoomType.Restroom, Option.Basins + 2): return Single(PropKind.MirrorSink, 0.75f);
                case (RoomType.Restroom, Option.Urinals): return Row(f, b, PropKind.Urinal, 3, 0.7f);
                case (RoomType.Restroom, Option.Urinals + 1): return Row(f, b, PropKind.Urinal, 2, 0.7f);
                case (RoomType.Restroom, Option.Urinals + 2): return Single(PropKind.Urinal, 0.6f);
                case (RoomType.Gym, Option.Biggest): return Single(PropKind.DumbbellRack, 1.5f);
                case (RoomType.Gym, Option.Smaller): return Single(PropKind.ExerciseBike, 0.65f);
                case (RoomType.Gym, Option.Smallest): return Out(PropKind.PunchingBag, 0.5f, 0.7f);
                case (RoomType.Library, Option.Biggest): return Row(f, b, PropKind.TallBookcase, 2, 1.02f);
                case (RoomType.Library, Option.Smaller): return Single(PropKind.TallBookcase, 1.05f);
                case (RoomType.Library, Option.Smallest): return Out(PropKind.Globe, 0.4f, 0.7f);
                case (RoomType.CopyRoom, Option.Biggest): { Single(PropKind.Copier, 1.0f); b.Item(f.P(0.2f, BackOf(PropKind.Copier), 1.05f)); return 1.0f; }
                case (RoomType.CopyRoom, Option.Smaller): return Single(PropKind.PaperStack, 0.85f);
                case (RoomType.CopyRoom, Option.Smallest): return Single(PropKind.Shredder, 0.5f);
                // the robots' control consoles (the house's terminals, LevelGenerator.PlaceTerminals), an operator's chair at each
                case (RoomType.ControlRoom, Option.TwoTerminals):
                    foreach (float u in new[] { -0.85f, 0.85f })
                    {
                        b.Add(PropKind.Terminal, f.P(u, BackOf(PropKind.Terminal)), f.Face);
                        b.Add(PropKind.OperatorChair, f.P(u, BackOf(PropKind.Terminal) + 0.85f), f.Back);
                    }
                    return 3.4f;
                case (RoomType.ControlRoom, Option.OneTerminal):
                    b.Add(PropKind.Terminal, f.P(0, BackOf(PropKind.Terminal)), f.Face);
                    b.Add(PropKind.OperatorChair, f.P(0, BackOf(PropKind.Terminal) + 0.85f), f.Back);
                    return 1.7f;
                case (RoomType.ControlRoom, Option.VideoWall): return Single(PropKind.MonitorWall, 2.45f);
                case (RoomType.Lab, Option.Biggest):
                    b.Add(PropKind.LabBench, f.P(0, BackOf(PropKind.LabBench)), f.Face);
                    b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.LabBench) + 0.1f, 0.91f));
                    return 1.85f;
                case (RoomType.Lab, Option.Smaller): return Single(PropKind.PartsCabinet, 0.95f);
                case (RoomType.Lab, Option.Smallest): return Single(PropKind.RobotArm, 0.7f);
                case (RoomType.NapRoom, Option.Biggest): return Along(PropKind.SleepPod);
                case (RoomType.NapRoom, Option.Smaller): return Out(PropKind.BeanBag, 0.6f, 0.95f);
                case (RoomType.Studio, Option.Biggest):
                    b.Add(PropKind.AcousticPanel, f.P(-0.62f, BackOf(PropKind.AcousticPanel)), f.Face, solid: false);
                    b.Add(PropKind.AcousticPanel, f.P(0.62f, BackOf(PropKind.AcousticPanel)), f.Face, solid: false);
                    b.Add(PropKind.MicStand, f.P(0, 0.9f), f.Face);
                    return 2.45f;
                case (RoomType.Studio, Option.Smaller): return Single(PropKind.MixingDesk, 1.65f);
                case (RoomType.Studio, Option.Smallest): { b.Add(PropKind.AcousticPanel, f.P(0, BackOf(PropKind.AcousticPanel)), f.Face, solid: false); return 1.25f; }
                case (RoomType.LockerRoom, Option.Biggest): return Row(f, b, PropKind.Lockers, 2, 1.05f);
                case (RoomType.LockerRoom, Option.Smaller): return Single(PropKind.Lockers, 1.05f);
                case (RoomType.LockerRoom, Option.Smallest): return Out(PropKind.Bench, 0.3f, 1.65f);
                case (RoomType.Lobby, Option.Seats): return Row(f, b, PropKind.WaitingSeats, 2, 2.5f);
                case (RoomType.Lobby, Option.Seats + 1): return Single(PropKind.WaitingSeats, 2.5f);
                case (RoomType.Lobby, Option.Mailboxes): { b.Add(PropKind.Mailboxes, f.P(0, BackOf(PropKind.Mailboxes)), f.Face, solid: false); return 1.25f; } // on the wall
                case (RoomType.Ward, Option.Biggest):
                case (RoomType.Ward, Option.Smaller):
                {
                    // patient beds head to the wall: the headwall unit over each, its curtain round it, a monitor and a night table beside
                    int beds = option == Option.Biggest ? 2 : 1;
                    float step = 1.78f, w = beds * step; // (two beds in a 4 m ward between walls 0.3 m thick)
                    for (int i = 0; i < beds; i++)
                    {
                        float u = (i - (beds - 1) / 2f) * step;
                        float n = BackOf(PropKind.HospitalBed) + 0.45f; // pulled out from the wall: the nurses reach the head, the headwall's call cord hangs free
                        b.Add(PropKind.HospitalBed, f.P(u, n), f.Face);
                        b.Add(PropKind.BedHeadwall, f.P(u, BackOf(PropKind.BedHeadwall)), f.Face, solid: false);
                        b.Add(PropKind.BedCurtain, f.P(u - 0.05f, n + 0.05f), f.Face, solid: false);
                        if (Chance(0.6)) b.Item(f.P(u + 0.2f, n + 0.4f, 0.66f));
                    }
                    b.Add(PropKind.NightTable, f.P(beds == 2 ? 0f : 0.82f, BackOf(PropKind.NightTable)), f.Face);
                    return w + (beds == 1 ? 0.5f : 0f);
                }
                case (RoomType.Bedroom, Option.Biggest):
                case (RoomType.Bedroom, Option.Smaller):
                {
                    // the double bed head to the wall, a night table each side (a small room: the bed alone)
                    b.Add(PropKind.DoubleBed, f.P(0, BackOf(PropKind.DoubleBed)), f.Face);
                    if (option == Option.Smaller) return 1.7f;
                    foreach (float s in new[] { -1.1f, 1.1f }) b.Add(PropKind.NightTable, f.P(s, BackOf(PropKind.NightTable)), f.Face);
                    if (Chance(0.5)) b.Item(f.P(Chance(0.5) ? -1.1f : 1.1f, BackOf(PropKind.NightTable), 0.56f));
                    return 2.75f;
                }
                case (RoomType.Living, Option.Biggest):
                case (RoomType.Living, Option.Smaller):
                    // the sofa against the wall (and its coffee table in front of it)
                    b.Add(PropKind.Sofa, f.P(0, BackOf(PropKind.Sofa)), f.Face);
                    if (option == Option.Biggest) b.Add(PropKind.CoffeeTable, f.P(0, BackOf(PropKind.Sofa) * 2f + 0.62f), f.Face);
                    return 2.3f;
                case (RoomType.Bathroom, Option.Bath): return Single(PropKind.Bathtub, 1.75f);
                case (RoomType.Bathroom, Option.ToiletBasin):
                    b.Add(PropKind.HomeToilet, f.P(-0.42f, BackOf(PropKind.HomeToilet)), f.Face);
                    b.Add(PropKind.BathVanity, f.P(0.36f, BackOf(PropKind.BathVanity)), f.Face);
                    return 1.35f;
                case (RoomType.FlatHall, Option.Biggest):
                    // the shoe cabinet under the coat hooks, a mirror's worth of wall beside
                    b.Add(PropKind.ShoeCabinet, f.P(-0.55f, BackOf(PropKind.ShoeCabinet)), f.Face);
                    b.Add(PropKind.HallHooks, f.P(0.55f, BackOf(PropKind.HallHooks)), f.Face, solid: false);
                    return 2.1f;
                case (RoomType.FlatHall, Option.Smaller): return Single(PropKind.ShoeCabinet, 0.95f); // a hallway of doors: the shoe cabinet alone
                case (RoomType.Infirmary, Option.Biggest): { float w = Along(PropKind.HospitalBed); b.Item(f.P(0, 0.5f, 0.66f)); return w; }
                case (RoomType.Infirmary, Option.Smaller): { Single(PropKind.MedicineCabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.MedicineCabinet), 1.65f)); return 0.95f; }
                case (RoomType.Infirmary, Option.Smallest): return Out(PropKind.PrivacyScreen, 0.3f, 1.65f);
                // the signature pieces (Signature): compact, against a wall
                case (RoomType.Office, Option.Signature):
                case (RoomType.Office, Option.Signature + 1):
                case (RoomType.OpenSpace, Option.Signature + 1):
                    b.Add(PropKind.Desk, f.P(0, 0.4f + WallGap), f.Face);
                    b.Add(PropKind.Chair, f.P(0, 1.2f), f.Back);
                    b.Item(f.P(R(-0.5f, 0.5f), 0.5f, DeskTop + 0.005f));
                    if (option != Option.Signature) return 1.65f;
                    b.Add(PropKind.FilePedestal, f.P(1.05f, BackOf(PropKind.FilePedestal)), f.Face);
                    return 2.55f;
                case (RoomType.OpenSpace, Option.Signature):
                    foreach (float u in new[] { -0.82f, 0.82f })
                    {
                        b.Add(PropKind.Desk, f.P(u, 0.4f + WallGap), f.Face);
                        b.Add(PropKind.Chair, f.P(u, 1.2f), f.Back);
                    }
                    b.Item(f.P(R(-1.2f, 1.2f), 0.5f, DeskTop + 0.005f));
                    return 3.3f;
                case (RoomType.OpenSpace, Option.Signature + 2):
                    Single(PropKind.StandingDesk, 1.45f);
                    b.Item(f.P(R(-0.4f, 0.4f), BackOf(PropKind.StandingDesk), 1.105f));
                    return 1.45f;
                case (RoomType.Server, Option.Signature):
                    for (int i = -1; i <= 1; i++) b.Add(PropKind.ServerRack, f.P(i * 0.72f, BackOf(PropKind.ServerRack)), f.Face);
                    return 2.2f;
                case (RoomType.Server, Option.Signature + 1): return Row(f, b, PropKind.ServerRack, 2, 0.72f);
                case (RoomType.Server, Option.Signature + 2): return Row(f, b, PropKind.NetworkCabinet, 2, 0.62f);
                case (RoomType.Meeting, Option.Signature):
                case (RoomType.Meeting, Option.Signature + 1):
                    // a small table against the wall, two chairs on the room side (or one)
                    b.Add(PropKind.RoundTable, f.P(0, 0.5f + WallGap), 0);
                    if (option == Option.Signature) { b.Add(PropKind.Chair, f.P(-0.35f, 1.3f), f.Back); b.Add(PropKind.Chair, f.P(0.35f, 1.3f), f.Back); }
                    else b.Add(PropKind.Chair, f.P(0, 1.3f), f.Back);
                    b.Item(f.P(0, 0.5f + WallGap, 0.76f));
                    return 1.25f;
                case (RoomType.Lab, Option.Signature):
                    b.Add(PropKind.WorkTable, f.P(0, BackOf(PropKind.WorkTable)), f.Face);
                    b.Add(PropKind.Chair, f.P(0, 1.2f), f.Back);
                    b.Item(f.P(R(-0.5f, 0.5f), BackOf(PropKind.WorkTable), DeskTop + 0.005f));
                    return 1.65f;
                case (RoomType.Lab, Option.Signature + 1): return Single(PropKind.WorkTable, 1.65f);
                case (RoomType.Office, Option.Signature + 2): b.Item(f.P(R(-0.5f, 0.5f), 0.5f, DeskTop + 0.005f)); return Single(PropKind.Desk, 1.65f);
                case (RoomType.Infirmary, Option.Signature): return Along(PropKind.ExamTable);
                case (RoomType.GameRoom, Option.Biggest): return Row(f, b, PropKind.ArcadeMachine, 2, 0.8f);
                case (RoomType.GameRoom, Option.Smaller): return Single(PropKind.ArcadeMachine, 0.8f);
                case (RoomType.GameRoom, Option.Smallest): { b.Add(PropKind.DartBoard, f.P(0, BackOf(PropKind.DartBoard)), f.Face, solid: false); return 0.65f; }
            }
            return 0f;
        }

        /// <summary>The walls of a room with the normal into it, longest first (rails, curves and stubs left out).</summary>
        List<(PlanWall w, Vector3 n)> FacesOf(PlanRoom room)
        {
            var list = new List<(PlanWall w, Vector3 n)>();
            foreach (var w in m_P.Walls)
            {
                if (w.Floor != room.Floor || w.Railing || w.Shaped || w.Length < 0.9f) continue;
                if (w.RoomA != room.Id && w.RoomB != room.Id) continue;
                var d = w.Dir;
                var n = new Vector3(-d.y, 0, d.x);
                var mid = w.At(w.Length / 2f);
                if (!InRoom(room.Floor, room.Id, mid.x + n.x * 0.3f, mid.y + n.z * 0.3f)) n = -n;
                if (!InRoom(room.Floor, room.Id, mid.x + n.x * 0.3f, mid.y + n.z * 0.3f)) continue;
                list.Add((w, n));
            }
            list.Sort((p, q) => q.w.Length.CompareTo(p.w.Length));
            return list;
        }

        void Islands(PlanRoom room, int count)
        {
            if (count <= 0) return;
            var spots = new List<Vector3>();
            var b = room.Bounds;
            for (float x = b.xMin + 1.5f; x < b.xMax - 1.4f; x += 0.5f)
                for (float z = b.yMin + 1.5f; z < b.yMax - 1.4f; z += 0.5f)
                    if (InRoom(room.Floor, room.Id, x, z)) spots.Add(new Vector3(x, LevelLayout.FloorY(room.Floor), z));
            // closest to the centre first, a little shuffled
            var c = room.Center;
            var keys = new Dictionary<Vector3, float>();
            foreach (var s in spots) keys[s] = (s - c).sqrMagnitude + (float)m_Rng.NextDouble() * 6f;
            spots.Sort((p, q) => keys[p].CompareTo(keys[q]));
            int placed = 0;
            foreach (var s in spots)
            {
                if (placed >= count) break;
                bool turn = Chance(0.5);
                var f = new Frame(s, turn ? Vector3.forward : Vector3.right, turn ? Vector3.left : Vector3.forward);
                var batch = new Batch();
                Island(room, f, batch);
                // a small room keeps a narrower way round its table (the walk check at placement has the last word)
                if (batch.Pieces.Count > 0 && TryCommit(room, batch, 0.05f, room.Area < 24f ? 0.8f : Aisle)) placed++;
            }
        }

        /// <summary>One island group of the room's type around frame origin.</summary>
        void Island(PlanRoom room, Frame f, Batch b)
        {
            double roll = m_Rng.NextDouble();
            switch (room.Type)
            {
                case RoomType.Living:
                    // the rug and the coffee table on it, a magazine or a cup
                    b.Add(PropKind.AreaRug, f.P(0, 0), f.Face, solid: false);
                    b.Add(PropKind.CoffeeTable, f.P(0, 0), f.Face);
                    if (roll < 0.6) b.Item(f.P(R(-0.3f, 0.3f), 0, 0.45f));
                    break;
                case RoomType.ChargingStation:
                    // the generator in the middle; later islands: battery stacks back to back
                    if (!m_GeneratorPlaced) { b.Add(PropKind.Generator, f.P(0, 0), f.Face); break; }
                    b.Add(PropKind.BatteryStack, f.P(0, 0.31f), f.Face);
                    b.Add(PropKind.BatteryStack, f.P(0, -0.31f), f.Back);
                    break;
                case RoomType.OpenSpace:
                    if (roll < 0.62) DeskCluster(f, b);
                    else if (roll < 0.78)
                    {   // standing desks back to back, a planter at the end
                        b.Add(PropKind.StandingDesk, f.P(0, 0.38f), f.Face);
                        b.Add(PropKind.StandingDesk, f.P(0, -0.38f), f.Back);
                        b.Add(PropKind.PlanterDivider, f.P(1.12f, 0), f.Face + 90f);
                        b.Item(f.P(R(-0.4f, 0.4f), Chance(0.5) ? 0.4f : -0.4f, 1.105f));
                    }
                    else if (roll < 0.88)
                    {   // two quiet pods facing each other over a pedestal
                        b.Add(PropKind.SofaPod, f.P(0, 0.75f), f.Back);
                        b.Add(PropKind.SofaPod, f.P(0, -0.75f), f.Face);
                    }
                    else { b.Add(PropKind.RoundTable, f.P(0, 0), 0); Chairs(f, b, 3); }
                    break;
                case RoomType.Meeting:
                    if (room.Area > 26f && roll < 0.55)
                    {   // the board table: chairs down both sides and one at each end
                        b.Add(PropKind.ConferenceTable, f.P(0, 0), f.Face);
                        for (int i = -1; i <= 1; i++)
                        {
                            b.Add(PropKind.MeetingChair, f.P(i * 1.1f, 0.95f), f.Back);
                            b.Add(PropKind.MeetingChair, f.P(i * 1.1f, -0.95f), f.Face);
                        }
                        b.Add(PropKind.MeetingChair, f.P(-2.15f, 0), f.Face + 90f);
                        b.Add(PropKind.MeetingChair, f.P(2.15f, 0), f.Face - 90f);
                        b.Item(f.P(R(-1.4f, 1.4f), R(-0.3f, 0.3f), 0.755f));
                    }
                    else if (room.Area > 20f && roll < 0.85)
                    {
                        b.Add(PropKind.MeetingTable, f.P(0, 0), f.Face);
                        for (int i = -1; i <= 1; i++)
                        {
                            b.Add(PropKind.MeetingChair, f.P(i * 1.1f, 1.0f), f.Back);
                            b.Add(PropKind.MeetingChair, f.P(i * 1.1f, -1.0f), f.Face);
                        }
                        b.Item(f.P(R(-1.2f, 1.2f), R(-0.3f, 0.3f), 0.78f));
                    }
                    else { b.Add(PropKind.RoundTable, f.P(0, 0), 0); Chairs(f, b, 4); b.Item(f.P(0, 0, 0.76f)); }
                    break;
                case RoomType.Kitchen:
                    if (roll < 0.35)
                    {   // the island: worktop, cupboards to the back, three stools at its overhang
                        b.Add(PropKind.KitchenIsland, f.P(0, 0), f.Face);
                        for (int i = -1; i <= 1; i++) b.Add(PropKind.BarStool, f.P(i * 0.62f, 0.78f), f.Back);
                        b.Item(f.P(R(-0.7f, 0.7f), 0, 0.925f));
                    }
                    else if (roll < 0.55)
                    {   // a breakfast bar: the counter and its stools on the far side
                        b.Add(PropKind.BarCounter, f.P(0, 0), f.Face);
                        for (int i = -1; i <= 1; i++) b.Add(PropKind.BarStool, f.P(i * 0.62f, 0.62f), f.Back);
                        b.Item(f.P(R(-0.6f, 0.6f), 0, 1.06f));
                    }
                    else if (roll < 0.85)
                    {   // the dining table, chairs round it
                        b.Add(PropKind.DiningTable, f.P(0, 0), f.Face);
                        foreach (float u in new[] { -0.42f, 0.42f })
                        {
                            b.Add(PropKind.Chair, f.P(u, 0.78f), f.Back);
                            b.Add(PropKind.Chair, f.P(u, -0.78f), f.Face);
                        }
                        if (Chance(0.5)) b.Add(PropKind.Chair, f.P(1.12f, 0), f.Face - 90f);
                        b.Item(f.P(R(-0.5f, 0.5f), R(-0.2f, 0.2f), 0.755f));
                    }
                    else
                    {
                        b.Add(PropKind.RoundTable, f.P(0, 0), 0);
                        Chairs(f, b, 3 + m_Rng.Next(2)); // the kitchen table, chairs all round
                        b.Item(f.P(R(-0.2f, 0.2f), 0, 0.76f));
                    }
                    break;
                case RoomType.Gym:
                    if (roll < 0.3) { b.Add(PropKind.WeightBench, f.P(0, 0), f.Face); b.Item(f.P(0.6f, 0.4f)); }
                    else if (roll < 0.5) { b.Add(PropKind.Treadmill, f.P(-0.5f, 0), f.Face); b.Add(PropKind.Treadmill, f.P(0.5f, 0), f.Face); }
                    else if (roll < 0.72) { b.Add(PropKind.PowerCage, f.P(0, 0), f.Face); b.Item(f.P(0, 0)); }
                    else { b.Add(PropKind.RowingMachine, f.P(-0.42f, 0), f.Face); b.Add(PropKind.RowingMachine, f.P(0.42f, 0), f.Face); }
                    break;
                case RoomType.Library:
                    if (roll < 0.38)
                    {   // a reading table, chairs along both sides
                        b.Add(PropKind.ReadingTable, f.P(0, 0), f.Face);
                        foreach (float u in new[] { -0.5f, 0.5f })
                        {
                            b.Add(PropKind.Chair, f.P(u, 0.75f), f.Back);
                            b.Add(PropKind.Chair, f.P(u, -0.75f), f.Face);
                        }
                        b.Item(f.P(R(-0.7f, 0.7f), R(-0.2f, 0.2f), 0.77f));
                    }
                    else if (roll < 0.72)
                    {   // a double-sided stack of bookcases
                        foreach (float u in new[] { -0.5f, 0.5f })
                        {
                            b.Add(PropKind.TallBookcase, f.P(u, 0.18f), f.Face);
                            b.Add(PropKind.TallBookcase, f.P(u, -0.18f), f.Back);
                        }
                        b.Item(f.P(R(-0.8f, 0.8f), Chance(0.5) ? 0.2f : -0.2f, BookcaseLevels[m_Rng.Next(3)]));
                    }
                    else
                    {   // study carrels back to back, a chair at each
                        b.Add(PropKind.StudyCarrel, f.P(0, 0.36f), f.Face);
                        b.Add(PropKind.StudyCarrel, f.P(0, -0.36f), f.Back);
                        b.Add(PropKind.Chair, f.P(0, 1.05f), f.Back);
                        b.Add(PropKind.Chair, f.P(0, -1.05f), f.Face);
                        b.Item(f.P(R(-0.3f, 0.3f), 0.45f, 0.765f));
                    }
                    break;
                case RoomType.CopyRoom:
                    if (roll < 0.5)
                    {
                        b.Add(PropKind.Desk, f.P(0, 0.4f), f.Face);
                        b.Add(PropKind.Desk, f.P(0, -0.4f), f.Back);
                        b.Add(PropKind.PaperStack, f.P(1.3f, 0), f.Face);
                        b.Item(f.P(R(-0.5f, 0.5f), 0.3f, 0.77f));
                    }
                    else
                    {   // the binding table, the mail cart beside it
                        b.Add(PropKind.WorkTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.MailCart, f.P(1.35f, 0), f.Face + 90f);
                        b.Item(f.P(R(-0.5f, 0.5f), R(-0.2f, 0.2f), 0.905f));
                    }
                    break;
                case RoomType.ControlRoom:
                    if (roll < 0.5)
                    {
                        b.Add(PropKind.ConsoleDesk, f.P(0, 0), f.Face);
                        b.Add(PropKind.OperatorChair, f.P(-0.6f, 0.85f), f.Back);
                        b.Add(PropKind.OperatorChair, f.P(0.6f, 0.85f), f.Back);
                        b.Item(f.P(R(-0.9f, 0.9f), 0.2f, 0.77f));
                    }
                    else
                    {   // the supervisor's desk: screens at its back, two operators at its front
                        b.Add(PropKind.SupervisorDesk, f.P(0, 0), f.Face);
                        b.Add(PropKind.OperatorChair, f.P(-0.45f, 0.85f), f.Back);
                        b.Add(PropKind.OperatorChair, f.P(0.45f, 0.85f), f.Back);
                        b.Item(f.P(R(-0.6f, 0.6f), 0.2f, 1.105f));
                    }
                    break;
                case RoomType.Lab:
                    if (roll < 0.45)
                    {   // two benches back to back
                        b.Add(PropKind.LabBench, f.P(0, 0.4f), f.Face);
                        b.Add(PropKind.LabBench, f.P(0, -0.4f), f.Back);
                        b.Add(PropKind.Chair, f.P(R(-0.4f, 0.4f), 1.2f), f.Back);
                        b.Add(PropKind.Chair, f.P(R(-0.4f, 0.4f), -1.2f), f.Face);
                        b.Item(f.P(R(-0.6f, 0.6f), Chance(0.5) ? 0.5f : -0.5f, 0.91f));
                    }
                    else if (roll < 0.62) { b.Add(PropKind.RobotArm, f.P(0, 0), f.Face + m_Rng.Next(4) * 90f); b.Item(f.P(0.7f, 0.4f)); }
                    else
                    {   // the robot on its table, a stool each side and the scope cart
                        b.Add(PropKind.RobotTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.Chair, f.P(-0.5f, 0.85f), f.Back);
                        b.Add(PropKind.ScopeCart, f.P(1.3f, 0), f.Face - 90f);
                        b.Item(f.P(0.35f, 0.1f, 0.905f));
                    }
                    break;
                case RoomType.NapRoom:
                    if (roll < 0.32) b.Add(PropKind.Hammock, f.P(0, 0), f.Face);
                    else if (roll < 0.6)
                    {
                        b.Add(PropKind.CoffeeTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.BeanBag, f.P(-1.0f, 0.2f), f.Face + 60f);
                        b.Add(PropKind.BeanBag, f.P(1.0f, 0.2f), f.Face - 60f);
                        b.Item(f.P(0, 0, 0.42f));
                    }
                    else if (roll < 0.8)
                    {   // a daybed screened off by its curtain, the night table at its head
                        b.Add(PropKind.Daybed, f.P(0, 0), f.Face);
                        b.Add(PropKind.CurtainDivider, f.P(0, -0.62f), f.Face);
                        b.Add(PropKind.NightTable, f.P(1.3f, -0.1f), f.Face);
                        b.Item(f.P(1.3f, -0.1f, 0.405f));
                    }
                    else { b.Add(PropKind.FloorCushions, f.P(0, 0), f.Face); b.Add(PropKind.FloorLamp, f.P(0.95f, -0.4f), f.Face); }
                    break;
                case RoomType.Studio:
                    if (roll < 0.4) { b.Add(PropKind.DrumKit, f.P(0, 0), f.Face); }
                    else if (roll < 0.7)
                    {   // two voices at their mics, facing each other
                        b.Add(PropKind.MicStand, f.P(0, 0.45f), f.Back);
                        b.Add(PropKind.MicStand, f.P(0, -0.45f), f.Face);
                        b.Add(PropKind.BarStool, f.P(0, 1.0f), f.Back);
                        b.Add(PropKind.BarStool, f.P(0, -1.0f), f.Face);
                    }
                    else
                    {   // the keyboard player: synth, stool, an amp beside
                        b.Add(PropKind.SynthStand, f.P(0, 0), f.Face);
                        b.Add(PropKind.BarStool, f.P(0, 0.75f), f.Back);
                        b.Add(PropKind.AmpCabinet, f.P(1.1f, 0), f.Face);
                    }
                    break;
                case RoomType.LockerRoom:
                    if (roll < 0.5) { b.Add(PropKind.Bench, f.P(0, 0), f.Face); if (Chance(0.5)) b.Item(f.P(R(-0.5f, 0.5f), 0, 0.46f)); }
                    else { b.Add(PropKind.BenchIsland, f.P(0, 0), f.Face); b.Item(f.P(R(-0.6f, 0.6f), Chance(0.5) ? 0.25f : -0.25f, 0.455f)); }
                    break;
                case RoomType.Infirmary:
                    if (roll < 0.5)
                    {
                        b.Add(PropKind.HospitalBed, f.P(0, 0), f.Face);
                        b.Add(PropKind.PrivacyScreen, f.P(1.25f, 0), f.Face + 90f);
                        b.Add(PropKind.IvStand, f.P(-0.8f, -0.6f), f.Face);
                        b.Item(f.P(0, 0.6f, 0.66f));
                    }
                    else
                    {   // the examination couch, the drip by it
                        b.Add(PropKind.ExamTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.IvStand, f.P(0.65f, -0.55f), f.Face);
                        b.Item(f.P(0, 0.3f, 0.725f));
                    }
                    break;
                case RoomType.GameRoom:
                    int game = m_Rng.Next(5);
                    if (game == 0) { b.Add(PropKind.PoolTable, f.P(0, 0), f.Face); b.Item(f.P(R(-0.8f, 0.8f), R(-0.3f, 0.3f), 0.81f)); }
                    else if (game == 1) { b.Add(PropKind.Foosball, f.P(0, 0), f.Face); b.Item(f.P(R(-0.4f, 0.4f), 0, 0.77f)); }
                    else if (game == 2) { b.Add(PropKind.PingPongTable, f.P(0, 0), f.Face); b.Item(f.P(0.6f, 0, 0.78f)); }
                    else if (game == 3) { b.Add(PropKind.AirHockey, f.P(0, 0), f.Face); b.Item(f.P(R(-0.6f, 0.6f), 0, 0.85f)); }
                    else { b.Add(PropKind.PokerTable, f.P(0, 0), f.Face); Chairs(f, b, 4); b.Item(f.P(R(-0.3f, 0.3f), R(-0.3f, 0.3f), 0.785f)); }
                    break;
                case RoomType.Server: // a cold aisle between two rows of racks
                    if (roll < 0.75)
                    {
                        for (int i = -1; i <= 1; i++)
                        {
                            b.Add(PropKind.ServerRack, f.P(i * 0.72f, 1.25f), f.Back);
                            b.Add(PropKind.ServerRack, f.P(i * 0.72f, -1.25f), f.Face);
                        }
                        b.Item(f.P(R(-0.8f, 0.8f), 0));
                    }
                    else
                    {   // the tape library back to back with the power: a block in the middle of the floor
                        b.Add(PropKind.TapeLibrary, f.P(-0.3f, 0.42f), f.Face);
                        b.Add(PropKind.UpsUnit, f.P(0.6f, -0.42f), f.Back + 90f);
                        b.Add(PropKind.NetworkCabinet, f.P(-0.55f, -0.33f), f.Back);
                    }
                    break;
                case RoomType.Workshop: // loose things on the floor between the bays and the benches
                    if (roll < 0.3) b.Add(PropKind.TireStack, f.P(0, 0), 0);
                    else if (roll < 0.55) { b.Add(PropKind.EngineStand, f.P(0, 0), f.Face); b.Item(f.P(-0.2f, 0, 1.14f)); }
                    else if (roll < 0.75) b.Add(PropKind.OilDrain, f.P(0, 0), 0);
                    else if (roll < 0.9) b.Add(PropKind.FloorJack, f.P(0, 0), f.Face);
                    else { b.Add(PropKind.TireStack, f.P(-0.45f, 0), 0); b.Add(PropKind.TireStack, f.P(0.45f, 0), 0); }
                    break;
                case RoomType.NurseStation: // the counter facing the corridor's way, the nurses' chairs behind, a cart beside
                    b.Add(PropKind.NurseStationDesk, f.P(0, 0), f.Face);
                    b.Add(PropKind.Chair, f.P(-0.7f, -0.95f), f.Face);
                    b.Add(PropKind.Chair, f.P(0.7f, -0.95f), f.Face);
                    b.Item(f.P(R(-1f, 1f), 0.1f, 0.78f));
                    break;
                case RoomType.Operating: // the table under its lamps, the anaesthesia machine at the head, the instruments at the side
                    b.Add(PropKind.SurgicalTable, f.P(0, 0), f.AlongU);
                    b.Add(PropKind.AnesthesiaMachine, f.P(-1.75f, 0), f.AlongU);
                    b.Add(PropKind.InstrumentTable, f.P(0.3f, 1.5f), f.Back);
                    b.Add(PropKind.VitalsMonitor, f.P(-1.6f, 1.0f), f.Back);
                    break;
                case RoomType.Imaging: // a CT scanner where it fits, else the X-ray room's table and tube stand
                    if (room.Area >= 30f && roll < 0.6) b.Add(PropKind.CtScanner, f.P(0, 0), f.AlongU);
                    else b.Add(PropKind.XrayMachine, f.P(0, 0), f.AlongU);
                    break;
                case RoomType.PaintBooth: // the car being painted, lengthwise in the booth
                    b.Add(PropKind.CarParked, f.P(0, 0), f.AlongU);
                    break;
                case RoomType.Storage: // a wrapped pallet and a stack of boxes by the pile
                    b.Add(PropKind.Pallet, f.P(0, 0), m_Rng.Next(2) * 90f);
                    b.Add(PropKind.Boxes, f.P(0.2f, 1.2f), f.Face);
                    break;
                case RoomType.Lounge:
                    if (roll < 0.22) { b.Add(PropKind.PingPongTable, f.P(0, 0), f.Face); b.Item(f.P(0.6f, 0, 0.78f)); }
                    else if (roll < 0.72)
                    {   // the living corner on a rug: coffee table, sofa, armchairs at its ends
                        b.Add(PropKind.AreaRug, f.P(0, -0.3f), f.Face, solid: false);
                        b.Add(PropKind.CoffeeTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.Sofa, f.P(0, -1.15f), f.Face);
                        b.Add(PropKind.Armchair, f.P(-1.35f, 0.2f), f.Face + 90f);
                        if (Chance(0.6)) b.Add(PropKind.Armchair, f.P(1.35f, 0.2f), f.Face - 90f);
                        b.Item(f.P(0, 0, 0.42f));
                    }
                    else
                    {   // the big sectional round a coffee table
                        b.Add(PropKind.SectionalSofa, f.P(0, -0.55f), f.Face);
                        b.Add(PropKind.AreaRug, f.P(0, 0.6f), f.Face, solid: false);
                        b.Add(PropKind.CoffeeTable, f.P(0, 0.85f), f.Face);
                        b.Item(f.P(0, 0.85f, 0.42f));
                    }
                    break;
                case RoomType.Lobby when m_L.PlanKind == HouseKind.Hospital:
                    // the admissions desk, rows of waiting seats back to back
                    if (roll < 0.45)
                    {
                        b.Add(PropKind.ReceptionDesk, f.P(0, 0), f.Face);
                        b.Add(PropKind.Chair, f.P(0, -0.85f), f.Face);
                        b.Item(f.P(-0.4f, 0.33f, 1.14f));
                    }
                    else
                    {
                        b.Add(PropKind.WaitingSeats, f.P(0, 0.35f), f.Back);
                        b.Add(PropKind.WaitingSeats, f.P(0, -0.35f), f.Face);
                    }
                    break;
                case RoomType.Lobby when m_L.PlanKind == HouseKind.Garage:
                    // the shop's front desk: the service counter, a row of waiting chairs facing it, a stack of tyres on show
                    if (roll < 0.45)
                    {
                        b.Add(PropKind.ReceptionDesk, f.P(0, 0), f.Face);
                        b.Add(PropKind.Chair, f.P(0, -0.85f), f.Face);
                        b.Item(f.P(-0.4f, 0.33f, 1.14f));
                    }
                    else if (roll < 0.8)
                        for (int i = -1; i <= 1; i++) b.Add(PropKind.GuestChair, f.P(i * 0.65f, 0), f.Face);
                    else { b.Add(PropKind.TireStack, f.P(-0.45f, 0), 0f); b.Add(PropKind.TireStack, f.P(0.45f, 0), 0f); }
                    break;
                case RoomType.Lobby:
                    if (roll < 0.32)
                    {   // the reception, the queue lines in front of it
                        b.Add(PropKind.ReceptionDesk, f.P(0, 0), f.Face);
                        b.Add(PropKind.Chair, f.P(0, -0.85f), f.Face);
                        b.Add(PropKind.QueueBarrier, f.P(-0.85f, 1.55f), f.Face);
                        b.Add(PropKind.QueueBarrier, f.P(0.85f, 1.55f), f.Face);
                        b.Item(f.P(-0.4f, 0.33f, 1.14f));
                    }
                    else if (roll < 0.55) b.Add(PropKind.RoundBenchPlanter, f.P(0, 0), 0f);
                    else if (roll < 0.7)
                    {   // the security line: turnstiles side by side, a kiosk at the end
                        for (int i = -1; i <= 1; i++) b.Add(PropKind.SecurityTurnstile, f.P(i * 1.22f, 0), f.Face);
                        b.Add(PropKind.InfoKiosk, f.P(2.2f, 0), f.Face);
                    }
                    else
                    {
                        b.Add(PropKind.CoffeeTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.Armchair, f.P(-1.1f, 0), f.Face + 90f);
                        b.Add(PropKind.Armchair, f.P(1.1f, 0), f.Face - 90f);
                        b.Item(f.P(0, 0, 0.42f));
                    }
                    break;
                case RoomType.Office:
                    if (roll < 0.5)
                    {   // the boss's desk in the middle: the chair behind it (at its drawers: the model's front), two guests in front
                        b.Add(PropKind.ExecutiveDesk, f.P(0, 0), f.Back);
                        b.Add(PropKind.Chair, f.P(0, -0.8f), f.Face);
                        b.Add(PropKind.GuestChair, f.P(-0.45f, 0.85f), f.Back);
                        b.Add(PropKind.GuestChair, f.P(0.45f, 0.85f), f.Back);
                        b.Item(f.P(R(-0.5f, 0.5f), 0.1f, 0.765f));
                    }
                    else
                    {
                        b.Add(PropKind.CoffeeTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.Armchair, f.P(0, 0.85f), f.Back);
                        b.Add(PropKind.Armchair, f.P(0, -0.85f), f.Face);
                        b.Item(f.P(0, 0, 0.42f));
                    }
                    break;
                case RoomType.Hall:
                    if (roll < 0.4) b.Add(PropKind.Sculpture, f.P(0, 0), f.Face);
                    else if (roll < 0.7) b.Add(PropKind.RoundBenchPlanter, f.P(0, 0), 0f);
                    else
                    {
                        b.Add(PropKind.CoffeeTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.Armchair, f.P(0, 0.85f), f.Back);
                        b.Add(PropKind.Armchair, f.P(0, -0.85f), f.Face);
                        b.Item(f.P(0, 0, 0.42f));
                    }
                    break;
            }
        }

        /// <summary>The metal shelving's decks within reach (Tools/Blender/rooms2_tech.py).</summary>
        static readonly float[] StorageDecks = { 0.61f, 1.09f, 1.57f };

        /// <summary>Back-to-back cluster of four desks with privacy screens, a planter closing one end sometimes; loot on some of them.</summary>
        void DeskCluster(Frame f, Batch b)
        {
            foreach (float u in new[] { -0.8f, 0.8f })
            {
                b.Add(PropKind.Desk, f.P(u, 0.4f), f.Face);
                b.Add(PropKind.Chair, f.P(u + R(-0.1f, 0.1f), 1.15f), f.Back + R(-12f, 12f));
                b.Add(PropKind.Desk, f.P(u, -0.4f), f.Back);
                b.Add(PropKind.Chair, f.P(u + R(-0.1f, 0.1f), -1.15f), f.Face + R(-12f, 12f));
                b.Add(PropKind.Partition, f.P(u, 0f), f.Face, solid: false);
                if (Chance(0.6)) b.Item(f.P(u + (Chance(0.5) ? -0.5f : 0.5f), Chance(0.5) ? 0.2f : -0.2f, DeskTop + 0.005f));
                if (Chance(0.35)) b.Add(PropKind.DeskLamp, f.P(u + 0.55f, 0.55f, DeskTop), f.Back, solid: false);
            }
            if (Chance(0.5)) b.Add(PropKind.PlanterDivider, f.P(Chance(0.5) ? 1.82f : -1.82f, 0), f.Face + 90f);
        }

        /// <summary>
        /// A kitchen whose middle had no room for the table with chairs all round (its middle stays free for the robots'
        /// patrol stop): a round table with a chair on each of two sides and a narrower walkway, or else the table against a
        /// wall with chairs on its three open sides.
        /// </summary>
        void SmallTable(PlanRoom room)
        {
            if (FreeTable(room)) return;
            float y = LevelLayout.FloorY(room.Floor);
            foreach (var (w, n) in FacesOf(room))
            {
                var u = new Vector3(w.Dir.x, 0, w.Dir.y);
                for (float t = 1.4f; t < w.Length - 1.4f; t += 0.25f)
                {
                    var face = new Vector3(w.A.x, y, w.A.y) + u * t + n * (LevelMetrics.WallThickness / 2f);
                    var f = new Frame(face, u, n);
                    var batch = new Batch();
                    var table = f.P(0, 0.5f + WallGap);
                    batch.Add(PropKind.RoundTable, table, 0);
                    foreach (var d in new[] { f.U, -f.U, f.N }) batch.Add(PropKind.Chair, table + d * 1.0f, Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg);
                    batch.Item(table + Vector3.up * 0.76f);
                    if (TryCommit(room, batch, 0.05f, front: n)) return;
                }
            }
        }

        bool FreeTable(PlanRoom room)
        {
            var b = room.Bounds;
            float y = LevelLayout.FloorY(room.Floor);
            var spots = new List<Vector3>();
            for (float x = b.xMin + 1.1f; x < b.xMax - 1.0f; x += 0.25f)
                for (float z = b.yMin + 1.1f; z < b.yMax - 1.0f; z += 0.25f)
                    if (InRoom(room.Floor, room.Id, x, z)) spots.Add(new Vector3(x, y, z));
            var c = room.Center;
            spots.Sort((p, q) => (p - c).sqrMagnitude.CompareTo((q - c).sqrMagnitude));
            // the chairs across the room: the robots' stop is along its long side (LevelGenerator.PatrolCentre)
            bool wide = b.width >= b.height;
            // two chairs, else one (a doorway's free floor on one side)
            foreach (int chairs in new[] { 2, 1 })
                foreach (var s in spots)
                    foreach (float side in chairs == 2 ? new[] { 1f } : new[] { 1f, -1f })
                    {
                        var f = new Frame(s, wide ? Vector3.forward : Vector3.right, wide ? Vector3.right : Vector3.forward);
                        var batch = new Batch();
                        batch.Add(PropKind.RoundTable, f.P(0, 0), 0);
                        foreach (var d in chairs == 2 ? new[] { f.U, -f.U } : new[] { f.U * side })
                            batch.Add(PropKind.Chair, f.O + d * 1.0f, Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg);
                        batch.Item(f.P(0, 0, 0.76f));
                        // a walkway round it, clear of other furniture (it may cross a doorway's or the patrol stop's free
                        // floor: those are walkways too)
                        var all = FootAt(PropKind.RoundTable, f.O, 0);
                        foreach (var p in batch.Pieces) all = Union(all, FootAt(p.Kind, p.Pos, p.Yaw));
                        if (m_Taken[room.Floor].Overlaps(Grow(all, 0.7f)) || !OnFloor(room.Floor, room.Id, all, 0.5f)) continue;
                        if (TryCommit(room, batch, 0.05f)) return true;
                    }
            return false;
        }

        void Chairs(Frame f, Batch b, int n)
        {
            // around a round table: on its four sides, facing it
            var sides = new[] { -f.N, f.U, f.N, -f.U };
            for (int i = 0; i < Mathf.Min(n, 4); i++)
            {
                var d = sides[i];
                b.Add(PropKind.Chair, f.O + d * 1.0f, Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg);
            }
        }
    }
}
