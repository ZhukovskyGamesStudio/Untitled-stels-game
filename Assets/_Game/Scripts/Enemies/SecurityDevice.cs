using System.Collections.Generic;
using Stealth.Game;
using Stealth.Player;
using Unity.Netcode;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Base for robots / devices with their own movement (turret, spider, vacuum, lidar, PIR, pressure plate).
    /// Handles the alert state (0 idle, 1 noticing, 2 detected), status colours, awareness replication and alarms.
    /// </summary>
    public abstract class SecurityDevice : NetworkBehaviour
    {
        public static readonly List<SecurityDevice> All = new();

        public Renderer[] StatusRenderers;
        public Light StatusLight;
        public float AlarmRadius = 30f;
        public float AlarmCooldown = 6f;

        public readonly NetworkVariable<byte> Alert = new();

        protected DetectorAwareness Awareness;
        protected float Meter;
        protected PlayerController Focus;
        float m_AlarmUntil;
        MaterialPropertyBlock m_Block;

        /// <summary>Server-side while the device is temporarily disabled (hacking).</summary>
        public bool Disabled { get; set; }

        protected bool SensorsActive => IsServer && !GameManager.Peaceful && !GameManager.InGracePeriod && !GameManager.FreezeRobots && !Disabled;

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            Awareness = GetComponent<DetectorAwareness>();
            Alert.OnValueChanged += (_, a) => ApplyVisual(a);
            ApplyVisual(Alert.Value);
            OnSpawned();
        }

        public override void OnNetworkDespawn() => All.Remove(this);

        protected virtual void OnSpawned() { }

        /// <summary>Runs on every peer (visuals, deterministic motion).</summary>
        protected virtual void TickLocal() { }

        /// <summary>Server: sensing and decisions. Use <see cref="Notice"/> / <see cref="Decay"/>.</summary>
        protected abstract void TickServer();

        protected virtual void OnDetected(PlayerController p) => RaiseAlarm(p.transform.position);

        protected virtual Color IdleColor => new(0.2f, 0.85f, 1f);

        void Update()
        {
            if (!IsSpawned) return;
            TickLocal();
            if (!IsServer) return;
            if (SensorsActive) TickServer();
            else Decay(1f);
            byte alert = Time.time < m_AlarmUntil ? (byte)2 : Meter > 0.15f ? (byte)1 : (byte)0;
            if (Disabled) alert = 0;
            if (Alert.Value != alert) Alert.Value = alert;
            Awareness?.ReportServer(alert == 2 ? 1f : Mathf.Clamp01(Meter), Focus);
        }

        /// <summary>Accumulate suspicion of a player; at 1 the device detects them.</summary>
        protected void Notice(PlayerController p, float ratePerSecond)
        {
            Focus = p;
            Meter += ratePerSecond * Time.deltaTime;
            if (Meter >= 1f && Time.time >= m_AlarmUntil)
            {
                m_AlarmUntil = Time.time + AlarmCooldown;
                OnDetected(p);
            }
            Meter = Mathf.Min(Meter, 1.2f);
        }

        protected void Decay(float ratePerSecond) => Meter = Mathf.Max(0f, Meter - ratePerSecond * Time.deltaTime);

        /// <summary>Instant detection (pressure plates, beams).</summary>
        protected void Trip(PlayerController p)
        {
            Focus = p;
            Meter = 1.2f;
            if (Time.time < m_AlarmUntil) return;
            m_AlarmUntil = Time.time + AlarmCooldown;
            OnDetected(p);
        }

        protected void RaiseAlarm(Vector3 at) => GameManager.Instance?.RaiseAlarmServer(at, AlarmRadius);

        void ApplyVisual(byte alert)
        {
            var c = alert switch { 0 => IdleColor, 1 => new Color(1f, 0.85f, 0.1f), _ => new Color(1f, 0.1f, 0.05f) };
            m_Block ??= new MaterialPropertyBlock();
            m_Block.SetColor("_BaseColor", c);
            m_Block.SetColor("_EmissionColor", c * 2.5f);
            if (StatusRenderers != null) foreach (var r in StatusRenderers) if (r != null) r.SetPropertyBlock(m_Block);
            if (StatusLight != null) StatusLight.color = c;
            OnAlertVisual(alert);
        }

        protected virtual void OnAlertVisual(byte alert) { }

        protected static Transform FindPart(Component root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}
