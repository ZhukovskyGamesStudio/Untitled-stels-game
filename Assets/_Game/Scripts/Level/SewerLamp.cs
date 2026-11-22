using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A caged lamp on a sewer wall (prefab Level/Sewer/Lamp, and the same lamps built into other pieces): its beam — a wide
    /// spot off the wall and down, the pool of light on the floor — and its halo, a short point light a step off the wall
    /// that lights the brick round the lamp, the vault over it and the floor under it. Without the halo the bulb glowed and
    /// the wall it hung on stayed black: the beam points away from it. Whoever places a lamp sets both through
    /// <see cref="Set(GameObject, Color, float, float)"/>.
    /// </summary>
    public class SewerLamp : MonoBehaviour
    {
        [Tooltip("The wide spot off the wall and down: the pool of light on the floor.")]
        public Light Beam;
        [Tooltip("The short point light by the lamp: the wall round it, the vault, the floor under it.")]
        public Light Halo;

        /// <summary>The halo's brightness, share of the beam's.</summary>
        public const float HaloShare = 0.5f;
        /// <summary>
        /// The halo reaches no further than this, metres: from a lamp 2.3 m up it ends under the house's floor over a
        /// basement (<see cref="Basement.Depth"/>) — it has no shadows.
        /// </summary>
        public const float HaloReach = 5.5f;

        /// <summary>The halo's brightness and reach for a beam's (pure).</summary>
        public static (float intensity, float range) HaloFor(float beamIntensity, float beamRange) =>
            (beamIntensity * HaloShare, Mathf.Min(HaloReach, beamRange * 0.5f));

        /// <summary>This lamp's colour and brightness (and its beam's reach, if given): the halo follows.</summary>
        public void Set(Color color, float intensity, float range = -1f)
        {
            if (Beam == null) return;
            if (range > 0f) Beam.range = range;
            Beam.color = color;
            Beam.intensity = intensity;
            if (Halo == null) return;
            var (haloIntensity, haloRange) = HaloFor(intensity, Beam.range);
            Halo.color = color;
            Halo.intensity = haloIntensity;
            Halo.range = haloRange;
        }

        /// <summary>A placed lamp's colour and brightness (see <see cref="Set(Color, float, float)"/>); nothing for a null.</summary>
        public static void Set(GameObject lamp, Color color, float intensity, float range = -1f)
        {
            if (lamp == null) return;
            if (lamp.TryGetComponent<SewerLamp>(out var l)) { l.Set(color, intensity, range); return; }
            foreach (var light in lamp.GetComponentsInChildren<Light>()) // (a lamp without the component: its lights as they are)
            {
                light.color = color;
                light.intensity = intensity;
                if (range > 0f) light.range = range;
            }
        }

        /// <summary>A placed lamp dimmed or brightened to a share of what its prefab has.</summary>
        public static void Scale(GameObject lamp, Color color, float share, float range = -1f)
        {
            if (lamp == null) return;
            if (lamp.TryGetComponent<SewerLamp>(out var l) && l.Beam != null) l.Set(color, l.Beam.intensity * share, range);
            else foreach (var light in lamp.GetComponentsInChildren<Light>())
            {
                light.color = color;
                light.intensity *= share;
                if (range > 0f) light.range = range;
            }
        }
    }
}
