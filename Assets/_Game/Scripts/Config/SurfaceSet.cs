using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Configs
{
    /// <summary>
    /// What the things of a house are made of (Data/Styles/Surfaces/*.asset, filled by the asset builder —
    /// AssetBuilder.Surfaces — from the textures of Art/Textures/Surfaces): a surface for every material the models carry,
    /// and for the parts of single models where one material stands for different things (the same white is a basin's
    /// glaze, a copier's plastic and a stack of paper). A <see cref="HouseLook"/> says which set its house wears. Picture only.
    /// </summary>
    [CreateAssetMenu(menuName = "Stealth/Surface Set")]
    public class SurfaceSet : ScriptableObject
    {
        [Tooltip("Every part painted with a material")] public MaterialSwap[] Materials = new MaterialSwap[0];
        [Tooltip("A part of one model (its mesh), before the rule of its material")] public PartSurface[] Parts = new PartSurface[0];

        Dictionary<Material, Material> m_Materials;
        Dictionary<(Mesh, Material), Material> m_Parts;

        void OnEnable() => Forget();
        void OnValidate() => Forget();

        /// <summary>The lists changed: they are read anew.</summary>
        public void Forget()
        {
            m_Materials = null;
            m_Parts = null;
        }

        /// <summary>The surface of a part: its mesh (the model's own, not a style's) and the material it carries. False: it keeps its own.</summary>
        public bool TryGet(Mesh mesh, Material material, out Material surface)
        {
            if (m_Materials == null)
            {
                m_Materials = new Dictionary<Material, Material>();
                foreach (var s in Materials ?? new MaterialSwap[0])
                    if (s.From != null && s.To != null) m_Materials[s.From] = s.To;
                m_Parts = new Dictionary<(Mesh, Material), Material>();
                foreach (var p in Parts ?? new PartSurface[0])
                    if (p.Mesh != null && p.From != null && p.To != null) m_Parts[(p.Mesh, p.From)] = p.To;
            }
            surface = null;
            if (material == null) return false;
            return (mesh != null && m_Parts.TryGetValue((mesh, material), out surface)) || m_Materials.TryGetValue(material, out surface);
        }
    }

    /// <summary>The surface of one part of one model.</summary>
    [Serializable]
    public struct PartSurface
    {
        [Tooltip("The part's mesh (in its model's FBX)")] public Mesh Mesh;
        [Tooltip("The material it carries")] public Material From;
        public Material To;
    }
}
