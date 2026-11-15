using Stealth.Configs;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The hospital's point of interest (prefab Props/MedTable): lie on it (hold E) and your health goes up to
    /// <see cref="Items.GadgetSettings.MedTableHp"/> — more than the usual two — once per player per house.
    /// </summary>
    public class MedTable : Interactable
    {
        /// <summary>Has this player (client id) used the table in this house (set by the game)?</summary>
        public static System.Func<ulong, bool> UsedBy = _ => false;

        public override UseKind Kind => UseKind.MedTable;
        public override float HoldTime => 1.2f;
        public override float Reach => 2.0f;
        public override float AimRadius => 0.7f; // a whole table

        public override string Prompt(PlayerController player, out bool available)
        {
            int hp = Tuning.Items.Gadgets.MedTableHp;
            available = false;
            if (UsedBy(player.ClientId)) return Loc.T("use.med.used");
            if (player.Hp >= hp) return Loc.T("use.med.full");
            available = true;
            return Loc.T("use.med");
        }
    }
}
