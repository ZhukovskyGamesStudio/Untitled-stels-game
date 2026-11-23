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
        [Tooltip("The house's main job: the vault's locks (switches, sequence) and the rewards of the vault and the hostage.")]
        public Stealth.Items.ObjectiveSettings Objectives = new();
        [Tooltip("Tools and places: stun cube, lure speaker, crates, workbench, med table.")]
        public GadgetSettings Gadgets = new();
        [Tooltip("The car's workbench: the run's recipes, a failed combine, blueprints and chess pieces in the houses.")]
        public CraftingSettings Crafting = new();
        [Tooltip("What the things made at the workbench do (upgraded screwdriver, keyboard, tablet, espresso, jump boots, gas mask, hammer, robot sensor).")]
        public CraftedSettings Crafted = new();
    }
}
