using NUnit.Framework;
using Stealth.UI;

namespace Stealth.Tests
{
    public class FrameMeterTests
    {
        [Test]
        public void TheMeterAveragesHalfASecondOfFrames()
        {
            var meter = new FrameMeter();
            int closed = 0;
            for (int i = 0; i < 45; i++) if (meter.Add(1f / 60f)) closed++;
            Assert.AreEqual(1, closed, "three quarters of a second of frames: one window closed");
            Assert.AreEqual(60f, meter.Fps, 0.5f);
            Assert.AreEqual(16.7f, meter.Milliseconds, 0.1f);
            // a slow stretch: the numbers are the frames shown and their mean time, not the last frame's
            meter.Reset();
            bool done = false;
            for (int i = 0; i < 9 && !done; i++) done = meter.Add(i % 2 == 0 ? 0.04f : 0.08f);
            Assert.IsTrue(done);
            Assert.AreEqual(9 / 0.52f, meter.Fps, 0.1f);
            Assert.AreEqual(520f / 9f, meter.Milliseconds, 0.1f);
            Assert.IsFalse(meter.Add(0f), "a frame of no time (a paused game) is not counted");
        }
    }
}
