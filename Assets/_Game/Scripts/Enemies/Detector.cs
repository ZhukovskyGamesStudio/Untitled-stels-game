using System;
using System.Collections.Generic;
using Stealth.Game;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Everything about a detector that the other peers need to see. The server owns it; the network twin
    /// (DetectorNet, assembly Stealth.Net) replicates it as a whole for every kind of robot and device.
    /// </summary>
    public struct DetectorSnapshot : IEquatable<DetectorSnapshot>
    {
        /// <summary>0..255 = 0..1 awareness; 255 = fully detected (chasing / alarm).</summary>
        public byte Awareness;
        /// <summary>Client id of the player it is noticing, <see cref="Detector.Nobody"/> if none.</summary>
        public ulong Focus;
        public bool Hacked;
        /// <summary>Kind-specific mode: robot state, device alert level.</summary>
        public byte Mode;
        /// <summary>Kind-specific variant: colour of a sorter (-1 none).</summary>
        public sbyte Variant;
        /// <summary>Kind-specific target client: turret aim (<see cref="Detector.Nobody"/> if none).</summary>
        public ulong Target;
        /// <summary>Loot a worker carries: shape * 16 + colour, -1 none (<see cref="Detector.EncodeCargo"/>).</summary>
        public short Cargo;

        public static DetectorSnapshot Initial => new() { Focus = Detector.Nobody, Variant = -1, Target = Detector.Nobody, Cargo = -1 };

        public bool Equals(DetectorSnapshot o) =>
            Awareness == o.Awareness && Focus == o.Focus && Hacked == o.Hacked && Mode == o.Mode && Variant == o.Variant && Target == o.Target
            && Cargo == o.Cargo;
    }

    /// <summary>
    /// Base of everything that watches the players: mobile robots (<see cref="RobotController"/>) and devices
    /// (<see cref="SecurityDevice"/>: camera, turret, spider, vacuum, lidar, PIR, pressure plate).
    ///
    /// Owns what they all share:
    ///  * identity (<see cref="Kind"/>) and the indicator point for the HUD;
    ///  * awareness — how much it notices whom (detection arcs, "?" / "!" icons);
    ///  * hacking (<see cref="IHackable"/>): a hacked detector is dark and senses nothing;
    ///  * status lights coloured by the subclass state;
    ///  * a registry of spawned detectors (<see cref="All"/>).
    /// No networking here: the twin calls <see cref="Spawned"/> / <see cref="ApplyReplicated"/> and carries pulses.
    /// </summary>
    public abstract class Detector : MonoBehaviour, IHackable
    {
        public const ulong Nobody = ulong.MaxValue;
        public static readonly List<Detector> All = new();

        /// <summary>One-shot effects shown on every peer (see <see cref="Pulse"/>).</summary>
        protected const byte PulseRadio = 1, PulseShot = 2, PulseStomp = 3;

        [Header("Detector")]
        public RobotKind Kind;
        [Tooltip("World point the HUD indicators point to (eye / lens). Defaults to 1 m above the pivot.")]
        public Transform Point;
        public StatusLights Status = new();

        DetectorSnapshot m_State = DetectorSnapshot.Initial;
        IDetectorLink m_Link;
        float m_HackedUntil;
        float m_NextAwarenessSend;

        /// <summary>This peer is the server (senses and decides).</summary>
        public bool IsServer { get; private set; }
        public bool IsSpawned { get; private set; }
        /// <summary>Stable id shared by all peers (phases of sweeps and scans).</summary>
        public ulong NetId { get; private set; }

        public DetectorSnapshot Snapshot => m_State;
        public float Awareness => m_State.Awareness / 255f;
        public ulong FocusClient => m_State.Focus;
        public bool IsHacked => m_State.Hacked;
        protected byte Mode => m_State.Mode;
        protected sbyte Variant => m_State.Variant;
        protected ulong TargetClient => m_State.Target;

        public Vector3 Position => Point != null ? Point.position : transform.position + Vector3.up;
        public string DisplayName => RobotInfo.Name(Kind);
        public string SensesDescription => RobotInfo.Senses(Kind);

        /// <summary>Awareness of a specific player (0 if it is focused on someone else).</summary>
        public float AwarenessOf(ulong clientId) => FocusClient == clientId ? Awareness : 0f;

        /// <summary>Tuning asset of this kind (next to the prefab).</summary>
        public abstract DetectorProfile ProfileBase { get; }

        /// <summary>Walking over to check a spot (orange "?").</summary>
        public virtual bool IsInvestigating => false;
        /// <summary>Chasing someone (red "!").</summary>
        public virtual bool IsChasing => false;

        /// <summary>Sensors may run this frame: server, the session allows sensing, not hacked.</summary>
        protected bool SensorsActive => IsServer && GameManager.SensorsAllowed && !IsHacked;

        // ------------------------------------------------------------------ lifecycle (called by the network twin)

        public void Spawned(IDetectorLink link, ulong netId, bool isServer)
        {
            m_Link = link;
            NetId = netId;
            IsServer = isServer;
            IsSpawned = true;
            All.Add(this);
            OnSpawned();
            RefreshStatus();
        }

        public void Despawned()
        {
            All.Remove(this);
            IsSpawned = false;
            m_Link = null;
            OnDespawned();
        }

        /// <summary>Client: the server's state of this detector.</summary>
        public void ApplyReplicated(DetectorSnapshot s)
        {
            if (s.Equals(m_State)) return;
            m_State = s;
            RefreshStatus();
        }

        /// <summary>Every peer: a one-shot effect announced by the server.</summary>
        public void ReceivePulse(byte effect) => OnPulse(effect);

        protected virtual void OnSpawned() { }
        protected virtual void OnDespawned() { }
        protected virtual void OnPulse(byte effect) { }

        /// <summary>Server: show an effect on every peer.</summary>
        protected void Pulse(byte effect)
        {
            if (m_Link != null) m_Link.BroadcastPulse(effect);
            else OnPulse(effect);
        }

        // ------------------------------------------------------------------ server-side state

        protected void SetMode(byte mode)
        {
            if (m_State.Mode == mode) return;
            m_State.Mode = mode;
            RefreshStatus();
        }

        protected void SetVariant(sbyte variant)
        {
            if (m_State.Variant == variant) return;
            m_State.Variant = variant;
            RefreshStatus();
        }

        protected void SetTarget(ulong target) => m_State.Target = target;

        /// <summary>Carried loot (-1 none), see <see cref="EncodeCargo"/>.</summary>
        public short Cargo => m_State.Cargo;

        public static short EncodeCargo(byte shape, byte color) => (short)(shape * 16 + color);
        public static (byte shape, byte color) DecodeCargo(short cargo) => ((byte)(cargo / 16), (byte)(cargo % 16));

        protected void SetCargo(short cargo)
        {
            if (m_State.Cargo == cargo) return;
            m_State.Cargo = cargo;
            RefreshStatus();
        }

        // ------------------------------------------------------------------ hacking

        public virtual bool CanBeHackedFrom(Vector3 player, float reach) => !IsHacked && Hacking.CanHackDevice(transform.position, player, reach);

        public void HackServer(float seconds)
        {
            if (!IsServer) return;
            m_HackedUntil = Time.time + seconds;
            m_State.Hacked = true;
            ReportAwarenessServer(0f, null);
            OnHackedServer();
            RefreshStatus();
        }

        /// <summary>Server: forget the current target / suspicion.</summary>
        protected virtual void OnHackedServer() { }

        /// <summary>Server, every frame: ends an expired hack. Returns true while still hacked.</summary>
        protected bool UpdateHackServer()
        {
            if (m_State.Hacked && Time.time >= m_HackedUntil)
            {
                m_State.Hacked = false;
                RefreshStatus();
            }
            return m_State.Hacked;
        }

        // ------------------------------------------------------------------ awareness

        /// <summary>Server: report current awareness (0..1) of <paramref name="focus"/>. Throttled to ~12 Hz.</summary>
        public void ReportAwarenessServer(float level, IIntruder focus)
        {
            if (!IsServer) return;
            byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(level) * 255f), 0, 255);
            ulong f = focus != null && b > 0 ? focus.ClientId : Nobody;
            var old = m_State.Awareness;
            bool important = (b == 255) != (old == 255) || f != m_State.Focus || (b == 0) != (old == 0);
            if (!important && (Time.time < m_NextAwarenessSend || Mathf.Abs(b - old) < 6)) return;
            m_NextAwarenessSend = Time.time + 0.08f;
            m_State.Awareness = b;
            m_State.Focus = f;
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
