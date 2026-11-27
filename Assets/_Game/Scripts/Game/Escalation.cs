using System;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>What the team does that makes the house more dangerous (each one moves the house's clock forward).</summary>
    public enum DangerEvent : byte { Alarm, Hack, Down, Objective, Capture }

    /// <summary>
    /// Tuning of the run's danger (RunConfig → Escalation). The danger of a house is its clock: it runs by itself and
    /// jumps forward with what the team does; the reinforcements, their kinds and the armed guards all count on it.
    /// </summary>
    [Serializable]
    public class EscalationSettings
    {
        [Header("What moves the house's clock forward, seconds")]
        [Tooltip("An alarm (alarms a few seconds apart count once).")]
        public float AlarmSeconds = 40f;
        [Tooltip("A robot or a device hacked.")]
        public float HackSeconds = 10f;
        [Tooltip("A player knocked down.")]
        public float DownSeconds = 20f;
        [Tooltip("A step of the main job done (the vault opened, the hostage freed).")]
        public float ObjectiveSeconds = 60f;
        [Tooltip("A player carried off to the prison or to a lair.")]
        public float CaptureSeconds = 30f;

        [Header("From house to house")]
        [Tooltip("Share of a house's clock the next house starts with: the run grows more dangerous as it goes.")]
        [Range(0f, 1f)] public float CarryOver = 0.2f;
        [Tooltip("The next house never starts with more than this on its clock, seconds.")]
        public float CarryMax = 150f;

        [Header("Who comes as a reinforcement")]
        [Tooltip("From this second of the clock the reinforcements include the robots of the red zone.")]
        public float RedAfter = 300f;
        [Tooltip("From this second armed guards come: the end of the house is near.")]
        public float ArmedAfter = 480f;
        [Tooltip("Seconds between armed guards after the first.")]
        public float ArmedEvery = 75f;
        [Tooltip("No more armed guards than this in one house.")]
        public int ArmedMax = 4;
    }

    /// <summary>The rules of the run's danger (pure, unit tested).</summary>
    public static class Escalation
    {
        /// <summary>How far an event moves the house's clock forward, seconds.</summary>
        public static float Seconds(DangerEvent e, EscalationSettings s) => e switch
        {
            DangerEvent.Alarm => s.AlarmSeconds,
            DangerEvent.Hack => s.HackSeconds,
            DangerEvent.Down => s.DownSeconds,
            DangerEvent.Objective => s.ObjectiveSeconds,
            DangerEvent.Capture => s.CaptureSeconds,
            _ => 0f,
        };

        /// <summary>The most dangerous zone whose robots come as reinforcements at this second of the clock.</summary>
        public static DangerZone Tier(float clock, EscalationSettings s) => clock >= s.RedAfter ? DangerZone.Red : DangerZone.Yellow;

        /// <summary>How many armed guards have come by this second of the clock.</summary>
        public static int ArmedDue(float clock, EscalationSettings s)
        {
            if (s.ArmedMax <= 0 || clock < s.ArmedAfter) return 0;
            return Mathf.Min(s.ArmedMax, 1 + Mathf.FloorToInt((clock - s.ArmedAfter) / Mathf.Max(1f, s.ArmedEvery)));
        }

        /// <summary>What the next house's clock starts with after a house left at <paramref name="clock"/>.</summary>
        public static float Carry(float clock, EscalationSettings s) => Mathf.Clamp(clock * s.CarryOver, 0f, Mathf.Max(0f, s.CarryMax));
    }
}
