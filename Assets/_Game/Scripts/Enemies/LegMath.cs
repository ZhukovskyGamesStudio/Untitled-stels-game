using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Two-bone leg inverse kinematics and stepping (pure, unit tested): where the knee goes for a hip and a planted foot,
    /// and when a foot that fell behind should step.
    /// </summary>
    public static class LegMath
    {
        /// <summary>
        /// The knee of a leg with a thigh of <paramref name="upper"/> and a shin of <paramref name="lower"/> metres reaching
        /// from <paramref name="hip"/> to <paramref name="foot"/>; it bends towards <paramref name="hint"/>. A foot out of
        /// reach straightens the leg towards it.
        /// </summary>
        public static Vector3 Knee(Vector3 hip, Vector3 foot, float upper, float lower, Vector3 hint)
        {
            var d = foot - hip;
            float dist = d.magnitude;
            var dir = dist > 1e-5f ? d / dist : Vector3.down;
            float len = Mathf.Clamp(dist, Mathf.Abs(upper - lower) + 1e-3f, upper + lower - 1e-3f);
            float cos = (upper * upper + len * len - lower * lower) / (2f * upper * len);
            float angle = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) * Mathf.Rad2Deg;
            var axis = Vector3.Cross(dir, hint);
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right);
            return hip + Quaternion.AngleAxis(angle, axis.normalized) * dir * upper;
        }

        /// <summary>A planted foot should step once its spot is this far behind where it belongs.</summary>
        public static bool ShouldStep(Vector3 planted, Vector3 target, float stride) => (planted - target).sqrMagnitude > stride * stride;

        /// <summary>The foot during a step: from → to over t = 0..1, lifted along <paramref name="up"/> in an arc.</summary>
        public static Vector3 Swing(Vector3 from, Vector3 to, float t, Vector3 up, float lift)
        {
            t = Mathf.Clamp01(t);
            float eased = t * t * (3f - 2f * t);
            return Vector3.Lerp(from, to, eased) + up * (Mathf.Sin(t * Mathf.PI) * lift);
        }
    }
}
