using System;
using UnityEngine;

namespace Stealth.Configs
{
    /// <summary>Mix of the game's sound: volumes per group, hearing distances, footsteps, music changes.</summary>
    [Serializable]
    public class AudioMix
    {
        [Header("Volumes (0..1)")]
        public float Music = 0.35f;
        [Tooltip("Music in the houses and the sewer (off = only the menu plays music).")]
        public bool LevelMusic = true;
        [Tooltip("Office hum inside, gas wind outside.")]
        public float Ambience = 0.3f;
        public float Alarm = 0.3f;
        public float Ui = 0.5f;
        public float Footsteps = 0.6f;
        [Tooltip("Robot beeps, turret shots, radio.")]
        public float Robots = 0.8f;
        [Tooltip("Pickup, drop, sell.")]
        public float Items = 0.6f;
        [Tooltip("Looping sounds of robots and lasers.")]
        public float Loops = 0.3f;

        [Header("Distances (m)")]
        [Tooltip("Full volume closer than this.")]
        public float MinDistance = 1.5f;
        [Tooltip("Silent further than this (one-shots).")]
        public float MaxDistance = 24f;
        [Tooltip("Silent further than this (robot motors, laser hum).")]
        public float LoopMaxDistance = 11f;
        [Tooltip("Share of a sound that gets through one floor slab (robots upstairs / downstairs are muffled, not loud).")]
        public float ThroughFloor = 0.12f;
        [Tooltip("Automatic doors opening and closing (they do it all the time: keep it low).")]
        public float Doors = 0.18f;

        [Header("Footsteps")]
        [Tooltip("Metres per step: the faster you go, the quicker the steps.")]
        public float StepLength = 1.25f;
        [Tooltip("Relative loudness of a crouched / walking / running step.")]
        public float CrouchStep = 0.3f, WalkStep = 0.65f, RunStep = 1f;
        [Tooltip("Speed (m/s) above which steps count as running.")]
        public float RunSpeed = 5f;

        [Header("Music")]
        [Tooltip("Seconds to crossfade between tracks.")]
        public float Crossfade = 1.5f;
        [Tooltip("The exploring track is a quiet bed under the footsteps: its share of the music volume.")]
        public float ExploreMusic = 0.45f;
        [Tooltip("The chase music keeps playing this long after the last chase / alarm.")]
        public float ChaseHold = 5f;
    }

    /// <summary>Sound mix (Data/Config/Audio.asset). The clips themselves are in Data/SoundBank.asset.</summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Audio", fileName = "Audio")]
    public class AudioConfig : ScriptableObject
    {
        public AudioMix Mix = new();
    }
}
