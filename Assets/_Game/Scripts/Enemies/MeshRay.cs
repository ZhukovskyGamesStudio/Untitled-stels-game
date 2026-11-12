using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// A ray against a mesh's triangles, no colliders needed (pure, unit tested): the sensor mounts of a robot find the
    /// surface of whatever model the current style gives it and sit on it.
    /// </summary>
    public static class MeshRay
    {
        /// <summary>Möller–Trumbore: the ray o + d·t hits triangle a, b, c (either side) at t ≥ 0.</summary>
        public static bool Triangle(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
        {
            t = 0f;
            var e1 = b - a;
            var e2 = c - a;
            var p = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-9f) return false;
            float inv = 1f / det;
            var s = o - a;
            float u = Vector3.Dot(s, p) * inv;
            if (u < 0f || u > 1f) return false;
            var q = Vector3.Cross(s, e1);
            float v = Vector3.Dot(d, q) * inv;
            if (v < 0f || u + v > 1f) return false;
            t = Vector3.Dot(e2, q) * inv;
            return t >= 0f;
        }

        /// <summary>The nearest hit of the ray on a triangle list (mesh space); false if it misses.</summary>
        public static bool Cast(Vector3 o, Vector3 d, Vector3[] vertices, int[] triangles, out float t)
        {
            t = float.MaxValue;
            bool any = false;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                if (!Triangle(o, d, vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]], out float h) || h >= t) continue;
                t = h;
                any = true;
            }
            return any;
        }
    }
}
