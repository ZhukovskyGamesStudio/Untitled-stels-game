namespace Stealth
{
    /// <summary>What a key of the house's replicated state (<see cref="IWorldState.IsOpened"/>) is about.</summary>
    public enum OpenKind : byte
    {
        Door = 1, Safe, Terminal, Storage, Crate, Med, Dryer, Console, Cage, Escape, HackSlot, Lights, Shaft, Vent, Crawl, Gate, Stage, Ajar, Hole,
    }

    /// <summary>The stages of a house's main job kept with the opened things (<see cref="OpenKind.Stage"/>).</summary>
    public enum JobStage
    {
        VaultFound = 1, VaultEntered, CageFound, CageKeyHeld, VaultOpen,
    }

    /// <summary>
    /// The keys of a house's state the server keeps and replicates as one set of ints (hacked doors, cracked safes, open
    /// drawers, searched crates, switched lights…): the kind in the top byte, the thing's index under it — two kinds never
    /// share a key (<c>OpenKeysTests</c>).
    /// </summary>
    public static class OpenKeys
    {
        public const int IndexBits = 24;
        public const int MaxIndex = (1 << IndexBits) - 1;
        /// <summary>Parts of one storage prop (doors, drawers) and hacks of one terminal a key can tell apart.</summary>
        public const int StorageParts = 16, TerminalUses = 16;

        public static int Of(OpenKind kind, int index) => ((int)kind << IndexBits) | (index & MaxIndex);
        public static OpenKind KindOf(int key) => (OpenKind)(key >> IndexBits);
        public static int IndexOf(int key) => key & MaxIndex;

        public static int Door(int door) => Of(OpenKind.Door, door);
        public static int Safe(int safe) => Of(OpenKind.Safe, safe);
        /// <summary>The storage door held open from inside, for now.</summary>
        public static int Escape(int door) => Of(OpenKind.Escape, door);
        /// <summary>The <paramref name="use"/>-th hack made from a hacking terminal (index in the layout's props).</summary>
        public static int Terminal(int terminal, int use) => Of(OpenKind.Terminal, terminal * TerminalUses + use);
        /// <summary>An open drawer / locker door (prop index in the layout, part of it).</summary>
        public static int Storage(int prop, int part) => Of(OpenKind.Storage, StoragePart(prop, part));
        /// <summary>
        /// A drawer left ajar (prop, part): it was looked through and found empty, then pushed back by another of its column
        /// being opened — it stays a little out, so a searched drawer is told from an untouched one at a glance.
        /// </summary>
        public static int Ajar(int prop, int part) => Of(OpenKind.Ajar, StoragePart(prop, part));
        /// <summary>A storage part as one number (a prop and its drawer or door: the index of <see cref="Stealth.Level.UseKind.StoragePart"/>).</summary>
        public static int StoragePart(int prop, int part) => prop * StorageParts + part;
        /// <summary>The prop and the part of <see cref="StoragePart"/>.</summary>
        public static (int prop, int part) StoragePartOf(int index) => (index / StorageParts, index % StorageParts);
        public static int Crate(int prop) => Of(OpenKind.Crate, prop);
        /// <summary>A hole the hammer knocked in floor <paramref name="floor"/>'s slab, its first cell (x, z) on the plan (its side: <see cref="Stealth.Items.CraftedSettings.HoleSize"/>).</summary>
        public static int Hole(int floor, int x, int z) => Of(OpenKind.Hole, (floor & 15) << 20 | (z & 1023) << 10 | (x & 1023));
        public static (int floor, int x, int z) HoleOf(int key) { int i = IndexOf(key); return (i >> 20 & 15, i & 1023, i >> 10 & 1023); }
        public static int Med(ulong client) => Of(OpenKind.Med, (int)(client % MaxIndex));
        public static int Dryer(int prop) => Of(OpenKind.Dryer, prop);
        /// <summary>A room whose lights a player switched (from how the house started).</summary>
        public static int Lights(int room) => Of(OpenKind.Lights, room);
        /// <summary>A crawl way (a vent in a wall) whose grille the team took off (doorway index).</summary>
        public static int Crawl(int doorway) => Of(OpenKind.Crawl, doorway);
        /// <summary>A shut vent grate whose lid was taken off with a screwdriver (by the grate's index).</summary>
        public static int Vent(int grate) => Of(OpenKind.Vent, grate);
        /// <summary>A vault lock panel solved.</summary>
        public static int Console(int console) => Of(OpenKind.Console, console);
        public static int CageOpen(int prop) => Of(OpenKind.Cage, prop);
        /// <summary>One of the team's hack slots taken (oldest first), by a robot / device of this kind (<see cref="Stealth.Level.RobotKind"/>).</summary>
        public static int HackSlot(int slot, int kind = 0) => Of(OpenKind.HackSlot, slot * HackKinds + kind);
        /// <summary>Kinds a hack slot's key can tell apart.</summary>
        public const int HackKinds = 64;
        /// <summary>The lid over the shaft to the basement is open.</summary>
        public static readonly int Shaft = Of(OpenKind.Shaft, 0);
        /// <summary>The lid over the exit hatch's shaft (down to the passage in the sewer).</summary>
        public static readonly int ExitShaft = Of(OpenKind.Shaft, 1);
        /// <summary>The sewer's gate is open.</summary>
        public static readonly int Gate = Of(OpenKind.Gate, 0);
        public static int Stage(JobStage stage) => Of(OpenKind.Stage, (int)stage);
    }
}
