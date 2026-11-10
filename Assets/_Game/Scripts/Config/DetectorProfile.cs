using System;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Procedural animation of a robot's parts (rotors, wheels, legs, lean, bob).</summary>
    [Serializable]
    public class AnimationSettings
    {
        [Tooltip("Rotor spin, degrees per second.")]
        public float RotorSpeed = 1800f;
        [Tooltip("Leg swings per meter of travel.")]
        public float StepFrequency = 1.6f;
        [Tooltip("Leg swing amplitude, degrees.")]
        public float LegSwing = 25f;
        [Tooltip("Flying bodies lean into the direction of travel (degrees at full speed).")]
        public float Lean;
        [Tooltip("Walkers bob up and down a little (meters).")]
        public float Bob;

        [Header("Walking legs (inverse kinematics)")]
        [Tooltip("A foot steps once it is this far behind its spot, in leg lengths.")]
        public float StepStride = 0.45f;
        [Tooltip("Seconds one step takes.")]
        public float StepTime = 0.22f;
        [Tooltip("How high a foot lifts while stepping, in leg lengths.")]
        public float StepLift = 0.22f;
    }

    /// <summary>
    /// Tuning of one kind of robot or device: a separate asset next to its prefab (…Config.asset), so that changing the
    /// numbers never conflicts with changing the prefab.
    /// </summary>
    public abstract class DetectorProfile : ScriptableObject
    {
        public AnimationSettings Animation = new();
    }
}
