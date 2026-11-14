using System.Collections.Generic;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Items
{
    /// <summary>Tuning of the tools and places of a house (ItemsConfig): stun cube, lure speaker, crates, workbench, med table.</summary>
    [System.Serializable]
    public class GadgetSettings
    {
        [Header("Stun cube (the office's robot programming console)")]
        [Tooltip("Seconds every robot and camera of the house is stunned.")]
        public float StunDuration = 10f;

        [Header("Lure speaker")]
        [Tooltip("Seconds it plays when dropped (unless a robot switches it off first).")]
        public float LureDuration = 25f;
        [Tooltip("The nearest robot within this distance (on its floor) comes to switch it off.")]
        public float LureRange = 22f;
        [Tooltip("A robot this close switches it off.")]
        public float LureSwitchOffDistance = 1.5f;
        [Tooltip("Seconds after being dropped before it starts playing (time to walk away).")]
        public float LureDelay = 1.5f;

        [Header("Crates")]
        [Tooltip("Seconds of holding E to search a crate.")]
        public float CrateSearchTime = 1.2f;
        [Tooltip("Items in a crate (min, max).")]
        public Vector2Int CrateItems = new(1, 3);
        [Tooltip("Extra colour bonus of crate loot (shifts it towards valuable colours).")]
        public int CrateBonus = 4;

        [Header("Workbench (garage): items of one colour become a diamond")]
        [Tooltip("Cells of items of one colour that make a diamond.")]
        public int DiamondInputCells = 8;
        [Tooltip("A diamond is worth this many cells of its colour (more than went in) and takes one cell.")]
        public int DiamondValueCells = 10;

        [Header("Med table (hospital)")]
        [Tooltip("Hit points the med table gives (once per player per house): more than the usual two.")]
        public int MedTableHp = 3;
    }

    /// <summary>The workbench's rule (pure, unit tested): which items of the backpack become diamonds.</summary>
    public static class Crafting
    {
        /// <summary>One diamond to make: the indices of the items that go into it and its colour.</summary>
        public struct Recipe
        {
            public List<int> Items;
            public int Color;
        }

        /// <summary>
        /// Items of one colour (loot only, not tools or diamonds) are combined, biggest first, until they make at least
        /// <see cref="GadgetSettings.DiamondInputCells"/> cells; every such group becomes a diamond of that colour.
        /// What is left over stays as it is.
        /// </summary>
        public static List<Recipe> Plan(IReadOnlyList<InventoryEntry> items, GadgetSettings s = null)
        {
            s ??= new GadgetSettings();
            var result = new List<Recipe>();
            var byColor = new Dictionary<int, List<int>>();
            for (int i = 0; i < items.Count; i++)
            {
                if (!ItemCatalog.IsLoot(items[i].Shape)) continue;
                if (!byColor.TryGetValue(items[i].Color, out var list)) byColor[items[i].Color] = list = new List<int>();
                list.Add(i);
            }
            foreach (var (color, list) in byColor)
            {
                list.Sort((a, b) => ItemCatalog.Cells(items[b].Shape).CompareTo(ItemCatalog.Cells(items[a].Shape)));
                int total = 0;
                foreach (int i in list) total += ItemCatalog.Cells(items[i].Shape);
                while (total >= s.DiamondInputCells && list.Count > 0)
                {
                    var take = new List<int>();
                    int cells = 0;
                    // biggest pieces first, then the smallest that tops it up (so as little as possible is wasted)
                    for (int k = 0; k < list.Count && cells < s.DiamondInputCells; k++)
                    {
                        int need = s.DiamondInputCells - cells;
                        int pick = k;
                        int c = ItemCatalog.Cells(items[list[k]].Shape);
                        if (c > need)
                            for (int j = list.Count - 1; j > k; j--)
                                if (ItemCatalog.Cells(items[list[j]].Shape) >= need) { pick = j; break; }
                        take.Add(list[pick]);
                        cells += ItemCatalog.Cells(items[list[pick]].Shape);
                        list.RemoveAt(pick);
                        k--;
                    }
                    if (cells < s.DiamondInputCells) break;
                    total -= cells;
                    result.Add(new Recipe { Items = take, Color = color });
                }
            }
            return result;
        }
    }
}
