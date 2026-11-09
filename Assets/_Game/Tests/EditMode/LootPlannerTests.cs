using System.Linq;
using NUnit.Framework;
using Stealth.Items;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    public class LootPlannerTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 42, 777 };

        [Test]
        public void ItemsAreSpacedApart()
        {
            foreach (var seed in Seeds)
            {
                var loot = LootPlanner.Plan(LevelGenerator.Generate(seed, 3), new System.Random(seed));
                for (int i = 0; i < loot.Count; i++)
                    for (int j = i + 1; j < loot.Count; j++)
                        Assert.GreaterOrEqual(Vector3.Distance(loot[i].Position, loot[j].Position), new LootSettings().MinSpacing - 1e-4f);
            }
        }

        [Test]
        public void EveryFloorGetsLoot()
        {
            foreach (var seed in Seeds)
            {
                var l = LevelGenerator.Generate(seed, 4);
                var loot = LootPlanner.Plan(l, new System.Random(seed));
                for (int f = 0; f < l.Floors; f++)
                    Assert.IsTrue(loot.Any(x => LevelLayout.FloorOf(x.Position.y) == f), $"seed {seed} floor {f}");
            }
        }

        [Test]
        public void EnoughLootForTheQuotaAndMoreOnDeeperLevels()
        {
            int Total(int level)
            {
                int sum = 0;
                foreach (var seed in Seeds)
                    foreach (var x in LootPlanner.Plan(LevelGenerator.Generate(seed, level), new System.Random(seed)))
                        sum += ItemCatalog.Value(x.Shape, x.Color);
                return sum / Seeds.Length;
            }
            int easy = Total(0), hard = Total(5);
            Assert.Greater(easy, 100, "level 0 has enough loot for a sensible quota");
            Assert.Greater(hard, easy * 2, "deeper buildings are much richer");
        }

        [Test]
        public void HigherFloorsAreRicherOnAverage()
        {
            float low = 0, high = 0;
            int nl = 0, nh = 0;
            foreach (var seed in Enumerable.Range(1, 40))
            {
                var l = LevelGenerator.Generate(seed, 3);
                foreach (var x in LootPlanner.Plan(l, new System.Random(seed)))
                {
                    int f = LevelLayout.FloorOf(x.Position.y);
                    if (f == 0) { low += ItemCatalog.Colors[x.Color].Multiplier; nl++; }
                    if (f == l.Floors - 1) { high += ItemCatalog.Colors[x.Color].Multiplier; nh++; }
                }
            }
            Assert.Greater(high / nh, low / nl);
        }
    }
}
