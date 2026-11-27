using Stealth.Level;
using Stealth.Configs;
using UnityEngine;

namespace Stealth.Audio
{
    /// <summary>
    /// A looping 3D sound on a prefab (robot motor, laser hum). The prefab holds the source and the clip;
    /// loudness and range come from the audio config. Starts at a random point so that two robots never hum in sync.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class LoopSound : MonoBehaviour
    {
        public AudioSource Source;

        bool m_On = true;
        float m_NextMuffle;

        void Start()
        {
            var mix = Tuning.Audio.Mix;
            Source.loop = true;
            Source.spatialBlend = 1f;
            Source.rolloffMode = AudioRolloffMode.Linear;
            Source.minDistance = mix.MinDistance * 0.5f;
            Source.maxDistance = mix.LoopMaxDistance;
            Source.volume = mix.Loops;
            Source.dopplerLevel = 0f;
            if (Source.clip == null) return;
            Source.time = Stealth.Level.LevelRandom.Shared(Stealth.Level.RandomChannel.Audio).Range(0f, Source.clip.length * 0.95f);
            Source.Play();
        }

        /// <summary>Sounding now (not switched off).</summary>
        public bool On => m_On;

        /// <summary>Muted while switched off (a hacked robot: <see cref="Enemies.Detector"/> switches it).</summary>
        public void SetActive(bool on)
        {
            if (m_On == on) return;
            m_On = on;
            m_NextMuffle = 0f;
        }

        /// <summary>The floors between it and the listener muffle it (<see cref="AudioDirector.Muffle"/>).</summary>
        void Update()
        {
            if (Time.time < m_NextMuffle) return;
            m_NextMuffle = Time.time + 0.2f;
            float muffle = AudioDirector.Instance != null ? AudioDirector.Instance.Muffle(transform.position) : 1f;
            float target = m_On ? Tuning.Audio.Mix.Loops * muffle : 0f;
            if (!Mathf.Approximately(Source.volume, target)) Source.volume = target;
        }
    }
}
