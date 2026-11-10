using System.Collections.Generic;
using Stealth.Configs;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The architecture of the current visual style in the built house: the kit pieces planned by
    /// <see cref="ArchitecturePlanner"/>, painted with the style's materials (each child of a piece by its role: Body,
    /// Accent, Eye), merged by <see cref="MeshBatcher"/> into one mesh per room cell (a house has tens of thousands of
    /// pieces; one renderer each was the bulk of the frame) and hung under the level root. Rebuilt whenever the style or
    /// the level changes; destroyed with the level.
    /// </summary>
    public static class StyleDecor
    {
        static readonly List<GameObject> s_Batches = new();
        static readonly List<Mesh> s_Meshes = new();
        static readonly Dictionary<GameObject, List<(Mesh mesh, Matrix4x4 local, string name, Material[] materials, BatchLook look)>> s_Parts = new();

        /// <summary>Pieces in the house now.</summary>
        public static int Count { get; private set; }
        /// <summary>Renderers they are drawn with.</summary>
        public static int Renderers => s_Batches.Count;

        public static void Clear()
        {
            foreach (var go in s_Batches)
                if (go != null) Object.Destroy(go);
            foreach (var m in s_Meshes)
                if (m != null) Object.Destroy(m);
            s_Batches.Clear();
            s_Meshes.Clear();
            Count = 0;
        }

        public static void Rebuild(StyleGeometry g, StyleKit kit, GameObject holder)
        {
            Clear();
            if (g == null || kit == null || holder == null || !g.HasArchitecture || ActiveLevel.Root == null || ActiveLevel.Shell == null) return;
            var root = ActiveLevel.Root.transform;
            var batcher = new MeshBatcher(root.worldToLocalMatrix);
            foreach (var p in ArchitecturePlanner.Plan(ActiveLevel.Shell, ActiveLevel.Layout, g))
            {
                bool finish = p.Kind is DecorKind.FloorFinish or DecorKind.WallFinish or DecorKind.CeilingFinish;
                var prefab = finish ? kit.Finish : (int)p.Kind < kit.Pieces.Length ? kit.Pieces[(int)p.Kind] : null;
                if (prefab == null) continue;
                Count++;
                if (!Mergeable(prefab)) { Place(prefab, p, g, root, finish); continue; }
                // as if instantiated at the piece's place under the level root and scaled to p.Scale
                var at = root.localToWorldMatrix * Matrix4x4.TRS(root.InverseTransformPoint(p.Position), Quaternion.Inverse(root.rotation) * p.Rotation, p.Scale);
                var chunk = MeshBatcher.ChunkOf(p.Position);
                foreach (var part in PartsOf(prefab))
                {
                    if (finish)
                    {
                        var mat = FinishOf(p, g, out var tiling);
                        batcher.Add(chunk, part.look, mat, part.mesh, 0, at * part.local, tiling);
                        continue;
                    }
                    var style = MaterialFor(p.Kind, part.name, g);
                    for (int i = 0; i < (style != null ? 1 : part.materials.Length); i++)
                        batcher.Add(chunk, part.look, style != null ? style : part.materials[i], part.mesh, i, at * part.local);
                }
            }
            var baked = batcher.Bake();
            foreach (var b in baked) s_Meshes.Add(b.Mesh);
            MeshBatcher.Spawn(baked, holder, root, "Decor", s_Batches);
        }

        static readonly Dictionary<GameObject, bool> s_Mergeable = new();
        static MaterialPropertyBlock s_Block;
        static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

        static bool Mergeable(GameObject prefab)
        {
            if (s_Mergeable.TryGetValue(prefab, out var ok)) return ok;
            ok = true;
            foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>())
                if (r.enabled && (!r.TryGetComponent<MeshFilter>(out var mf) || !MeshBatcher.CanMerge(mf.sharedMesh))) ok = false;
            s_Mergeable[prefab] = ok;
            if (!ok) Debug.LogWarning($"[StyleDecor] {prefab.name}: mesh not readable, drawn piece by piece");
            return ok;
        }

        /// <summary>A piece whose mesh cannot be merged (not readable): its own instance, as before merging.</summary>
        static void Place(GameObject prefab, DecorPiece p, StyleGeometry g, Transform root, bool finish)
        {
            var go = Object.Instantiate(prefab, p.Position, p.Rotation, root);
            go.transform.localScale = p.Scale;
            s_Batches.Add(go);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                if (!finish)
                {
                    var m = MaterialFor(p.Kind, r.gameObject.name, g);
                    if (m != null) r.sharedMaterial = m;
                    continue;
                }
                r.sharedMaterial = FinishOf(p, g, out var tiling);
                s_Block ??= new MaterialPropertyBlock();
                s_Block.Clear();
                s_Block.SetVector(BaseMapSt, tiling);
                r.SetPropertyBlock(s_Block);
            }
        }

        /// <summary>The material of a finish quad and its texture tiling (as a _BaseMap_ST: the texture follows the world).</summary>
        static Material FinishOf(DecorPiece p, StyleGeometry g, out Vector4 tiling)
        {
            var (mat, tileSize) = p.Kind switch
            {
                DecorKind.FloorFinish => (g.Floor, g.FloorTile),
                DecorKind.CeilingFinish => (g.CeilingFinish, g.CeilingTile),
                _ => (g.Wall, g.WallTile),
            };
            float tile = Mathf.Max(0.05f, tileSize);
            tiling = new Vector4(p.Surface.x / tile, p.Surface.y / tile, p.Surface.z / tile, p.Surface.w / tile);
            return mat;
        }

        /// <summary>The drawn parts of a kit prefab, relative to its root (cached: prefabs are assets).</summary>
        static List<(Mesh mesh, Matrix4x4 local, string name, Material[] materials, BatchLook look)> PartsOf(GameObject prefab)
        {
            if (s_Parts.TryGetValue(prefab, out var parts)) return parts;
            parts = new List<(Mesh, Matrix4x4, string, Material[], BatchLook)>();
            var toRoot = prefab.transform.worldToLocalMatrix;
            foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || !r.TryGetComponent<MeshFilter>(out var mf) || !MeshBatcher.CanMerge(mf.sharedMesh)) continue;
                parts.Add((mf.sharedMesh, toRoot * r.transform.localToWorldMatrix, r.gameObject.name, r.sharedMaterials, BatchLook.Of(r)));
            }
            s_Parts[prefab] = parts;
            return parts;
        }

        /// <summary>The style material for a child of a kit piece (pure).</summary>
        public static Material MaterialFor(DecorKind kind, string child, StyleGeometry g)
        {
            if (child.StartsWith("Eye")) return g.Glow ?? g.Trim;
            bool accent = child.StartsWith("Accent");
            Material m = kind switch
            {
                DecorKind.Wainscot => accent ? g.Panel : g.Trim,
                DecorKind.Pipes or DecorKind.PipeClamp => g.Pipe,
                DecorKind.Pilaster or DecorKind.Arch => accent ? g.Trim : g.Column,
                DecorKind.Beam => g.Beam,
                DecorKind.IBeam => accent ? g.Pipe : g.Beam,
                DecorKind.Coffer => accent ? g.Trim : g.Beam,
                DecorKind.Tray or DecorKind.TrayHanger => accent ? g.Trim : g.Pipe,
                _ => g.Trim,
            };
            return m ?? g.Trim;
        }
    }
}
