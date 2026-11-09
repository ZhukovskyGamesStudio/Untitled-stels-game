using Stealth.Player;
using Unity.Netcode;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Stationary turret: sweeps its head, locks on with a red laser sight (a clear warning) and fires if the
    /// player is still in sight when the lock completes.
    /// </summary>
    public class SentryTurret : SecurityDevice
    {
        public Transform Head;
        public LineRenderer Sight;
        public float Range = 14f;
        public float ViewAngle = 60f;
        public float SweepAngle = 120f;
        public float SweepPeriod = 7f;
        public float LockTime = 1.1f;
        public float Cooldown = 2.5f;

        /// <summary>Client id being aimed at (for the laser sight on every peer), Nobody = sweeping.</summary>
        public readonly NetworkVariable<ulong> AimTarget = new(DetectorAwareness.Nobody);

        float m_LockStart = -1f;
        float m_NextShot;
        float m_Yaw;

        float m_BaseWidth = 0.025f;

        protected override void OnSpawned()
        {
            if (Sight != null) m_BaseWidth = Sight.widthMultiplier;
            if (Head == null) Head = FindPart(this, "AccentHead_Pivot") ?? FindPart(this, "AccentHead");
        }

        Vector3 EyePos => transform.position + Vector3.up * 1.35f;

        protected override void TickLocal()
        {
            // Head: aim at the target if locked, otherwise sweep (shared server time keeps peers in sync).
            var target = AimTarget.Value != DetectorAwareness.Nobody ? PlayerController.Find(AimTarget.Value) : null;
            float yaw;
            if (target != null)
            {
                var to = target.transform.position - transform.position;
                yaw = Vector3.SignedAngle(transform.forward, new Vector3(to.x, 0, to.z), Vector3.up);
            }
            else yaw = Mathf.Sin(NetworkManager.ServerTime.TimeAsFloat * Mathf.PI * 2f / SweepPeriod) * SweepAngle / 2f;
            m_Yaw = Mathf.MoveTowardsAngle(m_Yaw, yaw, 180f * Time.deltaTime);
            if (Head != null) Head.localRotation = Quaternion.Euler(0f, m_Yaw, 0f);

            if (Sight != null)
            {
                Sight.enabled = target != null;
                if (target != null)
                {
                    // Stop short of the target: a beam ending inside the victim's camera would fill the screen.
                    var end = target.transform.position + Vector3.up * target.Height * 0.7f;
                    var dir = end - EyePos;
                    Sight.SetPosition(0, EyePos);
                    Sight.SetPosition(1, EyePos + dir * Mathf.Max(0f, 1f - 0.6f / Mathf.Max(dir.magnitude, 0.01f)));
                }
            }
        }

        Vector3 HeadForward => Quaternion.Euler(0f, m_Yaw, 0f) * transform.forward;

        protected override void TickServer()
        {
            PlayerController seen = null;
            float bestDist = float.MaxValue;
            foreach (var p in PlayerController.All)
            {
                if (p.Downed.Value) continue;
                var target = p.transform.position + Vector3.up * p.Height * 0.7f;
                float d = Vector3.Distance(EyePos, target);
                bool inView = AimTarget.Value == p.OwnerClientId || Perception.InCone(EyePos, HeadForward, target, Range, ViewAngle);
                if (inView && d < bestDist && d <= Range && Perception.LineOfSight(EyePos, p, 0.7f)) { seen = p; bestDist = d; }
            }

            if (seen == null)
            {
                Decay(0.6f);
                if (Meter < 0.2f) { AimTarget.Value = DetectorAwareness.Nobody; m_LockStart = -1f; }
                return;
            }
            Notice(seen, 2.2f * (seen.Crouching.Value ? 0.7f : 1f));
            if (Meter < 1f) return;

            // Locked on: aim, warn with the laser, then fire.
            if (AimTarget.Value != seen.OwnerClientId) { AimTarget.Value = seen.OwnerClientId; m_LockStart = Time.time; }
            if (m_LockStart >= 0f && Time.time - m_LockStart >= LockTime && Time.time >= m_NextShot)
            {
                m_NextShot = Time.time + Cooldown;
                m_LockStart = Time.time; // next shot needs another lock
                seen.TakeHitServer(transform.position);
                ShotRpc();
            }
        }

        protected override void OnDetected(PlayerController p) { } // shoots instead of raising alarms

        [Rpc(SendTo.Everyone)]
        void ShotRpc()
        {
            if (Sight != null) Sight.widthMultiplier = m_BaseWidth * 3f; // muzzle flash
            Invoke(nameof(ResetSight), 0.08f);
        }

        void ResetSight()
        {
            if (Sight != null) Sight.widthMultiplier = m_BaseWidth;
        }
    }
}
