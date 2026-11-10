using System;
using UnityEngine;

namespace Stealth.Configs
{
    /// <summary>
    /// One look of the game (Data/Styles/*.asset): colour grading, the whole-screen filter (pixels, outlines, cel bands,
    /// paint, hatching, 1-bit, camcorder, neon), the air (ambient, fog) and the palette of the world. Purely visual — robots
    /// see exactly the same in every style. Applied by <see cref="Level.VisualStyles"/>; the defaults are the current look.
    /// </summary>
    [CreateAssetMenu(menuName = "Stealth/Visual Style")]
    public class VisualStyle : ScriptableObject
    {
        [Tooltip("Name shown to the player and in the comparison")]
        public string Title = "Как сейчас";
        [Tooltip("Games and works that inspired the style")]
        public string References = "";
        [TextArea(2, 5), Tooltip("The idea in two sentences")]
        public string Pitch = "";

        public StyleGrade Grade = new();
        public StyleScreen Screen = new();
        public StyleWorld World = new();
        public StylePalette Palette = new();
    }

    public enum StyleTonemap { Neutral, Aces, None }

    /// <summary>Colour grading on the URP post-processing volume.</summary>
    [Serializable]
    public class StyleGrade
    {
        [Tooltip("Exposure, stops")] public float Exposure = 0.1f;
        [Range(-100, 100)] public float Contrast = 8f;
        [Range(-100, 100)] public float Saturation;
        public Color ColorFilter = Color.white;
        [Range(-180, 180)] public float HueShift;
        [Tooltip("Split toning: colour of the shadows (grey = off)")] public Color SplitShadows = Color.grey;
        [Tooltip("Split toning: colour of the highlights (grey = off)")] public Color SplitHighlights = Color.grey;
        [Range(-100, 100)] public float SplitBalance;
        [Range(-100, 100)] public float Temperature;
        [Range(-100, 100)] public float Tint;
        public StyleTonemap Tonemap = StyleTonemap.Neutral;
        public float BloomIntensity = 0.9f;
        public float BloomThreshold = 1f;
        [Range(0, 1)] public float BloomScatter = 0.7f;
        public Color BloomTint = Color.white;
        [Range(0, 1)] public float Vignette = 0.22f;
        public Color VignetteColor = Color.black;
        [Range(0, 1)] public float ChromaticAberration;
        [Range(0, 1)] public float FilmGrain;
        [Range(-1, 1)] public float LensDistortion;
    }

    /// <summary>The whole-screen filter (Art/Shaders/StyleScreen.shader). Zero = that part is off.</summary>
    [Serializable]
    public class StyleScreen
    {
        [Tooltip("Pixel block size at 720p (1 = full resolution)")] public float PixelSize = 1f;
        [Tooltip("Cel shading: number of light bands (0 = off)")] public float CelBands;
        [Range(0, 0.5f)] public float CelSoftness = 0.05f;
        [Range(0, 1), Tooltip("How much of the cel bands is mixed in")] public float CelMix = 1f;
        [Tooltip("Colours per channel (0 = off): 6 ≈ VGA, 32 ≈ PS1")] public float ColorLevels;
        [Tooltip("Ordered dither for the colour levels")] public bool OrderedDither;
        [Tooltip("Painterly filter radius, pixels (0 = off)")] public float PaintRadius;

        [Header("Outlines")]
        [Range(0, 1)] public float OutlineStrength;
        public float OutlineWidth = 1f;
        public float OutlineDepth = 8f;
        public float OutlineNormal = 2f;
        public Color OutlineColor = Color.black;
        [Range(0, 1), Tooltip("0 = ink colour, 1 = a darker shade of the surface")] public float OutlineSurfaceTint;

        [Header("Hatching and paper")]
        [Range(0, 1)] public float HatchStrength;
        public float HatchSpacing = 5f;
        [Range(0, 1), Tooltip("Brightness below which hatching starts")] public float HatchStart = 0.4f;
        [Range(0, 1)] public float PaperGrain;
        [Range(0, 1)] public float PaperTint;
        public Color PaperColor = Color.white;

        [Header("1-bit")]
        public bool OneBit;
        public Color Ink = Color.black;
        public Color Light = Color.white;
        public float DotSize = 1f;
        [Range(-0.5f, 0.5f)] public float DitherBias;

        [Header("Camcorder")]
        [Range(0, 1)] public float Scanlines;
        public float ChromaShift;
        [Range(0, 1)] public float Noise;
        public float Wobble;
        public bool RecDot;

        [Header("Neon edges")]
        public float NeonStrength;
        [Range(0, 1), Tooltip("How much of the normal picture stays under the edges")] public float NeonKeep = 1f;
        public float NeonFar = 20f;
        public Color NeonNear = Color.cyan;
        public Color NeonFarColor = Color.magenta;
    }

    /// <summary>The air: ambient light and the fog (the gas keeps its density — it is gameplay).</summary>
    [Serializable]
    public class StyleWorld
    {
        [Tooltip("Ambient light (black = night, only the lamps light)")] public Color Ambient = Color.black;
        public Color IndoorFog = new(0.012f, 0.014f, 0.02f);
        public float IndoorFogDensity = 0.03f;
        public Color GasFog = new(0.33f, 0.37f, 0.3f);
    }

    /// <summary>How the colours of the world change, by kind of material.</summary>
    [Serializable]
    public class StylePalette
    {
        public StyleTint Building = new();
        public StyleTint Furniture = new();
        public StyleTint Robots = new();
        public StyleTint Players = new();
        public StyleTint Loot = new();
    }

    [Serializable]
    public class StyleTint
    {
        public Color Tint = Color.white;
        [Range(0, 1), Tooltip("0 = own colour, 1 = the tint")] public float Amount;
        [Tooltip("Saturation multiplier")] public float Saturation = 1f;
        [Tooltip("Brightness multiplier")] public float Brightness = 1f;
        [Tooltip("Smoothness override (-1 = keep)")] public float Smoothness = -1f;

        public bool IsIdentity => Amount <= 0f && Mathf.Approximately(Saturation, 1f) && Mathf.Approximately(Brightness, 1f) && Smoothness < 0f;

        /// <summary>The colour of a material in this style (pure).</summary>
        public Color Apply(Color c)
        {
            var o = Color.Lerp(c, Tint, Amount);
            float grey = 0.2126f * o.r + 0.7152f * o.g + 0.0722f * o.b;
            o = Color.LerpUnclamped(new Color(grey, grey, grey, o.a), o, Saturation) * Brightness;
            o.a = c.a;
            return o;
        }
    }
}
