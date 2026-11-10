using System.Collections.Generic;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// How strongly each sensor channel notices a player (pure, unit tested). Nothing is all-or-nothing: the worse a sensor
    /// sees, hears or feels a player, the slower its suspicion grows.
    /// </summary>
    public static class SensorMath
    {
        static readonly VisionSettings s_Vision = new();
        static readonly HearingSettings s_Hearing = new();
        static readonly VibrationSettings s_Vibration = new();

        // ------------------------------------------------------------------ eyes

        /// <summary>Closer is faster: from <see cref="VisionSettings.FarFactor"/> at the range to <see cref="VisionSettings.NearFactor"/> up close.</summary>
        public static float DistanceFactor(float distance, float range, VisionSettings v = null)
        {
            v ??= s_Vision;
            float closeness = 1f - Mathf.Clamp01(distance / Mathf.Max(range, 0.01f));
            return Mathf.Lerp(v.FarFactor, v.NearFactor, closeness * closeness);
        }

        /// <summary>A partly hidden body is noticed much slower; a sliver of it — not at all.</summary>
        public static float ExposureFactor(float exposure, VisionSettings v = null)
        {
            v ??= s_Vision;
            return exposure < v.MinExposure ? 0f : Mathf.Pow(Mathf.Clamp01(exposure), v.ExposurePower);
        }

        /// <summary>Full speed in the middle of the view, slower towards its edge, much slower outside it (close behind).</summary>
        public static float ViewFactor(float offCentre, VisionSettings v = null)
        {
            v ??= s_Vision;
            if (offCentre > 1.001f) return v.BehindFactor;
            return Mathf.Lerp(1f, v.EdgeOfViewFactor, Mathf.InverseLerp(v.FullViewShare, 1f, offCentre));
        }

        /// <summary>
        /// Multiplier of an eye's own speed for this look: distance × visible share × place in the view × stance × motion × light.
        /// <paramref name="crouchFactor"/> &lt; 0 takes <see cref="VisionSettings.CrouchFactor"/>.
        /// </summary>
        public static float VisionFactor(in Glimpse g, float crouchFactor = -1f, VisionSettings v = null)
        {
            v ??= s_Vision;
            float crouch = g.Crouching ? (crouchFactor >= 0f ? crouchFactor : v.CrouchFactor) : 1f;
            return DistanceFactor(g.Distance, g.Range, v) * ExposureFactor(g.Exposure, v) * ViewFactor(g.OffCentre, v)
                   * crouch * (g.Moving ? 1f : v.StillFactor) * g.Light;
        }

        // ------------------------------------------------------------------ ears

        /// <summary>
        /// Loudness at the listener, 0..1: fades with distance up to <paramref name="hearingRadius"/>, then whatever walls let through.
        /// </summary>
        public static float Loudness(float distance, float hearingRadius, float transmission) =>
            hearingRadius <= 0f ? 0f : Mathf.Clamp01(1f - distance / hearingRadius) * Mathf.Clamp01(transmission);

        /// <summary>Share of the loudness that passes through these materials (each one muffles it further).</summary>
        public static float Transmission(IEnumerable<SoundMaterial> between, HearingSettings h = null)
        {
            h ??= s_Hearing;
            float t = 1f;
            foreach (var m in between) t *= h.Transmission(m);
            return t;
        }

        /// <summary>Suspicion a single noise adds (quieter than <see cref="HearingSettings.MinLoudness"/> — none).</summary>
        public static float NoiseSuspicion(float loudness, HearingSettings h = null)
        {
            h ??= s_Hearing;
            return loudness < h.MinLoudness ? 0f : loudness * h.SuspicionPerNoise;
        }

        // ------------------------------------------------------------------ floor and motion

        /// <summary>Faster players shake the floor / trip motion sensors harder.</summary>
        public static float SpeedFactor(float playerSpeed, VibrationSettings s = null)
        {
            s ??= s_Vibration;
            return Mathf.Clamp(playerSpeed / Mathf.Max(s.ReferenceSpeed, 0.01f), s.MinSpeedFactor, s.MaxSpeedFactor);
        }

        /// <summary>Floor vibrations: stronger close by and when running. Call only when <see cref="SeismicSense.Feels"/> is true.</summary>
        public static float SeismicRate(float seismicSpeed, float horizontalDistance, float range, float playerSpeed, VibrationSettings s = null) =>
            seismicSpeed * Mathf.Lerp(0.5f, 2f, 1f - Mathf.Clamp01(horizontalDistance / range)) * SpeedFactor(playerSpeed, s);

        /// <summary>Motion sensor: faster movement, closer and more of the body in view — faster alarm.</summary>
        public static float MotionRate(float detectionRate, float playerSpeed, float distance, float range, float exposure,
            VibrationSettings s = null, VisionSettings v = null) =>
            detectionRate * SpeedFactor(playerSpeed, s) * Mathf.Lerp(0.5f, 1.5f, 1f - Mathf.Clamp01(distance / range)) * ExposureFactor(exposure, v);
    }
}
