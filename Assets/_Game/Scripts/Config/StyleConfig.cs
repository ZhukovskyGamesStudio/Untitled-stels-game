using UnityEngine;

namespace Stealth.Configs
{
    /// <summary>
    /// The visual styles to choose from (Data/Styles/Styles.asset) and the materials of the world sorted by kind, so that a
    /// style can recolour them. The material lists are filled by the asset builder from the material folders.
    /// </summary>
    public class StyleConfig : ScriptableObject
    {
        [Tooltip("The styles, in the order the F8 picker lists them")]
        public VisualStyle[] Styles = new VisualStyle[0];
        [Tooltip("The look of the game: applied when the game starts (none = the plain look). the F8 picker and -style still switch it")]
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
        [Tooltip("Wear levels of a material (derelict painted metal): each object takes one")]
        public WearVariants[] Wear = new WearVariants[0];
        public PropReplacement[] Props = new PropReplacement[0];
        [Tooltip("Models designed for one style (Art/Models/Styles/PerStyle/<style>, Styles/Players)")]
        public StyleMeshes[] StyleModels = new StyleMeshes[0];
        [Tooltip("Draws a merged mesh of the house (style architecture, furniture): MeshFilter + MeshRenderer, nothing else")]
        public GameObject Batch;
        [Tooltip("The players as rigged humans, one per department (PlayerLooks.Department; Tools/Blender/human.py): shown in every style that has no rigged human of its own (StyleGeometry.PlayerRig)")]
        public GameObject[] PlayerHumans = new GameObject[0];
        [Tooltip("Items the rigged humans can wear (Prefabs/Player/Wear/<who>/<item>, Tools/Blender/human.py item=…): every item made for every person's build")]
        public Stealth.Player.WearPiece[] Wardrobe = new Stealth.Player.WearPiece[0];
        [Tooltip("What each department wears (PlayerLooks.Department order): item names, a slot each (a slot not named is bare); an empty list keeps the person's own outfit")]
        public HumanOutfit[] PlayerOutfits = new HumanOutfit[0];

        /// <summary>The outfit of a department (empty: the person's own).</summary>
        public string[] OutfitFor(int department) =>
            PlayerOutfits != null && department >= 0 && department < PlayerOutfits.Length && PlayerOutfits[department] != null
                ? PlayerOutfits[department].Items : System.Array.Empty<string>();

        public StyleKit KitFor(ModelFamily family)
        {
            int i = (int)family;
            if (Kits != null && i < Kits.Length && Kits[i] != null) return Kits[i];
            return Kits != null && Kits.Length > 0 ? Kits[0] : null;
        }
    }

    /// <summary>What a rigged human wears: item names of the wardrobe (HumanWardrobe), a slot each.</summary>
    [System.Serializable]
    public class HumanOutfit
    {
        [Tooltip("hoodie / shirt / vest / jumper, jeans / slacks / cargo / wide, sneakers / flats / boots / red_sneakers, messy / bob / buzz / bun, beard, headphones / torch / beret, glasses")]
        public string[] Items = new string[0];
    }
}
