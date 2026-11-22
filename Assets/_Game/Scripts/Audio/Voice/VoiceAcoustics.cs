using Stealth.Configs;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Audio
{
    /// <summary>What a room does to a voice: how its surfaces ring.</summary>
    public enum VoiceSpace : byte
    {
        /// <summary>The street and the yard: no walls to ring.</summary>
        Outdoors,
        /// <summary>Carpet, sofas, books, beds: short and dull.</summary>
        Soft,
        /// <summary>Plaster, lino, glass, racks: the ordinary hard room.</summary>
        Hard,
        /// <summary>Tiles and enamel: bright, ringing (toilets, bathrooms, kitchens, theatres).</summary>
        Tiled,
        /// <summary>Big halls: a long tail (lobby, gym, workshop).</summary>
        Hall,
        /// <summary>Steel and concrete stores: metallic flutter.</summary>
        Metal,
        /// <summary>The basement, the sewer, the tunnels: a long tail and a slap-back echo off the far walls.</summary>
        Tunnel,
    }

    /// <summary>Parameters of Unity's reverb and echo filters for a voice in some room (levels in millibels).</summary>
    public struct VoiceReverb
    {
        public float Decay, Room, RoomHF, Reflections, Reverb, Diffusion, Density;
        /// <summary>Slap-back echo: delay (s, 0 = none) and how much of each repeat comes back.</summary>
        public float EchoDelay, EchoDecay, EchoWet;

        public bool IsDry => Room <= -9999f;

        public static readonly VoiceReverb Dry = new()
        {
            Decay = 0.1f, Room = -10000f, RoomHF = -10000f, Reflections = -10000f, Reverb = -10000f, Diffusion = 100f, Density = 100f,
        };
    }

    /// <summary>
    /// How a voice sounds where it is (pure): distance falloff, the dull and quiet sound of a voice behind walls and
    /// slabs (<see cref="Stealth.Enemies.Perception.SoundTransmission"/> — the same walls the robots hear through), and the
    /// ring of the speaker's room from its type and size (a tiled toilet echoes, an office is dry, the sewer booms).
    /// </summary>
    public static class VoiceAcoustics
    {
        /// <summary>
        /// Gain at this distance: about full inside <see cref="VoiceSettings.MinDistance"/>, then much softer than inverse
        /// distance (<see cref="VoiceSettings.Rolloff"/>: a co-op game, a mate down the corridor is still heard), easing
        /// out to silence at the far limit along a smooth S from <see cref="VoiceSettings.FadeFrom"/> — no corner anywhere
        /// in the curve, so walking away is one even fade.
        /// </summary>
        /// <summary>How far away a voice over the radio sounds: right next to you, metres.</summary>
        public const float RadioDistance = 0.6f;

        /// <summary>
        /// A voice over the radio in the listener's hand: as loud and as clear as a speaker standing right by (never
        /// quieter than at <see cref="RadioDistance"/>, nothing muffled by walls or distance).
        /// </summary>
        public static (float gain, float cutoff) OverRadio(float gain, VoiceSettings s) =>
            (Mathf.Max(gain, DistanceGain(RadioDistance, s)), 22000f);

        public static float DistanceGain(float distance, VoiceSettings s)
        {
            if (distance >= s.MaxDistance) return 0f;
            // flat at the mouth and bending smoothly into (min / d) ^ rolloff further out: no knee at MinDistance
            float min = Mathf.Max(0.1f, s.MinDistance), q = distance / min;
            float g = Mathf.Pow(1f + q * q, -0.5f * Mathf.Max(0.05f, s.Rolloff));
            float from = s.MaxDistance * Mathf.Clamp(s.FadeFrom, 0f, 0.95f);
            if (distance > from)
            {
                float t = (distance - from) / (s.MaxDistance - from);
                g *= 1f - t * t * (3f - 2f * t); // smoothstep: flat where it starts and where it ends
            }
            return g;
        }

        /// <summary>
        /// One step of following a target over time (most of the way in <paramref name="seconds"/>): the voice's gain and
        /// muffling glide when a wall comes between instead of jumping.
        /// </summary>
        public static float Follow(float current, float target, float dt, float seconds) =>
            seconds <= 0f ? target : current + (target - current) * (1f - Mathf.Exp(-dt / seconds));

        /// <summary>Volume share and low-pass cutoff (Hz) of a voice that reaches the listener with this transmission.</summary>
        public static void Blocked(float transmission, VoiceSettings s, out float volume, out float cutoff)
        {
            float t = Mathf.Clamp01(transmission);
            volume = Mathf.Lerp(s.BlockedVolume, 1f, Mathf.Sqrt(t));
            // a wall takes the highs first: even a door (0.6) makes it noticeably duller
            cutoff = t >= 0.999f ? 22000f : Mathf.Exp(Mathf.Lerp(Mathf.Log(s.BlockedCutoff), Mathf.Log(9000f), t * t));
        }

        public static VoiceSpace SpaceOf(RoomType type) => type switch
        {
            RoomType.Office or RoomType.OpenSpace or RoomType.Meeting or RoomType.Lounge or RoomType.Library or RoomType.NapRoom
                or RoomType.Studio or RoomType.GameRoom or RoomType.Ward or RoomType.ControlRoom or RoomType.Bedroom
                or RoomType.Living => VoiceSpace.Soft,
            RoomType.Restroom or RoomType.Bathroom or RoomType.LockerRoom or RoomType.Kitchen or RoomType.Operating => VoiceSpace.Tiled,
            RoomType.Lobby or RoomType.Gym or RoomType.Workshop => VoiceSpace.Hall,
            RoomType.Storage or RoomType.PaintBooth or RoomType.Utility or RoomType.ChargingStation => VoiceSpace.Metal,
            _ => VoiceSpace.Hard, // corridors, halls, server rooms, labs, copy rooms, the clinic's rooms, flat hallways
        };

        /// <summary>
        /// The ring of a room of this kind and size (floor area m², height m), scaled by <paramref name="amount"/>
        /// (<see cref="VoiceSettings.Reverb"/>): bigger rooms ring longer, a hall's tail is longer than a toilet's.
        /// </summary>
        public static VoiceReverb ReverbOf(VoiceSpace space, float area, float height, float amount)
        {
            if (space == VoiceSpace.Outdoors || amount <= 0.001f) return VoiceReverb.Dry;
            VoiceReverb r = space switch
            {
                VoiceSpace.Soft => new VoiceReverb { Decay = 0.45f, Room = -1600f, RoomHF = -1400f, Reflections = -1400f, Reverb = -1800f, Diffusion = 100f, Density = 100f },
                VoiceSpace.Tiled => new VoiceReverb { Decay = 1.4f, Room = -800f, RoomHF = -150f, Reflections = -400f, Reverb = 150f, Diffusion = 75f, Density = 60f },
                VoiceSpace.Hall => new VoiceReverb { Decay = 2.4f, Room = -1000f, RoomHF = -500f, Reflections = -1100f, Reverb = -350f, Diffusion = 100f, Density = 100f },
                VoiceSpace.Metal => new VoiceReverb { Decay = 1.7f, Room = -900f, RoomHF = -300f, Reflections = -600f, Reverb = -250f, Diffusion = 55f, Density = 80f },
                VoiceSpace.Tunnel => new VoiceReverb { Decay = 3.2f, Room = -700f, RoomHF = -700f, Reflections = -700f, Reverb = -200f, Diffusion = 90f, Density = 100f,
                    EchoDelay = 0.3f, EchoDecay = 0.3f, EchoWet = 0.25f },
                _ => new VoiceReverb { Decay = 0.9f, Room = -1100f, RoomHF = -600f, Reflections = -900f, Reverb = -700f, Diffusion = 100f, Density = 100f },
            };
            float reference = space == VoiceSpace.Hall ? 9f : space == VoiceSpace.Tunnel ? 7f : 4.5f; // side of a typical room of the kind
            float side = Mathf.Pow(Mathf.Max(1f, area) * Mathf.Max(2f, height), 1f / 3f);
            float scale = Mathf.Clamp(side / reference, 0.6f, 1.8f);
            r.Decay = Mathf.Clamp(r.Decay * scale, 0.1f, 20f);
            if (r.EchoDelay > 0f) r.EchoDelay = Mathf.Clamp(r.EchoDelay * scale, 0.12f, 0.6f);
            // less (or more) of everything: -6 dB per halving of the amount, a bit longer when above 1
            float db = 2000f * Mathf.Log10(Mathf.Clamp(amount, 0.01f, 2f));
            r.Room = Mathf.Clamp(r.Room + db, -10000f, 0f);
            r.Reverb = Mathf.Clamp(r.Reverb + db, -10000f, 2000f);
            r.Reflections = Mathf.Clamp(r.Reflections + db, -10000f, 1000f);
            r.EchoWet *= Mathf.Clamp01(amount);
            return r;
        }
    }
}
