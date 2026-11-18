using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A piece of furniture dressed differently every time (prefabs built by AssetBuilder: the coat stand and the hall hooks
    /// with their garments, the shelves with their books): each slot (a hook, a place on a shelf) holds all its variants as
    /// children, hidden; <see cref="Dress"/> shows one of them (or none) and paints it from <see cref="Palette"/> — from the
    /// house's seed and the piece's index, so every peer sees the same coats. Runs once, when the level is built, before the
    /// furniture is merged (its renderers merge like any other).
    /// </summary>
    public class PropVariants : MonoBehaviour
    {
        [Tooltip("The slots: each child of a slot is one variant of what may hang or stand there.")]
        public Transform[] Slots = new Transform[0];
        [Tooltip("Chance that a slot stays empty.")]
        [Range(0f, 1f)] public float Empty = 0.25f;
        [Tooltip("Colours the variants' Body parts are painted in (one per variant, picked at random).")]
        public Material[] Palette = new Material[0];
        [Tooltip("Colours of the variants' Accent parts (a book's pages; empty: as modelled).")]
        public Material[] AccentPalette = new Material[0];
        [Tooltip("Every part painted on its own (a row of books in many colours) instead of the whole variant in one.")]
        public bool PerPart;
        [Tooltip("The piece's frame (shelves): while a style shows a model of its own for it, the slots hide and Own shows.")]
        public MeshFilter Frame;
        [Tooltip("The model's own contents (its baked-in books), shown only where a style's own model replaces the frame.")]
        public Renderer[] Own = new Renderer[0];

        void OnEnable() { VisualStyles.GeometryChanged += Refresh; Refresh(); }
        void OnDisable() => VisualStyles.GeometryChanged -= Refresh;

        /// <summary>
        /// A style with a model of its own for the frame (its own shelf heights, its own books): its contents show, the slots
        /// hide; otherwise the slots' variants show (dressed in the style's materials like everything else).
        /// </summary>
        void Refresh()
        {
            bool own = Frame != null && VisualStyles.IsOwnStyleMesh(Frame.sharedMesh);
            foreach (var r in Own) if (r != null) r.enabled = own;
            foreach (var slot in Slots) if (slot != null) slot.gameObject.SetActive(!own);
            if (!own) VisualStyles.Dress(gameObject);
        }

        /// <summary>Shows one variant (or none) in every slot, from <paramref name="seed"/>.</summary>
        public void Dress(int seed)
        {
            var rng = new System.Random(seed);
            foreach (var slot in Slots)
            {
                if (slot == null) continue;
                int n = slot.childCount;
                bool empty = rng.NextDouble() < Empty;
                int pick = rng.Next(Mathf.Max(1, n));
                int paint = rng.Next(Mathf.Max(1, Palette.Length));
                int accent = rng.Next(Mathf.Max(1, AccentPalette.Length));
                var parts = new System.Random(rng.Next());
                for (int i = 0; i < n; i++)
                {
                    var child = slot.GetChild(i).gameObject;
                    bool on = !empty && i == pick;
                    child.SetActive(on);
                    if (!on) continue;
                    foreach (var r in child.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (r.name.StartsWith("Accent")) { if (AccentPalette.Length > 0) r.sharedMaterial = AccentPalette[accent]; }
                        else if (!r.name.StartsWith("Eye") && Palette.Length > 0) r.sharedMaterial = Palette[PerPart ? parts.Next(Palette.Length) : paint];
                    }
                }
            }
        }
    }
}
