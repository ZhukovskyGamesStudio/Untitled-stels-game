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

        /// <summary>
        /// The merged furniture by the room it stands in (<see cref="RoomCulling"/> stops drawing a room nobody sees into):
        /// one group a room, not a 10 m square as the shell has; the mark of such a group in its key.
        /// </summary>
        const int RoomGroup = int.MinValue;
        static readonly List<(int room, Renderer renderer)> s_RoomBatches = new();
        public static IReadOnlyList<(int room, Renderer renderer)> RoomBatches => s_RoomBatches;

        /// <summary>The merge stands whole (not being made or undone) / grows with every merge made or undone.</summary>
        public static bool Complete { get; private set; }
        public static int Version { get; private set; }

        /// <summary>Renderers drawn merged / merged renderers drawing them.</summary>
        public static int Merged => s_Hidden.Count;
        public static int Renderers => s_Batches.Count;

        /// <summary>Names of the house's pieces (children of the level root, <see cref="LevelBuilder"/>) that never change once built.</summary>
        static readonly string[] ShellNames = { "Wall", "Floor", "Slab", "Roof", "Fence", "OutsideGround", "Window", "VentFrame", "SqueezeFrame", "ExitSign", "Light_", "ElectricPanel", "Breach", "Hole" };

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
            if (r == null || root == null || r.GetComponentInParent<KeepUnmerged>(true) != null) return false; // (a vent's grille comes off)
            var top = r.transform;
            while (top.parent != null && top.parent != root) top = top.parent;
            if (top.parent != root || !IsShellName(top.name)) return false;
            var lamp = top.GetComponent<RoomLight>();
            if (lamp == null) return true;
            if (lamp.Panels != null)
                foreach (var p in lamp.Panels)
                    if (p == r) return false;
            // (its soft glow — the diffusers' hot middle, the light on the ceiling — follows the lamp too)
            if (lamp.Halo == r) return false;
            if (lamp.Cores != null)
                foreach (var c in lamp.Cores)
                    if (c == r) return false;
            return true;
        }

        /// <summary>Bumped by every clear: a merge going on over several frames gives up when it is not the latest.</summary>
        static int s_Run;

        /// <summary>The props draw themselves again (before their meshes or materials change).</summary>
        public static void Clear()
        {
            s_Run++;
            Complete = false;
            Version++;
            s_RoomBatches.Clear();
            ShapeMaps.Prune();                                 // (the parts of a house taken down)
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
            foreach (var _ in RebuildSteps(holder)) { }
        }

        /// <summary>The same a piece of the frame at a time (<see cref="FrameBudget"/>): the house put up on the drive.</summary>
        public static IEnumerable<bool> RebuildSteps(GameObject holder)
        {
            Clear();
            int run = s_Run;
            if (holder == null || ActiveLevel.Root == null || !Application.isPlaying) yield break;
            var levelRoot = ActiveLevel.Root;
            var root = levelRoot.transform;
            bool Stale() => run != s_Run || ActiveLevel.Root != levelRoot || root == null;
            var batcher = new MeshBatcher(root.worldToLocalMatrix);
            var merged = new List<Renderer>();
            var plan = ActiveLevel.Layout?.Plan;
            foreach (var prop in ActiveLevel.Props)
            {
                if (FrameBudget.Over)
                {
                    yield return true;
                    if (Stale()) yield break;
                }
                if (prop == null) continue;
                var room = plan?.RoomAt(prop.transform.position + Vector3.up * 0.3f);
                foreach (var r in prop.GetComponentsInChildren<MeshRenderer>())
                {
                    // things that are used (and can be marked: their layer changes) draw themselves
                    if (r.GetComponentInParent<KeepUnmerged>() != null || r.GetComponentInParent<Interactable>() != null
                        || r.GetComponentInParent<Items.SafeBox>() != null || r.GetComponentInParent<SecurityTerminal>() != null
                        || !MeshBatcher.CanMerge(r, out var mesh)) continue;
                    if (room != null) batcher.Add(r, mesh, new Vector3Int(room.Id, room.Floor, RoomGroup));
                    else batcher.Add(r, mesh);
                    merged.Add(r);
                }
            }
            for (int i = 0; i < root.childCount; i++)
            {
                if (FrameBudget.Over)
                {
                    yield return true;
                    if (Stale()) yield break;
                }
                var piece = root.GetChild(i);
                if (!IsShellName(piece.name)) continue;
                foreach (var r in piece.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!IsShell(r, root) || !MeshBatcher.CanMerge(r, out var mesh)) continue;
                    batcher.Add(r, mesh);
                    merged.Add(r);
                }
            }
            if (FrameBudget.Over)
            {
                yield return true;
                if (Stale()) yield break;
            }
            var baked = batcher.Bake();
            foreach (var b in baked) s_Meshes.Add(b.Mesh);
            int first = s_Batches.Count;
            MeshBatcher.Spawn(baked, holder, root, "Props", s_Batches);
            for (int i = 0; i < baked.Count; i++)
                if (baked[i].Chunk.z == RoomGroup && s_Batches[first + i].TryGetComponent<MeshRenderer>(out var drawn)) s_RoomBatches.Add((baked[i].Chunk.x, drawn));
            foreach (var r in merged)
            {
                if (r == null) continue;
                r.forceRenderingOff = true;
                s_Hidden.Add(r);
            }
            Version++;
            Complete = true;
        }
    }
}
