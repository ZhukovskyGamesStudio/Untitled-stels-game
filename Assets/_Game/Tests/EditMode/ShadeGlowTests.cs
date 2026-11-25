using NUnit.Framework;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>A lampshade lit from inside (<see cref="ShadeGlow"/>, Art/Shaders/Shade.shader).</summary>
    public class ShadeGlowTests
    {
        [Test]
        public void TheShadesReachFromItsBulb()
        {
            var box = new Bounds(new Vector3(0f, 1.5f, 0f), new Vector3(0.4f, 0.3f, 0.4f));
            var (near, far) = ShadeGlow.Reach(box, new Vector3(0f, 1.36f, 0f)); // the bulb inside, near the bottom
            Assert.AreEqual(0f, near, 1e-4f, "inside the shade: it comes right up to the bulb");
            Assert.AreEqual(new Vector3(0.2f, 0.29f, 0.2f).magnitude, far, 1e-3f, "its farthest corner");
            (near, far) = ShadeGlow.Reach(box, new Vector3(0f, 1f, 0f)); // the bulb under the shade
            Assert.AreEqual(0.35f, near, 1e-3f);
            Assert.Greater(far, near);
        }

        [Test]
        public void TheLampsKnowTheirShadeAndTheirBulb()
        {
            var shader = Shader.Find("Stealth/Shade");
            Assert.IsNotNull(shader, "no shade shader");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Props/LampShade.mat");
            Assert.AreEqual(shader, material.shader, "the shade's material is lit from inside");
            Assert.Greater(material.GetFloat("_InnerPeak"), 1.5f, "bright by the bulb");
            Assert.Less(material.GetFloat("_InnerFloor"), 0.5f, "dim at the far rim");
            Assert.Less(material.GetColor("_BaseColor").maxColorComponent, 0.7f, "the cloth is darker than its glow: lit by the room alone it must not look lit from inside");
            foreach (string name in new[] { "DeskLamp", "FloorLamp" })
            {
                var lamp = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Props/{name}.prefab");
                var glow = lamp.GetComponent<ShadeGlow>();
                Assert.IsNotNull(glow, $"{name}: no ShadeGlow");
                Assert.IsNotEmpty(glow.Shades, $"{name}: no shade");
                Assert.IsNotEmpty(glow.Bulbs, $"{name}: no bulb");
                foreach (var shade in glow.Shades) Assert.AreEqual(material, shade.sharedMaterial, $"{name}: {shade.name} is not a shade");
            }
        }
    }
}
