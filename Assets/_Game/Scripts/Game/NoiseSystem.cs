using System;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// Server-side noise events. Robots with microphones subscribe to them.
    /// Footsteps and laughing (gas) already emit noise; voice chat should call <see cref="EmitServer"/> with the
    /// speaker's position and a radius derived from the microphone loudness.
    /// </summary>
    public static class NoiseSystem
    {
        /// <summary>position, radius (meters), source client id</summary>
        public static event Action<Vector3, float, ulong> OnNoise;

        public const ulong NoSource = ulong.MaxValue;

        public static void EmitServer(Vector3 position, float radius, ulong sourceClientId = NoSource)
        {
            if (radius <= 0f) return;
            OnNoise?.Invoke(position, radius, sourceClientId);
        }
    }
}
