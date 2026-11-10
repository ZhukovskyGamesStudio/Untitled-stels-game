using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Merging the static geometry of the house must not change what is drawn.</summary>
    public class MeshBatcherTests
    {
        static Mesh Triangle()
        {
            var m = new Mesh();
            m.SetVertices(new[] { new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 0, 0) });
            m.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back });
            m.SetTangents(new[] { new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1) });
            m.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0) });
            m.SetTriangles(new[] { 0, 1, 2 }, 0);
            return m;
        }

        static Material Lit() => new(Shader.Find("Universal Render Pipeline/Lit"));

        static readonly BatchLook Look = new() { Shadows = UnityEngine.Rendering.ShadowCastingMode.On, ReceiveShadows = true };

        [Test]
        public void PartsAreBakedWhereTheyWouldBeDrawn()
        {
            var mesh = Triangle();
            var mat = Lit();
            var b = new MeshBatcher(Matrix4x4.identity);
            var place = Matrix4x4.TRS(new Vector3(3, 1, 2), Quaternion.Euler(0, 90, 0), new Vector3(2, 1, 1));
            b.Add(MeshBatcher.ChunkOf(new Vector3(3, 1, 2)), Look, mat, mesh, 0, place);
            b.Add(MeshBatcher.ChunkOf(new Vector3(4, 1, 2)), Look, mat, mesh, 0, Matrix4x4.Translate(new Vector3(4, 1, 2)));
            var baked = b.Bake();
            Assert.AreEqual(1, baked.Count, "same room cell, same material: one mesh");
            var v = baked[0].Mesh.vertices;
            Assert.AreEqual(6, v.Length);
            for (int i = 0; i < 3; i++)
                Assert.Less((v[i] - place.MultiplyPoint3x4(mesh.vertices[i])).magnitude, 1e-5f);
            var n = baked[0].Mesh.normals[0];
            Assert.Less((n - (Quaternion.Euler(0, 90, 0) * Vector3.back)).magnitude, 1e-5f, "normals turn with the part");
        }

        [Test]
        public void RoomCellsAndMaterialsSplitTheBatches()
        {
            var mesh = Triangle();
            Material a = Lit(), c = Lit();
            var b = new MeshBatcher(Matrix4x4.identity);
            b.Add(MeshBatcher.ChunkOf(new Vector3(1, 0, 1)), Look, a, mesh, 0, Matrix4x4.identity);
            b.Add(MeshBatcher.ChunkOf(new Vector3(1, 0, 1)), Look, c, mesh, 0, Matrix4x4.identity);
            b.Add(MeshBatcher.ChunkOf(new Vector3(15, 0, 1)), Look, a, mesh, 0, Matrix4x4.identity);
            b.Add(MeshBatcher.ChunkOf(new Vector3(1, LevelMetrics.FloorHeight, 1)), Look, a, mesh, 0, Matrix4x4.identity);
            var noShadows = Look;
            noShadows.Shadows = UnityEngine.Rendering.ShadowCastingMode.Off;
            b.Add(MeshBatcher.ChunkOf(new Vector3(1, 0, 1)), noShadows, a, mesh, 0, Matrix4x4.identity);
            var baked = b.Bake();
            Assert.AreEqual(4, baked.Count, "a mesh per room cell, floor and way of drawing");
            var first = baked.First(x => x.Chunk == MeshBatcher.ChunkOf(new Vector3(1, 0, 1)) && x.Look.Equals(Look));
            Assert.AreEqual(2, first.Mesh.subMeshCount, "a submesh per material");
            CollectionAssert.AreEqual(new[] { a, c }, first.Materials);
        }

        [Test]
        public void AMirroredPartKeepsItsFrontFace()
        {
            var mesh = Triangle();
            var b = new MeshBatcher(Matrix4x4.identity);
            b.Add(Vector3Int.zero, Look, Lit(), mesh, 0, Matrix4x4.Scale(new Vector3(-1, 1, 1)));
            var baked = b.Bake()[0].Mesh;
            var v = baked.vertices;
            var t = baked.triangles;
            var face = Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]);
            var src = mesh.vertices;
            var srcFace = Vector3.Cross(src[1] - src[0], src[2] - src[0]);
            Assert.AreEqual(Mathf.Sign(Vector3.Dot(srcFace, mesh.normals[0])), Mathf.Sign(Vector3.Dot(face, baked.normals[0])),
                "the winding is flipped with the mirror, so the same side faces the camera");
            Assert.AreEqual(1f, baked.tangents[0].w, 1e-6f, "the bitangent sign flips like Unity does for a mirrored object");
        }

        [Test]
        public void TheTilingOfAPartGoesIntoItsUvs()
        {
            var mesh = Triangle();
            var mat = Lit();
            mat.SetVector("_BaseMap_ST", new Vector4(2f, 4f, 1f, 0.5f));
            var tiling = new Vector4(3f, 5f, 10f, -2f);
            var b = new MeshBatcher(Matrix4x4.identity);
            b.Add(Vector3Int.zero, Look, mat, mesh, 0, Matrix4x4.identity, tiling);
            var uv = b.Bake()[0].Mesh.uv;
            var src = mesh.uv;
            for (int i = 0; i < 3; i++)
            {
                // what the shader samples: uv * material tiling + offset, as the property block made it before
                var drawn = new Vector2(uv[i].x * 2f + 1f, uv[i].y * 4f + 0.5f);
                var before = new Vector2(src[i].x * tiling.x + tiling.z, src[i].y * tiling.y + tiling.w);
                Assert.Less((drawn - before).magnitude, 1e-4f);
            }
        }
    }
}
