using Stealth.Game;
using Unity.Netcode;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Base for robots / devices with their own movement and a suspicion meter instead of a navmesh brain
    /// (camera, turret, spider, vacuum, lidar, PIR, pressure plate).
    /// Subclasses sense in <see cref="TickServer"/> through <see cref="Notice"/> / <see cref="Decay"/> / <see cref="Trip"/>
    /// and react in <see cref="OnDetected"/> (default: building-wide alarm).
    /// </summary>
    public abstract class SecurityDevice : Detector
    {
        [Header("Alarm")]
        public float AlarmRadius = 30f;
        public float AlarmCooldown = 6f;

        /// <summary>0 idle, 1 noticing, 2 detected (replicated, drives the status colour).</summary>
        public readonly NetworkVariable<byte> Alert = new();

        protected readonly SuspicionMeter Meter = new();
        protected IIntruder Suspect { get; private set; }

        protected virtual Color IdleColor => SecurityPalette.Calm;
        protected override Color StatusColor => SecurityPalette.ForAlert(Alert.Value, IdleColor);

        protected override void OnSpawned()
        {
            Meter.AlarmCooldown = AlarmCooldown;
            Alert.OnValueChanged += OnAlertChanged;
            OnDeviceSpawned();
        }

        protected override void OnDespawned() => Alert.OnValueChanged -= OnAlertChanged;

        void OnAlertChanged(byte _, byte __) => RefreshStatus();

        protected virtual void OnDeviceSpawned() { }

        /// <summary>Runs on every peer (visuals, deterministic motion from the shared clock).</summary>
        protected virtual void TickLocal() { }

        /// <summary>Server: sensing and decisions.</summary>
        protected abstract void TickServer();

        protected virtual void OnDetected(IIntruder p) => RaiseAlarm(p.Position);

        protected override void OnHackedServer()
        {
            Meter.Clear();
            Alert.Value = 0;
        }

        void Update()
        {
            if (!IsSpawned) return;
            TickLocal();
            if (!IsServer) return;
            UpdateHackServer();
            if (SensorsActive) TickServer();
            else Decay(1f);
            byte alert = IsHacked ? (byte)0 : Meter.AlertLevel(Time.time);
            if (Alert.Value != alert) Alert.Value = alert;
            if (!IsHacked) ReportAwarenessServer(Meter.Awareness(Time.time), Suspect);
        }

        /// <summary>Accumulate suspicion of a player; at 1 the device detects them.</summary>
        protected void Notice(IIntruder p, float ratePerSecond)
        {
            Suspect = p;
            if (Meter.Notice(ratePerSecond, Time.deltaTime, Time.time)) OnDetected(p);
        }

        protected void Decay(float ratePerSecond) => Meter.Decay(ratePerSecond, Time.deltaTime);

        /// <summary>Instant detection (pressure plates, beams).</summary>
        protected void Trip(IIntruder p)
        {
            Suspect = p;
            if (Meter.Trip(Time.time)) OnDetected(p);
        }

        protected void RaiseAlarm(Vector3 at)
        {
            if (GameManager.Instance != null) GameManager.Instance.RaiseAlarmServer(at, AlarmRadius);
        }
    }
}
