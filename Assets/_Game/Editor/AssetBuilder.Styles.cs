using System;
using System.Collections.Generic;
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

            BuildKitMaterials();
            BuildExternalKitMaterials();
            bool rebuild = Environment.GetEnvironmentVariable("REBUILD_STYLES") == "1";
            var styles = new List<VisualStyle>();
            foreach (var def in StyleDefinitions())
            {
                var path = $"{StylesDir}/{def.name}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<VisualStyle>(path);
                if (asset != null && rebuild) { EditorUtility.CopySerialized(def, asset); asset.name = def.name; EditorUtility.SetDirty(asset); }
                if (asset == null) { AssetDatabase.CreateAsset(def, path); asset = def; }
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
            if (config.Default == null) config.Default = styles.FirstOrDefault(s => s.name == "S06_Storybook");
            config.Building = MaterialsIn("Environment");
            config.Furniture = MaterialsIn("Props");
            config.Robots = MaterialsIn("Robots");
            config.Players = MaterialsIn("Player");
            config.Loot = MaterialsIn("Items");
            config.Kits = BuildStyleKits();
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

        /// <summary>The guard robot designed for each style (Tools/Blender/guards.py → Art/Models/Styles/Guards/robot_guard__&lt;style&gt;.fbx).</summary>
        static StyleMeshes[] BuildStyleModels(List<VisualStyle> styles)
        {
            var list = new List<StyleMeshes>();
            var originals = AssetDatabase.LoadAllAssetsAtPath(ModelPath("robot_guard")).OfType<Mesh>().ToDictionary(m => m.name, m => m);
            foreach (var style in styles)
            {
                var path = $"{ModelsDir}/Styles/Guards/robot_guard__{style.name}.fbx";
                var meshes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Where(m => originals.ContainsKey(m.name)).ToArray();
                if (meshes.Length == 0) continue;
                list.Add(new StyleMeshes { Style = style, From = meshes.Select(m => originals[m.name]).ToArray(), To = meshes });
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

        /// <summary>A full-screen pass after post-processing that reads depth and normals (for outlines).</summary>
        static void AddStyleFeature(Material mat)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PcRendererPath);
            if (data == null) { Debug.LogWarning("[Builder] PC renderer not found: no style filter"); return; }
            var feature = data.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f.name == "StyleScreen");
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = "StyleScreen";
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
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            feature.passMaterial = mat;
            feature.passIndex = 0;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
        }

        // ------------------------------------------------------------------ the styles

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
            yield return Style("S00_Base", "Как сейчас", "—",
                "Текущий вид игры: чистый URP, ночь, свет только от ламп, голые стены.", s => { });

            yield return Style("S01_NightShift", "Ночная смена", "Lethal Company, Content Warning",
                "Промзона на ночной смене: рифлёный пол, гофрированные стены, трубы и двутавры под потолком, кривоватые модели. " +
                "Низкое разрешение, ступени света, дымка и край шлема по рамке кадра.",
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

            yield return Style("S02_Scrapyard", "Барахольщики", "R.E.P.O., PEAK",
                "Старый особняк, куда приехали за хламом: шахматный пол, тёмные обои с узором, деревянные панели, карнизы. " +
                "Мебель и роботы — пухлые пластиковые игрушки. Тепло, сочно, светится.",
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

            yield return Style("S03_Heist", "Ограбление", "PAYDAY 2, Hitman",
                "Банк ночью: мраморный пол, светлая штукатурка, пилястры у дверей, кессонный потолок, карнизы и плинтусы. " +
                "Холодные бирюзовые тени, тёплые лампы, глянец. Серьёзно и дорого.",
                s =>
                {
                    var g = s.Grade;
                    g.Tonemap = StyleTonemap.Aces; g.Exposure = 1.1f; g.Contrast = 18f; g.Saturation = 5f; g.Temperature = -12f;
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

            yield return Style("S04_Hunted", "Туман и твид", "Sir, You Are Being Hunted, Pathologic",
                "Старый английский дом: кирпич, облезлая штукатурка, доски на полу и тёмные балки, всё чуть кривое, как сделанное руками. " +
                "Жёлто-оливковая дымка, картинка как масло.",
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

            yield return Style("S05_Toon", "Мультяшный рейд", "Risk of Rain 2, Deep Rock Galactic, PEAK",
                "Гранёные модели с плоскими гранями, сочные тёплые стены, крупная плитка, мягкие ступени света и цветная дымка в глубине. " +
                "Без чёрных контуров: силуэты держит цвет.",
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

            yield return Style("S06_Storybook", "Книжка-чертёж", "Blue Prince, Disco Elysium, The Wolf Among Us",
                "Дом как иллюстрация из графического романа: арки и пилястры у дверей, кессонный потолок, панели, карнизы, паркет. " +
                "Тонкий чернильный контур, серо-голубые стены и тёплые лампы.",
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
                    geo.Trim = K("Trim_White"); geo.Panel = K("Panel_PaleBlue"); geo.Column = K("Trim_PaleBlue"); geo.Beam = K("Trim_White");
                });

            yield return Style("S07_Logbook", "Бортовой журнал", "Return of the Obra Dinn, ранний Macintosh",
                "Один бит, две краски: оливковые чернила и мятная бумага. Дом обшит досками, как корабль, под потолком балки. " +
                "Контур светлый на тёмном и тёмный на светлом.",
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

            yield return Style("S08_Footage", "Найденная плёнка", "Content Warning, Paranormal VHS, [REC]",
                "Вылазку снимает камера: серый мир с карандашной штриховкой на стенах, бетон, трубы и кабельные лотки. " +
                "Цвет остаётся только у света — ламп, экранов, лазеров. Видоискатель, REC, строки развёртки.",
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

            yield return Style("S09_WhiteRoom", "Белая комната", "SUPERHOT, Mirror's Edge",
                "Белые панели, светящиеся щели в стенах, бетонный пол. Роботы — гранёные красные кристаллы, лут — единственные краски. " +
                "Только силуэты и угрозы.",
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

            yield return Style("S10_Grid", "Сеть", "TRON, Rez, RUINER",
                "Мир глазами системы охраны: всё тёмное, рёбра светятся бирюзой вблизи и пурпуром вдали, по стенам бегут дорожки плат, " +
                "пол расчерчен светящейся сеткой, под потолком кабельные лотки. Лазеры и экраны видны поверх неона.",
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
        }
    }
}
