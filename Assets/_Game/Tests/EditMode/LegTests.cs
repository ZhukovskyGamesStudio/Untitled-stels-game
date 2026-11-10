using System.Linq;
using NUnit.Framework;
using Stealth.Enemies;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Walking legs: the knee solver, stepping, and the rig baked into the robot prefabs.</summary>
    public class LegTests
    {
        [Test]
        public void TheKneeKeepsBothBonesAndBendsTheRightWay()
        {
            var hip = new Vector3(0f, 0.9f, 0f);
            var foot = new Vector3(0f, 0.1f, 0.2f);
            var knee = LegMath.Knee(hip, foot, 0.45f, 0.45f, Vector3.forward);
            Assert.AreEqual(0.45f, Vector3.Distance(hip, knee), 1e-3f, "thigh length");
            Assert.AreEqual(0.45f, Vector3.Distance(knee, foot), 1e-3f, "shin length");
            Assert.Greater(knee.z, (hip.z + foot.z) / 2f, "a guard's knee bends forward");

            var back = LegMath.Knee(hip, foot, 0.45f, 0.45f, Vector3.back);
            Assert.Less(back.z, (hip.z + foot.z) / 2f);
        }

        [Test]
        public void AFootOutOfReachStraightensTheLeg()
        {
            var hip = Vector3.zero;
            var knee = LegMath.Knee(hip, new Vector3(0f, -5f, 0f), 0.45f, 0.45f, Vector3.forward);
            Assert.AreEqual(0.45f, knee.magnitude, 1e-3f);
            Assert.Less(knee.y, -0.44f, "pointing at the foot");
        }

        [Test]
        public void FeetStepInAnArc()
        {
            Assert.IsFalse(LegMath.ShouldStep(Vector3.zero, new Vector3(0.2f, 0f, 0f), 0.3f));
            Assert.IsTrue(LegMath.ShouldStep(Vector3.zero, new Vector3(0.4f, 0f, 0f), 0.3f));
            var mid = LegMath.Swing(Vector3.zero, Vector3.forward, 0.5f, Vector3.up, 0.2f);
            Assert.AreEqual(0.2f, mid.y, 1e-4f, "lifted in the middle of the step");
            Assert.Less(Vector3.Distance(Vector3.forward, LegMath.Swing(Vector3.zero, Vector3.forward, 1f, Vector3.up, 0.2f)), 1e-4f, "lands on its spot");
        }

        [TestCase("RobotGuard", 2)]
        [TestCase("RobotHound", 4)]
        [TestCase("RobotSpider", 8)]
        public void WalkersHaveALegRig(string prefab, int count)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Robots/{prefab}.prefab");
            var legs = go.GetComponent<ProceduralLegs>();
            Assert.IsNotNull(legs, $"{prefab} walks on its legs");
            Assert.AreEqual(count, legs.Legs.Length);
            foreach (var leg in legs.Legs)
            {
                Assert.IsNotNull(leg.Hip);
                Assert.IsNotNull(leg.Knee);
                Assert.AreEqual(leg.Hip, leg.Knee.parent, "the shin hangs on the thigh");
                Assert.Greater(leg.Upper, 0.05f);
                Assert.Greater(leg.Lower, 0.05f);
            }
            Assert.AreEqual(count / 2, legs.Legs.Count(l => l.Group == 0), "half the legs step at a time");
        }
    }
}
