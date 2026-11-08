using UnityEngine;

namespace Stealth.Player
{
    /// <summary>Per-machine player preferences (PlayerPrefs).</summary>
    public static class GameSettings
    {
        const string Prefix = "stealth.";

        public const float DefaultSensitivity = 0.1f;
        public const float DefaultFov = 75f;

        public static float Sensitivity { get; set; } = DefaultSensitivity;
        public static float Fov { get; set; } = DefaultFov;
        public static bool InvertY { get; set; }
        public static bool HeadBob { get; set; } = true;
        public static bool CrouchToggle { get; set; }

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
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "sens", Sensitivity);
            PlayerPrefs.SetFloat(Prefix + "fov", Fov);
            PlayerPrefs.SetInt(Prefix + "invertY", InvertY ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "headBob", HeadBob ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "crouchToggle", CrouchToggle ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            Sensitivity = DefaultSensitivity;
            Fov = DefaultFov;
            InvertY = false;
            HeadBob = true;
            CrouchToggle = false;
        }
    }
}
