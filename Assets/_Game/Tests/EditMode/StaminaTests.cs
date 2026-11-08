using NUnit.Framework;
using Stealth.Player;

namespace Stealth.Tests
{
    public class StaminaTests
    {
        const float Dt = 1f / 60f;

        static void Run(Stamina s, bool sprint, float seconds)
        {
            for (float t = 0; t < seconds; t += Dt) s.Tick(sprint, Dt);
        }

        [Test]
        public void SprintLastsAboutSixSeconds()
        {
            var s = new Stamina();
            Run(s, true, 5.5f);
            Assert.IsTrue(s.CanSprint);
            Run(s, true, 0.6f);
            Assert.IsFalse(s.CanSprint);
            Assert.IsTrue(s.Exhausted);
        }

        [Test]
        public void ExhaustionLastsUntilThreshold()
        {
            var s = new Stamina();
            Run(s, true, 6.05f);
            Run(s, false, 1.5f); // delay + a bit
            Assert.IsTrue(s.Exhausted, "still exhausted right after the delay");
            Run(s, false, 1.5f);
            Assert.IsFalse(s.Exhausted);
            Assert.GreaterOrEqual(s.Value, s.RecoverThreshold);
        }

        [Test]
        public void RegeneratesOnlyAfterDelay()
        {
            var s = new Stamina();
            Run(s, true, 2f);
            float v = s.Value;
            Run(s, false, 1f);
            Assert.AreEqual(v, s.Value, 1e-5f);
            Run(s, false, 5f);
            Assert.AreEqual(1f, s.Value, 1e-5f);
        }

        [Test]
        public void JumpCostsStamina()
        {
            var s = new Stamina();
            s.SpendJump();
            Assert.Less(s.Value, 1f);
        }
    }
}
