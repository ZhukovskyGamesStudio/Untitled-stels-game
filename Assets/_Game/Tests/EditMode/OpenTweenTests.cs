using NUnit.Framework;
using Stealth.Level;

namespace Stealth.Tests
{
    /// <summary>Lids, grates and gates: shown as the server has them on the first frame, then moving (and sounding) only on a change.</summary>
    public class OpenTweenTests
    {
        [Test]
        public void FirstStepSnapsSilently()
        {
            var t = new OpenTween();
            Assert.AreEqual(OpenMove.None, t.Step(true, 0.01f, 2f));
            Assert.AreEqual(1f, t.Value);
        }

        [Test]
        public void ChangeStartsOneMoveAndArrives()
        {
            var t = new OpenTween();
            t.Step(false, 0.1f, 1f);
            Assert.AreEqual(OpenMove.StartedOpening, t.Step(true, 0.25f, 1f));
            Assert.AreEqual(0.25f, t.Value, 1e-4f);
            Assert.IsFalse(t.Settled);
            for (int i = 0; i < 10; i++) Assert.AreEqual(OpenMove.None, t.Step(true, 0.25f, 1f));
            Assert.AreEqual(1f, t.Value);
            Assert.AreEqual(OpenMove.StartedShutting, t.Step(false, 0.1f, 1f));
        }
    }
}
