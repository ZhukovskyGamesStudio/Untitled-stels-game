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

        /// <summary>
        /// REBUILD in an open editor (its process cannot be given the variable): the prefab names, comma separated, from
        /// Temp/rebuild.txt; then the missing assets as usual.
        /// </summary>
        [MenuItem("Stealth/Rebuild Listed Prefabs (Temp-rebuild.txt)")]
        public static void RebuildListed()
        {
            const string file = "Temp/rebuild.txt";
            string list = File.Exists(file) ? File.ReadAllText(file).Trim() : "";
            if (list.Length == 0) { Debug.LogWarning($"[Builder] {file} is empty: nothing to rebuild"); return; }
            Environment.SetEnvironmentVariable("REBUILD", list);
            try { BuildAll(false); }
            finally { Environment.SetEnvironmentVariable("REBUILD", null); }
            Debug.Log($"[Builder] rebuilt {list}");
        }

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
                var car = BuildSewerPrefab(art, ui.RouteSign);
                BuildSceneAndSession(art, actors, ui, car);
                BuildRoomsTest();
                SharpPrints();
                Shapes();
                OnlyGlossMirrors();
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

        /// <summary>
        /// Points a field of an asset at what was just built (a field newer than the asset gets filled; nothing else is
        /// touched): the one line a new <see cref="LevelArt"/> field needs.
        /// </summary>
        static void Link<T>(Object asset, ref T field, T value) where T : Object
        {
            if (field == value) return;
            field = value;
            EditorUtility.SetDirty(asset);
        }

        /// <summary><see cref="Link{T}"/> for an array of assets (compared item by item).</summary>
        static void LinkAll<T>(Object asset, ref T[] field, T[] value) where T : Object
        {
            if (field != null && value != null && System.Linq.Enumerable.SequenceEqual(field, value)) return;
            field = value;
            EditorUtility.SetDirty(asset);
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

        /// <summary>
        /// Like <see cref="Cube"/> without a collider, but a box with its edges chamfered (<paramref name="chamfer"/> m): door
        /// linings, casings and lamp housings read as made things, not as the raw cube's knife edges. The mesh is built
        /// here, once per size, and saved under Art/Models/Generated (no runtime geometry).
        /// </summary>
        static GameObject ChamferBox(Transform parent, string name, Vector3 center, Vector3 size, Material mat, float chamfer = 0.006f)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.AddComponent<MeshFilter>().sharedMesh = ChamferMesh(size, Mathf.Min(chamfer, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.3f));
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        const string GeneratedDir = ModelsDir + "/Generated";

        /// <summary>A chamfered box mesh of this size (flat faces, chamfer strips and corner triangles; UVs in metres), cached as an asset.</summary>
        static Mesh ChamferMesh(Vector3 size, float c)
        {
            string key = $"box_{size.x:0.###}x{size.y:0.###}x{size.z:0.###}_{c:0.####}".Replace(',', '.');
            string path = $"{GeneratedDir}/{key}.asset";
            var have = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (have != null && !s_Force) return have;
            Vector3 h = size / 2f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Poly(Vector3 normal, params Vector3[] p)
            {
                int b = verts.Count;
                verts.AddRange(p);
                // fan, wound so the face looks along its normal
                // Unity's face normal is Cross(p1 − p0, p2 − p0) (left-handed, clockwise front): reverse when it points inwards
                bool flip = Vector3.Dot(Vector3.Cross(p[1] - p[0], p[2] - p[0]), normal) < 0f;
                for (int i = 1; i + 1 < p.Length; i++)
                {
                    if (flip) { tris.Add(b); tris.Add(b + i + 1); tris.Add(b + i); }
                    else { tris.Add(b); tris.Add(b + i); tris.Add(b + i + 1); }
                }
            }
            Vector3 V(int a, float va, int b, float vb, int k, float vk)
            {
                var v = Vector3.zero;
                v[a] = va; v[b] = vb; v[k] = vk;
                return v;
            }
            for (int a = 0; a < 3; a++)
            {
                int b = (a + 1) % 3, k = (a + 2) % 3;
                foreach (float sa in new[] { -1f, 1f })
                {
                    // the face
                    var n = Vector3.zero; n[a] = sa;
                    Poly(n, V(a, sa * h[a], b, -(h[b] - c), k, -(h[k] - c)), V(a, sa * h[a], b, h[b] - c, k, -(h[k] - c)),
                        V(a, sa * h[a], b, h[b] - c, k, h[k] - c), V(a, sa * h[a], b, -(h[b] - c), k, h[k] - c));
                    // the chamfer strips between this face and the next axis's faces (each edge once)
                    foreach (float sb in new[] { -1f, 1f })
                    {
                        var en = Vector3.zero; en[a] = sa; en[b] = sb;
                        Poly(en.normalized, V(a, sa * h[a], b, sb * (h[b] - c), k, -(h[k] - c)), V(a, sa * (h[a] - c), b, sb * h[b], k, -(h[k] - c)),
                            V(a, sa * (h[a] - c), b, sb * h[b], k, h[k] - c), V(a, sa * h[a], b, sb * (h[b] - c), k, h[k] - c));
                    }
                }
            }
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sy in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                        Poly(new Vector3(sx, sy, sz).normalized, new Vector3(sx * h.x, sy * (h.y - c), sz * (h.z - c)),
                            new Vector3(sx * (h.x - c), sy * h.y, sz * (h.z - c)), new Vector3(sx * (h.x - c), sy * (h.y - c), sz * h.z));
            var uvs = new List<Vector2>(new Vector2[verts.Count]); // box-projected, in metres, by each vertex's normal (below)
            var mesh = have != null ? have : new Mesh();
            mesh.Clear();
            mesh.name = key;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            var normals = mesh.normals;
            for (int i = 0; i < verts.Count; i++)
            {
                var an = new Vector3(Mathf.Abs(normals[i].x), Mathf.Abs(normals[i].y), Mathf.Abs(normals[i].z));
                var v = verts[i] + h;
                uvs[i] = an.x >= an.y && an.x >= an.z ? new Vector2(v.z, v.y) : an.y >= an.z ? new Vector2(v.x, v.z) : new Vector2(v.x, v.y);
            }
            mesh.SetUVs(0, uvs);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            if (have == null)
            {
                EnsureFolder(GeneratedDir);
                AssetDatabase.CreateAsset(mesh, path);
            }
            else EditorUtility.SetDirty(mesh);
            return mesh;
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
                if (path.StartsWith(ModelsDir + "/Humans/")) continue; // rigged and animated: AssetBuilder.Humans sets them up
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
