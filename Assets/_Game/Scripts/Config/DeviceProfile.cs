using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Common tuning of devices with their own scripts: what happens on detection and how fast they forget.</summary>
    public abstract class DeviceProfile : DetectorProfile
    {
        [Header("Alarm")]
        [Tooltip("Robots within this radius come running when it raises an alarm.")]
        public float AlarmRadius = 30f;
        [Tooltip("After an alarm it stays red this long before it can raise another one.")]
        public float AlarmCooldown = 6f;
        [Tooltip("Suspicion drained per second while it sees nobody.")]
        public float ForgetRate = 0.5f;

        [Header("Hearing")]
        [Tooltip("Its microphone: share of a talking player's noise radius it hears (voice chat only, not steps; 0 = deaf). Through walls like the robots (Perception.SoundTransmission).")]
        public float Hearing;
        [Tooltip("Suspicion added by a noise heard at full loudness (1 = detected at once; a voice heard twice in a row at 0.6 trips it).")]
        public float NoiseSuspicion = 0.6f;
    }
}
