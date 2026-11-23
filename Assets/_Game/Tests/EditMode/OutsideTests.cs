using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The outside: the gas kept visually away from the house, the sky above it, a few windows, furniture sized to a person.</summary>
    public class OutsideTests
    {
        static readonly Vector4 House = new(10f, 10f, 5f, 5f); // a 10 x 10 m house around (10, 10)

        [Test]
        public void TheGasThickensLinearlyOverTenMetresFromTheWalls()
        {
            var w = new StyleWorld();
            Assert.AreEqual(10f, w.GasClear, "the street's fog thickens over 10 m from the house");
            Assert.AreEqual(0f, GasView.Density(new Vector3(10f, 1f, 10f), House, w), "none inside");
            Assert.AreEqual(0f, GasView.Density(new Vector3(15f, 1f, 10f), House, w), "none at the wall");
            Assert.AreEqual(w.GasDensity * 0.5f, GasView.Density(new Vector3(20f, 1f, 10f), House, w), 1e-4f, "half way: half");
            Assert.AreEqual(w.GasDensity, GasView.Density(new Vector3(25f, 1f, 10f), House, w), 1e-4f, "full 10 m out");
            Assert.AreEqual(w.GasDensity, GasView.Density(new Vector3(60f, 1f, 60f), House, w), 1e-4f, "full far away");
        }

        [Test]
        public void TheGasIsALayerOverTheGroundTheSkyShowsAboveIt()
        {
            var w = new StyleWorld();
            var far = new Vector3(60f, 0f, 10f);
            float low = GasView.Density(far + Vector3.up * 1.7f, House, w);
            float roofs = GasView.Density(far + Vector3.up * 7f, House, w);
            float high = GasView.Density(far + Vector3.up * 30f, House, w);
            Assert.AreEqual(w.GasDensity, low, 1e-4f, "eye height is in the full gas");
            Assert.AreEqual(w.GasDensity, roofs, 1e-4f, "so are the neighbours' lower floors: nothing far stands out of it");
            Assert.Less(high, low * 0.01f, "30 m up it is almost clear");
        }

        [Test]
        public void TheDerelictStyleHasASky()
        {
            var style = AssetDatabase.LoadAssetAtPath<VisualStyle>("Assets/_Game/Data/Styles/S16_Derelict.asset");
            Assert.IsNotNull(style.World.Sky, "the world outside");
            Assert.IsNotNull(Shader.Find("Stealth/GasFog"));
            var window = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Environment/Window.mat");
            Assert.AreEqual("Universal Render Pipeline/Lit", window.shader.name, "window glass takes light");
            Assert.GreaterOrEqual(window.renderQueue, 3000, "and is see-through: the real outside shows through the hole");
            Assert.Less(window.GetColor("_BaseColor").a, 0.5f, "clear glass, not a white panel");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Level/Window.prefab");
            Assert.IsNotNull(prefab.GetComponent<BoxCollider>(), "the glass stops bodies and items");
        }

        [Test]
        public void TestHousesHaveNoneToTwoWindows()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/_Game/Data/Config/RoomsTest/Level.asset");
            var counts = new System.Collections.Generic.HashSet<int>();
            TestHouses.RoomsTests(24); // (made at once)
            for (int seed = 1; seed <= 24; seed++)
            {
                var l = TestHouses.RoomsTest(seed - 1);
                int n = LevelBuilder.WindowSpots(l).Count;
                Assert.That(n, Is.InRange(0, 2), $"seed {seed}");
                Assert.AreEqual(n, LevelBuilder.WindowSpots(l).Count, "the same windows every time");
                counts.Add(n);
            }
            Assert.IsTrue(counts.Contains(0) && counts.Contains(2), "sometimes none, sometimes two");
            Assert.AreEqual(-1, new BuildingSettings().WindowLimit(5), "the game's houses: a window wherever one fits");
        }

        [Test]
        public void EveryLootItemFitsInALockerCompartment()
        {
            // a compartment of the lockers facing −Z (the door's bounds), floor at 0
            var door = new Bounds(new Vector3(0f, 1.045f, -0.245f), new Vector3(0.47f, 1.97f, 0.02f));
            var inside = new Bounds(new Vector3(0f, 0.9f, -0.005f), new Vector3(0.47f, 1.7f, 0.46f));
            for (int shape = 0; shape < Items.ItemCatalog.Shapes.Length; shape++)
            {
                if (Items.ItemCatalog.HasPhysics(shape)) continue; // real items stand by their model's size (ShovelTests)
                var cells = Items.ItemCatalog.Shapes[shape].Cells;
                var (pos, rot) = Items.StorageMath.LootPose(cells, door, Vector3.back, 0.46f, drawer: false);
                int minX = cells.Min(c => c.x), maxX = cells.Max(c => c.x), minY = cells.Min(c => c.y), maxY = cells.Max(c => c.y);
                float cx = (minX + maxX) / 2f, cy = (minY + maxY) / 2f, k = Items.StorageMath.ItemCell;
                foreach (var c in cells)
                    foreach (var corner in new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 1f, 0.5f), new Vector3(-0.5f, 1f, 0.5f), new Vector3(0.5f, 0f, -0.5f) })
                    {
                        // grid X → model X, grid Y → model +Z (Tools/Blender/generate_models.py model_items), cells 0.96 of a cell
                        var local = new Vector3((c.x - cx + corner.x * 0.96f) * k, corner.y * 0.96f * k, (c.y - cy + corner.z * 0.96f) * k);
                        var p = pos + rot * local;
                        Assert.IsTrue(inside.Contains(p), $"{Items.ItemCatalog.Shapes[shape].Id}: {p} sticks out of the compartment");
                    }
            }
        }

        [TestCase("Lockers", 2.0f, 2.1f, 0.97f, 1.03f)]
        [TestCase("Cabinet", 1.15f, 1.22f, 0.88f, 0.92f)]
        public void FurnitureIsSizedToAPerson(string prefab, float minH, float maxH, float minW, float maxW)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Props/{prefab}.prefab");
            var meshes = go.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
            var b = new Bounds();
            bool first = true;
            foreach (var f in meshes)
            {
                var mb = f.sharedMesh.bounds;
                foreach (var c in new[] { mb.min, mb.max, new Vector3(mb.min.x, mb.max.y, mb.min.z), new Vector3(mb.max.x, mb.min.y, mb.max.z) })
                {
                    var p = go.transform.InverseTransformPoint(f.transform.TransformPoint(c));
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                }
            }
            Assert.That(b.size.y, Is.InRange(minH, maxH), $"{prefab} height (a person is 1.8 m)");
            Assert.That(b.size.x, Is.InRange(minW, maxW), $"{prefab} width");
        }
    }
}
