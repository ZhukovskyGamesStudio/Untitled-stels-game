using Stealth.Configs;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Wall PIR sensor: notices fast movement in its cone — the faster, closer and more of the body in view, the quicker.
    /// Sneaking slowly (crouched) or freezing beats it.
    /// </summary>
    public class MotionSensor : SecurityDevice
    {
        [Tooltip("Tuning (MotionSensorProfile) — a separate asset next to the prefab.")]
        public MotionSensorProfile Profile;
        protected override DeviceProfile Device => Profile;

        protected override Color IdleColor => new(1f, 0.55f, 0.85f);

        public static bool Triggers(float playerSpeed, float threshold) => playerSpeed > threshold;

        protected override void TickServer()
        {
            var eye = transform.position + transform.forward * 0.1f;
            var perception = Tuning.Perception;
            IIntruder seen = null;
            float best = 0f;
            foreach (var p in PlayerController.All)
            {
                if (p.IsDown || !Triggers(p.GroundSpeed, Profile.SpeedThreshold)) continue;
                if (!Perception.Look(eye, transform.forward, p, Profile.Range, Profile.ViewAngle, perception.Vision, out var g)) continue;
                float rate = SensorMath.MotionRate(Profile.DetectionRate, p.GroundSpeed, g.Distance, Profile.Range, g.Exposure, perception.Vibration, perception.Vision);
                if (rate > best) { seen = p; best = rate; }
            }
            if (seen == null) { Forget(); return; }
            Notice(seen, best);
        }
    }
}
