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
        public StyleGeometry Geometry = new();
    }

    public enum StyleTonemap { Neutral, Aces, None }

    /// <summary>
    /// The modelling language of a style (Tools/Blender/style_families.py rebuilds every model per family; the realistic
    /// family also swaps furniture for CC0 scanned models and uses PBR materials). Append only.
    /// </summary>
    public enum ModelFamily { None, Lowpoly, Toy, Lofi, Clean, Realistic }

    /// <summary>What frames the doorways.</summary>
    public enum DoorTrim { None, Casing, Pilasters, Arch }

    /// <summary>What hangs under the ceiling.</summary>
    public enum CeilingKind { None, Beams, IBeams, Coffers, Trays }

    /// <summary>
    /// The shape of the world in a style: reshaped models and the architecture added to the house — trims, panels,
    /// columns, beams, pipes, light strips, floor and wall finishes. Decoration only: no colliders, no shadows, robots and
    /// players move and see exactly as without it.
    /// </summary>
    [Serializable]
    public class StyleGeometry
    {
        [Tooltip("Models, their materials and the shape of the architecture kit")] public ModelFamily Family;

        [Header("Walls")]
        public bool Baseboard;
        public bool Crown;
        [Tooltip("Height of the wall panels (0 = none)")] public float WainscotHeight;
        [Tooltip("Height of a chair rail (0 = none)")] public float RailHeight;
        [Tooltip("Pipes along the walls under the ceiling")] public bool Pipes;
        [Tooltip("Glowing slits in the walls every this many metres (0 = none)")] public float StripSpacing;
        public DoorTrim Doors;

        [Header("Ceiling")]
        public CeilingKind Ceiling;

        [Header("Materials (null = keep the house as it is)")]
        public Material Floor;
        [Tooltip("Metres covered by one repeat of the floor texture")] public float FloorTile = 1f;
        public Material Wall;
        [Tooltip("Metres covered by one repeat of the wall texture")] public float WallTile = 1f;
        [Tooltip("Baseboards, cornices, rails, casings")] public Material Trim;
        [Tooltip("The boards of the wall panels")] public Material Panel;
        public Material Column;
        public Material Beam;
        public Material Pipe;
        public Material Glow;
        [Tooltip("Under the ceilings (tiles, boards…)")] public Material CeilingFinish;
        public float CeilingTile = 1.2f;

        public bool HasArchitecture => CeilingFinish != null || Baseboard || Crown || WainscotHeight > 0f || RailHeight > 0f || Pipes || StripSpacing > 0f ||
                                       Doors != DoorTrim.None || Ceiling != CeilingKind.None || Floor != null || Wall != null;
    }

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
        [Tooltip("Lines are light on dark surfaces and dark on light ones (Obra Dinn)")] public bool OutlineInvert;

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
        [Tooltip("Viewfinder corner brackets")] public bool Viewfinder;
        [Range(0, 1), Tooltip("Grey world: only bright, glowing things keep their colour (lamps, lasers, screens)")] public float ColorOnlyLights;
        [Range(0, 1), Tooltip("A helmet visor: dark curved rims at the top and bottom of the view")] public float Visor;

        [Header("Gradient map")]
        [Range(0, 1), Tooltip("Brightness picks a colour on a three-stop ramp (thermal camera, blueprint); 0 = off")] public float GradientMap;
        public Color MapDark = Color.black;
        public Color MapMid = Color.grey;
        public Color MapLight = Color.white;

        [Header("Grid paper")]
        [Range(0, 1), Tooltip("Lines of a drawing sheet over the picture (0 = off)")] public float GridStrength;
        [Tooltip("Cell size, pixels at 720p")] public float GridSpacing = 16f;
        public Color GridColor = Color.white;

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
