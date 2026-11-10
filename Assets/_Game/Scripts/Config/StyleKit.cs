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
        [Tooltip("Indexed by DecorKind; the finish kinds use the Finish quad")]
        public GameObject[] Pieces = new GameObject[0];
        public GameObject Finish;
    }

    /// <summary>Every model mesh and its reshaped variant for one treatment (Art/Models/Styles/&lt;Treatment&gt;).</summary>
    [Serializable]
    public class MeshVariants
    {
        public ModelTreatment Treatment;
        public Mesh[] From = new Mesh[0];
        public Mesh[] To = new Mesh[0];
    }
}
