using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Suspicion of a security device (pure, unit tested). Noticing fills it, losing sight drains it;
    /// at 1 the device detects the player and then stays "alarmed" for <see cref="AlarmCooldown"/> seconds.
    /// </summary>
    public sealed class SuspicionMeter
    {
        /// <summary>Overfill: the meter lingers a moment above 1 after the player leaves.</summary>
        public const float Cap = 1.2f;
        /// <summary>Above this the device shows "noticing" (yellow).</summary>
        public const float NoticedAt = 0.15f;

        public float AlarmCooldown = 6f;
        public float Value { get; private set; }

        float m_AlarmUntil = float.NegativeInfinity;

        public bool Alarmed(float now) => now < m_AlarmUntil;

        /// <summary>Accumulate suspicion. Returns true when the device detects the player right now.</summary>
        public bool Notice(float ratePerSecond, float dt, float now)
        {
            Value += ratePerSecond * dt;
            bool detected = false;
            if (Value >= 1f && !Alarmed(now))
            {
                m_AlarmUntil = now + AlarmCooldown;
                detected = true;
            }
            Value = Mathf.Min(Value, Cap);
            return detected;
        }

        /// <summary>Instant detection (plates, beams). Returns true unless the device is still alarmed.</summary>
        public bool Trip(float now)
        {
            Value = Cap;
            if (Alarmed(now)) return false;
            m_AlarmUntil = now + AlarmCooldown;
            return true;
        }

        public void Decay(float ratePerSecond, float dt) => Value = Mathf.Max(0f, Value - ratePerSecond * dt);

        /// <summary>Forget everything (hacked).</summary>
        public void Clear()
        {
            Value = 0f;
            m_AlarmUntil = float.NegativeInfinity;
        }

        /// <summary>0 idle, 1 noticing, 2 detected.</summary>
        public byte AlertLevel(float now) => Alarmed(now) ? (byte)2 : Value > NoticedAt ? (byte)1 : (byte)0;

        /// <summary>0..1 for the HUD indicator.</summary>
        public float Awareness(float now) => Alarmed(now) ? 1f : Mathf.Clamp01(Value);
    }
}
