namespace Stealth.Level
{
    /// <summary>
    /// The salts of the seeded streams that are not a <see cref="RandomChannel"/> (<c>LevelRandom.Mix(seed, Salt.X, index)</c>):
    /// one name, one value, one purpose — two streams with the same salt and index would draw the same numbers
    /// (<c>LevelRandomTests.SaltsAreDistinct</c>). A new stream takes a new salt here.
    /// </summary>
    public static class Salt
    {
        // the house and its plan
        public const int HouseKind = 0x4B1D, HouseKindSeed = 0x5EED, HouseTitle = 0x4E41, HouseSize = 0x512E, Program = 0x9206, PlanAttempt = 0xBAD;
        public const int Furniture = 0xF0E, StorageRoom = 0x5702, RaisedVents = 0x4856, Terminals = 0x7E41, Locks = 0x10C4;
        public const int ChargingRoom = 0xC4A6, Wards = 0x3A2D, Vents = 0x7E47, VentLids = 0x6A7E, VentClimbs = 0xC1B;
        public const int Utilities = 0x51E7, City = 0xC17E, CityDetail = 0xC1D7, Decor = 0x0DECA7, StreetDoors = 0xD00E;
        public const int Railings = 0x57A1, StairLooks = 0x57A3, PropVariants = 0x7A21, SpiderWebs = 0x5E8, Screens = 0x5C4E, Pictures = 0x91C7, Ruins = 0x2B1C, RoomLooks = 0x100C, WallThings = 0x7B19;
        // the special places and the main job
        public const int ShelterMap = 0x3A9E, HideRoom = 0x41DE, KeyDoors = 0x6E7D, Crates = 0xC7A7, CrateContents = 0xC7A8;
        public const int PointOfInterest = 0x9017, Reinforcements = 0x4E1F;
        public const int Objective = 0x0B7E, ObjectiveFinish = 0x0B81, VaultTreasure = 0x0B82;
        public const int SafeCode = 0x5AFE, VaultConsoleCode = 0x0C05;
        // the sewer
        public const int Sewer = 0x5E3E, SewerHall = 0x5E4A, CarUnload = 0xCA60, CarLoad = 0xCA61, RouteFuel = 0xF0E1, RouteLate = 0x1A7E;
        // crafting: the run's recipes, what breaks at a failed combine, the blueprints and chess pieces of a house, what an unscrewed device drops
        public const int Recipes = 0xC4AF, CraftBreak = 0xC4B0, Blueprints = 0xB1E9, ChessPieces = 0xC4E5, Unscrewed = 0x5C3E;
        // the car upgrade bench: the run's recipe of every upgrade and level
        public const int CarUpgrades = 0xCA67;
        public const int Cassettes = 0xCA55;
    }
}
