using System;
using UnityEngine;

namespace Stealth.Configs
{
    /// <summary>Mix of the game's sound: volumes per group, hearing distances, footsteps, music changes.</summary>
    [Serializable]
    public class AudioMix
    {
        [Header("Volumes (0..1)")]
        public float Music = 0.4f;
        [Tooltip("Office hum inside, gas wind outside.")]
        public float Ambience = 0.35f;
        public float Alarm = 0.45f;
        public float Ui = 0.6f;
        public float Footsteps = 0.55f;
        [Tooltip("Robot beeps, turret shots, radio.")]
        public float Robots = 0.8f;
        [Tooltip("Pickup, drop, sell.")]
        public float Items = 0.8f;
        [Tooltip("Looping sounds of robots and lasers.")]
        public float Loops = 0.3f;

        [Header("Distances (m)")]
        [Tooltip("Full volume closer than this.")]
        public float MinDistance = 1.5f;
        [Tooltip("Silent further than this (one-shots).")]
        public float MaxDistance = 24f;
        [Tooltip("Silent further than this (robot motors, laser hum).")]
        public float LoopMaxDistance = 11f;

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
