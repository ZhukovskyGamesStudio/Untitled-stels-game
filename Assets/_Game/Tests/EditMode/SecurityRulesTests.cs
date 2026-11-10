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
            Assert.AreEqual(SecurityPalette.Alarm, SecurityPalette.ForState(RobotState.Alerted, idle));
            Assert.AreNotEqual(SecurityPalette.Hacked, SecurityPalette.Calm, "a hacked robot looks dead");
        }

        static Glimpse Look(float distance = 5f, float exposure = 1f, float offCentre = 0f, bool crouching = false, bool moving = true, float light = 1f) =>
            new() { Distance = distance, Range = 13f, Exposure = exposure, OffCentre = offCentre, Crouching = crouching, Moving = moving, Light = light };

        /// <summary>Seconds a guard (DetectionSpeed 0.6) needs to fill its suspicion with this look.</summary>
        static float SecondsToDetect(in Glimpse g) => 1f / (0.6f * SensorMath.VisionFactor(g));

        [Test]
        public void AFullyVisibleWalkingPlayerTakesSecondsToNotice()
        {
            float mid = SecondsToDetect(Look(5f));
            Assert.That(mid, Is.InRange(1.4f, 2.6f), "5 m away, in plain view: a couple of seconds, not instantly");
            Assert.Less(SecondsToDetect(Look(2f)), mid, "closer is faster");
            Assert.Greater(SecondsToDetect(Look(11f)), 2.5f * mid, "far away is much slower");
        }

        [Test]
        public void APartlyHiddenBodyIsNoticedMuchSlower()
        {
            float full = SensorMath.VisionFactor(Look(5f));
            float half = SensorMath.VisionFactor(Look(5f, exposure: 0.5f));
            float sliver = SensorMath.VisionFactor(Look(5f, exposure: 0.15f));
            Assert.Less(half, full * 0.4f, "half of the body: less than 40% of the speed");
            Assert.Less(sliver, full * 0.06f, "15% of the body visible from 5 m: very slow");
            Assert.AreEqual(0f, SensorMath.VisionFactor(Look(5f, exposure: 0.1f)), "a sliver below the minimum is not noticed");
            Assert.Greater(SecondsToDetect(Look(5f, exposure: 0.15f)), 25f);
        }

        [Test]
        public void CrouchingStandingStillTheDarkAndTheEdgeOfViewAllSlowItDown()
        {
            float baseRate = SensorMath.VisionFactor(Look());
            Assert.Less(SensorMath.VisionFactor(Look(crouching: true)), baseRate);
            Assert.Less(SensorMath.VisionFactor(Look(moving: false)), baseRate);
            Assert.AreEqual(baseRate * 0.5f, SensorMath.VisionFactor(Look(light: 0.5f)), 1e-4f);
            Assert.AreEqual(baseRate, SensorMath.VisionFactor(Look(offCentre: 0.3f)), 1e-4f, "the middle of the view is seen at full speed");
            Assert.Less(SensorMath.VisionFactor(Look(offCentre: 1f)), baseRate * 0.5f, "the edge of the view is much slower");
            float behind = SensorMath.VisionFactor(Look(2f, offCentre: 1.7f, moving: false));
            Assert.Less(behind * 0.6f * 2.5f, 0.5f, "standing close behind a guard for a 2.5 s hack fills less than half of its suspicion");
            Assert.Less(SensorMath.VisionFactor(Look(crouching: true, moving: false, light: 0.5f)), baseRate * 0.2f, "all together: sneaking works");
        }

        [Test]
        public void BodySamplesCoverHeadToFeetAndBothShoulders()
        {
            var points = new Vector3[Perception.BodySampleCount];
            var feet = new Vector3(3, 0, 5);
            Perception.BodySamples(feet, 1.8f, new Vector3(3, 1.6f, 0), points);
            float top = float.MinValue, bottom = float.MaxValue;
            foreach (var p in points)
            {
                top = Mathf.Max(top, p.y);
                bottom = Mathf.Min(bottom, p.y);
                Assert.Less(Vector2.Distance(new Vector2(p.x, p.z), new Vector2(feet.x, feet.z)), 0.3f, "inside the body");
            }
            Assert.Greater(top, 1.6f);
            Assert.Less(bottom, 0.2f);
            Assert.AreEqual(0.44f, Mathf.Abs(points[2].x - points[3].x), 1e-3f, "shoulders lie across the line of view");
        }

        [Test]
        public void WallsMuffleSoundMoreThanDoorsAndFurniture()
        {
            var h = new HearingSettings();
            float open = SensorMath.Loudness(4f, 9.6f, 1f);
            float wall = SensorMath.Loudness(4f, 9.6f, SensorMath.Transmission(new[] { SoundMaterial.Wall }, h));
            float door = SensorMath.Loudness(4f, 9.6f, SensorMath.Transmission(new[] { SoundMaterial.Door }, h));
            float desk = SensorMath.Loudness(4f, 9.6f, SensorMath.Transmission(new[] { SoundMaterial.Furniture }, h));
            float twoWalls = SensorMath.Loudness(4f, 9.6f, SensorMath.Transmission(new[] { SoundMaterial.Wall, SoundMaterial.Wall }, h));
            Assert.Greater(open, desk);
            Assert.Greater(desk, door);
            Assert.Greater(door, wall);
            Assert.Greater(wall, twoWalls);
            Assert.AreEqual(0f, SensorMath.Loudness(10f, 9.6f, 1f), "out of earshot");
            Assert.Greater(SensorMath.Loudness(2f, 9.6f, 1f), open, "closer is louder");
        }

        [Test]
        public void QuietNoisesAddLittleSuspicionAndBarelyAudibleOnesNone()
        {
            var h = new HearingSettings();
            Assert.AreEqual(0f, SensorMath.NoiseSuspicion(h.MinLoudness * 0.9f, h));
            Assert.Less(SensorMath.NoiseSuspicion(0.2f, h), SensorMath.NoiseSuspicion(0.8f, h));
            // A listener (×1.6) hears a walking step (radius 6) through one wall from 5 m: it alone is not enough to check.
            float step = SensorMath.NoiseSuspicion(SensorMath.Loudness(5f, 9.6f, h.Wall), h);
            Assert.Less(step, 0.15f);
        }

        [Test]
        public void SeismicAndMotionSensorsFeelFastMovementStronger()
        {
            Assert.Greater(SensorMath.MotionRate(3f, 6f, 4f, 9f, 1f), SensorMath.MotionRate(3f, 3f, 4f, 9f, 1f), "faster");
            Assert.Greater(SensorMath.MotionRate(3f, 4f, 2f, 9f, 1f), SensorMath.MotionRate(3f, 4f, 8f, 9f, 1f), "closer");
            Assert.Greater(SensorMath.MotionRate(3f, 4f, 4f, 9f, 1f), SensorMath.MotionRate(3f, 4f, 4f, 9f, 0.4f), "more of the body in view");
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
