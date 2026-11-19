using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Tuning of the wall motion sensor (SensorMotionConfig, next to the prefab).</summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Devices/Motion Sensor", fileName = "SensorMotionConfig")]
    public class MotionSensorProfile : DeviceProfile
    {
        public float Range = 9f;
        public float ViewAngle = 100f;
        [Tooltip("Moving faster than this is noticed (crouch-walk 1.8, walk 3.6 m/s).")]
        public float SpeedThreshold = 0.35f;
        [Tooltip("How fast the suspicion fills while someone moves in view (per second at the base rate; 1.5: noticed in about twice the time it took at 3).")]
        public float DetectionRate = 1.5f;
    }
}
