using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Tuning of the wall spider (RobotSpiderConfig, next to the prefab).</summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Devices/Spider", fileName = "RobotSpiderConfig")]
    public class SpiderProfile : DeviceProfile
    {
        [Header("Crawling")]
        [Tooltip("Height on the wall, meters.")]
        public float CrawlHeight = 2.6f;
        public float CrawlSpeed = 1.3f;
        [Tooltip("About how often it crosses the ceiling to the opposite wall, seconds (0 = it keeps to the walls).")]
        public float CeilingEvery = 14f;

        [Header("Eyes")]
        public float Range = 9f;
        public float ViewAngle = 120f;
        public float DetectionRate = 1.6f;
        public float CrouchFactor = 0.6f;

        [Header("Leap")]
        public float LeapTime = 0.7f;
        public float ReturnTime = 1.3f;
        [Tooltip("Seconds between leaps.")]
        public float Cooldown = 3f;
        [Tooltip("How fast it corrects the landing point towards a moving target, m/s: a late sidestep still works.")]
        public float Homing = 2.2f;
        [Tooltip("Hits if the target is this close at landing.")]
        public float HitDistance = 1.5f;
    }
}
