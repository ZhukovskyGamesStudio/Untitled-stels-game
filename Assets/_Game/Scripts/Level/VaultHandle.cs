using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The wheel of the vault's round door (on the VaultDoor prefab): once the three lock panels have unlocked the door, a
    /// player opens it with E — the wheel spins the bolts back, then the door swings into the vault
    /// (<see cref="AutoDoor.IsSwungOpen"/>, <see cref="Game.GameManager.UseServer"/>). While it is locked the crosshair
    /// shows the wheel dim.
    /// </summary>
    public class VaultHandle : Interactable
    {
        public AutoDoor Door;

        public override UseKind Kind => UseKind.VaultDoor;
        public override float Reach => 2.4f;
        public override float AimRadius => 0.9f;
        /// <summary>The wheel's hub, seen through the bore from the corridor (the door's −Z side).</summary>
        public override Vector3 Point => transform.position + Vector3.up * 1.15f - transform.forward * 0.25f;

        public override string Prompt(PlayerController player, out bool available)
        {
            available = Door != null && !Door.Locked && !AutoDoor.IsSwungOpen(Door.Index);
            if (Door == null || AutoDoor.IsSwungOpen(Door.Index)) return null;
            return available ? Loc.T("use.vault_door") : Loc.T("use.vault_door.locked");
        }

        void LateUpdate()
        {
            // the door's index arrives with the level; an opened door needs no hint
            int index = Door != null && Door.Vault && !AutoDoor.IsSwungOpen(Door.Index) ? Door.Index : -1;
            if (Index != index) Index = index;
        }
    }
}
