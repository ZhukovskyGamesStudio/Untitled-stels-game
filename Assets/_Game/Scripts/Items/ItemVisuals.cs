using UnityEngine;

namespace Stealth.Items
{
    /// <summary>Meshes (same order as ItemCatalog.Shapes) and materials (same order as ItemCatalog.Colors).</summary>
    [CreateAssetMenu(menuName = "Stealth/Item Visuals")]
    public class ItemVisuals : ScriptableObject
    {
        public Mesh[] Meshes;
        public Material[] ColorMaterials;
    }
}
