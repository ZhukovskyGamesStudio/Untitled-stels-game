using System.Collections.Generic;
using Stealth.Enemies;
using Stealth.Items;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Things a player marks for the team (not robots — those are <see cref="Detector.ShowThroughWalls"/>): loot and tools,
    /// crates, safes, terminals, lock panels, the cage, levers, the car's parts. The mark travels as a position (a "thing"
    /// ping); every peer finds the thing there and puts its renderers on <see cref="XrayLayer"/> while the ping lasts — the
    /// renderer draws that layer through walls in yellow-green (features "MarkedThing*"), as it draws marked robots in red.
    /// </summary>
    public static class MarkedThings
    {
        /// <summary>Layer of a marked thing's renderers (the renderer outlines it through walls).</summary>
        public const int XrayLayer = 10;
        /// <summary>A thing is found within this distance of a mark's position.</summary>
        public const float FindRadius = 0.8f;

        static readonly Dictionary<Component, List<(Renderer renderer, int layer)>> s_Marked = new();
        static readonly HashSet<Component> s_Wanted = new();
        static readonly List<Component> s_Drop = new();

        /// <summary>Every markable thing in the scene now and the point that stands for it.</summary>
        static IEnumerable<(Component thing, Vector3 point)> All()
        {
            foreach (var i in WorldItem.All)
                if (i != null && !i.Taking) yield return (i, i.Collider != null ? i.Collider.bounds.center : i.transform.position);
            foreach (var i in Interactable.All)
                if (i != null && i.Index >= 0 && i.isActiveAndEnabled) yield return (i, i.Point);
            foreach (var s in SafeBox.All)
                if (s != null) yield return (s, s.transform.position + Vector3.up * 0.5f);
            foreach (var t in SecurityTerminal.All)
                if (t != null) yield return (t, t.transform.position + Vector3.up * 0.9f);
        }

        /// <summary>
        /// The thing under the crosshair: its collider is the first thing the view ray hits, or it is within a few degrees
        /// of the view and in plain sight (small things are hard to hit exactly). Null if none.
        /// </summary>
        public static Component Target(Vector3 eye, Vector3 forward, float range, out Vector3 point)
        {
            point = default;
            Component best = null;
            if (Physics.Raycast(eye, forward, out var hit, range, Perception.SightMask, QueryTriggerInteraction.Collide))
            {
                float near = FindRadius * 1.5f;
                foreach (var (thing, at) in All())
                {
                    bool own = hit.collider.transform.IsChildOf(thing.transform);
                    float d = (at - hit.point).magnitude;
                    if (!own && d >= near) continue;
                    if (own) { best = thing; point = at; break; }
                    best = thing; point = at; near = d;
                }
                if (best != null) return best;
            }
            float bestAngle = 4f;
            foreach (var (thing, at) in All())
            {
                var to = at - eye;
                float dist = to.magnitude;
                if (dist > range || dist < 0.05f) continue;
                float angle = Vector3.Angle(forward, to);
                if (angle >= bestAngle) continue;
                if (Physics.Raycast(eye, to / dist, out var block, dist - 0.3f, Perception.SightMask, QueryTriggerInteraction.Ignore)
                    && !block.collider.transform.IsChildOf(thing.transform)) continue;
                best = thing;
                point = at;
                bestAngle = angle;
            }
            return best;
        }

        /// <summary>The thing a mark at <paramref name="position"/> stands for (the nearest within <see cref="FindRadius"/>), or null.</summary>
        public static Component Find(Vector3 position)
        {
            Component best = null;
            float near = FindRadius;
            foreach (var (thing, at) in All())
            {
                float d = (at - position).magnitude;
                if (d < near) { near = d; best = thing; }
            }
            return best;
        }

        /// <summary>Every frame: exactly the things at these marks are drawn through walls (the rest get their layers back).</summary>
        public static void Show(IEnumerable<Vector3> marks)
        {
            s_Wanted.Clear();
            foreach (var m in marks)
            {
                var thing = Find(m);
                if (thing != null) s_Wanted.Add(thing);
            }
            s_Drop.Clear();
            foreach (var k in s_Marked.Keys) if (k == null || !s_Wanted.Contains(k)) s_Drop.Add(k);
            foreach (var k in s_Drop) Unmark(k);
            foreach (var t in s_Wanted) if (!s_Marked.ContainsKey(t)) Mark(t);
        }

        /// <summary>The renderers that show a thing: its own; a thing with none of its own (a seat of the car) — what it belongs to.</summary>
        static Renderer[] RenderersOf(Component thing)
        {
            var own = thing.GetComponentsInChildren<Renderer>();
            if (own.Length > 0 || thing is not Interactable i || i.ReachRoot == null) return own;
            return i.ReachRoot.GetComponentsInChildren<Renderer>();
        }

        static void Mark(Component thing)
        {
            var list = new List<(Renderer, int)>();
            foreach (var r in RenderersOf(thing))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || r.gameObject.layer == LevelMetrics.CeilingLayer || r.gameObject.layer == Detector.XrayLayer) continue;
                list.Add((r, r.gameObject.layer));
                r.gameObject.layer = XrayLayer;
            }
            s_Marked[thing] = list;
        }

        static void Unmark(Component thing)
        {
            if (!s_Marked.TryGetValue(thing, out var list)) return;
            foreach (var (r, layer) in list)
                if (r != null) r.gameObject.layer = layer;
            s_Marked.Remove(thing);
        }

        /// <summary>A new house: nothing is marked any more (its things are gone with the old one).</summary>
        public static void Clear()
        {
            foreach (var k in new List<Component>(s_Marked.Keys)) Unmark(k);
        }
    }
}
