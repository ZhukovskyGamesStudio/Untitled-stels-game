using Stealth.Configs;

namespace Stealth.Level
{
    /// <summary>
    /// What the locations' looks over a style share (<see cref="ShopLook"/>, <see cref="ClinicLook"/>, <see cref="HomeLook"/>):
    /// plain door casings, no crown, panels, rail or light strips — the building's own fittings instead of the style's trim.
    /// </summary>
    public static class LocationLook
    {
        /// <summary>A copy of the style's geometry with the plain trim; <paramref name="baseboard"/> null keeps the style's.</summary>
        public static StyleGeometry Plain(StyleGeometry g, bool pipes, CeilingKind ceiling, bool? baseboard = null)
        {
            var s = g.Clone();
            s.Doors = DoorTrim.Casing;
            if (baseboard.HasValue) s.Baseboard = baseboard.Value;
            s.Crown = false;
            s.WainscotHeight = 0f;
            s.RailHeight = 0f;
            s.Pipes = pipes;
            s.Ceiling = ceiling;
            s.StripSpacing = 0f;
            return s;
        }
    }
}
