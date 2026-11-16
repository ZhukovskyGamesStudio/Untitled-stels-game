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
            var wallParts = ActiveLevel.Shell?.WallParts;
            if (wallParts != null)
                foreach (var r in wallParts)
                    if (r != null) r.forceRenderingOff = false; // the bare wall's look until the finish takes over
            bool vault = ActiveLevel.Layout != null && ActiveLevel.Layout.VaultRoom >= 0; // dressed in steel in every style
            g = ShopLook.For(g, kit, ActiveLevel.Layout); // the auto repair shop dresses as one in every style
            g = ClinicLook.For(g, kit, ActiveLevel.Layout); // and the hospital as a hospital
            if (g == null || kit == null || holder == null || !(g.HasArchitecture || vault) || ActiveLevel.Root == null || ActiveLevel.Shell == null) return;
            s_Kit = kit;
            var root = ActiveLevel.Root.transform;
            var batcher = new MeshBatcher(root.worldToLocalMatrix);
            AddWallParts(batcher, wallParts, g, kit);
            foreach (var p in ArchitecturePlanner.Plan(ActiveLevel.Shell, ActiveLevel.Layout, g))
            {
                bool finish = p.IsQuad;
                var prefab = finish ? kit.Finish : (int)p.Kind < kit.Pieces.Length ? kit.Pieces[(int)p.Kind] : null;
                if (prefab == null) continue;
                Count++;
                if (!Mergeable(prefab)) { Place(prefab, p, g, root, finish); continue; }
                // as if instantiated at the piece's place under the level root and scaled to p.Scale
                var at = root.localToWorldMatrix * Matrix4x4.TRS(root.InverseTransformPoint(p.Position), Quaternion.Inverse(root.rotation) * p.Rotation, p.Scale);
                var chunk = MeshBatcher.ChunkOf(p.Position);
                foreach (var part in PartsOf(prefab))
                {
                    if (IsDecal(p.Kind))
                    {
                        // decay and posters: the whole image on the quad, the material picked from the style's list
                        batcher.Add(chunk, part.look, DecalOf(p, g), part.mesh, 0, at * part.local, new Vector4(1f, 1f, 0f, 0f));
                        continue;
                    }
                    if (finish)
                    {
                        var mat = FinishOf(p, g, out var tiling);
                        batcher.Add(chunk, part.look, mat, part.mesh, 0, at * part.local, tiling);
                        continue;
                    }
                    if (IsWallPart(p.Kind, part.name, g))
                    {
                        // an arch's spandrels are the wall itself: its finish, its texture continuing over the opening
                        var n = p.Rotation * Vector3.forward;
                        var right = Vector3.Cross(Vector3.up, -n);
                        var (paper, tile) = WallOf(p.Variant, g); // the finish of the room on the arch's side
                        var wall = new WallProjection
                        {
                            Right = right, Normal = n, Across = ArchitecturePlanner.Across(right), Tile = tile, FloorY = p.Position.y,
                        };
                        // in the plane of the finish quads around the opening (6 mm off the wall), or the outline filters draw the step
                        batcher.Add(chunk, part.look, paper ?? g.Wall, part.mesh, 0, Matrix4x4.Translate(n * ArchitecturePlanner.FinishLift) * at * part.local, null, wall);
                        continue;
                    }
                    var style = MaterialFor(p.Kind, part.name, g);
                    // no wall finish to continue: the spandrels with their lap stand just proud of the wall (no z-fighting with it)
                    var place = p.Kind == DecorKind.Arch && part.name.StartsWith("Body")
                        ? Matrix4x4.Translate(p.Rotation * Vector3.forward * ArchitecturePlanner.FinishLift) * at * part.local
                        : at * part.local;
                    for (int i = 0; i < (style != null ? 1 : part.materials.Length); i++)
                        batcher.Add(chunk, part.look, style != null ? style : part.materials[i], part.mesh, i, place);
                }
            }
            var baked = batcher.Bake();
            foreach (var b in baked) s_Meshes.Add(b.Mesh);
            MeshBatcher.Spawn(baked, holder, root, "Decor", s_Batches);
        }

        /// <summary>
        /// Parts of doors that are the wall itself (the corners round the vault's round door, <see cref="AutoDoor.WallParts"/>):
        /// the wall finish with the wall's own texture layout, in the plane of the finish quads round the opening — the
        /// door sits in the wall without a seam. Their own renderers (the bare wall's look) step aside.
        /// </summary>
        static void AddWallParts(MeshBatcher batcher, List<MeshRenderer> parts, StyleGeometry g, StyleKit kit)
        {
            if (parts == null) return;
            foreach (var r in parts)
            {
                if (r == null || !r.TryGetComponent<MeshFilter>(out var mf) || !MeshBatcher.CanMerge(mf.sharedMesh)) continue;
                // the vault's side of its door wall is the vault's steel, the corridor's side the style's wall
                bool inside = r.gameObject.name == "WallBack" && kit.VaultWall != null;
                var mat = inside ? kit.VaultWall : g.Wall;
                if (mat == null) continue;
                var t = r.transform;
                var n = t.forward; // the face looks out of the wall along the part's +Z
                var wall = new WallProjection
                {
                    Right = Vector3.Cross(Vector3.up, -n), Normal = n, Across = ArchitecturePlanner.Across(Vector3.Cross(Vector3.up, -n)),
                    Tile = inside ? kit.VaultWallTile : g.WallTile, FloorY = t.position.y,
                };
                batcher.Add(MeshBatcher.ChunkOf(t.position), BatchLook.Of(r), mat, mf.sharedMesh, 0,
                    Matrix4x4.Translate(n * ArchitecturePlanner.FinishLift) * t.localToWorldMatrix, null, wall);
                r.forceRenderingOff = true;
            }
        }

        static readonly Dictionary<GameObject, bool> s_Mergeable = new();
        /// <summary>The kit of the last rebuild (its vault finishes).</summary>
        static StyleKit s_Kit;
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
                if (IsDecal(p.Kind)) { r.sharedMaterial = DecalOf(p, g); r.SetPropertyBlock(null); continue; }
                r.sharedMaterial = FinishOf(p, g, out var tiling);
                s_Block ??= new MaterialPropertyBlock();
                s_Block.Clear();
                s_Block.SetVector(BaseMapSt, tiling);
                r.SetPropertyBlock(s_Block);
            }
        }

        static bool IsDecal(DecorKind k) => k is DecorKind.FloorDecal or DecorKind.WallDecal or DecorKind.Poster;

        /// <summary>
        /// A wall finish by its variant (<see cref="DecorPiece.Variant"/>): 0 the style's wall, 1 + 2k the k-th room look's
        /// wall, 2 + 2k its lower band; and the metres one repeat of its texture covers.
        /// </summary>
        static (Material, float) WallOf(int variant, StyleGeometry g)
        {
            int k = (variant - 1) / 2;
            if (variant <= 0 || g.RoomLooks == null || k >= g.RoomLooks.Length) return (g.Wall, g.WallTile);
            var look = g.RoomLooks[k];
            return variant % 2 == 1 ? (look.Wall ?? g.Wall, look.Wall != null ? look.WallTile : g.WallTile) : (look.Lower, look.LowerTile);
        }

        /// <summary>The material of a decal: its variant in the style's decay or poster list.</summary>
        static Material DecalOf(DecorPiece p, StyleGeometry g)
        {
            var list = p.Kind switch { DecorKind.FloorDecal => g.FloorDecay, DecorKind.WallDecal => g.WallDecay, _ => g.Posters };
            return list != null && p.Variant >= 0 && p.Variant < list.Length ? list[p.Variant] : null;
        }

        /// <summary>The material of a finish quad and its texture tiling (as a _BaseMap_ST: the texture follows the world).</summary>
        static Material FinishOf(DecorPiece p, StyleGeometry g, out Vector4 tiling)
        {
            var (mat, tileSize) = p.Kind switch
            {
                DecorKind.FloorFinish when p.Threshold => (s_Kit != null && s_Kit.Threshold != null ? s_Kit.Threshold : g.Trim, 1f),
                DecorKind.FloorFinish when p.Vault && s_Kit != null && s_Kit.VaultFloor != null => (s_Kit.VaultFloor, s_Kit.VaultFloorTile),
                DecorKind.WallFinish when p.Vault && s_Kit != null && s_Kit.VaultWall != null => (s_Kit.VaultWall, s_Kit.VaultWallTile),
                DecorKind.WallFinish when p.Variant > 0 => WallOf(p.Variant, g),
                DecorKind.CeilingFinish when p.Variant > 0 && g.RoomLooks != null && p.Variant <= g.RoomLooks.Length
                    => (g.RoomLooks[p.Variant - 1].Ceiling, g.RoomLooks[p.Variant - 1].CeilingTile),
                DecorKind.FloorFinish when p.Variant > 0 && g.RoomFloors != null && p.Variant <= g.RoomFloors.Length
                    => (g.RoomFloors[p.Variant - 1].Material, g.RoomFloors[p.Variant - 1].Tile),
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

        /// <summary>
        /// A child of a kit piece that is part of the wall it stands in — the spandrels (Body) of an arch, flush with the
        /// wall face — so it takes the style's wall finish with the wall's own texture layout instead of a trim colour:
        /// otherwise the joint with the wall over the opening shows as a line.
        /// </summary>
        public static bool IsWallPart(DecorKind kind, string child, StyleGeometry g) =>
            kind == DecorKind.Arch && g.Wall != null && child.StartsWith("Body");

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
