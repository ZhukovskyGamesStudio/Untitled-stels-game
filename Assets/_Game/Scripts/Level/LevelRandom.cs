using System.Collections.Generic;

namespace Stealth.Level
{
    /// <summary>What a random stream is for: each purpose draws from its own stream, so adding a roll in one place does not
    /// shift every other roll of the level. Append only — the values are part of the hash.</summary>
    public enum RandomChannel
    {
        Run = 0,      // the seeds of the houses of a run
        Layout = 1,   // the plan of the building (LevelGenerator)
        Loot = 2,     // what lies where (LootPlanner)
        Robots = 3,   // per robot: brain, cargo, colour of a sorter, first stomp
        Patrol = 4,   // where robots walk next
        Devices = 5,  // start positions of spiders and vacuums
        Audio = 6,    // pitch of steps and thumps, where a loop starts (cosmetic, every peer)
        Engine = 7,   // UnityEngine.Random of the level (safety net for code that still uses it)
    }

    /// <summary>
    /// All randomness of a run comes from one number. The run seed gives every house its seed
    /// (<see cref="LevelSeed"/>); the house seed gives a separate stream per purpose and per object
    /// (<see cref="Stream"/>, <see cref="Next"/>). Same run seed → the same houses, loot, robots, patrols and sounds
    /// (as long as the players do the same). Pure: no engine, no time.
    /// </summary>
    public static class LevelRandom
    {
        static readonly Dictionary<RandomChannel, System.Random> s_Shared = new();
        static readonly Dictionary<RandomChannel, int> s_Counters = new();

        /// <summary>Seed of the current house (0 before any house was built).</summary>
        public static int Seed { get; private set; }

        /// <summary>A new house starts: every stream restarts from its seed.</summary>
        public static void Begin(int levelSeed)
        {
            Seed = levelSeed;
            s_Shared.Clear();
            s_Counters.Clear();
        }

        /// <summary>Seed of house <paramref name="index"/> of a run (never 0: 0 means "no level").</summary>
        public static int LevelSeed(int runSeed, int index)
        {
            int s = Mix(runSeed, (int)RandomChannel.Run, index) & int.MaxValue;
            return s == 0 ? 1 : s;
        }

        /// <summary>A fresh stream for one thing (a robot, a room…) — the same for the same seed, channel and index.</summary>
        public static System.Random Stream(RandomChannel channel, int index = 0) => new(Mix(Seed, (int)channel, index));

        /// <summary>The next object of a channel gets its own stream (the n-th robot spawned, the n-th spider…).</summary>
        public static System.Random Next(RandomChannel channel)
        {
            s_Counters.TryGetValue(channel, out int n);
            s_Counters[channel] = n + 1;
            return Stream(channel, n);
        }

        /// <summary>One running stream per channel for a sequence of small rolls (patrol points, step pitch).</summary>
        public static System.Random Shared(RandomChannel channel)
        {
            if (!s_Shared.TryGetValue(channel, out var rng)) s_Shared[channel] = rng = Stream(channel, -1);
            return rng;
        }

        public static float Range(this System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        public static float Value(this System.Random rng) => (float)rng.NextDouble();

        /// <summary>Stable integer hash (murmur3 finaliser): the same on every machine and every .NET.</summary>
        public static int Mix(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u;
                h ^= (uint)b + 0x7F4A7C15u + (h << 6) + (h >> 2);
                h ^= (uint)c * 0x85EBCA77u + 0x165667B1u + (h << 6) + (h >> 2);
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return (int)h;
            }
        }
    }
}
