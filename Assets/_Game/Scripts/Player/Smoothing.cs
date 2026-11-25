using UnityEngine;

namespace Stealth.Player
{
    /// <summary>How the player wants edges smoothed (pause → Settings). Saved by number: append only.</summary>
    public enum Smoothing
    {
        /// <summary>Nothing: every edge as the renderer drew it.</summary>
        Off,
        /// <summary>Only the pass over the finished picture.</summary>
        Fxaa,
        /// <summary>The camera's SMAA and the pass over the finished picture (what the game had before temporal smoothing).</summary>
        Smaa,
        /// <summary>Temporal: every frame is drawn a fraction of a pixel aside and blended with the frames before it.</summary>
        Taa,
        /// <summary>Temporal, and the pass over the finished picture on top.</summary>
        TaaFxaa,
    }


    /// <summary>What the camera itself does about edges.</summary>
    public enum CameraSmoothing { None, Smaa, Temporal }

    /// <summary>Tuning of the temporal smoothing (PlayerConfig.Picture).</summary>
    [System.Serializable]
    public class SmoothingSettings
    {
        [Range(0, 4), Tooltip("Quality of the temporal filter: 0 very low … 3 high (9 samples, variance clamp, bicubic history), 4 very high (and filtering of the centre sample).")]
        public int Quality = 4;
        [Range(0.5f, 0.98f), Tooltip("How much of the picture is the frames before (the rest is the new frame): more is smoother and steadier, and ghosts more behind what moves.")]
        public float History = 0.9f;
        [Range(0.1f, 3f), Tooltip("How far the new frame's neighbours may stretch the range the old frames are clamped to: less — less ghosting and more flicker, more — steadier and blurrier.")]
        public float Clamp = 0.9f;
        [Range(0f, 1f), Tooltip("Sharpening after the temporal filter (it softens the picture a little): 0 = none.")]
        public float Sharpen = 0.35f;
        [Range(-1f, 0f), Tooltip("Textures are read this many detail levels sharper under the temporal filter (it averages the extra detail back): 0 = as without it. Lower shimmers more.")]
        public float MipBias = 0f;
    }

    /// <summary>
    /// Which smoothing runs for the player's choice (pure). The temporal filter blends the frames inside post-processing,
    /// before the style's screen filter: a style whose filter draws lines from the frame's depth (ink outlines, hatching)
    /// or turns the picture into blocks would get those lines and blocks trembling with the camera's sub-pixel shake —
    /// such a style falls back to SMAA and the final pass.
    /// </summary>
    public static class SmoothingRules
    {
        public const Smoothing Default = Smoothing.Taa;

        public static CameraSmoothing Camera(Smoothing choice, bool temporalFits) => choice switch
        {
            Smoothing.Off or Smoothing.Fxaa => CameraSmoothing.None,
            Smoothing.Smaa => CameraSmoothing.Smaa,
            _ => temporalFits ? CameraSmoothing.Temporal : CameraSmoothing.Smaa,
        };

        /// <summary>The pass over the finished picture, after the style's filter.</summary>
        public static bool FinalPass(Smoothing choice, bool temporalFits) => choice switch
        {
            Smoothing.Off => false,
            Smoothing.Taa => !temporalFits, // (the fallback is the old pair)
            _ => true,
        };

        public static Smoothing Next(Smoothing choice) => (Smoothing)(((int)choice + 1) % Count);

        public static Smoothing Clamp(int saved) => saved >= 0 && saved < Count ? (Smoothing)saved : Default;

        public const int Count = 5;

        /// <summary>The choice's name in the tables of strings.</summary>
        public static string Key(Smoothing choice) => choice switch
        {
            Smoothing.Off => "menu.smoothing_off",
            Smoothing.Fxaa => "menu.smoothing_fxaa",
            Smoothing.Smaa => "menu.smoothing_smaa",
            Smoothing.Taa => "menu.smoothing_taa",
            _ => "menu.smoothing_taa_fxaa",
        };

        /// <summary>Frames a second with no vertical sync (the screen tears, the picture is not held back).</summary>
        public const int UnsyncedRate = 144;
    }
}
