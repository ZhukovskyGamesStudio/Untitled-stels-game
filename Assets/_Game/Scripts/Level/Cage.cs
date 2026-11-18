using Stealth.Items;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The hostage's cage (Prefabs/Props/Cage, <see cref="LevelLayout.Cage"/>): a player carrying its key
    /// (<see cref="ItemCatalog.CageKey"/>, which one of the guards carries) opens it with E; the hostage slips away and
    /// leaves the team a reward (<see cref="Game.GameManager.UseServer"/>). Its door bars and the hostage are hidden once freed.
    /// </summary>
    public class Cage : Interactable
    {
        [Tooltip("The bars of the door (hidden once open) and their collider.")]
        public GameObject Door;
        public Collider DoorCollider;
        [Tooltip("The person inside (gone once freed).")]
        public GameObject Hostage;

        /// <summary>Which cages are open (set by the game; by prop index).</summary>
        public static System.Func<int, bool> IsFreed = _ => false;

        public override UseKind Kind => UseKind.Cage;
        public override float Reach => 2.2f;
        public override Vector3 Point => transform.position + transform.forward * (HouseFeatures.CageSize / 2f + 0.05f) + Vector3.up * 1.1f;
        public override float AimRadius => 0.8f;
        public bool Freed => Index >= 0 && IsFreed(Index);

        /// <summary>The key works only from the hand.</summary>
        public static bool HasKey(PlayerController player) =>
            player.TryGetComponent<PlayerInventory>(out var inventory) && inventory.Holds(ItemCatalog.CageKey);

        static bool Carries(PlayerController player) =>
            player.TryGetComponent<PlayerInventory>(out var inventory) && inventory.Has(ItemCatalog.CageKey);

        public override string Prompt(PlayerController player, out bool available)
        {
            // with the key: free the hostage; without: ask where it is (the hostage points out the guard who carries it)
            available = !Freed;
            if (Freed) return null;
            return HasKey(player) ? Loc.T("use.cage") : Carries(player) ? Loc.T("use.cage.hand") : Loc.T("use.cage.ask");
        }

        void LateUpdate()
        {
            bool closed = !Freed;
            if (Door != null && Door.activeSelf != closed) Door.SetActive(closed);
            if (DoorCollider != null && DoorCollider.enabled != closed) DoorCollider.enabled = closed;
            if (Hostage != null && Hostage.activeSelf != closed) Hostage.SetActive(closed);
        }
    }
}
