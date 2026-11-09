using System.Collections.Generic;
using Stealth.Game;
using Stealth.Level;
using Unity.Netcode;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Base of everything that watches the players: mobile robots (<see cref="RobotController"/>) and devices
    /// (<see cref="SecurityDevice"/>: camera, turret, spider, vacuum, lidar, PIR, pressure plate).
    ///
    /// Owns what they all share:
    ///  * identity (<see cref="Kind"/>) and the indicator point for the HUD;
    ///  * replicated awareness — how much it notices whom (detection arcs, "?" / "!" icons);
    ///  * hacking (<see cref="IHackable"/>): a hacked detector is dark and senses nothing;
    ///  * status lights coloured by the subclass state;
    ///  * a registry of spawned detectors (<see cref="All"/>).
    /// </summary>
    public abstract class Detector : NetworkBehaviour, IHackable
    {
        public const ulong Nobody = ulong.MaxValue;
        public static readonly List<Detector> All = new();

        [Header("Detector")]
        public RobotKind Kind;
        [Tooltip("World point the HUD indicators point to (eye / lens). Defaults to 1 m above the pivot.")]
        public Transform Point;
        public StatusLights Status = new();

        /// <summary>0..255 = 0..1 awareness; 255 = fully detected (chasing / alarm).</summary>
        public readonly NetworkVariable<byte> AwarenessLevel = new();
        /// <summary>Client id of the player it is noticing, <see cref="Nobody"/> if none.</summary>
        public readonly NetworkVariable<ulong> FocusClient = new(Nobody);
        public readonly NetworkVariable<bool> Hacked = new();

        float m_HackedUntil;
        float m_NextAwarenessSend;

        public float Awareness => AwarenessLevel.Value / 255f;
        public Vector3 Position => Point != null ? Point.position : transform.position + Vector3.up;
        public bool IsHacked => Hacked.Value;
        public string DisplayName => RobotInfo.Name(Kind);
        public string SensesDescription => RobotInfo.Senses(Kind);

        /// <summary>Awareness of a specific player (0 if it is focused on someone else).</summary>
        public float AwarenessOf(ulong clientId) => FocusClient.Value == clientId ? Awareness : 0f;

        /// <summary>Walking over to check a spot (orange "?").</summary>
        public virtual bool IsInvestigating => false;
        /// <summary>Chasing someone (red "!").</summary>
        public virtual bool IsChasing => false;

        /// <summary>Sensors may run this frame: server, the session allows sensing, not hacked.</summary>
        protected bool SensorsActive => IsServer && GameManager.SensorsAllowed && !IsHacked;

        // ------------------------------------------------------------------ lifecycle

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            Hacked.OnValueChanged += OnHackedChanged;
            OnSpawned();
            RefreshStatus();
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            Hacked.OnValueChanged -= OnHackedChanged;
            OnDespawned();
        }

        void OnHackedChanged(bool _, bool __) => RefreshStatus();

        protected virtual void OnSpawned() { }
        protected virtual void OnDespawned() { }

        // ------------------------------------------------------------------ hacking

        public virtual bool CanBeHackedFrom(Vector3 player) => !IsHacked && Hacking.CanHackDevice(transform.position, player);

        public void HackServer(float seconds)
        {
            if (!IsServer) return;
            m_HackedUntil = Time.time + seconds;
            Hacked.Value = true;
            ReportAwarenessServer(0f, null);
            OnHackedServer();
        }

        /// <summary>Server: forget the current target / suspicion.</summary>
        protected virtual void OnHackedServer() { }

        /// <summary>Server, every frame: ends an expired hack. Returns true while still hacked.</summary>
        protected bool UpdateHackServer()
        {
            if (Hacked.Value && Time.time >= m_HackedUntil) Hacked.Value = false;
            return Hacked.Value;
        }

        // ------------------------------------------------------------------ awareness

        /// <summary>Server: report current awareness (0..1) of <paramref name="focus"/>. Throttled to ~12 Hz.</summary>
        public void ReportAwarenessServer(float level, IIntruder focus)
        {
            if (!IsServer) return;
            byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(level) * 255f), 0, 255);
            ulong f = focus != null && b > 0 ? focus.ClientId : Nobody;
            var old = AwarenessLevel.Value;
            bool important = (b == 255) != (old == 255) || f != FocusClient.Value || (b == 0) != (old == 0);
            if (!important && (Time.time < m_NextAwarenessSend || Mathf.Abs(b - old) < 6)) return;
            m_NextAwarenessSend = Time.time + 0.08f;
            AwarenessLevel.Value = b;
            FocusClient.Value = f;
        }

        // ------------------------------------------------------------------ status lights

        /// <summary>Colour of the status lights in the current state (hacked is handled by the base).</summary>
        protected abstract Color StatusColor { get; }

        protected void RefreshStatus()
        {
            Status.Apply(IsHacked ? SecurityPalette.Hacked : StatusColor);
            OnStatusChanged();
        }

        /// <summary>Extra visuals after the status colour changed (spot lights, beams).</summary>
        protected virtual void OnStatusChanged() { }
    }
}
