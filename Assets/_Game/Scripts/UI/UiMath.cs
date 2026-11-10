using System.Collections.Generic;
using UnityEngine;

namespace Stealth.UI
{
    /// <summary>One contextual hint shown under the crosshair.</summary>
    public struct Prompt
    {
        public int Priority;
        public string Key;      // e.g. "E", "Tab", "" for none
        public string Text;
        public float Progress;  // 0..1 for hold actions, negative = no bar

        public Prompt(int priority, string key, string text, float progress = -1f)
        {
            Priority = priority;
            Key = key;
            Text = text;
            Progress = progress;
        }

        public bool IsValid => !string.IsNullOrEmpty(Text);
    }

    /// <summary>Pure UI helpers (unit tested).</summary>
    public static class UiMath
    {
        /// <summary>The single most important prompt; ties keep the first one added.</summary>
        public static Prompt Choose(IReadOnlyList<Prompt> prompts)
        {
            var best = default(Prompt);
            bool found = false;
            foreach (var p in prompts)
            {
                if (!p.IsValid) continue;
                if (!found || p.Priority > best.Priority) { best = p; found = true; }
            }
            return best;
        }

        /// <summary>
        /// Angle (degrees, clockwise from screen-up) at which a world point lies relative to where the camera looks.
        /// 0 = straight ahead, 90 = right, 180 = behind.
        /// </summary>
        public static float ScreenAngle(Vector3 cameraPos, Vector3 cameraForward, Vector3 target)
        {
            var f = new Vector3(cameraForward.x, 0, cameraForward.z);
            var d = new Vector3(target.x - cameraPos.x, 0, target.z - cameraPos.z);
            if (f.sqrMagnitude < 1e-6f || d.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.SignedAngle(f, d, Vector3.up);
        }

        /// <summary>
        /// Places a marker for a viewport point on screen; points behind the camera or outside the screen are pushed to
        /// the border (with a margin). Returns the GUI position (top-left origin) and whether it was clamped.
        /// </summary>
        public static Vector2 EdgeMarker(Vector3 viewport, float width, float height, float margin, out bool clamped)
        {
            var p = new Vector2(viewport.x, viewport.y);
            if (viewport.z < 0f) p = new Vector2(1f - p.x, 1f - p.y); // behind: mirror
            var centered = p - new Vector2(0.5f, 0.5f);
            float limitX = 0.5f - margin / width, limitY = 0.5f - margin / height;
            clamped = viewport.z < 0f || Mathf.Abs(centered.x) > limitX || Mathf.Abs(centered.y) > limitY;
            if (clamped)
            {
                if (viewport.z < 0f && centered.sqrMagnitude < 1e-6f) centered = new Vector2(0f, -1f);
                float scale = Mathf.Min(limitX / Mathf.Max(Mathf.Abs(centered.x), 1e-5f), limitY / Mathf.Max(Mathf.Abs(centered.y), 1e-5f));
                centered *= scale;
            }
            var screen = (centered + new Vector2(0.5f, 0.5f));
            return new Vector2(screen.x * width, (1f - screen.y) * height);
        }

        /// <summary>Noise radius (m) to meter bars: 0 silent, 1 quiet (crouch), 2 audible (walk), 3 loud (run / fall).</summary>
        public static int NoiseBars(float radius) => radius <= 0.1f ? 0 : radius <= 3f ? 1 : radius <= 8f ? 2 : 3;

        /// <summary>Fade for a timed message: 0 → 1 over <paramref name="fade"/>, holds, then 1 → 0 at the end.</summary>
        public static float MessageAlpha(float age, float duration, float fade = 0.25f)
        {
            if (age < 0f || age > duration) return 0f;
            return Mathf.Clamp01(Mathf.Min(age / fade, (duration - age) / fade));
        }

        /// <summary>
        /// Robot status icon: 0 none, 1 "?" (noticing something), 2 "?" (investigating), 3 "!" (chasing / alarm).
        /// </summary>
        public static int StateIcon(float awareness, bool investigating, bool chasing)
        {
            if (chasing || awareness >= 0.99f) return 3;
            if (investigating) return 2;
            return awareness > 0.05f ? 1 : 0;
        }

        /// <summary>Words for how lit the player is (HUD), from <see cref="LightLevel"/>.</summary>
        public static readonly string[] LightNames = { "в тени", "полумрак", "заметно", "на свету" };

        /// <summary>0 in the shadows, 1 half-light, 2 noticeable, 3 in the light — from visibility 0..1.</summary>
        public static int LightLevel(float visibility) => visibility < 0.2f ? 0 : visibility < 0.5f ? 1 : visibility < 0.8f ? 2 : 3;

        public static Color LightColor(int level) => level switch
        {
            0 => new Color(0.5f, 0.6f, 0.85f),
            1 => new Color(0.8f, 0.85f, 0.9f),
            2 => new Color(1f, 0.9f, 0.5f),
            _ => new Color(1f, 0.97f, 0.75f),
        };

        /// <summary>The suspicion bar over a detector: while it is noticing you and has not started chasing yet.</summary>
        public static bool ShowSuspicionBar(float myAwareness, bool chasing) => !chasing && myAwareness > 0.02f;

        /// <summary>Bar colour: yellow → orange as it fills; red and blinking once it has detected you (the warning moment).</summary>
        public static Color SuspicionColor(float fill, bool detected, float time) =>
            detected
                ? new Color(1f, 0.15f, 0.1f, 0.7f + 0.3f * Mathf.Sin(time * 14f))
                : Color.Lerp(new Color(1f, 0.92f, 0.35f), new Color(1f, 0.45f, 0.1f), Mathf.Clamp01(fill));

        /// <summary>UI scale: the layout is designed for 720p and scaled up for bigger windows (clamped).</summary>
        public static float Scale(float screenHeight) => Mathf.Clamp(screenHeight / 720f, 0.75f, 3f);
    }
}
