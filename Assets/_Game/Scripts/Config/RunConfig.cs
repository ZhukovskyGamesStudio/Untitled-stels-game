using Stealth.Game;
using UnityEngine;

namespace Stealth.Configs
{
    /// <summary>Balance of a run: fuel for the drives (the quota), penalties, alarms, grace period, hunter, the car. Edit Data/Config/Run.asset.</summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Run", fileName = "Run")]
    public class RunConfig : ScriptableObject
    {
        public RunSettings Rules = new();
        [Tooltip("How the team's pickup drives in the sewer.")]
        public Stealth.Vehicles.CarSettings Car = new();
        [Tooltip("The garage's car upgrade bench: the engine's fuel cut, the recipes' sizes, the kennel's box.")]
        public Stealth.Vehicles.CarUpgradeSettings Upgrades = new();
    }
}
