using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Stealth.Player;
using Stealth.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.Tests
{
    /// <summary>
    /// The interface's paper look holds together: text reads on its paper (WCAG contrast), every label of the prefabs is
    /// in one of the interface's fonts and those fonts have every character the string tables use, the screens keep the
    /// whole layout on any screen shape, and the small layout helpers.
    /// </summary>
    public class UiLookTests
    {
        const string FontsDir = "Assets/_Game/Art/Fonts";
        const string UiDir = "Assets/_Game/Prefabs/UI";

        static readonly (string name, Color c)[] Papers =
        {
            ("Paper", UiPalette.Paper), ("PaperShade", UiPalette.PaperShade), ("PaperDim", UiPalette.PaperDim),
            ("CarbonPink", UiPalette.CarbonPink), ("CarbonYellow", UiPalette.CarbonYellow),
            ("NoteYellow", UiPalette.NoteYellow), ("NoteGreen", UiPalette.NoteGreen), ("NotePink", UiPalette.NotePink),
            ("NoteBlue", UiPalette.NoteBlue), ("NoteOrange", UiPalette.NoteOrange),
        };

        [Test]
        public void InkAndPenReadOnEveryPaperAndNote()
        {
            foreach (var (name, paper) in Papers)
            {
                Assert.GreaterOrEqual(UiPalette.Contrast(UiPalette.Ink, paper), 7f, $"ink on {name}");
                Assert.GreaterOrEqual(UiPalette.Contrast(UiPalette.InkSoft, paper), 4.5f, $"soft ink on {name}");
                Assert.GreaterOrEqual(UiPalette.Contrast(UiPalette.Pen, paper), 4.5f, $"pen on {name}");
            }
        }

        [Test]
        public void TheColouredInksReadOnTheSheet()
        {
            foreach (var (name, ink) in new[] { ("Stamp", UiPalette.Stamp), ("Good", UiPalette.Good), ("Warn", UiPalette.Warn) })
            {
                Assert.GreaterOrEqual(UiPalette.Contrast(ink, UiPalette.Paper), 4f, $"{name} on paper");
                Assert.GreaterOrEqual(UiPalette.Contrast(ink, UiPalette.PaperDim), 3f, $"{name} on the dim paper in the world");
            }
            Assert.GreaterOrEqual(UiPalette.Contrast(UiPalette.InkFaint, UiPalette.Paper), 3f, "faint ink (hints, done tasks) still reads");
        }

        [Test]
        public void EveryPlayersNameReadsOnPaperInTheirInk()
        {
            for (int slot = 0; slot < PlayerLooks.Slots; slot++)
            {
                var ink = UiPalette.InkOf(PlayerLooks.TeamColor(slot));
                Assert.GreaterOrEqual(UiPalette.Contrast(ink, UiPalette.Paper), 4.5f, $"player {slot}");
                Assert.AreEqual(1f, ink.a);
            }
        }

        [Test]
        public void TheClockCardWarmsUpButTheTimeStaysReadable()
        {
            Assert.AreEqual(UiPalette.Paper, UiPalette.HeatPaper(0f));
            var hot = UiPalette.HeatPaper(1f);
            Assert.Greater(hot.r - hot.g, UiPalette.Paper.r - UiPalette.Paper.g + 0.15f, "hot is reddish");
            for (float h = 0f; h <= 1.001f; h += 0.1f)
                Assert.GreaterOrEqual(UiPalette.Contrast(UiPalette.Ink, UiPalette.HeatPaper(h)), 7f, $"heat {h:0.0}");
        }

        [Test]
        public void ThePaperIsNotPureWhite()
        {
            // a white sheet glares in a dark game
            Assert.Less(UiPalette.Paper.r + UiPalette.Paper.g + UiPalette.Paper.b, 2.9f);
            Assert.Less(UiPalette.PaperDim.grayscale, UiPalette.Paper.grayscale);
        }

        [Test]
        public void ContrastIsTheWcagRatio()
        {
            Assert.AreEqual(21f, UiPalette.Contrast(Color.black, Color.white), 0.01f);
            Assert.AreEqual(1f, UiPalette.Contrast(UiPalette.Paper, UiPalette.Paper), 1e-4f);
            Assert.AreEqual(UiPalette.Contrast(UiPalette.Ink, UiPalette.Paper), UiPalette.Contrast(UiPalette.Paper, UiPalette.Ink), 1e-5f);
        }

        [Test]
        public void AlignedTextStartsWhereItsAlignmentPutsIt()
        {
            Assert.AreEqual(-50f, UiMath.AlignedLeft(-50f, 100f, 40f, TextAnchor.MiddleLeft));
            Assert.AreEqual(-20f, UiMath.AlignedLeft(-50f, 100f, 40f, TextAnchor.UpperCenter));
            Assert.AreEqual(10f, UiMath.AlignedLeft(-50f, 100f, 40f, TextAnchor.LowerRight));
        }

        [Test]
        public void AnElementIsKeptWholeOnTheScreen()
        {
            var canvas = new Vector2(1280, 960);
            var half = new Vector2(100, 200);
            var top = new Vector2(100, 0);
            Assert.AreEqual(new Vector2(640, 500), UiMath.KeepOnScreen(new Vector2(640, 500), canvas, half, top, 8f), "inside: untouched");
            Assert.AreEqual(108f, UiMath.KeepOnScreen(new Vector2(-30, 500), canvas, half, top, 8f).x, "off the left edge");
            Assert.AreEqual(1172f, UiMath.KeepOnScreen(new Vector2(1400, 500), canvas, half, top, 8f).x, "off the right edge");
            Assert.AreEqual(208f, UiMath.KeepOnScreen(new Vector2(640, 50), canvas, half, top, 8f).y, "its bottom above the screen's");
            Assert.AreEqual(new Vector2(50, 50), UiMath.KeepOnScreen(new Vector2(0, 0), new Vector2(100, 100), half, top, 8f), "too small a screen: centred");
        }

        static IEnumerable<GameObject> UiPrefabs() => AssetDatabase.FindAssets("t:Prefab", new[] { UiDir })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>);

        [Test]
        public void EveryInterfaceLabelUsesTheInterfaceFonts()
        {
            var fonts = AssetDatabase.FindAssets("t:Font", new[] { FontsDir }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Font>).ToHashSet();
            Assert.GreaterOrEqual(fonts.Count, 5, "the five faces in Art/Fonts");
            var wrong = new List<string>();
            foreach (var prefab in UiPrefabs())
                foreach (var t in prefab.GetComponentsInChildren<Text>(true))
                    if (!fonts.Contains(t.font) && !t.GetComponentInParent<StudioSplash>(true)) wrong.Add($"{prefab.name}/{t.name}: {(t.font != null ? t.font.name : "none")}");
            Assert.IsEmpty(wrong, "labels in another font:\n" + string.Join("\n", wrong.Take(20)));
        }

        [Test]
        public void TheScreensKeepTheWholeLayoutOnAnyScreenShape()
        {
            foreach (var prefab in UiPrefabs())
            {
                var scaler = prefab.GetComponent<CanvasScaler>();
                if (scaler == null || prefab.GetComponent<Canvas>().renderMode != RenderMode.ScreenSpaceOverlay) continue;
                Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode, prefab.name);
                Assert.AreEqual(CanvasScaler.ScreenMatchMode.Expand, scaler.screenMatchMode, $"{prefab.name}: nothing cut off on 4:3 or 16:10");
                Assert.AreEqual(new Vector2(1280, 720), scaler.referenceResolution, prefab.name);
            }
        }

        [Test]
        public void TheFontsHaveEveryCharacterTheGameWrites()
        {
            // every face falls back to PT Mono (the arrows of the key names): a character is fine if either has it
            var mono = AssetDatabase.LoadAssetAtPath<Font>($"{FontsDir}/PTMono-Regular.ttf");
            Assert.IsNotNull(mono);
            var used = new HashSet<char>();
            foreach (var file in Directory.GetFiles("Assets/_Game/Resources/Strings", "*.csv"))
                foreach (char ch in File.ReadAllText(file))
                    if (ch > ' ' && ch != '﻿') used.Add(ch);
            foreach (var path in AssetDatabase.FindAssets("t:Font", new[] { FontsDir }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                var missing = used.Where(ch => !font.HasCharacter(ch) && !mono.HasCharacter(ch)).OrderBy(c => c).ToArray();
                Assert.IsEmpty(missing, $"{font.name} lacks: {new string(missing)}");
            }
        }
    }
}
