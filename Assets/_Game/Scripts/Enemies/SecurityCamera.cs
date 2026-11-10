using Stealth.Configs;
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
            var vision = Tuning.Perception.Vision;
            IIntruder seen = null;
            float best = 0f;
            foreach (var p in PlayerController.All)
            {
                IIntruder target = p;
                if (target.IsDown) continue;
                if (!Perception.Look(eye, Pivot.forward, target, Profile.ViewDistance, Profile.ViewAngle, vision, out var g)) continue;
                // Fully visible and moving at mid range in a lit room: about DetectTime / 0.7 seconds to detect.
                float rate = SensorMath.VisionFactor(g, -1f, vision) / Mathf.Max(Profile.DetectTime, 0.05f);
                if (rate > best) { seen = target; best = rate; }
            }
            if (seen == null) { Forget(); return; }
            Notice(seen, best);
        }
    }
}
