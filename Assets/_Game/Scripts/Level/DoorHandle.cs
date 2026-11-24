using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A door's leaves as something to use: E opens it, E again shuts it (<see cref="AutoDoor.ToggleServer"/>). On every door
    /// but the vault's. Not while it is locked (its lock is worked at instead) nor when doors open by themselves.
    /// </summary>
    public class DoorHandle : Interactable
    {
        public AutoDoor Door;

        public override UseKind Kind => UseKind.Door;
        public override float Reach => 2f;
        public override float AimRadius => 0.6f;
        public override Vector3 Point => transform.position + Vector3.up * 1.1f;

        /// <summary>Shut: anywhere on the leaves. Open: on a leaf where it stands now (the empty doorway is not the door).</summary>
        protected override (Vector3 from, Vector3 to, float radius) Span
        {
            get
            {
                if (Door == null || Door.OpenAmount < 0.5f) return (transform.position + Vector3.up * 0.3f, transform.position + Vector3.up * 2f, AimRadius);
                var leaf = Door.LeafMiddle(PlayerController.Local != null ? PlayerController.Local.View : null);
                return (new Vector3(leaf.x, transform.position.y + 0.4f, leaf.z), new Vector3(leaf.x, transform.position.y + 1.9f, leaf.z), 0.4f);
            }
        }

        public override string Prompt(PlayerController player, out bool available)
        {
            available = true;
            return Loc.T(Door != null && Door.OpenAmount > 0.5f ? "use.door_shut" : "use.door_open");
        }

        void LateUpdate()
        {
            // the door's spot arrives with the level
            int index = Door != null && Door.Spot >= 0 && !Door.Vault && !Door.Locked && !AutoDoor.Automatic ? Door.Spot : -1;
            if (Index != index) Index = index;
        }
    }
}
