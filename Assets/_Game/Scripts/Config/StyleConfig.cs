using UnityEngine;

namespace Stealth.Configs
{
    /// <summary>
    /// The visual styles to choose from (Data/Styles/Styles.asset) and the materials of the world sorted by kind, so that a
    /// style can recolour them. The material lists are filled by the asset builder from the material folders.
    /// </summary>
    public class StyleConfig : ScriptableObject
    {
        [Tooltip("The styles, in the order F8 cycles through them; the first is the default look")]
        public VisualStyle[] Styles = new VisualStyle[0];
        [Tooltip("The look of the game: applied when the game starts (none = the plain look). F8 and -style still switch it")]
        public VisualStyle Default;

        [Header("Materials by kind (filled by the asset builder)")]
        public Material[] Building = new Material[0];
        public Material[] Furniture = new Material[0];
        public Material[] Robots = new Material[0];
        public Material[] Players = new Material[0];
        public Material[] Loot = new Material[0];

        [Header("Geometry (filled by the asset builder)")]
        [Tooltip("Architecture kits by family (index = ModelFamily; None = the plain kit)")]
        public StyleKit[] Kits = new StyleKit[0];
        public MeshVariants[] Meshes = new MeshVariants[0];
        public MaterialVariants[] Materials = new MaterialVariants[0];
        public PropReplacement[] Props = new PropReplacement[0];
        [Tooltip("Models designed for one style (Art/Models/Styles/Guards)")]
        public StyleMeshes[] StyleModels = new StyleMeshes[0];
        [Tooltip("Draws a merged mesh of the house (style architecture, furniture): MeshFilter + MeshRenderer, nothing else")]
        public GameObject Batch;

        public StyleKit KitFor(ModelFamily family)
        {
            int i = (int)family;
            if (Kits != null && i < Kits.Length && Kits[i] != null) return Kits[i];
            return Kits != null && Kits.Length > 0 ? Kits[0] : null;
        }
    }
}
