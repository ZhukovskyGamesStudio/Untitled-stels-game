using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Stealth.Configs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        const string StylesDir = DataDir + "/Styles";
        const string StyleConfigPath = StylesDir + "/Styles.asset";
        const string StyleMaterialPath = StylesDir + "/StyleScreen.mat";
        const string StyleShaderPath = Root + "/Art/Shaders/StyleScreen.shader";
        const string FxaaMaterialPath = StylesDir + "/ScreenFxaa.mat";
        const string FxaaShaderPath = Root + "/Art/Shaders/ScreenFxaa.shader";
        const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";

        /// <summary>
        /// Visual styles: the whole-screen filter on the PC renderer, the style assets (created once from the definitions
        /// below and then tuned by hand; REBUILD_STYLES=1 recreates them) and the materials of the world sorted by kind.
        /// </summary>
        static void BuildStyles()
        {
            EnsureFolder(StylesDir);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(StyleShaderPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(StyleMaterialPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "StyleScreen" };
                AssetDatabase.CreateAsset(mat, StyleMaterialPath);
            }
            else if (mat.shader != shader) { mat.shader = shader; EditorUtility.SetDirty(mat); }
            AddStyleFeature(mat);
            AddFxaaFeature();
            AddXrayFeature();
            AddWorldTextFeature();
            AddGasFeature();

            BuildKitMaterials();
            BuildExternalKitMaterials();
            BuildHumanRig(); // the scientist: no style shows him now (the derelict station shows the departments too), the prefab stays for a style that wants him
            // REBUILD_STYLES=1: every style from its definition below; REBUILD_STYLES=S16_Derelict,S03_Payday: only these
            var rebuildList = (Environment.GetEnvironmentVariable("REBUILD_STYLES") ?? "").Split(',').Select(n => n.Trim()).Where(n => n.Length > 0).ToList();
            bool rebuildAll = rebuildList.Contains("1");
            var styles = new List<VisualStyle>();
            foreach (var def in StyleDefinitions())
            {
                var path = $"{StylesDir}/{def.name}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<VisualStyle>(path);
                if (asset != null && (rebuildAll || rebuildList.Contains(def.name))) { EditorUtility.CopySerialized(def, asset); asset.name = def.name; EditorUtility.SetDirty(asset); }
                if (asset == null) { AssetDatabase.CreateAsset(def, path); asset = def; }
                FillGenerated(asset, def);
                styles.Add(asset);
            }

            var config = AssetDatabase.LoadAssetAtPath<StyleConfig>(StyleConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<StyleConfig>();
                AssetDatabase.CreateAsset(config, StyleConfigPath);
            }
            config.Styles = styles.ToArray();
            // the game's look (chosen by hand later; first time: the storybook style)
            if (config.Default == null || !styles.Contains(config.Default)) config.Default = styles.FirstOrDefault(s => s.name == "S16_Derelict");
            config.Building = MaterialsIn("Environment");
            config.Furniture = MaterialsIn("Props");
            config.Robots = MaterialsIn("Robots");
            config.Players = MaterialsIn("Player");
            config.Loot = MaterialsIn("Items");
            config.Kits = BuildStyleKits();
            config.Batch = BuildBatchHolder();
            config.Meshes = BuildMeshVariants();
            config.Materials = BuildMaterialVariants();
            config.Wear = BuildWearVariants(config.Materials);
            config.Props = BuildPropReplacements();
            config.StyleModels = BuildStyleModels(styles);
            config.Wardrobe = BuildWardrobe();
            config.PlayerHumans = BuildDepartmentHumans();
            if (config.PlayerOutfits == null || config.PlayerOutfits.Length == 0) config.PlayerOutfits = DefaultOutfits();
            // the palette of a style recolours the family versions of a material too
            Material[] WithVariants(Material[] a) => a.Concat(config.Materials.SelectMany(set =>
                set.From.Select((m, i) => (m, i)).Where(x => a.Contains(x.m)).Select(x => set.To[x.i]))).ToArray();
            config.Building = WithVariants(config.Building);
            config.Furniture = WithVariants(config.Furniture);
            config.Robots = WithVariants(config.Robots);
            config.Players = WithVariants(config.Players);
            // ...and their wear levels
            Material[] WithWear(Material[] a) => a.Concat(config.Wear.Where(w => a.Contains(w.From)).SelectMany(w => w.To)).ToArray();
            config.Building = WithWear(config.Building);
            config.Furniture = WithWear(config.Furniture);
            config.Robots = WithWear(config.Robots);
            config.Players = WithWear(config.Players);
            EditorUtility.SetDirty(config);

            var root = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (root != null && root.Styles != config) { root.Styles = config; EditorUtility.SetDirty(root); }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Models designed for one style: every robot, device and sensor module (Tools/Blender/build_bestiary.py →
        /// Art/Models/Styles/PerStyle/&lt;style&gt;/&lt;model&gt;__&lt;style&gt;.fbx), matched to the original model by mesh name.
        /// </summary>
        static StyleMeshes[] BuildStyleModels(List<VisualStyle> styles)
        {
            var list = new List<StyleMeshes>();
            foreach (var style in styles)
            {
                var dir = $"{ModelsDir}/Styles/PerStyle/{style.name}";
                if (!AssetDatabase.IsValidFolder(dir)) continue;
                var from = new List<Mesh>();
                var to = new List<Mesh>();
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { dir }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var file = Path.GetFileNameWithoutExtension(path);
                    int cut = file.LastIndexOf("__", System.StringComparison.Ordinal);
                    if (cut < 0) continue;
                    if (Level.VisualStyles.Creature(file.Substring(0, cut))) continue; // the robots look the same in every style (VisualStyles.Invariant)
                    string original;
                    try { original = ModelPath(file.Substring(0, cut)); }
                    catch (FileNotFoundException) { continue; }
                    var originals = AssetDatabase.LoadAllAssetsAtPath(original).OfType<Mesh>().ToDictionary(m => m.name, m => m);
                    foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
                        if (originals.TryGetValue(mesh.name, out var o)) { from.Add(o); to.Add(mesh); }
                }
                if (from.Count > 0) list.Add(new StyleMeshes { Style = style, From = from.ToArray(), To = to.ToArray() });
            }
            // the players by department (Tools/Blender/players.py → Styles/Players/player__<style>__<department>.fbx)
            var player = AssetDatabase.LoadAllAssetsAtPath(ModelPath("player")).OfType<Mesh>().ToDictionary(m => m.name, m => m);
            string[] departments = { "backend", "qa", "sysadmin", "designer" };
            foreach (var style in styles)
                for (int d = 0; d < departments.Length; d++)
                {
                    var path = $"{ModelsDir}/Styles/Players/player__{style.name}__{departments[d]}.fbx";
                    var meshes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Where(m => player.ContainsKey(m.name)).ToArray();
                    if (meshes.Length == 0) continue;
                    list.Add(new StyleMeshes { Style = style, Variant = d, From = meshes.Select(m => player[m.name]).ToArray(), To = meshes });
                }
            return list.ToArray();
        }

        static Material[] MaterialsIn(string category) =>
            AssetDatabase.FindAssets("t:Material", new[] { $"{MaterialsDir}/{category}" })
                .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(m => m != null).OrderBy(m => m.name).ToArray();

        const string XrayMaterialPath = MaterialsDir + "/FX/MarkedXray.mat";

        /// <summary>
        /// Robots marked by a teammate are seen through walls: a RenderObjects pass draws the Marked layer
        /// (<see cref="Stealth.Enemies.Detector.XrayLayer"/>) again where it is hidden (depth test Greater), as a red glow.
        /// </summary>
        static void AddXrayFeature()
        {
            AddXray(XrayMaterialPath, "MarkedXray", new[] { "MarkedXrayVisible", "MarkedXray", "MarkedXrayOutline", "MarkedXrayFill" }, Stealth.Enemies.Detector.XrayLayer,
                null, null);
            // marked things (loot, crates, panels…): the same three passes on their own layer, in yellow-green
            AddXray(MaterialsDir + "/FX/MarkedThingXray.mat", "MarkedThingXray", new[] { "MarkedThingVisible", "MarkedThing", "MarkedThingOutline", "MarkedThingFill" },
                Stealth.Level.MarkedThings.XrayLayer, new Color(0.75f, 1f, 0.2f, 0.36f), new Color(0.85f, 1f, 0.35f, 1f));
        }

        /// <summary>
        /// The RenderObjects passes of the x-ray shader (in view, mask, outline, fill) on one layer, in this order in the
        /// renderer's list, with their material (colours: null = the shader's).
        /// </summary>
        static void AddXray(string materialPath, string materialName, string[] names, int layer, Color? fill, Color? edge)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PcRendererPath);
            var shader = Shader.Find("Stealth/MarkedXray");
            if (data == null || shader == null) { Debug.LogWarning("[Builder] no PC renderer or x-ray shader: marked things are not seen through walls"); return; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null)
            {
                EnsureFolder(MaterialsDir + "/FX");
                mat = new Material(shader) { name = materialName };
                if (fill.HasValue) mat.SetColor("_Color", fill.Value);
                if (edge.HasValue) mat.SetColor("_Edge", edge.Value);
                AssetDatabase.CreateAsset(mat, materialPath);
            }
            else if (mat.shader != shader) { mat.shader = shader; EditorUtility.SetDirty(mat); }

            // The passes of the same material, in this order: where it is in view, mask (stencil), outline outside it, fill inside it.
            for (int pass = 0; pass < names.Length; pass++)
            {
                var feature = data.rendererFeatures.OfType<RenderObjects>().FirstOrDefault(f => f.name == names[pass]);
                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance<RenderObjects>();
                    feature.name = names[pass];
                    AssetDatabase.AddObjectToAsset(feature, data);
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id);
                    var so = new SerializedObject(data);
                    var list = so.FindProperty("m_RendererFeatures");
                    var map = so.FindProperty("m_RendererFeatureMap");
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                    so.ApplyModifiedProperties();
                }
                var s = feature.settings;
                s.passTag = names[pass];
                s.Event = RenderPassEvent.AfterRenderingTransparents;
                s.filterSettings.RenderQueueType = RenderQueueType.Opaque;
                s.filterSettings.LayerMask = 1 << layer;
                s.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.Material;
                s.overrideMaterial = mat;
                s.overrideMaterialPassIndex = pass;
                s.overrideDepthState = true;
                // the first pass marks where the object is in plain view (its own depth); the others draw behind walls only
                s.depthCompareFunction = pass == 0 ? UnityEngine.Rendering.CompareFunction.LessEqual : UnityEngine.Rendering.CompareFunction.Greater;
                s.enableWrite = false;
                feature.SetActive(true);
                EditorUtility.SetDirty(feature);
            }
            // passes of one event run in the list's order: keep ours together and in order (a pass added later lands at the end)
            var order = new SerializedObject(data);
            var features = order.FindProperty("m_RendererFeatures");
            var ids = order.FindProperty("m_RendererFeatureMap");
            int Index(string n)
            {
                for (int i = 0; i < features.arraySize; i++)
                    if (features.GetArrayElementAtIndex(i).objectReferenceValue is ScriptableRendererFeature f && f.name == n) return i;
                return -1;
            }
            for (int k = 1; k < names.Length; k++)
            {
                int prev = Index(names[k - 1]), at = Index(names[k]);
                if (prev < 0 || at < 0 || at == prev + 1) continue;
                int to = at > prev ? prev + 1 : prev;
                features.MoveArrayElement(at, to);
                ids.MoveArrayElement(at, to);
            }
            order.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
        }

        /// <summary>
        /// World-space text (route panels in the sewer, the stash board) is kept out of the normal passes and drawn by a
        /// RenderObjects pass after the style filter: pixel blocks, hatching, 1-bit or neon never eat the letters.
        /// The scene depth still hides it behind walls.
        /// </summary>
        static void AddWorldTextFeature()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PcRendererPath);
            if (data == null) return;
            int bit = 1 << Stealth.Level.LevelMetrics.WorldTextLayer;
            data.opaqueLayerMask &= ~bit;
            data.transparentLayerMask &= ~bit;
            const string name = "WorldText";
            var feature = data.rendererFeatures.OfType<RenderObjects>().FirstOrDefault(f => f.name == name);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<RenderObjects>();
                feature.name = name;
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id);
                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                so.ApplyModifiedProperties();
            }
            var s = feature.settings;
            s.passTag = name;
            s.Event = RenderPassEvent.AfterRenderingPostProcessing + 1; // after the StyleScreen pass
            s.filterSettings.RenderQueueType = RenderQueueType.Transparent;
            s.filterSettings.LayerMask = bit;
            s.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.None;
            s.overrideDepthState = false;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
        }

        /// <summary>A full-screen pass after post-processing that reads depth and normals (for outlines).</summary>
        static void AddStyleFeature(Material mat) =>
            AddFullScreenFeature("StyleScreen", mat, ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);

        /// <summary>
        /// FXAA after the style filter (same injection point, later in the list): the filter's outlines are drawn after
        /// the camera's SMAA and would stay jagged.
        /// </summary>
        static void AddFxaaFeature()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(FxaaShaderPath);
            if (shader == null) { Debug.LogWarning("[Builder] FXAA shader not found"); return; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(FxaaMaterialPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "ScreenFxaa" };
                AssetDatabase.CreateAsset(mat, FxaaMaterialPath);
            }
            else if (mat.shader != shader) { mat.shader = shader; EditorUtility.SetDirty(mat); }
            AddFullScreenFeature("ScreenFxaa", mat, ScriptableRenderPassInput.None);
        }

        static void AddFullScreenFeature(string name, Material mat, ScriptableRenderPassInput requirements,
            FullScreenPassRendererFeature.InjectionPoint at = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PcRendererPath);
            if (data == null) { Debug.LogWarning($"[Builder] PC renderer not found: no {name} pass"); return; }
            var feature = data.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f.name == name);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = name;
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id);
                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                so.ApplyModifiedProperties();
            }
            feature.injectionPoint = at;
            feature.fetchColorBuffer = true;
            feature.requirements = requirements;
            feature.passMaterial = mat;
            feature.passIndex = 0;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
        }

        const string GasMaterialPath = StylesDir + "/GasFog.mat";
        const string SkyDir = Root + "/Art/Textures/Sky";
        const string DerelictSkyPath = SkyDir + "/derelict_night.png";

        /// <summary>
        /// The gas outside and the sky above it (Stealth.Level.GasView): a full-screen pass before post-processing that
        /// reads depth, and window glass that shows the same outside from within the house.
        /// </summary>
        static void AddGasFeature()
        {
            var shader = Shader.Find("Stealth/GasFog");
            if (shader == null) { Debug.LogWarning("[Builder] gas shader not found"); return; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(GasMaterialPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "GasFog" };
                AssetDatabase.CreateAsset(mat, GasMaterialPath);
            }
            else if (mat.shader != shader) { mat.shader = shader; EditorUtility.SetDirty(mat); }
            AddFullScreenFeature("GasFog", mat, ScriptableRenderPassInput.Depth, FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing);

            // window glass: lit, clear, a faint tint and sharp reflections — the real outside shows through the hole
            var window = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/Environment/Window.mat");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (window != null && lit != null && (window.shader != lit || window.renderQueue != (int)UnityEngine.Rendering.RenderQueue.Transparent))
            {
                window.shader = lit;
                window.SetColor("_BaseColor", new Color(0.55f, 0.62f, 0.6f, 0.14f));
                window.SetFloat("_Smoothness", 0.92f);
                window.SetFloat("_Metallic", 0f);
                window.SetColor("_EmissionColor", Color.black);
                window.DisableKeyword("_EMISSION");
                Translucent(window);
            }
        }

        /// <summary>The night sky of the derelict style (Tools/Blender/derelict_sky.py): a lat-long panorama imported as a cubemap.</summary>
        static Cubemap DerelictSky()
        {
            if (AssetImporter.GetAtPath(DerelictSkyPath) is not TextureImporter imp) return null;
            if (imp.textureShape != TextureImporterShape.TextureCube || imp.generateCubemap != TextureImporterGenerateCubemap.Cylindrical ||
                imp.maxTextureSize != 2048)
            {
                imp.textureType = TextureImporterType.Default;
                imp.textureShape = TextureImporterShape.TextureCube;
                imp.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
                imp.sRGBTexture = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.filterMode = FilterMode.Bilinear;
                imp.maxTextureSize = 2048;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Cubemap>(DerelictSkyPath);
        }

        // ------------------------------------------------------------------ the styles


        /// <summary>
        /// Generated lists a hand-tuned style asset lacks (it was made before they existed): decay decals and posters.
        /// Only fills what is empty, so hand edits stay.
        /// </summary>
        static void FillGenerated(VisualStyle asset, VisualStyle def)
        {
            var a = asset.Geometry;
            var d = def.Geometry;
            bool dirty = false;
            if ((a.FloorDecay == null || a.FloorDecay.Length == 0) && d.FloorDecay.Length > 0) { a.FloorDecay = d.FloorDecay; dirty = true; }
            if ((a.WallDecay == null || a.WallDecay.Length == 0) && d.WallDecay.Length > 0) { a.WallDecay = d.WallDecay; dirty = true; }
            if ((a.Posters == null || a.Posters.Length == 0) && d.Posters.Length > 0) { a.Posters = d.Posters; dirty = true; }
            if (a.DecayDensity <= 0f && d.DecayDensity > 0f) { a.DecayDensity = d.DecayDensity; dirty = true; }
            if (a.PosterChance <= 0f && d.PosterChance > 0f) { a.PosterChance = d.PosterChance; dirty = true; }
            if (a.PlayerRig == null && d.PlayerRig != null) { a.PlayerRig = d.PlayerRig; dirty = true; }
            if ((a.RoomFloors == null || a.RoomFloors.Length == 0) && d.RoomFloors.Length > 0) { a.RoomFloors = d.RoomFloors; dirty = true; }
            if ((a.RoomLooks == null || a.RoomLooks.Length == 0) && d.RoomLooks.Length > 0) { a.RoomLooks = d.RoomLooks; dirty = true; }
            if (asset.World.Sky == null && def.World.Sky != null) { asset.World.Sky = def.World.Sky; dirty = true; }
            if (dirty) EditorUtility.SetDirty(asset);
        }

        static VisualStyle Style(string asset, string title, string refs, string pitch, Action<VisualStyle> setup)
        {
            var s = ScriptableObject.CreateInstance<VisualStyle>();
            s.name = asset;
            s.Title = title;
            s.References = refs;
            s.Pitch = pitch;
            setup(s);
            return s;
        }

        static Color C(float r, float g, float b) => new(r, g, b);

        /// <summary>
        /// The styles. Each is a whole look: models (treatment), architecture and finishes, screen filter, grading, air and
        /// palette — chosen after looking at screenshots of the reference games (see the comparison page).
        /// </summary>
        static IEnumerable<VisualStyle> StyleDefinitions()
        {
            yield return Style("S06_Storybook", "Книжка-чертёж", "Blue Prince, Disco Elysium, The Wolf Among Us", // loc-ok
                "Дом как иллюстрация из графического романа: арки и пилястры у дверей, кессонный потолок, панели, карнизы, паркет. " + // loc-ok
                "Тонкий чернильный контур, серо-голубые стены и тёплые лампы.", // loc-ok
                s =>
                {
                    var g = s.Grade;
                    g.Exposure = 0.55f; g.Contrast = 6f; g.Saturation = 0f; g.BloomIntensity = 0.9f; g.Vignette = 0.25f;
                    g.SplitShadows = C(0.42f, 0.48f, 0.58f); g.SplitHighlights = C(0.62f, 0.56f, 0.46f);
                    var sc = s.Screen;
                    sc.CelBands = 5f; sc.CelSoftness = 0.12f; sc.CelMix = 0.6f;
                    sc.OutlineStrength = 0.85f; sc.OutlineWidth = 1f; sc.OutlineSurfaceTint = 0.2f; sc.OutlineColor = C(0.1f, 0.1f, 0.12f);
                    sc.HatchStrength = 0.25f; sc.HatchSpacing = 4f; sc.HatchStart = 0.3f;
                    sc.PaperGrain = 0.08f; sc.PaperTint = 0.15f; sc.PaperColor = C(1f, 0.97f, 0.9f);
                    s.World.Ambient = C(0.12f, 0.13f, 0.15f);
                    s.World.IndoorFog = C(0.16f, 0.18f, 0.22f); s.World.IndoorFogDensity = 0.03f;
                    s.World.GasFog = C(0.6f, 0.64f, 0.68f);
                    var geo = s.Geometry;
                    geo.Family = ModelFamily.Clean;
                    geo.WainscotHeight = 1f; geo.Crown = true; geo.Doors = DoorTrim.Arch;
                    geo.Floor = K("Real_Herringbone"); geo.FloorTile = 1.2f; geo.Wall = K("Wall_PlasterPale"); geo.WallTile = 2f;
                    geo.RoomFloors = StorybookRoomFloors();
                    geo.RoomLooks = StorybookRoomLooks();
                    geo.Trim = K("Trim_White"); geo.Panel = K("Panel_PaleBlue"); geo.Column = K("Trim_PaleBlue"); geo.Beam = K("Trim_White");
                });

            // Sasha's style (merged from feature_visual_style_11_prototype), now the mood without the decay: the abandoned
            // station is a house of its own (HouseKind.Abandoned, Level.DerelictLook) in every style; here every house keeps
            // its own look in realistic materials, each kind of room in its own finishes
            yield return Style("S16_Derelict", "Тихий комплекс", "Dead Space, SOMA, Alien: Isolation, Signalis", // loc-ok
                "Ночной комплекс, где давно никого нет: настоящие материалы — бетон, краска, плитка, ковролин, у каждой комнаты свои. " + // loc-ok
                "Почти темно — светят только лампы. Бирюзово-болезненная гамма, зерно, плотный сырой воздух, мёртвый город за окнами. " + // loc-ok
                "Модели реалистичные, люди похожи на людей.", // loc-ok
                s =>
                {
                    // The light is soft: a moderate exposure under the neutral curve and a fill in the shadows. It had been
                    // a high exposure (2.1 EV) under ACES with next to no fill (an ambient colour is sRGB: 0.07 is 0.5 % of
                    // white): walls near a lamp burnt out, everything a lamp did not reach was black, and each lit
                    // surface bloomed. The colours are the materials' own — the grade was taking a third of the
                    // saturation and tinting what was left green, the palette half of the rest: every room one grey-green.
                    var g = s.Grade;
                    g.Tonemap = StyleTonemap.Neutral; g.Exposure = 0.9f; g.Contrast = -4f; g.Saturation = -8f;
                    g.ColorFilter = C(0.98f, 1f, 0.985f); g.Temperature = 4f; g.Tint = -2f;
                    g.SplitShadows = C(0.44f, 0.5f, 0.53f); g.SplitHighlights = C(0.55f, 0.52f, 0.47f); g.SplitBalance = 0f; // cool shadows under warm lamps
                    g.BloomIntensity = 0.3f; g.BloomThreshold = 1.2f; g.BloomScatter = 0.6f; // a little glow on what really shines, no halo over the room
                    g.Vignette = 0.2f; g.VignetteColor = C(0f, 0.01f, 0.01f);
                    g.ChromaticAberration = 0.08f; g.FilmGrain = 0.2f; g.LensDistortion = 0.03f;
                    // a small fill everywhere (sRGB: about 1 % of white) — a room whose lamps are off is dark, a flashlight's
                    // job; a lit room's fill is its own lamps' (CeilingGlow: they light its ceiling and the tops of its walls)
                    s.World.Ambient = C(0.10f, 0.103f, 0.108f);
                    s.World.IndoorFog = C(0.012f, 0.02f, 0.02f); s.World.IndoorFogDensity = 0.02f;
                    s.World.LightScale = 1f;
                    // (what was tuned under the old exposure keeps its look: the tunnels' lamps, the gas and the sky outside)
                    s.World.UndergroundLights = 3.2f; s.World.GasFog = C(0.32f, 0.44f, 0.29f); s.World.SkyExposure = 2.3f;
                    // soft ceiling lamps: the diffusers glow evenly and low (not burnt-out white rectangles), hot only in
                    // their middle, with a pool of light on the ceiling round the fixture
                    s.World.LampGlow = 0.12f; s.World.CeilingGlow = 1f;
                    s.World.EyeGlow = 0.09f; s.World.EyeGlowRange = 3.2f; // what is right before the player is never pitch black
                    s.World.Sky = DerelictSky(); // a dead city under toxic clouds, above the gas
                    var p = s.Palette;
                    p.Building.Saturation = 0.9f; p.Building.Brightness = 1.1f;
                    p.Furniture.Saturation = 0.95f; p.Furniture.Brightness = 1.1f; p.Furniture.Smoothness = 0.25f;
                    var geo = s.Geometry;
                    geo.Family = ModelFamily.Realistic;
                    geo.Pipes = true; geo.Ceiling = CeilingKind.Trays; geo.Baseboard = true; geo.Doors = DoorTrim.Casing; geo.StripSpacing = 0f;
                    geo.Stairs = StairChoice.Concrete; // a facility's bare concrete stair
                    geo.Floor = K("Real_ConcreteClean"); geo.FloorTile = 2.5f; geo.Wall = K("Real_BeigeWall"); geo.WallTile = 2.5f;
                    geo.CeilingFinish = K("Real_CeilingTiles"); geo.CeilingTile = 1.2f;
                    // (door casings and skirting in the doors' own dark wood, pipes and trays plain galvanised steel: tread
                    // plate here read as rust on every doorway — that look is the abandoned station's alone, Level.DerelictLook)
                    geo.Trim = K("Real_DarkWood"); geo.Pipe = K("Trim_Metal"); geo.Beam = K("Trim_Metal"); geo.Glow = K("Glow_DimCyan");
                    geo.RoomLooks = RealisticRoomLooks();
                    geo.RoomFloors = RealisticRoomFloors();
                });
        }
    }
}
