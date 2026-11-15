using Stealth.Configs;
using Stealth.Items;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The garage's point of interest (prefab Props/Workbench): hold E and the things of one colour in your backpack become
    /// diamonds (<see cref="Crafting"/>): one cell instead of many, and worth more.
    /// </summary>
    public class Workbench : Interactable
    {
        public override UseKind Kind => UseKind.Workbench;
        public override float HoldTime => 1.5f;
        public override float Reach => 2.0f;
        public override float AimRadius => 0.7f; // a whole bench

        public override string Prompt(PlayerController player, out bool available)
        {
            var inventory = player.GetComponent<PlayerInventory>();
            var s = Tuning.Items.Gadgets;
            int count = inventory != null ? Crafting.Plan(inventory.Items, s).Count : 0;
            available = count > 0;
            return available
                ? Loc.F("use.workbench", count)
                : Loc.T("use.workbench.none");
        }
    }
}
