using Stealth.Items;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Configs
{
    /// <summary>
    /// How houses are generated: size and rooms by depth, light, doors and vents, how many guards and which ones,
    /// loot. Edit Data/Config/Level.asset. All peers generate the same house from it, so every build of a session
    /// must carry the same asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Level", fileName = "Level")]
    public class LevelConfig : ScriptableObject
    {
        public BuildingSettings Building = new();
        public SecuritySettings Security = new();
        public LootSettings Loot = new();
    }
}
