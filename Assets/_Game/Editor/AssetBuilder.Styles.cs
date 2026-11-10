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
            config.Building = MaterialsIn("Environment");
            config.Furniture = MaterialsIn("Props");
            config.Robots = MaterialsIn("Robots");
            config.Players = MaterialsIn("Player");
            config.Loot = MaterialsIn("Items");
            EditorUtility.SetDirty(config);

            var root = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (root != null && root.Styles != config) { root.Styles = config; EditorUtility.SetDirty(root); }
            AssetDatabase.SaveAssets();
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

        static IEnumerable<VisualStyle> StyleDefinitions()
        {
            yield return Style("S00_Base", "Как сейчас", "—",
                "Текущий вид игры: чистый URP, ночь, свет только от ламп.", s => { });

            yield return Style("S01_NightShift", "Ночная смена", "Lethal Company, Content Warning",
                "Низкое разрешение, ступенчатый свет и тонкие контуры, тяжёлая дымка, тёплые лампы на фоне грязной темноты. " +
                "Дёшево, читаемо и сразу «друзья против темноты».",
                s =>
                {
                    var g = s.Grade;
                    g.Tonemap = StyleTonemap.Aces; g.Exposure = 0.55f; g.Contrast = 22f; g.Saturation = -22f;
                    g.ColorFilter = C(1f, 0.94f, 0.84f); g.SplitShadows = C(0.42f, 0.5f, 0.52f); g.SplitHighlights = C(0.6f, 0.53f, 0.42f);
                    g.BloomIntensity = 1.4f; g.BloomThreshold = 0.8f; g.Vignette = 0.38f; g.FilmGrain = 0.45f;
                    var sc = s.Screen;
                    sc.PixelSize = 2.5f; sc.CelBands = 6f; sc.CelSoftness = 0.04f; sc.CelMix = 0.5f;
                    sc.OutlineStrength = 0.55f; sc.OutlineSurfaceTint = 0.6f;
                    s.World.Ambient = C(0.025f, 0.025f, 0.03f);
                    s.World.IndoorFog = C(0.05f, 0.045f, 0.035f); s.World.IndoorFogDensity = 0.06f;
                    s.World.GasFog = C(0.36f, 0.33f, 0.22f);
                    s.Palette.Building.Saturation = 0.7f;
                });

            yield return Style("S02_Scrapyard", "Барахольщики", "R.E.P.O., PEAK",
                "Тёплый игрушечный мир: насыщенные цвета, блестящие роботы-игрушки, сильное свечение и лёгкое «рыбье око». " +
                "Смешно, когда страшно.",
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
                    s.Palette.Building.Tint = C(0.85f, 0.74f, 0.58f); s.Palette.Building.Amount = 0.25f;
                    s.Palette.Furniture.Saturation = 1.3f;
                    s.Palette.Robots.Saturation = 1.45f; s.Palette.Robots.Smoothness = 0.75f;
                });

            yield return Style("S03_Heist", "Ограбление", "PAYDAY 2, Hitman",
                "Кино про ограбление: холодный ночной офис, бирюзовые тени и оранжевые лампы, глянцевые полы и чистая картинка. " +
                "Серьёзно и дорого.",
                s =>
                {
                    var g = s.Grade;
                    g.Tonemap = StyleTonemap.Aces; g.Exposure = 0.8f; g.Contrast = 22f; g.Saturation = 5f; g.Temperature = -18f;
                    g.SplitShadows = C(0.3f, 0.45f, 0.55f); g.SplitHighlights = C(0.62f, 0.52f, 0.4f);
                    g.BloomIntensity = 1.6f; g.BloomThreshold = 0.9f; g.BloomScatter = 0.75f;
                    g.Vignette = 0.3f; g.ChromaticAberration = 0.1f; g.FilmGrain = 0.15f;
                    s.World.Ambient = C(0.04f, 0.06f, 0.1f);
                    s.World.IndoorFog = C(0.02f, 0.03f, 0.045f); s.World.IndoorFogDensity = 0.035f;
                    s.Palette.Building.Smoothness = 0.5f; s.Palette.Building.Brightness = 1.1f;
                    s.Palette.Furniture.Smoothness = 0.4f;
                });

            yield return Style("S04_Hunted", "Туман и твид", "Sir, You Are Being Hunted, Pathologic",
                "Живописная серо-зелёная пасмурность: картинка как масло, мягкий бледный туман даже внутри, выцветшие цвета. " +
                "Тревожно и по-британски.",
                s =>
                {
                    var g = s.Grade;
                    g.Tonemap = StyleTonemap.Aces; g.Exposure = 0.6f; g.Contrast = -5f; g.Saturation = -35f;
                    g.ColorFilter = C(0.92f, 0.95f, 0.88f); g.SplitShadows = C(0.45f, 0.5f, 0.45f); g.SplitHighlights = C(0.6f, 0.57f, 0.5f);
                    g.BloomIntensity = 1f; g.BloomThreshold = 0.8f; g.BloomScatter = 0.9f; g.Vignette = 0.3f;
                    s.Screen.PaintRadius = 5f; s.Screen.PaperGrain = 0.08f;
                    s.World.Ambient = C(0.16f, 0.17f, 0.15f);
                    s.World.IndoorFog = C(0.36f, 0.39f, 0.35f); s.World.IndoorFogDensity = 0.11f;
                    s.World.GasFog = C(0.55f, 0.58f, 0.52f);
                    s.Palette.Building.Tint = C(0.7f, 0.68f, 0.6f); s.Palette.Building.Amount = 0.3f; s.Palette.Building.Saturation = 0.7f;
                });

            yield return Style("S05_Toon", "Мультяшный рейд", "Risk of Rain 2, Deep Rock Galactic",
                "Цел-шейдинг в четыре ступени, цветные контуры, сочные цвета и фиолетовая дымка. " +
                "Сразу видно, кто где, — арт, который хорошо читается в хаосе.",
                s =>
                {
                    var g = s.Grade;
                    g.Exposure = 0.7f; g.Contrast = 10f; g.Saturation = 40f;
                    g.SplitShadows = C(0.5f, 0.42f, 0.6f); g.SplitHighlights = C(0.6f, 0.55f, 0.45f);
                    g.BloomIntensity = 1.2f;
                    var sc = s.Screen;
                    sc.CelBands = 4f; sc.CelSoftness = 0.12f; sc.CelMix = 0.85f;
                    sc.OutlineStrength = 0.9f; sc.OutlineWidth = 1.2f; sc.OutlineSurfaceTint = 0.85f;
                    s.World.Ambient = C(0.16f, 0.13f, 0.24f);
                    s.World.IndoorFog = C(0.12f, 0.1f, 0.2f); s.World.IndoorFogDensity = 0.05f;
                    s.World.GasFog = C(0.55f, 0.5f, 0.7f);
                    foreach (var t in new[] { s.Palette.Building, s.Palette.Furniture, s.Palette.Robots, s.Palette.Players }) t.Saturation = 1.5f;
                    s.Palette.Building.Tint = C(0.55f, 0.6f, 0.75f); s.Palette.Building.Amount = 0.25f;
                });

            yield return Style("S06_Storybook", "Книжка-чертёж", "Blue Prince, Disco Elysium, The Wolf Among Us",
                "Иллюстрация из графического романа: толстые чернильные контуры, штриховка в тенях, бумага и тёплые ступени света. " +
                "Каждый кадр как страница.",
                s =>
                {
                    var g = s.Grade;
                    g.Exposure = 0.5f; g.Contrast = 5f; g.Saturation = 10f; g.BloomIntensity = 0.8f; g.Vignette = 0.25f;
                    var sc = s.Screen;
                    sc.CelBands = 5f; sc.CelSoftness = 0.12f; sc.CelMix = 0.8f;
                    sc.OutlineStrength = 1f; sc.OutlineWidth = 1.5f; sc.OutlineSurfaceTint = 0.3f; sc.OutlineColor = C(0.08f, 0.06f, 0.05f);
                    sc.HatchStrength = 0.55f; sc.HatchSpacing = 4f; sc.HatchStart = 0.35f;
                    sc.PaperGrain = 0.12f; sc.PaperTint = 0.25f; sc.PaperColor = C(1f, 0.95f, 0.85f);
                    s.World.Ambient = C(0.14f, 0.12f, 0.1f);
                    s.World.IndoorFog = C(0.2f, 0.18f, 0.15f); s.World.IndoorFogDensity = 0.03f;
                    s.World.GasFog = C(0.62f, 0.6f, 0.5f);
                    s.Palette.Building.Tint = C(0.75f, 0.68f, 0.55f); s.Palette.Building.Amount = 0.2f;
                    s.Palette.Furniture.Saturation = 1.15f;
                });

            yield return Style("S07_Logbook", "Бортовой журнал", "Return of the Obra Dinn, ранний Macintosh",
                "Один бит: две краски, растровые точки и контуры. Темнота — это сплошные чернила, свет лампы — пятно точек. " +
                "Самый узнаваемый и самый смелый вариант.",
                s =>
                {
                    var g = s.Grade;
                    g.Exposure = 1f; g.Contrast = 10f; g.BloomIntensity = 0.6f; g.Vignette = 0f;
                    var sc = s.Screen;
                    sc.PixelSize = 2f; sc.OneBit = true; sc.DotSize = 2f; sc.DitherBias = 0.04f;
                    sc.Ink = C(0.2f, 0.19f, 0.16f); sc.Light = C(0.9f, 0.87f, 0.72f);
                    sc.OutlineStrength = 1f; sc.OutlineColor = C(0.2f, 0.19f, 0.16f);
                    s.World.Ambient = C(0.05f, 0.05f, 0.05f);
                    s.World.IndoorFog = C(0.03f, 0.03f, 0.03f);
                    s.World.GasFog = C(0.5f, 0.5f, 0.5f);
                });

            yield return Style("S08_Footage", "Найденная плёнка", "Content Warning, Paranormal VHS, [REC], Splinter Cell",
                "Вылазка через дешёвую камеру в ночном режиме: зелёная картинка с усилением, строки развёртки, шум, " +
                "пересвеченные лампы и REC. Прямо просится запись забега и просмотр с друзьями.",
                s =>
                {
                    var g = s.Grade;
                    g.Exposure = 1.5f; g.Contrast = 25f; g.Saturation = -100f; g.ColorFilter = C(0.55f, 1f, 0.6f);
                    g.BloomIntensity = 2f; g.BloomThreshold = 0.6f; g.BloomScatter = 0.9f;
                    g.LensDistortion = 0.25f; g.Vignette = 0.45f; g.FilmGrain = 0.35f; g.ChromaticAberration = 0.5f;
                    var sc = s.Screen;
                    sc.PixelSize = 1.6f; sc.Scanlines = 0.25f; sc.ChromaShift = 2f; sc.Noise = 0.12f; sc.Wobble = 1f; sc.RecDot = true;
                    sc.PaperTint = 1f; sc.PaperColor = C(0.45f, 1f, 0.5f); // the night-shot green
                    s.World.Ambient = C(0.05f, 0.05f, 0.05f);
                });

            yield return Style("S09_WhiteRoom", "Белая комната", "SUPERHOT, Mirror's Edge",
                "Мир белый и пустой, роботы — красные, лут — единственные краски. Ноль шума, только силуэты и угрозы. " +
                "Стерильный офис как арт-объект.",
                s =>
                {
                    var g = s.Grade;
                    g.Exposure = 0.3f; g.Contrast = 20f; g.Saturation = 10f; g.BloomIntensity = 0.6f; g.Vignette = 0.1f;
                    s.World.Ambient = C(0.75f, 0.77f, 0.8f);
                    s.World.IndoorFog = C(0.85f, 0.86f, 0.88f); s.World.IndoorFogDensity = 0.02f;
                    s.World.GasFog = C(0.9f, 0.9f, 0.92f);
                    var p = s.Palette;
                    p.Building.Tint = C(0.92f, 0.92f, 0.92f); p.Building.Amount = 1f;
                    p.Furniture.Tint = C(0.85f, 0.86f, 0.88f); p.Furniture.Amount = 0.95f;
                    p.Robots.Tint = C(0.95f, 0.12f, 0.1f); p.Robots.Amount = 1f; p.Robots.Smoothness = 0.25f;
                    p.Players.Tint = C(0.12f, 0.12f, 0.12f); p.Players.Amount = 1f;
                    p.Loot.Saturation = 1.2f;
                });

            yield return Style("S10_Grid", "Сеть", "TRON, Rez, Ruiner, ночной режим камер",
                "Мир глазами системы охраны: всё тёмное, рёбра светятся бирюзовым вблизи и пурпурным вдали. " +
                "Абстрактно и очень «айтишно».",
                s =>
                {
                    var g = s.Grade;
                    g.Exposure = 0.3f; g.Contrast = 10f; g.Saturation = 20f; g.BloomIntensity = 2.5f; g.BloomThreshold = 0.8f; g.Vignette = 0.4f;
                    var sc = s.Screen;
                    sc.NeonStrength = 1.8f; sc.NeonKeep = 0.12f; sc.NeonFar = 22f;
                    sc.NeonNear = C(0.1f, 1f, 0.95f); sc.NeonFarColor = C(0.9f, 0.15f, 1f);
                    sc.OutlineDepth = 10f; sc.OutlineNormal = 3f;
                    s.World.Ambient = C(0.02f, 0.02f, 0.05f);
                    s.World.IndoorFog = C(0.02f, 0f, 0.05f); s.World.IndoorFogDensity = 0.04f;
                    s.World.GasFog = C(0.06f, 0f, 0.12f);
                    s.Palette.Building.Brightness = 0.5f;
                });
        }
    }
}
