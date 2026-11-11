using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Tuning of the pressure plate (SensorPlateConfig, next to the prefab).</summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Devices/Pressure Plate", fileName = "SensorPlateConfig")]
    public class PressurePlateProfile : DeviceProfile
    {
        [Tooltip("Half the side of the plate, meters.")]
        public float HalfSize = 0.55f;
    }
}
