using System.IO;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;

namespace Stealth.Tests
{
    public class LevelRandomTests
    {
        [Test]
        public void OneRunSeedGivesTheSameHousesAndStreams()
        {
            Assert.AreEqual(LevelRandom.LevelSeed(8, 3), LevelRandom.LevelSeed(8, 3));
            Assert.AreNotEqual(LevelRandom.LevelSeed(8, 3), LevelRandom.LevelSeed(8, 4), "every house of a run differs");
            Assert.AreNotEqual(LevelRandom.LevelSeed(8, 0), LevelRandom.LevelSeed(9, 0), "runs differ");
            for (int run = -5; run < 200; run++) Assert.Greater(LevelRandom.LevelSeed(run, 0), 0, "0 means no level");

            LevelRandom.Begin(1234);
            var robots = Enumerable.Range(0, 3).Select(_ => LevelRandom.Next(RandomChannel.Robots).Next()).ToArray();
            var patrol = Enumerable.Range(0, 5).Select(_ => LevelRandom.Shared(RandomChannel.Patrol).Next()).ToArray();
            LevelRandom.Begin(1234);
            CollectionAssert.AreEqual(robots, Enumerable.Range(0, 3).Select(_ => LevelRandom.Next(RandomChannel.Robots).Next()).ToArray(),
                "the n-th robot gets the same stream again");
            CollectionAssert.AreEqual(patrol, Enumerable.Range(0, 5).Select(_ => LevelRandom.Shared(RandomChannel.Patrol).Next()).ToArray());
            Assert.AreNotEqual(robots[0], robots[1], "each robot has its own stream");
        }

        [Test]
        public void ChannelsDoNotShiftEachOther()
        {
            LevelRandom.Begin(77);
            int loot = LevelRandom.Stream(RandomChannel.Loot).Next();
            LevelRandom.Begin(77);
            for (int i = 0; i < 10; i++) LevelRandom.Shared(RandomChannel.Audio).Next(); // extra cosmetic rolls…
            Assert.AreEqual(loot, LevelRandom.Stream(RandomChannel.Loot).Next(), "…do not change the loot");
        }

        /// <summary>Game code rolls only through LevelRandom (UnityEngine.Random is only seeded, as a safety net).</summary>
        [Test]
        public void GameCodeDoesNotUseUnseededRandom()
        {
            foreach (var path in Directory.GetFiles("Assets/_Game/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                if (path.Replace('\\', '/').Contains("/Game/DevAutomation")) continue;
                var text = File.ReadAllText(path);
                Assert.IsFalse(text.Contains("Random.Range(") || text.Contains("Random.value") || text.Contains("Random.insideUnit"),
                    $"{path}: use LevelRandom instead of UnityEngine.Random");
                Assert.IsFalse(text.Contains("new System.Random()") && !path.EndsWith("GameManager.cs"),
                    $"{path}: an unseeded System.Random (only the run seed itself may be fresh)");
            }
        }
    }
}
