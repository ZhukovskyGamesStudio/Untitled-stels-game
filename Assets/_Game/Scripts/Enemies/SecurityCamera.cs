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
        [Header("Camera")]
        public Transform Pivot;
        public Light Spot;
        public float Tilt = 28f;
        public float SweepAngle = 80f;
        public float SweepPeriod = 8f;
        public float ViewDistance = 14f;
        public float ViewAngle = 50f;
        [Tooltip("Seconds to detect a standing player at mid range in a lit room.")]
        public float DetectTime = 1.1f;

        static readonly Color s_Idle = new(0.75f, 0.9f, 1f);

        float m_Phase;

        protected override Color IdleColor => s_Idle;

        protected override void OnDeviceSpawned()
        {
            m_Phase = NetworkObjectId % 11 * 0.73f;
            if (Spot == null) return;
            Spot.range = ViewDistance;
            Spot.spotAngle = ViewAngle;
            Spot.innerSpotAngle = ViewAngle * 0.7f;
        }

        /// <summary>Yaw of the sweep at a moment of the shared clock (pure).</summary>
        public static float SweepYaw(float time, float sweepAngle, float period) => Mathf.Sin(time * Mathf.PI * 2f / period) * sweepAngle / 2f;

        protected override void TickLocal()
        {
            float t = NetworkManager.ServerTime.TimeAsFloat + m_Phase;
            Pivot.localRotation = Quaternion.Euler(Tilt, SweepYaw(t, SweepAngle, SweepPeriod), 0f);
        }

        protected override void OnStatusChanged()
        {
            if (Spot == null) return;
            Spot.color = Status.Current;
            Spot.intensity = Alert.Value == 2 && !IsHacked ? 6f : IsHacked ? 0f : 3f;
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
                if (d < bestDist && Perception.InCone(eye, Pivot.forward, aim, ViewDistance, ViewAngle) && Perception.LineOfSight(eye, target, 0.7f))
                {
                    seen = target;
                    bestDist = d;
                }
            }
            if (seen == null) { Decay(0.5f); return; }
            Notice(seen, SensorMath.CameraRate(DetectTime, bestDist, ViewDistance, seen.IsCrouching, Perception.LightFactor(seen.Position)));
        }
    }
}
