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

        [Header("A teammate's own words")]
        [Tooltip("The words it plays back last at least this long, seconds (shorter speech is not used).")]
        public float SnippetMin = 0.8f;
        [Tooltip("…and at most this long, seconds.")]
        public float SnippetMax = 2.5f;
        [Tooltip("Seconds of each player's latest speech the server remembers while a relay stands in the house.")]
        public float RememberSeconds = 8f;
        [Tooltip("Speech older than this is forgotten, seconds.")]
        public float ForgetAfter = 60f;
    }
}
