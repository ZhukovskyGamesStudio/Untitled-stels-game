using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Holes knocked through walls: broken brickwork in several variants, the plaster round them dressed as wall.</summary>
    public class RuinTests
    {
        static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Level/{name}.prefab");

        [Test]
        public void AHoleInAWallBrokeOneOfSeveralWays([Values("BreachFrame", "HoleFrame")] string name)
        {
            var go = Object.Instantiate(Prefab(name));
            try
            {
                var ruin = go.GetComponent<RuinFrame>();
                Assert.IsNotNull(ruin, "no RuinFrame");
                Assert.GreaterOrEqual(ruin.Variants.Length, 3);
                Assert.AreEqual(ruin.Variants.Length * 2, ruin.Skins.Length);
                var seen = new HashSet<int>();
                for (int seed = 1; seed < 40; seed++)
                {
                    var parts = new List<MeshRenderer>();
                    ruin.Show(LevelRandom.Mix(77, Salt.Ruins, seed), parts);
                    Assert.AreEqual(1, ruin.Variants.Count(v => v.activeSelf), "one way at a time");
                    int pick = System.Array.FindIndex(ruin.Variants, v => v.activeSelf);
                    seen.Add(pick);
                    Assert.AreEqual(2, parts.Count, "a skin on each face");
                    Assert.IsTrue(parts.All(p => p.transform.IsChildOf(ruin.Variants[pick].transform)), "the shown variant's own skins");
                    Assert.Less(Vector3.Dot(parts[0].transform.forward, parts[1].transform.forward), -0.99f, "they look out of the wall's two faces");
                }
                Assert.AreEqual(ruin.Variants.Length, seen.Count, "every variant turns up");
                Assert.IsEmpty(go.GetComponentsInChildren<Collider>(true), "decor: the wall pieces keep the shape");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheBrokenWallIsBrickMortarAndPlasterAndStaysInItsOpening([Values("BreachFrame", "HoleFrame")] string name)
        {
            bool hole = name == "HoleFrame";
            float w = LevelMetrics.ClearWidth(hole ? LevelMetrics.HoleWidth : LevelMetrics.BreachWidth), h = hole ? LevelMetrics.HoleHeight : LevelMetrics.DoorHeight;
            var ruin = Prefab(name).GetComponent<RuinFrame>();
            var meshes = new HashSet<Mesh>();
            foreach (var v in ruin.Variants)
            {
                var parts = v.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var material in new[] { "Ruin_Brick", "Ruin_Mortar", "Ruin_Plaster" })
                    Assert.IsTrue(parts.Any(r => r.sharedMaterial != null && r.sharedMaterial.name == material), $"{v.name}: no {material}");
                var bricks = parts.First(r => r.name.StartsWith("Brick")).GetComponent<MeshFilter>().sharedMesh;
                Assert.IsTrue(meshes.Add(bricks), "two variants are the same model");
                Assert.Greater(bricks.triangles.Length / 3, 1500, "brick by brick, not a few lumps");
                // nothing stands in the way: the bricks keep to the jambs and the head (the heaps lie at the sides, low)
                foreach (var p in bricks.vertices)
                {
                    var q = parts.First(r => r.name.StartsWith("Brick")).transform.TransformPoint(p) - v.transform.root.position;
                    if (q.y < 0.45f) continue;
                    Assert.IsTrue(Mathf.Abs(q.x) > w / 2f - 0.16f || q.y > h - 0.34f, $"{v.name}: a brick in the opening at {q}");
                    Assert.Less(Mathf.Abs(q.z), LevelMetrics.WallThickness / 2f + 0.03f, $"{v.name}: a brick standing out of the wall's face at {q}");
                }
            }
            // the plaster skin: a strip round the opening, torn into the gap by no more than a hand's width
            foreach (var skin in ruin.Skins)
            {
                var mesh = skin.GetComponent<MeshFilter>().sharedMesh;
                Assert.IsTrue(mesh.isReadable, "the style merges it into the wall finish");
                var b = mesh.bounds;
                Assert.AreEqual(w, b.size.x, 0.001f);
                Assert.AreEqual(h, b.max.y, 0.001f);
                Assert.AreEqual(0f, b.max.z, 0.0001f, "in the wall's face");
                foreach (var p in mesh.vertices)
                    Assert.IsTrue(Mathf.Abs(p.x) > w / 2f - 0.09f || p.y > h - 0.09f, $"plaster hanging in the opening at {p}");
            }
        }
    }
}
