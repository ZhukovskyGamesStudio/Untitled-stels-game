using Stealth.Configs;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The look of a staircase (prefabs Level/Stairs and StairsMirrored) or of a bay of a gallery railing (Level/RailingBay,
    /// where there are no collider boxes to hide): the prefab carries every built look
    /// (<see cref="StairStyle"/> order) over the plain collider boxes; the one the style asks for
    /// (<see cref="StyleGeometry.Stairs"/>), or the one that suits the current model family and the house seed, is shown
    /// (<see cref="StairStyles"/>), the boxes are hidden. A style with its own architecture dresses the look in it: treads
    /// in its floor finish, the solid body in its wall finish, balusters and rails in its trim (<see cref="StairStyles.Parts"/>),
    /// so the staircase belongs to the rooms around it. Purely visual: the colliders, the navmesh and the step heights
    /// never change. Follows the style when F8 changes it.
    /// </summary>
    public class StairLook : MonoBehaviour
    {
        [Tooltip("Renderers of the collider boxes: hidden while a built look is shown.")]
        public Renderer[] Blocks;
        [Tooltip("One per StairStyle, in its order (inactive in the prefab).")]
        public GameObject[] Variants;
        [Tooltip("A bay of a gallery railing (its own material slots), not a staircase.")]
        public bool Railing;

        static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        static MaterialPropertyBlock s_Block;

        int m_Seed;
        int m_Family = -1, m_Forced = -1;
        VisualStyle m_Style;
        Material[][] m_Built;

        public StairStyle Current { get; private set; }

        void Awake()
        {
            // the materials each look was built with: put back when a style has no architecture of its own
            if (Variants == null) return;
            m_Built = new Material[Variants.Length][];
            for (int i = 0; i < Variants.Length; i++)
                if (Variants[i] != null && Variants[i].TryGetComponent<Renderer>(out var r)) m_Built[i] = r.sharedMaterials;
        }

        /// <summary>Called by the level builder when it places the staircase.</summary>
        public void Configure(int seed)
        {
            m_Seed = seed;
            m_Family = -1;
            Refresh();
        }

        void LateUpdate()
        {
            if ((int)VisualStyles.Family != m_Family || Game.DevFlags.ForcedStairStyle != m_Forced || VisualStyles.Current != m_Style) Refresh();
        }

        void Refresh()
        {
            m_Family = (int)VisualStyles.Family;
            m_Forced = Game.DevFlags.ForcedStairStyle;
            m_Style = VisualStyles.Current;
            if (Variants == null || Variants.Length < StairStyles.Count) return;
            var g = m_Style != null ? m_Style.Geometry : null;
            var look = VisualStyles.Look;
            if (look != null) g = look.Dress(g); // the house's own stairs and finishes
            Current = m_Forced >= 0 && m_Forced < StairStyles.Count ? (StairStyle)m_Forced
                : StairStyles.Pick(g != null ? g.Stairs : StairChoice.Auto, VisualStyles.Family, m_Seed);
            for (int i = 0; i < Variants.Length; i++)
                if (Variants[i] != null && Variants[i].activeSelf != (i == (int)Current)) Variants[i].SetActive(i == (int)Current);
            if (Blocks != null)
                foreach (var r in Blocks)
                    if (r != null) r.enabled = false;
            Dress(g);
        }

        /// <summary>The shown look in the style's own floor, wall and trim (where it has them), or as it was built.</summary>
        void Dress(StyleGeometry g)
        {
            int i = (int)Current;
            var go = Variants[i];
            if (go == null || !go.TryGetComponent<Renderer>(out var r) || m_Built == null || m_Built[i] == null) return;
            var mats = (Material[])m_Built[i].Clone();
            r.sharedMaterials = mats;
            VisualStyles.Dress(go); // the family's version of each built material
            mats = r.sharedMaterials;
            var parts = g != null ? StairStyles.Parts(Current, Railing) : new StairPart[0];
            s_Block ??= new MaterialPropertyBlock();
            for (int k = parts.Length; k < mats.Length; k++) { s_Block.Clear(); r.SetPropertyBlock(s_Block, k); }
            for (int k = 0; k < mats.Length && k < parts.Length; k++)
            {
                var (mat, tile) = parts[k] switch
                {
                    StairPart.Body => (g.Wall, g.WallTile),
                    StairPart.Tread => (g.Floor, g.FloorTile),
                    StairPart.Trim => (g.Trim, 1f),
                    _ => (null, 1f),
                };
                // the mesh's UVs are in metres: the finish repeats every tile metres, like on the walls and floors
                s_Block.Clear();
                if (mat != null)
                {
                    mats[k] = mat;
                    float t = 1f / Mathf.Max(0.05f, tile);
                    s_Block.SetVector(BaseMapSt, new Vector4(t, t, 0f, 0f));
                }
                r.SetPropertyBlock(s_Block, k);
            }
            r.sharedMaterials = mats;
        }
    }
}
