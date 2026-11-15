using System.Collections.Generic;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Player
{
    /// <summary>
    /// What the player is looking at: a thin ray from the eyes along the view, the first solid thing it meets within reach
    /// (the player's own colliders and triggers are skipped). Interactive objects are used only this way — looking right
    /// at them — instead of anything within a radius.
    /// </summary>
    public static class LookTarget
    {
        static readonly List<RaycastHit> s_Hits = new();
        static RaycastHit[] s_Buffer = new RaycastHit[32];

        /// <summary>The component <typeparamref name="T"/> on (or above) the first solid thing under the crosshair, or null.</summary>
        public static T At<T>(Transform eye, Transform self, float reach, float radius) where T : Component =>
            At<T>(eye, self, reach, radius, out _);

        /// <summary>As <see cref="At{T}(Transform,Transform,float,float)"/>, with the hit (which collider, where).</summary>
        public static T At<T>(Transform eye, Transform self, float reach, float radius, out RaycastHit hit) where T : Component
        {
            hit = default;
            if (eye == null) return null;
            var ray = new Ray(eye.position, eye.forward);
            int n = Physics.SphereCastNonAlloc(ray, radius, s_Buffer, reach, ~(1 << LevelMetrics.CeilingLayer), QueryTriggerInteraction.Ignore);
            if (n == s_Buffer.Length) s_Buffer = new RaycastHit[n * 2];
            s_Hits.Clear();
            for (int i = 0; i < n; i++) s_Hits.Add(s_Buffer[i]);
            s_Hits.Sort((a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in s_Hits)
            {
                if (self != null && h.collider.transform.IsChildOf(self)) continue;
                if (h.distance <= 0f && h.point == Vector3.zero) continue; // overlapping at the start: not something in view
                hit = h;
                return h.collider.GetComponentInParent<T>();               // the first solid thing decides: it or nothing
            }
            return null;
        }
    }
}
