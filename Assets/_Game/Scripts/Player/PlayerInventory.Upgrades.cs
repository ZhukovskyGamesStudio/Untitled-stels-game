using System.Collections.Generic;
using Stealth.Game;
using Stealth.Vehicles;

namespace Stealth.Player
{
    /// <summary>The backpack at the garage's car upgrade bench: what it holds for a recipe, the request, the things taken.</summary>
    public partial class PlayerInventory
    {
        /// <summary>Owner: ask the server to raise a car upgrade one level (it checks the bench, the level and the recipe).</summary>
        public void RequestCarUpgrade(CarUpgrade upgrade) => Link.RequestCarUpgrade((byte)upgrade);

        /// <summary>
        /// The shape of every thing a recipe may take (index = the backpack's), -1 for one it may not: the worn belt bag
        /// itself (the things in it count, like the one in the hand).
        /// </summary>
        public void CarriedForRecipe(List<int> into)
        {
            into.Clear();
            for (int i = 0; i < m_Items.Count; i++) into.Add(Worn(m_Items[i]) ? -1 : m_Items[i].Shape);
        }

        /// <summary>Whether this backpack holds everything of <paramref name="recipe"/> (every peer: backpacks are replicated).</summary>
        public bool HoldsRecipe(IReadOnlyList<byte> recipe)
        {
            CarriedForRecipe(s_Carried);
            return CarUpgrades.Gather(s_Carried, recipe, s_Take);
        }

        /// <summary>For each thing of <paramref name="recipe"/>, whether this backpack has it (the bench ticks them off).</summary>
        public void RecipeHave(IReadOnlyList<byte> recipe, List<bool> have)
        {
            have.Clear();
            CarriedForRecipe(s_Carried);
            s_Take.Clear();
            foreach (byte need in recipe)
            {
                int found = -1;
                for (int i = 0; i < s_Carried.Count && found < 0; i++)
                    if (s_Carried[i] == need && !s_Take.Contains(i)) found = i;
                if (found >= 0) s_Take.Add(found);
                have.Add(found >= 0);
            }
        }

        static readonly List<int> s_Carried = new(), s_Take = new();

        /// <summary>Server: these things are used up (indices of the backpack, each once).</summary>
        public bool TakeServer(List<int> indices)
        {
            if (indices == null || indices.Count == 0) return false;
            foreach (int i in indices)
                if (i < 0 || i >= m_Items.Count) return false;
            var sorted = new List<int>(indices);
            sorted.Sort();
            for (int k = sorted.Count - 1; k >= 0; k--) m_Items.RemoveAt(sorted[k]);
            Changed();
            return true;
        }

        /// <summary>Server: the owner's request to raise a car upgrade.</summary>
        public void CarUpgradeServer(byte upgrade)
        {
            if (CanAct && upgrade < CarUpgrades.Count) GameManager.Instance?.CarUpgradeServer(m_Player, (CarUpgrade)upgrade);
        }
    }
}
