using System.Collections.Generic;
using Stealth.Configs;
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

        // ------------------------------------------------------------------ light

        /// <summary>Every layer, the ceiling layer too (the roof must cast the moon's shadow).</summary>
        const int ShadowMask = ~0;
        static readonly RaycastHit[] s_ShadowHits = new RaycastHit[16];
        static readonly Dictionary<IIntruder, (int frame, float light)> s_LightCache = new();

        /// <summary>
        /// Light at a point: the ambient light, the moon where nothing shades the point and every <see cref="LightSource"/>
        /// in range — brightness / distance² (as URP draws it), inside its cone, with nothing solid in between.
        /// </summary>
        public static float Illumination(Vector3 point, IlluminationSettings s = null)
        {
            s ??= Tuning.Perception.Illumination;
            var a = VisualStyles.GameplayAmbient;
            float sum = (0.2126f * a.r + 0.7152f * a.g + 0.0722f * a.b) * RenderSettings.ambientIntensity * s.AmbientWeight;
            foreach (var src in LightSource.All)
            {
                float brightness = src.Brightness;
                if (brightness <= 0f) continue;
                var light = src.Light;
                if (light.type == LightType.Directional)
                {
                    if (!Shaded(point, -light.transform.forward, 100f)) sum += brightness;
                    continue;
                }
                var from = light.transform.position;
                var to = point - from;
                float d = to.magnitude;
                if (d >= light.range) continue;
                float k = LightMath.Attenuation(d, light.range);
                if (light.type == LightType.Spot) k *= LightMath.SpotFactor(Vector3.Angle(light.transform.forward, to), light.innerSpotAngle, light.spotAngle);
                if (k * brightness < 0.002f) continue;
                if (d > 0.2f && Shaded(from, to / d, d - 0.1f)) continue;
                sum += k * brightness;
            }
            return sum;
        }

        /// <summary>Debug: the lights that reach a point (and the ones shaded, with what shades them).</summary>
        public static string Describe(Vector3 point)
        {
            var sb = new System.Text.StringBuilder();
            float total = 0f;
            foreach (var src in LightSource.All)
            {
                float b = src.Brightness;
                if (b <= 0f) continue;
                var light = src.Light;
                if (light.type == LightType.Directional) continue;
                var from = light.transform.position;
                var to = point - from;
                float d = to.magnitude;
                if (d >= light.range) continue;
                float k = LightMath.Attenuation(d, light.range);
                if (light.type == LightType.Spot) k *= LightMath.SpotFactor(Vector3.Angle(light.transform.forward, to), light.innerSpotAngle, light.spotAngle);
                if (k * b < 0.002f) continue;
                string blocker = null;
                int n = Physics.RaycastNonAlloc(from, to / d, s_ShadowHits, d - 0.1f, ShadowMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n && blocker == null; i++)
                {
                    var c = s_ShadowHits[i].collider;
                    if (c.GetComponentInParent<IIntruder>() == null && c.GetComponentInParent<Detector>() == null && c.GetComponentInParent<Items.WorldItem>() == null)
                        blocker = $"{c.transform.root.name}/{c.name}@{s_ShadowHits[i].distance:0.0}";
                }
                if (blocker == null) total += k * b;
                sb.Append($"{src.transform.parent?.name ?? src.name} {k * b:0.000}{(blocker != null ? " shaded by " + blocker : "")}; ");
            }
            return $"total {total:0.000}: {sb}";
        }

        /// <summary>Something solid on the way (players, robots and loot do not cast stealth shadows).</summary>
        static bool Shaded(Vector3 from, Vector3 dir, float distance)
        {
            int n = Physics.RaycastNonAlloc(from, dir, s_ShadowHits, distance, ShadowMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = s_ShadowHits[i].collider;
                if (c.GetComponentInParent<IIntruder>() != null || c.GetComponentInParent<Detector>() != null ||
                    c.GetComponentInParent<Items.WorldItem>() != null)
                    continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Light on a player: the brighter of the chest and the knees — eyes notice the best-lit part (legs in a lamp's pool
        /// under a desk give you away too; a lit chest above dark legs as well).
        /// </summary>
        public static float IlluminationOf(IIntruder p)
        {
            int frame = Time.frameCount;
            if (s_LightCache.TryGetValue(p, out var c) && c.frame == frame) return c.light;
            var s = Tuning.Perception.Illumination;
            float light = Mathf.Max(Illumination(p.Position + Vector3.up * (p.Height * 0.7f), s), Illumination(p.Position + Vector3.up * (p.Height * 0.25f), s));
            if (s_LightCache.Count > 32) s_LightCache.Clear();
            s_LightCache[p] = (frame, light);
            return light;
        }

        /// <summary>Multiplier of visual detection speed for how lit the player is (darkness hides).</summary>
        public static float LightFactor(IIntruder p) => SensorMath.LightFactor(IlluminationOf(p), Tuning.Perception.Illumination);

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
                Light = LightFactor(p),
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
