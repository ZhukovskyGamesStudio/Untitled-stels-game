using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// How smooth and how metal a material is on the whole: its numbers, or, where a mask map carries them, the map's
    /// average. Tells which materials mirror their room (<see cref="RoomProbes.Mirrors"/>) — the asset builder switches the
    /// room's picture off on the rest, and a test holds every material to it.
    /// </summary>
    public static class MaterialGloss
    {
        /// <summary>The shader of things' surfaces: its smoothness and metal always come from the mask.</summary>
        public const string SurfaceShader = "Stealth/Surface";
        /// <summary>URP's lit shader.</summary>
        public const string LitShader = "Universal Render Pipeline/Lit";
        /// <summary>URP's switch: the material takes no reflection probe (screen reflections stay).</summary>
        public const string NoMirrorKeyword = "_ENVIRONMENTREFLECTIONS_OFF";
        /// <summary>The switch's property on the material (1 — mirrors).</summary>
        public const string MirrorProperty = "_EnvironmentReflections";

        /// <summary>Whether the rule is about this material at all: a lit one of ours.</summary>
        public static bool Lit(Material m) => m != null && m.shader != null && (m.shader.name == SurfaceShader || m.shader.name == LitShader);

        /// <summary>The material's smoothness and metal on the whole, 0..1.</summary>
        public static (float smoothness, float metal) Of(Material m)
        {
            float smoothness = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0f;
            float metal = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
            bool surface = m.shader.name == SurfaceShader;
            if ((surface || m.IsKeywordEnabled("_METALLICSPECGLOSSMAP")) && m.HasProperty("_MetallicGlossMap"))
            {
                var mask = m.GetTexture("_MetallicGlossMap");
                var mean = mask != null ? Mean(mask) : Color.white; // (no map: the shader reads white)
                smoothness *= mean.a;
                metal = mean.r;
            }
            return (smoothness, metal);
        }

#if UNITY_EDITOR
        static readonly System.Collections.Generic.Dictionary<string, Color> s_Means = new();
#endif

        /// <summary>
        /// A texture's average colour, as stored. In the editor — from its file (the asset build and the tests run with no
        /// graphics device: nothing can be drawn and read back there); in a player — drawn small and read back.
        /// </summary>
        public static Color Mean(Texture texture)
        {
#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                if (s_Means.TryGetValue(path, out var known)) return known;
                var file = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                var mean = Color.white;
                if (file.LoadImage(System.IO.File.ReadAllBytes(path)))
                {
                    var pixels = file.GetPixels32();
                    long r = 0, g = 0, b = 0, a = 0, count = 0;
                    for (int k = 0; k < pixels.Length; k += 7) // (a stride that is no divisor of a power of two: no column is skipped)
                    {
                        var c = pixels[k];
                        r += c.r; g += c.g; b += c.b; a += c.a;
                        count++;
                    }
                    float to = 1f / (255f * Mathf.Max(1, count));
                    mean = new Color(r * to, g * to, b * to, a * to);
                }
                Object.DestroyImmediate(file);
                s_Means[path] = mean;
                return mean;
            }
#endif
            const int n = 8;
            var rt = RenderTexture.GetTemporary(n, n, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var was = RenderTexture.active;
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            var read = new Texture2D(n, n, TextureFormat.RGBA32, false, true);
            read.ReadPixels(new Rect(0, 0, n, n), 0, 0);
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(rt);
            var sum = Vector4.zero;
            foreach (var c in read.GetPixels()) sum += (Vector4)c;
            if (Application.isPlaying) Object.Destroy(read);
            else Object.DestroyImmediate(read);
            return sum / (n * n);
        }
    }
}
