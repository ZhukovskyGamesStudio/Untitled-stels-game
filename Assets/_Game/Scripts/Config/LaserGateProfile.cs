using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Tuning of the laser gates (LaserGateConfig, next to the prefab).</summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Devices/Laser Gate", fileName = "LaserGateConfig")]
    public class LaserGateProfile : ScriptableObject
    {
        [Header("Blinking pattern")]
        public float OnTime = 2.4f;
        public float OffTime = 1.6f;

        [Header("Moving pattern")]
        [Tooltip("Seconds for one full up-and-down cycle.")]
        public float MovePeriod = LaserGate.DefaultMovePeriod;

        [Header("Touching a beam")]
        public float AlarmRadius = 30f;
        [Tooltip("The same player can be hit again only after this many seconds.")]
        public float HitCooldown = 1.5f;
    }
}
