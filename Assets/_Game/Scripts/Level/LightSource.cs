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

        Light m_Light;

        public Light Light => m_Light != null ? m_Light : m_Light = GetComponent<Light>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Brightness right now (intensity × luminance of the colour); 0 when off.</summary>
        public float Brightness
        {
            get
            {
                var l = Light;
                if (l == null || !l.isActiveAndEnabled || l.intensity <= 0f) return 0f;
                var c = l.color;
                return l.intensity * (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b);
            }
        }
    }
}
