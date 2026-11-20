using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The ladder in the sewer under the house's exit hatch (prefab Level/Sewer/Ladder): E climbs it — the player comes out
    /// sitting in the exit hatch up in the house (<see cref="Game.GameManager.UseServer"/>). The other way: sitting in the
    /// exit hatch, crouch goes down to its foot. The team goes in and out of the house as often as it likes
    /// (<see cref="SewerLocation.UnderHouse"/>); in the sewer between houses there is no house above it to climb into.
    /// </summary>
    public class ExitLadder : Interactable
    {
        public override UseKind Kind => UseKind.ExitLadder;
        public override float Reach => 1.6f;
        public override Vector3 Point => transform.position + Vector3.up * 1.2f;
        public override float AimRadius => 0.7f;

        public override string Prompt(PlayerController player, out bool available)
        {
            var sewer = GetComponentInParent<SewerLocation>();
            available = sewer != null && sewer.UnderHouse && ActiveLevel.Layout != null && ActiveLevel.Ready; // (the server checks the rest)
            return available ? Loc.T("use.ladder_up") : null;
        }
    }
}
