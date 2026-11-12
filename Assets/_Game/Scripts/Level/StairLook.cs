using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The look of a staircase (prefabs Level/Stairs and StairsMirrored): the prefab carries every built look
    /// (<see cref="StairStyle"/> order) over the plain collider boxes; the one that suits the current model family and the
    /// house seed is shown (<see cref="StairStyles"/>), the boxes are hidden. Purely visual: the colliders, the navmesh
    /// and the step heights never change. Follows the style when F8 changes it.
    /// </summary>
    public class StairLook : MonoBehaviour
    {
        [Tooltip("Renderers of the collider boxes: hidden while a built look is shown.")]
        public Renderer[] Blocks;
        [Tooltip("One per StairStyle, in its order (inactive in the prefab).")]
        public GameObject[] Variants;

        int m_Seed;
        int m_Family = -1, m_Forced = -1;

        public StairStyle Current { get; private set; }

        /// <summary>Called by the level builder when it places the staircase.</summary>
        public void Configure(int seed)
        {
            m_Seed = seed;
            m_Family = -1;
            Refresh();
        }

        void LateUpdate()
        {
            if ((int)VisualStyles.Family != m_Family || Game.DevFlags.ForcedStairStyle != m_Forced) Refresh();
        }

        void Refresh()
        {
            m_Family = (int)VisualStyles.Family;
            m_Forced = Game.DevFlags.ForcedStairStyle;
            if (Variants == null || Variants.Length < StairStyles.Count) return;
            Current = m_Forced >= 0 && m_Forced < StairStyles.Count ? (StairStyle)m_Forced : StairStyles.Pick(VisualStyles.Family, m_Seed);
            for (int i = 0; i < Variants.Length; i++)
                if (Variants[i] != null && Variants[i].activeSelf != (i == (int)Current)) Variants[i].SetActive(i == (int)Current);
            if (Blocks != null)
                foreach (var r in Blocks)
                    if (r != null) r.enabled = false;
        }
    }
}
