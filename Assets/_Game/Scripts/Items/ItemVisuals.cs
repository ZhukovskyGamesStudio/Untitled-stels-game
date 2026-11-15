using UnityEngine;

namespace Stealth.Items
{
    /// <summary>Meshes (same order as ItemCatalog.Shapes) and materials (same order as ItemCatalog.Colors).</summary>
    [CreateAssetMenu(menuName = "Stealth/Item Visuals")]
    public class ItemVisuals : ScriptableObject
    {
        public Mesh[] Meshes;
        public Material[] ColorMaterials;
        [Tooltip("Tools with a model of their own (lure speaker): shown instead of the tinted mesh; null = the mesh.")]
        public GameObject[] Models;
        [Tooltip("Pictures drawn over an item's cells in the backpack and the stash (real items: the shovel); null = cells only. " +
                 "The picture is the size of the shape's cells in its base turn (Tools/Blender/item_icons.py).")]
        public Sprite[] Icons;
    }
}
