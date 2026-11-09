using NUnit.Framework;
using Stealth.Enemies;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Pure rules shared by devices and robots: suspicion meter, status colours, sensor rates, lasers.</summary>
    public class SecurityRulesTests
    {
        [Test]
        public void MeterDetectsAtOneAndThenStaysAlarmed()
        {
            var m = new SuspicionMeter { AlarmCooldown = 6f };
            Assert.IsFalse(m.Notice(2f, 0.4f, 0f));
            Assert.AreEqual(1, m.AlertLevel(0f), "noticing");
            Assert.IsTrue(m.Notice(2f, 0.2f, 0.2f), "detects at 1");
            Assert.AreEqual(2, m.AlertLevel(0.2f));
            Assert.AreEqual(1f, m.Awareness(0.2f));
            Assert.IsFalse(m.Notice(2f, 0.2f, 1f), "no second detection during the cooldown");
            Assert.AreEqual(SuspicionMeter.Cap, m.Value, 1e-4f, "capped");
            Assert.IsTrue(m.Notice(2f, 0.1f, 6.3f), "detects again after the cooldown");
        }

        [Test]
        public void MeterDecaysAndClears()
        {
            var m = new SuspicionMeter();
            m.Notice(1f, 0.5f, 0f);
            m.Decay(1f, 0.4f);
            Assert.AreEqual(0.1f, m.Value, 1e-4f);
            Assert.AreEqual(0, m.AlertLevel(0f), "below the noticing threshold");
            m.Decay(1f, 5f);
            Assert.AreEqual(0f, m.Value);
            m.Trip(1f);
            m.Clear();
            Assert.AreEqual(0, m.AlertLevel(1.1f), "hacking clears an alarm too");
        }

        [Test]
        public void TripIsInstantButRespectsTheCooldown()
        {
            var m = new SuspicionMeter { AlarmCooldown = 6f };
            Assert.IsTrue(m.Trip(0f));
            Assert.IsFalse(m.Trip(3f));
            Assert.IsTrue(m.Trip(7f));
        }

        [Test]
        public void StatusColoursFollowTheAlertLevel()
        {
            var idle = new Color(0.1f, 0.2f, 0.3f);
            Assert.AreEqual(idle, SecurityPalette.ForAlert(0, idle));
            Assert.AreEqual(SecurityPalette.Suspicious, SecurityPalette.ForAlert(1, idle));
            Assert.AreEqual(SecurityPalette.Alarm, SecurityPalette.ForAlert(2, idle));
            Assert.AreEqual(idle, SecurityPalette.ForState(RobotState.Patrol, idle));
            Assert.AreEqual(SecurityPalette.Investigating, SecurityPalette.ForState(RobotState.Investigate, idle));
            Assert.AreEqual(SecurityPalette.Alarm, SecurityPalette.ForState(RobotState.Chase, idle));
            Assert.AreNotEqual(SecurityPalette.Hacked, SecurityPalette.Calm, "a hacked robot looks dead");
        }

        [Test]
        public void VisionIsFasterUpCloseSlowerCrouchedAndInTheDark()
        {
            float near = SensorMath.VisionRate(1.6f, 2f, 13f, false, 1f);
            float far = SensorMath.VisionRate(1.6f, 12f, 13f, false, 1f);
            Assert.Greater(near, far);
            Assert.Less(SensorMath.VisionRate(1.6f, 6f, 13f, true, 1f), SensorMath.VisionRate(1.6f, 6f, 13f, false, 1f));
            Assert.AreEqual(SensorMath.VisionRate(1.6f, 6f, 13f, false, 1f) * 0.5f,
                SensorMath.VisionRate(1.6f, 6f, 13f, false, LightMath.VisionFactor(true)), 1e-4f);
        }

        [Test]
        public void CameraNeedsAboutItsDetectTimeAtMidRange()
        {
            float rate = SensorMath.CameraRate(1.1f, 7f, 14f, false, 1f);
            Assert.AreEqual(1.1f / 1.25f, 1f / rate, 0.01f, "mid range is 1.25x faster than the far edge");
            Assert.Less(SensorMath.CameraRate(1.1f, 7f, 14f, true, 1f), rate, "crouching helps");
        }

        [Test]
        public void SeismicIsStrongerCloseBy()
        {
            Assert.Greater(SensorMath.SeismicRate(1.1f, 1f, 10f), SensorMath.SeismicRate(1.1f, 9f, 10f));
            Assert.AreEqual(1.1f * 0.5f, SensorMath.SeismicRate(1.1f, 10f, 10f), 1e-4f);
        }

        [Test]
        public void SoundThroughASlabCarriesHalfAsFar()
        {
            var listener = new Vector3(5, 0.1f, 5);
            Assert.AreEqual(4f, SensorMath.HearingDistance(new Vector3(9, 0.1f, 5), listener), 1e-4f);
            var above = new Vector3(5, LevelMetrics.FloorHeight + 0.1f, 5);
            Assert.AreEqual(2f * LevelMetrics.FloorHeight, SensorMath.HearingDistance(above, listener), 1e-3f);
        }

        [Test]
        public void LaserBeamsTouchOnlyBodiesThatCrossThem()
        {
            var low = new[] { 1.25f };
            Assert.IsTrue(LaserGate.Touches(low, 0f, 1.8f), "standing");
            Assert.IsFalse(LaserGate.Touches(low, 0f, 1.1f), "crouching under");
            Assert.IsFalse(LaserGate.Touches(new[] { 0.3f }, 0.5f, 1.8f), "jumping over");
        }

        [Test]
        public void BlinkingBeamsWarnBeforeSwitchingOn()
        {
            Assert.IsTrue(LaserGate.BlinkOn(1f, 2.4f));
            Assert.IsFalse(LaserGate.BlinkOn(3f, 2.4f));
            bool warned = false;
            for (float t = 3.6f; t < 4f; t += 0.01f) warned |= LaserGate.BlinkWarning(t, 2.4f, 1.6f);
            Assert.IsTrue(warned, "flickers in the last moment of the gap");
            Assert.IsFalse(LaserGate.BlinkWarning(2.6f, 2.4f, 1.6f), "but not right after switching off");
        }

        [Test]
        public void CameraSweepStaysWithinItsAngle()
        {
            for (float t = 0; t < 20f; t += 0.1f)
                Assert.LessOrEqual(Mathf.Abs(SecurityCamera.SweepYaw(t, 80f, 8f)), 40.001f);
            Assert.AreEqual(40f, SecurityCamera.SweepYaw(2f, 80f, 8f), 1e-3f);
        }

        [Test]
        public void RadioRippleExpands()
        {
            Assert.AreEqual(0.3f, RadioRipple.RadiusAt(0f, 0.6f, 0.3f, 4f), 1e-4f);
            Assert.AreEqual(4f, RadioRipple.RadiusAt(0.6f, 0.6f, 0.3f, 4f), 1e-4f);
        }

        [Test]
        public void EveryKindHasANameAndADescription()
        {
            foreach (RobotKind k in System.Enum.GetValues(typeof(RobotKind)))
            {
                Assert.AreNotEqual("Робот", RobotInfo.Name(k), $"{k} has a name");
                Assert.IsNotEmpty(RobotInfo.Senses(k), $"{k} has a description");
            }
        }
    }
}
