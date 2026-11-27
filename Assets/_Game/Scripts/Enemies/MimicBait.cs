using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// The mimic's bait: while it sleeps it is "a crate" a player can search with E — the same prompt a real crate has
    /// (<see cref="SearchCrate"/>). Searching it wakes it (<see cref="RobotController.StirServer"/>).
    /// </summary>
    public class MimicBait : Interactable
    {
        [Tooltip("The mimic this bait is on.")]
        public RobotController Robot;

        public override UseKind Kind => UseKind.Mimic;
        public override float Reach => 1.8f;
        public override Vector3 Point => transform.position + Vector3.up * 0.5f;
        public override float AimRadius => 0.5f;

        /// <summary>Only a sleeping mimic passes for a crate.</summary>
        public override bool Usable(PlayerController player) => Robot != null && Robot.RestingShown && !Robot.IsHacked;

        public override string Prompt(PlayerController player, out bool available)
        {
            available = Usable(player);
            return available ? Loc.T("use.crate") : null;
        }

        void Update()
        {
            // which one of the mimics: its network id (the same on every peer), once it is spawned
            if (Index < 0 && Robot != null && Robot.IsSpawned) Configure((int)(Robot.NetId % 1000000));
        }
    }
}
