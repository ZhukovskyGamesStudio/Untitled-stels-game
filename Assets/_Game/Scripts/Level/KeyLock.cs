using Stealth.Items;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The lock of a key door (on the AutoDoor prefab; active only on doors of <see cref="LevelLayout.KeyDoors"/>): a
    /// player with a key block in the backpack opens it with E, the key is used up (<see cref="Game.GameManager.UseServer"/>).
    /// </summary>
    public class KeyLock : Interactable
    {
        public AutoDoor Door;
        [Tooltip("The lock panels beside the frame: shown only on key doors.")]
        public GameObject[] Panels;

        public override UseKind Kind => UseKind.KeyDoor;
        public override float Reach => 2.0f;
        public override Vector3 Point => transform.position + Vector3.up * 1.1f;

        public static bool HasKey(PlayerController player)
        {
            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null) return false;
            foreach (var e in inventory.Items) if (e.Shape == ItemCatalog.Key) return true;
            return false;
        }

        public override string Prompt(PlayerController player, out bool available)
        {
            available = Door != null && Door.Locked && HasKey(player);
            if (Door == null || !Door.Locked) return null;
            return available ? Loc.T("use.keydoor") : Loc.T("use.keydoor.nokey");
        }

        void LateUpdate()
        {
            // the door's index arrives with the level; open doors need no hint
            int index = Door != null && Door.NeedsKey && !Door.Vault && Door.Locked ? Door.Index : -1;
            if (Index != index) Index = index;
            bool show = Door != null && Door.NeedsKey && !Door.Vault; // the vault's door has its panels instead
            if (Panels != null)
                foreach (var p in Panels)
                    if (p != null && p.activeSelf != show) p.SetActive(show);
        }
    }
}
