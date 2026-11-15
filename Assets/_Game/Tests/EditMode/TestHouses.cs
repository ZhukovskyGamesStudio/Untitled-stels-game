using System.Collections.Generic;
using Stealth.Level;

namespace Stealth.Tests
{
    /// <summary>
    /// Generated houses shared by every test of the run: a house takes over half a second to generate, so a test that
    /// loops over dozens of its own was a test of minutes. Each (seed, house, players) is generated once; tests must not
    /// change these layouts (generate a fresh one for that).
    /// </summary>
    public static class TestHouses
    {
        static readonly Dictionary<(int seed, int level, int players), LevelLayout> s_Cache = new();

        /// <summary>A house with the default settings, generated on first use.</summary>
        public static LevelLayout Get(int seed, int level, int players = 1)
        {
            var key = (seed, level, players);
            if (!s_Cache.TryGetValue(key, out var l)) s_Cache[key] = l = LevelGenerator.Generate(seed, level, players: players);
            return l;
        }

        /// <summary>
        /// The seeds and houses of <see cref="Sample"/>: different masses, kinds and sizes, the first house to a deep one.
        /// 123456789/2 once lost its whole top floor (a stair's approach walled off by furniture); 3/2 has an octagon.
        /// </summary>
        public static readonly (int seed, int level)[] SampleKeys = { (1, 0), (7, 1), (42, 2), (1337, 3), (99991, 4), (123456789, 2), (3, 2) };

        /// <summary>Seven different houses (see <see cref="SampleKeys"/>), generated once for the whole run.</summary>
        public static IEnumerable<LevelLayout> Sample
        {
            get
            {
                foreach (var (seed, level) in SampleKeys) yield return Get(seed, level);
            }
        }
    }
}
