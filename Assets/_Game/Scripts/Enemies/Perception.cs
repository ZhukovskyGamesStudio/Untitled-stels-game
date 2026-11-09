using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Shared sensor geometry for robots and devices.</summary>
    public static class Perception
    {
        public const int SightMask = ~(1 << LevelGenerator.CeilingLayer);

        /// <summary>Is <paramref name="target"/> inside a cone (full <paramref name="angle"/> in degrees) and range?</summary>
        public static bool InCone(Vector3 origin, Vector3 forward, Vector3 target, float range, float angle)
        {
            var d = target - origin;
            if (d.sqrMagnitude > range * range) return false;
            return Vector3.Angle(forward, d) <= angle / 2f;
        }

        /// <summary>Horizontal-only cone check (robots that look around on their floor).</summary>
        public static bool InFlatCone(Vector3 origin, Vector3 forward, Vector3 target, float range, float angle)
        {
            var d = target - origin;
            if (d.sqrMagnitude > range * range) return false;
            var flat = new Vector3(d.x, 0, d.z);
            var f = new Vector3(forward.x, 0, forward.z);
            return flat.sqrMagnitude < 1e-4f || Vector3.Angle(f, flat) <= angle / 2f;
        }

        /// <summary>Nothing solid between the eye and the player (doors, walls, slabs block).</summary>
        public static bool LineOfSight(Vector3 eye, PlayerController p, float heightShare = 0.8f)
        {
            var target = p.transform.position + Vector3.up * (p.Height * heightShare);
            var dir = target - eye;
            float dist = dir.magnitude;
            if (Physics.Raycast(eye, dir / Mathf.Max(dist, 1e-4f), out var hit, dist + 0.5f, SightMask, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<PlayerController>() == p;
            return true;
        }

        /// <summary>Eye forward with an extra downward pitch (degrees).</summary>
        public static Vector3 PitchedForward(Transform body, float pitch) =>
            Quaternion.AngleAxis(pitch, body.right) * body.forward;

        /// <summary>Same floor (ignores small height differences on stairs).</summary>
        public static bool SameFloor(Vector3 a, Vector3 b) => LevelLayout.FloorOf(a.y) == LevelLayout.FloorOf(b.y);
    }
}
