using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A light that matters for stealth: robots see a player as well as the lights around light them up
    /// (<see cref="Enemies.Perception.Illumination"/>). Sits next to a <see cref="UnityEngine.Light"/> on ceiling lamps,
    /// desk and floor lamps, exit signs, vending machines, street lamps and the moon. Reads the light as it is right now:
    /// switched off, flickering or pulsing red during an alarm.
    /// </summary>
    [RequireComponent(typeof(Light)), ExecuteAlways]
    public class LightSource : MonoBehaviour
    {
        public static readonly List<LightSource> All = new();

        /// <summary>
        /// How bright the lights LOOK in the current visual style (<see cref="Configs.StyleWorld.LightScale"/>). Purely visual:
        /// <see cref="Brightness"/> keeps reading the intensity the game set, so robots see exactly the same in every style.
        /// </summary>
        public static float VisualScale = 1f;

        /// <summary>How bright the glowing lamp diffusers look in the current style (<see cref="Configs.StyleWorld.LampGlow"/>). Visual only.</summary>
        public static float GlowScale = 1f;

        Light m_Light;
        float m_Raw = -1f;      // the intensity the game set (lamp logic, flicker, alarm)
        float m_Shown = -1f;    // what we wrote into the light for the eye

        public Light Light => m_Light != null ? m_Light : m_Light = GetComponent<Light>();

        void OnEnable() => All.Add(this);
        void OnDisable()
        {
            All.Remove(this);
            if (m_Raw >= 0f && Light != null) Light.intensity = m_Raw; // leave the light as the game set it
            m_Raw = m_Shown = -1f;
        }

        /// <summary>The intensity the game set, without the visual scale of the style.</summary>
        public float RawIntensity
        {
            get
            {
                var l = Light;
                if (l == null) return 0f;
                return LightMath.RawIntensity(l.intensity, m_Raw, m_Shown);
            }
        }

        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            var l = Light;
            if (l == null) return;
            m_Raw = LightMath.RawIntensity(l.intensity, m_Raw, m_Shown);
            m_Shown = m_Raw * VisualScale;
            l.intensity = m_Shown;
        }

        /// <summary>Brightness right now (raw intensity × luminance of the colour); 0 when off. Ignores the style's scale.</summary>
        public float Brightness
        {
            get
            {
                var l = Light;
                float raw = RawIntensity;
                if (l == null || !l.isActiveAndEnabled || raw <= 0f) return 0f;
                var c = l.color;
                return raw * (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b);
            }
        }
    }
}
