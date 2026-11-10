using System;
using UnityEngine;

namespace Stealth.Configs
{
    /// <summary>
    /// Prefabs of the style architecture (Prefabs/StyleKit, built by the asset builder from Art/Models/StyleKit):
    /// one per <see cref="Level.DecorKind"/>, plus the quad that carries floor and wall finishes. Decoration only:
    /// no colliders, no shadows.
    /// </summary>
    public class StyleKit : ScriptableObject
    {
        [Tooltip("The models of which family the pieces are shaped like")] public ModelFamily Family;
        [Tooltip("Indexed by DecorKind; the finish kinds use the Finish quad")]
        public GameObject[] Pieces = new GameObject[0];
        public GameObject Finish;
    }

    /// <summary>Every model mesh and its variant in one family (Art/Models/Styles/&lt;Family&gt;).</summary>
    [Serializable]
    public class MeshVariants
    {
        public ModelFamily Family;
        public Mesh[] From = new Mesh[0];
        public Mesh[] To = new Mesh[0];
    }

    /// <summary>Models made for one style only (its own guard robot): they win over the family's version.</summary>
    [Serializable]
    public class StyleMeshes
    {
        public VisualStyle Style;
        [Tooltip("-1 = every object; otherwise only objects registered with this variant (a player's department slot)")]
        public int Variant = -1;
        public Mesh[] From = new Mesh[0];
        public Mesh[] To = new Mesh[0];
    }

    /// <summary>Every material of the world and its version in one family (flat, glossy plastic, pixel, PBR…).</summary>
    [Serializable]
    public class MaterialVariants
    {
        public ModelFamily Family;
        public Material[] From = new Material[0];
        public Material[] To = new Material[0];
    }

    /// <summary>A model replaced as a whole in one family (a CC0 scanned desk for ours): placed in the model's space.</summary>
    [Serializable]
    public class PropReplacement
    {
        public ModelFamily Family;
        [Tooltip("The Body mesh of our model: where it is, the replacement goes")] public Mesh Key;
        public GameObject Prefab;
        public Vector3 Position;
        public Vector3 Scale = Vector3.one;
    }
}
