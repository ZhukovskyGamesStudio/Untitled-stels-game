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

        // ------------------------------------------------------------------ how much of the body is visible

        public const int BodySampleCount = 7;
        /// <summary>Half the shoulder width: side samples of the chest.</summary>
        const float ShoulderOffset = 0.22f;

        static readonly Vector3[] s_Samples = new Vector3[BodySampleCount];

        /// <summary>
        /// Points of a body that eyes test (pure): head, chest, both shoulders (across the line of view), hips, knees, feet.
        /// </summary>
        public static void BodySamples(Vector3 feet, float height, Vector3 viewer, Vector3[] into)
        {
            var side = Vector3.Cross(Vector3.up, feet - viewer);
            side.y = 0f;
            side = side.sqrMagnitude > 1e-4f ? side.normalized * ShoulderOffset : Vector3.right * ShoulderOffset;
            var up = Vector3.up * height;
            var chest = feet + up * 0.72f;
            into[0] = feet + up * 0.94f;
            into[1] = chest;
            into[2] = chest + side;
            into[3] = chest - side;
            into[4] = feet + up * 0.5f;
            into[5] = feet + up * 0.28f;
            into[6] = feet + up * 0.08f;
        }

        /// <summary>
        /// Share (0..1) of the intruder's body visible from <paramref name="eye"/>: a ray to each sample point,
        /// furniture, walls and doors in between hide the parts behind them.
        /// </summary>
        public static float Exposure(Vector3 eye, IIntruder p)
        {
            BodySamples(p.Position, p.Height, eye, s_Samples);
            int seen = 0;
            foreach (var point in s_Samples)
            {
                var dir = point - eye;
                float dist = dir.magnitude;
                if (dist < 1e-3f) { seen++; continue; }
                if (!Physics.Raycast(eye, dir / dist, out var hit, dist, SightMask, QueryTriggerInteraction.Ignore) ||
                    ReferenceEquals(hit.collider.GetComponentInParent<IIntruder>(), p))
                    seen++;
            }
            return seen / (float)BodySampleCount;
        }

        /// <summary>
        /// A fixed eye (camera, turret, spider, motion sensor) looks at a player: in range, in the cone and with some of the
        /// body not hidden. Fills everything the vision rate depends on.
        /// </summary>
        public static bool Look(Vector3 eye, Vector3 forward, IIntruder p, float range, float viewAngle, VisionSettings v, out Glimpse glimpse)
        {
            glimpse = default;
            var aim = AimPoint(p, 0.6f);
            var to = aim - eye;
            float distance = to.magnitude;
            if (distance > range) return false;
            float half = viewAngle / 2f;
            float angle = distance < 1e-3f ? 0f : Vector3.Angle(forward, to);
            if (angle > half) return false;
            float exposure = Exposure(eye, p);
            if (exposure <= 0f) return false;
            glimpse = new Glimpse
            {
                Distance = distance,
                Range = range,
                Exposure = exposure,
                OffCentre = Mathf.Min(angle / Mathf.Max(half, 1f), 1f),
                Crouching = p.IsCrouching,
                Moving = p.GroundSpeed > v.StillSpeed,
                Light = LightFactor(p.Position),
            };
            return true;
        }

        // ------------------------------------------------------------------ sound through walls

        /// <summary>Height above the floor at which sounds travel between a source and a listener.</summary>
        public const float EarHeight = 1f;

        static readonly RaycastHit[] s_SoundHits = new RaycastHit[24];

        /// <summary>
        /// Share of a sound's loudness that gets from <paramref name="from"/> to <paramref name="to"/>: every wall, closed door,
        /// slab and piece of furniture on the straight line muffles it (<see cref="SoundBlocker"/>, <see cref="HearingSettings"/>).
        /// </summary>
        public static float SoundTransmission(Vector3 from, Vector3 to, HearingSettings settings)
        {
            var dir = to - from;
            float dist = dir.magnitude;
            if (dist < 1e-3f) return 1f;
            int n = Physics.RaycastNonAlloc(from, dir / dist, s_SoundHits, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float t = 1f;
            for (int i = 0; i < n; i++)
            {
                var material = MaterialOf(s_SoundHits[i].collider);
                if (material.HasValue) t *= settings.Transmission(material.Value);
            }
            return t;
        }

        /// <summary>What a collider is made of for sound; null = does not muffle (players, robots, loot).</summary>
        static SoundMaterial? MaterialOf(Collider c)
        {
            var blocker = c.GetComponentInParent<SoundBlocker>();
            if (blocker != null) return blocker.Material;
            if (c.GetComponentInParent<IIntruder>() != null || c.GetComponentInParent<Detector>() != null ||
                c.GetComponentInParent<Items.WorldItem>() != null)
                return null;
            return SoundMaterial.Furniture;
        }
    }
}
