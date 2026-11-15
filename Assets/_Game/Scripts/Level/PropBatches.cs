using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The furniture of the built house drawn merged (<see cref="MeshBatcher"/>, one mesh per room cell): every prop keeps
    /// its own object, colliders and lights, only its renderers stop drawing (<see cref="Renderer.forceRenderingOff"/>)
    /// while the merged copy draws for them. The house's own pieces that never change once built — walls, posts, slabs,
    /// the roof, window frames, vent and slot frames, exit signs, the bodies of the ceiling lamps (<see cref="IsShell"/>) —
    /// are merged the same way: thousands of renderers cost the frame far more in culling, sorting and shadow culling
    /// than their triangles do (static batching keeps every renderer). Styles swap the furniture's meshes and materials, so the merge is undone
    /// before a style is put on and made again after (<see cref="VisualStyles"/>). Rendering only, on every peer.
    /// </summary>
    public static class PropBatches
    {
        static readonly List<Renderer> s_Hidden = new();
        static readonly List<GameObject> s_Batches = new();
        static readonly List<Mesh> s_Meshes = new();

        /// <summary>Renderers drawn merged / merged renderers drawing them.</summary>
        public static int Merged => s_Hidden.Count;
        public static int Renderers => s_Batches.Count;

        /// <summary>Names of the house's pieces (children of the level root, <see cref="LevelBuilder"/>) that never change once built.</summary>
        static readonly string[] ShellNames = { "Wall", "Floor", "Slab", "Roof", "Fence", "OutsideGround", "Window", "VentFrame", "SqueezeFrame", "ExitSign", "Light_" };

        static bool IsShellName(string name)
        {
            foreach (var n in ShellNames)
                if (name.StartsWith(n, System.StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// A renderer of the house's unchanging shell (merged here rather than static-batched by the level builder): a
        /// lamp's glowing panels flicker by a property block and draw themselves.
        /// </summary>
        public static bool IsShell(Renderer r, Transform root)
        {
            if (r == null || root == null) return false;
            var top = r.transform;
            while (top.parent != null && top.parent != root) top = top.parent;
            if (top.parent != root || !IsShellName(top.name)) return false;
            var lamp = top.GetComponent<RoomLight>();
            if (lamp != null && lamp.Panels != null)
                foreach (var p in lamp.Panels)
                    if (p == r) return false;
            return true;
        }

        /// <summary>The props draw themselves again (before their meshes or materials change).</summary>
        public static void Clear()
        {
            foreach (var r in s_Hidden)
                if (r != null) r.forceRenderingOff = false;
            foreach (var go in s_Batches)
                if (go != null) Object.Destroy(go);
            foreach (var m in s_Meshes)
                if (m != null) Object.Destroy(m);
            s_Hidden.Clear();
            s_Batches.Clear();
            s_Meshes.Clear();
        }

        /// <summary>Merges the furniture as it looks now; <paramref name="holder"/> is the prefab a merged mesh is drawn with.</summary>
        public static void Rebuild(GameObject holder)
        {
            Clear();
            if (holder == null || ActiveLevel.Root == null || !Application.isPlaying) return;
            var root = ActiveLevel.Root.transform;
            var batcher = new MeshBatcher(root.worldToLocalMatrix);
            var merged = new List<Renderer>();
            foreach (var prop in ActiveLevel.Props)
            {
                if (prop == null) continue;
                foreach (var r in prop.GetComponentsInChildren<MeshRenderer>())
                {
                    if (r.GetComponentInParent<KeepUnmerged>() != null || !MeshBatcher.CanMerge(r, out var mesh)) continue;
                    batcher.Add(r, mesh);
                    merged.Add(r);
                }
            }
            for (int i = 0; i < root.childCount; i++)
            {
                var piece = root.GetChild(i);
                if (!IsShellName(piece.name)) continue;
                foreach (var r in piece.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!IsShell(r, root) || !MeshBatcher.CanMerge(r, out var mesh)) continue;
                    batcher.Add(r, mesh);
                    merged.Add(r);
                }
            }
            var baked = batcher.Bake();
            foreach (var b in baked) s_Meshes.Add(b.Mesh);
            MeshBatcher.Spawn(baked, holder, root, "Props", s_Batches);
            foreach (var r in merged)
            {
                r.forceRenderingOff = true;
                s_Hidden.Add(r);
            }
        }
    }
}
