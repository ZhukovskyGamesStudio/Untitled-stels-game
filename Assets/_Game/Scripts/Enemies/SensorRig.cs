using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Visible sensors on a mobile robot: camera eyes, microphones, ground probes, a sniffer snout. The prefab carries
    /// all of them; on spawn only those matching the robot's <see cref="RobotSenses"/> stay, so a player can read what a
    /// robot perceives from its look — and a tweak in the robot's config changes its look too.
    /// </summary>
    public class SensorRig : MonoBehaviour
    {
        public RobotController Robot;
        [Tooltip("Camera eyes: shown when the robot sees.")]
        public GameObject[] Eyes;
        [Tooltip("Microphones: shown when the robot hears.")]
        public GameObject[] Microphones;
        [Tooltip("Ground probes: shown when the robot feels footsteps through the floor.")]
        public GameObject[] GroundProbes;
        [Tooltip("Sniffer snout: shown when the robot follows scent trails.")]
        public GameObject[] Noses;

        /// <summary>Which mounts a robot with these senses shows (pure).</summary>
        public static (bool eyes, bool ears, bool ground, bool nose) Mounts(RobotSenses s) =>
            (s.HasEyes, s.HearingMultiplier > 0f, s.SeismicRange > 0f, s.SmellRange > 0f);

        void Start()
        {
            if (Robot != null && Robot.Profile != null) Apply(Robot.Senses);
        }

        public void Apply(RobotSenses senses)
        {
            var (eyes, ears, ground, nose) = Mounts(senses);
            Show(Eyes, eyes);
            Show(Microphones, ears);
            Show(GroundProbes, ground);
            Show(Noses, nose);
        }

        static void Show(GameObject[] parts, bool on)
        {
            if (parts == null) return;
            foreach (var p in parts) if (p != null) p.SetActive(on);
        }
    }
}
