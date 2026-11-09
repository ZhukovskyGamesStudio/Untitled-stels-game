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
        public float SpeedThreshold = 2.2f;
        public float DetectionRate = 3f;
    }
}
