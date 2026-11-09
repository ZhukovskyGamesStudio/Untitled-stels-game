using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Tuning of the ceiling vacuum (RobotVacuumConfig, next to the prefab).</summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Devices/Vacuum", fileName = "RobotVacuumConfig")]
    public class VacuumProfile : DeviceProfile
    {
        [Tooltip("Speed along the ceiling, m/s.")]
        public float Speed = 1.1f;
        [Tooltip("Rotation of the scan line, degrees per second.")]
        public float ScanSpeed = 75f;
        [Tooltip("Half the length of the scan line on the floor, meters.")]
        public float HalfLength = 3f;
    }
}
