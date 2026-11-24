using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The staircase looks a style can ask for, and the material slots it dresses in its own floor, wall and trim.</summary>
    public class StairLookTests
    {
        [Test]
        public void AStyleCanAskForItsLookOrLeaveItToTheFamily()
        {
            Assert.AreEqual(StairStyle.Wood, StairStyles.Pick(StairChoice.Wood, ModelFamily.Clean, 7));
            Assert.AreEqual(StairStyle.Concrete, StairStyles.Pick(StairChoice.Concrete, ModelFamily.Toy, 7));
            for (int seed = 0; seed < 20; seed++)
                Assert.AreEqual(StairStyles.Pick(ModelFamily.Clean, seed), StairStyles.Pick(StairChoice.Auto, ModelFamily.Clean, seed));
            Assert.AreEqual(StairStyles.Count + 1, System.Enum.GetValues(typeof(StairChoice)).Length, "Auto + every look");
        }

        [Test]
        public void TheDefaultStyleHasConcreteStairsDressedInItsOwnFinishes()
        {
            var config = AssetDatabase.LoadAssetAtPath<StyleConfig>("Assets/_Game/Data/Styles/Styles.asset");
            var g = config.Default.Geometry;
            Assert.AreEqual("S16_Derelict", config.Default.name, "the quiet facility is the game's look");
            Assert.AreEqual(StairChoice.Concrete, g.Stairs, "a facility's bare concrete stair");
            Assert.IsNotNull(g.Floor, "its treads take the floor finish");
            Assert.IsNotNull(g.Wall, "its body the wall finish");
            Assert.IsNotNull(g.Trim, "its balusters the trim");
        }

        [TestCase("stair_concrete")]
        [TestCase("stair_wood")]
        [TestCase("stair_glass")]
        public void NothingShowsThroughUnderTheRailOverTheTreads(string look)
        {
            var go = new GameObject("Stair");
            try
            {
                go.transform.position = new Vector3(0f, 700f, 0f);
                go.AddComponent<MeshCollider>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/_Game/Art/Models/Stairs/{look}.asset");
                Physics.SyncTransforms();
                float rise = LevelMetrics.FloorHeight / 2f / LevelMetrics.StepsPerFlight;
                for (int j = 0; j < LevelMetrics.StepsPerFlight; j++)
                    foreach (float back in new[] { 0.15f, 0.5f, 0.9f })
                        for (float up = 0.02f; up < rise * 0.95f; up += 0.05f)
                        {
                            // over this tread, from the middle of the flight towards its open side
                            var from = new Vector3(LevelMetrics.LandingEnd + (j + back) * LevelMetrics.StepRun, 700f + LevelMetrics.FloorHeight / 2f + (j + 1) * rise + up, (StairGeometry.LaneB0 + StairGeometry.LaneB1) / 2f);
                            if (from.y - 700f > LevelMetrics.FloorHeight / 2f + (j + back + 1f) * rise) continue; // over the nosing line: the railing's own
                            Assert.IsTrue(Physics.Raycast(from, Vector3.forward, 1.5f), $"step {j}, {back:0.00} in, {up:0.00} m over it: one sees through");
                        }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase("Stairs", false)]
        [TestCase("StairsMirrored", false)]
        [TestCase("RailingBay", true)]
        public void EveryMaterialSlotOfEveryLookHasItsPart(string prefab, bool railing)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Level/{prefab}.prefab");
            var look = go.GetComponent<StairLook>();
            Assert.AreEqual(railing, look.Railing);
            for (int i = 0; i < StairStyles.Count; i++)
            {
                var r = look.Variants[i].GetComponent<MeshRenderer>();
                int slots = look.Variants[i].GetComponent<MeshFilter>().sharedMesh.subMeshCount;
                Assert.AreEqual(slots, r.sharedMaterials.Length, $"{(StairStyle)i}: a material per submesh");
                Assert.AreEqual(slots, StairStyles.Parts((StairStyle)i, railing).Length, $"{(StairStyle)i}: a part per slot");
            }
        }
    }
}
