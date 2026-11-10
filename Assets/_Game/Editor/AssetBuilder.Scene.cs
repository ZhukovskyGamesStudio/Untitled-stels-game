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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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

            // The moon: shadows from the roof and walls, so it only shines in through the windows (striped by the blinds).
            var sun = new GameObject("Moonlight");
            var dl = sun.AddComponent<Light>();
            dl.type = LightType.Directional;
            dl.intensity = 0.32f;
            dl.color = new Color(0.62f, 0.72f, 1f);
            dl.shadows = LightShadows.Soft;
            dl.shadowStrength = 1f; // anything less lets a share of the moon through every wall and roof
            // Walls are only 20 cm thick: a small bias keeps the moon from leaking through them.
            var moonData = dl.GetUniversalAdditionalLightData();
            moonData.usePipelineSettings = false;
            dl.shadowBias = 0.05f;
            dl.shadowNormalBias = 0.1f;
            sun.AddComponent<LightSource>();
            sun.transform.rotation = Quaternion.Euler(38f, 30f, 0f);

            var menuCam = new GameObject("MenuCamera");
            var mc = menuCam.AddComponent<Camera>();
            ConfigureCamera(mc);
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

            // Night: no ambient at all — a closed room without lamps is pitch black; the lamps, the moon and glowing things do the rest.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = LightMath.NightAmbient;
            RenderSettings.skybox = null;
            RenderSettings.reflectionIntensity = 0f;

            // Post-processing: bloom on lamps and screens, neutral tone mapping, a soft vignette.
            var volume = new GameObject("PostProcess").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = BuildPostProcess();

            EnsureFolder(Path.GetDirectoryName(ScenePath)!.Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        const string PostProcessPath = DataDir + "/PostProcess.asset";

        static VolumeProfile BuildPostProcess()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessPath);
            if (profile != null && !s_Force) return profile;
            if (profile != null) AssetDatabase.DeleteAsset(PostProcessPath);
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostProcessPath);
            T Add<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }
            var bloom = Add<Bloom>();
            bloom.threshold.value = 1f;
            bloom.intensity.value = 0.9f;
            bloom.scatter.value = 0.7f;
            Add<Tonemapping>().mode.value = TonemappingMode.Neutral;
            var vignette = Add<Vignette>();
            vignette.intensity.value = 0.22f;
            vignette.smoothness.value = 0.45f;
            var color = Add<ColorAdjustments>();
            color.postExposure.value = 0.1f;
            color.contrast.value = 8f;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>Post-processing and anti-aliasing on (player camera, menu camera).</summary>
        static void ConfigureCamera(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
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
