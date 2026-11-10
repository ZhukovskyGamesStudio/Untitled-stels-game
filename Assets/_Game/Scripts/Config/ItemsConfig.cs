using Stealth.Items;
using UnityEngine;

namespace Stealth.Configs
{
    /// <summary>
    /// Loot colours: name, colour, value and spawn weight (shapes are fixed by the meshes). Edit Data/Config/Items.asset.
    /// The order of colours matches the item materials (Art/Materials/Items/Item_N) — rebuild assets after adding one.
    /// </summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Items", fileName = "Items")]
    public class ItemsConfig : ScriptableObject
    {
        [Tooltip("Points per cell of a grey (×1) item.")]
        public int PointsPerCell = 5;
        public ItemCatalog.ItemColor[] Colors = ItemCatalog.DefaultColors();
        [Tooltip("Safes: the dial minigame and the loot inside.")]
        public SafeSettings Safes = new();
    }
}
