using UnityEngine;

namespace Stealth.Player
{
    /// <summary>
    /// Where the player looks (pure, unit tested): the view ray from the eye. Things used with E (a robot to hack, a
    /// locked door, a safe, a terminal, a crate…) count only while they are under the crosshair — standing next to one
    /// changes nothing on the screen until the player looks at it.
    /// </summary>
    public readonly struct Aim
    {
        public readonly Vector3 Eye, Forward;

        public Aim(Vector3 eye, Vector3 forward)
        {
            Eye = eye;
            Forward = forward.sqrMagnitude > 1e-8f ? forward.normalized : Vector3.forward;
        }

        /// <summary>
        /// The view ray passes within <paramref name="radius"/> of the segment <paramref name="a"/>–<paramref name="b"/>
        /// (a thing's body from its foot to its top; a = b for a point), in front of the eye.
        /// </summary>
        public bool At(Vector3 a, Vector3 b, float radius) => Miss(a, b) <= radius;

        public bool At(Vector3 point, float radius) => At(point, point, radius);

        /// <summary>How far the view ray passes from the segment, metres (infinity when it is behind the eye).</summary>
        public float Miss(Vector3 a, Vector3 b)
        {
            // closest points between the ray (Eye + t·Forward, t ≥ 0) and the segment (a + s·(b − a), 0 ≤ s ≤ 1)
            var ab = b - a;
            var w = Eye - a;
            float len2 = Vector3.Dot(ab, ab), along = Vector3.Dot(Forward, ab);
            float d = Vector3.Dot(Forward, w), e = Vector3.Dot(ab, w);
            float denom = len2 - along * along;
            float s = len2 < 1e-8f ? 0f : denom > 1e-8f ? Mathf.Clamp01((e - along * d) / denom) : 0f;
            var onSegment = a + ab * s;
            float t = Vector3.Dot(onSegment - Eye, Forward);
            if (t < 0.05f) return float.PositiveInfinity; // behind the eye (or inside it)
            // the segment point closest to the ray point at t, then back again: one refinement is enough for short bodies
            var onRay = Eye + Forward * t;
            if (len2 > 1e-8f) onSegment = a + ab * Mathf.Clamp01(Vector3.Dot(onRay - a, ab) / len2);
            t = Mathf.Max(0.05f, Vector3.Dot(onSegment - Eye, Forward));
            return (Eye + Forward * t - onSegment).magnitude;
        }
    }
}
