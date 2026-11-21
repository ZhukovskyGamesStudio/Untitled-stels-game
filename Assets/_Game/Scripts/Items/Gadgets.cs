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

        [Header("Flashlight")]
        [Tooltip("Seconds a full flashlight shines before it is empty (a battery fills it again).")]
        public float FlashlightSeconds = 180f;

        [Header("Lure speaker")]
        [Tooltip("Seconds it plays when dropped (unless a robot switches it off first).")]
        public float LureDuration = 25f;
        [Tooltip("The nearest robot within this distance (on its floor) comes to switch it off.")]
        public float LureRange = 22f;
        [Tooltip("A robot this close switches it off.")]
        public float LureSwitchOffDistance = 1.5f;
        [Tooltip("Seconds after being dropped before it starts playing (time to walk away).")]
        public float LureDelay = 1.5f;

        [Header("Hand dryer (restrooms)")]
        [Tooltip("Seconds it roars once switched on.")]
        public float DryerTime = 4f;
        [Tooltip("How far robots hear it, metres (through walls and doors as any noise).")]
        public float DryerNoise = 14f;

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

        [Header("Starting items and the classes' tools")]
        [Tooltip("Instant coffee: seconds it lasts once drunk.")]
        public float CoffeeSeconds = 60f;
        [Tooltip("Instant coffee: walking and running speed × this.")]
        public float CoffeeMoveScale = 1.5f;
        [Tooltip("Instant coffee: every held-E work (hacking, unscrewing, picking locks, searching) goes this many times faster.")]
        public float CoffeeWorkScale = 2f;
        [Tooltip("Fuel finder: blinks fastest this close to loot (and closer), metres.")]
        public float FinderNear = 2f;
        [Tooltip("Fuel finder: blinks slowest this far from loot (and further), metres; beyond it, only rarely.")]
        public float FinderFar = 30f;
        [Tooltip("Fuel finder: seconds between blinks, near and far.")]
        public float FinderFastPeriod = 0.18f, FinderSlowPeriod = 1.8f;
        [Tooltip("Fuel finder: loot right behind the holder reads as this many times further than the same loot straight ahead (it points where you look).")]
        public float FinderBehind = 4f;
        [Tooltip("Radio: a teammate speaking makes the radio in someone's hand sound — robots hear it within this radius, metres.")]
        public float RadioNoise = 7f;
        [Tooltip("A fall from this high (and higher) slows a player down, metres (not with the boots).")]
        public float FallSlowFrom = 3f;
        [Tooltip("A fall from higher than this also hurts, metres (not with the boots).")]
        public float FallHurtFrom = 6f;
        [Tooltip("Seconds a hard landing slows a player down.")]
        public float FallSlowSeconds = 5f;
        [Tooltip("The speed left right after a landing from FallSlowFrom, and from FallHurtFrom and higher (× walking speed).")]
        public float FallSlowLight = 0.7f, FallSlowHeavy = 0.35f;
    }

    /// <summary>
    /// Rules of the starting items and the classes' tools (pure, unit tested): the coffee's boost, a hard landing, the fuel
    /// finder's blink.
    /// </summary>
    public static class GearRules
    {
        /// <summary>How high a body fell, metres, from its speed when it landed (m/s).</summary>
        public static float FallHeight(float impactSpeed, float gravity = 9.81f) => impactSpeed * impactSpeed / (2f * Mathf.Max(0.01f, gravity));

        /// <summary>
        /// What a landing from <paramref name="height"/> does: the speed left right after it (1 = none; it wears off over
        /// <see cref="GadgetSettings.FallSlowSeconds"/>) and whether it hurts. The boots take any fall.
        /// </summary>
        public static (float slow, bool hurt) Landing(float height, bool boots, GadgetSettings s)
        {
            if (boots || height < s.FallSlowFrom) return (1f, false);
            float t = Mathf.InverseLerp(s.FallSlowFrom, s.FallHurtFrom, height);
            return (Mathf.Lerp(s.FallSlowLight, s.FallSlowHeavy, t), height > s.FallHurtFrom);
        }

        /// <summary>The speed scale <paramref name="since"/> seconds after a landing that left <paramref name="slow"/>: back to 1 linearly.</summary>
        public static float SlowNow(float slow, float since, float seconds) =>
            since >= seconds || seconds <= 0f ? 1f : Mathf.Lerp(slow, 1f, Mathf.Clamp01(since / seconds));

        /// <summary>Seconds between the fuel finder's blinks at <paramref name="distance"/> metres from the nearest loot (∞: none in the house).</summary>
        public static float FinderPeriod(float distance, GadgetSettings s)
        {
            if (float.IsInfinity(distance)) return s.FinderSlowPeriod * 2f;
            float t = Mathf.InverseLerp(s.FinderNear, s.FinderFar, distance);
            return Mathf.Lerp(s.FinderFastPeriod, s.FinderSlowPeriod, t);
        }

        /// <summary>
        /// How far loot "reads" on the finder: its distance, stretched the further it is from where the holder looks
        /// (<paramref name="cosine"/> of the angle between the look and the way to it: ahead ×1, aside ×½(1+behind), behind ×behind).
        /// </summary>
        public static float FinderReading(float distance, float cosine, GadgetSettings s) =>
            distance * Mathf.Lerp(1f, Mathf.Max(1f, s.FinderBehind), (1f - Mathf.Clamp(cosine, -1f, 1f)) * 0.5f);

        /// <summary>Whether the finder's lamp is lit at <paramref name="phase"/> seconds into a blink of <paramref name="period"/>: a short flash.</summary>
        public static bool FinderLit(float phase, float period) => phase < Mathf.Min(0.09f, period * 0.5f);
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
