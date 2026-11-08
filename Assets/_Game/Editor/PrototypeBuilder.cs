using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using Stealth.Net;
using Stealth.Player;
using Stealth.UI;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Stealth.EditorTools
{
    /// <summary>
    /// Builds every prototype asset from code: materials, prefabs (props, items, player, robots, cameras, lasers),
    /// network prefab list and the Game scene. Safe to re-run: assets are overwritten.
    /// Menu: Stealth / Build Prototype Assets. Batch: -executeMethod Stealth.EditorTools.PrototypeBuilder.BuildAllBatch
    /// </summary>
    public static class PrototypeBuilder
    {
        const string Root = "Assets/_Game";
        const string Models = Root + "/Models";
        const string Mats = Root + "/Materials";
        const string Prefabs = Root + "/Prefabs";
        const string Data = Root + "/Data";
        const string ScenePath = Root + "/Scenes/Game.unity";

        static readonly Dictionary<string, Material> s_Mats = new();

        [MenuItem("Stealth/Build Prototype Assets")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Prefabs + "/Props");
            Directory.CreateDirectory(Data);
            ConfigureModelImport();
            BuildMaterials();

            var art = BuildLevelArt();
            var visuals = BuildItemVisuals();
            var item = BuildWorldItem(visuals);
            var player = BuildPlayer();
            var guard = BuildRobot("RobotGuard", "robot_guard", true);
            var listener = BuildRobot("RobotListener", "robot_listener", false);
            var cam = BuildSecurityCamera();
            var laser = BuildLaser();
            var session = BuildSession(art, item, guard, listener, cam, laser);

            var networkPrefabs = new[] { player, item, guard, listener, cam, laser, session };
            FixNetworkHashes(networkPrefabs);
            var list = BuildPrefabList(networkPrefabs);
            BuildScene(player, session, list);
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Builder] Prototype assets built");
        }

        public static void BuildAllBatch()
        {
            try
            {
                BuildAll();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

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
            Debug.Log($"[Builder] Player build: {report.summary.result}, {report.summary.totalErrors} errors");
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded && File.Exists("steam_appid.txt"))
                File.Copy("steam_appid.txt", Path.Combine(Path.GetDirectoryName(exe)!, "steam_appid.txt"), true);
        }

        public static void BuildPlayerBatch()
        {
            BuildPlayerWindows();
            EditorApplication.Exit(0);
        }

        // ------------------------------------------------------------------ materials

        static void ConfigureModelImport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Models }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                bool dirty = false;
                if (importer.materialImportMode != ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
                if (importer.importAnimation) { importer.importAnimation = false; dirty = true; }
                if (importer.animationType != ModelImporterAnimationType.None) { importer.animationType = ModelImporterAnimationType.None; dirty = true; }
                if (importer.isReadable) { importer.isReadable = false; dirty = true; }
                if (dirty) importer.SaveAndReimport();
            }
        }

        static Material Mat(string name, Color color, Color? emission = null, bool unlit = false, float smoothness = 0.25f)
        {
            var path = $"{Mats}/{name}.mat";
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", color);
            if (!unlit) mat.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else mat.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(mat);
            s_Mats[name] = mat;
            return mat;
        }

        static void BuildMaterials()
        {
            Mat("Wall", new Color(0.78f, 0.77f, 0.72f));
            Mat("Floor", new Color(0.30f, 0.32f, 0.35f), smoothness: 0.45f);
            Mat("Ceiling", new Color(0.86f, 0.86f, 0.84f));
            Mat("OutsideGround", new Color(0.24f, 0.29f, 0.22f));
            Mat("DoorFrame", new Color(0.45f, 0.38f, 0.30f));
            Mat("PropBody", new Color(0.62f, 0.60f, 0.56f));
            Mat("PropAccent", new Color(0.22f, 0.23f, 0.26f));
            Mat("PropScreen", new Color(0.15f, 0.35f, 0.5f), new Color(0.1f, 0.35f, 0.6f));
            Mat("Plant", new Color(0.25f, 0.55f, 0.25f));
            Mat("PlantPot", new Color(0.55f, 0.35f, 0.25f));
            Mat("Rack", new Color(0.12f, 0.13f, 0.15f), smoothness: 0.5f);
            Mat("RackLights", new Color(0.2f, 1f, 0.4f), new Color(0.2f, 1.6f, 0.5f));
            Mat("Hatch", new Color(0.32f, 0.32f, 0.33f), smoothness: 0.6f);
            Mat("HatchRing", new Color(1f, 0.8f, 0.1f), new Color(0.8f, 0.6f, 0.05f));
            Mat("RobotBody", new Color(0.86f, 0.87f, 0.9f), smoothness: 0.6f);
            Mat("RobotAccent", new Color(0.18f, 0.19f, 0.22f), smoothness: 0.5f);
            Mat("ListenerBody", new Color(0.95f, 0.55f, 0.15f), smoothness: 0.5f);
            Mat("SensorGlow", new Color(0.2f, 0.85f, 1f), new Color(0.4f, 1.7f, 2.4f));
            Mat("PlayerBody", Color.white);
            Mat("PlayerAccent", new Color(0.9f, 0.78f, 0.65f));
            Mat("PlayerGlasses", new Color(0.05f, 0.05f, 0.06f), smoothness: 0.9f);
            Mat("Laser", new Color(1f, 0.1f, 0.05f), unlit: true);
            Mat("LaserPost", new Color(0.15f, 0.15f, 0.17f), smoothness: 0.6f);
            for (int i = 0; i < ItemCatalog.Colors.Length; i++)
            {
                var c = ItemCatalog.Colors[i].Color;
                Mat($"Item_{i}", c, c * 0.7f, smoothness: 0.55f); // glow a bit so loot is readable in dark rooms
            }
        }

        // ------------------------------------------------------------------ model helpers

        static GameObject Model(string file)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{file}.fbx");
            if (asset == null) throw new FileNotFoundException($"Model {file}.fbx not found — run Tools/Blender/generate_models.py");
            var go = (GameObject)Object.Instantiate(asset);
            go.name = "Model";
            return go;
        }

        /// <summary>Assigns materials by child name: Body / Accent / Eye.</summary>
        static void Paint(GameObject model, string body, string accent, string eye)
        {
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.gameObject.name;
                string mat = n.StartsWith("Eye") ? eye : n.StartsWith("Accent") ? accent : body;
                r.sharedMaterial = s_Mats[mat];
            }
        }

        static Renderer[] Parts(GameObject model, string prefix) =>
            model.GetComponentsInChildren<Renderer>(true).Where(r => r.gameObject.name.StartsWith(prefix)).ToArray();

        static Bounds LocalBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var b = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var r in renderers)
            {
                var rb = r.bounds;
                var min = root.transform.InverseTransformPoint(rb.min);
                var max = root.transform.InverseTransformPoint(rb.max);
                var lb = new Bounds((min + max) / 2f, new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), Mathf.Abs(max.z - min.z)));
                if (first) { b = lb; first = false; }
                else b.Encapsulate(lb);
            }
            return b;
        }

        static GameObject Save(GameObject go, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ------------------------------------------------------------------ props

        static GameObject Prop(string name, string file, string body, string accent, string eye, bool collider = true)
        {
            var root = new GameObject(name);
            var model = Model(file);
            model.transform.SetParent(root.transform, false);
            Paint(model, body, accent, eye);
            var b = LocalBounds(root);
            if (collider)
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
            }
            Debug.Log($"[Builder] {name}: size {b.size}");
            return root;
        }

        static LevelArt BuildLevelArt()
        {
            const string path = Data + "/LevelArt.asset";
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>(path);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<LevelArt>();
                AssetDatabase.CreateAsset(art, path);
            }
            art.Wall = s_Mats["Wall"];
            art.Floor = s_Mats["Floor"];
            art.Ceiling = s_Mats["Ceiling"];
            art.OutsideGround = s_Mats["OutsideGround"];
            art.DoorFrame = s_Mats["DoorFrame"];
            string p = Prefabs + "/Props/";
            art.Desk = Save(Prop("Desk", "desk", "PropBody", "PropAccent", "PropScreen"), p + "Desk.prefab");
            art.Chair = Save(Prop("Chair", "chair", "PropAccent", "PropBody", "PropScreen"), p + "Chair.prefab");
            art.ServerRack = Save(Prop("ServerRack", "server_rack", "Rack", "Rack", "RackLights"), p + "ServerRack.prefab");
            art.Cabinet = Save(Prop("Cabinet", "cabinet", "PropBody", "PropAccent", "PropScreen"), p + "Cabinet.prefab");
            art.MeetingTable = Save(Prop("MeetingTable", "meeting_table", "PropBody", "PropAccent", "PropScreen"), p + "MeetingTable.prefab");
            art.Plant = Save(Prop("Plant", "plant", "Plant", "PlantPot", "Plant"), p + "Plant.prefab");
            art.KitchenCounter = Save(Prop("KitchenCounter", "kitchen_counter", "PropBody", "PropAccent", "PropAccent"), p + "KitchenCounter.prefab");
            var hatch = Prop("Hatch", "hatch", "Hatch", "PropAccent", "HatchRing");
            hatch.AddComponent<Hatch>();
            var box = hatch.GetComponent<BoxCollider>();
            box.size = new Vector3(box.size.x, 0.3f, box.size.z); // easy target for dropping items
            box.center = new Vector3(box.center.x, 0.1f, box.center.z);
            art.Hatch = Save(hatch, p + "Hatch.prefab");
            EditorUtility.SetDirty(art);
            return art;
        }

        // ------------------------------------------------------------------ items

        static ItemVisuals BuildItemVisuals()
        {
            const string path = Data + "/ItemVisuals.asset";
            var visuals = AssetDatabase.LoadAssetAtPath<ItemVisuals>(path);
            if (visuals == null)
            {
                visuals = ScriptableObject.CreateInstance<ItemVisuals>();
                AssetDatabase.CreateAsset(visuals, path);
            }
            visuals.Meshes = ItemCatalog.Shapes
                .Select(s => AssetDatabase.LoadAllAssetsAtPath($"{Models}/item_{s.Id}.fbx").OfType<Mesh>().First())
                .ToArray();
            visuals.ColorMaterials = Enumerable.Range(0, ItemCatalog.Colors.Length).Select(i => s_Mats[$"Item_{i}"]).ToArray();
            EditorUtility.SetDirty(visuals);
            return visuals;
        }

        static GameObject BuildWorldItem(ItemVisuals visuals)
        {
            var root = new GameObject("WorldItem");
            root.AddComponent<NetworkObject>();
            var mf = root.AddComponent<MeshFilter>();
            var mr = root.AddComponent<MeshRenderer>();
            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            mf.sharedMesh = visuals.Meshes[0];
            mr.sharedMaterial = visuals.ColorMaterials[0];
            var item = root.AddComponent<WorldItem>();
            item.Visuals = visuals;
            item.MeshFilter = mf;
            item.Renderer = mr;
            item.Collider = col;
            return Save(root, Prefabs + "/WorldItem.prefab");
        }

        // ------------------------------------------------------------------ player

        static GameObject BuildPlayer()
        {
            var root = new GameObject("Player");
            var cc = root.AddComponent<CharacterController>();
            cc.height = PlayerController.StandHeight;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, PlayerController.StandHeight / 2f, 0);
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.04f;
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;

            var model = Model("player");
            model.transform.SetParent(root.transform, false);
            Paint(model, "PlayerBody", "PlayerAccent", "PlayerGlasses");

            // Head pivot at the neck: teammates see where this player is looking.
            var headPivot = new GameObject("HeadPivot");
            headPivot.transform.SetParent(model.transform, false);
            headPivot.transform.localPosition = new Vector3(0, 1.3f, 0);
            foreach (var part in model.GetComponentsInChildren<Transform>(true).Where(t => t.name == "AccentHead" || t.name == "EyeGlasses").ToArray())
                part.SetParent(headPivot.transform, true);

            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0, 1.6f, 0);
            var cam = head.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 75f;
            cam.enabled = false;
            var listener = head.AddComponent<AudioListener>();
            listener.enabled = false;

            var motor = root.AddComponent<PlayerMotor>();
            var pc = root.AddComponent<PlayerController>();
            pc.Motor = motor;
            pc.Head = head.transform;
            pc.Camera = cam;
            pc.Listener = listener;
            pc.Model = model.transform;
            pc.HeadPivot = headPivot.transform;
            pc.BodyRenderers = model.GetComponentsInChildren<Renderer>(true);
            var inv = root.AddComponent<PlayerInventory>();
            var ui = root.AddComponent<InventoryUI>();
            ui.Player = pc;
            ui.Inventory = inv;
            return Save(root, Prefabs + "/Player.prefab");
        }

        // ------------------------------------------------------------------ enemies

        static GameObject BuildRobot(string name, string file, bool guard)
        {
            var root = new GameObject(name);
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;

            var model = Model(file);
            model.transform.SetParent(root.transform, false);
            Paint(model, guard ? "RobotBody" : "ListenerBody", "RobotAccent", "SensorGlow");
            var b = LocalBounds(root);

            var col = root.AddComponent<CapsuleCollider>();
            col.height = b.size.y;
            col.radius = guard ? 0.4f : 0.55f;
            col.center = new Vector3(0, b.size.y / 2f, 0);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            agent.radius = col.radius;
            agent.height = b.size.y;
            agent.angularSpeed = 360f;
            agent.acceleration = 14f;
            agent.stoppingDistance = 0.4f;
            agent.autoBraking = true;

            var eye = new GameObject("EyePoint");
            eye.transform.SetParent(root.transform, false);
            eye.transform.localPosition = guard ? new Vector3(0, 1.84f, 0.3f) : new Vector3(0, 1.2f, 0f);

            var lightGo = new GameObject("StatusLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = guard ? new Vector3(0, 1.85f, 0.45f) : new Vector3(0, 1.35f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3.5f;
            light.intensity = 2.5f;
            light.shadows = LightShadows.None;

            root.AddComponent<DetectorAwareness>().Point = eye.transform;
            var rc = root.AddComponent<RobotController>();
            rc.Eye = eye.transform;
            rc.StatusRenderers = Parts(model, "Eye");
            rc.StatusLight = light;
            if (guard)
            {
                rc.HasEyes = true;
                rc.ViewDistance = 13f;
                rc.ViewAngle = 100f;
                rc.HearingMultiplier = 0.8f;
                rc.ChaseHearingShare = 0f;
                rc.PatrolSpeed = 1.8f;
                rc.InvestigateSpeed = 2.8f;
                rc.ChaseSpeed = 4.4f;
            }
            else
            {
                rc.HasEyes = false;
                rc.CanUseStairs = false; // wheels
                rc.HearingMultiplier = 1.6f;
                rc.ChaseHearingShare = 0.6f;
                rc.PatrolSpeed = 1.5f;
                rc.InvestigateSpeed = 3.2f;
                rc.ChaseSpeed = 5f;
                rc.LoseTargetAfter = 3f;
                rc.ContactDistance = 1.4f;
            }
            return Save(root, $"{Prefabs}/{name}.prefab");
        }

        static GameObject BuildSecurityCamera()
        {
            var root = new GameObject("SecurityCamera");
            root.AddComponent<NetworkObject>();
            var pivot = new GameObject("Pivot");
            pivot.transform.SetParent(root.transform, false);
            var model = Model("security_camera");
            model.transform.SetParent(pivot.transform, false);
            Paint(model, "RobotAccent", "RobotBody", "SensorGlow");

            var spotGo = new GameObject("Spot");
            spotGo.transform.SetParent(pivot.transform, false);
            spotGo.transform.localPosition = new Vector3(0, -0.02f, 0.25f);
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.shadows = LightShadows.None;

            root.AddComponent<DetectorAwareness>().Point = spotGo.transform;
            var sc = root.AddComponent<SecurityCamera>();
            sc.Pivot = pivot.transform;
            sc.Spot = spot;
            sc.StatusRenderers = Parts(model, "Eye");
            return Save(root, Prefabs + "/SecurityCamera.prefab");
        }

        static GameObject BuildLaser()
        {
            var root = new GameObject("LaserGate");
            root.AddComponent<NetworkObject>();
            Transform Emitter(string n, float yaw)
            {
                var holder = new GameObject(n);
                holder.transform.SetParent(root.transform, false);
                var model = Model("laser_emitter");
                model.transform.SetParent(holder.transform, false);
                model.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                Paint(model, "LaserPost", "LaserPost", "Laser");
                return holder.transform;
            }
            var gate = root.AddComponent<LaserGate>();
            gate.EmitterA = Emitter("EmitterA", 90f);
            gate.EmitterB = Emitter("EmitterB", -90f);
            gate.BeamMaterial = s_Mats["Laser"];
            return Save(root, Prefabs + "/LaserGate.prefab");
        }

        // ------------------------------------------------------------------ session / network

        static GameObject BuildSession(LevelArt art, GameObject item, GameObject guard, GameObject listener, GameObject cam, GameObject laser)
        {
            var root = new GameObject("Session");
            root.AddComponent<NetworkObject>();
            var gm = root.AddComponent<GameManager>();
            gm.Art = art;
            gm.ItemPrefab = item.GetComponent<NetworkObject>();
            gm.GuardRobotPrefab = guard.GetComponent<NetworkObject>();
            gm.ListenerRobotPrefab = listener.GetComponent<NetworkObject>();
            gm.CameraPrefab = cam.GetComponent<NetworkObject>();
            gm.LaserPrefab = laser.GetComponent<NetworkObject>();
            root.AddComponent<GameHUD>();
            return Save(root, Prefabs + "/Session.prefab");
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
                Debug.Log($"[Builder] {prefab.name}: GlobalObjectIdHash {no.PrefabIdHash}");
                if (no.PrefabIdHash == 0) throw new System.Exception($"NetworkObject hash of {prefab.name} is 0");
            }
        }

        static NetworkPrefabsList BuildPrefabList(IEnumerable<GameObject> prefabs)
        {
            const string path = Data + "/NetworkPrefabs.asset";
            AssetDatabase.DeleteAsset(path);
            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            foreach (var p in prefabs) list.Add(new NetworkPrefab { Prefab = p });
            AssetDatabase.CreateAsset(list, path);
            return list;
        }

        static void BuildScene(GameObject player, GameObject session, NetworkPrefabsList list)
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
            menuCam.transform.position = new Vector3(0, 30, -30);
            menuCam.transform.rotation = Quaternion.Euler(40, 0, 0);

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
            net.AddComponent<GameMenu>();
            net.AddComponent<DevAutomation>();

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.21f, 0.22f);

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
    }
}
