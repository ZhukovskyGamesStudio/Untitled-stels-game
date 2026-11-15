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
        public void TheDefaultStyleHasWoodenStairsDressedInItsOwnFinishes()
        {
            var config = AssetDatabase.LoadAssetAtPath<StyleConfig>("Assets/_Game/Data/Styles/Styles.asset");
            var g = config.Default.Geometry;
            Assert.AreEqual(StairChoice.Wood, g.Stairs, "painted wood with balusters, like the panelled rooms around it");
            Assert.IsNotNull(g.Floor, "its treads take the floor finish");
            Assert.IsNotNull(g.Wall, "its body the wall finish");
            Assert.IsNotNull(g.Trim, "its balusters the trim");
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
