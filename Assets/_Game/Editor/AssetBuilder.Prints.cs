using System.IO;
using UnityEditor;
using UnityEngine;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        /// <summary>Folders of pictures with words and fine lines on them: labels, covers, sheets, machine fronts, screens.</summary>
        public static readonly string[] PrintFolders = { Root + "/Art/Textures/Covers", Root + "/Art/Textures/Screens" };

        /// <summary>Anisotropic samples of a printed picture: a label or a screen is nearly always looked at from an angle.</summary>
        public const int PrintAniso = 8;

        /// <summary>
        /// Printed pictures stay readable from across a room. Words are the first thing to break up when a texture is
        /// read small: with plain bilinear filtering the detail level snaps as the player walks (the letters jump), with
        /// no anisotropic filtering a tilted label is read from a level far too coarse or far too fine, the default block
        /// compression turns small high-contrast letters into blotches at the reduced levels, and a size that is not a
        /// power of two was stretched to one on import (every letter resampled). So: trilinear, anisotropic, the
        /// high-quality compression, their own size.
        /// </summary>
        static void SharpPrints()
        {
            int changed = 0;
            foreach (string folder in PrintFolders)
            {
                if (!Directory.Exists(folder)) continue;
                foreach (string file in Directory.GetFiles(folder, "*.png"))
                {
                    string path = file.Replace('\\', '/');
                    if (AssetImporter.GetAtPath(path) is not TextureImporter imp || imp.textureType != TextureImporterType.Default) continue;
                    if (imp.mipmapEnabled && imp.filterMode == FilterMode.Trilinear && imp.anisoLevel == PrintAniso
                        && imp.textureCompression == TextureImporterCompression.CompressedHQ && imp.npotScale == TextureImporterNPOTScale.None) continue;
                    imp.mipmapEnabled = true;
                    imp.filterMode = FilterMode.Trilinear;
                    imp.anisoLevel = PrintAniso;
                    imp.textureCompression = TextureImporterCompression.CompressedHQ;
                    imp.npotScale = TextureImporterNPOTScale.None;
                    imp.SaveAndReimport();
                    changed++;
                }
            }
            if (changed > 0) Debug.Log($"[Builder] {changed} printed pictures re-imported sharp");
        }
    }
}
