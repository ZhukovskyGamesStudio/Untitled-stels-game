using Stealth.Game;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Wall camera sweeping left-right. Rotation is derived from the shared server time, so it is identical
    /// on every peer without syncing transforms. Spotting a player raises an alarm: nearby robots come running.
    /// </summary>
    public class SecurityCamera : SecurityDevice
    {
        [Tooltip("Tuning (CameraProfile) — a separate asset next to the prefab.")]
        public CameraProfile Profile;
        protected override DeviceProfile Device => Profile;
        [Header("Camera")]
        public Transform Pivot;
        public Light Spot;

        static readonly Color s_Idle = new(0.75f, 0.9f, 1f);

        float m_Phase;

        protected override Color IdleColor => s_Idle;

        protected override void OnDeviceSpawned()
        {
            m_Phase = NetId % 11 * 0.73f;
            if (Spot == null) return;
            Spot.range = Profile.ViewDistance;
            Spot.spotAngle = Profile.ViewAngle;
            Spot.innerSpotAngle = Profile.ViewAngle * 0.7f;
        }

        /// <summary>Yaw of the sweep at a moment of the shared clock (pure).</summary>
        public static float SweepYaw(float time, float sweepAngle, float period) => Mathf.Sin(time * Mathf.PI * 2f / period) * sweepAngle / 2f;

        protected override void TickLocal()
        {
            float t = Session.ServerTimeF + m_Phase;
            Pivot.localRotation = Quaternion.Euler(Profile.Tilt, SweepYaw(t, Profile.SweepAngle, Profile.SweepPeriod), 0f);
        }

        protected override void OnStatusChanged()
        {
            if (Spot == null) return;
            Spot.color = Status.Current;
            Spot.intensity = Alert == 2 && !IsHacked ? 6f : IsHacked ? 0f : 3f;
        }

        protected override void TickServer()
        {
            var eye = Pivot.position + Pivot.forward * 0.3f;
            IIntruder seen = null;
            float bestDist = float.MaxValue;
            foreach (var p in PlayerController.All)
            {
                IIntruder target = p;
                if (target.IsDown) continue;
                var aim = Perception.AimPoint(target, 0.7f);
                float d = Vector3.Distance(eye, aim);
                if (d < bestDist && Perception.InCone(eye, Pivot.forward, aim, Profile.ViewDistance, Profile.ViewAngle) && Perception.LineOfSight(eye, target, 0.7f))
                {
                    seen = target;
                    bestDist = d;
                }
            }
            if (seen == null) { Forget(); return; }
            Notice(seen, SensorMath.CameraRate(Profile.DetectTime, bestDist, Profile.ViewDistance, seen.IsCrouching, Perception.LightFactor(seen.Position)));
        }
    }
}
