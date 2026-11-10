using Stealth.Game;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Ceiling lights of a room: two glowing panels and one point light between them with soft shadows (walls and slabs
    /// keep the light inside the room). In a dark room it is off
    /// (the panel too); a broken one flickers; during an alarm every lamp pulses red, even in dark rooms. Purely visual,
    /// runs on every peer; robots read the result through its <see cref="LightSource"/>.
    /// </summary>
    public class RoomLight : MonoBehaviour
    {
        [Tooltip("The light of the room (a child under the ceiling).")]
        public Light Lamp;
        public bool Flicker;
        public float Seed;
        [Tooltip("The glowing diffusers of the panels: their emission follows the light.")]
        public Renderer[] Panels;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly Color AlarmColor = new(1f, 0.15f, 0.1f);

        Light m_Light;
        Color m_BaseColor;
        float m_BaseIntensity;
        MaterialPropertyBlock m_Block;
        float m_LastGlow = -1f;
        Color m_LastColor;

        public void Init(Color color, float intensity, bool flicker, float seed)
        {
            m_Light = Lamp != null ? Lamp : GetComponentInChildren<Light>();
            m_BaseColor = color;
            m_BaseIntensity = intensity;
            Flicker = flicker;
            Seed = seed;
            Apply(color, intensity);
        }

        void Update()
        {
            if (m_Light == null) return;
            float k = Flicker ? LightMath.Flicker(Time.time + Seed) : 1f;
            var gm = GameManager.Instance;
            bool alarm = gm != null && gm.AlarmActive;
            var color = alarm ? Color.Lerp(m_BaseColor, AlarmColor, 0.8f) : m_BaseColor;
            float baseIntensity = alarm ? Mathf.Max(m_BaseIntensity, LightMath.AlarmIntensity) : m_BaseIntensity; // alarm lights even dark rooms
            Apply(color, baseIntensity * k * (alarm ? 0.7f + 0.3f * Mathf.Sin(Time.time * 6f) : 1f));
        }

        void Apply(Color color, float intensity)
        {
            m_Light.color = color;
            m_Light.intensity = intensity;
            m_Light.enabled = intensity > 0.001f;
            if (Panels == null || Panels.Length == 0) return;
            // The panel glows with the lamp (property block: the shared material stays untouched).
            float glow = Mathf.Clamp01(intensity / LightMath.RoomIntensity);
            if (Mathf.Abs(glow - m_LastGlow) < 0.02f && color == m_LastColor) return;
            m_LastGlow = glow;
            m_LastColor = color;
            m_Block ??= new MaterialPropertyBlock();
            m_Block.SetColor(EmissionId, color * (LightMath.PanelGlow * glow));
            foreach (var p in Panels) if (p != null) p.SetPropertyBlock(m_Block);
        }
    }

    /// <summary>Light rules (pure, unit tested): flicker, URP-like falloff, how visible a lit / dark player is.</summary>
    public static class LightMath
    {
        /// <summary>Night: no light comes from nowhere. A closed room without lamps is pitch black.</summary>
        public static readonly Color NightAmbient = Color.black;
        /// <summary>Fog indoors (dark, only takes the distance away) and the gas outside.</summary>
        public static readonly Color NightFog = new(0.012f, 0.014f, 0.02f);
        public static readonly Color GasFog = new(0.33f, 0.37f, 0.3f);
        /// <summary>Exponential-squared density of the gas outside: ~95% fog at 1.5 m.</summary>
        public const float GasFogDensity = 1.15f;

        /// <summary>Intensity of a normal ceiling lamp (the panel glows fully at it).</summary>
        public const float RoomIntensity = 3.6f;
        /// <summary>Every lamp gets at least this during an alarm.</summary>
        public const float AlarmIntensity = 1.4f;
        /// <summary>Emission of a lit panel (HDR, catches the bloom).</summary>
        public const float PanelGlow = 2.2f;

        /// <summary>Ceiling lamp cookie (pure): full light below the horizon, none straight up (dir.y = direction's height).</summary>
        public static float CookieUpFade(float dirY) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.02f, 0.2f, dirY));

        /// <summary>Broken fluorescent tube: mostly on, with short irregular drop-outs. Returns 0..1.</summary>
        public static float Flicker(float t)
        {
            float n = Mathf.PerlinNoise(t * 3.1f, 0.37f);
            float burst = Mathf.PerlinNoise(t * 17f, 5.1f);
            return n < 0.3f ? (burst > 0.5f ? 0.05f : 0.6f) : 1f;
        }

        /// <summary>
        /// How a point / spot light fades with distance, as URP draws it: inverse square, smoothly cut to 0 at the range.
        /// </summary>
        public static float Attenuation(float distance, float range)
        {
            if (range <= 0f || distance >= range) return 0f;
            float d2 = distance * distance;
            float f = d2 / (range * range);
            float smooth = Mathf.Clamp01(1f - f * f);
            return smooth * smooth / Mathf.Max(d2, 0.25f); // closer than 0.5 m counts as 0.5 m: no infinite hot spots
        }

        /// <summary>Spot cone: 1 inside the inner angle, fading to 0 at the outer one (degrees, full angles).</summary>
        public static float SpotFactor(float angleFromAxis, float innerAngle, float outerAngle)
        {
            float cosA = Mathf.Cos(angleFromAxis * Mathf.Deg2Rad);
            float cosOuter = Mathf.Cos(outerAngle * 0.5f * Mathf.Deg2Rad);
            float cosInner = Mathf.Cos(Mathf.Min(innerAngle, outerAngle - 0.1f) * 0.5f * Mathf.Deg2Rad);
            float t = Mathf.Clamp01((cosA - cosOuter) / Mathf.Max(cosInner - cosOuter, 1e-4f));
            return t * t;
        }
    }
}
