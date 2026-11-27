using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stealth.Level
{
    /// <summary>How a merged renderer draws: parts are merged only with parts that draw the same way.</summary>
    public struct BatchLook : System.IEquatable<BatchLook>
    {
        public int Layer;
        public ShadowCastingMode Shadows;
        public bool ReceiveShadows;
        public uint RenderingLayers;

        public static BatchLook Of(Renderer r) => new()
        {
            Layer = r.gameObject.layer, Shadows = r.shadowCastingMode, ReceiveShadows = r.receiveShadows, RenderingLayers = r.renderingLayerMask,
        };

        public bool Equals(BatchLook o) => Layer == o.Layer && Shadows == o.Shadows && ReceiveShadows == o.ReceiveShadows && RenderingLayers == o.RenderingLayers;
        public override bool Equals(object obj) => obj is BatchLook o && Equals(o);
        public override int GetHashCode() => (Layer * 397 ^ (int)Shadows * 31 ^ (ReceiveShadows ? 17 : 0)) ^ (int)RenderingLayers;
    }

    /// <summary>
    /// UVs projected from the world like the wall finish quads (<see cref="ArchitecturePlanner"/>): along the wall by
    /// <see cref="Right"/>, up by the height over the floor, one repeat every <see cref="Tile"/> metres. A part dressed in
    /// the wall's finish this way continues its texture without a seam (the spandrels of an arch). Faces turned away from
    /// the wall (an arch's soffit) project along the axes they face, across the wall by <see cref="Across"/> (the same from
    /// both faces, as the doorway's reveals: <see cref="ArchitecturePlanner.Across"/>).
    /// </summary>
    public struct WallProjection
    {
        public Vector3 Right, Normal, Across;
        public float Tile, FloorY;
    }

    /// <summary>One merged mesh: a submesh per material, drawn the way <see cref="Look"/> says.</summary>
    public struct BakedBatch
    {
        /// <summary>Its vertices name the models' own baked maps (<see cref="ShapeMaps"/>): the renderer must say so to the shader.</summary>
        public bool Shaped;
        public Mesh Mesh;
        public Material[] Materials;
        public BatchLook Look;
        public Vector3Int Chunk;
    }

    /// <summary>
    /// Merges the static geometry of the house (style architecture, furniture) into a few big meshes: one per room cell
    /// and floor (so frustum culling still drops what is out of view) with one submesh per material. Thousands of small
    /// renderers cost far more CPU than their triangles; merged, a room is a handful of draws. Every part is baked exactly
    /// as Unity draws it — vertices, normals and tangents through the part's matrix (inverse transpose for normals,
    /// winding and bitangent sign flipped under a mirroring scale), texture tiling folded into the UVs — so the picture
    /// does not change. Only what draws the same way is merged: <see cref="BatchLook"/> (layer, shadows) and the material.
    /// </summary>
    public sealed class MeshBatcher
    {
        struct Part
        {
            public Mesh Mesh;
            public int SubMesh;
            public Matrix4x4 Matrix;
            public bool Tiled;
            public Vector4 Tiling;
            public bool Projected;
            public WallProjection Projection;
            public Matrix4x4 World;
            /// <summary>The slice of the model's own baked maps (<see cref="ShapeMaps"/>), −1: none; and the UV set of the mesh they lie over.</summary>
            public int Slice, ShapeChannel;
        }

        sealed class Source
        {
            public Vector3[] Vertices, Normals;
            public Vector4[] Tangents;
            public Vector2[] Uv, Uv2, Uv3;
            public int[][] Indices;
            /// <summary>Per submesh: the vertices its triangles use (first, count).</summary>
            public (int first, int count)[] Ranges;
        }

        readonly Matrix4x4 m_ToRoot;
        readonly Dictionary<(Vector3Int chunk, BatchLook look), Dictionary<Material, List<Part>>> m_Groups = new();
        readonly List<(Vector3Int chunk, BatchLook look)> m_Order = new();
        readonly Dictionary<Mesh, Source> m_Sources = new();
        static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

        /// <param name="toRoot">World → space of the object the merged meshes will hang under.</param>
        public MeshBatcher(Matrix4x4 toRoot) => m_ToRoot = toRoot;

        /// <summary>Parts added so far.</summary>
        public int Parts { get; private set; }

        /// <summary>The room cell and floor a point belongs to (merged meshes are cut along these).</summary>
        public static Vector3Int ChunkOf(Vector3 world) =>
            new(Mathf.FloorToInt(world.x / LevelMetrics.Cell), LevelLayout.FloorOf(world.y), Mathf.FloorToInt(world.z / LevelMetrics.Cell));

        /// <summary>A mesh can be merged: readable from script and made of triangles.</summary>
        public static bool CanMerge(Mesh mesh)
        {
            if (mesh == null || !mesh.isReadable) return false;
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetTopology(i) != MeshTopology.Triangles) return false;
            return true;
        }

        /// <summary>
        /// The renderer has a property block of its own: it draws itself (a drawer that moves, a shade that glows by its
        /// bulb, a lamp that follows the alarm). The blocks on its materials are another matter — there a model's own baked
        /// maps are named (<see cref="ShapeMaps"/>), and the merged mesh carries those in its vertices.
        /// </summary>
        public static bool DrawsItself(Renderer r)
        {
            if (!r.HasPropertyBlock()) return false;
            r.GetPropertyBlock(s_Probe);
            return !s_Probe.isEmpty;
        }

        static readonly MaterialPropertyBlock s_Probe = new();

        /// <summary>A renderer as it is drawn now can be merged (its mesh can, no material is see-through, nothing animates it).</summary>
        public static bool CanMerge(MeshRenderer r, out Mesh mesh)
        {
            mesh = null;
            if (r == null || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy || r.isPartOfStaticBatch || DrawsItself(r)) return false;
            if (!r.TryGetComponent<MeshFilter>(out var mf) || !CanMerge(mf.sharedMesh)) return false;
            foreach (var m in r.sharedMaterials)
                if (m == null || m.renderQueue >= (int)RenderQueue.Transparent) return false;
            mesh = mf.sharedMesh;
            return true;
        }

        /// <summary>Adds one submesh of <paramref name="mesh"/> placed by <paramref name="world"/>.</summary>
        /// <param name="tiling">Texture tiling of this part (as a _BaseMap_ST); folded into the UVs against the material's own.</param>
        /// <param name="projection">UVs from the world like the wall finish (instead of the mesh's own).</param>
        public void Add(Vector3Int chunk, BatchLook look, Material material, Mesh mesh, int subMesh, Matrix4x4 world, Vector4? tiling = null,
            WallProjection? projection = null, int slice = -1, int shapeChannel = 1)
        {
            if (material == null || mesh == null || Mathf.Abs(world.determinant) < 1e-12f) return;
            var key = (chunk, look);
            if (!m_Groups.TryGetValue(key, out var byMaterial))
            {
                m_Groups[key] = byMaterial = new Dictionary<Material, List<Part>>();
                m_Order.Add(key);
            }
            if (!byMaterial.TryGetValue(material, out var parts)) byMaterial[material] = parts = new List<Part>();
            parts.Add(new Part
            {
                Mesh = mesh, SubMesh = Mathf.Clamp(subMesh, 0, mesh.subMeshCount - 1), Matrix = m_ToRoot * world,
                Tiled = tiling.HasValue, Tiling = tiling ?? default,
                Projected = projection.HasValue, Projection = projection ?? default, World = world, Slice = slice, ShapeChannel = shapeChannel,
            });
            Parts++;
        }

        /// <summary>Adds a renderer the way it draws now (every material on its submesh); see <see cref="CanMerge(MeshRenderer, out Mesh)"/>.</summary>
        public void Add(MeshRenderer r, Mesh mesh) => Add(r, mesh, ChunkOf(r.bounds.center));

        /// <summary>The same into a group the caller names (<paramref name="chunk"/> is only its key).</summary>
        public void Add(MeshRenderer r, Mesh mesh, Vector3Int chunk)
        {
            var mats = r.sharedMaterials;
            var look = BatchLook.Of(r);
            var m = r.transform.localToWorldMatrix;
            int slice = ShapeMaps.SliceOf(r), channel = ShapeMaps.ChannelOf(r);
            for (int i = 0; i < mats.Length; i++) Add(chunk, look, mats[i], mesh, i, m, null, null, slice, channel);
        }

        /// <summary>The merged meshes (the caller owns them and destroys them with the level).</summary>
        public List<BakedBatch> Bake()
        {
            var result = new List<BakedBatch>();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uv = new List<Vector2>();
            var uv2 = new List<Vector2>();   // the models' own maps: where each vertex lies on its model's pictures…
            var shape = new List<Vector2>(); // …and which pictures (x: slice + 1, 0 none; y: ±2, −2 where the part is mirrored)
            foreach (var key in m_Order)
            {
                vertices.Clear(); normals.Clear(); tangents.Clear(); uv.Clear(); uv2.Clear(); shape.Clear();
                bool shaped = false;
                var subMeshes = new List<List<int>>();
                var materials = new List<Material>();
                foreach (var (material, parts) in m_Groups[key])
                {
                    var indices = new List<int>();
                    var materialTiling = material.HasProperty(BaseMapSt) ? material.GetVector(BaseMapSt) : new Vector4(1, 1, 0, 0);
                    foreach (var p in parts)
                    {
                        Append(p, materialTiling, vertices, normals, tangents, uv, indices);
                        shaped |= AppendShape(p, uv2, shape);
                    }
                    if (indices.Count == 0) continue;
                    subMeshes.Add(indices);
                    materials.Add(material);
                }
                if (subMeshes.Count == 0) continue;
                var mesh = new Mesh
                {
                    name = $"Batch {key.chunk}",
                    indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetTangents(tangents);
                mesh.SetUVs(0, uv);
                if (shaped)
                {
                    mesh.SetUVs(1, uv2);
                    mesh.SetUVs(3, shape);
                }
                mesh.subMeshCount = subMeshes.Count;
                for (int i = 0; i < subMeshes.Count; i++) mesh.SetTriangles(subMeshes[i], i, false);
                mesh.RecalculateBounds();
                result.Add(new BakedBatch { Mesh = mesh, Materials = materials.ToArray(), Look = key.look, Chunk = key.chunk, Shaped = shaped });
            }
            m_Sources.Clear();
            return result;
        }

        /// <summary>Puts the merged meshes on screen: one <paramref name="holder"/> (MeshFilter + MeshRenderer) each, under <paramref name="parent"/>.</summary>
        public static void Spawn(List<BakedBatch> batches, GameObject holder, Transform parent, string name, List<GameObject> objects)
        {
            foreach (var b in batches)
            {
                b.Mesh.UploadMeshData(true); // the CPU copy is not needed any more
                var go = Object.Instantiate(holder, parent);
                go.name = $"{name}_{b.Chunk.y}_{b.Chunk.x}_{b.Chunk.z}";
                go.layer = b.Look.Layer;
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                go.GetComponent<MeshFilter>().sharedMesh = b.Mesh;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterials = b.Materials;
                if (b.Shaped) ShapeMaps.MarkMerged(r);
                r.shadowCastingMode = b.Look.Shadows;
                r.receiveShadows = b.Look.ReceiveShadows;
                r.renderingLayerMask = b.Look.RenderingLayers;
                objects.Add(go);
            }
        }

        void Append(Part p, Vector4 materialTiling, List<Vector3> vertices, List<Vector3> normals, List<Vector4> tangents, List<Vector2> uv, List<int> indices)
        {
            var s = SourceOf(p.Mesh);
            var (first, count) = s.Ranges[p.SubMesh];
            var m = p.Matrix;
            var n = m.inverse.transpose;
            bool mirrored = m.determinant < 0f;
            int start = vertices.Count - first;
            // UVs: the shader scales them by the material's tiling; the part's own tiling (a property block before) goes in first.
            float sx = Mathf.Abs(materialTiling.x) > 1e-6f ? materialTiling.x : 1f, sy = Mathf.Abs(materialTiling.y) > 1e-6f ? materialTiling.y : 1f;
            for (int i = first; i < first + count; i++)
            {
                vertices.Add(m.MultiplyPoint3x4(s.Vertices[i]));
                normals.Add(s.Normals != null ? n.MultiplyVector(s.Normals[i]).normalized : Vector3.up);
                if (s.Tangents != null)
                {
                    var t = s.Tangents[i];
                    var dir = m.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
                    tangents.Add(new Vector4(dir.x, dir.y, dir.z, mirrored ? -t.w : t.w));
                }
                else tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                var c = s.Uv != null ? s.Uv[i] : Vector2.zero;
                if (p.Projected)
                {
                    var w = p.World.MultiplyPoint3x4(s.Vertices[i]);
                    var wn = s.Normals != null ? p.World.inverse.transpose.MultiplyVector(s.Normals[i]) : p.Projection.Normal;
                    var want = Project(p.Projection, w, wn);
                    c = new Vector2((want.x - materialTiling.z) / sx, (want.y - materialTiling.w) / sy);
                }
                else if (p.Tiled)
                    c = new Vector2((c.x * p.Tiling.x + p.Tiling.z - materialTiling.z) / sx, (c.y * p.Tiling.y + p.Tiling.w - materialTiling.w) / sy);
                uv.Add(c);
            }
            var src = s.Indices[p.SubMesh];
            for (int i = 0; i + 2 < src.Length; i += 3)
            {
                indices.Add(start + src[i]);
                if (mirrored) { indices.Add(start + src[i + 2]); indices.Add(start + src[i + 1]); }
                else { indices.Add(start + src[i + 1]); indices.Add(start + src[i + 2]); }
            }
        }

        /// <summary>The part's second UV set and its maps' slice, vertex for vertex with <see cref="Append"/>; false: it has no maps of its own.</summary>
        /// <summary>What a merged vertex that names its model's maps carries beside the slice (negative: the part is mirrored); the shader knows it.</summary>
        public const float ShapeSign = 2f;

        bool AppendShape(Part p, List<Vector2> uv2, List<Vector2> shape)
        {
            var s = SourceOf(p.Mesh);
            var (first, count) = s.Ranges[p.SubMesh];
            var own = p.ShapeChannel == 2 ? s.Uv3 : s.Uv2;        // (the set the model's maps were baked over)
            bool has = p.Slice >= 0 && own != null;
            // (the sign is two, not one: a stream a mesh does not have may read as ones — that must not name the first model)
            var id = has ? new Vector2(p.Slice + 1, p.Matrix.determinant < 0f ? -ShapeSign : ShapeSign) : Vector2.zero;
            for (int i = first; i < first + count; i++)
            {
                uv2.Add(has ? own[i] : Vector2.zero);
                shape.Add(id);
            }
            return has;
        }

        /// <summary>The texture coordinate of a world point (with its normal) under a wall projection (before the material's tiling).</summary>
        public static Vector2 Project(WallProjection p, Vector3 world, Vector3 normal)
        {
            float tile = Mathf.Max(0.05f, p.Tile);
            float u = Vector3.Dot(world, p.Right), v = world.y - p.FloorY, d = Vector3.Dot(world, p.Across);
            float face = Mathf.Abs(Vector3.Dot(normal, p.Normal)), up = Mathf.Abs(normal.y), side = Mathf.Abs(Vector3.Dot(normal, p.Right));
            if (face >= up && face >= side) return new Vector2(u, v) / tile; // on the wall: exactly as the finish quads
            return up >= side ? new Vector2(u, d) / tile : new Vector2(d, v) / tile;
        }

        /// <summary>A part copies the vertices its submesh uses (the descriptor's range) and its triangles.</summary>
        Source SourceOf(Mesh mesh)
        {
            if (m_Sources.TryGetValue(mesh, out var s)) return s;
            s = new Source { Vertices = mesh.vertices, Indices = new int[mesh.subMeshCount][], Ranges = new (int, int)[mesh.subMeshCount] };
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            var uv = mesh.uv;
            s.Normals = normals.Length == s.Vertices.Length ? normals : null;
            s.Tangents = tangents.Length == s.Vertices.Length ? tangents : null;
            s.Uv = uv.Length == s.Vertices.Length ? uv : null;
            var second = mesh.uv2;
            s.Uv2 = second.Length == s.Vertices.Length ? second : null;
            var third = mesh.uv3;
            s.Uv3 = third.Length == s.Vertices.Length ? third : null;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                s.Indices[i] = mesh.GetIndices(i); // with the base vertex applied: indices into the whole buffer
                int lo = int.MaxValue, hi = -1;
                foreach (int k in s.Indices[i]) { if (k < lo) lo = k; if (k > hi) hi = k; }
                s.Ranges[i] = hi < 0 ? (0, 0) : (lo, hi - lo + 1);
            }
            m_Sources[mesh] = s;
            return s;
        }
    }
}
