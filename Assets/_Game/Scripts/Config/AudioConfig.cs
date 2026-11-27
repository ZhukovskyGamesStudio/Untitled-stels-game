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
        [Tooltip("Doors opening and shutting (robots work them all the time: not too loud).")]
        public float Doors = 0.35f;
        [Tooltip("On a ladder: a boot on a rung, of a footstep's volume.")]
        public float LadderFeet = 0.9f;
        [Tooltip("On a ladder: a hand closing on a rung, of a footstep's volume (soft: skin on steel).")]
        public float LadderHands = 0.5f;
        [Tooltip("On the way down a ladder the feet and hands are this much quieter (the body lowers itself onto them).")]
        public float LadderDown = 0.75f;

        [Header("Footsteps")]
        [Tooltip("Metres per step: the faster you go, the quicker the steps.")]
        public float StepLength = 1.25f;
        [Tooltip("Relative loudness of a crouched / walking / running step.")]
        public float CrouchStep = 0.3f, WalkStep = 0.65f, RunStep = 1f;
        [Tooltip("Steps in water and on the wet sewer floor (the squelch, StepSurface.Wet): this share of any other step's loudness.")]
        public float WetStep = 0.5f;
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

    /// <summary>
    /// Proximity voice chat (Stealth.Audio.Voice*): the microphone chain (noise suppression, gain, gate), the codec, the
    /// jitter buffer, how voices sound in the house (distance, walls, rooms) and how loud they are for the robots.
    /// The player's own choices (mode, push-to-talk, sensitivity, volume) are in <see cref="Stealth.Audio.VoicePrefs"/>.
    /// </summary>
    [Serializable]
    public class VoiceSettings
    {
        [Header("Codec")]
        [Tooltip("Opus bitrate, bits per second (24000 = clear speech, 32000 = near studio, 48000 = music).")]
        public int Bitrate = 32000;
        [Tooltip("Expected packet loss, % (Opus adds that much redundancy so a lost packet is rebuilt from the next).")]
        public int PacketLoss = 10;

        [Header("Microphone")]
        [Tooltip("Voice activation: the gate opens when the denoiser's voice probability is at least this.")]
        public float VoiceProbability = 0.55f;
        [Tooltip("Voice activation: and the frame is at least this loud (dBFS) at sensitivity 0.5 (the player's slider moves it ±20 dB).")]
        public float GateDb = -48f;
        [Tooltip("Seconds the gate stays open after the last voiced frame (so word endings are not cut).")]
        public float Hangover = 0.4f;
        [Tooltip("Automatic gain: the level speech is brought to (dBFS).")]
        public float TargetDb = -20f;
        [Tooltip("Automatic gain never boosts more than this (dB): a quiet voice stays a bit quieter than a loud one.")]
        public float MaxGainDb = 15f;
        [Tooltip("Automatic gain never cuts more than this (dB).")]
        public float MaxCutDb = 12f;
        [Tooltip("Seconds for the gain to rise / to fall by most of the way.")]
        public float GainRise = 1.2f, GainFall = 0.15f;

        [Header("Network")]
        [Tooltip("Frames (20 ms each) the listener collects before a voice starts playing: the cushion against network jitter (it grows by itself on a jittery connection).")]
        public int JitterFrames = 3;
        [Tooltip("More frames than this buffered: the oldest are dropped to catch up.")]
        public int JitterMax = 12;
        [Tooltip("The cushion grows by at most this many frames on a jittery connection (one per late packet)...")]
        public int JitterGrow = 4;
        [Tooltip("...and shrinks by one after this many seconds without a late packet.")]
        public float JitterCalm = 8f;

        [Header("In the world")]
        public float Volume = 1f;
        [Tooltip("About full volume closer than this (m); the fade bends in smoothly around it.")]
        public float MinDistance = 3f;
        [Tooltip("Silent further than this (m).")]
        public float MaxDistance = 120f;
        [Tooltip("How fast a voice gets quieter with distance: far away gain ≈ (MinDistance / distance) ^ this (1 = inverse distance, real life; lower = carries further).")]
        public float Rolloff = 0.55f;
        [Tooltip("Share of MaxDistance from which the voice eases out to silence (a smooth S, no edge).")]
        public float FadeFrom = 0.45f;
        [Tooltip("Seconds a voice takes to follow a change of loudness or muffling (stepping behind a wall, a door closing): it glides, never jumps.")]
        public float Smoothing = 0.12f;
        [Tooltip("Low-pass cutoff (Hz) of a voice behind everything that blocks sound (a wall, a slab): it comes through dull.")]
        public float BlockedCutoff = 700f;
        [Tooltip("Share of volume left at the most blocked (transmission 0).")]
        public float BlockedVolume = 0.08f;
        [Tooltip("How strongly rooms ring: 0 dry everywhere, 1 as designed, more = cathedral.")]
        public float Reverb = 1f;
        [Tooltip("Seconds between the acoustic checks of a voice (walls, rooms).")]
        public float AcousticsEvery = 0.1f;

        [Header("Robots hear it")]
        [Tooltip("Noise radius (m) of a whisper (raw microphone level at WhisperDb, before gain).")]
        public float WhisperRadius = 2f;
        [Tooltip("Noise radius (m) of normal speech at SpeechDb.")]
        public float SpeechRadius = 8f;
        [Tooltip("Noise radius (m) of a shout at ShoutDb and above.")]
        public float ShoutRadius = 20f;
        [Tooltip("Raw microphone levels (dBFS) of a whisper, speech, a shout (after noise suppression, before gain).")]
        public float WhisperDb = -42f, SpeechDb = -26f, ShoutDb = -10f;
        [Tooltip("Seconds between the noises a talking player makes for the robots.")]
        public float NoiseEvery = 0.25f;
    }

    /// <summary>Sound mix (Data/Config/Audio.asset). The clips themselves are in Data/SoundBank.asset.</summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Audio", fileName = "Audio")]
    public class AudioConfig : ScriptableObject
    {
        public AudioMix Mix = new();
        public VoiceSettings Voice = new();
    }
}
