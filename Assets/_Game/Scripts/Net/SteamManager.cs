using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace Stealth.Net
{
    /// <summary>
    /// Initializes Steamworks and pumps its callbacks. Uses AppID from steam_appid.txt (480 = Spacewar for development).
    /// If Steam is not running the game still works through the local (UDP) transport.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class SteamManager : MonoBehaviour
    {
        public static SteamManager Instance { get; private set; }
        public static bool Initialized { get; private set; }
        public static string Error { get; private set; } = "";

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
#if !DISABLESTEAMWORKS
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nosteam") >= 0)
            {
                Error = "отключён флагом -nosteam";
                return;
            }
            try
            {
                if (!Packsize.Test() || !DllCheck.Test())
                {
                    Error = "неверная сборка steam_api";
                    return;
                }
                var result = SteamAPI.InitEx(out var message);
                Initialized = result == ESteamAPIInitResult.k_ESteamAPIInitResult_OK;
                Error = Initialized ? "" : $"{result}: {message}";
                if (Initialized) Debug.Log($"[Steam] Initialized as {SteamFriends.GetPersonaName()} ({SteamUser.GetSteamID()})");
                else Debug.LogWarning($"[Steam] Init failed: {Error}");
            }
            catch (System.DllNotFoundException e)
            {
                Error = "steam_api64.dll не найдена";
                Debug.LogWarning("[Steam] " + e.Message);
            }
#else
            Error = "Steamworks отключён для этой платформы";
#endif
        }

        void Update()
        {
#if !DISABLESTEAMWORKS
            if (Initialized) SteamAPI.RunCallbacks();
#endif
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
#if !DISABLESTEAMWORKS
            if (Initialized) SteamAPI.Shutdown();
#endif
            Initialized = false;
        }

        public static string PersonaName
        {
            get
            {
#if !DISABLESTEAMWORKS
                if (Initialized) return SteamFriends.GetPersonaName();
#endif
                return System.Environment.UserName;
            }
        }
    }
}
