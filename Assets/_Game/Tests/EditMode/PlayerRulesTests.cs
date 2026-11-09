using NUnit.Framework;
using Stealth.Player;

namespace Stealth.Tests
{
    /// <summary>Health, laughing gas and footstep noise of the player (pure rules).</summary>
    public class PlayerRulesTests
    {
        [Test]
        public void TwoHitsDownAPlayerWithABreatherBetween()
        {
            var h = HealthState.Full(0f);
            Assert.IsFalse(h.TryHit(1f), "invulnerable right after the level starts");
            Assert.IsTrue(h.TryHit(4f));
            Assert.AreEqual(1, h.Hp);
            Assert.IsFalse(h.TryHit(4.5f), "a moment to run after the first hit");
            Assert.IsTrue(h.TryHit(6f));
            Assert.IsTrue(h.Downed);
            Assert.IsFalse(h.TryHit(10f), "no hits while downed");
        }

        [Test]
        public void RevivedPlayersComeBackWithOneHp()
        {
            var h = HealthState.Full(0f);
            h.Knockout();
            Assert.IsTrue(h.Downed);
            h.Revive(10f);
            Assert.IsFalse(h.Downed);
            Assert.AreEqual(1, h.Hp);
            Assert.IsFalse(h.TryHit(11f), "short protection after the revive");
        }

        [Test]
        public void LaughingGasMakesNoiseAndKnocksOut()
        {
            var gas = new GasExposure();
            int laughs = 0;
            GasExposure.Effect last = GasExposure.Effect.None;
            for (float t = 0; t < GasExposure.Limit + 0.5f; t += 0.1f)
            {
                last = gas.Tick(true, 0.1f);
                if (last == GasExposure.Effect.Laugh) laughs++;
                if (last == GasExposure.Effect.Knockout) break;
            }
            Assert.AreEqual(GasExposure.Effect.Knockout, last);
            int expected = (int)((GasExposure.Limit - GasExposure.LaughAfter) / GasExposure.LaughInterval);
            Assert.That(laughs, Is.InRange(expected - 1, expected + 2), "laughs every 1.2 s once it kicks in");
        }

        [Test]
        public void GasWearsOffIndoors()
        {
            var gas = new GasExposure();
            for (int i = 0; i < 100; i++) gas.Tick(true, 0.1f);
            Assert.IsTrue(gas.Laughing);
            for (int i = 0; i < 10; i++) Assert.AreEqual(GasExposure.Effect.None, gas.Tick(false, 0.1f), "no laughing inside");
            for (int i = 0; i < 30; i++) gas.Tick(false, 0.1f);
            Assert.AreEqual(0f, gas.Time, 1e-4f, "three times faster than it builds up");
        }

        [Test]
        public void FootstepsAreQuietWhenCrouchingAndLoudWhenRunning()
        {
            var steps = new FootstepNoise();
            Assert.Less(steps.Radius(true, false), steps.Radius(false, false));
            Assert.Greater(steps.Radius(false, true), steps.Radius(false, false));
            Assert.Less(FootstepNoise.Interval(false, true), FootstepNoise.Interval(false, false), "running steps come faster");
        }

        [Test]
        public void StandingStillOrJumpingMakesNoSteps()
        {
            var steps = new FootstepNoise();
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(0f, steps.Tick(0.2f, true, false, false, 0.1f), "standing");
                Assert.AreEqual(0f, steps.Tick(4f, false, false, false, 0.1f), "in the air");
            }
            float total = 0f;
            int count = 0;
            for (int i = 0; i < 20; i++)
            {
                float r = steps.Tick(3.6f, true, false, false, 0.1f);
                if (r > 0f) { total += r; count++; }
            }
            Assert.AreEqual(4, count, "a step every 0.45 s over two seconds");
            Assert.AreEqual(steps.WalkRadius, total / count);
        }

        [Test]
        public void LandingNoiseGrowsWithTheFall()
        {
            Assert.AreEqual(0f, FootstepNoise.LandingRadius(2f), "stepping down a stair");
            Assert.Greater(FootstepNoise.LandingRadius(9f), FootstepNoise.LandingRadius(4f));
            Assert.AreEqual(14f, FootstepNoise.LandingRadius(20f), 1e-4f, "capped");
        }
    }
}
