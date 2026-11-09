using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Server-side scent trails: every player leaves a breadcrumb every half meter / half second. Hounds smell the
    /// breadcrumbs and follow them towards the newest one. Pure data (unit tested); GameManager feeds it.
    /// </summary>
    public static class ScentTrail
    {
        public const float Lifetime = 40f;
        public const float Spacing = 0.6f;

        public struct Node
        {
            public Vector3 Position;
            public float Time;
        }

        static readonly Dictionary<ulong, List<Node>> s_Trails = new();

        public static void Clear() => s_Trails.Clear();

        public static IReadOnlyList<Node> TrailOf(ulong id) => s_Trails.TryGetValue(id, out var l) ? l : (IReadOnlyList<Node>)System.Array.Empty<Node>();

        public static void Record(ulong id, Vector3 position, float now)
        {
            if (!s_Trails.TryGetValue(id, out var list)) s_Trails[id] = list = new List<Node>();
            if (list.Count > 0 && (list[^1].Position - position).sqrMagnitude < Spacing * Spacing) return;
            list.Add(new Node { Position = position, Time = now });
            int expired = 0;
            while (expired < list.Count && now - list[expired].Time > Lifetime) expired++;
            if (expired > 0) list.RemoveRange(0, expired);
        }

        /// <summary>Strength 0..1 of the freshest scent of player <paramref name="id"/> within <paramref name="radius"/>.</summary>
        public static float Smell(ulong id, Vector3 at, float radius, float now)
        {
            if (!s_Trails.TryGetValue(id, out var list)) return 0f;
            float best = 0f;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                float age = now - list[i].Time;
                if (age > Lifetime) break;
                if ((list[i].Position - at).sqrMagnitude <= radius * radius) best = Mathf.Max(best, 1f - age / Lifetime);
            }
            return best;
        }

        /// <summary>
        /// Where to go next when following the trail: the newest breadcrumb within <paramref name="reach"/> of the
        /// hound (the trail leads forward in time, i.e. towards the player).
        /// </summary>
        public static bool NextAlongTrail(ulong id, Vector3 from, float reach, out Vector3 next)
        {
            next = from;
            if (!s_Trails.TryGetValue(id, out var list)) return false;
            for (int i = list.Count - 1; i >= 0; i--)
                if ((list[i].Position - from).sqrMagnitude <= reach * reach)
                {
                    next = list[i].Position;
                    return true;
                }
            return false;
        }
    }
}
