using System.IO;
using System.Linq;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        /// <summary>Where the models' own baked maps are (Tools/Blender/shapebake.py): &lt;model&gt;_n.png and _m.png.</summary>
        public const string ShapeTextures = Root + "/Art/Textures/Shapes";
        const string ShapeLibraryPath = DataDir + "/ShapeLibrary.asset";

        /// <summary>The side of every model's normals and of its masks in the game (the arrays want one size each; a picture baked bigger is brought down).</summary>
        public const int ShapeNormalSize = 2048, ShapeMaskSize = 1024;

        static ShapeLibrary s_Shapes;

        /// <summary>
        /// The list of the models that have baked maps (<see cref="ShapeLibrary"/>): every pair of pictures in the folder,
        /// by name, imported the way the game's arrays want them — one size and one block format each kind (two-channel
        /// normals; the masks half the side: they are soft).
        /// </summary>
        static ShapeLibrary Shapes()
        {
            if (s_Shapes != null) return s_Shapes;
            var models = Directory.Exists(ShapeTextures)
                ? Directory.GetFiles(ShapeTextures, "*_n.png").Select(f => Path.GetFileName(f).Replace("_n.png", "")).Where(m => File.Exists($"{ShapeTextures}/{m}_m.png")).OrderBy(m => m, System.StringComparer.Ordinal).ToArray()
                : new string[0];
            foreach (string m in models)
            {
                ShapeImport($"{ShapeTextures}/{m}_n.png", ShapeNormalSize, TextureImporterFormat.BC5);
                ShapeImport($"{ShapeTextures}/{m}_m.png", ShapeMaskSize, TextureImporterFormat.BC7);
            }
            s_Shapes = DataAsset<ShapeLibrary>(ShapeLibraryPath, l => { });
            var normals = models.Select(m => AssetDatabase.LoadAssetAtPath<Texture2D>($"{ShapeTextures}/{m}_n.png")).ToArray();
            var masks = models.Select(m => AssetDatabase.LoadAssetAtPath<Texture2D>($"{ShapeTextures}/{m}_m.png")).ToArray();
            // the surfaces that came at 2K (the furniture's and the rooms' scans): the old-models look reads them a level down
            var highRes = AssetDatabase.FindAssets("t:Texture2D", new[] { SurfaceTextures, ExternalDir + "/Textures" }.Where(AssetDatabase.IsValidFolder).ToArray())
                .Select(g => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g))).Where(t => t != null && t.width >= 2048)
                .OrderBy(t => AssetDatabase.GetAssetPath(t), System.StringComparer.Ordinal).ToArray();
            // every mesh of every baked model (its file, wherever under the models it lies: a base model, a style's variant)
            var files = Directory.GetFiles(Root + "/Art/Models", "*.fbx", SearchOption.AllDirectories).ToLookup(f => Path.GetFileNameWithoutExtension(f), f => f.Replace('\\', '/'));
            var meshes = new System.Collections.Generic.List<Mesh>();
            var slices = new System.Collections.Generic.List<int>();
            var channels = new System.Collections.Generic.List<int>();
            var scratch = new System.Collections.Generic.List<Vector2>();
            int spare = 0;
            for (int i = 0; i < models.Length; i++)
                foreach (string file in files[models[i]])
                    foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(file).OfType<Mesh>().OrderBy(m => m.name, System.StringComparer.Ordinal))
                    {
                        // the baked set is the last one the mesh has: a part that came out of its script with a spare set
                        // of its own carries the baked one third (a fourth or further: none here — its part goes without)
                        int channel = 0;
                        for (int k = 1; k < 4; k++)
                        {
                            mesh.GetUVs(k, scratch);
                            if (scratch.Count == mesh.vertexCount) channel = k;
                        }
                        if (channel < 1 || channel > 2) continue;
                        if (channel == 2) spare++;
                        meshes.Add(mesh);
                        slices.Add(i);
                        channels.Add(channel);
                    }
            if (spare > 0) Debug.Log($"[Builder] {spare} of {meshes.Count} baked meshes carry their maps' UVs as the third set");
            if (!s_Shapes.Models.SequenceEqual(models) || !s_Shapes.Normals.SequenceEqual(normals) || !s_Shapes.Masks.SequenceEqual(masks) || !s_Shapes.HighRes.SequenceEqual(highRes)
                || !s_Shapes.Meshes.SequenceEqual(meshes) || !s_Shapes.Slices.SequenceEqual(slices) || !s_Shapes.Channels.SequenceEqual(channels))
            {
                s_Shapes.Models = models;
                s_Shapes.Normals = normals;
                s_Shapes.Masks = masks;
                s_Shapes.HighRes = highRes;
                s_Shapes.Meshes = meshes.ToArray();
                s_Shapes.Slices = slices.ToArray();
                s_Shapes.Channels = channels.ToArray();
                EditorUtility.SetDirty(s_Shapes);
            }
            // the styles' dressing tells every renderer the slice of the mesh it shows
            var styles = AssetDatabase.LoadAssetAtPath<Stealth.Configs.StyleConfig>(StyleConfigPath);
            if (styles != null && styles.Shapes != s_Shapes)
            {
                styles.Shapes = s_Shapes;
                EditorUtility.SetDirty(styles);
            }
            return s_Shapes;
        }

        static void ShapeImport(string path, int size, TextureImporterFormat format)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter imp) return;
            var platform = imp.GetPlatformTextureSettings("Standalone");
            if (imp.textureType == TextureImporterType.Default && !imp.sRGBTexture && imp.mipmapEnabled && imp.wrapMode == TextureWrapMode.Clamp
                && platform.overridden && platform.format == format && platform.maxTextureSize == size && imp.npotScale == TextureImporterNPOTScale.ToNearest
                && imp.ignoreMipmapLimit) return;
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = false;
            imp.mipmapEnabled = true;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 4;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.npotScale = TextureImporterNPOTScale.ToNearest;
            imp.maxTextureSize = size;
            imp.ignoreMipmapLimit = true; // (ShapeLibrary packs the arrays from the level the texture size setting names)
            imp.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Standalone", overridden = true, maxTextureSize = size, format = format, textureCompression = TextureImporterCompression.CompressedHQ,
            });
            imp.SaveAndReimport();
        }
    }
}
