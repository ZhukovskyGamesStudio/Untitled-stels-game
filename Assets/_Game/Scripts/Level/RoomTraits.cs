namespace Stealth.Level
{
    /// <summary>
    /// What a room's type says about it, in one place (pure): the plan, the rules, the furniture, the hatches and the
    /// ducts ask here instead of each keeping its own list of types.
    /// </summary>
    public static class RoomTraits
    {
        /// <summary>A way through, not a room: corridors, stair halls, the lobby.</summary>
        public static bool Circulation(RoomType t) => t is RoomType.Corridor or RoomType.Hall or RoomType.Lobby;

        /// <summary>
        /// Laid out by a location's own plan where it belongs (the shop's hall and booth, the plant room, the hospital's
        /// wards, station, theatres and imaging rooms, a flat's rooms): neither rule enforcement nor zoning moves or retypes it.
        /// </summary>
        public static bool Planned(RoomType t) => t is RoomType.Workshop or RoomType.PaintBooth or RoomType.Utility
            or RoomType.Ward or RoomType.NurseStation or RoomType.Operating or RoomType.Imaging
            or RoomType.FlatHall or RoomType.Living or RoomType.Bedroom or RoomType.Bathroom;

        /// <summary>
        /// A room with a single door, a dead end: the storage room (it is locked), the shop's paint booth (sealed), the
        /// plant room, the hospital's rooms (one way in, off the corridor).
        /// </summary>
        public static bool OneDoor(RoomType t) => t is RoomType.Storage or RoomType.PaintBooth or RoomType.Utility
            or RoomType.Ward or RoomType.NurseStation or RoomType.Operating or RoomType.Imaging;

        /// <summary>The hospital's rooms whose own pieces need their walls first (a ward's beds, a theatre's table, a scanner, the station's counter).</summary>
        public static bool Equipped(RoomType t) => t is RoomType.Ward or RoomType.Operating or RoomType.Imaging or RoomType.NurseStation;

        /// <summary>
        /// The middle of the room is furniture (the meeting table, the toilet's cubicles, the booth's car, the station's
        /// counter, the theatre's table, a scanner): its patrol point is at the door.
        /// </summary>
        public static bool MiddleIsFurniture(RoomType t) => t is RoomType.Meeting or RoomType.Restroom or RoomType.PaintBooth
            or RoomType.NurseStation or RoomType.Operating or RoomType.Imaging or RoomType.Bedroom or RoomType.Bathroom;

        /// <summary>A room with drains (water pipes, a riser): the exit hatch's favourite, the utilities' risers.</summary>
        public static bool Wet(RoomType t) => t is RoomType.Restroom or RoomType.Kitchen or RoomType.LockerRoom or RoomType.Bathroom;

        /// <summary>
        /// No hatch, no vault and no cage in it: its floor is taken (a corridor, the storage room, the plant room, the
        /// booth's car, the hospital's equipment).
        /// </summary>
        public static bool FloorTaken(RoomType t) => t is RoomType.Corridor or RoomType.Storage or RoomType.PaintBooth or RoomType.Utility
            or RoomType.Operating or RoomType.Imaging or RoomType.NurseStation;
    }
}
