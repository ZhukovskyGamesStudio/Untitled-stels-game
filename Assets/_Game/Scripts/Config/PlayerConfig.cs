using Stealth.Game;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Configs
{
    /// <summary>Everything about how the player moves, survives and makes noise. Edit Data/Config/Player.asset.</summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Player", fileName = "Player")]
    public class PlayerConfig : ScriptableObject
    {
        public MovementSettings Movement = new();
        public StaminaSettings Stamina = new();
        public HealthSettings Health = new();
        public NoiseSettings Noise = new();
        public GasSettings Gas = new();
        [Tooltip("Holding E: reviving teammates and hacking.")]
        public InteractionSettings Interaction = new();
        [Tooltip("Pings, marking robots, how far the map reveals.")]
        public TeamSettings Team = new();
    }
}
