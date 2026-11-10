using System.Collections.Generic;
using Stealth.Configs;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Stealth.Level
{
    /// <summary>
    /// Puts a <see cref="VisualStyle"/> on screen, on this peer only: the post-processing volume of the scene, the
    /// whole-screen filter (globals of Stealth/StyleScreen), ambient and fog, and the colours of the world's materials
    /// (originals are kept and restored). Purely visual: robots read the light through <see cref="LightSource"/> and the
    /// gameplay ambient (<see cref="GameplayAmbient"/>), which no style changes.
    /// </summary>
    public static class VisualStyles
    {
        static StyleConfig s_Config;
        static VisualStyle s_Current;
        static VolumeProfile s_BaseProfile, s_Profile;
        static Volume s_Volume;
        static bool s_Active;
        static Color s_BaseAmbient;
        static readonly Dictionary<Material, (Color color, float smoothness)> s_Originals = new();
        static readonly VisualStyle s_Default = null;

        /// <summary>Style on screen now; null = the look the game is built with.</summary>
        public static VisualStyle Current => s_Current;

        /// <summary>The air of the current style (the defaults when there is none).</summary>
        public static StyleWorld World => s_Current != null ? s_Current.World : s_DefaultWorld;
        static readonly StyleWorld s_DefaultWorld = new();

        /// <summary>The ambient light robots reckon with: the game's own, whatever a style shows.</summary>
        public static Color GameplayAmbient => s_Active ? s_BaseAmbient : RenderSettings.ambientLight;

        public static StyleConfig Config
        {
            get
            {
                if (s_Config == null) s_Config = Tuning.Styles;
                return s_Config;
            }
        }

        public static IReadOnlyList<VisualStyle> All => Config != null ? Config.Styles : System.Array.Empty<VisualStyle>();

        /// <summary>Finds a style by asset name or title (case-insensitive); null if there is none.</summary>
        public static VisualStyle Find(string name)
        {
            foreach (var s in All)
                if (s != null && (string.Equals(s.name, name, System.StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(s.Title, name, System.StringComparison.OrdinalIgnoreCase)))
                    return s;
            return null;
        }

        /// <summary>The next style in the list (wraps around).</summary>
        public static VisualStyle Next()
        {
            var all = All;
            if (all.Count == 0) return null;
            int i = 0;
            for (int k = 0; k < all.Count; k++) if (all[k] == s_Current) i = k + 1;
            var next = all[i % all.Count];
            Apply(next);
            return next;
        }

        /// <summary>Applies a style (null = the base look). Safe to call again after a level is built.</summary>
        public static void Apply(VisualStyle style)
        {
            if (!s_Active)
            {
                s_BaseAmbient = RenderSettings.ambientLight;
                Application.quitting += Restore;
            }
            s_Active = true;
            s_Current = style;
            ApplyVolume(style);
            ApplyScreen(style != null ? style.Screen : null);
            ApplyWorld(World);
            ApplyPalette(style != null ? style.Palette : null);
            ApplyGeometry(style != null ? style.Geometry : null);
        }

        /// <summary>A level was (re)built: the game's atmosphere was reset, put the style back on.</summary>
        public static void Reapply()
        {
            if (!s_Active) return;
            s_BaseAmbient = RenderSettings.ambientLight;
            Apply(s_Current);
        }

        /// <summary>Back to the base look; the materials get their own colours back (they are assets in the editor).</summary>
        public static void Restore()
        {
            s_Treatment = ModelTreatment.None;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>()) Dress(mf);
            StyleDecor.Clear();
            foreach (var (mat, orig) in s_Originals)
            {
                if (mat == null) continue;
                SetColor(mat, orig.color);
                if (mat.HasProperty(SmoothnessId)) mat.SetFloat(SmoothnessId, orig.smoothness);
            }
            s_Originals.Clear();
            if (s_Volume != null && s_BaseProfile != null) s_Volume.sharedProfile = s_BaseProfile;
            Shader.SetGlobalFloat("_StyOn", 0f);
            if (s_Active) RenderSettings.ambientLight = s_BaseAmbient;
            s_Current = s_Default;
            s_Active = false;
        }

        /// <summary>Puts the fog of the style on at once (the player's fog otherwise eases towards it over a second).</summary>
        public static void SnapFog(bool outside)
        {
            var w = World;
            RenderSettings.fogColor = outside ? w.GasFog : w.IndoorFog;
            RenderSettings.fogDensity = outside ? LightMath.GasFogDensity : w.IndoorFogDensity;
        }

        // ------------------------------------------------------------------ geometry

        static ModelTreatment s_Treatment;
        static Dictionary<Mesh, Mesh>[] s_Variants;
        static readonly Dictionary<Mesh, Mesh> s_OriginalOf = new();

        static void ApplyGeometry(StyleGeometry g)
        {
            s_Treatment = g != null ? g.Treatment : ModelTreatment.None;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>()) Dress(mf);
            if (Application.isPlaying) StyleDecor.Rebuild(g, Config != null ? Config.Kit : null);
        }

        static void LoadVariants()
        {
            if (s_Variants != null) return;
            s_Variants = new Dictionary<Mesh, Mesh>[System.Enum.GetValues(typeof(ModelTreatment)).Length];
            for (int i = 0; i < s_Variants.Length; i++) s_Variants[i] = new Dictionary<Mesh, Mesh>();
            if (Config == null || Config.Meshes == null) return;
            foreach (var set in Config.Meshes)
                for (int i = 0; i < set.From.Length && i < set.To.Length; i++)
                {
                    if (set.From[i] == null || set.To[i] == null) continue;
                    s_Variants[(int)set.Treatment][set.From[i]] = set.To[i];
                    s_OriginalOf[set.To[i]] = set.From[i];
                }
        }

        /// <summary>
        /// Gives a freshly spawned object (robot, item, player) the models of the current style. Cheap and safe to call
        /// for anything: meshes without a variant stay as they are.
        /// </summary>
        public static void Dress(GameObject go)
        {
            if (!s_Active || go == null) return;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) Dress(mf);
        }

        static void Dress(MeshFilter mf)
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) return;
            LoadVariants();
            var original = s_OriginalOf.TryGetValue(mesh, out var o) ? o : mesh;
            var target = s_Treatment != ModelTreatment.None && s_Variants[(int)s_Treatment].TryGetValue(original, out var v) ? v : original;
            if (target != mesh) mf.sharedMesh = target;
        }

        // ------------------------------------------------------------------ post-processing

        static void ApplyVolume(VisualStyle style)
        {
            if (s_Volume == null)
            {
                foreach (var v in Object.FindObjectsByType<Volume>())
                    if (v.isGlobal) { s_Volume = v; break; }
                if (s_Volume == null) return;
                s_BaseProfile = s_Volume.sharedProfile;
            }
            if (style == null) { s_Volume.sharedProfile = s_BaseProfile; return; }
            if (s_Profile == null) s_Profile = Object.Instantiate(s_BaseProfile);
            var g = style.Grade;

            var bloom = Get<Bloom>(s_Profile);
            bloom.intensity.Override(g.BloomIntensity);
            bloom.threshold.Override(g.BloomThreshold);
            bloom.scatter.Override(g.BloomScatter);
            bloom.tint.Override(g.BloomTint);
            bloom.active = g.BloomIntensity > 0f;

            Get<Tonemapping>(s_Profile).mode.Override(g.Tonemap switch
            {
                StyleTonemap.Aces => TonemappingMode.ACES,
                StyleTonemap.None => TonemappingMode.None,
                _ => TonemappingMode.Neutral,
            });

            var vignette = Get<Vignette>(s_Profile);
            vignette.intensity.Override(g.Vignette);
            vignette.color.Override(g.VignetteColor);

            var color = Get<ColorAdjustments>(s_Profile);
            color.postExposure.Override(g.Exposure);
            color.contrast.Override(g.Contrast);
            color.saturation.Override(g.Saturation);
            color.colorFilter.Override(g.ColorFilter);
            color.hueShift.Override(g.HueShift);

            var split = Get<SplitToning>(s_Profile);
            split.shadows.Override(g.SplitShadows);
            split.highlights.Override(g.SplitHighlights);
            split.balance.Override(g.SplitBalance);

            var wb = Get<WhiteBalance>(s_Profile);
            wb.temperature.Override(g.Temperature);
            wb.tint.Override(g.Tint);

            var ca = Get<ChromaticAberration>(s_Profile);
            ca.intensity.Override(g.ChromaticAberration);

            var grain = Get<FilmGrain>(s_Profile);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(g.FilmGrain);
            grain.response.Override(0.6f);

            Get<LensDistortion>(s_Profile).intensity.Override(g.LensDistortion);

            s_Volume.sharedProfile = s_Profile;
        }

        static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var c)) c = profile.Add<T>(true);
            c.active = true;
            return c;
        }

        // ------------------------------------------------------------------ whole-screen filter

        static void ApplyScreen(StyleScreen s)
        {
            Shader.SetGlobalFloat("_StyOn", s != null ? 1f : 0f);
            if (s == null) return;
            Shader.SetGlobalVector("_StyPixel", new Vector4(s.PixelSize, 0, 0, 0));
            Shader.SetGlobalVector("_StyCel", new Vector4(s.CelBands, s.ColorLevels, s.CelSoftness, s.CelMix));
            Shader.SetGlobalVector("_StyOutline", new Vector4(s.OutlineStrength, s.OutlineWidth, s.OutlineDepth, s.OutlineNormal));
            var oc = s.OutlineColor;
            Shader.SetGlobalVector("_StyOutlineColor", new Vector4(oc.r, oc.g, oc.b, s.OutlineSurfaceTint));
            Shader.SetGlobalVector("_StyPaint", new Vector4(s.PaintRadius, 0, 0, 0));
            Shader.SetGlobalVector("_StyHatch", new Vector4(s.HatchStrength, s.HatchSpacing, s.HatchStart, 0));
            Shader.SetGlobalVector("_StyPaper", new Vector4(s.PaperGrain, s.PaperTint, 0, 0));
            Shader.SetGlobalColor("_StyPaperColor", s.PaperColor);
            Shader.SetGlobalVector("_StyDither", new Vector4(s.OneBit ? 1 : 0, s.DotSize, s.DitherBias, s.OrderedDither ? 1 : 0));
            Shader.SetGlobalColor("_StyInk", s.Ink);
            Shader.SetGlobalColor("_StyLight", s.Light);
            Shader.SetGlobalVector("_StyVhs", new Vector4(s.Scanlines, s.ChromaShift, s.Noise, s.Wobble));
            Shader.SetGlobalVector("_StyRec", new Vector4(s.RecDot ? 1 : 0, 0, 0, 0));
            Shader.SetGlobalVector("_StyGlow", new Vector4(s.NeonStrength, s.NeonKeep, s.NeonFar, 0));
            Shader.SetGlobalColor("_StyGlowA", s.NeonNear);
            Shader.SetGlobalColor("_StyGlowB", s.NeonFarColor);
            Shader.SetGlobalVector("_StyExtra", new Vector4(s.OutlineInvert ? 1 : 0, s.ColorOnlyLights, s.Visor, s.Viewfinder ? 1 : 0));
        }

        // ------------------------------------------------------------------ air

        static void ApplyWorld(StyleWorld w)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = s_Current != null ? w.Ambient : s_BaseAmbient;
        }

        // ------------------------------------------------------------------ palette

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        static void ApplyPalette(StylePalette p)
        {
            var c = Config;
            if (c == null) return;
            Recolor(c.Building, p?.Building);
            Recolor(c.Furniture, p?.Furniture);
            Recolor(c.Robots, p?.Robots);
            Recolor(c.Players, p?.Players);
            Recolor(c.Loot, p?.Loot);
        }

        static void Recolor(Material[] materials, StyleTint tint)
        {
            if (materials == null) return;
            foreach (var mat in materials)
            {
                if (mat == null || !mat.HasProperty(BaseColorId)) continue;
                if (!s_Originals.TryGetValue(mat, out var orig))
                {
                    orig = (mat.GetColor(BaseColorId), mat.HasProperty(SmoothnessId) ? mat.GetFloat(SmoothnessId) : 0f);
                    s_Originals[mat] = orig;
                }
                SetColor(mat, tint == null ? orig.color : tint.Apply(orig.color));
                if (mat.HasProperty(SmoothnessId))
                    mat.SetFloat(SmoothnessId, tint != null && tint.Smoothness >= 0f ? tint.Smoothness : orig.smoothness);
            }
        }

        static void SetColor(Material mat, Color c)
        {
            mat.SetColor(BaseColorId, c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        }
    }
}
