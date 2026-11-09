using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Expanding ring over a robot that radios for backup (prefab FX/RadioRipple: a looped LineRenderer).
    /// Spawned on every peer, destroys itself when done.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class RadioRipple : MonoBehaviour
    {
        public float Duration = 0.6f;
        public float StartRadius = 0.3f;
        public float EndRadius = 4f;
        public float Height = 0.3f;
        public float Width = 0.06f;

        LineRenderer m_Line;
        float m_T;

        /// <summary>Ring radius at <paramref name="t"/> seconds (pure).</summary>
        public static float RadiusAt(float t, float duration, float start, float end) => Mathf.Lerp(start, end, t / duration);

        void Awake() => m_Line = GetComponent<LineRenderer>();

        void Update()
        {
            m_T += Time.deltaTime;
            float r = RadiusAt(m_T, Duration, StartRadius, EndRadius);
            int n = m_Line.positionCount;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                m_Line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Height, Mathf.Sin(a) * r));
            }
            m_Line.widthMultiplier = Width * Mathf.Clamp01(1f - m_T / Duration);
            if (m_T > Duration) Destroy(gameObject);
        }
    }
}
