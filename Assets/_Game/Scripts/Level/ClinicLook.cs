using System.Collections.Generic;
using Stealth.Configs;

namespace Stealth.Level
{
    /// <summary>
    /// The hospital's architecture over any style (pure): plain door frames and skirting, no arches, pilasters, panels or
    /// cornices. Every room has washable paint above a wall-protection band to 1 m with a bumper rail along its top (what
    /// beds and trolleys hit), acoustic ceiling tiles and sheet vinyl underfoot; the patient rooms in a warmer paint; the
    /// theatres and imaging rooms in white panels over a seamless green-grey floor.
    /// </summary>
    public static class ClinicLook
    {
        /// <summary>The geometry of the style dressed for this house (the style's own when the house is not a hospital).</summary>
        public static StyleGeometry For(StyleGeometry g, StyleKit kit, LevelLayout l)
        {
            if (g == null || kit == null || l == null || l.Kind != HouseKind.Hospital || kit.ClinicWall == null) return g;
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
                if (t is RoomType.Utility or RoomType.Hall) continue; // the plant room and the stairs keep the style's
                bool theatre = t is RoomType.Operating or RoomType.Imaging;
                looks.Add(theatre
                    ? new RoomLook { Room = t, Wall = kit.TheatreWall, WallTile = 2f, Plain = true, Ceiling = kit.ClinicCeiling, CeilingTile = 1.2f }
                    : new RoomLook
                    {
                        Room = t, Wall = t == RoomType.Ward ? kit.ClinicWardWall : kit.ClinicWall, WallTile = 2f,
                        Lower = kit.ClinicGuard, LowerTile = 2f, LowerHeight = 1f, Plain = true, Ceiling = kit.ClinicCeiling, CeilingTile = 1.2f,
                    });
                floors.Add(new RoomFloor { Room = t, Material = theatre ? kit.TheatreFloor : kit.ClinicFloor, Tile = 2f });
            }
            s.RoomLooks = looks.ToArray();
            s.RoomFloors = floors.ToArray();
            return s;
        }
    }
}
