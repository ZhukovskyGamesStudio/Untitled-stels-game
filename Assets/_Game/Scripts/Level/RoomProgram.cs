using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>One room the house is to have: its type and its size (never under its type's smallest, <see cref="RoomRules"/>).</summary>
    public struct ProgramRoom
    {
        public RoomType Type;
        /// <summary>Floor area, m².</summary>
        public float Area;
        /// <summary>The house must have it (the storage room; every type in the rooms test): never left out for lack of room.</summary>
        public bool Required;
        /// <summary>A linked house: the block sized for this room (−1: the plan finds it a place).</summary>
        public int Block;
    }

    /// <summary>
    /// The house's room program (pure), decided before any wall: which rooms each floor has and how big each is. The plan
    /// is then laid out to hold them (<see cref="PlanGenerator"/>): the building's size comes from the rooms, every room
    /// gets a place of at least its size. Corridors, the lobby and the stair halls are not in it — the plan adds them, and
    /// they never count as rooms.
    /// </summary>
    public sealed class RoomProgram
    {
        public readonly List<ProgramRoom>[] Floors;

        public RoomProgram(int floors)
        {
            Floors = new List<ProgramRoom>[Mathf.Max(1, floors)];
            for (int f = 0; f < Floors.Length; f++) Floors[f] = new List<ProgramRoom>();
        }

        /// <summary>The rooms' floor area of a floor, m².</summary>
        public float AreaOf(int f)
        {
            float a = 0f;
            foreach (var r in Floors[f]) a += r.Area;
            return a;
        }

        public int Count
        {
            get
            {
                int n = 0;
                foreach (var list in Floors) n += list.Count;
                return n;
            }
        }

        /// <summary>
        /// Draws the program: the required rooms first (a storage room if the house has one; every type at least once with
        /// <see cref="BuildingSettings.EveryRoomType"/>), spread over the floors; then every floor is filled up to
        /// <paramref name="roomArea"/> m² of rooms with types drawn by <see cref="BuildingSettings.RoomWeights"/>, each of a
        /// size between its type's smallest and biggest (<see cref="RoomRules"/>).
        /// </summary>
        public static RoomProgram Make(int seed, int floors, float roomArea, BuildingSettings b, bool storage)
        {
            var p = new RoomProgram(floors);
            var rng = new System.Random(LevelRandom.Mix(seed, 0x9206, 0));
            var rules = b.RoomTypeRules;
            int next = 0;
            var house = new Dictionary<RoomType, int>();
            var onFloor = new Dictionary<(int, RoomType), int>();
            void Count(int f, RoomType t)
            {
                house[t] = (house.TryGetValue(t, out var h) ? h : 0) + 1;
                onFloor[(f, t)] = (onFloor.TryGetValue((f, t), out var n) ? n : 0) + 1;
            }
            bool Full(int f, RoomType t) =>
                RoomRules.Capped(RoomRules.For(t, rules), house.TryGetValue(t, out var h) ? h : 0, onFloor.TryGetValue((f, t), out var n) ? n : 0);
            void Add(int f, RoomType t, bool required)
            {
                p.Floors[f].Add(new ProgramRoom { Type = t, Area = Draw(rng, t, rules), Required = required, Block = -1 });
                Count(f, t);
            }
            if (b.EveryRoomType)
                foreach (var t in RoomRules.Programmed) Add(next++ % p.Floors.Length, t, true);
            else if (storage) Add(0, RoomType.Storage, true); // on the ground floor: its own dead-end block in a linked house
            for (int f = 0; f < p.Floors.Length; f++)
            {
                for (int guard = 0; guard < 200 && p.AreaOf(f) < roomArea; guard++)
                {
                    // a type that has reached its cap (one kitchen a floor, one gym a house…) is drawn again
                    var t = Pick(rng, b);
                    for (int again = 0; again < 12 && Full(f, t); again++) t = Pick(rng, b);
                    if (Full(f, t)) t = RoomType.Office;
                    Count(f, t);
                    float area = Draw(rng, t, rules);
                    // the last room of a floor: no bigger than what is left, if its type allows
                    float left = roomArea - p.AreaOf(f);
                    if (area > left) area = Mathf.Max(RoomRules.MinArea(t, rules), left);
                    p.Floors[f].Add(new ProgramRoom { Type = t, Area = area, Block = -1 });
                }
            }
            return p;
        }

        /// <summary>A size for a room of this type: anywhere between its smallest and biggest.</summary>
        public static float Draw(System.Random rng, RoomType t, RoomRule[] rules)
        {
            float lo = Mathf.Max(6f, RoomRules.MinArea(t, rules)), hi = RoomRules.MaxArea(t, rules);
            return lo + (float)rng.NextDouble() * (hi - lo);
        }

        /// <summary>A type by the weights (the locked storage room is not drawn: its weight stands for offices).</summary>
        public static RoomType Pick(System.Random rng, BuildingSettings b)
        {
            int total = 0;
            foreach (var w in b.RoomWeights) if (Drawn(w.Room)) total += Mathf.Max(0, w.Weight);
            if (total <= 0) return RoomType.Office;
            int roll = rng.Next(total);
            foreach (var w in b.RoomWeights)
            {
                if (!Drawn(w.Room)) continue;
                roll -= Mathf.Max(0, w.Weight);
                if (roll < 0) return w.Room == RoomType.Storage ? RoomType.Office : w.Room;
            }
            return RoomType.Office;
        }

        static bool Drawn(RoomType t) => t is not (RoomType.Lobby or RoomType.Hall or RoomType.Corridor);
    }
}
