using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Tuning of the relay (Prefabs/Sensors/SensorRelayConfig): how often and to whom it calls.</summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Relay Profile", fileName = "RelayConfig")]
    public class RelayProfile : DeviceProfile
    {
        [Tooltip("Seconds between its calls (a little more or less each time).")]
        public float Period = 28f;
        [Tooltip("It calls only when a player is within this distance, metres.")]
        public float Earshot = 22f;
        [Tooltip("…and nobody is closer than this (they would see who is calling), metres.")]
        public float TooClose = 5f;
    }
}
