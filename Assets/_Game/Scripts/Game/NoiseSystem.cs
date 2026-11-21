using System;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// Server-side noise events. Robots with microphones subscribe to them.
    /// Footsteps and laughing (gas) emit noise; the voice chat (VoiceHub) emits <see cref="EmitVoiceServer"/> with a
    /// radius from the speaker's microphone level.
    /// </summary>
    public static class NoiseSystem
    {
        /// <summary>position, radius (meters), source client id</summary>
        public static event Action<Vector3, float, ulong> OnNoise;

        public const ulong NoSource = ulong.MaxValue;

        /// <summary>A player's voice in the chat (VoiceHub): devices with microphones listen only to these.</summary>
        public static event Action<Vector3, float, ulong> OnVoice;

        public static void EmitServer(Vector3 position, float radius, ulong sourceClientId = NoSource)
        {
            if (radius <= 0f) return;
            OnNoise?.Invoke(position, radius, sourceClientId);
        }

        /// <summary>A talking player: a noise for the robots like any other, and a voice for the devices' microphones.</summary>
        public static void EmitVoiceServer(Vector3 position, float radius, ulong sourceClientId)
        {
            if (radius <= 0f) return;
            OnNoise?.Invoke(position, radius, sourceClientId);
            OnVoice?.Invoke(position, radius, sourceClientId);
        }
    }
}
