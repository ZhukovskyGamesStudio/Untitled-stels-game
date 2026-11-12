using System.Collections.Generic;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Items
{
    /// <summary>Tuning of loot in a house (LevelConfig).</summary>
    [System.Serializable]
    public class LootSettings
    {
        [Tooltip("Items in the first house.")]
        public int BaseItems = 6;
        [Tooltip("More items per house.")]
        public int ItemsPerHouse = 2;
        [Tooltip("One more item per this many rooms of the building.")]
        public int RoomsPerExtraItem = 3;
        [Tooltip("Items never lie closer than this, meters.")]
        public float MinSpacing = 1.5f;

        [Header("Value bonus (shifts colours towards valuable ones)")]
        public int BonusPerHouse = 2;
        public int BonusPerFloor = 4;
        [Tooltip("Dark rooms are riskier and richer.")]
        public int DarkRoomBonus = 6;
    }

    /// <summary>Server-side loot placement rules (pure, unit tested).</summary>
    public static class LootPlanner
    {
        public struct Loot
        {
            public Vector3 Position;
            public int Shape;
            public int Color;
        }

        /// <summary>How many items a building gets: grows with the level and the number of rooms.</summary>
        public static int ItemCount(LevelLayout l, LootSettings s = null)
        {
            s ??= new LootSettings();
            return s.BaseItems + l.LevelIndex * s.ItemsPerHouse + Mathf.RoundToInt(l.RoomUnits) / Mathf.Max(1, s.RoomsPerExtraItem);
        }

        /// <summary>
        /// Picks loot positions: round-robin over floors (every floor gets loot), never closer than
        /// <see cref="LootSettings.MinSpacing"/> to each other. Value bonus: higher floors and dark rooms are richer.
        /// </summary>
        public static List<Loot> Plan(LevelLayout l, System.Random rng, LootSettings s = null)
        {
            s ??= new LootSettings();
            float spacing = s.MinSpacing;
            var byFloor = new List<List<Vector3>>();
            for (int f = 0; f < l.Floors; f++) byFloor.Add(new List<Vector3>());
            foreach (var spot in l.ItemSpots) byFloor[Mathf.Clamp(LevelLayout.FloorOf(spot.y), 0, l.Floors - 1)].Add(spot);

            int want = ItemCount(l, s);
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
                    if (result.Exists(r => (r.Position - p).sqrMagnitude < spacing * spacing)) continue;
                    int bonus = l.LevelIndex * s.BonusPerHouse + LevelLayout.FloorOf(p.y) * s.BonusPerFloor + (l.IsDarkAt(p + Vector3.up * 0.5f) ? s.DarkRoomBonus : 0);
                    result.Add(new Loot { Position = p, Shape = rng.Next(ItemCatalog.Shapes.Length), Color = ItemCatalog.RandomColor(rng, bonus) });
                    placed = true;
                }
                misses = placed ? 0 : misses + 1;
            }
            return result;
        }
    }
}
