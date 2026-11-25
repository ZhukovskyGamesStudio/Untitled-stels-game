using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// A thing hanging from a point (pure, tested): its tip is a weight on a string — carried along by the point, pulled
    /// down by gravity, slowed by the air, never further from the point than the string nor swung more than
    /// <see cref="MaxSwing"/> off the way it hangs at rest on its carrier (a carabiner on a backpack does not swing through
    /// the pack). Verlet steps: the last two tip places are the state.
    /// </summary>
    public static class DangleMath
    {
        /// <summary>The share of the tip's speed kept each 1/60 s (the rest is the air and the friction at the clip).</summary>
        public const float Keep = 0.93f;
        /// <summary>The farthest it swings off its rest direction, degrees.</summary>
        public const float MaxSwing = 75f;

        /// <summary>
        /// One step: the tip's new place (and the old one becomes <paramref name="previous"/>).
        /// </summary>
        /// <param name="pivot">Where it hangs from now (world).</param>
        /// <param name="rest">The way it hangs at rest on its carrier now (world, unit).</param>
        /// <param name="length">From the pivot to the weight's centre, metres.</param>
        public static Vector3 Step(Vector3 pivot, Vector3 rest, float length, Vector3 tip, ref Vector3 previous, Vector3 gravity, float dt)
        {
            if (dt <= 0f) return tip;
            float keep = Mathf.Pow(Keep, dt * 60f);
            var next = tip + (tip - previous) * keep + gravity * (dt * dt);
            previous = tip;
            var dir = next - pivot;
            if (dir.sqrMagnitude < 1e-10f) dir = rest;
            dir.Normalize();
            float off = Vector3.Angle(rest, dir);
            if (off > MaxSwing) dir = Vector3.Slerp(rest, dir, MaxSwing / off).normalized;
            return pivot + dir * length;
        }
    }
}
