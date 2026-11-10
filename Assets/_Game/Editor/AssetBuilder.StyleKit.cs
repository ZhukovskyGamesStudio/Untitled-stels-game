using System.Collections.Generic;
using System.IO;
using System.Linq;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        const string KitModelsDir = ModelsDir + "/StyleKit";
        const string KitPrefabsDir = PrefabsDir + "/StyleKit";
        const string KitMaterialsDir = StylesDir + "/Kit";
        const string FinishTexturesDir = Root + "/Art/Textures/Finishes";
        const string StyleModelsDir = ModelsDir + "/Styles";

        /// <summary>Materials of the style architecture by name (filled by <see cref="BuildKitMaterials"/>).</summary>
        static readonly Dictionary<string, Material> s_Kit = new();

        static Material K(string name) => s_Kit.TryGetValue(name, out var m) ? m : null;

        /// <summary>
        /// Lit materials for the style architecture and the floor and wall finishes (textures from
        /// Tools/Art/generate_textures.py). Rebuilt every time: they are generated, not tuned by hand.
        /// </summary>
        static void BuildKitMaterials()
        {
            EnsureFolder(KitMaterialsDir);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { FinishTexturesDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                bool glow = path.EndsWith("_glow.png");
                if (imp.wrapMode == TextureWrapMode.Repeat && imp.anisoLevel == 4 && imp.sRGBTexture == !glow) continue;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.anisoLevel = 4;
                imp.mipmapEnabled = true;
                imp.sRGBTexture = !glow;
                imp.SaveAndReimport();
            }

            Texture2D T(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{FinishTexturesDir}/{n}.png");
            void Mat(string name, Color color, string tex = null, float smooth = 0.25f, float metal = 0f, string glowTex = null, Color? glow = null)
            {
                var path = $"{KitMaterialsDir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(m, path);
                }
                m.SetColor("_BaseColor", color);
                m.SetTexture("_BaseMap", tex != null ? T(tex) : null);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", metal);
                if (glow.HasValue)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", glow.Value);
                    m.SetTexture("_EmissionMap", glowTex != null ? T(glowTex) : null);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                else
                {
                    m.DisableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }
                EditorUtility.SetDirty(m);
                s_Kit[name] = m;
            }

            // floors
            Mat("Floor_Diamond", new Color(0.5f, 0.46f, 0.4f), "diamondplate", 0.45f, 0.6f);
            Mat("Floor_Checker", new Color(0.86f, 0.8f, 0.7f), "checker", 0.55f);
            Mat("Floor_Marble", new Color(0.93f, 0.9f, 0.84f), "marble", 0.75f);
            Mat("Floor_Planks", new Color(0.5f, 0.36f, 0.24f), "planks", 0.3f);
            Mat("Floor_PlanksPale", new Color(0.78f, 0.76f, 0.7f), "planks", 0.2f);
            Mat("Floor_Parquet", new Color(0.62f, 0.46f, 0.3f), "parquet", 0.45f);
            Mat("Floor_ToonTiles", new Color(0.62f, 0.8f, 0.9f), "bigtiles", 0.3f);
            Mat("Floor_Concrete", new Color(0.72f, 0.72f, 0.7f), "concrete", 0.15f);
            Mat("Floor_ConcreteWhite", new Color(0.96f, 0.97f, 0.99f), "concrete", 0.3f);
            Mat("Floor_Grid", new Color(0.32f, 0.32f, 0.38f), "gridtiles", 0.6f, 0f, "gridtiles_glow", new Color(0.2f, 1.6f, 1.8f));
            // walls
            Mat("Wall_Corrugated", new Color(0.42f, 0.48f, 0.44f), "corrugated", 0.35f, 0.45f);
            Mat("Wall_Damask", new Color(0.5f, 0.14f, 0.13f), "damask", 0.2f);
            Mat("Wall_PlasterWarm", new Color(0.92f, 0.86f, 0.76f), "plaster", 0.15f);
            Mat("Wall_PlasterOld", new Color(0.74f, 0.7f, 0.58f), "plaster", 0.1f);
            Mat("Wall_PlasterPale", new Color(0.74f, 0.8f, 0.88f), "plaster", 0.15f);
            Mat("Wall_PlasterToon", new Color(0.98f, 0.72f, 0.5f), "plaster", 0.1f);
            Mat("Wall_Brick", new Color(0.66f, 0.38f, 0.3f), "brick", 0.1f);
            Mat("Wall_Boards", new Color(0.78f, 0.76f, 0.68f), "boards", 0.15f);
            Mat("Wall_Hatch", new Color(0.78f, 0.78f, 0.76f), "hatch", 0.1f);
            Mat("Wall_Panels", new Color(0.96f, 0.97f, 0.99f), "panels", 0.35f);
            Mat("Wall_Circuit", new Color(0.24f, 0.24f, 0.3f), "circuit", 0.5f, 0f, "circuit_glow", new Color(0.15f, 1.3f, 1.5f));
            // trims and parts
            Mat("Trim_WoodDark", new Color(0.24f, 0.14f, 0.08f), null, 0.45f);
            Mat("Trim_WoodMid", new Color(0.46f, 0.3f, 0.18f), null, 0.35f);
            Mat("Trim_White", new Color(0.93f, 0.93f, 0.9f), null, 0.3f);
            Mat("Trim_PaleBlue", new Color(0.6f, 0.68f, 0.78f), null, 0.3f);
            Mat("Trim_Stone", new Color(0.84f, 0.82f, 0.76f), "marble", 0.55f);
            Mat("Trim_Metal", new Color(0.42f, 0.44f, 0.47f), null, 0.5f, 0.8f);
            Mat("Trim_Rust", new Color(0.42f, 0.27f, 0.17f), null, 0.3f, 0.4f);
            Mat("Trim_Brass", new Color(0.78f, 0.62f, 0.32f), null, 0.6f, 1f);
            Mat("Trim_Black", new Color(0.07f, 0.07f, 0.08f), null, 0.4f);
            Mat("Trim_Toon", new Color(0.98f, 0.55f, 0.22f), null, 0.2f);
            Mat("Trim_Olive", new Color(0.32f, 0.3f, 0.2f), null, 0.2f);
            Mat("Pipe_Green", new Color(0.24f, 0.36f, 0.28f), null, 0.4f, 0.3f);
            Mat("Panel_WoodDark", new Color(0.3f, 0.18f, 0.1f), "boards", 0.4f);
            Mat("Panel_PaleBlue", new Color(0.66f, 0.74f, 0.84f), null, 0.3f);
            Mat("Glow_White", Color.white, null, 0.5f, 0f, null, new Color(2.4f, 2.6f, 2.8f));
            Mat("Glow_Cyan", new Color(0.2f, 1f, 1f), null, 0.5f, 0f, null, new Color(0.3f, 2.6f, 3f));
            AssetDatabase.SaveAssets();
        }

        /// <summary>Architecture kits of every family (index = ModelFamily; None = the plain kit).</summary>
        static StyleKit[] BuildStyleKits()
        {
            var families = (ModelFamily[])System.Enum.GetValues(typeof(ModelFamily));
            var kits = new StyleKit[families.Length];
            foreach (var f in families) kits[(int)f] = BuildStyleKit(f);
            return kits;
        }

        /// <summary>Prefabs of the architecture pieces: the kit models, no colliders, no shadows (they must not change the light).</summary>
        static StyleKit BuildStyleKit(ModelFamily family)
        {
            bool plain = family == ModelFamily.None;
            string models = plain ? KitModelsDir : $"{KitModelsDir}/{family}";
            string prefabs = plain ? KitPrefabsDir : $"{KitPrefabsDir}/{family}";
            string asset = plain ? $"{StylesDir}/StyleKit.asset" : $"{StylesDir}/StyleKit_{family}.asset";
            if (!plain && !AssetDatabase.IsValidFolder(models)) return null;
            EnsureFolder(prefabs);
            var files = new Dictionary<DecorKind, string>
            {
                [DecorKind.Baseboard] = "kit_baseboard", [DecorKind.Crown] = "kit_crown", [DecorKind.Rail] = "kit_rail",
                [DecorKind.Wainscot] = "kit_wainscot", [DecorKind.Pipes] = "kit_pipes", [DecorKind.PipeClamp] = "kit_pipe_clamp",
                [DecorKind.Strip] = "kit_strip", [DecorKind.Pilaster] = "kit_pilaster", [DecorKind.Casing] = "kit_casing",
                [DecorKind.Arch] = "kit_arch", [DecorKind.Beam] = "kit_beam", [DecorKind.IBeam] = "kit_ibeam",
                [DecorKind.Coffer] = "kit_coffer", [DecorKind.Tray] = "kit_tray", [DecorKind.TrayHanger] = "kit_tray_hanger",
            };
            var kit = AssetDatabase.LoadAssetAtPath<StyleKit>(asset);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<StyleKit>();
                AssetDatabase.CreateAsset(kit, asset);
            }
            kit.Family = family;
            int count = System.Enum.GetValues(typeof(DecorKind)).Length;
            kit.Pieces = new GameObject[count];
            foreach (var (kind, file) in files)
            {
                var path = $"{models}/{file}.fbx";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"[Builder] {path} missing: run Tools/Blender/style_kit.py"); continue; }
                kit.Pieces[(int)kind] = Prefab($"{prefabs}/{kind}.prefab", () =>
                {
                    var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    go.name = kind.ToString();
                    foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                    {
                        r.shadowCastingMode = ShadowCastingMode.Off;
                        r.sharedMaterial = K(r.gameObject.name.StartsWith("Eye") ? "Glow_White" : "Trim_White");
                    }
                    return go;
                });
            }
            kit.Finish = Prefab($"{KitPrefabsDir}/Finish.prefab", () =>
            {
                var go = new GameObject("Finish");
                go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.sharedMaterial = K("Floor_Concrete");
                return go;
            });
            EditorUtility.SetDirty(kit);
            return kit;
        }

        /// <summary>Every model mesh and its variant per family (Art/Models/Styles/&lt;Family&gt;/…/&lt;name&gt;__&lt;family&gt;.fbx), matched by mesh name.</summary>
        static MeshVariants[] BuildMeshVariants()
        {
            var sets = new List<MeshVariants>();
            foreach (ModelFamily f in System.Enum.GetValues(typeof(ModelFamily)))
            {
                if (f == ModelFamily.None) continue;
                var dir = $"{StyleModelsDir}/{f}";
                if (!AssetDatabase.IsValidFolder(dir)) continue;
                var from = new List<Mesh>();
                var to = new List<Mesh>();
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { dir }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var file = Path.GetFileNameWithoutExtension(path);
                    int cut = file.LastIndexOf("__", System.StringComparison.Ordinal);
                    if (cut < 0) continue;
                    string original;
                    try { original = ModelPath(file.Substring(0, cut)); }
                    catch (FileNotFoundException) { continue; }
                    var originals = AssetDatabase.LoadAllAssetsAtPath(original).OfType<Mesh>().ToDictionary(m => m.name, m => m);
                    foreach (var variant in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
                        if (originals.TryGetValue(variant.name, out var o))
                        {
                            from.Add(o);
                            to.Add(variant);
                        }
                }
                sets.Add(new MeshVariants { Family = f, From = from.ToArray(), To = to.ToArray() });
            }
            return sets.ToArray();
        }
    }
}
