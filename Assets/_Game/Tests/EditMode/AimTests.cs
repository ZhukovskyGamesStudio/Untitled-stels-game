using NUnit.Framework;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>What the crosshair is on: E and its icon work on what the player looks at, not on whatever is nearest.</summary>
    public class AimTests
    {
        static readonly Aim Ahead = new(new Vector3(0f, 1.6f, 0f), Vector3.forward);

        [Test]
        public void LookingAtAThingCountsStandingNextToItDoesNot()
        {
            Assert.IsTrue(Ahead.At(new Vector3(0.2f, 1.5f, 2f), 0.45f), "under the crosshair");
            Assert.IsFalse(Ahead.At(new Vector3(1.2f, 1.0f, 0.5f), 0.45f), "beside the player, out of the view's middle");
            Assert.IsFalse(Ahead.At(new Vector3(0f, 1.6f, -1.5f), 0.45f), "behind");
        }

        [Test]
        public void ATallThingCountsAnywhereAlongItsBody()
        {
            // a door from 0.2 m to 2.1 m: looking at its foot or its top is still looking at it
            var down = new Aim(new Vector3(0f, 1.6f, 0f), new Vector3(0f, -1.1f, 1.5f));
            var up = new Aim(new Vector3(0f, 1.6f, 0f), new Vector3(0f, 0.5f, 1.5f));
            var foot = new Vector3(0f, 0.2f, 1.5f);
            var top = new Vector3(0f, 2.1f, 1.5f);
            Assert.IsTrue(down.At(foot, top, 0.6f));
            Assert.IsTrue(up.At(foot, top, 0.6f));
            Assert.IsTrue(Ahead.At(foot, top, 0.6f));
            Assert.IsFalse(new Aim(new Vector3(0f, 1.6f, 0f), Vector3.right).At(foot, top, 0.6f), "looking away along the wall");
        }

        [Test]
        public void TheMissIsTheDistanceFromTheViewRay()
        {
            Assert.AreEqual(0.3f, Ahead.Miss(new Vector3(0.3f, 1.6f, 3f), new Vector3(0.3f, 1.6f, 3f)), 1e-4f);
            Assert.AreEqual(0.5f, Ahead.Miss(new Vector3(-0.5f, 0f, 2f), new Vector3(-0.5f, 3f, 2f)), 1e-4f, "a post crossing the ray's height");
            Assert.IsTrue(float.IsPositiveInfinity(Ahead.Miss(new Vector3(0f, 1.6f, -2f), new Vector3(0f, 1.6f, -2f))));
        }
    }
}
