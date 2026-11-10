using UnityEngine;

namespace Stealth.Level
{
    /// <summary>What a solid thing is made of, for sounds passing through it.</summary>
    public enum SoundMaterial : byte { Furniture, Wall, Door, Slab }

    /// <summary>
    /// Marks a level piece that muffles sound (walls, slabs, doors). Colliders without it count as furniture.
    /// How much each material lets through — Data/Config/Perception (Hearing).
    /// </summary>
    public class SoundBlocker : MonoBehaviour
    {
        public SoundMaterial Material = SoundMaterial.Wall;
    }
}
