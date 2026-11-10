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

        static bool s_Loaded;

        public static void EnsureLoaded()
        {
            if (s_Loaded) return;
            s_Loaded = true;
            Load();
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
        }
    }
}
