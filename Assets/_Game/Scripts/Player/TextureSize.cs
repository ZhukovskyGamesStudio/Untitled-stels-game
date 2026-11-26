namespace Stealth.Player
{
    /// <summary>How big the textures are read (Settings → Picture): full, half or a quarter of their size.</summary>
    public enum TextureSize { Full = 0, Half = 1, Quarter = 2 }

    /// <summary>
    /// The texture size setting's rules (pure): which detail levels of every texture are skipped (the engine's global
    /// mipmap limit; the models' baked maps are packed from the same level), what the menu cycles to and how it is named.
    /// Full is the bake's 2K (the models' maps alone take about 1.8 GB of video memory), half 1K (about 0.5 GB), a quarter 512.
    /// </summary>
    public static class TextureSizeRules
    {
        public const TextureSize Default = TextureSize.Half;

        /// <summary>The detail levels skipped from the top (0: the full picture).</summary>
        public static int Skip(TextureSize s) => (int)s;

        public static TextureSize Clamp(int v) => v < 0 || v > 2 ? Default : (TextureSize)v;

        /// <summary>The next choice of the menu's button: full → half → quarter → full.</summary>
        public static TextureSize Next(TextureSize s) => (TextureSize)(((int)s + 1) % 3);

        /// <summary>The menu's label key.</summary>
        public static string Key(TextureSize s) => s switch
        {
            TextureSize.Full => "menu.texture_size_full",
            TextureSize.Quarter => "menu.texture_size_quarter",
            _ => "menu.texture_size_half",
        };
    }
}
