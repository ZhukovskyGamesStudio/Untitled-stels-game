using System.IO;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Game;
using Stealth.Player;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Smoothing of the picture: the player's choice (<see cref="SmoothingRules"/>), what must not flicker by itself, printed pictures.</summary>
    public class ScreenSmoothingTests
    {
        [Test]
        public void TheChoiceTurnsIntoWhatTheCameraAndTheFinalPassDo()
        {
            Assert.AreEqual(CameraSmoothing.None, SmoothingRules.Camera(Smoothing.Off, true));
            Assert.IsFalse(SmoothingRules.FinalPass(Smoothing.Off, true));
            Assert.AreEqual(CameraSmoothing.None, SmoothingRules.Camera(Smoothing.Fxaa, true));
            Assert.IsTrue(SmoothingRules.FinalPass(Smoothing.Fxaa, true));
            Assert.AreEqual(CameraSmoothing.Smaa, SmoothingRules.Camera(Smoothing.Smaa, true));
            Assert.IsTrue(SmoothingRules.FinalPass(Smoothing.Smaa, true));
            Assert.AreEqual(CameraSmoothing.Temporal, SmoothingRules.Camera(Smoothing.Taa, true));
            Assert.IsFalse(SmoothingRules.FinalPass(Smoothing.Taa, true), "temporal alone: nothing over the finished picture");
            Assert.AreEqual(CameraSmoothing.Temporal, SmoothingRules.Camera(Smoothing.TaaFxaa, true));
            Assert.IsTrue(SmoothingRules.FinalPass(Smoothing.TaaFxaa, true));
        }

        [Test]
        public void AStyleThatDrawsItsLinesAfterTheBlendFallsBackToTheOldPair()
        {
            foreach (var choice in new[] { Smoothing.Taa, Smoothing.TaaFxaa })
            {
                Assert.AreEqual(CameraSmoothing.Smaa, SmoothingRules.Camera(choice, false), $"{choice}");
                Assert.IsTrue(SmoothingRules.FinalPass(choice, false), $"{choice}: the ink lines are drawn after the camera's own smoothing");
            }
            Assert.IsTrue(ScreenSmoothing.TemporalFits(null));
            Assert.IsTrue(ScreenSmoothing.TemporalFits(new StyleScreen()));
            Assert.IsFalse(ScreenSmoothing.TemporalFits(new StyleScreen { OutlineStrength = 0.6f }), "outlines come from the frame's depth: they would tremble");
            Assert.IsFalse(ScreenSmoothing.TemporalFits(new StyleScreen { HatchStrength = 0.4f }));
            Assert.IsFalse(ScreenSmoothing.TemporalFits(new StyleScreen { PixelSize = 3f }));
            Assert.IsFalse(ScreenSmoothing.TemporalFits(new StyleScreen { OneBit = true }));
        }

        [Test]
        public void TheChoiceCyclesThroughEveryModeAndASavedNumberIsAlwaysAMode()
        {
            var seen = new System.Collections.Generic.HashSet<Smoothing>();
            var s = Smoothing.Off;
            for (int i = 0; i < SmoothingRules.Count; i++) { seen.Add(s); s = SmoothingRules.Next(s); }
            Assert.AreEqual(SmoothingRules.Count, seen.Count);
            Assert.AreEqual(Smoothing.Off, s, "back where it started");
            Assert.AreEqual(System.Enum.GetValues(typeof(Smoothing)).Length, SmoothingRules.Count);
            Assert.AreEqual(SmoothingRules.Default, SmoothingRules.Clamp(-3));
            Assert.AreEqual(SmoothingRules.Default, SmoothingRules.Clamp(99));
            Assert.AreEqual(Smoothing.Smaa, SmoothingRules.Clamp((int)Smoothing.Smaa));
            foreach (Smoothing mode in System.Enum.GetValues(typeof(Smoothing)))
                Assert.AreNotEqual(SmoothingRules.Key(mode), Loc.T(SmoothingRules.Key(mode)), $"{mode}: no name in the tables");
        }

        /// <summary>
        /// URP's blue-noise ambient occlusion takes another noise texture and another offset every frame of a player
        /// (it expects a temporal filter after it; in the editor with tests the noise stands still, so it cannot be
        /// seen there): dark corners crawled while the player stood still.
        /// </summary>
        [Test]
        public void AmbientOcclusionDoesNotChangeItsNoiseEveryFrame()
        {
            string text = File.ReadAllText("Assets/Settings/PC_Renderer.asset");
            int at = text.IndexOf("m_Name: ScreenSpaceAmbientOcclusion", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, "the renderer has no ambient occlusion feature");
            int method = text.IndexOf("AOMethod:", at, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(method, 0);
            Assert.AreEqual("1", text.Substring(method + "AOMethod:".Length, 3).Trim(), "0 is blue noise (a new pattern every frame), 1 the interleaved gradient (steady)");
        }

        [Test]
        public void PrintedPicturesAreReadFromAnAngleAndFromAfar()
        {
            // (the folders and the number of AssetBuilder.SharpPrints: the tests do not see the editor's assembly)
            foreach (string folder in new[] { "Assets/_Game/Art/Textures/Covers", "Assets/_Game/Art/Textures/Screens" })
                foreach (string file in Directory.GetFiles(folder, "*.png"))
                {
                    string path = file.Replace('\\', '/');
                    if (AssetImporter.GetAtPath(path) is not TextureImporter imp || imp.textureType != TextureImporterType.Default) continue;
                    Assert.IsTrue(imp.mipmapEnabled, path);
                    Assert.AreEqual(FilterMode.Trilinear, imp.filterMode, $"{path}: the detail level snaps as the player walks");
                    Assert.GreaterOrEqual(imp.anisoLevel, 8, $"{path}: a tilted label breaks up");
                    Assert.AreEqual(TextureImporterCompression.CompressedHQ, imp.textureCompression, $"{path}: small letters turn to blotches");
                    Assert.AreEqual(TextureImporterNPOTScale.None, imp.npotScale, $"{path}: stretched to a power of two, every letter resampled");
                    Assert.AreEqual(-0.5f, imp.mipMapBias, 1e-4f, $"{path}: read at the plain detail level, small letters are a smudge from two metres");
                    Assert.AreEqual(TextureImporterMipFilter.KaiserFilter, imp.mipmapFilter, $"{path}: the box filter greys thin strokes out of the reduced levels");
                }
        }

        [Test]
        public void MovingThingsOfOurOwnShadersTellTheTemporalFilterWhereTheyWere()
        {
            foreach (string shader in new[] { "Surface", "Shade" })
            {
                string text = File.ReadAllText($"Assets/_Game/Art/Shaders/{shader}.shader");
                StringAssert.Contains("\"LightMode\" = \"MotionVectors\"", text, $"{shader}: a drawer or a door of this material smears as it moves");
            }
        }
    
        [Test]
        public void LightingAndTexturesAreSetApartAndBothStartNew()
        {
            bool lighting = GameSettings.NewLighting, textures = GameSettings.NewTextures;
            try
            {
                GameSettings.NewLighting = GameSettings.NewTextures = false;
                GameSettings.ResetToDefaults();
                Assert.IsTrue(GameSettings.NewLighting, "the reworked light is the default");
                Assert.IsTrue(GameSettings.NewTextures, "the models' maps and the 2K scans are the default");
                GameSettings.NewLighting = false;
                Assert.IsTrue(GameSettings.NewTextures, "one does not move the other");
            }
            finally
            {
                GameSettings.NewLighting = lighting;
                GameSettings.NewTextures = textures;
            }
            foreach (string key in new[] { "menu.lighting_new", "menu.lighting_old", "menu.textures_new", "menu.textures_old" })
                Assert.AreNotEqual(key, Loc.T(key), $"{key}: no name in the tables");
        }

        [Test]
        public void TheDefaultStyleRemembersTheLookItHadBefore()
        {
            var style = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles.Default;
            Assert.IsTrue(style.Old.Has, $"{style.name}: \"old style\" in the settings would change nothing");
            Assert.AreNotEqual(style.Grade.Tonemap, style.Old.Tonemap);
            Assert.AreEqual(0f, style.Old.LampPool, "the old lamps lit their rooms evenly");
            Assert.Greater(style.Old.RoomFill, style.Grade.RoomFill);
        }
    }
}
