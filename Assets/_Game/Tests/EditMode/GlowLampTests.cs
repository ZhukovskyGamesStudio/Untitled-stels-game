using NUnit.Framework;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>A lamp that is part of a piece of furniture gives real light (<see cref="GlowLamp"/>): a glowing shade alone lit nothing.</summary>
    public class GlowLampTests
    {
        [Test]
        public void TheLightHangsOverWhatGlows()
        {
            var shade = new Bounds(new Vector3(0.1f, 0.87f, 0.08f), new Vector3(0.23f, 0.16f, 0.23f));
            var at = GlowLamp.Above(shade, 0.5f);
            Assert.AreEqual(0.1f, at.x, 1e-4f);
            Assert.AreEqual(0.08f, at.z, 1e-4f);
            Assert.AreEqual(0.95f + 0.5f, at.y, 1e-4f, "over the top of the shade, not in it");
        }

        [Test]
        public void FurnitureWithALampOfItsOwnLightsWhatIsUnderIt()
        {
            foreach (string name in new[] { "NightTable", "SideTable", "ReadingTable", "SleepPod" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Props/{name}.prefab");
                Assert.IsNotNull(prefab, name);
                var glow = prefab.GetComponent<GlowLamp>();
                Assert.IsNotNull(glow, $"{name}: its lamp glows and lights nothing (no GlowLamp)");
                Assert.IsNotNull(glow.Lamp, $"{name}: no light");
                Assert.IsNotEmpty(glow.Bulbs, $"{name}: nothing glows");
                Assert.IsNotNull(glow.Lamp.GetComponent<LightSource>(), $"{name}: robots must see a player in its light like in any lamp's");
                Assert.AreEqual(LightType.Spot, glow.Lamp.type, $"{name}: a point light shines up through the slab");
                Assert.Greater(Vector3.Dot(glow.Lamp.transform.forward, Vector3.down), 0.99f, $"{name}: the light shines down");
                Assert.Less(glow.Lamp.spotAngle, 170f, $"{name}: nothing of it goes sideways");
                float top = float.MinValue;
                foreach (var b in glow.Bulbs) top = Mathf.Max(top, b.bounds.max.y);
                Assert.Greater(glow.Lamp.transform.position.y, top + 0.2f, $"{name}: right at the shade the light burns a white disc into the furniture");
                // without shadows it stays small (LightLeakTests: it must not lie on the neighbour's floor); a bigger one is shadowed
                if (glow.Lamp.shadows == LightShadows.None) Assert.LessOrEqual(glow.Lamp.range, 2f, $"{name}: too long to stay in its room without shadows");
                else Assert.LessOrEqual(glow.Lamp.range, 3f, $"{name}: a little lamp, not a ceiling light");
                Assert.Less(glow.Lamp.intensity, 1.3f, $"{name}: dimmer than a desk lamp");
            }
        }
    }
}
