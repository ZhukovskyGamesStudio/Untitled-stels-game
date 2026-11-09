using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Wall PIR sensor: notices fast movement in its cone. Sneaking slowly (crouched) or freezing beats it.</summary>
    public class MotionSensor : SecurityDevice
    {
        public float Range = 9f;
        public float ViewAngle = 100f;
        public float SpeedThreshold = 2.2f; // crouch-walk is 1.8, walking 3.6

        protected override Color IdleColor => new(1f, 0.55f, 0.85f);

        public static bool Triggers(float playerSpeed, float threshold) => playerSpeed > threshold;

        protected override void TickServer()
        {
            var eye = transform.position + transform.forward * 0.1f;
            foreach (var p in PlayerController.All)
            {
                if (p.IsDown || !Triggers(p.GroundSpeed, SpeedThreshold)) continue;
                var target = p.Position + Vector3.up;
                if (!Perception.InCone(eye, transform.forward, target, Range, ViewAngle) || !Perception.LineOfSight(eye, p, 0.6f)) continue;
                Notice(p, 3f);
                return;
            }
            Decay(0.5f);
        }
    }
}
