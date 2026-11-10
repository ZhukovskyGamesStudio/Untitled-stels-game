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
            Source.time = Random.Range(0f, Source.clip.length * 0.95f);
            Source.Play();
        }

        /// <summary>Muted while switched off (a hacked robot, a laser that is off).</summary>
        public void SetActive(bool on)
        {
            float target = on ? Tuning.Audio.Mix.Loops : 0f;
            if (!Mathf.Approximately(Source.volume, target)) Source.volume = target;
        }
    }
}
