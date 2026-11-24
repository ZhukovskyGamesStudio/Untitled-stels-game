using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A hole knocked through a wall (Level/BreachFrame, Level/HoleFrame): several ways it broke, of which the house shows one
    /// by its seed, and the plaster skin round it on each face — wall, so the style dresses each in the finish of the room
    /// it looks into (<see cref="LevelShell.WallParts"/>). Decor: the wall pieces keep the opening's shape.
    /// </summary>
    public class RuinFrame : MonoBehaviour
    {
        /// <summary>One child per way it broke.</summary>
        public GameObject[] Variants;
        /// <summary>The plaster round the hole, two per variant: the face at the frame's −Z, then the one at +Z.</summary>
        public MeshRenderer[] Skins;

        /// <summary>Which of <paramref name="count"/> variants a seed shows.</summary>
        public static int VariantOf(int seed, int count) => count <= 0 ? 0 : (int)((uint)seed % (uint)count);

        /// <summary>Shows the variant of this seed alone and hands its two skins to <paramref name="wallParts"/>.</summary>
        public void Show(int seed, List<MeshRenderer> wallParts)
        {
            if (Variants == null || Variants.Length == 0) return;
            int pick = VariantOf(seed, Variants.Length);
            for (int k = 0; k < Variants.Length; k++)
                if (Variants[k] != null) Variants[k].SetActive(k == pick);
            if (Skins == null || wallParts == null) return;
            for (int k = 2 * pick; k < 2 * pick + 2 && k < Skins.Length; k++)
                if (Skins[k] != null) wallParts.Add(Skins[k]);
        }
    }
}
