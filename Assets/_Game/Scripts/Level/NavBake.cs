using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Stealth.Level
{
    /// <summary>
    /// A house's navmesh baked on the drive without a stall: what NavMeshSurface.UpdateNavMesh does — gather the build
    /// sources of the surface's children (with its modifiers as markups, its modifier volumes), then bake on worker threads —
    /// but the gathering a piece of the frame at a time, child by child (<see cref="FrameBudget"/>). Gathered in one call it
    /// was a half-second frame (the [Perf] log's "navmesh" step).
    /// </summary>
    public static class NavBake
    {
        /// <summary>The steps; the bake's operation goes to <paramref name="started"/> once it is under way.</summary>
        public static IEnumerable<bool> Steps(NavMeshSurface surface, System.Action<AsyncOperation> started)
        {
            if (surface == null) yield break;
            if (surface.navMeshData == null)
            {
                surface.navMeshData = new NavMeshData(surface.agentTypeID) { position = surface.transform.position, rotation = surface.transform.rotation };
                surface.AddData();
            }
            int mask = surface.layerMask;
            var markups = new List<NavMeshBuildMarkup>();
            foreach (var m in surface.GetComponentsInChildren<NavMeshModifier>())
            {
                if (!m.isActiveAndEnabled || (mask & (1 << m.gameObject.layer)) == 0 || !m.AffectsAgentType(surface.agentTypeID)) continue;
                markups.Add(new NavMeshBuildMarkup
                {
                    root = m.transform, overrideArea = m.overrideArea, area = m.area, ignoreFromBuild = m.ignoreFromBuild,
                    applyToChildren = m.applyToChildren, overrideGenerateLinks = m.overrideGenerateLinks, generateLinks = m.generateLinks,
                });
            }
            var sources = new List<NavMeshBuildSource>();
            var part = new List<NavMeshBuildSource>();
            var root = surface.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                if (FrameBudget.Over) yield return true;
                var child = root.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                NavMeshBuilder.CollectSources(child, mask, surface.useGeometry, surface.defaultArea, false, markups, false, part);
                sources.AddRange(part);
                part.Clear();
            }
            if (surface.ignoreNavMeshAgent) sources.RemoveAll(x => x.component != null && x.component.gameObject.GetComponent<NavMeshAgent>() != null);
            if (surface.ignoreNavMeshObstacle) sources.RemoveAll(x => x.component != null && x.component.gameObject.GetComponent<NavMeshObstacle>() != null);
            foreach (var m in surface.GetComponentsInChildren<NavMeshModifierVolume>())
            {
                if (!m.isActiveAndEnabled || (mask & (1 << m.gameObject.layer)) == 0 || !m.AffectsAgentType(surface.agentTypeID)) continue;
                var scale = m.transform.lossyScale;
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.ModifierBox,
                    transform = Matrix4x4.TRS(m.transform.TransformPoint(m.center), m.transform.rotation, Vector3.one),
                    size = new Vector3(m.size.x * Mathf.Abs(scale.x), m.size.y * Mathf.Abs(scale.y), m.size.z * Mathf.Abs(scale.z)),
                    area = m.area,
                });
            }
            yield return true;
            started?.Invoke(NavMeshBuilder.UpdateNavMeshDataAsync(surface.navMeshData, surface.GetBuildSettings(), sources, Bounds(surface.transform, sources)));
        }

        /// <summary>The sources' box in the surface's unscaled frame, a little inflated (as NavMeshSurface works it out).</summary>
        static Bounds Bounds(Transform surface, List<NavMeshBuildSource> sources)
        {
            var toLocal = Matrix4x4.TRS(surface.position, surface.rotation, Vector3.one).inverse;
            var result = new Bounds();
            foreach (var src in sources)
            {
                var local = src.shape switch
                {
                    NavMeshBuildSourceShape.Mesh when src.sourceObject is Mesh mesh => mesh.bounds,
                    NavMeshBuildSourceShape.Box or NavMeshBuildSourceShape.Sphere or NavMeshBuildSourceShape.Capsule or NavMeshBuildSourceShape.ModifierBox
                        => new Bounds(Vector3.zero, src.size),
                    _ => (Bounds?)null,
                };
                if (local is Bounds b) result.Encapsulate(World(toLocal * src.transform, b));
            }
            result.Expand(0.1f);
            return result;
        }

        static Bounds World(Matrix4x4 m, Bounds b)
        {
            var ax = new Vector3(Mathf.Abs(m.m00), Mathf.Abs(m.m10), Mathf.Abs(m.m20));
            var ay = new Vector3(Mathf.Abs(m.m01), Mathf.Abs(m.m11), Mathf.Abs(m.m21));
            var az = new Vector3(Mathf.Abs(m.m02), Mathf.Abs(m.m12), Mathf.Abs(m.m22));
            var c = m.MultiplyPoint(b.center);
            var e = ax * b.extents.x + ay * b.extents.y + az * b.extents.z;
            return new Bounds(c, 2f * e);
        }
    }
}
