using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The apartment block's plan (<see cref="BuildingSettings.Apartments"/>), laid out like a real one-entrance block: a
    /// short slab, more storeys than other houses and a few flats on each. Everything that runs through the storeys stands
    /// one above the other, as in a real block: one stairwell at the end of the back with its flights stacked floor over
    /// floor (<see cref="Stair.Stacked"/>), and every floor above the ground the same typical floor — the same flats, so
    /// the bathrooms and kitchens (their risers and vent shafts) stack too, and the same doors and crawl ways. On every
    /// floor the landing runs the building's length; two flats at the front, one at the back beside the stairs. The ground
    /// floor keeps the flats that fit under the typical ones; in front of the stairs is the entrance hall (the only way in
    /// from the street) and beside it the plant room with the building's drain (the entry hatch) and a store or the
    /// caretaker's office. A flat is a hallway and a bathroom on the landing's side and its kitchen and rooms by the
    /// windows; flats never open into each other (only crawl ways do), and only the hallway opens onto the landing.
    /// </summary>
    public static partial class PlanGenerator
    {
        sealed class ApartmentLayout
        {
            public int Width;                // cells
            public bool SideLeft;            // the back flat is at low x, the stairwell at the high end (else mirrored)
            public int Side;                 // cells: the back flat's width
            public RectInt Landing;          // cells, the same on every floor
            public RectInt FrontBand, BackBand;
            public RectInt Stairwell;        // cells, the same on every floor
            public RectInt Lift;             // cells: the lift's shaft between the back flat and the stairwell (width 0: no lift)
            public int NextFlat;
        }

        /// <summary>
        /// Depths across the slab, metres, as in a real panel block (the P-44T: a 15–17 m deep body): the flats at the front
        /// (7), the landing (3), the flats, the lift and the stairs at the back (7).
        /// </summary>
        const float FlatDepth = 7f;
        const int LandingDepth = 3;
        /// <summary>The stairwell's width, metres: the two flights side by side (<see cref="StairGeometry"/> lanes, 4 m).</summary>
        const int StairwellWidth = 4;
        /// <summary>A flat's hallway strip (with the bathroom) on the landing's side, cells (3 m: the bathroom's narrow door fits its wall); the rest of its depth (4 m) is its rooms.</summary>
        const int FlatInner = 6;
        /// <summary>The kitchen's width along the facade, cells (3 m: 12 m², a real block's 8–13).</summary>
        const int FlatKitchen = 6;
        /// <summary>A bedroom's width along the facade, cells (3.5 m: 14 m²).</summary>
        const int FlatBedroom = 7;
        /// <summary>The ventilation shaft by each flat's bathroom, cells (1 x 0.5 m), one above the other through every floor.</summary>
        const int ShaftLong = 2, ShaftShort = 1;
        /// <summary>The bathroom's width, cells (3 m: the tub, the basin and the toilet beside the door's swing; our doors are wider than a real block's).</summary>
        const int FlatBath = 6;
        /// <summary>The entrance hall's width at the front, cells (5 m), in front of the stairwell.</summary>
        const int EntranceCells = 10;
        /// <summary>The lift's shaft, metres wide (beside the stairwell, the back band's whole depth: sealed but for its doorway).</summary>
        const int LiftWidth = 3;
        /// <summary>The lift's well, cells deep from the landing (2.5 m): where the cab rides; the shaft behind it is sealed.</summary>
        const int LiftWellCells = 5;

        static List<RectInt> ApartmentsMass(Ctx c, System.Random rng, out string kind)
        {
            int depthCells = Mathf.RoundToInt(FlatDepth * M) * 2 + LandingDepth * M;
            // the back flat from 9 m to 12 m (two rooms: 54–72 m²), the front ones 8 to 9.5 m (48–57 m²)
            int side = 9 + rng.Next(4);
            // a lift beside the stairs: always in a tall block, sometimes in a lower one (its own stream)
            bool lift = c.Floors >= c.B.LiftAlwaysFrom || c.Rng(0x11F7).NextDouble() < c.B.LiftChance;
            int liftW = lift ? LiftWidth : 0;
            int width = side + liftW + StairwellWidth;
            int front = Mathf.RoundToInt(FlatDepth * M);
            var a = new ApartmentLayout
            {
                Width = width * M,
                SideLeft = rng.Next(2) == 0,
                Side = side * M,
                Landing = new RectInt(0, front, width * M, LandingDepth * M),
                FrontBand = new RectInt(0, 0, width * M, front),
                BackBand = new RectInt(0, front + LandingDepth * M, width * M, front),
            };
            if (lift) a.Lift = new RectInt(ApX(a, a.Side, liftW * M), a.BackBand.yMin, liftW * M, a.BackBand.height);
            a.Stairwell = new RectInt(ApX(a, a.Side + liftW * M, StairwellWidth * M), a.BackBand.yMin, StairwellWidth * M, a.BackBand.height);
            c.Apartments = a;
            kind = "apartments";
            return new List<RectInt> { new(0, 0, width, depthCells / M) };
        }

        /// <summary>x of a rectangle measured from the back flat's side (mirrors when that flat is at high x).</summary>
        static int ApX(ApartmentLayout a, int x, int w) => a.SideLeft ? x : a.Width - x - w;

        /// <summary>The entrance hall at the front in front of the stairwell; one stairwell, a staircase from every floor in it.</summary>
        static void ReserveApartments(Ctx c)
        {
            var a = c.Apartments;
            c.Lobby = new RectInt(ApX(a, a.Width - EntranceCells, EntranceCells), 0, EntranceCells, a.FrontBand.height);
            // the flights from the back wall towards the landing (Stacked: one above the other), against the end wall
            var corner = new Vector2Int(a.SideLeft ? 1 : -1, 1);
            for (int f = 0; f < c.Floors - 1; f++) c.Halls.Add((f, a.Stairwell, corner));
            if (a.Lift.width > 0) c.Plan.LiftWell = new RectInt(a.Lift.xMin, a.Lift.yMin, a.Lift.width, LiftWellCells);
        }

        /// <summary>
        /// One floor of the block: the landing, then the typical floor's flats — two at the front, one at the back beside
        /// the stairs. On the ground floor the front flat under the stairs' end gives way to the entrance hall, the plant
        /// room and a store.
        /// </summary>
        static void ZoneApartments(Ctx c, int f, List<Region> regions, int[,] owner, System.Func<RectInt, Region, int> claim)
        {
            var a = c.Apartments;
            claim(a.Landing, new Region { Type = RoomType.Corridor, Shape = RoomShape.Corridor, Fixed = true });
            int half = a.Width / 2;
            var frontFar = new RectInt(ApX(a, 0, half), a.FrontBand.yMin, half, a.FrontBand.height);
            var frontNear = new RectInt(ApX(a, half, a.Width - half), a.FrontBand.yMin, a.Width - half, a.FrontBand.height);
            var back = new RectInt(ApX(a, 0, a.Side), a.BackBand.yMin, a.Side, a.BackBand.height);
            Flat(c, frontFar, true, claim);
            Flat(c, back, false, claim);
            if (f > 0) Flat(c, frontNear, true, claim);
            else
            {
                // beside the entrance hall: the plant room (the drain, the entry hatch) and a store or the caretaker's office
                bool storage = false;
                if (c.Program != null) foreach (var p in c.Program.Floors[0]) storage |= p.Type == RoomType.Storage && p.Required;
                int rest = frontNear.width - EntranceCells;
                int plantW = rest >= 12 ? 6 : rest;
                var plant = new RectInt(ApX(a, half + rest - plantW, plantW), a.FrontBand.yMin, plantW, a.FrontBand.height);
                claim(plant, new Region { Type = RoomType.Utility, Shape = RoomShape.Rect, Fixed = true, Typed = true });
                if (rest > plantW)
                    claim(new RectInt(ApX(a, half, rest - plantW), a.FrontBand.yMin, rest - plantW, a.FrontBand.height),
                        new Region { Type = storage ? RoomType.Storage : RoomType.Office, Shape = RoomShape.Rect, Fixed = true, Typed = true });
            }
            AssignTypes(c, f, regions);
            Commit(c, f, regions, owner);
            // the lift's shaft: sealed on every floor (nobody's room, nothing placed), its doorway is cut onto the landing
            void Seal(RectInt r)
            {
                for (int x = r.xMin; x < r.xMax; x++)
                    for (int z = r.yMin; z < r.yMax; z++) c.Plan.Set(f, x, z, CellKind.Pocket, -1);
            }
            Seal(a.Lift);
            // the ventilation shafts by the typical floor's bathrooms: through every floor, the ground floor's rooms too
            Seal(Shaft(c, frontFar, true));
            Seal(Shaft(c, frontNear, true));
            Seal(Shaft(c, back, false));
        }

        /// <summary>Which end of a flat its bathroom is at (the flat's own stream: the same on every floor).</summary>
        static bool BathHigh(Ctx c, RectInt r, bool front)
        {
            bool roll = c.Rng(0xF1A7, r.xMin * 2 + (front ? 1 : 0)).Next(2) == 0;
            // the back flat's bathroom at its end away from the stairwell (the stairs' approach reaches through that wall)
            return front ? roll : !c.Apartments.SideLeft;
        }

        /// <summary>
        /// A flat's ventilation shaft (cells): a box in its bathroom's outer corner against the landing, beside the neighbour's
        /// — a real block's shafts run beside the bathrooms, floor over floor.
        /// </summary>
        static RectInt Shaft(Ctx c, RectInt r, bool front)
        {
            bool high = BathHigh(c, r, front);
            int x = high ? r.xMax - ShaftLong : r.xMin; // the bathroom's outer corner against the landing
            int z = front ? r.yMax - ShaftShort : r.yMin;
            return new RectInt(x, z, ShaftLong, ShaftShort);
        }

        /// <summary>A balcony, metres: as wide as a real block's (the P-44T's "boat" loggia: 3.2 x 0.85 m), a little deeper to step out on.</summary>
        const float BalconyLength = 3.2f, BalconyDepth = 1.1f;

        /// <summary>
        /// Balconies on the floors above the ground: a flat's living room (some flats' kitchens) on the front or back facade gets
        /// one — a door in its outer wall and the slab outside. Decided per flat place (its own stream), so they stand one above
        /// the other on every floor like a real block's.
        /// </summary>
        static void Balconies(Ctx c)
        {
            var plan = c.Plan;
            plan.Balconies.Clear();
            float depth = plan.NZ * HousePlan.Res;
            for (int i = 0; i < plan.Walls.Count; i++)
            {
                var w = plan.Walls[i];
                if (w.Floor == 0 || !w.Exterior || w.Shaped || w.Length < BalconyLength + 0.6f) continue;
                int room = Mathf.Max(w.RoomA, w.RoomB);
                if (room < 0) continue;
                var r = plan.Rooms[room];
                if (r.Flat < 0 || r.Type is not (RoomType.Living or RoomType.Kitchen)) continue;
                // the long facades only (front z = 0, back z = depth), not the end walls
                bool front = Mathf.Abs(w.A.y) < 0.01f && Mathf.Abs(w.B.y) < 0.01f, rear = Mathf.Abs(w.A.y - depth) < 0.01f && Mathf.Abs(w.B.y - depth) < 0.01f;
                if (!front && !rear) continue;
                var rect = r.Rects.Count > 0 ? r.Rects[0] : default;
                var roll = c.Rng(0xBA1C, rect.xMin * 4 + (front ? 1 : 0) + (r.Type == RoomType.Kitchen ? 2 : 0)).NextDouble();
                if (roll >= (r.Type == RoomType.Living ? 0.8 : 0.3)) continue;
                float at = w.Length / 2f;
                if (!plan.FreeOn(i, at, BalconyLength, 0.3f)) continue;
                plan.Openings.Add(new PlanOpening { Wall = i, Kind = EdgeType.Door, At = at, Width = LevelMetrics.DoorWidth });
                var mid = w.At(at);
                plan.Balconies.Add(new PlanBalcony { Floor = w.Floor, Center = mid, Out = new Vector2(0f, front ? -1f : 1f), Length = BalconyLength, Depth = BalconyDepth });
            }
        }

        /// <summary>The lift's doorway on every floor: in the middle of the shaft's wall onto the landing.</summary>
        static void LiftDoorways(Ctx c)
        {
            var plan = c.Plan;
            if (!plan.HasLift) return;
            var well = plan.LiftWell;
            float z = well.yMin * HousePlan.Res, x0 = well.xMin * HousePlan.Res, x1 = well.xMax * HousePlan.Res;
            for (int i = 0; i < plan.Walls.Count; i++)
            {
                var w = plan.Walls[i];
                if (w.Exterior || (w.RoomA >= 0) == (w.RoomB >= 0)) continue;
                int room = Mathf.Max(w.RoomA, w.RoomB);
                if (plan.Rooms[room].Type != RoomType.Corridor) continue;
                var mid = (w.A + w.B) / 2f;
                if (Mathf.Abs(w.A.y - z) > 0.01f || Mathf.Abs(w.B.y - z) > 0.01f || mid.x < x0 || mid.x > x1) continue;
                plan.Openings.Add(new PlanOpening { Wall = i, Kind = EdgeType.Lift, At = w.Length / 2f, Width = LevelMetrics.LiftDoorWidth });
            }
        }

        /// <summary>
        /// A flat: the hallway and the bathroom along the landing, the kitchen, a bedroom (a wide flat) and the living room
        /// along the facade, the kitchen at the hallway's end away from the bathroom so its door is off the hallway; a narrow
        /// flat is a studio. Which end the bathroom is at follows from the flat's place, not its floor: the flats of every
        /// floor are the same and their bathrooms stack.
        /// </summary>
        static void Flat(Ctx c, RectInt r, bool front, System.Func<RectInt, Region, int> claim)
        {
            int id = c.Apartments.NextFlat++;
            void Room(RectInt rect, RoomType t) => claim(rect, new Region { Type = t, Shape = RoomShape.Rect, Fixed = true, Typed = true, Flat = id });
            // the landing is at the band's high z for the front flats, at its low z for the back ones
            int innerZ = front ? r.yMax - FlatInner : r.yMin, outerZ = front ? r.yMin : r.yMin + FlatInner, outerD = r.height - FlatInner;
            bool bathHigh = BathHigh(c, r, front);
            int bath = FlatBath; // (a studio's too: the tub needs the room beside the door's swing)
            int bathX = bathHigh ? r.xMax - bath : r.xMin;
            Room(new RectInt(bathX, innerZ, bath, FlatInner), RoomType.Bathroom);
            Room(new RectInt(bathHigh ? r.xMin : r.xMin + bath, innerZ, r.width - bath, FlatInner), RoomType.FlatHall);
            var rooms = new List<(RoomType type, int w)>();
            if (r.width < 14) rooms.Add((RoomType.Living, r.width));
            else
            {
                rooms.Add((RoomType.Kitchen, FlatKitchen));
                if (r.width >= 20) rooms.Add((RoomType.Bedroom, FlatBedroom));
                int used = 0;
                foreach (var (_, w) in rooms) used += w;
                rooms.Add((RoomType.Living, r.width - used));
            }
            // from the hallway's far end (away from the bathroom): the kitchen first
            int x = bathHigh ? r.xMin : r.xMax;
            foreach (var (type, w) in rooms)
            {
                var rect = bathHigh ? new RectInt(x, outerZ, w, outerD) : new RectInt(x - w, outerZ, w, outerD);
                Room(rect, type);
                x += bathHigh ? w : -w;
            }
        }

        /// <summary>A bathroom's door is a narrow one (<see cref="LevelMetrics.NarrowDoorWidth"/>), as in a real flat.</summary>
        static bool NarrowDoor(HousePlan plan, (int, int) key) =>
            plan.Rooms[key.Item1].Type == RoomType.Bathroom || plan.Rooms[key.Item2].Type == RoomType.Bathroom;

        /// <summary>A flat's room (its id), or −1 for the landing, the stairs and every room of another house.</summary>
        static int FlatOf(HousePlan plan, int room) => room >= 0 && room < plan.Rooms.Count ? plan.Rooms[room].Flat : -1;

        /// <summary>
        /// What may open between two rooms of the block: inside a flat the rooms onto the hallway (the bathroom by a door), the
        /// kitchen onto the living room, never one room through another; a
        /// flat onto the landing only through its hallway's door (ruins, open arches and walked loops never); two flats, or a
        /// flat's room and the landing, never — a real block has no crawl ways between flats or into the stairwell.
        /// </summary>
        static bool FlatAllows(HousePlan plan, (int, int) key, EdgeType kind)
        {
            int fa = FlatOf(plan, key.Item1), fb = FlatOf(plan, key.Item2);
            if (fa < 0 && fb < 0) return true;
            bool crawl = kind is EdgeType.Vent or EdgeType.Squeeze or EdgeType.Hole;
            var ta = plan.Rooms[key.Item1].Type;
            var tb = plan.Rooms[key.Item2].Type;
            if (ta == RoomType.Bathroom || tb == RoomType.Bathroom)
                return fa == fb && (ta == RoomType.FlatHall || tb == RoomType.FlatHall) && kind == EdgeType.Door;
            if (fa >= 0 && fb >= 0 && fa != fb) return false;
            if (fa < 0 || fb < 0)
            {
                var flatRoom = fa >= 0 ? ta : tb;
                var other = plan.Rooms[fa >= 0 ? key.Item2 : key.Item1].Type;
                return flatRoom == RoomType.FlatHall && other == RoomType.Corridor && kind == EdgeType.Door;
            }
            if (crawl || kind is EdgeType.InnerWindow or EdgeType.Breach) return false; // nor between a flat's own rooms
            // inside a flat the rooms open onto the hallway (a door, or an arch to the kitchen or the living room), and the
            // kitchen onto the living room; never one room through another (a bedroom has its one door)
            bool hall = ta == RoomType.FlatHall || tb == RoomType.FlatHall;
            var other2 = ta == RoomType.FlatHall ? tb : ta;
            if (hall) return kind == EdgeType.Door || (kind == EdgeType.Open && other2 is RoomType.Kitchen or RoomType.Living);
            bool kitchenLiving = (ta == RoomType.Kitchen && tb == RoomType.Living) || (tb == RoomType.Kitchen && ta == RoomType.Living);
            return kitchenLiving && kind is EdgeType.Door or EdgeType.Open;
        }

        /// <summary>
        /// The random stream of a floor's doors and crawl ways: in the apartment block every floor above the ground draws the
        /// same (a typical floor: the same flats get the same doors and vents, one above the other).
        /// </summary>
        static int OpeningsFloor(Ctx c, int f) => c.Apartments != null && f > 0 ? 1 : f;
    }
}
