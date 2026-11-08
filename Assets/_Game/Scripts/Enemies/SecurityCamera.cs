using Stealth.Game;
using Stealth.Level;
using Stealth.Player;
using Unity.Netcode;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Wall camera sweeping left-right. Rotation is derived from the shared server time, so it is identical
    /// on every peer without syncing transforms. Spotting a player raises an alarm: nearby robots come running.
    /// </summary>
    public class SecurityCamera : NetworkBehaviour
    {
        public Transform Pivot;
        public Light Spot;
        public Renderer[] StatusRenderers;

        public float Tilt = 28f;
        public float SweepAngle = 80f;
        public float SweepPeriod = 8f;
        public float ViewDistance = 14f;
        public float ViewAngle = 50f;
        public float DetectTime = 1.1f;
        public float AlarmRadius = 35f;
        public float AlarmCooldown = 6f;

        /// <summary>0 idle, 1 noticing, 2 alarm</summary>
        public readonly NetworkVariable<byte> Alert = new();

        float m_Meter;
        float m_Phase;
        float m_AlarmUntil;
        MaterialPropertyBlock m_Block;

        public override void OnNetworkSpawn()
        {
            m_Phase = (NetworkObjectId % 11) * 0.73f;
            Alert.OnValueChanged += (_, a) => ApplyVisual(a);
            ApplyVisual(Alert.Value);
            if (Spot != null)
            {
                Spot.range = ViewDistance;
                Spot.spotAngle = ViewAngle;
                Spot.innerSpotAngle = ViewAngle * 0.7f;
            }
        }

        void ApplyVisual(byte alert)
        {
            var c = alert switch { 0 => new Color(0.75f, 0.9f, 1f), 1 => new Color(1f, 0.85f, 0.1f), _ => new Color(1f, 0.1f, 0.05f) };
            if (Spot != null) { Spot.color = c; Spot.intensity = alert == 2 ? 6f : 3f; }
            m_Block ??= new MaterialPropertyBlock();
            m_Block.SetColor("_BaseColor", c);
            m_Block.SetColor("_EmissionColor", c * 3f);
            foreach (var r in StatusRenderers) if (r != null) r.SetPropertyBlock(m_Block);
        }

        void Update()
        {
            if (!IsSpawned) return;
            float t = NetworkManager.ServerTime.TimeAsFloat + m_Phase;
            float yaw = Mathf.Sin(t * Mathf.PI * 2f / SweepPeriod) * SweepAngle / 2f;
            Pivot.localRotation = Quaternion.Euler(Tilt, yaw, 0f);

            if (!IsServer) return;
            PlayerController seen = null;
            float bestDist = float.MaxValue;
            foreach (var p in PlayerController.All)
            {
                if (p.Downed.Value) continue;
                if (CanSee(p, out var d) && d < bestDist) { seen = p; bestDist = d; }
            }

            if (seen != null)
            {
                float closeness = 1f - Mathf.Clamp01(bestDist / ViewDistance);
                m_Meter += Time.deltaTime / DetectTime * Mathf.Lerp(0.5f, 2f, closeness) * (seen.Crouching.Value ? 0.7f : 1f);
            }
            else m_Meter = Mathf.Max(0f, m_Meter - Time.deltaTime * 0.5f);

            if (m_Meter >= 1f && Time.time >= m_AlarmUntil && seen != null)
            {
                m_AlarmUntil = Time.time + AlarmCooldown;
                GameManager.Instance?.RaiseAlarmServer(seen.transform.position, AlarmRadius);
            }
            byte alert = Time.time < m_AlarmUntil ? (byte)2 : m_Meter > 0.15f ? (byte)1 : (byte)0;
            if (Alert.Value != alert) Alert.Value = alert;
            if (alert == 2 && seen == null) m_Meter = Mathf.Min(m_Meter, 0.9f);
        }

        bool CanSee(PlayerController p, out float distance)
        {
            var eye = Pivot.position + Pivot.forward * 0.3f;
            var target = p.transform.position + Vector3.up * (p.Height * 0.7f);
            var dir = target - eye;
            distance = dir.magnitude;
            if (distance > ViewDistance || Vector3.Angle(Pivot.forward, dir) > ViewAngle / 2f) return false;
            const int mask = ~(1 << LevelGenerator.CeilingLayer);
            if (Physics.Raycast(eye, dir.normalized, out var hit, distance + 0.5f, mask, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<PlayerController>() == p;
            return true;
        }
    }
}
