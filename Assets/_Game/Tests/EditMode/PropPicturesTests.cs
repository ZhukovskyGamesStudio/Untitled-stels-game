using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>What differs from piece to piece: boards' writing by whose house it is, mirrors, projector screens, the trolley's books, the globe.</summary>
    public class PropPicturesTests
    {
        static LevelArt Art => AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");

        static List<int> Cells(PropKind kind, HouseKind house)
        {
            var list = new List<int>();
            for (int i = 0; i < 1500; i++)
            {
                Assert.IsTrue(PropPictures.TryPick(kind, house, LevelRandom.Mix(4242, Salt.Pictures, i), out var pick), $"{kind} in {house}");
                Assert.That(pick.Faces.Cell, Is.InRange(0, pick.Faces.Cols * pick.Faces.Rows - 1));
                list.Add(pick.Faces.Cell);
            }
            return list;
        }

        [Test]
        public void ABoardShowsWhatItsHouseIsAboutOrIsWiped()
        {
            foreach (HouseKind house in System.Enum.GetValues(typeof(HouseKind)))
                foreach (var kind in new[] { PropKind.Whiteboard, PropKind.MobileWhiteboard })
                {
                    var cells = Cells(kind, house);
                    var written = PropPictures.Writings(house);
                    foreach (int c in written) Assert.Contains(c, cells, $"{house}");
                    var wiped = cells.Where(c => !written.Contains(c)).ToList();
                    Assert.IsTrue(wiped.All(c => c <= PropPictures.Washed + 1), $"{house}: nothing of another house's");
                    Assert.That(wiped.Count / (float)cells.Count, Is.InRange(0.22f, 0.42f), $"{house}: some are wiped or blank");
                }
            CollectionAssert.Contains(PropPictures.Writings(HouseKind.Abandoned), PropPictures.Formulas);
            CollectionAssert.Contains(PropPictures.Writings(HouseKind.Garage), PropPictures.Chart);
            CollectionAssert.Contains(PropPictures.Writings(HouseKind.Office), PropPictures.Code);
            CollectionAssert.Contains(PropPictures.Writings(HouseKind.Office), PropPictures.Jokes);
        }

        [Test]
        public void MirrorsAndProjectorScreensComeWholeDirtyAndBroken()
        {
            foreach (var kind in new[] { PropKind.WashBasin, PropKind.MirrorSink, PropKind.DoubleSink })
            {
                var kept = Cells(kind, HouseKind.Office);
                var left = Cells(kind, HouseKind.Abandoned);
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, kept.Distinct());
                Assert.Greater(left.Count(c => c > 0), kept.Count(c => c > 0), "worse where nobody cleans");
            }
            var screens = Cells(PropKind.ProjectorScreen, HouseKind.Office);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4 }, screens.Distinct());
            Assert.Greater(Cells(PropKind.ProjectorScreen, HouseKind.Abandoned).Count(c => c > 0), screens.Count(c => c > 0));
            Assert.IsFalse(PropPictures.TryPick(PropKind.Sofa, HouseKind.Office, 1, out _));
        }

        [Test]
        public void EveryPictureLiesOnAFaceOfItsModel()
        {
            Assert.AreEqual(System.Enum.GetValues(typeof(PictureKind)).Length, Art.Pictures.Length);
            foreach (var m in Art.Pictures) Assert.IsNotNull(m.GetTexture("_BaseMap"), m.name);
            Assert.IsTrue(Art.Pictures[(int)PictureKind.Projector].IsKeywordEnabled("_ALPHATEST_ON"), "one sees through a torn screen");
            foreach (PropKind kind in System.Enum.GetValues(typeof(PropKind)))
            {
                if (!PropPictures.TryPick(kind, HouseKind.Office, 4, out var pick)) continue;
                var parts = Art.Prop(kind).GetComponentsInChildren<MeshRenderer>(true)
                    .Where(r => pick.Part != null ? r.name.StartsWith(pick.Part) : r.sharedMaterial == Art.Mirror).ToArray();
                Assert.IsNotEmpty(parts, $"{kind}: nothing to show it on");
                float w = 1f / pick.Faces.Cols, h = 1f / pick.Faces.Rows;
                var cell = new Rect(pick.Faces.Cell % pick.Faces.Cols * w, 1f - (pick.Faces.Cell / pick.Faces.Cols + 1) * h, w, h);
                bool whole = parts.Any(r =>
                {
                    var uv = ScreenFaces.Of(r.GetComponent<MeshFilter>().sharedMesh, pick.Faces).uv.Where(u => cell.Contains(u)).ToArray();
                    return uv.Length >= 4 && uv.Max(u => u.x) - uv.Min(u => u.x) > 0.9f * w && uv.Max(u => u.y) - uv.Min(u => u.y) > 0.9f * h;
                });
                Assert.IsTrue(whole, $"{kind}: its picture is not whole on any face");
            }
        }

        [Test]
        public void TheTrolleysBooksAreManyKindsAndABurntOneStaysBurnt()
        {
            var go = Object.Instantiate(Art.Prop(PropKind.BookCart));
            try
            {
                var v = go.GetComponent<PropVariants>();
                Assert.AreEqual(6, v.Slots.Length, "both sides of three shelves");
                for (int seed = 1; seed <= 20; seed++) v.Dress(seed);
                var burnt = go.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Fixed")).ToArray();
                Assert.IsNotEmpty(burnt);
                Assert.IsTrue(burnt.All(r => r.sharedMaterial.name == "BookCharred"), "dressing the trolley painted a burnt book");
                Assert.Less(burnt.Length, go.GetComponentsInChildren<MeshRenderer>(true).Length / 3, "a fire is rare");
                Assert.IsTrue(v.Own.All(r => r != null && !r.enabled), "the model's own plain books step aside");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheGlobeWearsTheMapOnce()
        {
            var ball = Art.Prop(PropKind.Globe).GetComponentsInChildren<MeshRenderer>(true).First(r => r.name.StartsWith("Trim"));
            Assert.AreEqual("GlobeMap", ball.sharedMaterial.name);
            Assert.IsNotNull(ball.sharedMaterial.GetTexture("_BaseMap"));
            var mesh = ball.GetComponent<MeshFilter>().sharedMesh;
            var uv = mesh.uv;
            var t = mesh.triangles;
            Assert.Greater(uv.Max(u => u.x) - uv.Min(u => u.x), 0.9f);
            for (int i = 0; i < t.Length; i += 3)
                Assert.Less(Mathf.Max(uv[t[i]].x, uv[t[i + 1]].x, uv[t[i + 2]].x) - Mathf.Min(uv[t[i]].x, uv[t[i + 1]].x, uv[t[i + 2]].x), 0.55f, "a face with the whole world squeezed on it (the seam)");
        }

        [Test]
        public void ShelvesHoldThingsOfTheirOwnLook([Values("MetalShelving", "SupplyShelf", "ComponentShelf", "PaintRack", "CashShelf")] string name)
        {
            var print = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Props/{name}.prefab").GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Print")).ToArray();
            Assert.IsNotEmpty(print);
            foreach (var r in print)
            {
                Assert.AreEqual("MachinePrint", r.sharedMaterial.name);
                var uv = r.GetComponent<MeshFilter>().sharedMesh.uv;
                Assert.Greater(uv.Select(u => (Mathf.FloorToInt(u.x * 16f), Mathf.FloorToInt(u.y * 16f))).Distinct().Count(), 3, "labels and colours");
            }
        }
    }
}
