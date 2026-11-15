using NUnit.Framework;
using Stealth.Game;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    public class DifficultyTests
    {
        [Test]
        public void BuildingsGrowWithDepth()
        {
            // the numbers the generator builds from: rooms per floor, floors and guards never shrink from house to house
            var b = new BuildingSettings();
            var sec = new SecuritySettings();
            float prevRooms = 0f;
            int prevFloors = 0, prevRobots = 0;
            for (int level = 0; level < 8; level++)
            {
                float rooms = b.RoomsFor(level, 1);
                int floors = b.FloorsFor(level), robots = sec.RobotsFor(level, floors, 0);
                Assert.GreaterOrEqual(rooms, prevRooms, $"level {level}");
                Assert.GreaterOrEqual(floors, prevFloors, $"level {level}");
                Assert.Greater(robots, prevRobots, $"level {level}");
                prevRooms = rooms;
                prevFloors = floors;
                prevRobots = robots;
            }
            Assert.Greater(prevRooms * prevFloors, 18f);
            // and a house follows them
            var first = TestHouses.Get(1, 0);
            var deep = TestHouses.Get(99991, 4);
            Assert.Greater(deep.RoomUnits, first.RoomUnits);
            Assert.Greater(deep.RobotSpawns.Count, first.RobotSpawns.Count);
        }

        [Test]
        public void NothingHostileStartsNearTheHatch()
        {
            foreach (var l in TestHouses.Sample)
                {
                    int seed = l.Seed, level = l.LevelIndex;
                    foreach (var r in l.RobotSpawns)
                        if (r.Floor == 0)
                        {
                            Assert.Greater(Vector3.Distance(r.Position, l.HatchPosition), 12f, $"seed {seed} level {level}");
                            if (r.Kind == RobotKind.Warden) Assert.Greater(Vector3.Distance(r.Position, l.ExitHatchPosition), 8f, $"a warden at the exit, seed {seed} level {level}");
                        }
                    foreach (var c in l.CameraSpots)
                        if (LevelLayout.FloorOf(c.position.y) == 0)
                            Assert.Greater(Vector3.Distance(c.position, l.HatchPosition), 8f, $"camera too close to the hatch, seed {seed}");
                }
        }

        [Test]
        public void HunterArrivesAfterAlarmsOrTime()
        {
            Assert.IsFalse(RunState.HunterDue(0, 10f, 0, 0));
            Assert.IsFalse(RunState.HunterDue(2, 10f, 0, 0), "two alarms are forgiven on the first house");
            Assert.IsTrue(RunState.HunterDue(3, 10f, 0, 0));
            Assert.IsTrue(RunState.HunterDue(2, 10f, 1, 0), "deeper houses are less forgiving");
            Assert.IsTrue(RunState.HunterDue(0, 400f, 0, 0), "lingering too long");
            Assert.IsFalse(RunState.HunterDue(3, 10f, 0, 1), "only one hunter on the first houses");
            Assert.IsTrue(RunState.HunterDue(4, 10f, 3, 1), "a second hunter deeper after more alarms");
        }

        [Test]
        public void QuotaGrowsWithDepthAndPenalties()
        {
            Assert.Greater(RunState.QuotaFor(1000, 3, 0), RunState.QuotaFor(1000, 0, 0));
            Assert.Greater(RunState.QuotaFor(1000, 0, 2), RunState.QuotaFor(1000, 0, 0));
            Assert.AreEqual(0, RunState.QuotaFor(1000, 2, 1) % 5, "rounded to 5");
            Assert.AreEqual(10, RunState.QuotaFor(0, 0, 0), "never zero");
            Assert.LessOrEqual(RunState.QuotaFor(1000, 50, 0), 850, "never more than 85% of the loot");
        }
    }
}
