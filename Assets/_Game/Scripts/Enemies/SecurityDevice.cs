using Stealth.Configs;
using Stealth.Game;
using Stealth.Player;
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
            if (IsServer && Device.Hearing > 0f) NoiseSystem.OnVoice += OnVoice;
            OnDeviceSpawned();
        }

        protected override void OnDespawned() => NoiseSystem.OnVoice -= OnVoice;

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

        /// <summary>
        /// Server: a player's voice in the chat (NoiseSystem.OnVoice) — a device with a microphone (<see cref="DeviceProfile.Hearing"/>)
        /// grows suspicious of the player who made it, as loud as it reaches it through walls and doors.
        /// </summary>
        void OnVoice(Vector3 position, float radius, ulong source)
        {
            if (!SensorsActive || Device == null || Device.Hearing <= 0f) return;
            var player = PlayerController.Find(source);
            if (player == null || player.IsDown) return;
            float hearing = radius * Device.Hearing;
            var from = position + Vector3.up * Perception.EarHeight;
            var ear = transform.position;
            float distance = Vector3.Distance(from, ear);
            if (distance >= hearing) return;
            var settings = Tuning.Perception.Hearing;
            float loudness = SensorMath.Loudness(distance, hearing, Perception.SoundTransmission(from, ear, settings));
            if (loudness < settings.MinLoudness) return;
            Suspect = player;
            if (Meter.Notice(loudness * Device.NoiseSuspicion, 1f, Time.time)) OnDetected(player);
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
