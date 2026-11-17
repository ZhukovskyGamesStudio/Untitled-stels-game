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
            public int NextFlat;
        }

        /// <summary>Depths across the slab, metres: the flats at the front (7.5), the landing (3), the flats and stairs at the back (7.5).</summary>
        const float FlatDepth = 7.5f;
        const int LandingDepth = 3;
        /// <summary>The stairwell's width, metres: the two flights side by side (<see cref="StairGeometry"/> lanes, 4 m).</summary>
        const int StairwellWidth = 4;
        /// <summary>A flat's hallway strip (with the bathroom) on the landing's side, cells (3.5 m: the bathroom's narrow door fits its wall); the rest of its depth is its rooms.</summary>
        const int FlatInner = 7;
        /// <summary>The kitchen's and a bedroom's width along the facade, cells (3.5 m); the living room takes the rest.</summary>
        const int FlatKitchen = 7;
        /// <summary>The bathroom's width, cells (2.5 m).</summary>
        const int FlatBath = 5;
        /// <summary>The entrance hall's width at the front, cells (5 m), in front of the stairwell.</summary>
        const int EntranceCells = 10;

        static List<RectInt> ApartmentsMass(Ctx c, System.Random rng, out string kind)
        {
            int depthCells = Mathf.RoundToInt(FlatDepth * M) * 2 + LandingDepth * M;
            // wider in later houses: the back flat from 14 m to 19 m (two rooms), the front ones 9 to 11.5 m
            int side = Mathf.Clamp(14 + c.House + rng.Next(2) * 2, 14, 19);
            int width = side + StairwellWidth;
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
            a.Stairwell = new RectInt(ApX(a, a.Side, StairwellWidth * M), a.BackBand.yMin, StairwellWidth * M, a.BackBand.height);
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
            var rng = c.Rng(0xF1A7, r.xMin * 2 + (front ? 1 : 0));
            // the landing is at the band's high z for the front flats, at its low z for the back ones
            int innerZ = front ? r.yMax - FlatInner : r.yMin, outerZ = front ? r.yMin : r.yMin + FlatInner, outerD = r.height - FlatInner;
            bool bathHigh = rng.Next(2) == 0;
            // a studio's bathroom is narrower: its hallway keeps a wall to the landing a door fits in (4 m)
            int bath = r.width < 15 ? 4 : FlatBath;
            int bathX = bathHigh ? r.xMax - bath : r.xMin;
            Room(new RectInt(bathX, innerZ, bath, FlatInner), RoomType.Bathroom);
            Room(new RectInt(bathHigh ? r.xMin : r.xMin + bath, innerZ, r.width - bath, FlatInner), RoomType.FlatHall);
            var rooms = new List<(RoomType type, int w)>();
            if (r.width < 15) rooms.Add((RoomType.Living, r.width));
            else
            {
                rooms.Add((RoomType.Kitchen, FlatKitchen));
                if (r.width >= 25) rooms.Add((RoomType.Bedroom, FlatKitchen));
                rooms.Add((RoomType.Living, r.width - FlatKitchen * rooms.Count));
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
        /// What may open between two rooms of the block: inside a flat anything, but its bathroom only onto its hallway; a
        /// flat onto the landing only through its hallway's door (ruins, open arches and walked loops never); two flats,
        /// or a flat's room and the landing, only by a crawl way (a vent, a slot, a hole low in the wall).
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
            if (fa >= 0 && fb >= 0 && fa != fb) return crawl;
            if (fa < 0 || fb < 0)
            {
                var flatRoom = fa >= 0 ? ta : tb;
                var other = plan.Rooms[fa >= 0 ? key.Item2 : key.Item1].Type;
                if (flatRoom == RoomType.FlatHall && other == RoomType.Corridor && kind == EdgeType.Door) return true;
                return crawl;
            }
            return kind is not (EdgeType.InnerWindow or EdgeType.Breach);
        }

        /// <summary>
        /// The random stream of a floor's doors and crawl ways: in the apartment block every floor above the ground draws the
        /// same (a typical floor: the same flats get the same doors and vents, one above the other).
        /// </summary>
        static int OpeningsFloor(Ctx c, int f) => c.Apartments != null && f > 0 ? 1 : f;
    }
}
