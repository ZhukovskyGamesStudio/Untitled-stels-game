using Stealth.Game;
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
        /// <summary>The device's tuning (alarm, forgetting) — the typed Profile field of the subclass.</summary>
        protected abstract DeviceProfile Device { get; }
        public override DetectorProfile ProfileBase => Device;

        /// <summary>0 idle, 1 noticing, 2 detected (replicated, drives the status colour).</summary>
        public byte Alert => Mode;

        protected readonly SuspicionMeter Meter = new();
        protected IIntruder Suspect { get; private set; }

        protected virtual Color IdleColor => SecurityPalette.Calm;
        protected override Color StatusColor => SecurityPalette.ForAlert(Alert, IdleColor);

        protected override void OnSpawned()
        {
            Meter.AlarmCooldown = Device.AlarmCooldown;
            OnDeviceSpawned();
        }

        protected virtual void OnDeviceSpawned() { }

        /// <summary>Runs on every peer (visuals, deterministic motion from the shared clock).</summary>
        protected virtual void TickLocal() { }

        /// <summary>Server: sensing and decisions.</summary>
        protected abstract void TickServer();

        protected virtual void OnDetected(IIntruder p) => RaiseAlarm(p.Position);

        protected override void OnHackedServer()
        {
            Meter.Clear();
            SetMode(0);
        }

        void Update()
        {
            if (!IsSpawned) return;
            TickLocal();
            if (!IsServer) return;
            UpdateHackServer();
            if (SensorsActive) TickServer();
            else Decay(Mathf.Max(1f, Device.ForgetRate));
            byte alert = IsHacked ? (byte)0 : Meter.AlertLevel(Time.time);
            SetMode(alert);
            if (!IsHacked) ReportAwarenessServer(Meter.Awareness(Time.time), Suspect);
        }

        /// <summary>Accumulate suspicion of a player; at 1 the device detects them.</summary>
        protected void Notice(IIntruder p, float ratePerSecond)
        {
            Suspect = p;
            if (Meter.Notice(ratePerSecond, Time.deltaTime, Time.time)) OnDetected(p);
        }

        protected void Decay(float ratePerSecond) => Meter.Decay(ratePerSecond, Time.deltaTime);

        /// <summary>Nobody in sight: forget at the configured rate.</summary>
        protected void Forget() => Decay(Device.ForgetRate);

        /// <summary>Instant detection (pressure plates, beams).</summary>
        protected void Trip(IIntruder p)
        {
            Suspect = p;
            if (Meter.Trip(Time.time)) OnDetected(p);
        }

        protected void RaiseAlarm(Vector3 at)
        {
            if (GameManager.Instance != null) GameManager.Instance.RaiseAlarmServer(at, Device.AlarmRadius);
        }
    }
}
