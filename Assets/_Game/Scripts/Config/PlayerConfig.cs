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
        [Tooltip("Knocked out and held: freeing a teammate, a lone player's way back, the carriers.")]
        public CaptiveSettings Captive = new();
        [Tooltip("Nets, stuns, the muddled head, goo, smoke.")]
        public AilmentSettings Ailments = new();
        public NoiseSettings Noise = new();
        public GasSettings Gas = new();
        [Tooltip("Holding E: reviving teammates and hacking.")]
        public InteractionSettings Interaction = new();
        [Tooltip("Pings, marking robots, how far the map reveals.")]
        public TeamSettings Team = new();
        [Tooltip("Smoothing of edges over time (temporal anti-aliasing).")]
        public SmoothingSettings Picture = new();
        [Tooltip("How far the head turns with the body held: on the shaft's ladder, in a seat of the car.")]
        public LookSettings Look = new();
    }
}
