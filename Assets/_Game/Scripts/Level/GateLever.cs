using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>The sewer gate's lever (on the exit's side): E opens the gate for the car (<see cref="SewerGate"/>).</summary>
    public class GateLever : Interactable
    {
        public override UseKind Kind => UseKind.GateLever;
        public override float HoldTime => 0.6f;
        public override Vector3 Point => transform.position;

        public override string Prompt(PlayerController player, out bool available)
        {
            available = !SewerGate.IsOpen();
            return available ? Loc.T("use.lever") : null;
        }
    }
}
