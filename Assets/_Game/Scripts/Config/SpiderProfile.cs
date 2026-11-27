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

        [Header("Web (it has no eyes: the wires across its room tell it)")]
        [Tooltip("Metres between the wires along the room.")]
        public float StrandSpacing = 1.7f;
        [Tooltip("The way past a wire at its open end, metres.")]
        public float StrandGap = 1.0f;
        public int MaxStrands = 6;
        [Tooltip("How fast it scurries along the walls towards whoever touched a wire, m/s.")]
        public float RushSpeed = 6f;
        [Tooltip("It leaps once this close to them, metres.")]
        public float LeapRange = 4.5f;

        [Header("Leap")]
        public float LeapTime = 0.7f;
        public float ReturnTime = 1.3f;
        [Tooltip("Seconds between leaps.")]
        public float Cooldown = 3f;
        [Tooltip("How fast it corrects the landing point towards a moving target, m/s: a late sidestep still works.")]
        public float Homing = 2.2f;
        [Tooltip("Hits if the target is this close at landing.")]
        public float HitDistance = 1.5f;
        [Tooltip("A player it knocks out it spins into a cocoon and hangs this high above the floor, metres (a teammate cuts them down with E).")]
        public float CocoonHeight = 1.1f;
    }
}
