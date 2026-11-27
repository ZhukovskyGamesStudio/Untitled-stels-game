using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>How dangerous a room is: who works there, how many traps, how rich the loot. Saved in layouts: append only.</summary>
    public enum DangerZone : byte
    {
        /// <summary>Service staff only (they snitch, they do not fight), next to no traps.</summary>
        Green,
        /// <summary>Staff and guards, traps.</summary>
        Yellow,
        /// <summary>The worst of the house; the best loot.</summary>
        Red,
    }

    /// <summary>Tuning of the danger zones of a house (LevelConfig → Security → Danger).</summary>
    [System.Serializable]
    public class DangerSettings
    {
        [Tooltip("Zones at all: off = the whole house is yellow (the rules before the zones).")]
        public bool Enabled = true;
        [Tooltip("Share of the house's floor that is red (never the way from the entry to the exit, never a corridor or a stair hall).")]
        [Range(0f, 1f)] public float RedShare = 0.2f;
        [Tooltip("Share of the house's floor that is yellow.")]
        [Range(0f, 1f)] public float YellowShare = 0.4f;
        [Tooltip("First house (0-based) with red rooms.")]
        public int RedFromHouse = 0;
        [Tooltip("How much chance moves a room up or down the order (0: the deepest rooms are always the red ones).")]
        public float Jitter = 0.3f;
        [Tooltip("Chance that a trap, a device or a camera picked for a green room stays there (the rest move deeper).")]
        [Range(0f, 1f)] public float GreenTraps = 0.15f;
        [Tooltip("Loot value bonus in a green, a yellow and a red room.")]
        public int[] LootBonus = { 0, 6, 14 };
        [Tooltip("How much likelier a robot is to stand in a room of its own zone than in a more dangerous one.")]
        public float OwnZoneWeight = 2.5f;

        public int LootBonusOf(DangerZone z) => LootBonus == null || LootBonus.Length == 0 ? 0 : LootBonus[Mathf.Min((int)z, LootBonus.Length - 1)];
    }

    /// <summary>
    /// The danger zones of a house (pure, from the seed): every room is green, yellow or red. The rooms of the shortest
    /// way from the entry to the exit are never red, so the team can always leave without setting foot in a red room;
    /// the entry room and the rooms next to it are green. The rest is ordered by depth from the entry (with a seeded
    /// jitter): the deepest rooms are red, the next yellow, by their share of the floor. The main job's room is red
    /// (yellow if it lies on the way out), the robots' charging room at least yellow.
    /// </summary>
    public static class DangerZones
    {
        /// <summary>Assigns <see cref="LevelLayout.Zones"/>.</summary>
        public static void Assign(LevelLayout l, DangerSettings s)
        {
            s ??= new DangerSettings();
            int n = l.Plan.Rooms.Count;
            var zones = new DangerZone[n];
            l.Zones = zones;
            if (!s.Enabled)
            {
                for (int i = 0; i < n; i++) zones[i] = DangerZone.Yellow;
                return;
            }
            int entry = l.EntryRoom >= 0 ? l.EntryRoom : l.LobbyRoom;
            var graph = new RoomGraph(l.Plan, l.Stairs, Passage.Robots);
            var steps = graph.Steps(entry);
            var safe = WayOut(l, graph, steps, entry);
            var calm = new HashSet<int> { entry };
            foreach (int r in graph.Neighbours(entry)) calm.Add(r);

            int deepest = 1;
            foreach (int d in steps) deepest = Mathf.Max(deepest, d);
            float total = 0f;
            var order = new List<(int room, float score)>();
            for (int i = 0; i < n; i++)
            {
                var room = l.Plan.Rooms[i];
                total += room.Area;
                if (calm.Contains(i)) continue;
                var rng = new System.Random(LevelRandom.Mix(l.Seed, Salt.DangerZones, i));
                float depth = steps[i] < 0 ? 1f : steps[i] / (float)deepest;
                float score = depth + s.Jitter * ((float)rng.NextDouble() - 0.5f) + (room.Dark ? 0.1f : 0f);
                if (i == l.VaultRoom) score += 10f;          // the main job: the most dangerous room there is
                if (i == l.ChargingRoom) score += 0.35f;     // the guards' own room
                order.Add((i, score));
            }
            order.Sort((a, b) => a.score != b.score ? b.score.CompareTo(a.score) : a.room.CompareTo(b.room));

            float red = 0f, yellow = 0f;
            bool reds = l.LevelIndex >= s.RedFromHouse;
            foreach (var (i, _) in order)
            {
                var room = l.Plan.Rooms[i];
                bool way = safe.Contains(i) || RoomTraits.Circulation(room.Type);
                if (reds && !way && red < s.RedShare * total)
                {
                    zones[i] = DangerZone.Red;
                    red += room.Area;
                }
                else if (yellow < s.YellowShare * total || i == l.VaultRoom || i == l.ChargingRoom)
                {
                    zones[i] = DangerZone.Yellow;
                    yellow += room.Area;
                }
            }
        }

        /// <summary>The rooms of the shortest way from the entry to the exit (both ends with it); the walked ways first, then any way a player has.</summary>
        static HashSet<int> WayOut(LevelLayout l, RoomGraph graph, int[] steps, int entry)
        {
            var way = new HashSet<int> { entry };
            int exit = l.ExitRoom;
            if (exit < 0 || exit >= steps.Length) return way;
            if (steps[exit] < 0)
            {
                graph = new RoomGraph(l.Plan, l.Stairs, Passage.Players);
                steps = graph.Steps(entry);
                if (steps[exit] < 0) { way.Add(exit); return way; }
            }
            for (int at = exit; at != entry;)
            {
                way.Add(at);
                int next = -1;
                foreach (int o in graph.Neighbours(at))
                    if (steps[o] == steps[at] - 1 && (next < 0 || o < next)) next = o;
                if (next < 0) break;
                at = next;
            }
            return way;
        }

        /// <summary>The exit can be walked to from the entry without entering a red room (what <see cref="Assign"/> promises).</summary>
        public static bool WayOutAvoidsRed(LevelLayout l)
        {
            int entry = l.EntryRoom >= 0 ? l.EntryRoom : l.LobbyRoom, exit = l.ExitRoom;
            if (exit < 0 || l.Zones == null) return true;
            var closed = new HashSet<int>();
            for (int i = 0; i < l.Zones.Length; i++) if (l.Zones[i] == DangerZone.Red) closed.Add(i);
            if (closed.Contains(entry) || closed.Contains(exit)) return false;
            if (new RoomGraph(l.Plan, l.Stairs, Passage.Robots).Steps(entry, -1, closed)[exit] >= 0) return true;
            // (an exit only a player's ways lead to: those ways, then)
            return new RoomGraph(l.Plan, l.Stairs, Passage.Robots).Steps(entry)[exit] < 0
                && new RoomGraph(l.Plan, l.Stairs, Passage.Players).Steps(entry, -1, closed)[exit] >= 0;
        }

        /// <summary>A robot of zone <paramref name="own"/> makes its rounds in a room of zone <paramref name="room"/>: never in a calmer one than its own.</summary>
        public static bool Patrols(DangerZone own, DangerZone room) => room >= own;

        /// <summary>The zone of the more dangerous side of a doorway (a trap in it guards that side).</summary>
        public static DangerZone Of(LevelLayout l, Doorway d)
        {
            var a = l.ZoneOf(d.RoomA);
            var b = l.ZoneOf(d.RoomB);
            return a > b ? a : b;
        }
    }
}
