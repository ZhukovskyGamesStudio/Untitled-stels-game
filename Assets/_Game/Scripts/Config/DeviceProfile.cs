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
    }
}
