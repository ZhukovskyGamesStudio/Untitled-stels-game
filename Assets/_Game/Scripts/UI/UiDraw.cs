using UnityEngine;

namespace Stealth.UI
{
    /// <summary>Tiny IMGUI helpers for the placeholder interface.</summary>
    public static class UiDraw
    {
        static GUIStyle s_Label;
        static Texture2D s_Vignette;

        public static void Rect(Rect r, Color c) =>
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, c, 0, 0);

        public static void Label(Rect r, string text, int size, TextAnchor anchor, Color color, bool bold = false)
        {
            s_Label ??= new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true };
            s_Label.fontSize = size;
            s_Label.alignment = anchor;
            s_Label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            var shadow = new Color(0, 0, 0, color.a * 0.8f);
            s_Label.normal.textColor = shadow;
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, s_Label);
            s_Label.normal.textColor = color;
            GUI.Label(r, text, s_Label);
        }

        public static void Bar(Rect r, float t, Color fill)
        {
            Rect(r, new Color(0, 0, 0, 0.5f));
            Rect(new Rect(r.x + 2, r.y + 2, (r.width - 4) * Mathf.Clamp01(t), r.height - 4), fill);
        }

        /// <summary>Full-screen edge darkening tinted with <paramref name="c"/>.</summary>
        public static void Vignette(Color c)
        {
            if (s_Vignette == null)
            {
                const int size = 128;
                s_Vignette = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        var d = new Vector2(x / (size - 1f) - 0.5f, y / (size - 1f) - 0.5f).magnitude * 2f;
                        s_Vignette.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, d))));
                    }
                s_Vignette.Apply();
            }
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), s_Vignette, ScaleMode.StretchToFill, true, 0, c, 0, 0);
        }
    }
}
