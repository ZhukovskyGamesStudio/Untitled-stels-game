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
        public string Title = "Как сейчас"; // loc-ok: authoring default (the player sees style.<name>)
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
    /// family also swaps furniture for CC0 scanned models and uses PBR materials; the derelict family is the abandoned
    /// sci-fi facility: dented, panelled, self-made grimy PBR surfaces and its own furniture). Append only.
    /// </summary>
    public enum ModelFamily { None, Lowpoly, Toy, Lofi, Clean, Realistic, Derelict }

    /// <summary>What frames the doorways.</summary>
    public enum DoorTrim { None, Casing, Pilasters, Arch }

    /// <summary>Which staircase look a style uses (<see cref="Stealth.Level.StairStyle"/> + 1); Auto — by the model family and the house seed.</summary>
    public enum StairChoice { Auto, Concrete, Wood, Steel, Glass }

    /// <summary>What hangs under the ceiling.</summary>
    // (beams, I-beams and coffers, 1–3, are gone: they kept ending in the middle of rooms — playtest; Trays keeps its saved value)
    public enum CeilingKind { None = 0, Trays = 4 }

    /// <summary>A kind of room with a floor of its own in a style (<see cref="StyleGeometry.RoomFloors"/>).</summary>
    [Serializable]
    public struct RoomFloor
    {
        public Stealth.Level.RoomType Room;
        public Material Material;
        [Tooltip("Metres covered by one repeat of its texture")] public float Tile;
    }

    /// <summary>
    /// A kind of room dressed its own way in a style (<see cref="StyleGeometry.RoomLooks"/>): tiles up to some height in the
    /// restrooms and the kitchen, padding in the gym, paint instead of the style's wallpaper, a plain or acoustic ceiling.
    /// </summary>
    [Serializable]
    public struct RoomLook
    {
        public Stealth.Level.RoomType Room;
        [Tooltip("The walls' finish (null = the style's)")] public Material Wall;
        [Tooltip("Metres covered by one repeat of its texture")] public float WallTile;
        [Tooltip("From the floor up to LowerHeight: tiles, padding (null = none)")] public Material Lower;
        public float LowerTile;
        public float LowerHeight;
        [Tooltip("No panels and chair rail of the style (and no skirting under tiles): plain walls")] public bool Plain;
        [Tooltip("A ceiling of its own (null = the style's); the style's beams or coffers are left out there")] public Material Ceiling;
        public float CeilingTile;
    }

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
        [Tooltip("The look of the staircases and gallery railings (Auto = by the model family and the house seed). Whatever the look, " +
                 "its treads take the floor finish, its solid body the wall finish and its rails and balusters the trim of the style.")]
        public StairChoice Stairs;

        [Header("Ceiling")]
        public CeilingKind Ceiling;

        [Header("Decay (decals scattered over the house from its seed)")]
        [Tooltip("Big chunks on the floors: rust, oil, scorch marks, rubble")] public Material[] FloorDecay = new Material[0];
        [Tooltip("On the walls: leaks, mold, peeled paint, soot (a texture taller than wide hangs from the ceiling)")]
        public Material[] WallDecay = new Material[0];
        [Tooltip("Floor decals per square metre (walls: per metre of wall × 0.6); 0 = none")] public float DecayDensity;
        [Tooltip("Posters on the walls")] public Material[] Posters = new Material[0];
        [Range(0, 1), Tooltip("Chance of a poster on 4 m of wall")] public float PosterChance;
        [Range(0, 2), Tooltip("Things hung on the walls per 4 m of free wall: framed prints, certificates, signs, thermostats (Level.WallThings picks by room); 0 = bare walls")]
        public float ThingChance;

        [Header("Players")]
        [Tooltip("A rigged human (Player/HumanRig) shown instead of the player model; null = the style's player models")]
        public GameObject PlayerRig;

        [Header("Materials (null = keep the house as it is)")]
        public Material Floor;
        [Tooltip("Metres covered by one repeat of the floor texture")] public float FloorTile = 1f;
        [Tooltip("Floors of their own in some kinds of rooms (tiles in kitchens and restrooms, rubber in the gym, carpet in the library); the rest keep Floor")]
        public RoomFloor[] RoomFloors = new RoomFloor[0];
        public Material Wall;
        [Tooltip("Metres covered by one repeat of the wall texture")] public float WallTile = 1f;
        [Tooltip("Walls and ceilings of their own in some kinds of rooms (tiles in restrooms and kitchens, padding in the gym…)")]
        public RoomLook[] RoomLooks = new RoomLook[0];
        [Tooltip("Baseboards, cornices, rails, casings")] public Material Trim;
        [Tooltip("The boards of the wall panels")] public Material Panel;
        public Material Column;
        public Material Beam;
        public Material Pipe;
        public Material Glow;
        [Tooltip("Under the ceilings (tiles, boards…)")] public Material CeilingFinish;
        public float CeilingTile = 1.2f;

        /// <summary>The index in <see cref="RoomFloors"/> of a room type's own floor, or −1 (the style's <see cref="Floor"/>).</summary>
        public int RoomFloorOf(Stealth.Level.RoomType room)
        {
            if (RoomFloors == null) return -1;
            for (int i = 0; i < RoomFloors.Length; i++)
                if (RoomFloors[i].Room == room && RoomFloors[i].Material != null) return i;
            return -1;
        }

        /// <summary>
        /// The index in <see cref="RoomLooks"/> of a room type's own look, or −1. A type listed several times has that many
        /// looks to choose from: <paramref name="pick"/> (a number of the room, from the house seed) takes one — every wall
        /// of one room asks with the same number.
        /// </summary>
        public int RoomLookOf(Stealth.Level.RoomType room, int pick = 0)
        {
            if (RoomLooks == null) return -1;
            int first = -1, count = 0;
            for (int i = 0; i < RoomLooks.Length; i++)
                if (RoomLooks[i].Room == room) { if (count++ == 0) first = i; }
            if (count <= 1) return first;
            int want = (int)((uint)pick % (uint)count);
            for (int i = 0; i < RoomLooks.Length; i++)
                if (RoomLooks[i].Room == room && want-- == 0) return i;
            return first;
        }

        /// <summary>A copy (the arrays shared until replaced): a house dresses itself on top of the style (<see cref="HouseLook"/>).</summary>
        public StyleGeometry Clone() => (StyleGeometry)MemberwiseClone();

        public bool HasArchitecture => CeilingFinish != null || Baseboard || Crown || WainscotHeight > 0f || RailHeight > 0f || Pipes || StripSpacing > 0f ||
                                       Doors != DoorTrim.None || Ceiling != CeilingKind.None || Floor != null || Wall != null ||
                                       (DecayDensity > 0f && (FloorDecay.Length > 0 || WallDecay.Length > 0)) || (PosterChance > 0f && Posters.Length > 0);
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
        [Range(0, 1), Tooltip("Shiny and metal things mirror what is on the screen (0 = not at all). Nothing else is mirrored: a dark room stays dark")] public float Reflections;
        [Range(0, 2), Tooltip("Bounced light: lit surfaces on the screen light what faces them — a ceiling over a lit floor, the underside of a desk (0 = none). A picture only; a dark room stays dark")]
        public float Bounce;
        [Range(0.5f, 6f), Tooltip("How far a surface throws its bounced light, metres")] public float BounceReach = 4.5f;
        [Range(0, 3), Tooltip("Room fill: the soft light a room's burning lamps leave in it after bouncing off its walls — shadows in a lit room are not black (0 = none). Never crosses a wall; a room with its lamps off gets nothing. A picture only")]
        public float RoomFill;
        [Range(-1, 1)] public float LensDistortion;

        public StyleGrade Clone() => (StyleGrade)MemberwiseClone();
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
        [Tooltip("How bright lamps look (1 = as they are). Visual only: robots still see the real light")]
        public float LightScale = 1f;
        [Tooltip("How bright the glowing lamp diffusers look (1 = as they are), apart from the light they cast. Visual only")]
        public float LampGlow = 1f;
        [Tooltip("The soft look of a ceiling lamp (0 = none: a flat glowing panel): a hot middle on each diffuser fading to its edges, and a pool of light on the ceiling round the fixture, fading out. 1 = full. Visual only (no light is cast)")]
        public float CeilingGlow;
        [Tooltip("A faint light round the player's own eyes, its strength (0 = none): what is right before the player — the inside of a drawer, a corner in his own shadow — is never pitch black. Only its owner sees it; robots do not (visual only)")]
        public float EyeGlow;
        [Tooltip("How far the light round the eyes reaches, metres")]
        public float EyeGlowRange = 2.2f;
        [Tooltip("Under the ground (the sewer, the basement) the lamps look this many times brighter than LightScale makes them: a style with a low exposure for its rooms keeps its tunnels lit. Visual only")]
        public float UndergroundLights = 1f;

        [Header("Outside (visual only: the gas in the air still hurts where it did)")]
        [Tooltip("The world outside: a latitude-longitude panorama imported as a cubemap, seen above the gas (doors, windows). Empty = no sky")]
        public Cubemap Sky;
        [Tooltip("How bright the sky looks")] public float SkyExposure = 1f;
        [Tooltip("How thick the gas looks where it is full, per metre (0.03: half gone ~25 m into it; 1.6: nothing seen 2 m into it)")]
        public float GasDensity = 0.14f;
        [Tooltip("How far the sky panorama is, metres: the gas between the eye and it veils it (shorter = the world outside shows more)")]
        public float SkyDistance = 70f;
        [Tooltip("Metres from the outer walls over which the gas thickens from none to full (it looks pushed away from the house)")]
        public float GasClear = 10f;
        [Tooltip("Height above the ground the gas stays full, metres")] public float GasLayer = 8f;
        [Tooltip("Above that it thins out over this many metres (exponentially): the sky shows overhead and from upper floors")]
        public float GasFalloff = 4f;

        public StyleWorld Clone() => (StyleWorld)MemberwiseClone();
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
