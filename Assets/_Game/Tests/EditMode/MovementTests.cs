using NUnit.Framework;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Tests
{
    public class MovementTests
    {
        const float Dt = 1f / 60f;

        static (float timeTo90, Vector3 v) Simulate(float speed, float accel, float decel, float seconds)
        {
            var v = Vector3.zero;
            float t90 = -1f;
            for (float t = 0; t < seconds; t += Dt)
            {
                v = MovementMath.Accelerate(v, Vector3.forward, speed, accel, decel, Dt);
                if (t90 < 0 && v.magnitude >= speed * 0.9f) t90 = t + Dt;
            }
            return (t90, v);
        }

        [Test]
        public void ReachesWalkSpeedQuicklyButNotInstantly()
        {
            var (t90, v) = Simulate(3.6f, 45f, 60f, 1f);
            Assert.That(t90, Is.InRange(0.03f, 0.15f), "responsive: 90% speed in well under 0.15 s");
            Assert.AreEqual(3.6f, v.magnitude, 1e-3f);
        }

        [Test]
        public void StopsWithinAShortDistance()
        {
            var v = new Vector3(0, 0, 6.2f);
            float distance = 0;
            for (int i = 0; i < 120 && v.sqrMagnitude > 0; i++)
            {
                v = MovementMath.Accelerate(v, Vector3.zero, 0f, 45f, 60f, Dt);
                distance += v.magnitude * Dt;
            }
            Assert.AreEqual(0f, v.magnitude, 1e-4f);
            Assert.Less(distance, 0.45f, "sprint stop should not slide more than ~half a meter");
        }

        [Test]
        public void ReleasingSprintSlowsToWalkSpeed()
        {
            var v = new Vector3(0, 0, 6.2f);
            for (int i = 0; i < 30; i++) v = MovementMath.Accelerate(v, Vector3.forward, 3.6f, 45f, 60f, Dt);
            Assert.AreEqual(3.6f, v.magnitude, 1e-3f);
        }

        [Test]
        public void TurningKeepsSpeedBounded()
        {
            var v = new Vector3(0, 0, 3.6f);
            for (int i = 0; i < 60; i++)
            {
                v = MovementMath.Accelerate(v, Vector3.right, 3.6f, 45f, 60f, Dt);
                Assert.LessOrEqual(v.magnitude, 3.6f + 1e-3f);
            }
            Assert.Greater(Vector3.Dot(v.normalized, Vector3.right), 0.99f);
        }

        [Test]
        public void IgnoresVerticalComponent()
        {
            var v = MovementMath.Accelerate(new Vector3(0, 5, 0), Vector3.forward, 3f, 45f, 60f, Dt);
            Assert.AreEqual(0f, v.y);
        }

        [Test]
        public void JumpVelocityReachesHeight()
        {
            float v0 = MovementMath.JumpVelocity(1.1f, -9.81f);
            Assert.AreEqual(1.1f, v0 * v0 / (2f * 9.81f), 1e-3f);
        }

        [Test]
        public void CoyoteTimeAllowsLateJump()
        {
            Assert.IsTrue(MovementMath.CanJump(10.1f, 10f, 10.2f, 0.12f), "left the edge 0.1 s ago");
            Assert.IsFalse(MovementMath.CanJump(10.2f, 10f, 10.3f, 0.12f), "too late");
        }

        [Test]
        public void BufferedPressExpires()
        {
            Assert.IsTrue(MovementMath.CanJump(5f, 5f, 5.05f, 0.12f), "pressed shortly before landing");
            Assert.IsFalse(MovementMath.CanJump(5f, 5f, 4.9f, 0.12f), "press expired before landing");
        }
    }
}
