using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Player
{
    /// <summary>
    /// "I'm stuck": finds the nearest spot where the player stands free — on a floor under the feet, the capsule touching
    /// nothing — first in rings around the player, then at the fallback points (patrol points of this floor, the
    /// sewer's spawns). The owner moves itself there: the movement is the owner's, and a client has the level's colliders
    /// but no navmesh. <see cref="Offsets"/> is pure; <see cref="FindSpot"/> asks physics.
    /// </summary>
    public static class Unstuck
    {
        /// <summary>Points around a spot to try, nearest first: rings <paramref name="step"/> apart, <paramref name="around"/> per ring.</summary>
        public static IEnumerable<Vector3> Offsets(int rings = 7, float step = 0.4f, int around = 12)
        {
            for (int r = 1; r <= rings; r++)
                for (int i = 0; i < around; i++)
                {
                    float a = (i + (r % 2) * 0.5f) * Mathf.PI * 2f / around; // every other ring turned half a step
                    yield return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (r * step);
                }
        }

        static readonly Collider[] s_Hits = new Collider[16];
        static readonly RaycastHit[] s_Floor = new RaycastHit[8];

        /// <summary>The nearest free standing spot for a capsule of this size, or false if there is none.</summary>
        public static bool FindSpot(Vector3 feet, float radius, float height, IEnumerable<Vector3> fallback, Transform self, out Vector3 spot)
        {
            foreach (var o in Offsets())
                if (Fits(feet + o, feet.y, radius, height, self, out spot)) return true;
            var rest = new List<Vector3>();
            if (fallback != null) rest.AddRange(fallback);
            rest.Sort((a, b) => (a - feet).sqrMagnitude.CompareTo((b - feet).sqrMagnitude));
            foreach (var p in rest)
            {
                if (Fits(p, p.y, radius, height, self, out spot)) return true;
                foreach (var o in Offsets(3, 0.4f, 8))
                    if (Fits(p + o, p.y, radius, height, self, out spot)) return true;
            }
            spot = feet;
            return false;
        }

        /// <summary>A floor within half a metre of <paramref name="floorY"/> under the point, and room for the capsule on it.</summary>
        static bool Fits(Vector3 at, float floorY, float radius, float height, Transform self, out Vector3 spot)
        {
            spot = at;
            var from = new Vector3(at.x, floorY + 0.6f, at.z);
            int n = Physics.RaycastNonAlloc(from, Vector3.down, s_Floor, 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = s_Floor[i];
                if (Mine(h.collider, self) || h.distance >= best || h.normal.y < 0.7f) continue;
                best = h.distance;
                spot = h.point;
                found = true;
            }
            if (!found) return false;
            var bottom = spot + Vector3.up * (radius + 0.08f);
            var top = spot + Vector3.up * Mathf.Max(radius + 0.08f, height - radius);
            int c = Physics.OverlapCapsuleNonAlloc(bottom, top, radius + 0.03f, s_Hits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < c; i++)
                if (!Mine(s_Hits[i], self)) return false;
            spot += Vector3.up * 0.05f;
            return true;
        }

        /// <summary>The player's own collider and other players do not block a spot.</summary>
        static bool Mine(Collider c, Transform self) =>
            c.transform.IsChildOf(self) || c.GetComponentInParent<PlayerController>() != null;
    }
}
