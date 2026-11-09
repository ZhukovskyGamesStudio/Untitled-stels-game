using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Tuning of the lidar pole (SensorLidarConfig, next to the prefab).</summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Devices/Lidar", fileName = "SensorLidarConfig")]
    public class LidarProfile : DeviceProfile
    {
        [Tooltip("Height of the beam above the floor: jump over it.")]
        public float BeamHeight = 0.38f;
        public float Range = 6f;
        [Tooltip("Rotation, degrees per second.")]
        public float Speed = 110f;
    }
}
