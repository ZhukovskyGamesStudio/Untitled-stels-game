using System;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Configs
{
    /// <summary>
    /// What one kind of house looks like over the visual style (Data/Styles/Houses/&lt;kind&gt;.asset, one per
    /// <see cref="HouseKind"/>, edited by hand: the asset builder makes a missing one and never overwrites it): its shell and
    /// finishes, its rooms, its decay, which models its furniture is, its materials, its light and its colours. An empty
    /// part keeps the style's. Picture only: robots see the same light and the same house in any look.
    /// </summary>
    [CreateAssetMenu(menuName = "Stealth/House Look")]
    public class HouseLook : ScriptableObject
    {
        public HouseKind Kind;
        [Tooltip("The shell, the rooms and the decay only in styles with these models (empty: in every style): a look made of scanned surfaces has no place in a drawn style")]
        public ModelFamily[] ShellIn = new ModelFamily[0];

        [Header("Trims and services")]
        [Tooltip("The house has its own trims and services (below); off: the style's")]
        public bool OwnTrim;
        public DoorTrim Doors = DoorTrim.Casing;
        public bool Baseboard = true, Crown;
        [Tooltip("Height of the wall panels, metres (0 = none)")] public float WainscotHeight;
        [Tooltip("Height of a chair rail, metres (0 = none)")] public float RailHeight;
        [Tooltip("Pipes along the walls under the ceiling")] public bool Pipes;
        public CeilingKind Ceiling;
        [Tooltip("Glowing slits in the walls every this many metres (0 = none)")] public float StripSpacing;
        [Tooltip("The staircases (Auto: the style's)")] public StairChoice Stairs;

        [Header("Finishes (empty = the style's)")]
        public Material Floor;
        [Tooltip("Metres covered by one repeat of the texture")] public float FloorTile = 2f;
        public Material Wall;
        public float WallTile = 2f;
        [Tooltip("Under the ceilings")] public Material CeilingFinish;
        public float CeilingTile = 1.2f;
        [Tooltip("Skirting, cornices, rails, door casings")] public Material Trim;
        public Material Panel, Column, Beam, Pipe;

        [Header("Rooms of their own")]
        [Tooltip("Rooms not listed below keep what the style gives them; off: they take the finishes above")]
        public bool KeepStyleRooms = true;
        public RoomLook[] RoomLooks = new RoomLook[0];
        public RoomFloor[] RoomFloors = new RoomFloor[0];

        [Header("Decay (decals scattered over the house from its seed)")]
        public Material[] FloorDecay = new Material[0];
        public Material[] WallDecay = new Material[0];
        [Tooltip("Floor decals per square metre (walls: per metre of wall × 0.6); 0 = none")] public float DecayDensity;
        public Material[] Posters = new Material[0];
        [Range(0, 1), Tooltip("Chance of a poster on 4 m of wall")] public float PosterChance;

        [Header("Furniture")]
        [Tooltip("The models and materials the house is furnished with (None: the style's)")]
        public ModelFamily Furniture = ModelFamily.None;
        [Tooltip("…in styles with these models")]
        public ModelFamily[] FurnitureIn = { ModelFamily.Realistic };
        [Tooltip("What its furniture and fittings are made of (Data/Styles/Surfaces; none: the models' own materials)")]
        public SurfaceSet Surfaces;
        [Tooltip("…in styles with these models")]
        public ModelFamily[] SurfacesIn = { ModelFamily.Realistic };
        [Tooltip("Materials of the house's furniture and fittings changed one for another (after the models' own materials)")]
        public MaterialSwap[] Materials = new MaterialSwap[0];

        [Header("Light and colour (over the style's)")]
        public HouseGrade Grade = new();
        public HouseAir Air = new();

        /// <summary>The shell is dressed in a style with these models.</summary>
        public bool ShellAppliesIn(ModelFamily family) => ShellIn == null || ShellIn.Length == 0 || Array.IndexOf(ShellIn, family) >= 0;

        /// <summary>The models its furniture is in a style with these models (None: the style's own).</summary>
        public ModelFamily FurnitureFor(ModelFamily family) =>
            Furniture != ModelFamily.None && FurnitureIn != null && Array.IndexOf(FurnitureIn, family) >= 0 ? Furniture : ModelFamily.None;

        /// <summary>What its things are made of in a style with these models (null: the models' own materials).</summary>
        public SurfaceSet SurfacesFor(ModelFamily family) => Surfaces != null && SurfacesIn != null && Array.IndexOf(SurfacesIn, family) >= 0 ? Surfaces : null;

        bool HasShell => OwnTrim || Stairs != StairChoice.Auto || Floor != null || Wall != null || CeilingFinish != null || Trim != null || Panel != null || Column != null
            || Beam != null || Pipe != null || !KeepStyleRooms || Count(RoomLooks) > 0 || Count(RoomFloors) > 0 || DecayDensity > 0f || PosterChance > 0f;

        static int Count(Array a) => a?.Length ?? 0;

        /// <summary>The style's geometry dressed for this house (the style's own when the look has nothing to say about it).</summary>
        public StyleGeometry Dress(StyleGeometry g)
        {
            if (g == null || !HasShell || !ShellAppliesIn(g.Family)) return g;
            var s = g.Clone();
            if (OwnTrim)
            {
                s.Doors = Doors;
                s.Baseboard = Baseboard;
                s.Crown = Crown;
                s.WainscotHeight = WainscotHeight;
                s.RailHeight = RailHeight;
                s.Pipes = Pipes;
                s.Ceiling = Ceiling;
                s.StripSpacing = StripSpacing;
            }
            if (Stairs != StairChoice.Auto) s.Stairs = Stairs;
            if (Floor != null) { s.Floor = Floor; s.FloorTile = FloorTile; }
            if (Wall != null) { s.Wall = Wall; s.WallTile = WallTile; }
            if (CeilingFinish != null) { s.CeilingFinish = CeilingFinish; s.CeilingTile = CeilingTile; }
            if (Trim != null) s.Trim = Trim;
            if (Panel != null) s.Panel = Panel;
            if (Column != null) s.Column = Column;
            if (Beam != null) s.Beam = Beam;
            if (Pipe != null) s.Pipe = Pipe;
            s.RoomLooks = Merge(KeepStyleRooms ? g.RoomLooks : null, RoomLooks, r => r.Room);
            s.RoomFloors = Merge(KeepStyleRooms ? g.RoomFloors : null, RoomFloors, r => r.Room);
            s.FloorDecay = FloorDecay ?? new Material[0];
            s.WallDecay = WallDecay ?? new Material[0];
            s.Posters = Posters ?? new Material[0];
            s.DecayDensity = DecayDensity;
            s.PosterChance = PosterChance;
            return s;
        }

        /// <summary>The house's own entries, then the style's for the rooms the house does not list.</summary>
        static T[] Merge<T>(T[] style, T[] own, Func<T, RoomType> room)
        {
            own ??= new T[0];
            if (style == null || style.Length == 0) return own;
            var kept = Array.FindAll(style, e => Array.FindIndex(own, o => room(o) == room(e)) < 0);
            var all = new T[own.Length + kept.Length];
            own.CopyTo(all, 0);
            kept.CopyTo(all, own.Length);
            return all;
        }
    }

    /// <summary>One material for another.</summary>
    [Serializable]
    public struct MaterialSwap
    {
        [Tooltip("The material the models carry (Art/Materials)")] public Material From;
        public Material To;
    }

    /// <summary>How a house's colours differ from the style's: added to its grade (nothing: zero, white).</summary>
    [Serializable]
    public class HouseGrade
    {
        [Tooltip("Stops brighter or darker")] public float Exposure;
        public float Contrast;
        public float Saturation;
        [Tooltip("Warmer (+) or colder (−)")] public float Temperature;
        [Tooltip("Towards magenta (+) or green (−)")] public float Tint;
        [Tooltip("Multiplies the style's filter")] public Color ColorFilter = Color.white;
        [Tooltip("The shadows' colour, and how much of it (0 = the style's own)")] public Color SplitShadows = Color.grey;
        [Tooltip("The highlights' colour")] public Color SplitHighlights = Color.grey;
        [Range(0, 1)] public float Split;
        public float FilmGrain;
        public float Vignette;

        /// <summary>The style's grade in this house (pure): <paramref name="share"/> of the difference (0 underground … 1 in the house).</summary>
        public StyleGrade Over(StyleGrade style, float share)
        {
            var g = style.Clone();
            g.Exposure += Exposure * share;
            g.Contrast = Mathf.Clamp(g.Contrast + Contrast * share, -100f, 100f);
            g.Saturation = Mathf.Clamp(g.Saturation + Saturation * share, -100f, 100f);
            g.Temperature = Mathf.Clamp(g.Temperature + Temperature * share, -100f, 100f);
            g.Tint = Mathf.Clamp(g.Tint + Tint * share, -100f, 100f);
            g.ColorFilter *= Color.Lerp(Color.white, ColorFilter, share);
            g.SplitShadows = Color.Lerp(g.SplitShadows, SplitShadows, Split * share);
            g.SplitHighlights = Color.Lerp(g.SplitHighlights, SplitHighlights, Split * share);
            g.FilmGrain = Mathf.Clamp01(g.FilmGrain + FilmGrain * share);
            g.Vignette = Mathf.Clamp01(g.Vignette + Vignette * share);
            return g;
        }
    }

    /// <summary>How a house's air and light differ from the style's: multiplied onto them (nothing: one, white).</summary>
    [Serializable]
    public class HouseAir
    {
        [Tooltip("The fill in the shadows: its colour and strength")] public Color AmbientTint = Color.white;
        public float Ambient = 1f;
        [Tooltip("The haze indoors: its colour, how much of it (0 = the style's own), how thick")] public Color Fog = Color.black;
        [Range(0, 1)] public float FogColor;
        public float FogDensity = 1f;
        [Tooltip("How bright the lamps look (the picture only)")] public float LightScale = 1f;
        [Tooltip("How bright the lamps' diffusers look")] public float LampGlow = 1f;

        /// <summary>The style's air in this house (pure).</summary>
        public StyleWorld Over(StyleWorld style)
        {
            var w = style.Clone();
            w.Ambient = new Color(w.Ambient.r * AmbientTint.r * Ambient, w.Ambient.g * AmbientTint.g * Ambient, w.Ambient.b * AmbientTint.b * Ambient, w.Ambient.a);
            w.IndoorFog = Color.Lerp(w.IndoorFog, Fog, FogColor);
            w.IndoorFogDensity *= FogDensity;
            w.LightScale *= LightScale;
            w.LampGlow *= LampGlow;
            return w;
        }
    }
}
