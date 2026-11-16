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

        /// <summary>Caps and swaps (see the class). <paramref name="stairs"/>: the house's staircases (the core).</summary>
        public static void Arrange(HousePlan plan, IReadOnlyList<Stair> stairs, RoomRule[] rules)
        {
            var info = new Dictionary<int, (bool facade, bool core)>();
            foreach (var r in plan.Rooms)
            {
                if (!Movable(r)) continue;
                info[r.Id] = (RoomRules.OutsideOf(plan, r) >= FacadeWall, NearCore(plan, stairs, r));
            }
            Cap(plan, rules, info);
            for (int f = 0; f < plan.Floors; f++) Swap(plan, rules, info, f);
        }

        /// <summary>Capped and zoned (an atrium is capped too, but keeps its place: its type was chosen for the void).</summary>
        static bool Movable(PlanRoom r) => r.Type is not (RoomType.Lobby or RoomType.Hall or RoomType.Corridor or RoomType.Storage
            or RoomType.Workshop or RoomType.PaintBooth or RoomType.Utility); // the shop's hall and booth, the plant room: laid out where they belong

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

        /// <summary>How well a type sits in a room of this place (higher is better).</summary>
        public static int Score(RoomType type, bool facade, bool core, RoomRule[] rules)
        {
            int s = RoomRules.For(type, rules).Zone switch
            {
                RoomZone.Facade => facade ? 2 : -2,      // a windowless office is the worst miss
                RoomZone.Interior => facade ? -1 : 1,    // a store on the facade wastes its windows
                _ => 0,
            };
            if (type == RoomType.Restroom) s += core ? 2 : -1; // the toilets with the stairs and risers
            return s;
        }

        /// <summary>Rooms over their type's cap (by floor, by house) become an uncapped type that holds the room.</summary>
        static void Cap(HousePlan plan, RoomRule[] rules, Dictionary<int, (bool facade, bool core)> info)
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
                int sa = Score(a.Type, info[a.Id].facade, info[a.Id].core, rules), sb = Score(b.Type, info[b.Id].facade, info[b.Id].core, rules);
                return sa != sb ? sb.CompareTo(sa) : a.Id.CompareTo(b.Id);
            });
            foreach (var r in order)
            {
                var rule = RoomRules.For(r.Type, rules);
                int h = house.TryGetValue(r.Type, out var hn) ? hn : 0, fl = floor.TryGetValue((r.Floor, r.Type), out var fn) ? fn : 0;
                if ((rule.PerHouse > 0 && h >= rule.PerHouse) || (rule.PerFloor > 0 && fl >= rule.PerFloor))
                    r.Type = Uncapped(plan, r, rules, info[r.Id].facade);
                house[r.Type] = (house.TryGetValue(r.Type, out var h2) ? h2 : 0) + 1;
                floor[(r.Floor, r.Type)] = (floor.TryGetValue((r.Floor, r.Type), out var f2) ? f2 : 0) + 1;
            }
        }

        /// <summary>Everyday types that are never capped, in order of preference.</summary>
        static readonly RoomType[] Everyday = { RoomType.OpenSpace, RoomType.Meeting, RoomType.Office, RoomType.Lab, RoomType.Server };

        static RoomType Uncapped(HousePlan plan, PlanRoom r, RoomRule[] rules, bool facade)
        {
            RoomType best = RoomType.Office;
            int bestScore = int.MinValue;
            foreach (var t in Everyday)
            {
                var rule = RoomRules.For(t, rules);
                if (rule.PerHouse > 0 || rule.PerFloor > 0 || !RoomRules.Holds(plan, r, t, rules)) continue;
                int s = Score(t, facade, false, rules);
                if (s > bestScore) { bestScore = s; best = t; }
            }
            return best;
        }

        /// <summary>Pairs of rooms on a floor swap types while that places them better (and both still hold their rule).</summary>
        static void Swap(HousePlan plan, RoomRule[] rules, Dictionary<int, (bool facade, bool core)> info, int f)
        {
            var rooms = new List<PlanRoom>();
            foreach (var r in plan.Rooms) if (r.Floor == f && r.Levels == 1 && info.ContainsKey(r.Id)) rooms.Add(r);
            int Here(PlanRoom r, RoomType t) => Score(t, info[r.Id].facade, info[r.Id].core, rules);
            for (int pass = 0; pass < 8; pass++)
            {
                bool changed = false;
                for (int i = 0; i < rooms.Count; i++)
                    for (int j = i + 1; j < rooms.Count; j++)
                    {
                        var a = rooms[i];
                        var b = rooms[j];
                        if (a.Type == b.Type) continue;
                        int gain = Here(a, b.Type) + Here(b, a.Type) - Here(a, a.Type) - Here(b, b.Type);
                        if (gain <= 0 || !RoomRules.Holds(plan, a, b.Type, rules) || !RoomRules.Holds(plan, b, a.Type, rules)) continue;
                        (a.Type, b.Type) = (b.Type, a.Type);
                        changed = true;
                    }
                if (!changed) break;
            }
        }
    }
}
