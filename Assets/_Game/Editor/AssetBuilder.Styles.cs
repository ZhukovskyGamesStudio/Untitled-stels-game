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
            s_HumanRig = BuildHumanRig();
            bool rebuild = Environment.GetEnvironmentVariable("REBUILD_STYLES") == "1";
            var styles = new List<VisualStyle>();
            foreach (var def in StyleDefinitions())
            {
                if (!EnabledStyles.Contains(def.name)) continue; // the other styles stay on disk, out of the game
                var path = $"{StylesDir}/{def.name}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<VisualStyle>(path);
                if (asset != null && rebuild) { EditorUtility.CopySerialized(def, asset); asset.name = def.name; EditorUtility.SetDirty(asset); }
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
            // the game's look: the abandoned facility (the only style of this branch)
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
            config.Props = BuildPropReplacements();
            config.StyleModels = BuildStyleModels(styles);
            // the palette of a style recolours the family versions of a material too
            Material[] WithVariants(Material[] a) => a.Concat(config.Materials.SelectMany(set =>
                set.From.Select((m, i) => (m, i)).Where(x => a.Contains(x.m)).Select(x => set.To[x.i]))).ToArray();
            config.Building = WithVariants(config.Building);
            config.Furniture = WithVariants(config.Furniture);
            config.Robots = WithVariants(config.Robots);
            config.Players = WithVariants(config.Players);
            EditorUtility.SetDirty(config);

            var root = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (root != null && root.Styles != config) { root.Styles = config; EditorUtility.SetDirty(root); }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The styles in the game (F8 cycles through them). This branch plays only «Заброшенный комплекс»: the other style
        /// assets stay on disk but are not built into the game, and the model generators make nothing for them
        /// (Tools/Blender: style_families, style_kit, build_bestiary, players default to S16 / the derelict family).
        /// </summary>
        static readonly string[] EnabledStyles = { "S16_Derelict" };

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
            yield return Style("S00_Base", "Как сейчас", "—", // loc-ok: authoring data of the style asset (the player sees style.<name>)
                "Текущий вид игры: чистый URP, ночь, свет только от ламп, голые стены.", s => { }); // loc-ok

            yield return Style("S01_NightShift", "Ночная смена", "Lethal Company, Content Warning", // loc-ok
                "Промзона на ночной смене: рифлёный пол, гофрированные стены, трубы и двутавры под потолком, кривоватые модели. " + // loc-ok
                "Низкое разрешение, ступени света, дымка и край шлема по рамке кадра.", // loc-ok
                s =>
                {
                    var g = s.Grade;
                    g.Tonemap = StyleTonemap.Aces; g.Exposure = 0.8f; g.Contrast = 22f; g.Saturation = -22f;
                    g.ColorFilter = C(1f, 0.94f, 0.84f); g.SplitShadows = C(0.42f, 0.5f, 0.52f); g.SplitHighlights = C(0.6f, 0.53f, 0.42f);
                    g.BloomIntensity = 1.4f; g.BloomThreshold = 0.8f; g.Vignette = 0.3f; g.FilmGrain = 0.45f;
                    var sc = s.Screen;
                    sc.PixelSize = 2.5f; sc.CelBands = 6f; sc.CelSoftness = 0.04f; sc.CelMix = 0.5f;
                    sc.OutlineStrength = 0.55f; sc.OutlineSurfaceTint = 0.6f; sc.Visor = 1f;
                    s.World.Ambient = C(0.05f, 0.05f, 0.055f);
                    s.World.IndoorFog = C(0.05f, 0.045f, 0.035f); s.World.IndoorFogDensity = 0.06f;
                    s.World.GasFog = C(0.36f, 0.33f, 0.22f);
                    s.Palette.Building.Saturation = 0.7f;
                    var geo = s.Geometry;
                    geo.Family = ModelFamily.Lofi;
                    geo.Pipes = true; geo.Ceiling = CeilingKind.IBeams; geo.Baseboard = true;
                    geo.Floor = K("Px_Plate"); geo.FloorTile = 1.5f; geo.Wall = K("Px_Corrugated"); geo.WallTile = 2f;
                    geo.CeilingFinish = K("Px_Concrete"); geo.CeilingTile = 2f;
                    geo.Trim = K("Px_Rust"); geo.Beam = K("Px_Rust"); geo.Pipe = K("Px_Pipe");
                });

            yield return Style("S02_Scrapyard", "Барахольщики", "R.E.P.O., PEAK", // loc-ok
                "Старый особняк, куда приехали за хламом: шахматный пол, тёмные обои с узором, деревянные панели, карнизы. " + // loc-ok
                "Мебель и роботы — пухлые пластиковые игрушки. Тепло, сочно, светится.", // loc-ok
                s =>
                {
                    var g = s.Grade;
                    g.Exposure = 0.45f; g.Contrast = 15f; g.Saturation = 25f; g.ColorFilter = C(1f, 0.93f, 0.8f);
                    g.BloomIntensity = 2f; g.BloomThreshold = 0.7f; g.BloomScatter = 0.8f; g.BloomTint = C(1f, 0.82f, 0.62f);
                    g.Vignette = 0.35f; g.ChromaticAberration = 0.35f; g.LensDistortion = 0.22f;
                    s.Screen.PixelSize = 1.5f;
                    s.World.Ambient = C(0.07f, 0.05f, 0.032f);
                    s.World.IndoorFog = C(0.05f, 0.035f, 0.02f); s.World.IndoorFogDensity = 0.04f;
                    s.World.GasFog = C(0.42f, 0.36f, 0.22f);
                    s.Palette.Furniture.Saturation = 1.3f;
                    s.Palette.Robots.Saturation = 1.45f; s.Palette.Robots.Smoothness = 0.75f;
                    var geo = s.Geometry;
                    geo.Family = ModelFamily.Toy;
                    geo.WainscotHeight = 1.05f; geo.Crown = true; geo.Doors = DoorTrim.Casing;
                    geo.Floor = K("Real_CheckerTiles"); geo.FloorTile = 1.6f; geo.Wall = K("Real_Wallpaper"); geo.WallTile = 1.5f;
                    geo.CeilingFinish = K("Real_Plaster2"); geo.CeilingTile = 2f;
                    geo.Trim = K("Trim_WoodDark"); geo.Panel = K("Real_PaneledWood");
                });

            yield return Style("S03_Heist", "Ограбление", "PAYDAY 2, Hitman", // loc-ok
                "Банк ночью: мраморный пол, светлая штукатурка, пилястры у дверей, кессонный потолок, карнизы и плинтусы. " + // loc-ok
                "Холодные бирюзовые тени, тёплые лампы, глянец. Серьёзно и дорого.", // loc-ok
                s =>
                {
                    var g = s.Grade;
                    g.Tonemap = StyleTonemap.Aces; g.Exposure = 1.5f; g.Contrast = 18f; g.Saturation = 5f; g.Temperature = -12f;
                    g.SplitShadows = C(0.3f, 0.45f, 0.55f); g.SplitHighlights = C(0.62f, 0.52f, 0.4f);
                    g.BloomIntensity = 1.6f; g.BloomThreshold = 0.9f; g.BloomScatter = 0.75f;
                    g.Vignette = 0.3f; g.ChromaticAberration = 0.1f; g.FilmGrain = 0.15f;
                    s.World.Ambient = C(0.24f, 0.26f, 0.3f);
                    s.World.IndoorFog = C(0.02f, 0.03f, 0.045f); s.World.IndoorFogDensity = 0.035f;
                    s.Palette.Furniture.Smoothness = 0.4f;
                    var geo = s.Geometry;
                    geo.Family = ModelFamily.Realistic;
                    geo.Baseboard = true; geo.Crown = true; geo.Doors = DoorTrim.Pilasters;
                    geo.Floor = K("Real_Marble"); geo.FloorTile = 2f; geo.Wall = K("Real_Plaster"); geo.WallTile = 2.5f;
                    geo.CeilingFinish = K("Real_CeilingTiles"); geo.CeilingTile = 1.2f;
                    geo.Trim = K("Real_DarkWood"); geo.Column = K("Real_Marble");
                });

            yield return Style("S04_Hunted", "Туман и твид", "Sir, You Are Being Hunted, Pathologic", // loc-ok
                "Старый английский дом: кирпич, облезлая штукатурка, доски на полу и тёмные балки, всё чуть кривое, как сделанное руками. " + // loc-ok
                "Жёлто-оливковая дымка, картинка как масло.", // loc-ok
                s =>
                {
                    var g = s.Grade;
                    g.Tonemap = StyleTonemap.Aces; g.Exposure = 1.05f; g.Contrast = 5f; g.Saturation = -25f;
                    g.ColorFilter = C(1f, 0.95f, 0.72f); g.SplitShadows = C(0.42f, 0.48f, 0.38f); g.SplitHighlights = C(0.65f, 0.6f, 0.42f);
                    g.BloomIntensity = 1.1f; g.BloomThreshold = 0.75f; g.BloomScatter = 0.9f; g.Vignette = 0.32f;
                    s.Screen.PaintRadius = 4f; s.Screen.PaperGrain = 0.06f;
                    s.World.Ambient = C(0.22f, 0.21f, 0.13f);
                    s.World.IndoorFog = C(0.36f, 0.35f, 0.2f); s.World.IndoorFogDensity = 0.09f;
                    s.World.GasFog = C(0.62f, 0.58f, 0.32f);
                    var geo = s.Geometry;
                    geo.Family = ModelFamily.Realistic;
                    geo.Baseboard = true; geo.RailHeight = 0.95f; geo.Ceiling = CeilingKind.Beams; geo.Doors = DoorTrim.Casing;
                    geo.Floor = K("Real_OldFloor"); geo.FloorTile = 1.5f; geo.Wall = K("Real_Peeling"); geo.WallTile = 2.5f;
                    geo.CeilingFinish = K("Real_Plaster2"); geo.CeilingTile = 2f;
                    geo.Trim = K("Real_DarkPlanks"); geo.Beam = K("Real_DarkPlanks");
                });

            yield return Style("S05_Toon", "Мультяшный рейд", "Risk of Rain 2, Deep Rock Galactic, PEAK", // loc-ok
                "Гранёные модели с плоскими гранями, сочные тёплые стены, крупная плитка, мягкие ступени света и цветная дымка в глубине. " + // loc-ok
                "Без чёрных контуров: силуэты держит цвет.", // loc-ok
                s =>
                {
                    var g = s.Grade;
                    g.Exposure = 0.7f; g.Contrast = 12f; g.Saturation = 40f;
                    g.SplitShadows = C(0.48f, 0.4f, 0.62f); g.SplitHighlights = C(0.62f, 0.55f, 0.42f);
                    g.BloomIntensity = 1.3f;
                    var sc = s.Screen;
                    sc.CelBands = 4f; sc.CelSoftness = 0.12f; sc.CelMix = 0.8f;
                    sc.OutlineStrength = 0.35f; sc.OutlineWidth = 1f; sc.OutlineSurfaceTint = 1f;
                    s.World.Ambient = C(0.16f, 0.13f, 0.24f);
                    s.World.IndoorFog = C(0.5f, 0.32f, 0.42f); s.World.IndoorFogDensity = 0.065f;
                    s.World.GasFog = C(0.8f, 0.55f, 0.45f);
                    foreach (var t in new[] { s.Palette.Building, s.Palette.Furniture, s.Palette.Robots, s.Palette.Players }) t.Saturation = 1.5f;
                    var geo = s.Geometry;
                    geo.Family = ModelFamily.Lowpoly;
                    geo.Baseboard = true;
                    geo.Floor = K("Floor_ToonTiles"); geo.FloorTile = 2.5f; geo.Wall = K("Wall_PlasterToon"); geo.WallTile = 3f;
                    geo.Trim = K("Trim_Toon");
                });

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
                    geo.WainscotHeight = 1f; geo.Crown = true; geo.Doors = DoorTrim.Arch; geo.Ceiling = CeilingKind.Coffers;
                    geo.Floor = K("Real_Herringbone"); geo.FloorTile = 1.2f; geo.Wall = K("Wall_PlasterPale"); geo.WallTile = 2f;
                    geo.RoomFloors = StorybookRoomFloors();
                    geo.Trim = K("Trim_White"); geo.Panel = K("Panel_PaleBlue"); geo.Column = K("Trim_PaleBlue"); geo.Beam = K("Trim_White");
                });

            yield return Style("S07_Logbook", "Бортовой журнал", "Return of the Obra Dinn, ранний Macintosh", // loc-ok
                "Один бит, две краски: оливковые чернила и мятная бумага. Дом обшит досками, как корабль, под потолком балки. " + // loc-ok
                "Контур светлый на тёмном и тёмный на светлом.", // loc-ok
                s =>
                {
                    var g = s.Grade;
                    g.Exposure = 1f; g.Contrast = 10f; g.BloomIntensity = 0.6f; g.Vignette = 0f;
                    var sc = s.Screen;
                    sc.PixelSize = 2f; sc.OneBit = true; sc.DotSize = 2f; sc.DitherBias = 0.04f;
                    sc.Ink = C(0.2f, 0.2f, 0.13f); sc.Light = C(0.85f, 0.98f, 0.92f);
                    sc.OutlineStrength = 1f; sc.OutlineInvert = true;
                    s.World.Ambient = C(0.05f, 0.05f, 0.05f);
                    s.World.IndoorFog = C(0.03f, 0.03f, 0.03f);
                    s.World.GasFog = C(0.5f, 0.5f, 0.5f);
                    var geo = s.Geometry;
                    geo.Family = ModelFamily.Clean;
                    geo.Baseboard = true; geo.Ceiling = CeilingKind.Beams; geo.Doors = DoorTrim.Casing;
                    geo.Floor = K("Real_PlankFloor"); geo.FloorTile = 1.5f; geo.Wall = K("Real_RawPlanks"); geo.WallTile = 1.5f;
                    geo.CeilingFinish = K("Real_RawPlanks"); geo.CeilingTile = 1.5f;
                    geo.Trim = K("Trim_WoodMid"); geo.Beam = K("Trim_WoodMid");
                });

            yield return Style("S08_Footage", "Найденная плёнка", "Content Warning, Paranormal VHS, [REC]", // loc-ok
                "Вылазку снимает камера: серый мир с карандашной штриховкой на стенах, бетон, трубы и кабельные лотки. " + // loc-ok
                "Цвет остаётся только у света — ламп, экранов, лазеров. Видоискатель, REC, строки развёртки.", // loc-ok
                s =>
                {
                    var g = s.Grade;
                    g.Exposure = 0.85f; g.Contrast = 18f; g.Saturation = 10f;
                    g.BloomIntensity = 2f; g.BloomThreshold = 0.6f; g.BloomScatter = 0.9f;
                    g.LensDistortion = 0.2f; g.Vignette = 0.4f; g.FilmGrain = 0.3f; g.ChromaticAberration = 0.4f;
                    var sc = s.Screen;
                    sc.PixelSize = 1.4f; sc.Scanlines = 0.15f; sc.ChromaShift = 1.5f; sc.Noise = 0.08f; sc.Wobble = 0.8f;
                    sc.RecDot = true; sc.Viewfinder = true; sc.ColorOnlyLights = 1f;
                    s.World.Ambient = C(0.06f, 0.06f, 0.06f);
                    var geo = s.Geometry;
                    geo.Family = ModelFamily.Toy;
                    geo.Pipes = true; geo.Ceiling = CeilingKind.Trays; geo.Baseboard = true;
                    geo.Floor = K("Real_HangarConcrete"); geo.FloorTile = 2f; geo.Wall = K("Real_ConcreteWall"); geo.WallTile = 2f;
                    geo.CeilingFinish = K("Real_ConcreteWall"); geo.CeilingTile = 2f;
                    geo.Trim = K("Trim_Metal"); geo.Pipe = K("Trim_Metal");
                });

            yield return Style("S09_WhiteRoom", "Белая комната", "SUPERHOT, Mirror's Edge", // loc-ok
                "Белые панели, светящиеся щели в стенах, бетонный пол. Роботы — гранёные красные кристаллы, лут — единственные краски. " + // loc-ok
                "Только силуэты и угрозы.", // loc-ok
                s =>
                {
                    var g = s.Grade;
                    g.Exposure = 0.3f; g.Contrast = 20f; g.Saturation = 10f; g.BloomIntensity = 0.8f; g.Vignette = 0.1f;
                    g.ColorFilter = C(0.94f, 0.98f, 1f);
                    s.World.Ambient = C(0.75f, 0.78f, 0.82f);
                    s.World.IndoorFog = C(0.86f, 0.9f, 0.93f); s.World.IndoorFogDensity = 0.02f;
                    s.World.GasFog = C(0.9f, 0.92f, 0.95f);
                    var p = s.Palette;
                    p.Building.Tint = C(0.92f, 0.94f, 0.95f); p.Building.Amount = 1f;
                    p.Furniture.Tint = C(0.86f, 0.88f, 0.9f); p.Furniture.Amount = 0.95f;
                    p.Robots.Tint = C(0.95f, 0.12f, 0.1f); p.Robots.Amount = 1f; p.Robots.Smoothness = 0.25f;
                    p.Players.Tint = C(0.12f, 0.12f, 0.12f); p.Players.Amount = 1f;
                    p.Loot.Saturation = 1.2f;
                    var geo = s.Geometry;
                    geo.Family = ModelFamily.Lowpoly;
                    geo.StripSpacing = 2.4f;
                    geo.Floor = K("Floor_ConcreteWhite"); geo.FloorTile = 2f; geo.Wall = K("Wall_Panels"); geo.WallTile = 2f;
                    geo.Trim = K("Trim_White"); geo.Glow = K("Glow_White");
                });

            yield return Style("S10_Grid", "Сеть", "TRON, Rez, RUINER", // loc-ok
                "Мир глазами системы охраны: всё тёмное, рёбра светятся бирюзой вблизи и пурпуром вдали, по стенам бегут дорожки плат, " + // loc-ok
                "пол расчерчен светящейся сеткой, под потолком кабельные лотки. Лазеры и экраны видны поверх неона.", // loc-ok
                s =>
                {
                    var g = s.Grade;
                    g.Exposure = 0.3f; g.Contrast = 10f; g.Saturation = 20f; g.BloomIntensity = 2.5f; g.BloomThreshold = 0.8f; g.Vignette = 0.4f;
                    var sc = s.Screen;
                    sc.NeonStrength = 1.6f; sc.NeonKeep = 0.15f; sc.NeonFar = 22f;
                    sc.NeonNear = C(0.1f, 1f, 0.95f); sc.NeonFarColor = C(0.9f, 0.15f, 1f);
                    sc.OutlineDepth = 10f; sc.OutlineNormal = 3f;
                    s.World.Ambient = C(0.02f, 0.02f, 0.05f);
                    s.World.IndoorFog = C(0.02f, 0f, 0.05f); s.World.IndoorFogDensity = 0.04f;
                    s.World.GasFog = C(0.06f, 0f, 0.12f);
                    s.Palette.Building.Brightness = 0.5f;
                    var geo = s.Geometry;
                    geo.Family = ModelFamily.Clean;
                    geo.Pipes = true; geo.Ceiling = CeilingKind.Trays;
                    geo.Floor = K("Floor_Grid"); geo.FloorTile = 1f; geo.Wall = K("Wall_Circuit"); geo.WallTile = 1f;
                    geo.Trim = K("Trim_Black"); geo.Pipe = K("Trim_Black"); geo.Glow = K("Glow_Cyan");
                });

            // ---- by players' requests: three in the spirit of games they named, two of our own (nothing copied: our models,
            // our textures, only the mood — light, colour and the screen filter)

            yield return Style("S11_Noir", "Нуар-детектив", "по мотивам Shadows of Doubt, Blade Runner", // loc-ok
                "Ночной город за окном и контора детектива: ковролин, обои с узором, тёмные деревянные панели. Роботы, мебель " + // loc-ok
                "и люди собраны из вокселей (4 см), картинка чёткая, мало цветов. Синие тени, янтарные лампы, дождливая дымка.", // loc-ok
                s =>
                {
                    var g = s.Grade;
                    g.Tonemap = StyleTonemap.Aces; g.Exposure = 2f; g.Contrast = 18f; g.Saturation = -15f;
                    g.SplitShadows = C(0.25f, 0.4f, 0.6f); g.SplitHighlights = C(0.75f, 0.55f, 0.3f); g.SplitBalance = -15f;
                    g.BloomIntensity = 1.8f; g.BloomThreshold = 0.65f; g.BloomScatter = 0.8f; g.BloomTint = C(1f, 0.8f, 0.55f);
                    g.Vignette = 0.4f; g.FilmGrain = 0.25f;
                    var sc = s.Screen;
                    sc.PixelSize = 0f; sc.ColorLevels = 20f; sc.OrderedDither = true; // crisp voxels, no pixel blocks
                    sc.OutlineStrength = 0.35f; sc.OutlineSurfaceTint = 0.8f;
                    s.World.Ambient = C(0.14f, 0.15f, 0.24f);
                    s.World.IndoorFog = C(0.06f, 0.07f, 0.12f); s.World.IndoorFogDensity = 0.035f;
                    s.World.GasFog = C(0.18f, 0.22f, 0.32f);
                    s.Palette.Building.Saturation = 0.6f; s.Palette.Building.Brightness = 0.85f;
                    var geo = s.Geometry;
                    geo.Family = ModelFamily.Lofi;
                    geo.WainscotHeight = 1.1f; geo.Crown = true; geo.Doors = DoorTrim.Casing;
                    geo.Floor = K("Real_Carpet"); geo.FloorTile = 2f; geo.Wall = K("Wall_Damask"); geo.WallTile = 1.5f;
                    geo.CeilingFinish = K("Real_Plaster2"); geo.CeilingTile = 2f;
                    geo.Trim = K("Trim_WoodDark"); geo.Panel = K("Panel_WoodDark");
                });

            yield return Style("S12_TestChamber", "Испытательный комплекс", "по мотивам Portal", // loc-ok
                "Стерильная лаборатория: светлые панели стен, тёмный бетонный пол, голубые световые щели, металлические плинтусы. " + // loc-ok
                "Ровный холодный свет, чистые глянцевые роботы. Тихо, как перед тестом.", // loc-ok
                s =>
                {
                    var g = s.Grade;
                    g.Exposure = 0.5f; g.Contrast = 14f; g.Saturation = -10f; g.ColorFilter = C(0.95f, 0.98f, 1f); g.Temperature = -8f;
                    g.BloomIntensity = 1.2f; g.BloomThreshold = 0.9f; g.Vignette = 0.15f;
                    s.World.Ambient = C(0.33f, 0.35f, 0.38f);
                    s.World.IndoorFog = C(0.5f, 0.55f, 0.6f); s.World.IndoorFogDensity = 0.015f;
                    s.World.GasFog = C(0.7f, 0.75f, 0.8f);
                    var p = s.Palette;
                    p.Building.Tint = C(0.85f, 0.87f, 0.88f); p.Building.Amount = 0.7f;
                    p.Furniture.Tint = C(0.75f, 0.77f, 0.8f); p.Furniture.Amount = 0.5f; p.Furniture.Saturation = 0.6f;
                    p.Robots.Smoothness = 0.8f;
                    var geo = s.Geometry;
                    geo.Family = ModelFamily.Clean;
                    geo.Baseboard = true; geo.StripSpacing = 3.2f; geo.Doors = DoorTrim.Casing;
                    geo.Floor = K("Real_ConcreteClean"); geo.FloorTile = 2.5f; geo.Wall = K("Wall_Panels"); geo.WallTile = 1.2f;
                    geo.CeilingFinish = K("Real_CeilingTiles"); geo.CeilingTile = 1.2f;
                    geo.Trim = K("Trim_Metal"); geo.Glow = K("Glow_Cyan");
                });

            yield return Style("S13_Comic", "Комикс-пустошь", "по мотивам Borderlands", // loc-ok
                "Нарисованный от руки мир на краю пустыни: жирный чёрный контур, три ступени света, штриховка в тенях. " + // loc-ok
                "Ребристое железо, рифлёный пол, ржавые балки и трубы. Сочно, тепло и пыльно.", // loc-ok
                s =>
                {
                    var g = s.Grade;
                    g.Exposure = 1.7f; g.Contrast = 15f; g.Saturation = 35f; g.ColorFilter = C(1f, 0.95f, 0.85f); g.Temperature = 15f;
                    g.BloomIntensity = 1f; g.Vignette = 0.2f;
                    var sc = s.Screen;
                    sc.CelBands = 3f; sc.CelSoftness = 0.05f; sc.CelMix = 0.85f;
                    sc.OutlineStrength = 1f; sc.OutlineWidth = 2f; sc.OutlineDepth = 10f; sc.OutlineNormal = 3f;
                    sc.OutlineColor = C(0.05f, 0.04f, 0.03f); sc.OutlineSurfaceTint = 0f;
                    sc.HatchStrength = 0.55f; sc.HatchSpacing = 5f; sc.HatchStart = 0.42f;
                    s.World.Ambient = C(0.32f, 0.24f, 0.15f);
                    s.World.IndoorFog = C(0.4f, 0.28f, 0.15f); s.World.IndoorFogDensity = 0.02f;
                    s.World.GasFog = C(0.85f, 0.6f, 0.35f);
                    foreach (var t in new[] { s.Palette.Building, s.Palette.Furniture, s.Palette.Robots, s.Palette.Players }) t.Saturation = 1.3f;
                    var geo = s.Geometry;
                    geo.Family = ModelFamily.Clean;
                    geo.Pipes = true; geo.Ceiling = CeilingKind.IBeams; geo.Baseboard = true;
                    geo.Floor = K("Floor_Diamond"); geo.FloorTile = 1.5f; geo.Wall = K("Wall_Corrugated"); geo.WallTile = 2f;
                    geo.Trim = K("Trim_Rust"); geo.Beam = K("Trim_Rust"); geo.Pipe = K("Pipe_Green");
                });

            yield return Style("S14_Thermal", "Тепловизор", "своё: тепловизионная камера", // loc-ok
                "Дом глазами тепловизора: холодные стены и мебель тонут в индиго, лампы и тёплые тела светятся пурпуром и жёлтым. " + // loc-ok
                "Роботы и люди — самые горячие пятна в кадре. Чуть шума матрицы.", // loc-ok
                s =>
                {
                    var g = s.Grade;
                    g.Exposure = 0.6f; g.Contrast = 25f; g.Saturation = 0f; g.BloomIntensity = 1.2f; g.Vignette = 0.3f;
                    var sc = s.Screen;
                    sc.PixelSize = 1.5f; sc.GradientMap = 1f;
                    sc.MapDark = C(0.02f, 0f, 0.12f); sc.MapMid = C(0.85f, 0.1f, 0.45f); sc.MapLight = C(1f, 0.95f, 0.55f);
                    sc.Noise = 0.05f; sc.Scanlines = 0.08f;
                    s.World.Ambient = C(0.08f, 0.08f, 0.08f);
                    s.World.IndoorFog = C(0f, 0f, 0f); s.World.IndoorFogDensity = 0.03f;
                    s.World.GasFog = C(0.25f, 0.25f, 0.25f);
                    var p = s.Palette;
                    p.Building.Saturation = 0f; p.Building.Brightness = 0.35f;
                    p.Furniture.Saturation = 0f; p.Furniture.Brightness = 0.5f;
                    p.Robots.Tint = Color.white; p.Robots.Amount = 0.6f; p.Robots.Brightness = 2.2f;
                    p.Players.Tint = Color.white; p.Players.Amount = 0.6f; p.Players.Brightness = 2f;
                    p.Loot.Brightness = 1.6f;
                    s.Geometry.Family = ModelFamily.Lowpoly;
                });

            yield return Style("S15_Blueprint", "Синька", "своё: архитектурные чертежи", // loc-ok
                "Вылазка по чертежу: мир в оттенках синьки, все рёбра — белые линии, по экрану — сетка кальки. " + // loc-ok
                "Карнизы, наличники и кессоны читаются как на плане здания.", // loc-ok
                s =>
                {
                    var g = s.Grade;
                    g.Exposure = 0.7f; g.Contrast = 8f; g.Saturation = 0f; g.BloomIntensity = 0.6f; g.Vignette = 0.25f;
                    var sc = s.Screen;
                    sc.GradientMap = 1f;
                    sc.MapDark = C(0.03f, 0.12f, 0.35f); sc.MapMid = C(0.12f, 0.32f, 0.65f); sc.MapLight = C(0.75f, 0.88f, 1f);
                    sc.OutlineStrength = 1f; sc.OutlineWidth = 1.2f; sc.OutlineColor = C(0.92f, 0.96f, 1f); sc.OutlineSurfaceTint = 0f;
                    sc.GridStrength = 0.35f; sc.GridSpacing = 18f; sc.GridColor = C(0.55f, 0.72f, 0.95f);
                    sc.PaperGrain = 0.05f;
                    s.World.Ambient = C(0.15f, 0.15f, 0.17f);
                    s.World.IndoorFog = C(0.03f, 0.1f, 0.25f); s.World.IndoorFogDensity = 0.03f;
                    s.World.GasFog = C(0.2f, 0.35f, 0.6f);
                    var geo = s.Geometry;
                    geo.Family = ModelFamily.Clean;
                    geo.Baseboard = true; geo.Crown = true; geo.Doors = DoorTrim.Casing; geo.Ceiling = CeilingKind.Coffers;
                    geo.Floor = K("Floor_Concrete"); geo.FloorTile = 2f; geo.Wall = K("Wall_PlasterPale"); geo.WallTile = 2f;
                    geo.Trim = K("Trim_White"); geo.Beam = K("Trim_White");
                });

            // Sasha's style (merged from feature_visual_style_11_prototype): its own model family, kit, props, decay and a rigged human
            yield return Style("S16_Derelict", "Заброшенный комплекс", "Dead Space, SOMA, Alien: Isolation, Signalis", // loc-ok
                "Брошенная научная станция: грязный бетон, ржавые трубы и кабельные лотки, стальные плиты. Почти темно — " + // loc-ok
                "светят только умирающие лампы и редкие холодные щели электроники. Бирюзово-болезненная гамма, зерно, плотный сырой воздух. " + // loc-ok
                "Модели реалистичные, люди похожи на людей.", // loc-ok
                s =>
                {
                    var g = s.Grade;
                    g.Tonemap = StyleTonemap.Aces; g.Exposure = 2.1f; g.Contrast = 4f; g.Saturation = -35f;
                    g.ColorFilter = C(0.86f, 0.97f, 0.94f); g.Temperature = -18f; g.Tint = -8f;
                    g.SplitShadows = C(0.3f, 0.45f, 0.45f); g.SplitHighlights = C(0.55f, 0.6f, 0.5f); g.SplitBalance = -20f;
                    g.BloomIntensity = 0.9f; g.BloomThreshold = 0.95f; g.BloomScatter = 0.88f; // a soft halo round the lamps g.BloomTint = C(0.8f, 1f, 0.95f);
                    g.Vignette = 0.2f; g.VignetteColor = C(0f, 0.01f, 0.01f);
                    g.ChromaticAberration = 0.25f; g.FilmGrain = 0.4f; g.LensDistortion = 0.06f;
                    s.World.Ambient = C(0.07f, 0.08f, 0.08f); // the lamps light the rooms, not a flat grey fill
                    s.World.IndoorFog = C(0.012f, 0.02f, 0.02f); s.World.IndoorFogDensity = 0.02f;
                    s.World.GasFog = C(0.22f, 0.3f, 0.2f); s.World.LightScale = 1.4f; s.World.LampGlow = 0.5f; // gentle lamps: soft pools of light, diffusers read as lamps without glare
                    s.World.Sky = DerelictSky(); // a dead city under toxic clouds, above the gas
                    var p = s.Palette;
                    p.Building.Saturation = 0.45f; p.Building.Brightness = 1.3f;
                    p.Furniture.Saturation = 0.5f; p.Furniture.Brightness = 1.3f; p.Furniture.Smoothness = 0.25f;
                    p.Robots.Saturation = 0.6f;
                    p.Players.Saturation = 0.8f;
                    var geo = s.Geometry;
                    geo.Family = ModelFamily.Derelict;
                    geo.Pipes = true; geo.Ceiling = CeilingKind.Trays; geo.Baseboard = true; geo.StripSpacing = 7f;
                    geo.Floor = K("Dr_FloorPlate"); geo.FloorTile = 2f; geo.Wall = K("Dr_SteelPanels"); geo.WallTile = 2.4f;
                    geo.CeilingFinish = K("Dr_CeilingVents"); geo.CeilingTile = 2.4f;
                    geo.Trim = K("Dr_Rust"); geo.Pipe = K("Dr_Rust"); geo.Beam = K("Dr_PaintedMetal"); geo.Glow = K("Glow_DimCyan");
                    geo.PlayerRig = s_HumanRig;
                    geo.FloorDecay = new[] { K("Dc_floor_rust"), K("Dc_floor_oil"), K("Dc_floor_scorch"), K("Dc_floor_rubble") };
                    geo.WallDecay = new[] { K("Dc_wall_rust"), K("Dc_wall_rust"), K("Dc_wall_leak"), K("Dc_wall_mold"), K("Dc_wall_peel"), K("Dc_wall_scorch") }; // rust twice as often
                    geo.Posters = Enumerable.Range(0, 6).Select(i => K($"Dc_poster_{i}")).ToArray();
                    geo.DecayDensity = 0.14f;
                    geo.PosterChance = 0.25f;
                });
        }
    }
}
