using System.Collections.Generic;
using NUnit.Framework;
using Stealth.Enemies;
using Stealth.Level;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stealth.Tests
{
    /// <summary>
    /// Light must not get where it cannot: through walls, slabs and the roof. A closed box without windows is pitch black
    /// inside, whatever shines outside (the moon, a lamp next to it, a lamp on its roof).
    /// </summary>
    public class LightLeakTests
    {
        static LevelArt Art => AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");

        [Test]
        public void WallsSlabsAndTheRoofCastShadows()
        {
            var art = Art;
            foreach (var prefab in new[] { art.WallBlock, art.TrimBlock, art.FloorSlab, art.Roof })
                foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                    Assert.AreEqual(ShadowCastingMode.TwoSided, r.shadowCastingMode, $"{prefab.name}/{r.name} must block light from both sides");
        }

        [Test]
        public void LampsThatCouldShineThroughAWallCastShadows()
        {
            var art = Art;
            var prefabs = new List<GameObject> { art.RoomLight, art.StreetLamp, art.DeskLamp, art.FloorLamp, art.VendingMachine, art.ExitSign };
            foreach (var prefab in prefabs)
                foreach (var light in prefab.GetComponentsInChildren<Light>(true))
                {
                    if (light.shadows != LightShadows.None) continue;
                    // Unshadowed lights are allowed only as small spots that cannot reach up to the floor above.
                    Assert.AreEqual(LightType.Spot, light.type, $"{prefab.name}: a point light without shadows shines through walls");
                    float upward = 90f - Vector3.Angle(Vector3.down, light.transform.forward) - light.spotAngle / 2f; // < 0: the cone reaches above the horizon
                    float reachUp = upward >= 0f ? 0f : light.range * Mathf.Sin(-upward * Mathf.Deg2Rad);
                    Assert.Less(light.transform.position.y + reachUp, LevelMetrics.FloorHeight, $"{prefab.name}: {light.name} reaches the floor above");
                    Assert.LessOrEqual(light.range, 3f, $"{prefab.name}: {light.name} is too long to stay in its room without shadows");
                }
        }

        [Test]
        public void RobotAndDeviceLightsCastShadows()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs/Robots", "Assets/_Game/Prefabs/Sensors" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var light in prefab.GetComponentsInChildren<Light>(true))
                    Assert.AreNotEqual(LightShadows.None, light.shadows, $"{prefab.name}/{light.name} would glow through floors and walls");
            }
        }

        /// <summary>
        /// Every material reacts to light. One that glows must glow for real (HDR emission above the bloom threshold) —
        /// a dim emission just looks like a material that ignores the lamps. Unlit only for effects and the gas behind windows.
        /// </summary>
        [Test]
        public void MaterialsEitherTakeLightOrReallyGlow()
        {
            var unlitAllowed = new HashSet<string> { "Laser", "Radio", "Stomp", "Window" };
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Game/Art/Materials" }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat.shader.name.Contains("Unlit"))
                {
                    Assert.IsTrue(unlitAllowed.Contains(mat.name), $"{mat.name} is unlit: it ignores the lamps");
                    continue;
                }
                if (!mat.IsKeywordEnabled("_EMISSION")) continue;
                var e = mat.GetColor("_EmissionColor");
                Assert.GreaterOrEqual(Mathf.Max(e.r, Mathf.Max(e.g, e.b)), 1f, $"{mat.name} glows dimly: make it bloom or not glow");
            }
        }

        [Test]
        public void OnlyTheNearestLampsThatFitTheShadowAtlasAreDrawn()
        {
            // Small lamps must really use low maps, or the budget underestimates them and the atlas overflows.
            var desk = Art.DeskLamp.GetComponentInChildren<Light>();
            var data = new SerializedObject(desk.GetComponent("UniversalAdditionalLightData"));
            Assert.AreEqual(0, data.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue, "desk lamp: low shadow tier");
            Assert.AreEqual(1.5f, LightBudget.Cost(LightType.Point, 9.5f), 1e-4f, "a room lamp: six medium maps");
            Assert.Less(LightBudget.Cost(LightType.Spot, 3f), LightBudget.Cost(LightType.Spot, 14f), "a small lamp is cheap");
            var costs = new List<float> { 1.5f, 1.5f, 1.5f, 1.5f, 0.0625f, 1.5f };
            var on = new bool[costs.Count];
            LightBudget.Choose(costs, 5f, on);
            CollectionAssert.AreEqual(new[] { true, true, true, false, true, false }, on, "nearest first; a small one still fits");
            Assert.Less(LightBudget.Budget, 16f, "spare room in the 4096² atlas (16 maps of 1024²)");
        }

        readonly List<GameObject> m_Objects = new();
        List<LightSource> m_Disabled;

        [SetUp]
        public void SetUp()
        {
            m_Disabled = new List<LightSource>(LightSource.All);
            foreach (var o in m_Disabled) o.enabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in m_Objects) Object.DestroyImmediate(go);
            m_Objects.Clear();
            foreach (var o in m_Disabled) if (o != null) o.enabled = true;
        }

        GameObject Make(string name)
        {
            var go = new GameObject(name);
            m_Objects.Add(go);
            return go;
        }

        void Wall(Vector3 center, Vector3 size)
        {
            var go = Make("Wall");
            go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        Light AddLight(Vector3 at, LightType type, Quaternion rotation)
        {
            var go = Make("Light");
            go.transform.SetPositionAndRotation(at, rotation);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.intensity = 3f;
            l.range = 10f;
            l.spotAngle = 150f;
            go.AddComponent<LightSource>();
            return l;
        }

        [Test]
        public void AClosedBoxIsPitchBlackInside()
        {
            // 2 x 2.4 x 2 m box of 20 cm walls on a slab, roof on top.
            Wall(new Vector3(0, 1.2f, 1.1f), new Vector3(2.4f, 2.6f, 0.2f));
            Wall(new Vector3(0, 1.2f, -1.1f), new Vector3(2.4f, 2.6f, 0.2f));
            Wall(new Vector3(1.1f, 1.2f, 0), new Vector3(0.2f, 2.6f, 2.4f));
            Wall(new Vector3(-1.1f, 1.2f, 0), new Vector3(0.2f, 2.6f, 2.4f));
            Wall(new Vector3(0, -0.1f, 0), new Vector3(2.4f, 0.2f, 2.4f));
            Wall(new Vector3(0, 2.5f, 0), new Vector3(2.4f, 0.2f, 2.4f));
            AddLight(new Vector3(0, 10f, 0), LightType.Directional, Quaternion.Euler(50f, 30f, 0f)); // the moon
            AddLight(new Vector3(2f, 1.5f, 0f), LightType.Point, Quaternion.identity);               // a lamp next to the box
            AddLight(new Vector3(0f, 3.2f, 0f), LightType.Spot, Quaternion.Euler(90f, 0f, 0f));      // a lamp on the roof, shining down
            Physics.SyncTransforms();
            var s = new IlluminationSettings { AmbientWeight = 0f }; // the game's ambient is black (checked below); the test scene's is not

            Assert.AreEqual(Color.black, LightMath.NightAmbient, "no light from nowhere");
            foreach (var p in new[] { new Vector3(0, 1.3f, 0), new Vector3(0.8f, 0.2f, 0.8f), new Vector3(-0.8f, 2.2f, -0.8f) })
                Assert.AreEqual(0f, Perception.Illumination(p, s), 1e-5f, $"light inside the box at {p}");
            Assert.Greater(Perception.Illumination(new Vector3(3f, 1.5f, 0f), s), 0.1f, "outside, next to the lamp, it is lit");
        }
    }
}
