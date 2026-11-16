using System.Collections.Generic;
using Stealth.Configs;

namespace Stealth.Level
{
    /// <summary>
    /// The auto repair shop's architecture over any style (pure): what makes it a workshop and not an office with cars in
    /// it. No arches, pilasters, wall panels, chair rails or cornices — plain steel door frames; pipes and cable trays
    /// in the open. The hall, the parts store and the mechanics' rooms in painted concrete block with a dark band to
    /// 1.2 m (the part that gets knocked by trolleys and tyres), an epoxy floor and a metal deck overhead; the paint booth
    /// in white filter panels with a steel grating underfoot. The front rooms (the reception, the offices, the break
    /// room) keep the style's finishes.
    /// </summary>
    public static class ShopLook
    {
        /// <summary>The rooms dressed as the workshop (block walls, epoxy floor).</summary>
        public static readonly RoomType[] Workshop = { RoomType.Workshop, RoomType.Storage, RoomType.LockerRoom };

        /// <summary>The geometry of the style dressed for this house (the style's own when the house is not a shop).</summary>
        public static StyleGeometry For(StyleGeometry g, StyleKit kit, LevelLayout l)
        {
            if (g == null || kit == null || l == null || l.Kind != HouseKind.Garage || kit.ShopWall == null) return g;
            var s = g.Clone();
            s.Doors = DoorTrim.Casing;
            s.Crown = false;
            s.WainscotHeight = 0f;
            s.RailHeight = 0f;
            s.Pipes = true;
            s.Ceiling = CeilingKind.Trays;
            s.StripSpacing = 0f;
            var looks = new List<RoomLook>();
            var floors = new List<RoomFloor>();
            var mine = new HashSet<RoomType>(Workshop) { RoomType.PaintBooth };
            if (g.RoomLooks != null) foreach (var r in g.RoomLooks) if (!mine.Contains(r.Room)) looks.Add(r);
            if (g.RoomFloors != null) foreach (var r in g.RoomFloors) if (!mine.Contains(r.Room)) floors.Add(r);
            foreach (var t in Workshop)
            {
                looks.Add(new RoomLook
                {
                    Room = t, Wall = kit.ShopWall, WallTile = 2f, Lower = kit.ShopWallLower, LowerTile = 2f, LowerHeight = 1.2f, Plain = true,
                    Ceiling = kit.ShopCeiling, CeilingTile = 1f,
                });
                floors.Add(new RoomFloor { Room = t, Material = kit.ShopFloor, Tile = 2f });
            }
            looks.Add(new RoomLook { Room = RoomType.PaintBooth, Wall = kit.BoothWall, WallTile = 1f, Plain = true, Ceiling = kit.BoothCeiling, CeilingTile = 1f });
            floors.Add(new RoomFloor { Room = RoomType.PaintBooth, Material = kit.BoothFloor, Tile = 1f });
            s.RoomLooks = looks.ToArray();
            s.RoomFloors = floors.ToArray();
            return s;
        }
    }
}
