using System.Linq;
using NUnit.Framework;
using Stealth.Items;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    public class LootPlannerTests
    {
        // loot planning is cheap: many draws over the shared houses (generating a house is the slow part)
        [Test]
        public void ItemsAreSpacedApart()
        {
            foreach (var l in TestHouses.Sample)
                for (int k = 0; k < 4; k++)
                {
                    var loot = LootPlanner.Plan(l, new System.Random(k));
                    for (int i = 0; i < loot.Count; i++)
                        for (int j = i + 1; j < loot.Count; j++)
                            Assert.GreaterOrEqual(Vector3.Distance(loot[i].Position, loot[j].Position), new LootSettings().MinSpacing - 1e-4f, $"seed {l.Seed}");
                }
        }

        [Test]
        public void EveryFloorGetsLoot()
        {
            foreach (var l in TestHouses.Sample)
            {
                var loot = LootPlanner.Plan(l, new System.Random(l.Seed));
                for (int f = 0; f < l.Floors; f++)
                    Assert.IsTrue(loot.Any(x => LevelLayout.FloorOf(x.Position.y) == f), $"seed {l.Seed} floor {f}");
            }
        }

        [Test]
        public void EnoughLootForTheQuotaAndMoreOnDeeperLevels()
        {
            int Total(LevelLayout l)
            {
                int sum = 0;
                for (int k = 0; k < 8; k++)
                    foreach (var x in LootPlanner.Plan(l, new System.Random(k)))
                        sum += ItemCatalog.Value(x.Shape, x.Color);
                return sum / 8;
            }
            int easy = Total(TestHouses.Get(1, 0)), hard = Total(TestHouses.Get(99991, 4));
            Assert.Greater(easy, 100, "the first house has enough loot for a sensible quota");
            Assert.Greater(hard, easy * 1.7f, $"deeper buildings are much richer: {easy} → {hard}");
        }

        [Test]
        public void HigherFloorsAreRicherOnAverage()
        {
            // a higher floor adds to the bonus, and a bigger bonus draws more valuable colours
            var s = new LootSettings();
            Assert.Greater(s.BonusPerFloor, 0);
            float Mean(int bonus)
            {
                var rng = new System.Random(5);
                float sum = 0f;
                for (int i = 0; i < 4000; i++) sum += ItemCatalog.Colors[ItemCatalog.RandomColor(rng, bonus)].Multiplier;
                return sum / 4000f;
            }
            float ground = Mean(3 * s.BonusPerHouse), top = Mean(3 * s.BonusPerHouse + 2 * s.BonusPerFloor);
            Assert.Greater(top, ground * 1.05f, $"ground {ground:0.00}, two floors up {top:0.00}");
        }
    
        [Test]
        public void BehindAKeyDoorThereIsAlwaysSomething()
        {
            int regions = 0;
            foreach (var l in TestHouses.Sample)
            {
                var behind = LootPlanner.KeyRegions(l);
                var loot = LootPlanner.Plan(l, new System.Random(l.Seed));
                for (int k = 0; k < behind.Count; k++)
                {
                    int inside = loot.Count(x => l.Plan.RoomAt(x.Position + Vector3.up * 0.3f) is { } r && behind[k].Contains(r.Id));
                    Assert.Greater(inside, 0, $"seed {l.Seed}: nothing behind key door {k} (rooms {string.Join(",", behind[k])})");
                    regions++;
                }
            }
            Assert.Greater(regions, 0, "the shared houses have key doors");
        }
    }
}
