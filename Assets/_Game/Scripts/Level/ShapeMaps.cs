using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Which renderers show a model with baked maps of its own (Tools/Blender/shapebake.py; the maps are slices of the
    /// arrays of <see cref="ShapeLibrary"/>). A renderer is told its slice by the mesh it shows NOW — the model of a prop
    /// changes with the visual style and the house, and each variant has its own maps or none: the dressing of a renderer
    /// (VisualStyles) calls <see cref="Tag"/> once it has chosen the mesh and the materials. The slice goes into property
    /// blocks of the renderer's MATERIALS (never the renderer's own block) while the part is drawn by itself, and into the
    /// vertices once it is merged with the furniture round it (<see cref="MeshBatcher"/>). The renderer's own block stays
    /// what it always was — the sign of a part that draws itself (a drawer that moves, a shade that glows by its bulb):
    /// when the slice lived there, every such part with maps was merged into the room and stood still while the real one
    /// moved unseen. What glows (a screen, a lamp) or is printed takes no maps: nothing of the shape shows through.
    /// Picture only.
    /// </summary>
    public static class ShapeMaps
    {
        static readonly int SliceId = Shader.PropertyToID("_ShapeSlice"), UvId = Shader.PropertyToID("_ShapeUv"), InVerticesId = Shader.PropertyToID("_ShapeInVertices");
        /// <summary>The renderers that were told a slice, the UV set its maps lie over, and how many of their materials may carry a block of ours.</summary>
        static readonly Dictionary<Renderer, (int slice, int channel, int materials)> s_Slices = new();
        static readonly MaterialPropertyBlock s_Block = new();
        static readonly List<Material> s_Materials = new();

        /// <summary>The slice of a part's own maps (−1: it has none): a part drawn merged carries it in its vertices.</summary>
        public static int SliceOf(Renderer r) => r != null && s_Slices.TryGetValue(r, out var entry) ? entry.slice : -1;

        /// <summary>The UV set of the part's mesh its maps lie over (1: the second, 2: the third).</summary>
        public static int ChannelOf(Renderer r) => r != null && s_Slices.TryGetValue(r, out var entry) ? entry.channel : 1;

        /// <summary>A part by its name takes maps (pure): not what glows (Eye…) and not what is printed or shines through a picture.</summary>
        public static bool Takes(string part) =>
            !(part.StartsWith("Eye") || part.StartsWith("Keys") || part.StartsWith("Print") || part.StartsWith("Glow") || part.StartsWith("Lit") || part.StartsWith("Pane"));

        /// <summary>Tells a renderer the slice of the mesh it shows (or that it has none any more): after its materials are chosen.</summary>
        public static void Tag(Renderer r, ShapeLibrary library)
        {
            if (r == null || library == null) return;
            int slice = -1, channel = 1;
            if (Takes(r.name) && r.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null) (slice, channel) = library.Of(filter.sharedMesh);
            bool had = s_Slices.TryGetValue(r, out var entry);
            if (slice < 0)
            {
                if (!had) return;
                s_Slices.Remove(r);
                for (int i = 0; i < entry.materials; i++) r.SetPropertyBlock(null, i);
                return;
            }
            library.Bind();
            s_Slices[r] = (slice, channel, Write(r, slice, channel, had ? entry.materials : 0));
        }

        /// <summary>Forgets the renderers that are gone (a house was taken down).</summary>
        public static void Prune()
        {
            s_Gone.Clear();
            foreach (var r in s_Slices.Keys)
                if (r == null) s_Gone.Add(r);
            foreach (var r in s_Gone) s_Slices.Remove(r);
            s_Gone.Clear();
        }

        static readonly List<Renderer> s_Gone = new();

        /// <summary>A mesh merged of many models: its vertices name each model's maps, and its renderer says that they do.</summary>
        public static void MarkMerged(Renderer r)
        {
            r.GetPropertyBlock(s_Block);
            s_Block.SetFloat(InVerticesId, 1f);
            r.SetPropertyBlock(s_Block);
        }

        /// <summary>Every renderer under a root (the tools that show a prop without the game's styles).</summary>
        public static void Tag(GameObject root, ShapeLibrary library)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) Tag(r, library);
        }

        /// <summary>
        /// The slice on every material that reads it (the others get none: a lamp's shade keeps its own shader's block).
        /// A material's block stands in place of the renderer's for that material, so what the renderer's holds now is
        /// carried along. Returns how many materials there are.
        /// </summary>
        static int Write(Renderer r, int slice, int channel, int before)
        {
            r.GetSharedMaterials(s_Materials);
            int count = s_Materials.Count;
            for (int i = 0; i < Mathf.Max(count, before); i++)
            {
                var m = i < count ? s_Materials[i] : null;
                if (m == null || !m.HasProperty(SliceId))
                {
                    if (i < before) r.SetPropertyBlock(null, i);
                    continue;
                }
                r.GetPropertyBlock(s_Block);
                s_Block.SetFloat(SliceId, slice + 1); // (the slice + 1: zero, what a value lost on the way reads as, is "none")
                s_Block.SetFloat(UvId, channel);
                r.SetPropertyBlock(s_Block, i);
            }
            s_Materials.Clear();
            return count;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => s_Slices.Clear();
    }
}
