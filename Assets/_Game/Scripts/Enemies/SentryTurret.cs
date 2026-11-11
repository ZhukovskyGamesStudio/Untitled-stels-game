using Stealth.Configs;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Stationary turret: sweeps its head, locks on with a red laser sight (a clear warning) and fires if the
    /// player is still in sight when the lock completes.
    /// </summary>
    public class SentryTurret : SecurityDevice
    {
        [Tooltip("Tuning (TurretProfile) — a separate asset next to the prefab.")]
        public TurretProfile Profile;
        protected override DeviceProfile Device => Profile;
        public Transform Head;
        public LineRenderer Sight;

        /// <summary>Client id being aimed at (for the laser sight on every peer), Nobody = sweeping.</summary>
        public ulong AimTarget => TargetClient;

        float m_LockStart = -1f;
        float m_NextShot;
        float m_Yaw;

        float m_BaseWidth = 0.025f;

        protected override void OnDeviceSpawned()
        {
            if (Sight != null) m_BaseWidth = Sight.widthMultiplier;
        }

        Vector3 EyePos => transform.position + Vector3.up * 1.35f;

        protected override void TickLocal()
        {
            // Hacked: the head stops where it was, no sight beam.
            if (IsHacked)
            {
                if (Sight != null) Sight.enabled = false;
                return;
            }
            // Head: aim at the target if locked, otherwise sweep (shared server time keeps peers in sync).
            var target = AimTarget != Nobody ? PlayerController.Find(AimTarget) : null;
            float yaw;
            if (target != null)
            {
                var to = target.Position - transform.position;
                yaw = Vector3.SignedAngle(transform.forward, new Vector3(to.x, 0, to.z), Vector3.up);
            }
            else yaw = Mathf.Sin(Stealth.Game.Session.ServerTimeF * Mathf.PI * 2f / Profile.SweepPeriod) * Profile.SweepAngle / 2f;
            m_Yaw = Mathf.MoveTowardsAngle(m_Yaw, yaw, 180f * Time.deltaTime);
            if (Head != null) Head.localRotation = Quaternion.Euler(0f, m_Yaw, 0f);

            if (Sight != null)
            {
                Sight.enabled = target != null;
                if (target != null)
                {
                    // Stop short of the target: a beam ending inside the victim's camera would fill the screen.
                    var end = target.Position + Vector3.up * target.Height * 0.7f;
                    var dir = end - EyePos;
                    Sight.SetPosition(0, EyePos);
                    Sight.SetPosition(1, EyePos + dir * Mathf.Max(0f, 1f - 0.6f / Mathf.Max(dir.magnitude, 0.01f)));
                }
            }
        }

        Vector3 HeadForward => Quaternion.Euler(0f, m_Yaw, 0f) * transform.forward;

        protected override void TickServer()
        {
            var vision = Tuning.Perception.Vision;
            IIntruder seen = null;
            float best = 0f;
            foreach (var p in PlayerController.All)
            {
                if (p.IsDown) continue;
                // Locked on: the head tracks the target, so it stays in the middle of the view.
                bool locked = AimTarget == p.ClientId;
                if (!Perception.Look(EyePos, HeadForward, p, Profile.Range, locked ? 360f : Profile.ViewAngle, vision, out var g)) continue;
                if (locked) g.OffCentre = 0f;
                float rate = Profile.DetectionRate * SensorMath.VisionFactor(g, Profile.CrouchFactor, vision);
                if (rate > best) { seen = p; best = rate; }
            }

            if (seen == null)
            {
                Forget();
                if (Meter.Value < 0.2f) { SetTarget(Nobody); m_LockStart = -1f; }
                return;
            }
            Notice(seen, best);
            if (Meter.Value < 1f) return;

            // Locked on: aim, warn with the laser, then fire.
            if (AimTarget != seen.ClientId) { SetTarget(seen.ClientId); m_LockStart = Time.time; }
            if (m_LockStart >= 0f && Time.time - m_LockStart >= Profile.LockTime && Time.time >= m_NextShot)
            {
                m_NextShot = Time.time + Profile.Cooldown;
                m_LockStart = Time.time; // next shot needs another lock
                seen.HitServer(transform.position);
                Pulse(PulseShot);
            }
        }

        protected override void OnDetected(IIntruder p) { } // shoots instead of raising alarms

        protected override void OnPulse(byte effect)
        {
            if (effect != PulseShot) return;
            Audio.Sfx.At(Audio.Sfx.Bank?.TurretShot, Position);
            if (Sight == null) return;
            Sight.widthMultiplier = m_BaseWidth * 3f; // muzzle flash
            Invoke(nameof(ResetSight), 0.08f);
        }

        void ResetSight()
        {
            if (Sight != null) Sight.widthMultiplier = m_BaseWidth;
        }
    }
}
