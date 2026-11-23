using System.Collections.Generic;

namespace Stealth.Player
{
    /// <summary>What a slot of a rigged human's wardrobe does to wear an outfit.</summary>
    public enum WearAction
    {
        /// <summary>Wears the person's own item of the slot (the one in their model).</summary>
        Own,
        /// <summary>Wears another item, made for this person (a WearPiece).</summary>
        Swap,
        /// <summary>Nothing in the slot.</summary>
        Bare,
    }

    /// <summary>
    /// The wardrobe's decisions (pure): which slot an item goes in, and what each slot does to wear an outfit — a list of
    /// item names, one a slot at most; a slot the outfit does not name is left bare.
    /// </summary>
    public static class WardrobeRules
    {
        /// <summary>The slots of a rigged human, in the order they are worn: clothes, then hair, then what sits on the head.</summary>
        public static readonly string[] Slots = { "top", "bottom", "feet", "hair", "beard", "head", "eyes" };

        /// <summary>The slot of a renderer of a person's own outfit by its name ("BodyClothes.top" — top); null for the body.</summary>
        public static string SlotOfPart(string rendererName)
        {
            int dot = rendererName.LastIndexOf('.');
            if (dot < 0) return null;
            string slot = rendererName.Substring(dot + 1);
            return System.Array.IndexOf(Slots, slot) >= 0 ? slot : null;
        }

        /// <summary>The part of a renderer without its slot ("BodyClothes.top" — BodyClothes).</summary>
        public static string PartOf(string rendererName)
        {
            int dot = rendererName.LastIndexOf('.');
            return dot < 0 || SlotOfPart(rendererName) == null ? rendererName : rendererName.Substring(0, dot);
        }

        /// <summary>
        /// What every slot does: <paramref name="own"/> — the person's own item of each slot (slot → item), <paramref name="outfit"/>
        /// — the items to wear, <paramref name="slotOf"/> — an item's slot (null: no such item, it is ignored).
        /// </summary>
        public static Dictionary<string, (WearAction action, string item)> Plan(IReadOnlyDictionary<string, string> own, IEnumerable<string> outfit,
                                                                               System.Func<string, string> slotOf)
        {
            var wanted = new Dictionary<string, string>();
            foreach (var item in outfit)
            {
                string slot = item != null ? slotOf(item) : null;
                if (slot != null) wanted[slot] = item;                      // (the last one named for a slot wins)
            }
            var plan = new Dictionary<string, (WearAction, string)>();
            foreach (var slot in Slots)
            {
                if (!wanted.TryGetValue(slot, out var item)) plan[slot] = (WearAction.Bare, null);
                else if (own.TryGetValue(slot, out var mine) && mine == item) plan[slot] = (WearAction.Own, item);
                else plan[slot] = (WearAction.Swap, item);
            }
            return plan;
        }
    }
}
