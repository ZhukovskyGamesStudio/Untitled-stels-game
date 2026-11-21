using NUnit.Framework;
using Stealth.Game;
using Stealth.Level;

namespace Stealth.Tests
{
    /// <summary>The run starts in the sewer: the spawn is lit, the sewers between houses keep their dim pools of light.</summary>
    public class SewerLightTests
    {
        [Test]
        public void TheRunsFirstSewerIsLitOnBothWallsWithNoBrokenLamps()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var p = SewerPlan.Make(seed, RunMap.StartDepth);
                Assert.IsTrue(p.StartLights, $"seed {seed}");
                foreach (var s in p.Segments)
                {
                    Assert.AreNotEqual(0, s.LampSide, $"seed {seed}: no dark segment at the spawn");
                    Assert.IsTrue(s.LampBoth, $"seed {seed}");
                    Assert.IsFalse(s.LampBroken, $"seed {seed}");
                }
            }
        }

        [Test]
        public void TheSewersBetweenHousesStayDimAndUnchanged()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var p = SewerPlan.Make(seed, 2);
                Assert.IsFalse(p.StartLights);
                foreach (var s in p.Segments) Assert.IsFalse(s.LampBoth);
            }
        }

        [Test]
        public void TheStartsLightsComeFromTheSeed()
        {
            // every peer builds the sewer itself: the same seed, the same lamps
            var a = SewerPlan.Make(7, RunMap.StartDepth);
            var b = SewerPlan.Make(7, RunMap.StartDepth);
            Assert.AreEqual(a.Segments.Count, b.Segments.Count);
            Assert.AreEqual(a.LampColor, b.LampColor);
            for (int i = 0; i < a.Segments.Count; i++)
            {
                Assert.AreEqual(a.Segments[i].Variant, b.Segments[i].Variant);
                Assert.AreEqual(a.Segments[i].LampSide, b.Segments[i].LampSide);
                Assert.AreEqual(a.Segments[i].Junk.Count, b.Segments[i].Junk.Count);
            }
        }
    }
}
