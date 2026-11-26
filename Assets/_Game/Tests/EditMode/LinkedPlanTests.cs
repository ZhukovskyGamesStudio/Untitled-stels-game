using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>A linked house: the links and corridor lengths come first, the blocks are placed to keep them.</summary>
    public class LinkedPlanTests
    {
        static BuildingSettings Settings() => new() { LinkedChance = 1f, LinkedCorridorShare = 0.75f, LinkCorridorLength = new Vector2Int(3, 9), MinCorridors = 1 };

        [Test]
        public void EveryCorridorJoinsItsTwoBlocksAtItsLength()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var b = Settings();
                var rng = new System.Random(seed);
                var m = PlanGenerator.Linked(rng, 3 + seed % 6, 1 + seed % 3, b);
                Assert.IsNotNull(m, $"seed {seed}: no layout");
                Assert.GreaterOrEqual(m.Corridors.Count, 1, $"seed {seed}: a linked house has a corridor");
                foreach (var (a, c, corridor) in m.Links)
                {
                    var ra = m.Blocks[a];
                    var rc = m.Blocks[c];
                    if (corridor < 0)
                    {
                        Assert.IsTrue(Touch(ra, rc), $"seed {seed}: blocks {a} and {c} join wall to wall");
                        continue;
                    }
                    var k = m.Corridors[corridor];
                    Assert.IsTrue(Touch(k, ra) && Touch(k, rc), $"seed {seed}: corridor {corridor} runs from block {a} to block {c}");
                    int len = Mathf.Max(k.width, k.height);
                    Assert.AreEqual(PlanGenerator.LinkCorridorWidth, Mathf.Min(k.width, k.height));
                    Assert.That(len, Is.InRange(b.LinkCorridorLength.x, b.LinkCorridorLength.y), $"seed {seed}: corridor length");
                }
                var all = new System.Collections.Generic.List<RectInt>(m.Blocks);
                all.AddRange(m.Corridors);
                for (int i = 0; i < all.Count; i++)
                {
                    Assert.GreaterOrEqual(all[i].xMin, 0);
                    Assert.GreaterOrEqual(all[i].yMin, 0);
                    for (int j = i + 1; j < all.Count; j++) Assert.IsFalse(all[i].Overlaps(all[j]), $"seed {seed}: pieces {i} and {j} overlap");
                }
            }
        }

        [Test]
        public void ALinkedHouseIsAllReachable()
        {
            var b = Settings();
            for (int seed = 0; seed < 40; seed++)
            {
                var o = PlanGenerator.Generate(seed, 1 + seed % 2, 900f, b);
                Assert.AreEqual("linked", o.Mass, $"seed {seed}");
                Assert.AreEqual(0, o.Unreached, $"seed {seed}: every room reached");
            }
        }

        /// <summary>Two rectangles share an edge (not just a corner).</summary>
        static bool Touch(RectInt a, RectInt b)
        {
            bool overlapZ = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > 0;
            bool overlapX = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > 0;
            return ((a.xMax == b.xMin || b.xMax == a.xMin) && overlapZ) || ((a.yMax == b.yMin || b.yMax == a.yMin) && overlapX);
        }
    }
}
