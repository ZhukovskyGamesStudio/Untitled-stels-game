using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>
    /// The engine's hopper on the car's hood: it eats anything. Hold E: the loot from your backpack becomes fuel (with an
    /// empty backpack — everything heaped in the bed). The drive to the next house burns the house's "quota" of it.
    /// </summary>
    public class CarHopper : Interactable
    {
        public override UseKind Kind => UseKind.CarHopper;
        public override float HoldTime => 0.5f;
        public override float Reach => 2.3f;
        public override Vector3 Point => transform.position;

        void Awake() => Index = 0;

        /// <summary>Fuel the loot in this backpack gives.</summary>
        public static int BackpackFuel(PlayerInventory inventory)
        {
            int v = 0;
            if (inventory != null) foreach (var e in inventory.Items) if (!ItemCatalog.IsTool(e.Shape)) v += e.Value;
            return v;
        }

        public static int BedFuel(Cargo cargo)
        {
            int v = 0;
            foreach (var e in cargo.Items) if (!ItemCatalog.IsTool(e.Shape)) v += e.Value;
            return v;
        }

        public override string Prompt(PlayerController player, out bool available)
        {
            var gm = GameManager.Instance;
            int pack = BackpackFuel(player.GetComponent<PlayerInventory>());
            int bed = gm != null ? BedFuel(gm.Cargo) : 0;
            available = pack > 0 || bed > 0;
            if (pack > 0) return Loc.F("use.hopper", pack);
            if (bed > 0) return Loc.F("use.hopper.bed", bed);
            return Loc.T("use.hopper.empty");
        }
    }
}
