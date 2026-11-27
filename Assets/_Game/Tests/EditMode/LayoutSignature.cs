using System.Collections.Generic;
using System.Linq;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// What a plan looks like, in the numbers that tell one kind of layout from another (every kind of house has its own:
    /// PlanGenerator.Blocks, <c>Scheme</c>): how much of it is corridor, how many islands its corridors go round, how big its
    /// biggest room is, how far one can see down a corridor, whether a court rises through it. For the tests that keep the
    /// kinds apart (HouseKindLayoutTests) and the report (LevelAudit).
    /// </summary>
    public static class LayoutSignature
    {
        static bool Corridor(HousePlan plan, int f, int x, int z) =>
            plan.KindAt(f, x, z) == CellKind.Room && plan.Rooms[plan.IdAt(f, x, z)].Type == RoomType.Corridor;

        static bool Inside(HousePlan plan, int f, int x, int z) =>
            x >= 0 && z >= 0 && x < plan.NX && z < plan.NZ && plan.KindAt(f, x, z) != CellKind.Outside;

        /// <summary>Of the floors' area, the share that is corridor.</summary>
        public static float CorridorShare(LevelLayout l)
        {
            var plan = l.Plan;
            int all = 0, corridor = 0;
            for (int f = 0; f < plan.Floors; f++)
                for (int x = 0; x < plan.NX; x++)
                    for (int z = 0; z < plan.NZ; z++)
                    {
                        if (plan.KindAt(f, x, z) != CellKind.Room) continue;
                        all++;
                        if (Corridor(plan, f, x, z)) corridor++;
                    }
            return corridor / (float)Mathf.Max(1, all);
        }

        /// <summary>
        /// Islands a floor: pieces of it (16 m² and more) that corridors go all the way round — no cell of one on the facade.
        /// A ring's core is one, a hall between aisles is one, a court with its gallery is one; rooms in rows off a corridor
        /// are none.
        /// </summary>
        public static float Islands(LevelLayout l)
        {
            var plan = l.Plan;
            int islands = 0;
            for (int f = 0; f < plan.Floors; f++)
            {
                var seen = new bool[plan.NX, plan.NZ];
                for (int x = 0; x < plan.NX; x++)
                    for (int z = 0; z < plan.NZ; z++)
                    {
                        if (seen[x, z] || !Inside(plan, f, x, z) || Corridor(plan, f, x, z)) continue;
                        var piece = new List<Vector2Int> { new(x, z) };
                        seen[x, z] = true;
                        bool facade = false;
                        for (int i = 0; i < piece.Count; i++)
                            foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                            {
                                var n = piece[i] + d;
                                if (!Inside(plan, f, n.x, n.y)) { facade = true; continue; }
                                if (seen[n.x, n.y] || Corridor(plan, f, n.x, n.y)) continue;
                                seen[n.x, n.y] = true;
                                piece.Add(n);
                            }
                        if (!facade && piece.Count * HousePlan.Res * HousePlan.Res >= 16f) islands++;
                    }
            }
            return islands / (float)plan.Floors;
        }

        /// <summary>The biggest room that is not a corridor, m².</summary>
        public static float BiggestRoom(LevelLayout l) =>
            l.Plan.Rooms.Where(r => r.Type is not (RoomType.Corridor or RoomType.Lobby or RoomType.Hall)).Select(r => r.Area).DefaultIfEmpty(0f).Max();

        /// <summary>The longest straight run of corridor, metres (how far one sees down it).</summary>
        public static float LongestStraight(LevelLayout l)
        {
            var plan = l.Plan;
            int best = 0;
            for (int f = 0; f < plan.Floors; f++)
            {
                for (int x = 0; x < plan.NX; x++)
                    for (int z = 0, run = 0; z < plan.NZ; z++) { run = Corridor(plan, f, x, z) ? run + 1 : 0; best = Mathf.Max(best, run); }
                for (int z = 0; z < plan.NZ; z++)
                    for (int x = 0, run = 0; x < plan.NX; x++) { run = Corridor(plan, f, x, z) ? run + 1 : 0; best = Mathf.Max(best, run); }
            }
            return best * HousePlan.Res;
        }

        /// <summary>The longest straight corridor over the building's longer side (1: a corridor from end to end).</summary>
        public static float StraightShare(LevelLayout l) =>
            LongestStraight(l) / Mathf.Max(1f, Mathf.Max(l.Plan.NX, l.Plan.NZ) * HousePlan.Res);

        /// <summary>The widest place in the corridors: the side of the biggest square of corridor, metres (a hub's hall).</summary>
        public static float WidestCorridor(LevelLayout l)
        {
            var plan = l.Plan;
            int best = 0;
            for (int f = 0; f < plan.Floors; f++)
            {
                var side = new int[plan.NX, plan.NZ];
                for (int x = 0; x < plan.NX; x++)
                    for (int z = 0; z < plan.NZ; z++)
                    {
                        if (!Corridor(plan, f, x, z)) continue;
                        side[x, z] = x == 0 || z == 0 ? 1 : 1 + Mathf.Min(side[x - 1, z], Mathf.Min(side[x, z - 1], side[x - 1, z - 1]));
                        best = Mathf.Max(best, side[x, z]);
                    }
            }
            return best * HousePlan.Res;
        }

        /// <summary>
        /// How mirrored the building is about its entrance: of the ground floor's cells, the share whose mirror image in the
        /// line through the lobby's middle (the better of the two ways) is in the building too. 1: a symmetrical house.
        /// </summary>
        public static float Symmetry(LevelLayout l)
        {
            var plan = l.Plan;
            var lobby = plan.Rooms[l.LobbyRoom].Bounds;
            float best = 0f;
            for (int axis = 0; axis < 2; axis++)
            {
                int mid = Mathf.RoundToInt((axis == 0 ? lobby.center.x : lobby.center.y) / HousePlan.Res), all = 0, same = 0;
                for (int x = 0; x < plan.NX; x++)
                    for (int z = 0; z < plan.NZ; z++)
                    {
                        if (!Inside(plan, 0, x, z)) continue;
                        all++;
                        if (Inside(plan, 0, axis == 0 ? 2 * mid - 1 - x : x, axis == 0 ? z : 2 * mid - 1 - z)) same++;
                    }
                best = Mathf.Max(best, same / (float)Mathf.Max(1, all));
            }
            return best;
        }

        /// <summary>The biggest room that rises through floors (an atrium, a court), m²; 0 without one.</summary>
        public static float Court(LevelLayout l) => l.Plan.Rooms.Where(r => r.Levels > 1).Select(r => r.Area).DefaultIfEmpty(0f).Max();
    }
}
