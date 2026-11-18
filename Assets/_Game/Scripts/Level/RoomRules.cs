using System;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// What a room type allows (<see cref="BuildingSettings.RoomTypeRules"/>): its size (the room program draws a typed room
    /// between the smallest and the biggest size, and a typed room is never smaller than the smallest), its shape, whether
    /// it needs an outside wall (windows), and what loot lies in it. A type without a rule has no limits. Its furniture is
    /// the furnisher's (<see cref="Furnisher"/>), its robots the security's (a storage room brings the workers,
    /// <see cref="StorageRooms"/>).
    /// </summary>
    [Serializable]
    public struct RoomRule
    {
        public RoomType Room;
        [Tooltip("Floor area, m²: smallest (a room of this type is never smaller) and biggest the room program draws (0 = 2.5 × the smallest).")]
        public Vector2 Area;
        [Tooltip("Longest side over the shortest at most (0 = any).")]
        public float MaxAspect;
        [Tooltip("Metres of outside wall it needs (its windows); 0 = may lie inside the building.")]
        public float OutsideWall;
        [Tooltip("Only a plain rectangle (no L or U: its furniture needs whole walls and a free middle).")]
        public bool RectOnly;
        [Tooltip("Biggest loot piece that lies in it, cells (0 = any): a kitchen holds small things.")]
        public int MaxLootCells;
        [Tooltip("Richer loot here: added to the colour roll's value bonus.")]
        public int LootBonus;
        [Tooltip("Where it belongs: on the facade (work rooms, by the windows), inside (service rooms), or anywhere (Zoning).")]
        public RoomZone Zone;
        [Tooltip("How private: +1 deep inside the house (servers, control room, labs), −1 near the lobby (meeting rooms, " +
                 "kitchen, lounge), 0 anywhere (RoomDepth, Zoning).")]
        public int Privacy;
        [Tooltip("At most this many on a floor (0 = no limit): one kitchen a floor.")]
        public int PerFloor;
        [Tooltip("At most this many in a house (0 = no limit): one gym a house. A house kind that favours the type lifts both limits.")]
        public int PerHouse;

        public RoomRule(RoomType room, Vector2 area, float maxAspect = 0f, float outsideWall = 0f, int maxLootCells = 0, int lootBonus = 0,
            bool rectOnly = false, RoomZone zone = RoomZone.Any, int perFloor = 0, int perHouse = 0, int privacy = 0)
        {
            Privacy = privacy;
            Zone = zone;
            PerFloor = perFloor;
            PerHouse = perHouse;
            RectOnly = rectOnly;
            Room = room;
            Area = area;
            MaxAspect = maxAspect;
            OutsideWall = outsideWall;
            MaxLootCells = maxLootCells;
            LootBonus = lootBonus;
        }
    }

    /// <summary>
    /// How good it is for two room types to be next door (<see cref="BuildingSettings.RoomAdjacency"/>, <see cref="Zoning"/>):
    /// positive — they belong together (the kitchen by the open space, lockers by the gym), negative — they should not touch
    /// (a nap room by the game room, toilets by the kitchen).
    /// </summary>
    [Serializable]
    public struct RoomAdjacency
    {
        public RoomType A, B;
        [Tooltip("Positive: next door is good; negative: keep apart.")]
        public float Score;

        public RoomAdjacency(RoomType a, RoomType b, float score) { A = a; B = b; Score = score; }
    }

    /// <summary>The rules of the room types (pure).</summary>
    public static class RoomRules
    {
        /// <summary>Which types belong next door to which (as in real offices), and which should be kept apart.</summary>
        public static RoomAdjacency[] AdjacencyDefaults() => new[]
        {
            new RoomAdjacency(RoomType.Kitchen, RoomType.OpenSpace, 1.5f),   // the tea point by the desks
            new RoomAdjacency(RoomType.Kitchen, RoomType.Lounge, 1.2f),
            new RoomAdjacency(RoomType.Kitchen, RoomType.Meeting, 0.5f),     // catering
            new RoomAdjacency(RoomType.Kitchen, RoomType.Restroom, -1.5f),   // hygiene
            new RoomAdjacency(RoomType.Meeting, RoomType.OpenSpace, 0.5f),
            new RoomAdjacency(RoomType.CopyRoom, RoomType.OpenSpace, 1f),
            new RoomAdjacency(RoomType.CopyRoom, RoomType.Office, 0.6f),
            new RoomAdjacency(RoomType.Server, RoomType.ControlRoom, 1.5f),  // the racks and the screens that watch them
            new RoomAdjacency(RoomType.Server, RoomType.Server, 0.6f),       // a server block
            new RoomAdjacency(RoomType.Server, RoomType.Lab, 0.5f),
            new RoomAdjacency(RoomType.LockerRoom, RoomType.Gym, 2f),        // change, then train
            new RoomAdjacency(RoomType.LockerRoom, RoomType.Restroom, 0.8f),
            new RoomAdjacency(RoomType.Infirmary, RoomType.Restroom, 0.5f),
            new RoomAdjacency(RoomType.Lounge, RoomType.GameRoom, 0.8f),
            new RoomAdjacency(RoomType.NapRoom, RoomType.GameRoom, -1.5f),   // noise next to sleep
            new RoomAdjacency(RoomType.NapRoom, RoomType.Gym, -1f),
            new RoomAdjacency(RoomType.NapRoom, RoomType.Studio, -1f),
            new RoomAdjacency(RoomType.Library, RoomType.GameRoom, -1f),     // noise next to quiet
            new RoomAdjacency(RoomType.Library, RoomType.Studio, -0.8f),
            new RoomAdjacency(RoomType.Library, RoomType.Gym, -0.8f),
        };

        /// <summary>The table as a lookup (both orders).</summary>
        public static System.Collections.Generic.Dictionary<(RoomType, RoomType), float> AdjacencyMap(RoomAdjacency[] table)
        {
            var map = new System.Collections.Generic.Dictionary<(RoomType, RoomType), float>();
            if (table == null) return map;
            foreach (var a in table)
            {
                map[(a.A, a.B)] = a.Score;
                map[(a.B, a.A)] = a.Score;
            }
            return map;
        }

        /// <summary>The types a house is made of (not the lobby, the stair halls and the corridors: the plan adds those).</summary>
        public static readonly RoomType[] Programmed =
        {
            RoomType.OpenSpace, RoomType.Office, RoomType.Meeting, RoomType.Kitchen, RoomType.Server, RoomType.Storage,
            RoomType.Restroom, RoomType.Lounge, RoomType.Gym, RoomType.Library, RoomType.CopyRoom, RoomType.ControlRoom,
            RoomType.Lab, RoomType.NapRoom, RoomType.Studio, RoomType.LockerRoom, RoomType.Infirmary, RoomType.GameRoom,
        };

        public static RoomRule[] Defaults() => new[]
        {
            // zones: work rooms on the facade, service rooms inside; caps: one kitchen and one toilet block a floor, one of
            // each special room a house (real offices; a house kind that favours a type lifts its cap, HouseKinds)
            new RoomRule(RoomType.Office, new Vector2(6f, 30f), zone: RoomZone.Facade), // the catch-all: any room big enough to be one
            new RoomRule(RoomType.OpenSpace, new Vector2(40f, 140f), zone: RoomZone.Facade),
            new RoomRule(RoomType.Meeting, new Vector2(14f, 50f), zone: RoomZone.Facade, privacy: -1),
            // a kitchen: a mid-sized room with its run, one fridge and a table with chairs; small things (big enough for the
            // run along a wall, the table in the middle and the doors' walkways). Windows and a plain rectangle are not needed
            new RoomRule(RoomType.Kitchen, new Vector2(24f, 64f), maxAspect: 2.2f, maxLootCells: 4, perFloor: 1, privacy: -1),
            new RoomRule(RoomType.Server, new Vector2(12f, 60f), zone: RoomZone.Interior, privacy: 1),
            // the storage room: a small or mid-sized room behind one locked door, shelves and a pile; anything, richer
            new RoomRule(RoomType.Storage, new Vector2(9f, 40f), maxAspect: 3.2f, lootBonus: 2, zone: RoomZone.Interior),
            new RoomRule(RoomType.Restroom, new Vector2(8f, 26f), zone: RoomZone.Interior, perFloor: 1),
            new RoomRule(RoomType.Lounge, new Vector2(28f, 90f), zone: RoomZone.Facade, perFloor: 1, privacy: -1),
            new RoomRule(RoomType.Gym, new Vector2(22f, 80f), perHouse: 1, privacy: -1),
            new RoomRule(RoomType.Library, new Vector2(22f, 70f), zone: RoomZone.Facade, perHouse: 1),
            new RoomRule(RoomType.CopyRoom, new Vector2(9f, 26f), zone: RoomZone.Interior, perFloor: 1),
            new RoomRule(RoomType.ControlRoom, new Vector2(24f, 60f), zone: RoomZone.Interior, perHouse: 1, privacy: 1),
            new RoomRule(RoomType.Lab, new Vector2(16f, 70f), privacy: 1),
            new RoomRule(RoomType.NapRoom, new Vector2(14f, 36f), zone: RoomZone.Interior, perHouse: 1),
            new RoomRule(RoomType.Studio, new Vector2(16f, 40f), zone: RoomZone.Interior, perHouse: 1, privacy: 1),
            new RoomRule(RoomType.LockerRoom, new Vector2(16f, 50f), zone: RoomZone.Interior, perHouse: 1),
            new RoomRule(RoomType.Infirmary, new Vector2(18f, 50f), perHouse: 1),
            new RoomRule(RoomType.GameRoom, new Vector2(26f, 100f), perHouse: 1, privacy: -1),
            // the auto repair shop's own rooms: never drawn by the program (no weight), laid out by PlanGenerator.Garage
            new RoomRule(RoomType.Workshop, new Vector2(80f, 700f), perHouse: 1),
            new RoomRule(RoomType.PaintBooth, new Vector2(20f, 80f), zone: RoomZone.Interior, perHouse: 1),
            new RoomRule(RoomType.Utility, new Vector2(6f, 24f), perHouse: 1),
            new RoomRule(RoomType.Ward, new Vector2(12f, 40f)),
            new RoomRule(RoomType.NurseStation, new Vector2(10f, 40f)),
            new RoomRule(RoomType.Operating, new Vector2(24f, 70f)),
            new RoomRule(RoomType.Imaging, new Vector2(18f, 60f)),
            // the apartment block's flats (PlanGenerator.Apartments)
            new RoomRule(RoomType.FlatHall, new Vector2(6f, 40f), maxLootCells: 4),
            new RoomRule(RoomType.Living, new Vector2(12f, 60f)),
            new RoomRule(RoomType.Bedroom, new Vector2(10f, 30f), privacy: 1),
            new RoomRule(RoomType.Bathroom, new Vector2(4f, 12f), maxLootCells: 2, privacy: 1),
        };

        /// <summary>The rule of a type, or one without limits.</summary>
        public static RoomRule For(RoomType type, RoomRule[] rules)
        {
            if (rules != null) foreach (var r in rules) if (r.Room == type) return r;
            return new RoomRule(type, Vector2.zero);
        }

        /// <summary>The smallest a room of this type may be, m².</summary>
        public static float MinArea(RoomType type, RoomRule[] rules) => Mathf.Max(0f, For(type, rules).Area.x);

        /// <summary>The biggest the room program draws for this type, m².</summary>
        public static float MaxArea(RoomType type, RoomRule[] rules)
        {
            var r = For(type, rules);
            float min = Mathf.Max(6f, r.Area.x);
            return r.Area.y > 0f ? Mathf.Max(min, r.Area.y) : min * 2.5f;
        }

        /// <summary>
        /// A room of this area, proportions and outside wall (metres) may be of the rule's type. <paramref name="upTo"/>: the
        /// biggest size counts too (choosing a type for a room); a laid-out room only has to reach the smallest.
        /// </summary>
        public static bool Fits(in RoomRule r, float area, float aspect, float outsideWall, bool rect = true, bool upTo = true)
        {
            if (r.RectOnly && !rect) return false;
            if (area < r.Area.x || (upTo && r.Area.y > 0f && area > r.Area.y)) return false;
            if (r.MaxAspect > 0f && aspect > r.MaxAspect) return false;
            return outsideWall >= r.OutsideWall;
        }

        internal static float AspectOf(PlanRoom r) => Mathf.Max(r.Bounds.width, r.Bounds.height) / Mathf.Max(0.5f, Mathf.Min(r.Bounds.width, r.Bounds.height));

        public static float OutsideOf(HousePlan plan, PlanRoom r)
        {
            float outside = 0f;
            foreach (var w in plan.Walls)
                if (w.Exterior && (w.RoomA == r.Id || w.RoomB == r.Id)) outside += w.Length;
            return outside;
        }

        internal static bool Holds(HousePlan plan, PlanRoom r, RoomType type, RoomRule[] rules) =>
            Fits(For(type, rules), r.Area, AspectOf(r), OutsideOf(plan, r), r.Shape == RoomShape.Rect && r.Shaped.Count == 0, upTo: false);

        /// <summary>
        /// The finished plan, before the furniture: every room holds its type's rule (never smaller than its smallest size;
        /// a kitchen a rectangle at the facade). A room the layout or the shaping left out of its rule swaps types with a room
        /// where both fit; failing that it becomes the first ordinary type that fits it (an office, an open space…).
        /// </summary>
        public static void Enforce(HousePlan plan, RoomRule[] rules)
        {
            foreach (var r in plan.Rooms)
            {
                if (r.Kept || RoomTraits.Circulation(r.Type) || Planned(r.Type) || Holds(plan, r, r.Type, rules)) continue;
                bool swapped = false;
                if (r.Type != RoomType.Storage) // the storage room's place is its own (one door: StorageRooms)
                    foreach (var o in plan.Rooms)
                    {
                        if (o == r || o.Kept || o.Type is RoomType.Corridor or RoomType.Lobby or RoomType.Hall or RoomType.Storage || Planned(o.Type) || o.Type == r.Type) continue;
                        if (!Holds(plan, o, r.Type, rules) || !Holds(plan, r, o.Type, rules)) continue;
                        (o.Type, r.Type) = (r.Type, o.Type);
                        swapped = true;
                        break;
                    }
                if (swapped) continue;
                r.Type = RoomType.Office;
                foreach (var t in Fallbacks)
                    if (Holds(plan, r, t, rules)) { r.Type = t; break; }
            }
        }

        /// <summary>
        /// Types a location's own plan lays out where they belong (the shop's hall and booth, the plant room, the hospital's
        /// wards, station, theatres and imaging rooms): neither rule enforcement nor zoning moves or retypes them.
        /// </summary>
        public static bool Planned(RoomType t) => RoomTraits.Planned(t);

        /// <summary>What a room that fits nothing else becomes, in order (an office takes almost any room).</summary>
        static readonly RoomType[] Fallbacks = { RoomType.OpenSpace, RoomType.Office, RoomType.Lounge, RoomType.Meeting, RoomType.CopyRoom, RoomType.Restroom };

        /// <summary>The type has reached its cap with these counts (house, this floor).</summary>
        public static bool Capped(in RoomRule r, int inHouse, int onFloor) =>
            (r.PerHouse > 0 && inHouse >= r.PerHouse) || (r.PerFloor > 0 && onFloor >= r.PerFloor);

        /// <summary>A loot shape is allowed in the room (small enough for it).</summary>
        public static bool LootFits(in RoomRule r, int cells) => r.MaxLootCells <= 0 || cells <= r.MaxLootCells;
    }
}
