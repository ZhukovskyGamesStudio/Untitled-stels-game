using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Items;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>What things are made of: the surface sets of the houses, the items' surfaces, the printed covers.</summary>
    public class SurfaceTests
    {
        static StyleConfig Styles => Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles;
        static ItemVisuals Visuals => AssetDatabase.LoadAssetAtPath<ItemVisuals>("Assets/_Game/Data/ItemVisuals.asset");

        static SurfaceSet Set(string name) => AssetDatabase.LoadAssetAtPath<SurfaceSet>($"Assets/_Game/Data/Styles/Surfaces/{name}.asset");

        [Test]
        public void TheSampleHousesWearTheirSurfaces()
        {
            Assert.AreEqual(Set("Clean"), Styles.LookOf(HouseKind.Office).SurfacesFor(ModelFamily.Realistic));
            Assert.AreEqual(Set("Worn"), Styles.LookOf(HouseKind.Abandoned).SurfacesFor(ModelFamily.Realistic));
            Assert.IsNull(Styles.LookOf(HouseKind.Office).SurfacesFor(ModelFamily.Clean), "the drawn style keeps its drawn colours");
        }

        [Test]
        public void EverySurfaceHasItsRelief([Values("Clean", "Worn")] string name)
        {
            var set = Set(name);
            Assert.Greater(set.Materials.Length, 25);
            Assert.Greater(set.Parts.Length, 40);
            foreach (var (from, to) in set.Materials.Select(m => (m.From, m.To)).Concat(set.Parts.Select(p => (p.From, p.To))))
            {
                Assert.IsNotNull(from);
                Assert.IsNotNull(to, from.name);
                Assert.IsNotNull(to.GetTexture("_BumpMap"), $"{to.name}: no relief");
                Assert.IsNotNull(to.GetTexture("_MetallicGlossMap"), $"{to.name}: no sheen");
                Assert.AreEqual("Stealth/Surface", to.shader.name, to.name);
                Assert.IsNotNull(to.GetTexture("_WearMap"), $"{to.name}: no wear");
                StringAssert.DoesNotContain("rust", AssetDatabase.GetAssetPath(to.GetTexture("_BaseMap")).ToLowerInvariant(), "wear without rust");
                Assert.IsFalse(from.name is "Wall" or "Floor" or "Ceiling" && set.Materials.Any(m => m.From == from), "the slabs are stretched cubes: no texture fits them");
            }
        }

        [Test]
        public void ASurfaceStandsForOneMaterialOnly([Values("Clean", "Worn")] string name)
        {
            var set = Set(name);
            foreach (var g in set.Materials.Select(m => (m.From, m.To)).Concat(set.Parts.Select(p => (p.From, p.To))).GroupBy(m => m.To))
                Assert.AreEqual(1, g.Select(m => m.From).Distinct().Count(), $"{g.Key.name}: a change of style could not find the way back");
            foreach (var p in set.Parts)
            {
                Assert.IsTrue(set.TryGet(p.Mesh, p.From, out var to));
                Assert.AreEqual(p.To, to, "a part's own rule comes before its material's");
            }
            var m0 = set.Materials[0];
            Assert.IsTrue(set.TryGet(null, m0.From, out var any));
            Assert.AreEqual(m0.To, any);
            Assert.IsFalse(set.TryGet(null, m0.To, out _), "a surface has no surface");
        }

        [Test]
        public void TheModelsOnScreenHaveUvsForTheSurfaces()
        {
            var realistic = Styles.Meshes.First(m => m.Family == ModelFamily.Realistic);
            var own = Styles.StyleModels.Where(m => m.Style == Styles.Default && m.Variant == -1).ToArray();
            foreach (var p in Set("Clean").Parts)
            {
                var mesh = p.Mesh;
                var styled = own.Select(m => (m, i: System.Array.IndexOf(m.From, mesh))).FirstOrDefault(x => x.i >= 0);
                int k = System.Array.IndexOf(realistic.From, mesh);
                if (styled.m != null) mesh = styled.m.To[styled.i];
                else if (k >= 0) mesh = realistic.To[k];
                Assert.IsTrue(mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0), $"{AssetDatabase.GetAssetPath(mesh)} {mesh.name}");
            }
        }

        [Test]
        public void ItemsAreMadeOfSomething()
        {
            var realistic = Styles.Materials.First(m => m.Family == ModelFamily.Realistic);
            foreach (var model in Visuals.Models.Where(m => m != null && m.name != "LureSpeakerModel"))
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var m = r.sharedMaterial;
                    if (r.name.StartsWith("Eye") || m.GetTexture("_BaseMap") != null || m.IsKeywordEnabled("_EMISSION")) continue;
                    int i = System.Array.IndexOf(realistic.From, m);
                    if (m.name is "FlashlightLens" or "MapInk" or "MapPaper") continue; // glass, ink
                    Assert.GreaterOrEqual(i, 0, $"{model.name}/{r.name}: {m.name}");
                    Assert.IsNotNull(realistic.To[i].GetTexture("_BumpMap"), $"{model.name}/{r.name}: {m.name} is flat");
                }
        }

        [Test]
        public void TheStunnerIsAThingNotATintedGlowingPiece()
        {
            var model = Visuals.Models[ItemCatalog.Stun];
            Assert.IsNotNull(model);
            var parts = model.GetComponentsInChildren<MeshRenderer>(true);
            Assert.GreaterOrEqual(parts.Select(r => r.sharedMaterial).Distinct().Count(), 5, "case, rubber, steel, copper, paint, glow");
            foreach (var r in parts)
                Assert.AreEqual(r.name.StartsWith("Eye"), r.sharedMaterial.IsKeywordEnabled("_EMISSION"), r.name);
            Assert.IsTrue(ItemCatalog.HasPhysics(ItemCatalog.Stun));
        }

        [Test]
        public void BooksAndMagazinesArePrinted()
        {
            foreach (var name in new[] { "BookRed", "BookBlue", "BookGreen", "BookOchre", "BookBrown", "BookBlack", "BookCream", "BookTeal", "BookPages", "Printed" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Game/Art/Materials/Props/{name}.mat");
                Assert.IsNotNull(m.GetTexture("_BaseMap"), name);
                foreach (var set in Styles.Materials.Where(s => s.Family is ModelFamily.Realistic or ModelFamily.Derelict))
                {
                    int i = System.Array.IndexOf(set.From, m);
                    if (i >= 0) Assert.AreEqual(m.GetTexture("_BaseMap"), set.To[i].GetTexture("_BaseMap"), $"{name} in {set.Family}");
                }
            }
            foreach (int shape in new[] { (int)ItemCatalog.Book, ItemCatalog.Radio + 9 })
                Assert.IsTrue(Visuals.Models[shape].GetComponentsInChildren<MeshRenderer>(true).Any(r => r.name.StartsWith("Body") && r.sharedMaterial.GetTexture("_BaseMap") != null),
                    Visuals.Models[shape].name);
            foreach (var prop in new[] { "MagazineRack", "SideTable", "ReadingTable" })
                Assert.IsTrue(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Props/{prop}.prefab").GetComponentsInChildren<MeshRenderer>(true)
                    .Any(r => r.sharedMaterial.name == "Printed"), prop);
            var bookcase = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Props/TallBookcase.prefab").GetComponent<PropVariants>();
            Assert.IsTrue(bookcase.Own.Any(r => r.name.StartsWith("Trim")), "the model's own blank books give way to the printed ones");
        }
    }
}
