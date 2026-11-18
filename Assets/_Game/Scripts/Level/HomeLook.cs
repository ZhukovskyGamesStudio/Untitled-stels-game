using System.Collections.Generic;
using Stealth.Configs;

namespace Stealth.Level
{
    /// <summary>
    /// The apartment block's architecture over any style (pure): plain door frames and skirting, no arches, panels,
    /// cornices, trays or pipes. The flats are papered — warm patterned paper in the living rooms, a softer one in the
    /// bedrooms, a darker one in the hallways — over parquet, under whitewashed ceilings; the kitchens are painted above a
    /// tiled band, the bathrooms tiled floor to ceiling. The stairwell, the landings and the entrance hall have the
    /// block's classic oil-painted band to 1.5 m under whitewash and a small-tile floor.
    /// </summary>
    public static class HomeLook
    {
        /// <summary>The geometry of the style dressed for this house (the style's own when the house is not an apartment block).</summary>
        public static StyleGeometry For(StyleGeometry g, StyleKit kit, LevelLayout l)
        {
            if (g == null || kit == null || l == null || l.Kind != HouseKind.Apartments || kit.HomeWallpaper == null) return g;
            var s = g.Clone();
            s.Doors = DoorTrim.Casing;
            s.Baseboard = true;
            s.Crown = false;
            s.WainscotHeight = 0f;
            s.RailHeight = 0f;
            s.Pipes = false;
            s.Ceiling = CeilingKind.None;
            s.StripSpacing = 0f;
            var looks = new List<RoomLook>();
            var floors = new List<RoomFloor>();
            foreach (RoomType t in System.Enum.GetValues(typeof(RoomType)))
            {
                if (t == RoomType.Utility) continue; // the plant room keeps the style's
                switch (t)
                {
                    case RoomType.Living:
                    case RoomType.Bedroom:
                    case RoomType.FlatHall:
                        looks.Add(new RoomLook
                        {
                            Room = t, Wall = t == RoomType.Living ? kit.HomeWallpaper : t == RoomType.Bedroom ? kit.HomeWallpaperBed : kit.HomeWallpaperHall,
                            WallTile = 1.2f, Plain = true, Ceiling = kit.HomeCeiling, CeilingTile = 2f,
                        });
                        floors.Add(new RoomFloor { Room = t, Material = kit.HomeParquet, Tile = 1.5f });
                        break;
                    case RoomType.Kitchen:
                        looks.Add(new RoomLook
                        {
                            Room = t, Wall = kit.HomeKitchenWall, WallTile = 2f, Lower = kit.HomeKitchenTile, LowerTile = 1.2f, LowerHeight = 1.5f,
                            Plain = true, Ceiling = kit.HomeCeiling, CeilingTile = 2f,
                        });
                        floors.Add(new RoomFloor { Room = t, Material = kit.HomeKitchenFloor, Tile = 1.2f });
                        break;
                    case RoomType.Bathroom:
                        looks.Add(new RoomLook { Room = t, Wall = kit.HomeBathTile, WallTile = 1.2f, Plain = true, Ceiling = kit.HomeCeiling, CeilingTile = 2f });
                        floors.Add(new RoomFloor { Room = t, Material = kit.HomeBathFloor, Tile = 0.8f });
                        break;
                    default:
                        // the stairwell, the landings, the entrance hall and the rooms of the ground floor
                        looks.Add(new RoomLook
                        {
                            Room = t, Wall = kit.HomeStairWall, WallTile = 2f, Lower = kit.HomeStairLower, LowerTile = 2f, LowerHeight = 1.5f,
                            Plain = true, Ceiling = kit.HomeCeiling, CeilingTile = 2f,
                        });
                        floors.Add(new RoomFloor { Room = t, Material = kit.HomeStairFloor, Tile = 1.2f });
                        break;
                }
            }
            s.RoomLooks = looks.ToArray();
            s.RoomFloors = floors.ToArray();
            return s;
        }
    }
}
