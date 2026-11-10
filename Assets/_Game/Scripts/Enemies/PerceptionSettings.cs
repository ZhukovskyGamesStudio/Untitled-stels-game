using System;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// How eyes (robots, cameras, turrets, spiders) turn what they see into suspicion per second. The sensor's own speed
    /// (DetectionSpeed, DetectTime…) is multiplied by these factors (PerceptionConfig).
    /// </summary>
    [Serializable]
    public class VisionSettings
    {
        [Tooltip("Rate multiplier at the far end of the view range.")]
        public float FarFactor = 0.2f;
        [Tooltip("Rate multiplier right in front of the eye (grows with closeness squared).")]
        public float NearFactor = 2.2f;
        [Tooltip("A partly hidden body is noticed slower: rate × (visible share ^ this). 2 = 15% visible is 50 times slower.")]
        public float ExposurePower = 1.6f;
        [Tooltip("Less of the body than this share visible: not noticed at all.")]
        public float MinExposure = 0.12f;
        [Tooltip("Share of the half view angle (from the centre) that is seen at full speed.")]
        public float FullViewShare = 0.4f;
        [Tooltip("Rate multiplier at the edge of the view cone.")]
        public float EdgeOfViewFactor = 0.35f;
        [Tooltip("Rate multiplier for players outside the cone but close by (robots sense them within PeripheralDistance): " +
                 "sneaking up behind a robot to hack it should be possible.")]
        public float BehindFactor = 0.12f;
        [Tooltip("Rate multiplier for a crouching player (robots and cameras; turrets and spiders have their own).")]
        public float CrouchFactor = 0.6f;
        [Tooltip("Robots see a crouching player only this share of their view distance.")]
        public float CrouchRangeFactor = 0.7f;
        [Tooltip("Rate multiplier for a player standing still.")]
        public float StillFactor = 0.6f;
        [Tooltip("Slower than this (m/s) counts as standing still.")]
        public float StillSpeed = 0.4f;
    }

    /// <summary>
    /// How light on the player changes how well eyes see them (PerceptionConfig). The light at the player is the sum of
    /// every lamp around (brightness / distance², cut by the lamp's range and cone, zero behind an obstacle), the moon
    /// where it shines in and the ambient light — the same lights the player sees on screen.
    /// </summary>
    [Serializable]
    public class IlluminationSettings
    {
        [Tooltip("Light at the player at or below which they are in full darkness.")]
        public float DarkLevel = 0.005f;
        [Tooltip("Light at the player at or above which they are fully lit.")]
        public float LitLevel = 0.12f;
        [Tooltip("Vision rate multiplier in full darkness (1 = darkness does not help).")]
        public float DarkFactor = 0.15f;
        [Tooltip("How much the scene's ambient light counts.")]
        public float AmbientWeight = 1f;
    }

    /// <summary>How sounds travel to microphones (PerceptionConfig).</summary>
    [Serializable]
    public class HearingSettings
    {
        [Header("Share of the loudness that passes through")]
        public float Wall = 0.35f;
        [Tooltip("A closed door (an open one lets everything through).")]
        public float Door = 0.6f;
        [Tooltip("A floor / ceiling slab between storeys.")]
        public float Slab = 0.3f;
        [Tooltip("Furniture and anything else solid.")]
        public float Furniture = 0.85f;

        [Header("Reaction")]
        [Tooltip("Quieter than this (0..1 at the listener) is not heard.")]
        public float MinLoudness = 0.05f;
        [Tooltip("Suspicion added by one noise at full loudness (a footstep right next to the robot).")]
        public float SuspicionPerNoise = 0.5f;

        public float Transmission(SoundMaterial m) => m switch
        {
            SoundMaterial.Wall => Wall,
            SoundMaterial.Door => Door,
            SoundMaterial.Slab => Slab,
            _ => Furniture,
        };
    }

    /// <summary>Floor vibrations and motion sensors (PerceptionConfig).</summary>
    [Serializable]
    public class VibrationSettings
    {
        [Tooltip("Player speed (m/s) at which seismic and motion sensors work at their configured rate; faster is noticed faster.")]
        public float ReferenceSpeed = 4f;
        [Tooltip("Limits of the speed multiplier.")]
        public float MinSpeedFactor = 0.4f;
        public float MaxSpeedFactor = 1.8f;
    }

    /// <summary>One look of an eye at a player: everything the vision rate depends on.</summary>
    public struct Glimpse
    {
        public float Distance;
        public float Range;
        /// <summary>Share of the body the eye can see, 0..1 (<see cref="Perception.Exposure"/>).</summary>
        public float Exposure;
        /// <summary>Angle from the centre of view divided by the half view angle: 0 centre, 1 edge, above 1 — outside (close behind).</summary>
        public float OffCentre;
        public bool Crouching;
        public bool Moving;
        /// <summary>Light multiplier at the player (<see cref="Perception.LightFactor"/>): 1 fully lit, down to DarkFactor in darkness.</summary>
        public float Light;
    }
}
