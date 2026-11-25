using UnityEngine;

namespace Stealth.UI
{
    /// <summary>
    /// The interface's one palette: an office's paperwork — off-white forms typed in ink, sticky notes, highlighter
    /// marks, a red rubber stamp, a blue ballpoint. Shared by the prefab builder and the presenters, so every screen uses
    /// the same few colours (UiPaletteTests keeps the text readable on its paper). The paper is never pure white: in a
    /// dark game a white sheet glares.
    /// </summary>
    public static class UiPalette
    {
        static Color Hex(int rgb, float a = 1f) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        // ------------------------------------------------------------------ paper
        /// <summary>A sheet of the form: warm off-white.</summary>
        public static readonly Color Paper = Hex(0xF1EDE2);
        /// <summary>A field or a cell on the sheet, a shade darker.</summary>
        public static readonly Color PaperShade = Hex(0xE3DDCD);
        /// <summary>Paper in the world (the stash board, the tunnel notices, the tablet): dimmer, it is not lit by anything.</summary>
        public static readonly Color PaperDim = Hex(0xCFC8B6);
        /// <summary>The second and third copies of a form.</summary>
        public static readonly Color CarbonPink = Hex(0xF0CCD0), CarbonYellow = Hex(0xF2E6B0);
        /// <summary>The desk under the menu's papers.</summary>
        public static readonly Color Desk = Hex(0x2A2724), DeskDeep = Hex(0x1C1A18);
        /// <summary>A veil over the game behind a paper (pause, terminals).</summary>
        public static readonly Color Veil = new(0.07f, 0.065f, 0.06f, 0.72f);

        // ------------------------------------------------------------------ ink
        public static readonly Color Ink = Hex(0x23262B);
        /// <summary>Secondary text: captions, numbers in a row.</summary>
        public static readonly Color InkSoft = Hex(0x4C525A);
        /// <summary>Hints, done tasks, what cannot be used now.</summary>
        public static readonly Color InkFaint = Hex(0x80858C);
        /// <summary>The printed lines of a form.</summary>
        public static readonly Color Rule = Hex(0xAEBDCB);
        /// <summary>The red margin line.</summary>
        public static readonly Color Margin = Hex(0xD79B92);
        /// <summary>The rubber stamp: warnings, alarms, robots on the map.</summary>
        public static readonly Color Stamp = Hex(0xC03A2D);
        /// <summary>The ballpoint: handwriting on the notes, the player's own marks.</summary>
        public static readonly Color Pen = Hex(0x2B4896);
        /// <summary>Green ink: done, enough, ready.</summary>
        public static readonly Color Good = Hex(0x2F7D40);
        /// <summary>Orange ink: careful.</summary>
        public static readonly Color Warn = Hex(0x985210);

        // ------------------------------------------------------------------ sticky notes and highlighters
        public static readonly Color NoteYellow = Hex(0xF6DE79), NoteGreen = Hex(0xB7E09E), NotePink = Hex(0xF3B4C4),
            NoteBlue = Hex(0xACD4EC), NoteOrange = Hex(0xF4BF7C);
        public static readonly Color MarkYellow = Hex(0xF0E447), MarkGreen = Hex(0x8BD658), MarkPink = Hex(0xF08AB8),
            MarkBlue = Hex(0x7EC4EC), MarkOrange = Hex(0xF5A64A);

        // ------------------------------------------------------------------ over the world (no paper behind)
        /// <summary>Text and marks drawn straight over the 3D view: paper-coloured with an ink rim.</summary>
        public static readonly Color OnWorld = Hex(0xF1EDE2, 0.92f);
        public static readonly Color OnWorldDim = Hex(0xF1EDE2, 0.6f);
        /// <summary>The rim round text over the world.</summary>
        public static readonly Color Rim = new(0.08f, 0.08f, 0.09f, 0.85f);
        /// <summary>Danger over the world: a red that reads on dark and light scenes alike.</summary>
        public static readonly Color Danger = Hex(0xEE4636);
        /// <summary>Caution over the world (a robot wondering).</summary>
        public static readonly Color Caution = Hex(0xF5C842);
        /// <summary>The way out over the world (the exit hatch, its marker on the map).</summary>
        public static readonly Color Exit = Hex(0x7BD86A);

        /// <summary>The same colour with another alpha.</summary>
        public static Color A(this Color c, float a) => new(c.r, c.g, c.b, a);

        /// <summary>
        /// A player's colour as ink on paper: the team colours are bright (they are the clothes' accents) and wash out on
        /// a sheet, so a name is written in the same hue, darker.
        /// </summary>
        public static Color InkOf(Color team)
        {
            // as much of the hue as the paper allows: darkened towards the ink until it reads (a bright lime needs more)
            for (float t = 0.4f; t < 1f; t += 0.05f)
            {
                var c = Color.Lerp(team, Ink, t).A(1f);
                if (Contrast(c, Paper) >= 4.8f) return c;
            }
            return Ink;
        }

        /// <summary>
        /// The house clock's card as robots keep coming: plain paper, the pink copy, then a reddish sheet — light enough
        /// that the typed time stays readable at any heat (0..1).
        /// </summary>
        public static Color HeatPaper(float heat)
        {
            heat = Mathf.Clamp01(heat);
            var hot = Hex(0xEDA79C);
            return heat < 0.5f ? Color.Lerp(Paper, CarbonPink, heat * 2f) : Color.Lerp(CarbonPink, hot, (heat - 0.5f) * 2f);
        }

        /// <summary>WCAG contrast ratio of two opaque colours (1..21): text wants 4.5 and more.</summary>
        public static float Contrast(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        static float Luminance(Color c)
        {
            static float Lin(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
        }
    }
}
