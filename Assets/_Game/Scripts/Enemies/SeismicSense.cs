using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Floor vibration rules (pure, unit tested). A seismic robot stomps; a wave runs out along its floor, through walls.
    /// The wave feels whoever is moving on the floor when it passes — freeze and it goes by.
    /// </summary>
    public static class SeismicSense
    {
        /// <summary>Radius of the wave <paramref name="age"/> seconds after the stomp.</summary>
        public static float WaveRadius(float age, float waveSpeed) => Mathf.Max(0f, age) * waveSpeed;

        /// <summary>Is the wave front at the player now (front thickness covers one frame of travel + a body)?</summary>
        public static bool WaveTouches(float horizontalDistance, float radius, float waveSpeed, float dt) =>
            Mathf.Abs(horizontalDistance - radius) <= waveSpeed * dt + 0.6f;

        /// <summary>
        /// Suspicion a wave adds: nothing for a player standing still (or creeping slower than <see cref="StillSpeed"/>),
        /// more for running, less crouched, weaker far out.
        /// </summary>
        public static float WaveSuspicion(float strength, float playerSpeed, bool crouching, float distance, float range, VibrationSettings s = null)
        {
            if (playerSpeed < StillSpeed || distance > range) return 0f;
            float far = Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(distance / range));
            return strength * SensorMath.SpeedFactor(playerSpeed, s) * (crouching ? 0.35f : 1f) * far;
        }

        /// <summary>Slower than this counts as standing still for the floor waves.</summary>
        public const float StillSpeed = 0.4f;

    }
}
