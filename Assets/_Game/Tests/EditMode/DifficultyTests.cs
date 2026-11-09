using NUnit.Framework;
using Stealth.Game;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    public class DifficultyTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6 };

        [Test]
        public void BuildingsGrowWithDepth()
        {
            int prevRooms = 0, prevRobots = 0;
            for (int level = 0; level < 8; level++)
            {
                var l = LevelGenerator.Generate(1, level);
                int rooms = l.Width * l.Height * l.Floors;
                Assert.GreaterOrEqual(rooms, prevRooms, $"level {level}");
                Assert.GreaterOrEqual(l.RobotSpawns.Count, prevRobots, $"level {level}");
                prevRooms = rooms;
                prevRobots = l.RobotSpawns.Count;
            }
            Assert.Greater(prevRooms, 18);
        }

        [Test]
        public void NothingHostileStartsNearTheHatch()
        {
            foreach (var seed in Seeds)
                for (int level = 0; level < 6; level++)
                {
                    var l = LevelGenerator.Generate(seed, level);
                    foreach (var r in l.RobotSpawns)
                        if (r.Floor == 0) Assert.Greater(Vector3.Distance(r.Position, l.HatchPosition), 12f, $"seed {seed} level {level}");
                    foreach (var c in l.CameraSpots)
                        if (LevelLayout.FloorOf(c.position.y) == 0)
                            Assert.Greater(Vector3.Distance(c.position, l.HatchPosition), 8f, $"camera too close to the hatch, seed {seed}");
                }
        }

        [Test]
        public void HunterArrivesAfterAlarmsOrTime()
        {
            Assert.IsFalse(GameManager.HunterDue(0, 10f, 0, 0));
            Assert.IsFalse(GameManager.HunterDue(2, 10f, 0, 0), "two alarms are forgiven on the first house");
            Assert.IsTrue(GameManager.HunterDue(3, 10f, 0, 0));
            Assert.IsTrue(GameManager.HunterDue(2, 10f, 1, 0), "deeper houses are less forgiving");
            Assert.IsTrue(GameManager.HunterDue(0, 400f, 0, 0), "lingering too long");
            Assert.IsFalse(GameManager.HunterDue(3, 10f, 0, 1), "only one hunter on the first houses");
            Assert.IsTrue(GameManager.HunterDue(4, 10f, 3, 1), "a second hunter deeper after more alarms");
        }

        [Test]
        public void QuotaGrowsWithDepthAndPenalties()
        {
            Assert.Greater(GameManager.QuotaFor(1000, 3, 0), GameManager.QuotaFor(1000, 0, 0));
            Assert.Greater(GameManager.QuotaFor(1000, 0, 2), GameManager.QuotaFor(1000, 0, 0));
            Assert.AreEqual(0, GameManager.QuotaFor(1000, 2, 1) % 5, "rounded to 5");
            Assert.AreEqual(10, GameManager.QuotaFor(0, 0, 0), "never zero");
            Assert.LessOrEqual(GameManager.QuotaFor(1000, 50, 0), 850, "never more than 85% of the loot");
        }
    }
}
