using Stealth.Game;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// What a patch left by a robot looks like (<see cref="Hazards"/>): the cleaner's goo puddle, a cloud of smoke or of
    /// gas. A prefab instantiated on every peer where the patch is announced; it grows to the patch's radius, fades out
    /// and removes itself when the patch is gone (by the shared clock).
    /// </summary>
    public class HazardView : MonoBehaviour
    {
        [Tooltip("What this prefab shows.")]
        public HazardKind Kind;
        [Tooltip("Scaled to the patch's radius (the model is one metre in radius).")]
        public Transform Body;
        [Tooltip("The cloud's particles (stopped a moment before the end so it thins out).")]
        public ParticleSystem Cloud;

        /// <summary>Seconds a patch takes to grow in and to shrink away.</summary>
        public const float Grow = 0.8f;

        float m_Radius;
        double m_Born, m_Until;

        /// <summary>How big the patch looks <paramref name="age"/> seconds in with <paramref name="left"/> seconds to go: 0 … 1 of its radius (pure).</summary>
        public static float Share(float age, float left) => Mathf.Clamp01(Mathf.Min(age, left) / Grow);

        /// <summary>Every peer: show a patch of this radius until this server time.</summary>
        public void Show(float radius, double until)
        {
            m_Radius = radius;
            m_Born = Session.ServerTime;
            m_Until = until;
            if (Cloud != null)
            {
                var shape = Cloud.shape;
                shape.radius = radius * 0.8f;
            }
            Update();
        }

        void Update()
        {
            double now = Session.ServerTime;
            if (now >= m_Until) { Destroy(gameObject); return; }
            float share = Share((float)(now - m_Born), (float)(m_Until - now));
            if (Body != null) Body.localScale = new Vector3(m_Radius * share, Kind == HazardKind.Goo ? 1f : m_Radius * share, m_Radius * share);
            if (Cloud != null && Cloud.isEmitting && m_Until - now < Cloud.main.startLifetime.constantMax) Cloud.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
