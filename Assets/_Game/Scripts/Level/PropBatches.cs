using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The furniture of the built house drawn merged (<see cref="MeshBatcher"/>, one mesh per room cell): every prop keeps
    /// its own object, colliders and lights, only its renderers stop drawing (<see cref="Renderer.forceRenderingOff"/>)
    /// while the merged copy draws for them. Styles swap the furniture's meshes and materials, so the merge is undone
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
                    if (!MeshBatcher.CanMerge(r, out var mesh)) continue;
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
