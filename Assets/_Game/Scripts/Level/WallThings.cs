using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// What hangs on a wall for the look of it (the style kit's <see cref="Stealth.Configs.StyleKit.Things"/>, in this order;
    /// models — Tools/Blender/wall_things.py, the same list in wall_things_atlas.py). Decor: no colliders, nothing to use.
    /// </summary>
    public enum WallThing
    {
        LandscapeA, LandscapeB, AbstractA, AbstractB, PhotoA, PhotoB, Certificate, Calendar,
        EvacPlan, FirstAid, TinSign, Health, Family, Botanical, NoSmoking, Thermostat, VentGrille,
    }

    /// <summary>
    /// Which of them a room hangs up, by whose house it is and what the room is for: prints and certificates in offices,
    /// family photographs in flats, enamel signs in a repair shop, hygiene posters in a hospital, plans and signs in
    /// corridors and plant rooms; nothing where the walls are tiled or the room is a vault. Pure.
    /// </summary>
    public static class WallThings
    {
        public const int Count = (int)WallThing.VentGrille + 1;

        /// <summary>Width and height on the wall, metres (wall_things_atlas.THINGS).</summary>
        public static Vector2 Size(WallThing t) => t switch
        {
            WallThing.LandscapeA => new(0.70f, 0.50f), WallThing.LandscapeB => new(0.90f, 0.60f), WallThing.AbstractA => new(0.50f, 0.70f),
            WallThing.AbstractB => new(0.60f, 0.60f), WallThing.PhotoA => new(0.40f, 0.30f), WallThing.PhotoB => new(0.30f, 0.40f),
            WallThing.Certificate => new(0.30f, 0.42f), WallThing.Calendar => new(0.30f, 0.45f), WallThing.EvacPlan => new(0.42f, 0.30f),
            WallThing.FirstAid => new(0.30f, 0.30f), WallThing.TinSign => new(0.45f, 0.30f), WallThing.Health => new(0.42f, 0.60f),
            WallThing.Family => new(0.24f, 0.30f), WallThing.Botanical => new(0.30f, 0.42f), WallThing.NoSmoking => new(0.20f, 0.20f),
            WallThing.Thermostat => new(0.085f, 0.125f), _ => new(0.40f, 0.20f),
        };

        /// <summary>The height of its middle above the floor: pictures at eye height, signs a little higher, the grille by the floor.</summary>
        public static float Height(WallThing t) => t switch
        {
            WallThing.VentGrille => 0.42f,
            WallThing.Thermostat => 1.45f,
            WallThing.FirstAid or WallThing.NoSmoking or WallThing.TinSign => 1.75f,
            _ => 1.55f,
        };

        static readonly WallThing[] None = new WallThing[0];
        static readonly WallThing[] Desk = { WallThing.LandscapeA, WallThing.LandscapeB, WallThing.AbstractA, WallThing.AbstractB, WallThing.PhotoA, WallThing.PhotoB,
                                             WallThing.Certificate, WallThing.Certificate, WallThing.Calendar, WallThing.Thermostat, WallThing.Botanical };
        static readonly WallThing[] Passage = { WallThing.AbstractA, WallThing.AbstractB, WallThing.LandscapeB, WallThing.PhotoB, WallThing.EvacPlan, WallThing.EvacPlan,
                                                WallThing.NoSmoking, WallThing.FirstAid, WallThing.VentGrille, WallThing.VentGrille };
        static readonly WallThing[] Rest = { WallThing.PhotoA, WallThing.PhotoB, WallThing.Botanical, WallThing.Calendar, WallThing.LandscapeA, WallThing.AbstractB };
        static readonly WallThing[] Plant = { WallThing.EvacPlan, WallThing.NoSmoking, WallThing.FirstAid, WallThing.Thermostat, WallThing.VentGrille };
        static readonly WallThing[] Home = { WallThing.Family, WallThing.Family, WallThing.PhotoA, WallThing.PhotoB, WallThing.LandscapeA, WallThing.Botanical, WallThing.AbstractA };
        static readonly WallThing[] HomeKitchen = { WallThing.Calendar, WallThing.Botanical, WallThing.Family };
        static readonly WallThing[] Shop = { WallThing.TinSign, WallThing.TinSign, WallThing.Calendar, WallThing.FirstAid, WallThing.NoSmoking, WallThing.EvacPlan };
        static readonly WallThing[] Clinic = { WallThing.Health, WallThing.Health, WallThing.FirstAid, WallThing.EvacPlan, WallThing.NoSmoking, WallThing.Thermostat };
        static readonly WallThing[] Bedside = { WallThing.Botanical, WallThing.LandscapeA, WallThing.Health };

        /// <summary>What a room of this kind hangs up in a house of this kind (repeats weigh the choice); empty — bare walls.</summary>
        public static WallThing[] For(HouseKind house, RoomType room)
        {
            // tiled, padded or steel walls, and rooms nobody decorates
            if (room is RoomType.Restroom or RoomType.Bathroom or RoomType.LockerRoom or RoomType.Gym or RoomType.PaintBooth or RoomType.Operating
                or RoomType.ChargingStation or RoomType.Imaging) return None;
            if (room is RoomType.Server or RoomType.Storage or RoomType.Utility or RoomType.ControlRoom or RoomType.Lab) return Plant;
            switch (house)
            {
                case HouseKind.Apartments:
                    return room is RoomType.Corridor or RoomType.Lobby or RoomType.Hall ? None : room == RoomType.Kitchen ? HomeKitchen : Home;
                case HouseKind.Garage:
                    return room is RoomType.Lobby or RoomType.Office ? Desk : Shop;
                case HouseKind.Hospital:
                    return room == RoomType.Ward ? Bedside : room is RoomType.Office or RoomType.Lounge ? Rest : Clinic;
                case HouseKind.Abandoned:
                    return room is RoomType.Bedroom or RoomType.Living ? Rest : Plant;
            }
            if (room is RoomType.Corridor or RoomType.Hall or RoomType.Lobby) return Passage;
            if (room is RoomType.Kitchen or RoomType.Lounge or RoomType.NapRoom or RoomType.GameRoom or RoomType.Studio) return Rest;
            return Desk;
        }
    }
}
