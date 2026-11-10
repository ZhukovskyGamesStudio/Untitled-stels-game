using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Stealth.Tests
{
    public class StyleTests
    {
        [Test]
        public void TintsRecolourMaterials()
        {
            var c = new Color(0.2f, 0.4f, 0.6f, 1f);
            var none = new StyleTint();
            Assert.IsTrue(none.IsIdentity);
            Assert.AreEqual(c, none.Apply(c));
            var red = new StyleTint { Tint = Color.red, Amount = 1f };
            Assert.AreEqual(Color.red, red.Apply(c));
            var grey = new StyleTint { Saturation = 0f }.Apply(c);
            Assert.AreEqual(grey.r, grey.g, 1e-4f, "no saturation: grey");
            Assert.AreEqual(c.r * 0.5f, new StyleTint { Brightness = 0.5f }.Apply(c).r, 1e-4f);
        }

        [Test]
        public void StylesAreConfiguredAndTheFilterIsOnTheRenderer()
        {
            var root = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            Assert.IsNotNull(root.Styles, "GameConfig has no styles");
            var styles = root.Styles.Styles;
            Assert.GreaterOrEqual(styles.Length, 11, "the base look and ten styles");
            Assert.IsTrue(styles.All(s => s != null));
            Assert.AreEqual(styles.Length, styles.Select(s => s.Title).Distinct().Count(), "titles are unique");
            Assert.AreEqual(0f, styles[0].Screen.OutlineStrength, "the first style is the base look");
            Assert.IsNotNull(root.Styles.Default, "the game has a look");
            CollectionAssert.Contains(styles, root.Styles.Default);
            Assert.IsNotEmpty(root.Styles.Building);
            Assert.IsNotEmpty(root.Styles.Robots);

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            var feature = renderer.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f.name == "StyleScreen");
            Assert.IsNotNull(feature, "the style filter is not on the PC renderer");
            Assert.IsTrue(feature.isActive);
            Assert.AreEqual("Stealth/StyleScreen", feature.passMaterial.shader.name);
            Assert.IsTrue(feature.passMaterial.shader.isSupported, "the style shader does not compile");
            Assert.IsTrue((feature.requirements & ScriptableRenderPassInput.Normal) != 0, "outlines need the normals");
        }
    }
}
