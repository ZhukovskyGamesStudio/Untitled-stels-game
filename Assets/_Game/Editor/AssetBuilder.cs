using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Stealth.EditorTools
{
    /// <summary>
    /// Creates the project's assets from code: materials, prefabs (level pieces, props, player, items, robots,
    /// sensors, FX, UI), data assets, the network prefab list and the Game scene.
    ///
    /// Two modes:
    ///  * <b>missing only</b> (default, menu "Stealth / Build Missing Assets"): existing assets are loaded and kept,
    ///    so hand edits in prefabs survive; only absent ones are created;
    ///  * <b>overwrite</b> ("Stealth / Rebuild All Assets", batch flag -force): everything is regenerated
    ///    in place (GUIDs and network hashes are preserved because paths do not change).
    ///
    /// Tuning assets (Data/Config, …Config.asset next to robot / device prefabs, Resources/GameConfig) are only ever created, never overwritten —
    /// even with -force: they hold hand-tuned balance.
    ///
    /// Batch: Unity -batchmode -executeMethod Stealth.EditorTools.AssetBuilder.BuildAllBatch [-force]
    /// The parts live in partial files: Config, Materials, Level, Actors, UI, Scene.
    /// </summary>
    public static partial class AssetBuilder
    {
        const string Root = "Assets/_Game";
        const string MaterialsDir = Root + "/Art/Materials";
        const string ModelsDir = Root + "/Art/Models";
        const string SpritesDir = Root + "/Art/Sprites";
        const string PrefabsDir = Root + "/Prefabs";
        const string DataDir = Root + "/Data";
        const string ScenePath = Root + "/Scenes/Game.unity";

        static bool s_Force;

        [MenuItem("Stealth/Build Missing Assets")]
        public static void BuildMissing() => BuildAll(false);

        [MenuItem("Stealth/Rebuild All Assets (overwrite)")]
        public static void RebuildAll()
        {
            if (EditorUtility.DisplayDialog("Rebuild all assets",
                    "Regenerate every material, prefab and the Game scene from code? Manual edits in them will be lost.", "Rebuild", "Cancel"))
                BuildAll(true);
        }

        public static void BuildAllBatch()
        {
            try
            {
                BuildAll(Environment.GetCommandLineArgs().Contains("-force"));
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildAll(bool force)
        {
            s_Force = force;
            try
            {
                ConfigureModelImport();
                BuildConfigs();
                BuildMaterials();
                BuildStyles();
                BuildSprites();
                s_AudioDirector = BuildAudioDirector(BuildSoundBank());
                var art = BuildLevelArt();
                var actors = BuildActors();
                var ui = BuildUi();
                BuildSceneAndSession(art, actors, ui);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Builder] Assets built ({(force ? "overwrite" : "missing only")})");
            }
            finally
            {
                s_Force = false;
            }
        }

        // ------------------------------------------------------------------ asset helpers

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Loads the prefab at <paramref name="path"/>, or builds and saves it (always in overwrite mode).</summary>
        static GameObject Prefab(string path, Func<GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !s_Force && !Rebuild(path)) return existing;
            EnsureFolder(Path.GetDirectoryName(path)!.Replace('\\', '/'));
            var go = build();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>REBUILD=AutoDoor,Safe rebuilds just these prefabs (by file name) from code, keeping their GUIDs.</summary>
        static bool Rebuild(string path)
        {
            var list = Environment.GetEnvironmentVariable("REBUILD");
            if (string.IsNullOrEmpty(list)) return false;
            var name = Path.GetFileNameWithoutExtension(path);
            foreach (var n in list.Split(','))
                if (n.Trim() == name) return true;
            return false;
        }

        /// <summary>Loads or creates a ScriptableObject; <paramref name="fill"/> runs on creation and in overwrite mode.</summary>
        static T DataAsset<T>(string path, Action<T> fill) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            bool create = asset == null;
            if (create)
            {
                EnsureFolder(Path.GetDirectoryName(path)!.Replace('\\', '/'));
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            if (create || s_Force)
            {
                fill(asset);
                EditorUtility.SetDirty(asset);
            }
            return asset;
        }

        /// <summary>Instantiates a prefab as a linked (nested) instance under <paramref name="parent"/>.</summary>
        static GameObject Nested(GameObject prefab, Transform parent, string name = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (name != null) go.name = name;
            return go;
        }

        static GameObject Child(Transform parent, string name, Vector3 localPosition = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        /// <summary>A cube primitive (optionally without collider) with a material and no shadows.</summary>
        static GameObject Cube(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        // ------------------------------------------------------------------ models

        static void ConfigureModelImport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                bool dirty = false;
                if (importer.materialImportMode != ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
                if (importer.importAnimation) { importer.importAnimation = false; dirty = true; }
                if (importer.animationType != ModelImporterAnimationType.None) { importer.animationType = ModelImporterAnimationType.None; dirty = true; }
                if (!importer.isReadable) { importer.isReadable = true; dirty = true; } // runtime static batching of props
                if (dirty) importer.SaveAndReimport();
            }
        }

        static string ModelPath(string file)
        {
            foreach (var guid in AssetDatabase.FindAssets(file + " t:Model", new[] { ModelsDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == file) return path;
            }
            throw new FileNotFoundException($"Model {file}.fbx not found under {ModelsDir} — run Tools/Blender/generate_models.py / robots.py");
        }

        static GameObject Model(string file)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(file));
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
                r.sharedMaterial = M(mat);
            }
        }

        static Renderer[] Parts(GameObject model, string prefix) =>
            model.GetComponentsInChildren<Renderer>(true).Where(r => r.gameObject.name.StartsWith(prefix)).ToArray();

        static Transform Find(GameObject root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        static Bounds LocalBounds(GameObject root)
        {
            var b = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
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

        static List<string> s_Log = new();
    }
}
