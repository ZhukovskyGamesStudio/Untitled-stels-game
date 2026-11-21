using System.Collections.Generic;
using Stealth.Configs;

namespace Stealth.Level
{
    /// <summary>
    /// The abandoned research station's architecture over any style (pure): what makes it a dead station and not an office
    /// with the lights out. Steel wall panels and floor plates, vented ceiling panels, rusted trims and pipes, cable trays
    /// in the open, no light slits (the power is out); rust, oil, scorch marks and rubble on the floors, rust, leaks, mould
    /// and peeling paint on the walls, old posters. The wet rooms keep a tiled band, the quarters a scuffed vinyl floor.
    /// </summary>
    public static class DerelictLook
    {
        /// <summary>The station's decals: floor chunks per square metre (walls per metre × 0.6), and a poster's chance on 4 m of wall.</summary>
        public const float DecayDensity = 0.14f, PosterChance = 0.25f;

        /// <summary>The geometry of the style dressed for this house (the style's own when the house is not the station).</summary>
        public static StyleGeometry For(StyleGeometry g, StyleKit kit, LevelLayout l)
        {
            if (g == null || kit == null || l == null || l.PlanKind != HouseKind.Abandoned || kit.DerelictWall == null) return g;
            var s = LocationLook.Plain(g, pipes: true, CeilingKind.Trays, baseboard: true);
            s.Wall = kit.DerelictWall; s.WallTile = 2.4f;
            s.Floor = kit.DerelictFloor; s.FloorTile = 2f;
            if (kit.DerelictCeiling != null) { s.CeilingFinish = kit.DerelictCeiling; s.CeilingTile = 2.4f; }
            if (kit.DerelictRust != null) { s.Trim = kit.DerelictRust; s.Pipe = kit.DerelictRust; }
            if (kit.DerelictPaint != null) s.Beam = kit.DerelictPaint;
            s.FloorDecay = kit.FloorDecay ?? new UnityEngine.Material[0];
            s.WallDecay = kit.WallDecay ?? new UnityEngine.Material[0];
            s.Posters = kit.Posters ?? new UnityEngine.Material[0];
            s.DecayDensity = DecayDensity;
            s.PosterChance = PosterChance;
            // the rooms: the station's own, not the style's themed rooms
            var looks = new List<RoomLook>();
            var floors = new List<RoomFloor>();
            foreach (RoomType t in System.Enum.GetValues(typeof(RoomType)))
            {
                bool wet = t is RoomType.Restroom or RoomType.LockerRoom or RoomType.Kitchen;
                bool quarters = t is RoomType.Bedroom or RoomType.Lounge or RoomType.Library or RoomType.Office;
                if (wet && kit.DerelictTiles != null)
                    looks.Add(new RoomLook { Room = t, Wall = kit.DerelictWall, WallTile = 2.4f, Lower = kit.DerelictTiles, LowerTile = 1f, LowerHeight = 1.4f, Plain = true });
                if ((quarters || wet || t is RoomType.Infirmary or RoomType.Lab) && kit.DerelictVinyl != null)
                    floors.Add(new RoomFloor { Room = t, Material = kit.DerelictVinyl, Tile = 2f });
            }
            s.RoomLooks = looks.ToArray();
            s.RoomFloors = floors.ToArray();
            return s;
        }
    }
}
