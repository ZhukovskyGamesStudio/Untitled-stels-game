using Stealth.Game;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>Room ceiling light: optional flicker, red pulse during an alarm. Purely visual, runs on every peer.</summary>
    [RequireComponent(typeof(Light))]
    public class RoomLight : MonoBehaviour
    {
        public bool Flicker;
        public float Seed;

        Light m_Light;
        Color m_BaseColor;
        float m_BaseIntensity;

        public void Init(Color color, float intensity, bool flicker, float seed)
        {
            m_Light = GetComponent<Light>();
            m_BaseColor = color;
            m_BaseIntensity = intensity;
            Flicker = flicker;
            Seed = seed;
            m_Light.color = color;
            m_Light.intensity = intensity;
        }

        void Update()
        {
            if (m_Light == null) return;
            float k = Flicker ? LightMath.Flicker(Time.time + Seed) : 1f;
            var gm = GameManager.Instance;
            bool alarm = gm != null && gm.AlarmActive;
            m_Light.color = alarm ? Color.Lerp(m_BaseColor, new Color(1f, 0.15f, 0.1f), 0.8f) : m_BaseColor;
            float baseIntensity = alarm ? Mathf.Max(m_BaseIntensity, 0.6f) : m_BaseIntensity; // alarm lights even dark rooms
            m_Light.intensity = baseIntensity * k * (alarm ? 0.7f + 0.3f * Mathf.Sin(Time.time * 6f) : 1f);
        }
    }

    public static class LightMath
    {
        /// <summary>Broken fluorescent tube: mostly on, with short irregular drop-outs. Returns 0..1.</summary>
        public static float Flicker(float t)
        {
            float n = Mathf.PerlinNoise(t * 3.1f, 0.37f);
            float burst = Mathf.PerlinNoise(t * 17f, 5.1f);
            return n < 0.3f ? (burst > 0.5f ? 0.05f : 0.6f) : 1f;
        }

        /// <summary>How much a robot's eyes see in a room: dark rooms halve the detection speed.</summary>
        public static float VisionFactor(bool dark) => dark ? 0.5f : 1f;
    }
}
