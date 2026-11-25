using NUnit.Framework;
using Stealth.Player;

namespace Stealth.Tests
{
    /// <summary>
    /// A look round with the body held: on the ladder the body keeps facing the rungs and only the head turns, within its
    /// limit; in a seat the head turns and the body a little; never round the back, from whatever the body brought.
    /// </summary>
    public class LookLimitsTests
    {
        [Test]
        public void OnTheLadderOnlyTheHeadTurnsAndNotRoundTheBack()
        {
            var s = new LookSettings { LadderHead = 100f };
            float look = 0f;
            for (int i = 0; i < 50; i++) look = LookLimits.Turn(look, 10f, s.LadderHead, 0f); // the mouse dragged on and on
            Assert.AreEqual(100f, look, 1e-4f, "the head stops at its limit — no 360° on the ladder");
            Assert.AreEqual(0f, LookLimits.Body(look, s.LadderHead, 0f), "the body keeps facing the rungs");
            look = LookLimits.Turn(look, -350f, s.LadderHead, 0f);
            Assert.AreEqual(-100f, look, 1e-4f, "and the other way");
        }

        [Test]
        public void InASeatTheHeadTurnsAndTheBodyALittle()
        {
            var s = new LookSettings { SeatHead = 95f, SeatBody = 15f };
            float look = LookLimits.Turn(0f, 500f, s.SeatHead, s.SeatBody);
            Assert.AreEqual(110f, look, 1e-4f, "head and body together");
            float body = LookLimits.Body(look, s.SeatHead, s.SeatBody);
            Assert.AreEqual(15f, body, 1e-4f, "the body turns on the seat only a little");
            Assert.AreEqual(95f, look - body, 1e-4f, "the rest is the head's");
            Assert.AreEqual(-6f, LookLimits.Body(LookLimits.Turn(0f, -44f, s.SeatHead, s.SeatBody), s.SeatHead, s.SeatBody), 1e-4f, "a share of every look");
        }

        [Test]
        public void TakingHoldTheLookTheBodyBroughtIsHeldWithin()
        {
            Assert.AreEqual(30f, LookLimits.Take(120f, 90f, 100f, 0f), 1e-4f, "looking a little aside: kept");
            Assert.AreEqual(100f, LookLimits.Take(260f, 90f, 100f, 0f), 1e-4f, "facing away from the rungs (170° round): turned to the limit");
            Assert.AreEqual(-100f, LookLimits.Take(-30f, 90f, 100f, 0f), 1e-4f, "round the other way (−120°)");
        }
    }
}
