using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Audio
{
    /// <summary>
    /// A guard's own voice loop, on every peer, by what it is doing (replicated state): a humming charge while it rests on
    /// its dock, a purr and a chitter while it walks its rounds or goes to check something, a fast aggressive warble while
    /// it chases — a player hears from the next room which of the three it is. Fades between them; floors muffle it
    /// (<see cref="AudioDirector.Muffle"/>); silent while hacked.
    /// </summary>
    public class GuardSounds : MonoBehaviour
    {
        public AudioSource Source;
        public AudioClip Charging, Patrolling, Chasing;
        [Tooltip("Loudness of each, over the loops' volume.")]
        public float ChargingGain = 0.7f, PatrollingGain = 0.9f, ChasingGain = 1.4f;

        RobotController m_Robot;
        AudioClip m_Want;
        float m_Gain, m_Fade, m_NextMuffle, m_Muffle = 1f;

        void Awake() => m_Robot = GetComponent<RobotController>();

        void Start()
        {
            if (Source == null) return;
            var mix = Tuning.Audio.Mix;
            Source.loop = true;
            Source.spatialBlend = 1f;
            Source.rolloffMode = AudioRolloffMode.Linear;
            Source.minDistance = mix.MinDistance * 0.5f;
            Source.maxDistance = mix.LoopMaxDistance * 1.4f;
            Source.dopplerLevel = 0f;
            Source.volume = 0f;
        }

        /// <summary>Which loop and how loud (pure): resting → charging; chasing or warned → chasing; otherwise walking about.</summary>
        public static (int clip, float gain) Pick(bool resting, RobotState state, bool hacked)
        {
            if (hacked) return (-1, 0f);
            if (resting) return (0, 1f);
            return state is RobotState.Chase or RobotState.Alerted ? (2, 1f) : (1, 1f);
        }

        void Update()
        {
            if (Source == null || m_Robot == null) return;
            var (clip, gain) = Pick(m_Robot.RestingShown, m_Robot.State, m_Robot.IsHacked);
            var want = clip switch { 0 => Charging, 1 => Patrolling, 2 => Chasing, _ => null };
            float g = clip switch { 0 => ChargingGain, 1 => PatrollingGain, 2 => ChasingGain, _ => 0f } * gain;
            if (want != m_Want) { m_Want = want; m_Gain = g; }
            // fade out the old loop, swap, fade the new one in
            float target = Source.clip == m_Want ? m_Gain : 0f;
            m_Fade = Mathf.MoveTowards(m_Fade, target, Time.deltaTime * 2.5f);
            if (Source.clip != m_Want && m_Fade <= 0.001f)
            {
                Source.clip = m_Want;
                if (m_Want != null) { Source.time = LevelRandom.Shared(RandomChannel.Audio).Range(0f, m_Want.length * 0.9f); Source.Play(); }
                else Source.Stop();
            }
            if (Time.time >= m_NextMuffle)
            {
                m_NextMuffle = Time.time + 0.2f;
                m_Muffle = AudioDirector.Instance != null ? AudioDirector.Instance.Muffle(transform.position) : 1f;
            }
            Source.volume = m_Fade * Tuning.Audio.Mix.Loops * m_Muffle;
        }
    }
}
