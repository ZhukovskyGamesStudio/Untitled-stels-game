using UnityEngine;

namespace Stealth.Player
{
    /// <summary>Per-machine player preferences (PlayerPrefs).</summary>
    public static class GameSettings
    {
        const string Prefix = "stealth.";

        public const float DefaultSensitivity = 0.1f;
        public const float DefaultFov = 75f;
        public const float DefaultVolume = 0.8f;
        public const float DefaultMusicVolume = 0.7f;

        public static float Sensitivity { get; set; } = DefaultSensitivity;
        public static float Fov { get; set; } = DefaultFov;
        public static bool InvertY { get; set; }
        public static bool HeadBob { get; set; } = true;
        public static bool CrouchToggle { get; set; }
        /// <summary>Master volume (0..1).</summary>
        public static float MasterVolume { get; set; } = DefaultVolume;
        /// <summary>Music volume (0..1), on top of the master volume.</summary>
        public static float MusicVolume { get; set; } = DefaultMusicVolume;
        /// <summary>How edges are smoothed (<see cref="SmoothingRules"/>).</summary>
        public static Smoothing Smoothing { get; set; } = SmoothingRules.Default;
        /// <summary>The style's light and colour after their rework (off: as they were before it). Picture only.</summary>
        public static bool NewLighting { get; set; } = true;
        /// <summary>The models' own baked maps and the 2K surface scans (off: without the maps, the scans as they were at 1K). Picture only.</summary>
        public static bool NewTextures { get; set; } = true;
        /// <summary>How big the textures are read (<see cref="TextureSizeRules"/>). Picture only.</summary>
        public static TextureSize TextureSize { get; set; } = TextureSizeRules.Default;
        /// <summary>The frame rate and the frame's time in a corner of the screen (<see cref="FrameMeter"/>).</summary>
        public static bool ShowFps { get; set; }
        /// <summary>Frames wait for the screen's refresh: no tearing.</summary>
        public static bool VSync { get; set; } = true;

        static bool s_Loaded;

        public static void EnsureLoaded()
        {
            if (s_Loaded) return;
            s_Loaded = true;
            Load();
            Controls.Load();
        }

        public static void Load()
        {
            Sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "sens", DefaultSensitivity), 0.01f, 1f);
            Fov = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "fov", DefaultFov), 60f, 100f);
            InvertY = PlayerPrefs.GetInt(Prefix + "invertY", 0) == 1;
            HeadBob = PlayerPrefs.GetInt(Prefix + "headBob", 1) == 1;
            CrouchToggle = PlayerPrefs.GetInt(Prefix + "crouchToggle", 0) == 1;
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "volume", DefaultVolume));
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "music", DefaultMusicVolume));
            Smoothing = SmoothingRules.Clamp(PlayerPrefs.GetInt(Prefix + "smoothing", (int)SmoothingRules.Default));
            VSync = PlayerPrefs.GetInt(Prefix + "vsync", 1) == 1;
            ShowFps = PlayerPrefs.GetInt(Prefix + "showFps", 0) == 1;
            NewLighting = PlayerPrefs.GetInt(Prefix + "lighting", 1) == 1;
            NewTextures = PlayerPrefs.GetInt(Prefix + "textures", 1) == 1;
            TextureSize = TextureSizeRules.Clamp(PlayerPrefs.GetInt(Prefix + "textureSize", (int)TextureSizeRules.Default));
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "sens", Sensitivity);
            PlayerPrefs.SetFloat(Prefix + "fov", Fov);
            PlayerPrefs.SetInt(Prefix + "invertY", InvertY ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "headBob", HeadBob ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "crouchToggle", CrouchToggle ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "volume", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "music", MusicVolume);
            PlayerPrefs.SetInt(Prefix + "smoothing", (int)Smoothing);
            PlayerPrefs.SetInt(Prefix + "vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "showFps", ShowFps ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "lighting", NewLighting ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "textures", NewTextures ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "textureSize", (int)TextureSize);
            Controls.Save();
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            Sensitivity = DefaultSensitivity;
            Fov = DefaultFov;
            InvertY = false;
            HeadBob = true;
            CrouchToggle = false;
            MasterVolume = DefaultVolume;
            MusicVolume = DefaultMusicVolume;
            Smoothing = SmoothingRules.Default;
            VSync = true;
            ShowFps = false;
            NewLighting = true;
            NewTextures = true;
            TextureSize = TextureSizeRules.Default;
        }
    }
}
