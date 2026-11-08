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

        static Texture2D s_Arc;

        /// <summary>Soft arc segment centred on the screen centre, rotated by <paramref name="angle"/> (0 = up, clockwise).</summary>
        public static void DirectionArc(float angle, float radius, Color c)
        {
            if (s_Arc == null)
            {
                const int w = 128, h = 32;
                s_Arc = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = x / (w - 1f) * 2f - 1f;          // -1..1 across
                        float v = y / (h - 1f);                     // 0 bottom .. 1 top
                        float curve = 1f - u * u * 0.6f;            // bow towards the centre
                        float band = Mathf.Clamp01(1f - Mathf.Abs(v - curve * 0.6f) * 4f);
                        float fade = Mathf.Clamp01(1f - Mathf.Abs(u) * 1.1f);
                        s_Arc.SetPixel(x, y, new Color(1, 1, 1, band * fade));
                    }
                s_Arc.Apply();
            }
            var centre = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, centre);
            GUI.DrawTexture(new Rect(centre.x - radius * 0.7f, centre.y - radius - 20f, radius * 1.4f, 36f), s_Arc, ScaleMode.StretchToFill, true, 0, c, 0, 0);
            GUI.matrix = matrix;
        }
    }
}
