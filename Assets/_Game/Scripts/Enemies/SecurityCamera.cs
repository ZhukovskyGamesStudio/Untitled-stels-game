using Stealth.Configs;
using Stealth.Game;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Wall camera sweeping left-right. Rotation is derived from the shared server time, so it is identical
    /// on every peer without syncing transforms. Spotting a player raises an alarm: nearby robots come running.
    /// A player at a camera terminal can take it over and turn it by hand (<see cref="SteerServer"/>, the yaw is
    /// replicated as <see cref="Detector.AimDegrees"/>): while steered it raises nothing, and no camera notices whoever
    /// sits at a terminal.
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

        float m_Yaw, m_Droop;
        bool m_Posed;

        /// <summary>Degrees a second the head may turn to catch up with the sweep (faster than the sweep itself).</summary>
        const float CatchUpSpeed = 90f;
        /// <summary>A hacked camera hangs its head by this much, degrees.</summary>
        const float HackedDroop = 28f;

        /// <summary>How far a player may turn a camera from its middle, degrees.</summary>
        public const float SteerLimit = 80f;
        /// <summary>The head's yaw now, degrees from the middle.</summary>
        public float CurrentYaw => m_Yaw;

        float m_PreviewYaw, m_PreviewUntil;
        object m_SteeredBy;
        float m_SteerYaw, m_SteerAt;

        /// <summary>Owner at the terminal: turn the head right away (the server's copy follows a moment later).</summary>
        public void PreviewAim(float yaw)
        {
            m_PreviewYaw = Mathf.Clamp(yaw, -SteerLimit, SteerLimit);
            m_PreviewUntil = Time.time + 0.4f;
        }

        /// <summary>Server: a player at a terminal turns the camera (refreshed while they stay; released after a pause).</summary>
        public void SteerServer(object by, float yaw)
        {
            m_SteeredBy = by;
            m_SteerYaw = Mathf.Clamp(yaw, -SteerLimit, SteerLimit);
            m_SteerAt = Time.time;
        }

        public void ReleaseServer(object by)
        {
            if (m_SteeredBy == by) m_SteeredBy = null;
        }

        protected override void TickLocal()
        {
            float t = Session.ServerTimeF + m_Phase;
            float sweep = SweepYaw(t, Profile.SweepAngle, Profile.SweepPeriod);
            bool preview = Time.time < m_PreviewUntil;
            float goal = preview ? m_PreviewYaw : IsSteered ? AimDegrees : sweep;
            // Hacked: the head stops where it was and hangs; back on, it eases into the sweep again.
            if (!m_Posed) { m_Yaw = goal; m_Posed = true; }
            else if (preview) m_Yaw = goal;
            else if (!IsHacked) m_Yaw = Mathf.MoveTowards(m_Yaw, goal, (IsSteered ? 4f : 1f) * CatchUpSpeed * Time.deltaTime);
            m_Droop = Mathf.MoveTowards(m_Droop, IsHacked ? HackedDroop : 0f, 60f * Time.deltaTime);
            Pivot.localRotation = Quaternion.Euler(Profile.Tilt + m_Droop, m_Yaw, 0f);
        }

        protected override void OnStatusChanged()
        {
            if (Spot == null) return;
            Spot.color = Status.Current;
            Spot.intensity = Alert == 2 && !IsHacked ? 6f : IsHacked ? 0f : 3f;
        }

        protected override void TickServer()
        {
            if (m_SteeredBy != null && Time.time - m_SteerAt > 1.5f) m_SteeredBy = null; // the player left the terminal
            SetAim(m_SteeredBy != null ? (short)Mathf.RoundToInt(m_SteerYaw * 10f) : NotSteered);
            if (m_SteeredBy != null) { Forget(); return; } // in a player's hands: it reports nothing
            var eye = Point != null ? Point.position : Pivot.position + Pivot.forward * 0.5f; // the lens
            var vision = Tuning.Perception.Vision;
            IIntruder seen = null;
            float best = 0f;
            foreach (var p in PlayerController.All)
            {
                IIntruder target = p;
                if (target.IsDown || p.AtTerminal >= 0) continue; // the one watching the cameras is not on them
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
