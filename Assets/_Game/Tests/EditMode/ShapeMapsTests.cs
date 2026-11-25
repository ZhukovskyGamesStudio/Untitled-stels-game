using System.IO;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// A model's own baked maps (<see cref="ShapeMaps"/>, <see cref="ShapeLibrary"/>, Tools/Blender/shapebake.py): every
    /// model that has the two pictures is in the library and its props name their slice; the meshes have the second UV
    /// set the pictures were baked over; a mesh merged of many models keeps both.
    /// </summary>
    public class ShapeMapsTests
    {
        const string Textures = "Assets/_Game/Art/Textures/Shapes";

        static ShapeLibrary Library => AssetDatabase.LoadAssetAtPath<ShapeLibrary>("Assets/_Game/Data/ShapeLibrary.asset");

        [Test]
        public void TheLibraryListsEveryBakedModelAndItsPicturesFitOneArray()
        {
            if (!Directory.Exists(Textures)) Assert.Ignore("no model has baked maps");
            var library = Library;
            Assert.IsNotNull(library, "no ShapeLibrary (build the assets)");
            var baked = Directory.GetFiles(Textures, "*_n.png").Select(f => Path.GetFileName(f).Replace("_n.png", "")).OrderBy(m => m, System.StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(baked, library.Models, "the library and the folder of pictures differ (build the assets)");
            Assert.AreEqual(library.Models.Length, library.Normals.Length);
            Assert.AreEqual(library.Models.Length, library.Masks.Length);
            foreach (var (pictures, kind) in new[] { (library.Normals, "normals"), (library.Masks, "masks") })
            {
                Assert.IsFalse(pictures.Any(p => p == null), $"a model's {kind} are missing");
                var first = pictures[0];
                foreach (var p in pictures)
                {
                    // (the game copies them into one array block for block: one size, one format)
                    Assert.AreEqual((first.width, first.height, first.format, first.mipmapCount), (p.width, p.height, p.format, p.mipmapCount), $"{p.name}: does not fit the array of {kind}");
                    Assert.IsFalse(((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(p))).sRGBTexture, $"{p.name}: numbers, not colour");
                }
            }
            Assert.AreEqual(TextureFormat.BC5, library.Normals[0].format, "the shader reads the normals' two channels");
        }

        [Test]
        public void TheLibraryKnowsTheMeshesOfEveryBakedModelAndTheyHaveTheSecondUvSet()
        {
            if (!Directory.Exists(Textures)) Assert.Ignore("no model has baked maps");
            var library = Library;
            Assert.AreEqual(library.Meshes.Length, library.Slices.Length);
            var styles = AssetDatabase.LoadAssetAtPath<Stealth.Configs.StyleConfig>("Assets/_Game/Data/Styles/Styles.asset");
            Assert.AreSame(library, styles.Shapes, "the styles' dressing does not know the library: no part would be told its maps");
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < library.Meshes.Length; i++)
            {
                var mesh = library.Meshes[i];
                Assert.IsNotNull(mesh);
                string model = library.Models[library.Slices[i]];
                StringAssert.EndsWith($"/{model}.fbx", AssetDatabase.GetAssetPath(mesh), $"{mesh.name}: another model's maps");
                Assert.AreEqual(library.Slices[i], library.SliceOf(mesh));
                seen.Add(library.Slices[i]);
                if (!ShapeMaps.Takes(mesh.name)) continue;      // (a screen, a print: never read with the maps)
                var uv = library.Channels[i] == 2 ? mesh.uv3 : mesh.uv2;
                Assert.AreEqual(mesh.vertexCount, uv.Length, $"{model}/{mesh.name}: no UV set of the maps — they would lie on it at random");
                Assert.IsTrue(uv.All(t => t.x > -0.001f && t.x < 1.001f && t.y > -0.001f && t.y < 1.001f), $"{model}/{mesh.name}: its second UV set leaves the picture");
            }
            Assert.AreEqual(library.Models.Length, seen.Count, "maps baked for a model whose file is not there");
        }

        [Test]
        public void APartIsToldTheMapsOfTheMeshItShowsAndForgetsThemWhenItShowsAnother()
        {
            if (!Directory.Exists(Textures)) Assert.Ignore("no model has baked maps");
            var library = Library;
            var baked = library.Meshes.First(m => ShapeMaps.Takes(m.name));
            var go = new GameObject("Body");
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            var surface = new Material(Shader.Find("Stealth/Surface"));
            var other = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.sharedMaterials = new[] { surface, other };
            filter.sharedMesh = baked;
            var block = new MaterialPropertyBlock();
            int sliceId = Shader.PropertyToID("_ShapeSlice");
            ShapeMaps.Tag(renderer, library);
            Assert.AreEqual(library.SliceOf(baked), ShapeMaps.SliceOf(renderer));
            renderer.GetPropertyBlock(block, 0);
            Assert.AreEqual(library.SliceOf(baked), (int)block.GetFloat(sliceId), "its slice travels in the block of the material that reads it while it is drawn alone");
            renderer.GetPropertyBlock(block, 1);
            Assert.IsTrue(block.isEmpty, "a material that does not read the maps is told nothing");
            Assert.IsFalse(MeshBatcher.DrawsItself(renderer), "the renderer's own block is not ours to take: it is the sign of a part that draws itself");
            var plain = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
            filter.sharedMesh = plain;                           // (the style changed: another variant of the model, without maps)
            ShapeMaps.Tag(renderer, library);
            Assert.AreEqual(-1, ShapeMaps.SliceOf(renderer));
            renderer.GetPropertyBlock(block, 0);
            Assert.IsTrue(block.isEmpty, "the maps of the model it showed before were left on it");
            go.name = "EyeScreen";
            filter.sharedMesh = baked;
            ShapeMaps.Tag(renderer, library);
            Assert.AreEqual(-1, ShapeMaps.SliceOf(renderer), "what glows shows nothing of its shape");
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(plain);
            Object.DestroyImmediate(surface);
            Object.DestroyImmediate(other);
        }

        [Test]
        public void APartThatDrawsItselfStaysOutOfTheMergeWithItsMapsOrWithout()
        {
            // the play of 2026-11-25: with the slice in the renderer's own block, a block "that only names the maps" was
            // let into the merge — and so was every drawer, cabinet door and lamp shade whose block said something else
            if (!Directory.Exists(Textures)) Assert.Ignore("no model has baked maps");
            var library = Library;
            var baked = library.Meshes.First(m => ShapeMaps.Takes(m.name) && MeshBatcher.CanMerge(m));
            var go = new GameObject("BodyDrawer0");
            go.AddComponent<MeshFilter>().sharedMesh = baked;
            var renderer = go.AddComponent<MeshRenderer>();
            var surface = new Material(Shader.Find("Stealth/Surface"));
            renderer.sharedMaterials = Enumerable.Repeat(surface, baked.subMeshCount).ToArray();
            ShapeMaps.Tag(renderer, library);
            Assert.IsTrue(MeshBatcher.CanMerge(renderer, out _), "a still part with maps merges with the furniture round it");
            var block = new MaterialPropertyBlock();
            block.SetFloat("_StorageMoving", 1f);                 // (what Items.Storage puts on its drawers and doors)
            renderer.SetPropertyBlock(block);
            Assert.IsFalse(MeshBatcher.CanMerge(renderer, out _), "a part that moves was merged into the room: it would stand still while the real one moves unseen");
            ShapeMaps.Tag(renderer, library);                    // (the style is applied again)
            Assert.IsFalse(MeshBatcher.CanMerge(renderer, out _), "…after its maps were named again");
            renderer.GetPropertyBlock(block, 0);
            Assert.AreEqual(1f, block.GetFloat("_StorageMoving"), "the material's block stands in place of the renderer's: what that one said is carried along");
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(surface);
        }

        [Test]
        public void TheMovingPartsOfFurnitureThatOpensDrawThemselves([Values("FilePedestal", "FilingCabinet", "Credenza", "Lockers", "Cabinet")] string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Props/{name}.prefab");
            Assert.IsNotNull(prefab, name);
            var go = Object.Instantiate(prefab, new Vector3(960f, 0f, 960f), Quaternion.identity);
            try
            {
                var storage = go.GetComponent<Stealth.Items.Storage>();
                Assert.IsNotNull(storage, $"{name} does not open");
                storage.Configure(0);
                ShapeMaps.Tag(go, Library);
                int moving = 0;
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                {
                    bool part = false;
                    for (var t = r.transform; t != null && t != go.transform; t = t.parent)
                        part |= t.name.Contains("Door") || t.name.Contains("Drawer");
                    if (!part) continue;
                    moving++;
                    Assert.IsFalse(MeshBatcher.CanMerge(r, out _), $"{name}/{r.name}: a door or a drawer in the merged furniture stands shut while the real one opens");
                }
                Assert.Greater(moving, 0, $"{name}: no doors and no drawers found");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AMergedMeshKeepsEveryModelsMapsApart()
        {
            // two quads, one with maps (slice 3) and one without, merged: each vertex says whose pictures it reads
            Mesh Quad(bool second)
            {
                var m = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, new Vector3(1, 1, 0) }, triangles = new[] { 0, 2, 1, 1, 2, 3 } };
                m.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
                if (second) m.uv2 = new[] { new Vector2(0.1f, 0.1f), new Vector2(0.2f, 0.1f), new Vector2(0.1f, 0.2f), new Vector2(0.2f, 0.2f) };
                m.RecalculateNormals();
                return m;
            }
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var batcher = new MeshBatcher(Matrix4x4.identity);
            var look = default(BatchLook);
            var a = Quad(true);
            var b = Quad(false);
            batcher.Add(Vector3Int.zero, look, material, a, 0, Matrix4x4.identity, null, null, 3);
            batcher.Add(Vector3Int.zero, look, material, b, 0, Matrix4x4.Translate(Vector3.right * 2f));
            batcher.Add(Vector3Int.zero, look, material, a, 0, Matrix4x4.Scale(new Vector3(-1f, 1f, 1f)), null, null, 3);
            var baked = batcher.Bake();
            Assert.AreEqual(1, baked.Count);
            var mesh = baked[0].Mesh;
            var ids = new System.Collections.Generic.List<Vector2>();
            mesh.GetUVs(3, ids);
            Assert.AreEqual(12, ids.Count, "every vertex names its maps");
            for (int i = 0; i < 4; i++) Assert.AreEqual(new Vector2(4f, 1f), ids[i], "slice 3, as it stands");
            for (int i = 4; i < 8; i++) Assert.AreEqual(Vector2.zero, ids[i], "no maps of its own");
            for (int i = 8; i < 12; i++) Assert.AreEqual(new Vector2(4f, -1f), ids[i], "slice 3, mirrored: its frame turns over");
            Assert.AreEqual(new Vector2(0.2f, 0.2f), mesh.uv2[3]);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        [Test]
        public void TheSurfaceShaderReadsThem()
        {
            var shader = Shader.Find("Stealth/Surface");
            Assert.IsNotNull(shader);
            Assert.GreaterOrEqual(shader.FindPropertyIndex("_ShapeSlice"), 0, "Stealth/Surface has no _ShapeSlice");
            var material = new Material(shader);
            Assert.AreEqual(-1f, material.GetFloat("_ShapeSlice"), "a model without maps of its own is shaded as before");
            Object.DestroyImmediate(material);
        }
    }
}
