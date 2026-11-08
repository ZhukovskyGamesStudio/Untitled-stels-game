using NUnit.Framework;
using Stealth.UI;
using UnityEngine;

namespace Stealth.Tests
{
    public class UiMathTests
    {
        [Test]
        public void ChoosesHighestPriorityPrompt()
        {
            var p = UiMath.Choose(new[]
            {
                new Prompt(10, "Tab", "item"),
                new Prompt(50, "E", "revive"),
                new Prompt(30, "F", "hatch"),
            });
            Assert.AreEqual("revive", p.Text);
        }

        [Test]
        public void IgnoresEmptyPromptsAndReturnsNothingWhenEmpty()
        {
            Assert.IsFalse(UiMath.Choose(new Prompt[0]).IsValid);
            Assert.AreEqual("a", UiMath.Choose(new[] { new Prompt(99, "", ""), new Prompt(1, "", "a") }).Text);
        }

        [Test]
        public void ScreenAngleQuadrants()
        {
            var cam = Vector3.zero;
            Assert.AreEqual(0f, UiMath.ScreenAngle(cam, Vector3.forward, new Vector3(0, 5, 10)), 0.01f);
            Assert.AreEqual(90f, UiMath.ScreenAngle(cam, Vector3.forward, Vector3.right), 0.01f);
            Assert.AreEqual(-90f, UiMath.ScreenAngle(cam, Vector3.forward, Vector3.left), 0.01f);
            Assert.AreEqual(180f, Mathf.Abs(UiMath.ScreenAngle(cam, Vector3.forward, Vector3.back)), 0.01f);
        }

        [Test]
        public void EdgeMarkerKeepsVisiblePointsAndClampsOthers()
        {
            var inside = UiMath.EdgeMarker(new Vector3(0.25f, 0.75f, 5f), 1000, 500, 40, out bool c1);
            Assert.IsFalse(c1);
            Assert.AreEqual(new Vector2(250, 125), inside);

            var right = UiMath.EdgeMarker(new Vector3(3f, 0.5f, 5f), 1000, 500, 40, out bool c2);
            Assert.IsTrue(c2);
            Assert.AreEqual(960f, right.x, 0.01f);
            Assert.AreEqual(250f, right.y, 0.01f);

            var behind = UiMath.EdgeMarker(new Vector3(0.6f, 0.5f, -2f), 1000, 500, 40, out bool c3);
            Assert.IsTrue(c3, "points behind the camera always go to the edge");
            Assert.Less(behind.x, 500f, "a point behind-right shows on the left side");
        }

        [Test]
        public void NoiseBarsMatchMovementModes()
        {
            Assert.AreEqual(0, UiMath.NoiseBars(0f));
            Assert.AreEqual(1, UiMath.NoiseBars(2f));   // crouch
            Assert.AreEqual(2, UiMath.NoiseBars(6f));   // walk
            Assert.AreEqual(3, UiMath.NoiseBars(13f));  // sprint
            Assert.AreEqual(3, UiMath.NoiseBars(9f));   // laughing in gas
        }

        [Test]
        public void MessageFadesInHoldsAndFadesOut()
        {
            Assert.AreEqual(0f, UiMath.MessageAlpha(-1f, 3f));
            Assert.AreEqual(0.5f, UiMath.MessageAlpha(0.125f, 3f), 1e-4f);
            Assert.AreEqual(1f, UiMath.MessageAlpha(1.5f, 3f));
            Assert.AreEqual(0.5f, UiMath.MessageAlpha(2.875f, 3f), 1e-4f);
            Assert.AreEqual(0f, UiMath.MessageAlpha(3.1f, 3f));
        }

        [Test]
        public void ScaleIsClamped()
        {
            Assert.AreEqual(1f, UiMath.Scale(720));
            Assert.AreEqual(1.5f, UiMath.Scale(1080));
            Assert.AreEqual(0.75f, UiMath.Scale(200));
            Assert.AreEqual(3f, UiMath.Scale(5000));
        }
    }
}
