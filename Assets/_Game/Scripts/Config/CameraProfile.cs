using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Tuning of the wall camera (SecurityCameraConfig, next to the prefab).</summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Devices/Camera", fileName = "SecurityCameraConfig")]
    public class CameraProfile : DeviceProfile
    {
        /// <summary>It has a microphone: hears the players talking (DeviceProfile.Hearing; existing assets without the field get this).</summary>
        public CameraProfile() => Hearing = 0.8f;

        [Tooltip("Downward tilt, degrees.")]
        public float Tilt = 28f;
        public float SweepAngle = 80f;
        public float SweepPeriod = 8f;
        public float ViewDistance = 14f;
        public float ViewAngle = 50f;
        [Tooltip("Seconds to detect a standing player at mid range in a lit room.")]
        public float DetectTime = 2.75f;
    }
}
