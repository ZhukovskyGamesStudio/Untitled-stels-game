using Stealth.Game;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Ceiling lamp of a room: a spot light shining down with soft shadows and a glowing panel. In a dark room it is off
    /// (the panel too); a broken one flickers; during an alarm every lamp pulses red, even in dark rooms. Purely visual,
    /// runs on every peer; robots read the result through its <see cref="LightSource"/>.
    /// </summary>
    public class RoomLight : MonoBehaviour
    {
        [Tooltip("The spot light shining down (a child: lights point along their local +Z).")]
        public Light Lamp;
        public bool Flicker;
        public float Seed;
        [Tooltip("The glowing diffuser of the fixture: its emission follows the light.")]
        public Renderer Panel;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly Color AlarmColor = new(1f, 0.15f, 0.1f);

        Light m_Light;
        Color m_BaseColor;
        float m_BaseIntensity;
        MaterialPropertyBlock m_Block;
        float m_LastGlow = -1f;
        Color m_LastColor;

        /// <param name="castShadows">Only one lamp of a room casts shadows: enough for the look, half the cost.</param>
        public void Init(Color color, float intensity, bool flicker, float seed, bool castShadows = true)
        {
            m_Light = Lamp != null ? Lamp : GetComponentInChildren<Light>();
            if (!castShadows) m_Light.shadows = LightShadows.None;
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
            if (Panel == null) return;
            // The panel glows with the lamp (property block: the shared material stays untouched).
            float glow = Mathf.Clamp01(intensity / LightMath.RoomIntensity);
            if (Mathf.Abs(glow - m_LastGlow) < 0.02f && color == m_LastColor) return;
            m_LastGlow = glow;
            m_LastColor = color;
            m_Block ??= new MaterialPropertyBlock();
            m_Block.SetColor(EmissionId, color * (LightMath.PanelGlow * glow));
            Panel.SetPropertyBlock(m_Block);
        }
    }

    /// <summary>Light rules (pure, unit tested): flicker, URP-like falloff, how visible a lit / dark player is.</summary>
    public static class LightMath
    {
        /// <summary>Intensity of a normal ceiling lamp (the panel glows fully at it).</summary>
        public const float RoomIntensity = 2.6f;
        /// <summary>Every lamp gets at least this during an alarm.</summary>
        public const float AlarmIntensity = 1.4f;
        /// <summary>Emission of a lit panel (HDR, catches the bloom).</summary>
        public const float PanelGlow = 2.2f;

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
