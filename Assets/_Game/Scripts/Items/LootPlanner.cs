using System.Collections.Generic;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Items
{
    /// <summary>Server-side loot placement rules (pure, unit tested).</summary>
    public static class LootPlanner
    {
        public const float MinSpacing = 1.5f;

        public struct Loot
        {
            public Vector3 Position;
            public int Shape;
            public int Color;
        }

        /// <summary>How many items a building gets: grows with the level and the number of rooms.</summary>
        public static int ItemCount(LevelLayout l) =>
            6 + l.LevelIndex * 2 + l.Width * l.Height * l.Floors / 3;

        /// <summary>
        /// Picks loot positions: round-robin over floors (every floor gets loot), never closer than
        /// <see cref="MinSpacing"/> to each other. Value bonus: higher floors and dark rooms are richer.
        /// </summary>
        public static List<Loot> Plan(LevelLayout l, System.Random rng)
        {
            var byFloor = new List<List<Vector3>>();
            for (int f = 0; f < l.Floors; f++) byFloor.Add(new List<Vector3>());
            foreach (var s in l.ItemSpots) byFloor[Mathf.Clamp(LevelLayout.FloorOf(s.y), 0, l.Floors - 1)].Add(s);

            int want = ItemCount(l);
            var result = new List<Loot>();
            int floor = 0, misses = 0;
            while (result.Count < want && misses < l.Floors)
            {
                var spots = byFloor[floor];
                floor = (floor + 1) % l.Floors;
                bool placed = false;
                while (spots.Count > 0 && !placed)
                {
                    int i = rng.Next(spots.Count);
                    var p = spots[i];
                    spots.RemoveAt(i);
                    if (result.Exists(r => (r.Position - p).sqrMagnitude < MinSpacing * MinSpacing)) continue;
                    int bonus = l.LevelIndex * 2 + LevelLayout.FloorOf(p.y) * 4 + (l.IsDarkAt(p + Vector3.up * 0.5f) ? 6 : 0);
                    result.Add(new Loot { Position = p, Shape = rng.Next(ItemCatalog.Shapes.Length), Color = ItemCatalog.RandomColor(rng, bonus) });
                    placed = true;
                }
                misses = placed ? 0 : misses + 1;
            }
            return result;
        }
    }
}
