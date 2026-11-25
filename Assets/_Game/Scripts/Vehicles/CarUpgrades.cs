using System.Collections.Generic;
using Stealth.Items;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>What the garage's car upgrade bench (Level.Workbench) improves on the team's pickup.</summary>
    public enum CarUpgrade : byte
    {
        /// <summary>Levels 1–3: the drive on costs less fuel (<see cref="CarUpgradeSettings.EngineCutPerLevel"/> a level).</summary>
        Engine,
        /// <summary>Levels 1–3: shelves in the cargo bed (only to look at for now).</summary>
        Trunk,
        /// <summary>Levels 1–3: Linus's backpack — a bigger pack on his back, a bigger stash grid (<see cref="CarUpgradeSettings.KennelStash"/>).</summary>
        Kennel,
        /// <summary>Unique: the panic button on the glovebox (nominal for now: bought, does nothing yet).</summary>
        Teleport,
        /// <summary>Unique, not designed yet: shown, cannot be bought.</summary>
        Unique5,
        /// <summary>Unique, not designed yet: shown, cannot be bought.</summary>
        Unique6,
    }

    /// <summary>Tuning of the car upgrade bench (RunConfig → Upgrades).</summary>
    [System.Serializable]
    public class CarUpgradeSettings
    {
        [Tooltip("Each level of the engine cuts the fuel the drive to the next house costs by this share (0.1 = 10 % a level).")]
        [Range(0f, 0.3f)] public float EngineCutPerLevel = 0.1f;
        [Tooltip("How many things a levelled upgrade's recipe asks for, for level 1, 2 and 3 (random loot of the run, CraftingSettings.Pool).")]
        public int[] RecipeSize = { 2, 3, 4 };
        [Tooltip("How many things a unique upgrade's recipe asks for.")]
        public int UniqueRecipeSize = 4;
        [Tooltip("The storage robot's box (Linus's backpack), columns x rows, by the upgrade's level 0..3 (at most Game.TeamStash.MaxWidth x MaxHeight).")]
        public Vector2Int[] KennelStash = { new(3, 5), new(4, 5), new(4, 7), new(6, 7) };
    }

    /// <summary>
    /// The team's car upgrades this run (pure, unit tested): a level per <see cref="CarUpgrade"/> — the levelled ones go
    /// 0 → 3, a level at a time; the unique ones 0 → 1; the undesigned ones stay 0. The server raises them
    /// (GameManager.CarUpgradeServer), every peer gets the packed levels (<see cref="Pack"/>). New run: all at 0.
    /// </summary>
    public sealed class CarUpgrades
    {
        public const int Count = 6;
        public const int MaxLevelled = 3;
        /// <summary>The most things a recipe asks for (the bench's card has a place for each).</summary>
        public const int MostThings = 4;
        /// <summary>Bits a level takes in <see cref="Pack"/>.</summary>
        const int Bits = 4;

        readonly byte[] m_Levels = new byte[Count];
        /// <summary>Bumped on every change.</summary>
        public int Version { get; private set; }

        /// <summary>The highest level of an upgrade: 3 for the levelled, 1 for the unique, 0 for those not designed yet.</summary>
        public static int MaxLevel(CarUpgrade u) => u switch
        {
            CarUpgrade.Engine or CarUpgrade.Trunk or CarUpgrade.Kennel => MaxLevelled,
            CarUpgrade.Teleport => 1,
            _ => 0,
        };

        public static bool IsUnique(CarUpgrade u) => u >= CarUpgrade.Teleport;

        /// <summary>Shown on the bench but not made yet (cannot be bought).</summary>
        public static bool IsPlanned(CarUpgrade u) => MaxLevel(u) == 0;

        public int Level(CarUpgrade u) => (int)u < Count ? m_Levels[(int)u] : 0;

        /// <summary>The next level can be bought (the one before it is there: the levels go one at a time).</summary>
        public bool CanRaise(CarUpgrade u) => (int)u < Count && Level(u) < MaxLevel(u);

        /// <summary>One level up (false: at the top, or not designed).</summary>
        public bool Raise(CarUpgrade u)
        {
            if (!CanRaise(u)) return false;
            m_Levels[(int)u]++;
            Version++;
            return true;
        }

        /// <summary>
        /// Any level at once, up or down (the cheat panel), clamped to the upgrade's own 0..<see cref="MaxLevel"/>. False
        /// if nothing changed.
        /// </summary>
        public bool Set(CarUpgrade u, int level)
        {
            if ((int)u >= Count) return false;
            byte to = (byte)Mathf.Clamp(level, 0, MaxLevel(u));
            if (m_Levels[(int)u] == to) return false;
            m_Levels[(int)u] = to;
            Version++;
            return true;
        }

        public void Clear()
        {
            System.Array.Clear(m_Levels, 0, Count);
            Version++;
        }

        /// <summary>The levels for the wire: <see cref="Bits"/> bits each.</summary>
        public uint Pack()
        {
            uint v = 0;
            for (int i = 0; i < Count; i++) v |= (uint)(m_Levels[i] & 0xF) << (i * Bits);
            return v;
        }

        /// <summary>Peers: the server's levels (each clamped to its upgrade's top).</summary>
        public void Apply(uint packed)
        {
            bool changed = false;
            for (int i = 0; i < Count; i++)
            {
                byte level = (byte)Mathf.Min((int)(packed >> (i * Bits) & 0xF), MaxLevel((CarUpgrade)i));
                if (m_Levels[i] == level) continue;
                m_Levels[i] = level;
                changed = true;
            }
            if (changed) Version++;
        }

        /// <summary>The share of a drive's fuel the team still pays with the engine at <paramref name="level"/> (1, 0.9, 0.8, 0.7 by default).</summary>
        public static float FuelFactor(int level, CarUpgradeSettings s)
        {
            s ??= new CarUpgradeSettings();
            return Mathf.Clamp01(1f - s.EngineCutPerLevel * Mathf.Clamp(level, 0, MaxLevelled));
        }

        /// <summary>A drive's fuel <paramref name="cost"/> with the engine at <paramref name="level"/>: cut, in fives like every cost, never below 5.</summary>
        public static int EngineCost(int cost, int level, CarUpgradeSettings s)
        {
            if (cost <= 0 || level <= 0) return cost;
            return Mathf.Max(5, Mathf.RoundToInt(cost * FuelFactor(level, s) / 5f) * 5);
        }

        /// <summary>The storage robot's box (columns, rows) with its backpack at <paramref name="level"/>.</summary>
        public static Vector2Int StashSize(int level, CarUpgradeSettings s)
        {
            var sizes = (s ?? new CarUpgradeSettings()).KennelStash;
            if (sizes == null || sizes.Length == 0) return new Vector2Int(Game.TeamStash.Width, Game.TeamStash.Height);
            var v = sizes[Mathf.Clamp(level, 0, sizes.Length - 1)];
            return new Vector2Int(Mathf.Clamp(v.x, 1, Game.TeamStash.MaxWidth), Mathf.Clamp(v.y, 1, Game.TeamStash.MaxHeight));
        }

        /// <summary>
        /// Which things of <paramref name="carried"/> (shapes, -1: not to be taken — worn, in the hand) a recipe takes: each
        /// ingredient once, the first carried thing of its shape. False (and <paramref name="take"/> empty) if one is missing.
        /// </summary>
        public static bool Gather(IReadOnlyList<int> carried, IReadOnlyList<byte> recipe, List<int> take)
        {
            take.Clear();
            if (recipe == null || recipe.Count == 0) return false;
            foreach (byte need in recipe)
            {
                int found = -1;
                for (int i = 0; i < carried.Count && found < 0; i++)
                    if (carried[i] == need && !take.Contains(i)) found = i;
                if (found < 0) { take.Clear(); return false; }
                take.Add(found);
            }
            return true;
        }
    }

    /// <summary>
    /// The recipes of the car upgrades in one run (pure, unit tested): for every upgrade and every level a set of loot to
    /// bring (shapes of <see cref="CraftingSettings.Pool"/>, each once, more for a higher level), drawn from the run's
    /// seed — every peer makes the same book, so the bench shows the server's recipes.
    /// </summary>
    public sealed class CarUpgradeBook
    {
        readonly byte[][] m_Recipes = new byte[CarUpgrades.Count * (CarUpgrades.MaxLevelled + 1)][];
        public int RunSeed { get; }

        static CarUpgradeBook s_Current;

        /// <summary>The book of this run (kept while the seed is the same).</summary>
        public static CarUpgradeBook Use(int runSeed, CarUpgradeSettings s, CraftingSettings craft)
        {
            if (s_Current == null || s_Current.RunSeed != runSeed) s_Current = new CarUpgradeBook(runSeed, s, craft);
            return s_Current;
        }

        public CarUpgradeBook(int runSeed, CarUpgradeSettings s, CraftingSettings craft)
        {
            s ??= new CarUpgradeSettings();
            RunSeed = runSeed;
            var pool = PoolOf(craft ?? new CraftingSettings());
            for (int u = 0; u < CarUpgrades.Count; u++)
            {
                var upgrade = (CarUpgrade)u;
                for (int level = 1; level <= CarUpgrades.MaxLevel(upgrade); level++)
                {
                    int n = CarUpgrades.IsUnique(upgrade) ? s.UniqueRecipeSize
                        : s.RecipeSize != null && s.RecipeSize.Length > 0 ? s.RecipeSize[Mathf.Clamp(level - 1, 0, s.RecipeSize.Length - 1)] : level + 1;
                    n = Mathf.Clamp(n, 1, Mathf.Min(pool.Count, CarUpgrades.MostThings));
                    // (a stream of its own per upgrade and level: a new upgrade does not shift the others' recipes)
                    var rng = new System.Random(LevelRandom.Mix(runSeed, Salt.CarUpgrades, u * 8 + level));
                    var list = new List<byte>(n);
                    while (list.Count < n)
                    {
                        byte pick = pool[rng.Next(pool.Count)];
                        if (!list.Contains(pick)) list.Add(pick);
                    }
                    list.Sort();
                    m_Recipes[u * (CarUpgrades.MaxLevelled + 1) + level] = list.ToArray();
                }
            }
        }

        /// <summary>What reaching <paramref name="level"/> of an upgrade takes (sorted shapes); null for a level it has not.</summary>
        public byte[] Recipe(CarUpgrade u, int level)
        {
            if ((int)u >= CarUpgrades.Count || level < 1 || level > CarUpgrades.MaxLevel(u)) return null;
            return m_Recipes[(int)u * (CarUpgrades.MaxLevelled + 1) + level];
        }

        static List<byte> PoolOf(CraftingSettings craft)
        {
            var pool = new List<byte>();
            if (craft.Pool != null)
                foreach (var id in craft.Pool)
                {
                    int i = System.Array.FindIndex(ItemCatalog.Shapes, x => x.Id == id);
                    if (i >= 0 && !pool.Contains((byte)i)) pool.Add((byte)i);
                }
            if (pool.Count < 4) pool.AddRange(new byte[] { ItemCatalog.Book, ItemCatalog.Cans, ItemCatalog.Battery, 28 }); // (tomatoes)
            return pool;
        }
    }
}
