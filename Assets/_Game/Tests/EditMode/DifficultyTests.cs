using System.Linq;
using NUnit.Framework;
using Stealth.Game;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    public class DifficultyTests
    {
        [Test]
        public void ABuildingsSizeComesFromItsKindAndSeedNotFromTheHouseOrTheTeam()
        {
            var b = new BuildingSettings();
            foreach (var size in b.Sizes)
                for (int seed = 1; seed < 40; seed++)
                {
                    var (area, floors) = b.SizeOf(size.Kind, seed);
                    Assert.That(area, Is.InRange(size.MinArea, size.MaxArea), $"{size.Kind} {seed}");
                    Assert.That(floors, Is.InRange(size.MinFloors, size.MaxFloors), $"{size.Kind} {seed}");
                }
            // the same seed as the first house and as a late one, for one and for four
            int office = Enumerable.Range(1, 400).First(s => HouseKinds.Of(s, 0) == HouseKind.Office && HouseKinds.Of(s, 5) == HouseKind.Office);
            var first = TestHouses.Get(office, 0);
            var late = TestHouses.Get(office, 5, 4);
            Assert.AreEqual(first.Floors, late.Floors);
            Assert.AreEqual(first.FloorArea, late.FloorArea, 1f);
            Assert.AreEqual(4, late.Players);
            Assert.Greater(late.RobotSpawns.Count, first.RobotSpawns.Count, "but more guards");
        }

        [Test]
        public void GuardsAndLootGrowWithTheAreaTheHouseAndTheTeam()
        {
            var s = new SecuritySettings();
            Assert.Greater(s.RobotsFor(8000f, 0, 1), s.RobotsFor(4000f, 0, 1));
            Assert.Greater(s.RobotsFor(6000f, 5, 1), s.RobotsFor(6000f, 0, 1));
            Assert.Greater(s.RobotsFor(6000f, 2, 4), s.RobotsFor(6000f, 2, 1));
            Assert.Less(s.RobotsFor(6000f, 2, 4), 2 * s.RobotsFor(6000f, 2, 1), "much softer than x4");
            Assert.AreEqual(s.RobotsFor(6000f, 2, 4), s.RobotsFor(6000f, 2, 9), "the last entry repeats");
            var l = TestHouses.Get(42, 2);
            var team = TestHouses.Get(42, 2, 4);
            Assert.AreEqual(l.FloorArea, team.FloorArea, 1f, "the same building");
            Assert.Greater(Stealth.Items.LootPlanner.ItemCount(team), Stealth.Items.LootPlanner.ItemCount(l), "denser loot");
        }

        [Test]
        public void EveryHouseHoldsItsQuotaTwice()
        {
            foreach (var l in TestHouses.Sample)
            {
                var run = new RunState();
                float want = run.LootFor(l.LevelIndex, 4); // (the most a house is asked to hold)
                var loot = Stealth.Items.LootPlanner.Plan(l, new System.Random(l.Seed), null, want);
                Assert.GreaterOrEqual(Stealth.Items.LootPlanner.ValueOf(loot), want, $"seed {l.Seed} level {l.LevelIndex} ({l.Kind})");
                for (int i = 0; i < loot.Count; i++)
                    for (int j = i + 1; j < loot.Count; j++)
                        Assert.Greater((loot[i].Position - loot[j].Position).magnitude, new Stealth.Items.LootSettings().MinSpacing - 0.01f, $"seed {l.Seed}: two things on one spot");
            }
        }

        [Test]
        public void NothingHostileStartsNearTheHatch()
        {
            foreach (var l in TestHouses.Sample)
                {
                    int seed = l.Seed, level = l.LevelIndex;
                    foreach (var r in l.RobotSpawns)
                        if (r.Floor == 0)
                            Assert.Greater(Vector3.Distance(r.Position, l.HatchPosition), 12f, $"seed {seed} level {level}");
                    foreach (var c in l.CameraSpots)
                        if (LevelLayout.FloorOf(c.position.y) == 0)
                            Assert.Greater(Vector3.Distance(c.position, l.HatchPosition), 8f, $"camera too close to the hatch, seed {seed}");
                }
        }
    }
}
