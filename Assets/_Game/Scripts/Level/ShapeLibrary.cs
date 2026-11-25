using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Stealth.Level
{
    /// <summary>
    /// Every model's own baked maps (the builder lists them: AssetBuilder.Shapes), a model a slice in the order of
    /// <see cref="Models"/>. When the game starts they are copied into two texture arrays, one pair of textures for the
    /// whole game — so the furniture of a room can still be merged into one mesh whatever models it is made of
    /// (<see cref="ShapeMaps"/>). The copy is the graphics card's (compressed blocks as they are): all the pictures of
    /// one kind must be of one size and format, which the builder sees to.
    /// </summary>
    [CreateAssetMenu(menuName = "Stealth/Shape Library", fileName = "ShapeLibrary")]
    public class ShapeLibrary : ScriptableObject
    {
        [Tooltip("Normals of the models' detailed versions: RG, tangent space of the second UV set.")]
        public Texture2D[] Normals = System.Array.Empty<Texture2D>();
        [Tooltip("R occlusion (1 open), G outer edges (0 an edge), B cavities (0 a tight corner).")]
        public Texture2D[] Masks = System.Array.Empty<Texture2D>();
        [Tooltip("The model files the slices belong to, in order (Tools/Blender/shapebake.py's names).")]
        public string[] Models = System.Array.Empty<string>();
        [Tooltip("Every mesh of those models…")]
        public Mesh[] Meshes = System.Array.Empty<Mesh>();
        [Tooltip("…and the slice of each (the same order).")]
        public int[] Slices = System.Array.Empty<int>();
        [Tooltip("…and which of the mesh's UV sets the maps were baked over (1: the second, 2: the third — a part that came with a spare set of its own has the baked one after it).")]
        public int[] Channels = System.Array.Empty<int>();
        [Tooltip("The surface textures that came at twice their old size (2K scans): shown a detail level down when the player asks for the old models.")]
        public Texture2D[] HighRes = System.Array.Empty<Texture2D>();

        static readonly int NormalsId = Shader.PropertyToID("_ShapeNormals"), MasksId = Shader.PropertyToID("_ShapeMasks"), BoundId = Shader.PropertyToID("_ShapesBound");
        static ShapeLibrary s_Bound;
        static Texture2DArray s_Normals, s_Masks;
        static bool s_Detail = true;

        /// <summary>
        /// The models and surfaces at their new detail (Settings → Look: "new models"). Off, the 2K surface textures are
        /// read one level down — as they were at 1K (the models' own maps are switched off by the shader's _ShapesOff).
        /// </summary>
        public static bool Detail
        {
            get => s_Detail;
            set
            {
                if (s_Detail == value) return;
                s_Detail = value;
                if (s_Bound != null) s_Bound.Sharpen();
            }
        }

        void Sharpen()
        {
            foreach (var t in HighRes)
                if (t != null) t.mipMapBias = s_Detail ? 0f : 1f;
        }

        /// <summary>The slice of a model's maps, −1: it has none.</summary>
        public int SliceOf(string model) => System.Array.IndexOf(Models, model);

        System.Collections.Generic.Dictionary<Mesh, (int slice, int channel)> m_ByMesh;

        /// <summary>The slice of the maps baked for a mesh, −1: it has none (another variant of the model, a model without maps).</summary>
        public int SliceOf(Mesh mesh) => Of(mesh).slice;

        /// <summary>The slice of a mesh's maps (−1: none) and the UV set they lie over (1: the second, 2: the third).</summary>
        public (int slice, int channel) Of(Mesh mesh)
        {
            if (m_ByMesh == null)
            {
                m_ByMesh = new System.Collections.Generic.Dictionary<Mesh, (int, int)>();
                for (int i = 0; i < Meshes.Length && i < Slices.Length; i++)
                    if (Meshes[i] != null) m_ByMesh[Meshes[i]] = (Slices[i], i < Channels.Length ? Channels[i] : 1);
            }
            return mesh != null && m_ByMesh.TryGetValue(mesh, out var entry) ? entry : (-1, 1);
        }

        void OnEnable() => m_ByMesh = null;

        /// <summary>Packs the pictures into the two arrays and hands them to the shaders (once: they are global).</summary>
        public void Bind()
        {
            if (s_Bound == this) return;
            s_Bound = this;
            Sharpen();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || SystemInfo.copyTextureSupport == CopyTextureSupport.None) return; // (tests, a server: nothing draws)
            s_Normals = Pack(Normals, "ShapeNormals");
            s_Masks = Pack(Masks, "ShapeMasks");
            if (s_Normals == null || s_Masks == null) return;
            Shader.SetGlobalTexture(NormalsId, s_Normals);
            Shader.SetGlobalTexture(MasksId, s_Masks);
            Shader.SetGlobalFloat(BoundId, 1f);
        }

        static Texture2DArray Pack(Texture2D[] pictures, string name)
        {
            Texture2D first = null;
            foreach (var p in pictures) if (p != null) { first = p; break; }
            if (first == null) return null;
            var array = new Texture2DArray(first.width, first.height, pictures.Length, first.graphicsFormat, TextureCreationFlags.MipChain)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4, hideFlags = HideFlags.HideAndDontSave,
            };
            for (int i = 0; i < pictures.Length; i++)
            {
                var p = pictures[i];
                if (p == null) continue;
                if (p.width != first.width || p.height != first.height || p.graphicsFormat != first.graphicsFormat || p.mipmapCount != first.mipmapCount)
                {
                    Debug.LogWarning($"[Shapes] {p.name}: {p.width}×{p.height} {p.graphicsFormat}, the others are {first.width}×{first.height} {first.graphicsFormat} — its model goes without its maps");
                    continue;
                }
                for (int mip = 0; mip < p.mipmapCount; mip++) Graphics.CopyTexture(p, 0, mip, array, i, mip); // (level by level: the whole-element copy left the smaller levels unwritten)
            }
            return array;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            s_Bound = null;
            s_Normals = s_Masks = null;
            Shader.SetGlobalFloat(BoundId, 0f);
        }
    }
}
