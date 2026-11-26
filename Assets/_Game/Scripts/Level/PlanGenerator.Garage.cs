using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The auto repair shop's plan (<see cref="BuildingSettings.Garage"/>), laid out the way real service stations are:
    /// the workshop hall two storeys high along the street with a bay for every sectional door, the customer block beside
    /// it (the reception at the facade, the stair, the service office and the toilets behind), a strip of back rooms behind
    /// the hall (the paint booth, the parts store, the mechanics' locker room) and upstairs a gallery over the hall that
    /// leads to the rooms above the block and the back rooms (the break room, the manager, a store).
    /// </summary>
    public static partial class PlanGenerator
    {
        /// <summary>Where the shop's parts are, in cells.</summary>
        sealed class GarageLayout
        {
            public RectInt Hall, Back, Block;
            public bool BlockLeft;
            public int Bays;
        }

        /// <summary>One service bay along the street, metres (a car, the lines round it and room to open its doors).</summary>
        public const int BayWidth = 4;
        /// <summary>The workshop hall's depth from the street, metres: a car in its bay, the walkway behind, the benches.</summary>
        const int HallDepthMin = 14;

        /// <summary>
        /// The shop's mass (metres): the hall with the back rooms behind it as one wing, the two-storey block beside it.
        /// As many bays as the area holds; the block on either side.
        /// </summary>
        static List<RectInt> GarageMass(Ctx c, System.Random rng, float area, out string kind)
        {
            int hallD = HallDepthMin + rng.Next(2), backD = 6 + rng.Next(3), blockW = 10 + rng.Next(3);
            int bays = Mathf.Clamp(Mathf.RoundToInt((area / (hallD + backD) - blockW - 2) / BayWidth) + rng.Next(2), 4, 14);
            int hallW = bays * BayWidth + 2;
            bool left = rng.Next(2) == 0;
            int depth = hallD + backD;
            int hx = left ? blockW : 0, bx = left ? 0 : hallW;
            c.Garage = new GarageLayout
            {
                Hall = new RectInt(hx * M, 0, hallW * M, hallD * M),
                Back = new RectInt(hx * M, hallD * M, hallW * M, backD * M),
                Block = new RectInt(bx * M, 0, blockW * M, depth * M),
                BlockLeft = left,
                Bays = bays,
            };
            kind = "garage";
            return new List<RectInt> { new(hx, 0, hallW, depth), new(bx, 0, blockW, depth) };
        }

        /// <summary>The reception across the front of the block, the stair hall right behind it on the block's outer side.</summary>
        static void ReserveGarage(Ctx c)
        {
            var g = c.Garage;
            c.Lobby = new RectInt(g.Block.xMin, 0, g.Block.width, 7 * M);
            if (c.Floors < 2) return;
            int side = 7 * M;
            int x = g.BlockLeft ? g.Block.xMin : g.Block.xMax - side;
            var hall = new RectInt(x, c.Lobby.yMax, side, side);
            // the stair in the corner on the outside wall, at the back: its hole away from the reception
            c.Halls.Add((0, hall, new Vector2Int(g.BlockLeft ? -1 : 1, 1)));
        }

        /// <summary>
        /// One floor of the shop. Ground: the hall (two storeys, a bay per sectional door), the back rooms cut along the
        /// strip, the rest of the block. Upstairs: the gallery (an L along the hall's back and its side on the block), rooms
        /// behind it. Every room typed here; leftovers typed by the weights.
        /// </summary>
        static void ZoneGarage(Ctx c, int f, List<Region> regions, int[,] owner, System.Func<RectInt, Region, int> claim)
        {
            var g = c.Garage;
            int nx = c.Plan.NX, nz = c.Plan.NZ;
            var rng = c.Rng(0x6A2A, f);
            var queue = new Queue<RoomType>();
            void Room(RectInt r, RoomType t) => claim(r, new Region { Type = t, Shape = RoomShape.Rect, Typed = true });

            if (f == 0)
            {
                claim(g.Hall, new Region { Type = RoomType.Workshop, Shape = RoomShape.Rect, Fixed = true, Typed = true, Levels = Mathf.Min(2, c.Floors) });
                // the back rooms: the paint booth (8–9 m), the parts store if the house has one, the locker room, an office
                bool storage = false;
                if (c.Program != null) foreach (var p in c.Program.Floors[0]) storage |= p.Type == RoomType.Storage && p.Required;
                var types = new List<RoomType> { RoomType.PaintBooth };
                if (storage) types.Add(RoomType.Storage);
                types.Add(RoomType.LockerRoom);
                CutStrip(g.Back, types, rng, (r, t) => Room(r, t));
            }
            else
            {
                // the gallery: along the hall's back over the back rooms, and down the block's side of the hall to the stair
                int deep = 5;                                                     // 2.5 m
                int side = g.Block.width - 7 * M;                                 // between the stair hall and the hall
                var gallery = new Region { Type = RoomType.Corridor, Shape = RoomShape.Corridor, Fixed = true };
                int id = regions.Count;
                void Take(RectInt r)
                {
                    for (int x = r.xMin; x < r.xMax; x++)
                        for (int z = r.yMin; z < r.yMax; z++)
                        {
                            if (x < 0 || z < 0 || x >= nx || z >= nz || owner[x, z] != -1) continue;
                            owner[x, z] = id;
                            gallery.Cells.Add(new Vector2Int(x, z));
                        }
                    gallery.Rects.Add(r);
                }
                Take(new RectInt(g.Back.xMin, g.Back.yMin, g.Back.width, deep));
                int gx = g.BlockLeft ? g.Block.xMax - side : g.Block.xMin;
                Take(new RectInt(gx, 0, side, g.Back.yMin + deep));
                if (gallery.Cells.Count > 0) regions.Add(gallery);
                var behind = new RectInt(g.Back.xMin, g.Back.yMin + deep, g.Back.width, g.Back.height - deep);
                CutStrip(behind, new List<RoomType> { RoomType.Kitchen, RoomType.Office, RoomType.Office }, rng, (r, t) => Room(r, t));
            }
            // the rest of the block: rooms as the free rectangles come (big ones halved), the service rooms first
            queue.Clear();
            if (f == 0) { queue.Enqueue(RoomType.Office); queue.Enqueue(RoomType.Restroom); queue.Enqueue(RoomType.Office); }
            else { queue.Enqueue(RoomType.Office); queue.Enqueue(RoomType.Restroom); queue.Enqueue(RoomType.Lounge); queue.Enqueue(RoomType.Office); }
            foreach (var r in Rectangles(owner, nx, nz))
            {
                if (r.width < MinSide || r.height < MinSide) continue;
                float area = r.width * r.height / (float)(M * M);
                var parts = new List<RectInt> { r };
                if (area > 42f)
                {
                    bool alongX = r.width >= r.height;
                    int cut = (alongX ? r.width : r.height) / 2;
                    parts = alongX
                        ? new List<RectInt> { new(r.xMin, r.yMin, cut, r.height), new(r.xMin + cut, r.yMin, r.width - cut, r.height) }
                        : new List<RectInt> { new(r.xMin, r.yMin, r.width, cut), new(r.xMin, r.yMin + cut, r.width, r.height - cut) };
                }
                foreach (var p in parts) Room(p, queue.Count > 0 ? queue.Dequeue() : RoomType.Office);
            }

            Cleanup(regions, owner, nx, nz);
            for (int settle = 0; settle < 4; settle++)
            {
                Slim(regions, owner, nx, nz);
                SplitDisconnected(regions, owner, nx, nz);
                Cleanup(regions, owner, nx, nz);
            }
            AssignTypes(c, f, regions);
            Commit(c, f, regions, owner);
        }

        /// <summary>
        /// A strip cut across its length into rooms of the given types, in order (each 5–9 m along it, the paint booth the
        /// widest); what is left at the end joins the last room.
        /// </summary>
        static void CutStrip(RectInt strip, List<RoomType> types, System.Random rng, System.Action<RectInt, RoomType> room,
            System.Func<RoomType, System.Random, int> widthOf = null, bool fromEnd = false, int minRest = MinSide)
        {
            widthOf ??= (t, r) => t == RoomType.PaintBooth ? 8 + r.Next(2) : 5 + r.Next(3);
            bool alongX = strip.width >= strip.height;
            int length = alongX ? strip.width : strip.height;
            int at = 0, k = 0;
            while (at < length)
            {
                var type = k < types.Count ? types[k] : RoomType.Office;
                int w = widthOf(type, rng) * M;
                if (length - (at + w) < minRest) w = length - at; // a sliver at the end joins this room (too short for a door of its own)
                // from the start of the strip, or from its far end (fromEnd: the first rooms at the strip's high end)
                int o = fromEnd ? length - at - w : at;
                var r = alongX ? new RectInt(strip.xMin + o, strip.yMin, w, strip.height) : new RectInt(strip.xMin, strip.yMin + o, strip.width, w);
                room(r, type);
                at += w;
                k++;
            }
        }

        /// <summary>The shop's gallery looks down into the hall over a railing; every other room upstairs walls it off.</summary>
        static void GarageGalleries(Ctx c)
        {
            var plan = c.Plan;
            var keys = new List<(int, int, int)>(c.Galleries.Keys);
            foreach (var key in keys)
            {
                var (_, voidRoom, other) = key;
                c.Galleries[key] = plan.Rooms[voidRoom].Type == RoomType.Workshop && plan.Rooms[other].Type == RoomType.Corridor;
            }
        }
    }
}
