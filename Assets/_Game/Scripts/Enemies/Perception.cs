using Stealth.Level;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Shared sensor geometry for robots and devices (cones, line of sight, floors, light).</summary>
    public static class Perception
    {
        public const int SightMask = ~(1 << LevelMetrics.CeilingLayer);

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

        /// <summary>Point on the intruder the sensors aim at (a share of the body height).</summary>
        public static Vector3 AimPoint(IIntruder p, float heightShare = 0.8f) => p.Position + Vector3.up * (p.Height * heightShare);

        /// <summary>Nothing solid between the eye and the intruder (doors, walls, slabs block).</summary>
        public static bool LineOfSight(Vector3 eye, IIntruder p, float heightShare = 0.8f)
        {
            var target = AimPoint(p, heightShare);
            var dir = target - eye;
            float dist = dir.magnitude;
            if (Physics.Raycast(eye, dir / Mathf.Max(dist, 1e-4f), out var hit, dist + 0.5f, SightMask, QueryTriggerInteraction.Ignore))
                return ReferenceEquals(hit.collider.GetComponentInParent<IIntruder>(), p);
            return true;
        }

        /// <summary>Eye forward with an extra downward pitch (degrees).</summary>
        public static Vector3 PitchedForward(Transform body, float pitch) =>
            Quaternion.AngleAxis(pitch, body.right) * body.forward;

        /// <summary>Same floor (ignores small height differences on stairs).</summary>
        public static bool SameFloor(Vector3 a, Vector3 b) => LevelLayout.FloorOf(a.y) == LevelLayout.FloorOf(b.y);

        /// <summary>Is the point in an unlit room of the current level?</summary>
        public static bool IsDark(Vector3 p) => ActiveLevel.Layout != null && ActiveLevel.Layout.IsDarkAt(p);

        /// <summary>Multiplier of visual detection speed at a point (dark rooms halve it).</summary>
        public static float LightFactor(Vector3 p) => LightMath.VisionFactor(IsDark(p));
    }

    /// <summary>Detection rates of the different sensor channels (pure, unit tested).</summary>
    public static class SensorMath
    {
        /// <summary>Eyes: faster up close, slower when crouching or in the dark.</summary>
        public static float VisionRate(float detectionSpeed, float distance, float viewDistance, bool crouching, float lightFactor)
        {
            float closeness = 1f - Mathf.Clamp01(distance / viewDistance);
            return detectionSpeed * Mathf.Lerp(0.35f, 2.5f, closeness) * (crouching ? 0.6f : 1f) * lightFactor;
        }

        /// <summary>Wall camera: needs <paramref name="detectTime"/> seconds at mid range.</summary>
        public static float CameraRate(float detectTime, float distance, float viewDistance, bool crouching, float lightFactor)
        {
            float closeness = 1f - Mathf.Clamp01(distance / viewDistance);
            return 1f / detectTime * Mathf.Lerp(0.5f, 2f, closeness) * (crouching ? 0.7f : 1f) * lightFactor;
        }

        /// <summary>Floor vibrations: stronger close by. Call only when <see cref="SeismicSense.Feels"/> is true.</summary>
        public static float SeismicRate(float seismicSpeed, float horizontalDistance, float range) =>
            seismicSpeed * Mathf.Lerp(0.5f, 2f, 1f - Mathf.Clamp01(horizontalDistance / range));

        /// <summary>Hearing distance: sound through a slab is muffled, other floors hear only half as far.</summary>
        public static float HearingDistance(Vector3 source, Vector3 listener)
        {
            float d = Vector3.Distance(source, listener);
            return LevelLayout.FloorOf(source.y) != LevelLayout.FloorOf(listener.y) ? d * 2f : d;
        }
    }
}
