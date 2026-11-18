using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>Where a room type belongs in the building (<see cref="RoomRule.Zone"/>).</summary>
    public enum RoomZone : byte
    {
        /// <summary>Anywhere.</summary>
        Any,
        /// <summary>On the facade, by the windows: the rooms people work and meet in.</summary>
        Facade,
        /// <summary>Inside the building, away from the windows: service rooms (toilets, servers, stores, copiers).</summary>
        Interior,
    }

    /// <summary>
    /// Zoning of a finished plan (pure), both generators: how many of a themed room a floor and a house have
    /// (<see cref="RoomRule.PerFloor"/>, <see cref="RoomRule.PerHouse"/>: one kitchen a floor, one gym a house), then the
    /// rooms of a floor swap types so that work rooms lie on the facade, service rooms inside, toilets by the stairs (the
    /// building's core). A swap only happens where both rooms still hold their new type's rule (<see cref="RoomRules"/>);
    /// the plan's walls and doors stay as they are. Not moved: the lobby, stair halls, corridors, the storage room (its one
    /// door was laid for it) and atriums (their type was chosen for the void).
    /// </summary>
    public static class Zoning
    {
        /// <summary>Metres of outside wall that make a room a facade room (a window's worth).</summary>
        public const float FacadeWall = 2f;
        /// <summary>A toilet this close to a staircase (metres, plan) is by the core.</summary>
        public const float CoreReach = 9f;

        /// <summary>
        /// Caps and swaps (see the class). <paramref name="stairs"/>: the house's staircases (the core); <paramref name="steps"/>:
        /// each room's steps from the lobby (<see cref="RoomDepth"/>; null: depth not used).
        /// </summary>
        public static void Arrange(HousePlan plan, IReadOnlyList<Stair> stairs, RoomRule[] rules, int[] steps = null, RoomAdjacency[] adjacency = null)
        {
            s_Adjacency = RoomRules.AdjacencyMap(adjacency);
            s_Neighbours = Neighbours(plan);
            var info = new Dictionary<int, (bool facade, bool core, float deep)>();
            foreach (var r in plan.Rooms)
            {
                if (!Movable(r)) continue;
                info[r.Id] = (RoomRules.OutsideOf(plan, r) >= FacadeWall, NearCore(plan, stairs, r), steps != null ? RoomDepth.Share(steps, r.Id) : 0.5f);
            }
            Cap(plan, rules, info);
            for (int f = 0; f < plan.Floors; f++) Swap(plan, rules, info, f);
        }

        /// <summary>Capped and zoned (an atrium is capped too, but keeps its place: its type was chosen for the void).</summary>
        static bool Movable(PlanRoom r) => r.Type is not (RoomType.Lobby or RoomType.Hall or RoomType.Corridor or RoomType.Storage)
            && !RoomRules.Planned(r.Type); // the locations' own rooms: laid out where they belong

        /// <summary>The room touches a stair hall or lies close to a staircase on its floor.</summary>
        static bool NearCore(HousePlan plan, IReadOnlyList<Stair> stairs, PlanRoom r)
        {
            foreach (var w in plan.Walls)
            {
                if (w.Floor != r.Floor || (w.RoomA != r.Id && w.RoomB != r.Id)) continue;
                int o = w.RoomA == r.Id ? w.RoomB : w.RoomA;
                if (o >= 0 && o < plan.Rooms.Count && plan.Rooms[o].Type == RoomType.Hall) return true;
            }
            var c = new Vector2(r.Center.x, r.Center.z);
            if (stairs != null)
                foreach (var s in stairs)
                    if ((s.Floor == r.Floor || s.Floor + 1 == r.Floor) && (s.Origin - c).magnitude <= CoreReach) return true;
            return false;
        }

        /// <summary>
        /// How well a type sits in a room of this place (higher is better): its zone (facade / inside), toilets by the core,
        /// and its privacy against the room's depth from the lobby (<paramref name="deep"/> 0 … 1: public rooms near the
        /// entrance, private ones deep inside).
        /// </summary>
        public static float Score(RoomType type, bool facade, bool core, RoomRule[] rules, float deep = 0.5f)
        {
            var rule = RoomRules.For(type, rules);
            float s = rule.Zone switch
            {
                RoomZone.Facade => facade ? 2f : -2f,    // a windowless office is the worst miss
                RoomZone.Interior => facade ? -1f : 1f,  // a store on the facade wastes its windows
                _ => 0f,
            };
            if (type == RoomType.Restroom) s += core ? 2f : -1f; // the toilets with the stairs and risers
            s += rule.Privacy * (deep * 2f - 1f) * PrivacyWeight;
            return s;
        }

        // the floor being arranged: who is next door, and how good a pair of types is next door (RoomAdjacency)
        [System.ThreadStatic] static Dictionary<(RoomType, RoomType), float> s_Adjacency;
        [System.ThreadStatic] static Dictionary<int, List<(int room, float weight)>> s_Neighbours;

        /// <summary>
        /// Rooms next door to each room: a shared wall of a metre or more on the same floor (weight 1), half again if a way
        /// people walk joins them (a door, an arch, a breach).
        /// </summary>
        static Dictionary<int, List<(int room, float weight)>> Neighbours(HousePlan plan)
        {
            var weight = new Dictionary<(int, int), float>();
            for (int i = 0; i < plan.Walls.Count; i++)
            {
                var w = plan.Walls[i];
                if (w.RoomA < 0 || w.RoomB < 0 || w.RoomA == w.RoomB || w.Length < 1f) continue;
                var key = w.RoomA < w.RoomB ? (w.RoomA, w.RoomB) : (w.RoomB, w.RoomA);
                if (!weight.ContainsKey(key)) weight[key] = 1f;
            }
            foreach (var o in plan.Openings)
            {
                if (!RoomDepth.Walked(o.Kind)) continue;
                var w = plan.Walls[o.Wall];
                if (w.RoomA < 0 || w.RoomB < 0) continue;
                var key = w.RoomA < w.RoomB ? (w.RoomA, w.RoomB) : (w.RoomB, w.RoomA);
                if (weight.ContainsKey(key)) weight[key] = 1.5f;
            }
            var map = new Dictionary<int, List<(int, float)>>();
            foreach (var kv in weight)
            {
                (map.TryGetValue(kv.Key.Item1, out var a) ? a : map[kv.Key.Item1] = new List<(int, float)>()).Add((kv.Key.Item2, kv.Value));
                (map.TryGetValue(kv.Key.Item2, out var b) ? b : map[kv.Key.Item2] = new List<(int, float)>()).Add((kv.Key.Item1, kv.Value));
            }
            return map;
        }

        /// <summary>How good type <paramref name="t"/> is in room <paramref name="room"/> by its neighbours (all but <paramref name="skip"/>).</summary>
        static float NextDoor(HousePlan plan, int room, RoomType t, int skip)
        {
            if (s_Adjacency == null || s_Adjacency.Count == 0 || s_Neighbours == null || !s_Neighbours.TryGetValue(room, out var list)) return 0f;
            float s = 0f;
            foreach (var (other, w) in list)
                if (other != skip && s_Adjacency.TryGetValue((t, plan.Rooms[other].Type), out var v)) s += v * w;
            return s;
        }

        /// <summary>How much depth weighs against the zone (a public room deep inside costs at most this much).</summary>
        public const float PrivacyWeight = 1.5f;

        /// <summary>Rooms over their type's cap (by floor, by house) become an uncapped type that holds the room.</summary>
        static void Cap(HousePlan plan, RoomRule[] rules, Dictionary<int, (bool facade, bool core, float deep)> info)
        {
            var house = new Dictionary<RoomType, int>();
            var floor = new Dictionary<(int, RoomType), int>();
            // the rooms that keep their type (an atrium) count first: the movable ones make way for them
            foreach (var r in plan.Rooms)
            {
                if (info.ContainsKey(r.Id) || r.Type is RoomType.Lobby or RoomType.Hall or RoomType.Corridor) continue;
                house[r.Type] = (house.TryGetValue(r.Type, out var hk) ? hk : 0) + 1;
                floor[(r.Floor, r.Type)] = (floor.TryGetValue((r.Floor, r.Type), out var fk) ? fk : 0) + 1;
            }
            // the best-placed room of a type keeps it: the order is by how well the type sits there, then by id
            var order = new List<PlanRoom>();
            foreach (var r in plan.Rooms) if (info.ContainsKey(r.Id)) order.Add(r);
            order.Sort((a, b) =>
            {
                float sa = Score(a.Type, info[a.Id].facade, info[a.Id].core, rules, info[a.Id].deep), sb = Score(b.Type, info[b.Id].facade, info[b.Id].core, rules, info[b.Id].deep);
                return sa != sb ? sb.CompareTo(sa) : a.Id.CompareTo(b.Id);
            });
            foreach (var r in order)
            {
                var rule = RoomRules.For(r.Type, rules);
                int h = house.TryGetValue(r.Type, out var hn) ? hn : 0, fl = floor.TryGetValue((r.Floor, r.Type), out var fn) ? fn : 0;
                if ((rule.PerHouse > 0 && h >= rule.PerHouse) || (rule.PerFloor > 0 && fl >= rule.PerFloor))
                    r.Type = Uncapped(plan, r, rules, info[r.Id].facade, info[r.Id].deep);
                house[r.Type] = (house.TryGetValue(r.Type, out var h2) ? h2 : 0) + 1;
                floor[(r.Floor, r.Type)] = (floor.TryGetValue((r.Floor, r.Type), out var f2) ? f2 : 0) + 1;
            }
        }

        /// <summary>Everyday types that are never capped, in order of preference.</summary>
        static readonly RoomType[] Everyday = { RoomType.OpenSpace, RoomType.Meeting, RoomType.Office, RoomType.Lab, RoomType.Server };

        static RoomType Uncapped(HousePlan plan, PlanRoom r, RoomRule[] rules, bool facade, float deep)
        {
            RoomType best = RoomType.Office;
            float bestScore = float.MinValue;
            foreach (var t in Everyday)
            {
                var rule = RoomRules.For(t, rules);
                if (rule.PerHouse > 0 || rule.PerFloor > 0 || !RoomRules.Holds(plan, r, t, rules)) continue;
                float s = Score(t, facade, false, rules, deep);
                if (s > bestScore) { bestScore = s; best = t; }
            }
            return best;
        }

        /// <summary>Pairs of rooms on a floor swap types while that places them better (and both still hold their rule).</summary>
        static void Swap(HousePlan plan, RoomRule[] rules, Dictionary<int, (bool facade, bool core, float deep)> info, int f)
        {
            var rooms = new List<PlanRoom>();
            foreach (var r in plan.Rooms) if (r.Floor == f && r.Levels == 1 && info.ContainsKey(r.Id)) rooms.Add(r);
            float Here(PlanRoom r, RoomType t) => Score(t, info[r.Id].facade, info[r.Id].core, rules, info[r.Id].deep);
            for (int pass = 0; pass < 8; pass++)
            {
                bool changed = false;
                for (int i = 0; i < rooms.Count; i++)
                    for (int j = i + 1; j < rooms.Count; j++)
                    {
                        var a = rooms[i];
                        var b = rooms[j];
                        if (a.Type == b.Type) continue;
                        // the place itself (zone, core, depth) and the neighbours (the pair a–b itself stays the same pair)
                        float gain = Here(a, b.Type) + Here(b, a.Type) - Here(a, a.Type) - Here(b, b.Type)
                            + NextDoor(plan, a.Id, b.Type, b.Id) + NextDoor(plan, b.Id, a.Type, a.Id)
                            - NextDoor(plan, a.Id, a.Type, b.Id) - NextDoor(plan, b.Id, b.Type, a.Id);
                        if (gain <= 0.01f || !RoomRules.Holds(plan, a, b.Type, rules) || !RoomRules.Holds(plan, b, a.Type, rules)) continue;
                        (a.Type, b.Type) = (b.Type, a.Type);
                        changed = true;
                    }
                if (!changed) break;
            }
        }
    }
}
