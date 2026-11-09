using Stealth.Game;
using UnityEngine;

namespace Stealth.Configs
{
    /// <summary>Balance of a run: quota, penalties, alarms, grace period, hunter. Edit Data/Config/Run.asset.</summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Run", fileName = "Run")]
    public class RunConfig : ScriptableObject
    {
        public RunSettings Rules = new();
    }
}
