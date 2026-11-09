using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Tuning of the sentry turret (RobotTurretConfig, next to the prefab).</summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Devices/Turret", fileName = "RobotTurretConfig")]
    public class TurretProfile : DeviceProfile
    {
        [Header("Eyes")]
        public float Range = 14f;
        public float ViewAngle = 60f;
        [Tooltip("Suspicion per second while it sees you standing.")]
        public float DetectionRate = 2.2f;
        [Tooltip("Detection rate multiplier when you crouch.")]
        public float CrouchFactor = 0.7f;

        [Header("Head")]
        public float SweepAngle = 120f;
        public float SweepPeriod = 7f;

        [Header("Shooting")]
        [Tooltip("Seconds of red laser sight before the shot: time to get out of view.")]
        public float LockTime = 1.1f;
        [Tooltip("Seconds between shots.")]
        public float Cooldown = 2.5f;
    }
}
