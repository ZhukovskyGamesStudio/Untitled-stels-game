using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// Generated houses shared by every test of the run. A house takes about half a second to generate, and the slow tests
    /// used to make some 1500 of them, each test its own seeds. Now every (seed, house, players, settings) is generated
    /// once per run (<see cref="Make"/>), the tests that look at "many different houses" share sets of them
    /// (<see cref="Corpus"/>, <see cref="Offices"/>, <see cref="Kinds"/>, <see cref="RoomsTest"/>), and a set is made all
    /// at once on several threads (<see cref="Many"/>: the generator is pure, the houses are the same as one by one).
    /// Tests must not change these layouts: a test that changes one generates its own with <see cref="LevelGenerator.Generate(int,int,BuildingSettings,SecuritySettings,int,FeatureSettings)"/>.
    /// </summary>
    public static class TestHouses
    {
        /// <summary>One house to make: the arguments of <see cref="LevelGenerator.Generate(int,int,BuildingSettings,SecuritySettings,int,FeatureSettings,bool,bool)"/>.</summary>
        public readonly struct Spec
        {
            public readonly int Seed, Level, Players;
            public readonly BuildingSettings Building;
            public readonly SecuritySettings Security;
            public readonly FeatureSettings Features;
            public readonly bool Classic, ClassicEnemies;

            public Spec(int seed, int level, BuildingSettings building = null, SecuritySettings security = null, int players = 1,
                FeatureSettings features = null, bool classic = false, bool classicEnemies = false)
            {
                Seed = seed; Level = level; Players = players; Building = building; Security = security; Features = features;
                Classic = classic; ClassicEnemies = classicEnemies;
            }

            /// <summary>The house's key: no settings and the default settings make the same house (the generator takes the defaults for null).</summary>
            public string Key => $"{Seed}|{Level}|{Players}|{Classic}|{ClassicEnemies}|{Json(Building, s_Building)}|{Json(Security, s_Security)}|{Json(Features, s_Features)}";

            public LevelLayout Generate() => LevelGenerator.Generate(Seed, Level, Building, Security, Players, Features, Classic, ClassicEnemies);
        }

        static readonly Dictionary<string, LevelLayout> s_Cache = new();
        static readonly ConditionalWeakTable<LevelLayout, Box> s_Removed = new();

        sealed class Box { public int Removed; }

        /// <summary>
        /// Threads that make a set of houses at once: more than eight only made it slower (the threads wait for each other's
        /// garbage collections), measured on 24 cores.
        /// </summary>
        static int Threads => Mathf.Clamp(System.Environment.ProcessorCount / 3, 1, 8);

        /// <summary>
        /// These houses, each generated once per run; the missing ones all at once on <see cref="Threads"/> threads. In the
        /// order asked.
        /// </summary>
        public static List<LevelLayout> Many(IEnumerable<Spec> specs)
        {
            var list = specs.ToList();
            var keys = list.Select(s => s.Key).ToList();
            var missing = new List<int>();
            var seen = new HashSet<string>();
            for (int i = 0; i < list.Count; i++)
                if (!s_Cache.ContainsKey(keys[i]) && seen.Add(keys[i])) missing.Add(i);
            var made = new (LevelLayout l, int removed)[missing.Count];
            if (missing.Count == 1)
            {
                var l = list[missing[0]].Generate();
                made[0] = (l, Furnisher.LastRemoved);
            }
            else if (missing.Count > 1)
                Parallel.For(0, missing.Count, new ParallelOptions { MaxDegreeOfParallelism = Threads }, i =>
                {
                    var l = list[missing[i]].Generate();
                    made[i] = (l, Furnisher.LastRemoved); // (the furnisher's statistics are the thread's own)
                });
            for (int i = 0; i < missing.Count; i++)
            {
                s_Cache[keys[missing[i]]] = made[i].l;
                s_Removed.Add(made[i].l, new Box { Removed = made[i].removed });
            }
            return keys.Select(k => s_Cache[k]).ToList();
        }

        /// <summary>
        /// <see cref="LevelGenerator.Generate(int,int,BuildingSettings,SecuritySettings,int,FeatureSettings,bool,bool)"/>,
        /// generated once per run for the same arguments (the settings are compared by their contents).
        /// </summary>
        public static LevelLayout Make(int seed, int levelIndex, BuildingSettings building = null, SecuritySettings security = null, int players = 1,
            FeatureSettings features = null, bool classic = false, bool classicEnemies = false) =>
            Many(new[] { new Spec(seed, levelIndex, building, security, players, features, classic, classicEnemies) })[0];

        static readonly string s_Building = JsonUtility.ToJson(new BuildingSettings()), s_Security = JsonUtility.ToJson(new SecuritySettings()),
            s_Features = JsonUtility.ToJson(new FeatureSettings());

        static string Json(object settings, string defaults)
        {
            if (settings == null) return "";
            string json = JsonUtility.ToJson(settings);
            return json == defaults ? "" : json;
        }

        /// <summary>A house with the default settings, generated on first use.</summary>
        public static LevelLayout Get(int seed, int level, int players = 1) => Make(seed, level, players: players);

        /// <summary>Furniture groups the final walkability check took out of a house made here (<see cref="Furnisher.LastRemoved"/> when it was generated).</summary>
        public static int Removed(LevelLayout l) => s_Removed.TryGetValue(l, out var b) ? b.Removed : 0;

        public static int Removed(int seed, int level, int players = 1) => Removed(Get(seed, level, players));

        /// <summary>
        /// The seeds and houses of <see cref="Sample"/>: different masses, kinds and sizes, the first house to a deep one.
        /// 123456789/2 once lost its whole top floor (a stair's approach walled off by furniture); 17/3 has a rotunda (the first houses are small and plain now).
        /// </summary>
        public static readonly (int seed, int level)[] SampleKeys = { (1, 0), (7, 1), (42, 2), (1337, 3), (99991, 4), (123456789, 2), (17, 3) };

        /// <summary>Seven different houses (see <see cref="SampleKeys"/>), generated once for the whole run.</summary>
        public static IEnumerable<LevelLayout> Sample => Many(SampleKeys.Select(k => new Spec(k.seed, k.level)));

        /// <summary>
        /// The k-th house of the shared set: houses 0 … 5 by turns (the first house to a deep one), a team of one every third,
        /// else of two; its own seed. The same keys on every run, so a statistic over them is the same every time.
        /// </summary>
        public static (int seed, int level, int players) CorpusKey(int k) => (7919 * (k + 1) + 13 * (k % 6), k % 6, k % 3 == 0 ? 1 : 2);

        /// <summary>How many houses of the shared set the "most houses" statistics look at (<see cref="Corpus"/>).</summary>
        public const int CorpusSize = 48;

        /// <summary>
        /// The first <paramref name="count"/> houses of the shared set (<see cref="CorpusKey"/>), with the default settings
        /// (or the classic plan generator, or the old enemy mode): the tests that measure "most houses" or "every house"
        /// draw from it, so a house made for one of them serves all the others.
        /// </summary>
        public static List<LevelLayout> Corpus(int count, bool classic = false, bool classicEnemies = false) =>
            Many(Enumerable.Range(0, count).Select(k =>
            {
                var (seed, level, players) = CorpusKey(k);
                return new Spec(seed, level, players: players, classic: classic, classicEnemies: classicEnemies);
            }));

        /// <summary>How many offices the office generator's statistics look at (<see cref="Offices"/>).</summary>
        public const int OfficeCount = 24;

        /// <summary>
        /// The first <paramref name="count"/> houses of the shared set's keys (<see cref="CorpusKey"/>) that the office
        /// generator lays out (not a location with a plan of its own, <see cref="HouseKinds.OwnPlan"/>; not the small first
        /// house): the statistics of the office generator's plan — depth, zoning, ways in — draw from it. A house's kind
        /// comes from its seed, so the others are skipped without being generated.
        /// </summary>
        public static List<LevelLayout> Offices(int count = OfficeCount, bool classic = false)
        {
            var specs = new List<Spec>();
            for (int k = 0; specs.Count < count && k < 100000; k++)
            {
                var (seed, level, players) = CorpusKey(k);
                if (level == 0 || HouseKinds.OwnPlan(HouseKinds.Of(seed, level))) continue;
                specs.Add(new Spec(seed, level, players: players, classic: classic));
            }
            return Many(specs);
        }

        /// <summary>
        /// A house of this kind (the hospital, the repair shop, an office character…), at <paramref name="level"/>: the same
        /// one for every test that wants "a house of every kind" (<see cref="HouseKinds.SeedOf"/>).
        /// </summary>
        public static LevelLayout OfKind(HouseKind kind, int level, int players = 1) => Make(HouseKinds.SeedOf(kind, level, 5), level, players: players);

        /// <summary>A house of every kind at each of these levels (<see cref="OfKind"/>), made at once: kind by kind, level by level.</summary>
        public static List<(HouseKind kind, int level, LevelLayout layout)> Kinds(params int[] levels)
        {
            var keys = new List<(HouseKind kind, int level)>();
            foreach (HouseKind kind in System.Enum.GetValues(typeof(HouseKind)))
                foreach (int level in levels) keys.Add((kind, level));
            var houses = Many(keys.Select(k => new Spec(HouseKinds.SeedOf(k.kind, k.level, 5), k.level)));
            return keys.Select((k, i) => (k.kind, k.level, houses[i])).ToList();
        }

        /// <summary>
        /// The keys of the first <paramref name="count"/> houses of <paramref name="kind"/> among seeds 1, 2, 3… (house
        /// <paramref name="houseOf"/>(seed)): a house's kind comes from its seed, so no other house is generated to find them.
        /// </summary>
        public static List<Spec> FirstKeys(HouseKind kind, int count, System.Func<int, int> houseOf, int below = 400)
        {
            var specs = new List<Spec>();
            for (int seed = 1; specs.Count < count && seed < below; seed++)
            {
                int house = houseOf(seed);
                if (HouseKinds.Of(seed, house) == kind) specs.Add(new Spec(seed, house));
            }
            return specs;
        }

        /// <summary>The first houses of <paramref name="kind"/> (<see cref="FirstKeys"/>), made at once.</summary>
        public static List<LevelLayout> First(HouseKind kind, int count, System.Func<int, int> houseOf, int below = 400) =>
            Many(FirstKeys(kind, count, houseOf, below));

        static Configs.LevelConfig s_RoomsTest;

        static Configs.LevelConfig RoomsTestLevel =>
            s_RoomsTest ??= UnityEditor.AssetDatabase.LoadAssetAtPath<Configs.LevelConfig>("Assets/_Game/Data/Config/RoomsTest/Level.asset");

        /// <summary>
        /// The k-th house of the rooms test (<c>Data/Config/RoomsTest/Level.asset</c>: one floor with every room type), house
        /// <paramref name="house"/> for a team of <paramref name="players"/>: the same houses for every test of that level.
        /// </summary>
        public static Spec RoomsTestSpec(int k, int house = 0, int players = 1) =>
            new(HouseKinds.TestRoomsSeed(7919 * (k + 1) + house), house, RoomsTestLevel.Building, RoomsTestLevel.Security, players, RoomsTestLevel.Features);

        public static LevelLayout RoomsTest(int k, int house = 0, int players = 1) => Many(new[] { RoomsTestSpec(k, house, players) })[0];

        /// <summary>The first <paramref name="count"/> houses of the rooms test (house 0, one player), made at once.</summary>
        public static List<LevelLayout> RoomsTests(int count) => Many(Enumerable.Range(0, count).Select(k => RoomsTestSpec(k)));
    }
}
