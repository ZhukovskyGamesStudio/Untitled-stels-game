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

        [Header("Materials by kind (filled by the asset builder)")]
        public Material[] Building = new Material[0];
        public Material[] Furniture = new Material[0];
        public Material[] Robots = new Material[0];
        public Material[] Players = new Material[0];
        public Material[] Loot = new Material[0];
    }
}
