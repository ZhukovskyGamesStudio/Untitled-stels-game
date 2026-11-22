using UnityEngine;

namespace Stealth.Audio
{
    /// <summary>The player's voice chat choices (pause → Settings), kept in PlayerPrefs.</summary>
    public static class VoicePrefs
    {
        const string Prefix = "stealth.voice.";
        static bool s_Loaded;
        static VoiceMode s_Mode;
        static float s_Sensitivity, s_Volume;
        static bool s_Suppress;
        static string s_Device;

        /// <summary>Changes whenever a choice changes (the microphone restarts when the device does).</summary>
        public static int Version { get; private set; }

        public static VoiceMode Mode { get { Load(); return s_Mode; } set { Load(); s_Mode = value; Save(); } }
        /// <summary>Voice activation: 0 = only loud speech opens the microphone, 1 = a whisper does.</summary>
        public static float Sensitivity { get { Load(); return s_Sensitivity; } set { Load(); s_Sensitivity = Mathf.Clamp01(value); Save(); } }
        /// <summary>Volume of the other players' voices (0..2).</summary>
        public static float Volume { get { Load(); return s_Volume; } set { Load(); s_Volume = Mathf.Clamp(value, 0f, 2f); Save(); } }
        /// <summary>Noise suppression (RNNoise) on the microphone.</summary>
        public static bool NoiseSuppression { get { Load(); return s_Suppress; } set { Load(); s_Suppress = value; Save(); } }
        /// <summary>The microphone's device name; empty = the system default.</summary>
        public static string Device { get { Load(); return s_Device; } set { Load(); s_Device = value ?? ""; Save(); } }

        static void Load()
        {
            if (s_Loaded) return;
            s_Loaded = true;
            s_Mode = (VoiceMode)Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "mode", (int)VoiceMode.VoiceActivity), 0, (int)VoiceMode.Off);
            s_Sensitivity = PlayerPrefs.GetFloat(Prefix + "sensitivity", 0.5f);
            s_Volume = PlayerPrefs.GetFloat(Prefix + "volume", 1f);
            s_Suppress = PlayerPrefs.GetInt(Prefix + "suppress", 1) != 0;
            s_Device = PlayerPrefs.GetString(Prefix + "device", "");
        }

        static void Save()
        {
            PlayerPrefs.SetInt(Prefix + "mode", (int)s_Mode);
            PlayerPrefs.SetFloat(Prefix + "sensitivity", s_Sensitivity);
            PlayerPrefs.SetFloat(Prefix + "volume", s_Volume);
            PlayerPrefs.SetInt(Prefix + "suppress", s_Suppress ? 1 : 0);
            PlayerPrefs.SetString(Prefix + "device", s_Device);
            Version++;
        }

        public static void ResetToDefaults()
        {
            foreach (var k in new[] { "mode", "sensitivity", "volume", "suppress", "device" }) PlayerPrefs.DeleteKey(Prefix + k);
            s_Loaded = false;
            Load();
            Version++;
        }

        /// <summary>The mode after this one (the settings button cycles them).</summary>
        public static VoiceMode Next(VoiceMode m) => (VoiceMode)(((int)m + 1) % ((int)VoiceMode.Off + 1));
    }
}
