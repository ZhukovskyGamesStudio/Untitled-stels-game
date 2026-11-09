using UnityEngine;

namespace Stealth.Level
{
    /// <summary>Every kind of robot and security device. Index into GameManager.RobotPrefabs.</summary>
    public enum RobotKind
    {
        Guard,          // legs, camera eye, weak hearing
        Listener,       // wheels, blind, microphones
        Drone,          // flies, downward camera, raises alarms
        Spider,         // crawls on walls, ambushes from above
        Vacuum,         // rides the ceiling, scans the floor with a laser line
        Turret,         // stationary, aims and shoots
        Thumper,        // tracks, feels footsteps through the floor (seismic)
        Hound,          // legs, follows the scent trail
        Sorter,         // sees only players carrying loot of its colour
        Worker,         // harmless, but snitches (alarm)
        Hunter,         // arrives after alarms, all sensors
        Lidar,          // floor pole, spinning laser line at knee height
        MotionSensor,   // wall PIR: notices fast movement
        PressurePlate,  // floor tile: stepping on it raises an alarm
        Camera,         // wall camera sweeping left-right, raises alarms (placed by the camera spots, not the roster)
    }

    /// <summary>One line of the security roster: from which house a kind appears and how often.</summary>
    [System.Serializable]
    public struct RosterEntry
    {
        public RobotKind Kind;
        [Tooltip("First house (0-based) where it can appear.")]
        public int MinLevel;
        [Tooltip("Relative chance among the unlocked kinds.")]
        public float Weight;
        public RosterEntry(RobotKind k, int min, float w) { Kind = k; MinLevel = min; Weight = w; }
    }

    /// <summary>Who guards the houses (LevelConfig): mobile robots and fixed devices, with unlock depth and weights.</summary>
    [System.Serializable]
    public class RosterSettings
    {
        public RosterEntry[] Robots = RobotRoster.DefaultRobots();
        public RosterEntry[] Devices = RobotRoster.DefaultDevices();
    }

    /// <summary>Which robots appear on which level (weights, unlock depth). Pure, unit tested.</summary>
    public static class RobotRoster
    {
        public static readonly int KindCount = System.Enum.GetValues(typeof(RobotKind)).Length;

        static readonly RosterSettings s_Default = new();

        public static RosterEntry[] DefaultRobots() => new RosterEntry[]
        {
            new(RobotKind.Guard, 0, 5f),
            new(RobotKind.Listener, 0, 3f),
            new(RobotKind.Worker, 0, 2f),
            new(RobotKind.Vacuum, 0, 1.5f),
            new(RobotKind.Drone, 1, 2f),
            new(RobotKind.Spider, 1, 2f),
            new(RobotKind.Thumper, 2, 2f),
            new(RobotKind.Sorter, 2, 1.5f),
            new(RobotKind.Turret, 2, 1.5f),
            new(RobotKind.Hound, 3, 2f),
        };

        public static RosterEntry[] DefaultDevices() => new RosterEntry[]
        {
            new(RobotKind.PressurePlate, 0, 2f),
            new(RobotKind.MotionSensor, 1, 2f),
            new(RobotKind.Lidar, 1, 1.5f),
        };

        public static bool IsDevice(RobotKind k) => k == RobotKind.Lidar || k == RobotKind.MotionSensor || k == RobotKind.PressurePlate;

        /// <summary>Robots with legs / propellers can change floors; wheels, tracks and fixed devices cannot.</summary>
        public static bool CanUseStairs(RobotKind k) =>
            k == RobotKind.Guard || k == RobotKind.Drone || k == RobotKind.Hound || k == RobotKind.Hunter || k == RobotKind.Sorter;

        public static RobotKind PickRobot(int level, System.Random rng, RosterSettings roster = null) => Pick((roster ?? s_Default).Robots, level, rng);
        public static RobotKind PickDevice(int level, System.Random rng, RosterSettings roster = null) => Pick((roster ?? s_Default).Devices, level, rng);

        public static bool IsUnlocked(RobotKind k, int level, RosterSettings roster = null)
        {
            roster ??= s_Default;
            foreach (var e in roster.Robots) if (e.Kind == k) return level >= e.MinLevel;
            foreach (var e in roster.Devices) if (e.Kind == k) return level >= e.MinLevel;
            return false; // Hunter: spawned by alarms; cameras: own spots — never picked from the roster
        }

        static RobotKind Pick(RosterEntry[] table, int level, System.Random rng)
        {
            float total = 0f;
            foreach (var e in table) if (level >= e.MinLevel) total += e.Weight;
            float roll = (float)rng.NextDouble() * total;
            foreach (var e in table)
            {
                if (level < e.MinLevel) continue;
                roll -= e.Weight;
                if (roll <= 0f) return e.Kind;
            }
            return table[0].Kind;
        }
    }
}
