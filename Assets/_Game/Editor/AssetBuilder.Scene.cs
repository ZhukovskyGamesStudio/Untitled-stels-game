using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Stealth.Game;
using Stealth.Level;
using Stealth.Net;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        static void BuildSceneAndSession(LevelArt art, Actors actors, UiPrefabs ui)
        {
            var session = Prefab(PrefabsDir + "/Network/Session.prefab", () =>
            {
                var root = new GameObject("Session");
                root.AddComponent<NetworkObject>();
                var gm = root.AddComponent<GameManager>();
                root.AddComponent<GameManagerNet>();
                gm.Art = art;
                gm.ItemPrefab = actors.Item;
                gm.RobotPrefabs = actors.Robots.ToArray();
                gm.LaserPrefab = actors.Laser;
                return root;
            });

            var networkPrefabs = new List<GameObject> { actors.Player, actors.Item, actors.Laser, session };
            networkPrefabs.AddRange(actors.Robots.Where(r => r != null));
            FixNetworkHashes(networkPrefabs);
            var list = DataAsset<NetworkPrefabsList>(DataDir + "/NetworkPrefabs.asset", l =>
            {
                foreach (var p in l.PrefabList.ToArray()) l.Remove(p);
                foreach (var p in networkPrefabs) l.Add(new NetworkPrefab { Prefab = p });
            });
            // New network prefabs must always be registered, even when the list itself is kept.
            foreach (var p in networkPrefabs)
                if (!list.Contains(p)) { list.Add(new NetworkPrefab { Prefab = p }); EditorUtility.SetDirty(list); }

            if (s_Force || !File.Exists(ScenePath)) BuildScene(actors.Player, session, list, ui);
            ConfigurePlayerSettings();
        }

        static void FixNetworkHashes(IEnumerable<GameObject> prefabs)
        {
            var validate = typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var prefab in prefabs)
            {
                var no = prefab.GetComponent<NetworkObject>();
                if (no.PrefabIdHash == 0 && validate != null)
                {
                    validate.Invoke(no, null);
                    EditorUtility.SetDirty(prefab);
                    PrefabUtility.SavePrefabAsset(prefab);
                }
                if (no.PrefabIdHash == 0) throw new System.Exception($"NetworkObject hash of {prefab.name} is 0");
            }
        }

        static void BuildScene(GameObject player, GameObject session, NetworkPrefabsList list, UiPrefabs ui)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Moonlight");
            var dl = sun.AddComponent<Light>();
            dl.type = LightType.Directional;
            dl.intensity = 0.35f;
            dl.color = new Color(0.7f, 0.8f, 1f);
            dl.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(55f, 30f, 0f);

            var menuCam = new GameObject("MenuCamera");
            var mc = menuCam.AddComponent<Camera>();
            mc.clearFlags = CameraClearFlags.SolidColor;
            mc.backgroundColor = new Color(0.06f, 0.07f, 0.08f);
            menuCam.AddComponent<AudioListener>();
            menuCam.transform.SetPositionAndRotation(new Vector3(0, 30, -30), Quaternion.Euler(40, 0, 0));

            var net = new GameObject("Network");
            var nm = net.AddComponent<NetworkManager>();
            var utp = net.AddComponent<UnityTransport>();
            var steamTransport = net.AddComponent<SteamNetworkingTransport>();
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = utp,
                PlayerPrefab = player,
                TickRate = 30,
                ConnectionApproval = false,
            };
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
            net.AddComponent<SteamManager>();
            net.AddComponent<SteamLobby>();
            var gn = net.AddComponent<GameNetwork>();
            gn.Manager = nm;
            gn.LocalTransport = utp;
            gn.SteamTransport = steamTransport;
            gn.SessionPrefab = session;
            gn.MenuCamera = mc;
            net.AddComponent<DevAutomation>();

            // Interface: pre-built prefabs, placed in the scene as linked instances.
            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            foreach (var prefab in ui.Screens) if (prefab != null) PrefabUtility.InstantiatePrefab(prefab);
            if (s_AudioDirector != null) PrefabUtility.InstantiatePrefab(s_AudioDirector);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.21f, 0.22f);

            EnsureFolder(Path.GetDirectoryName(ScenePath)!.Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
        }

        // ------------------------------------------------------------------ player build

        [MenuItem("Stealth/Build Windows Player")]
        public static void BuildPlayerWindows()
        {
            const string exe = "Builds/Windows/StealthPrototype.exe";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log($"[Builder] Player build: {report.summary.result}, {report.summary.totalErrors} errors, {report.summary.totalSize / (1024 * 1024)} MB");
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded && File.Exists("steam_appid.txt"))
                File.Copy("steam_appid.txt", Path.Combine(Path.GetDirectoryName(exe)!, "steam_appid.txt"), true);
        }

        public static void BuildPlayerBatch()
        {
            BuildPlayerWindows();
            EditorApplication.Exit(0);
        }
    }
}
