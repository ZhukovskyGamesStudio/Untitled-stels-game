using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>Every kind of robot and security device. Index into GameManager.RobotPrefabs.</summary>
    public enum RobotKind
    {
        Guard = 0,              // legs, camera eye, weak hearing
        Listener = 1,           // wheels, blind, microphones
        Drone = 2,              // flies, downward camera, raises alarms
        Spider = 3,             // crawls on walls, ambushes from above
        Vacuum = 4,             // rides the ceiling, scans the floor with a laser line
        Turret = 5,             // stationary, aims and shoots
        Thumper = 6,            // tracks, feels footsteps through the floor (seismic)
        Sorter = 8,             // sees only players carrying loot of its colour
        Worker = 9,             // harmless, but snitches (alarm)
        Lidar = 11,             // floor pole, spinning laser line at knee height
        MotionSensor = 12,      // wall PIR: notices fast movement
        PressurePlate = 13,     // floor tile: stepping on it raises an alarm
        Camera = 14,            // wall camera sweeping left-right, raises alarms (placed by the camera spots, not the roster)
        Sweeper = 15,           // floor cleaner, blind; a lidar spins at ankle height — stand on a chair or a desk
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

    /// <summary>
    /// Whose side a robot is on. Servants of the machine work as a team: they raise alarms and answer them. Renegades
    /// went their own way: no alarms, no radio, deaf to both — and they take a caught player to their own lair, not to prison.
    /// </summary>
    public enum Faction : byte { Servant, Renegade }

    /// <summary>A kind's share of a house of some type (LevelConfig → Roster → ByHouse): likelier there, or only there.</summary>
    [System.Serializable]
    public struct HouseRoster
    {
        public RobotKind Kind;
        public HouseKind House;
        [Tooltip("Its weight in this type of house is multiplied by this.")]
        public float Scale;
        [Tooltip("It appears in this type of house only (a nurse outside a hospital).")]
        public bool Only;
        public HouseRoster(RobotKind k, HouseKind h, float scale, bool only = false) { Kind = k; House = h; Scale = scale; Only = only; }
    }

    /// <summary>Who guards the houses (LevelConfig): mobile robots and fixed devices, with unlock depth and weights.</summary>
    [System.Serializable]
    public class RosterSettings
    {
        public RosterEntry[] Robots = RobotRoster.DefaultRobots();
        public RosterEntry[] Devices = RobotRoster.DefaultDevices();
        [Tooltip("Every type of house has its own mix: a kind is likelier in some houses, or lives in one type only.")]
        public HouseRoster[] ByHouse = RobotRoster.DefaultByHouse();
    }

    /// <summary>Which robots appear on which level (weights, unlock depth). Pure, unit tested.</summary>
    public static class RobotRoster
    {
        /// <summary>Slots of a table by kind (the numbers are saved and travel: 7, 10 and 16 were robots the game no longer has).</summary>
        public const int KindCount = 16;

        public static readonly RobotKind[] Kinds = (RobotKind[])System.Enum.GetValues(typeof(RobotKind));

        static readonly RosterSettings s_Default = new();

        public static RosterEntry[] DefaultRobots() => new RosterEntry[]
        {
            // the first houses: mostly robots on their rounds (listener, vacuum, sweeper, drone), few guards
            new(RobotKind.Guard, 0, 2f),
            new(RobotKind.Listener, 0, 4f),
            new(RobotKind.Worker, 0, 2f),
            new(RobotKind.Vacuum, 0, 3f),
            new(RobotKind.Sweeper, 0, 3f),
            new(RobotKind.Drone, 0, 2f),
            new(RobotKind.Spider, 1, 2f),
            new(RobotKind.Thumper, 2, 2f),
            new(RobotKind.Sorter, 2, 1.5f),
            new(RobotKind.Turret, 2, 1.5f),
        };

        public static RosterEntry[] DefaultDevices() => new RosterEntry[]
        {
            new(RobotKind.PressurePlate, 0, 2f),
            new(RobotKind.MotionSensor, 0, 2f),
            new(RobotKind.Lidar, 1, 1.5f),
        };

        public static HouseRoster[] DefaultByHouse() => new HouseRoster[]
        {
            new(RobotKind.Worker, HouseKind.Warehouse, 2.5f),
            new(RobotKind.Sorter, HouseKind.Warehouse, 2f),
            new(RobotKind.Listener, HouseKind.DataCenter, 2f),
            new(RobotKind.Sweeper, HouseKind.Hospital, 2f),
            new(RobotKind.Spider, HouseKind.Abandoned, 3f),
            new(RobotKind.Drone, HouseKind.Headquarters, 2f),
        };

        /// <summary>The zone a kind belongs to: it stands in rooms at least this dangerous (<see cref="DangerZones"/>).</summary>
        public static DangerZone ZoneOf(RobotKind k) => k switch
        {
            RobotKind.Worker or RobotKind.Sweeper or RobotKind.Listener or RobotKind.Lidar => DangerZone.Green,
            RobotKind.Turret => DangerZone.Red,
            _ => DangerZone.Yellow,
        };

        /// <summary>Whose side a kind is on.</summary>
        public static Faction FactionOf(RobotKind k) => k switch
        {
            RobotKind.Spider => Faction.Renegade,
            _ => Faction.Servant,
        };

        /// <summary>A kind's weight in a house of this type: × its scale there, 0 where it does not live.</summary>
        public static float HouseScale(RobotKind k, HouseKind house, HouseRoster[] byHouse)
        {
            float scale = 1f;
            if (byHouse == null) return scale;
            foreach (var e in byHouse)
            {
                if (e.Kind != k) continue;
                if (e.House == house) scale *= e.Scale;
                else if (e.Only && !HasHouse(byHouse, k, house)) return 0f;
            }
            return scale;
        }

        static bool HasHouse(HouseRoster[] byHouse, RobotKind k, HouseKind house)
        {
            foreach (var e in byHouse) if (e.Kind == k && e.House == house) return true;
            return false;
        }

        /// <summary>
        /// A robot for a room of this zone in a house of this type: only kinds of that zone or a calmer one, the room's own
        /// zone <paramref name="ownZone"/> times likelier; the house's mix applied. No kind fits: any unlocked one of the house.
        /// </summary>
        public static RobotKind PickRobot(RosterEntry[] table, int level, System.Random rng, DangerZone zone, HouseKind house, RosterSettings roster, float ownZone = 2.5f)
        {
            var byHouse = (roster ?? s_Default).ByHouse;
            var fit = new List<RosterEntry>();
            var any = new List<RosterEntry>();
            foreach (var e in table)
            {
                if (level < e.MinLevel) continue;
                var scaled = e;
                scaled.Weight = e.Weight * HouseScale(e.Kind, house, byHouse);
                if (scaled.Weight <= 0f) continue;
                any.Add(scaled);
                var own = ZoneOf(e.Kind);
                if (own > zone) continue;
                if (own == zone) scaled.Weight *= ownZone;
                fit.Add(scaled);
            }
            var from = fit.Count > 0 ? fit : any;
            return from.Count > 0 ? Pick(from.ToArray(), level, rng) : Pick(table, level, rng);
        }

        /// <summary>A kind that may come in as a reinforcement: a robot of the house's table, unlocked — not a harmless worker, not a device.</summary>
        /// <summary>
        /// The house calls its own: servants of the yellow zone and, late on its clock (<paramref name="tier"/> red), of the
        /// red one — never the staff of the green rooms, never a renegade, never a kind that does not live in this house.
        /// </summary>
        public static RobotKind PickReinforcement(RosterEntry[] table, int level, System.Random rng, DangerZone tier = DangerZone.Red,
            HouseKind house = HouseKind.Office, HouseRoster[] byHouse = null)
        {
            var allowed = new System.Collections.Generic.List<RosterEntry>();
            foreach (var e in table)
            {
                if (e.MinLevel > level || e.Weight <= 0f || IsDevice(e.Kind) || e.Kind is RobotKind.Worker or RobotKind.Camera) continue;
                var zone = ZoneOf(e.Kind);
                if (zone == DangerZone.Green || zone > tier || FactionOf(e.Kind) == Faction.Renegade) continue;
                var scaled = e;
                scaled.Weight *= HouseScale(e.Kind, house, byHouse);
                if (scaled.Weight > 0f) allowed.Add(scaled);
            }
            if (allowed.Count == 0) return RobotKind.Guard;
            return Pick(allowed.ToArray(), level, rng);
        }

        /// <summary>Who comes late on the house's clock (Game.Escalation): the guard with a gun.</summary>
        public const RobotKind ArmedGuard = RobotKind.Guard;

        public static bool IsDevice(RobotKind k) => k == RobotKind.Lidar || k == RobotKind.MotionSensor || k == RobotKind.PressurePlate;

        /// <summary>Robots with legs / propellers can change floors; wheels, tracks and fixed devices cannot.</summary>
        public static bool CanUseStairs(RobotKind k) =>
            k is RobotKind.Guard or RobotKind.Drone or RobotKind.Sorter;

        public static RobotKind PickRobot(int level, System.Random rng, RosterSettings roster = null) => Pick((roster ?? s_Default).Robots, level, rng);

        /// <summary>A robot from this table.</summary>
        public static RobotKind PickRobot(RosterEntry[] table, int level, System.Random rng) => Pick(table, level, rng);

        /// <summary>Kinds that first appear on this house: each of them is placed at least once there, so players meet it.</summary>
        public static List<RobotKind> Newcomers(RosterEntry[] table, int level)
        {
            var list = new List<RobotKind>();
            foreach (var e in table) if (e.MinLevel == level && level > 0 && e.Weight > 0f) list.Add(e.Kind);
            return list;
        }
        public static RobotKind PickDevice(int level, System.Random rng, RosterSettings roster = null) => Pick((roster ?? s_Default).Devices, level, rng);

        public static bool IsUnlocked(RobotKind k, int level, RosterSettings roster = null)
        {
            roster ??= s_Default;
            foreach (var e in roster.Robots) if (e.Kind == k) return level >= e.MinLevel;
            foreach (var e in roster.Devices) if (e.Kind == k) return level >= e.MinLevel;
            return false; // cameras: own spots — never picked from the roster
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
