using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>The sewer gate's lever (on the exit's side): E opens the gate for the car (<see cref="SewerGate"/>).</summary>
    public class GateLever : Interactable
    {
        public override UseKind Kind => UseKind.GateLever;
        public override float HoldTime => 0.6f;
        /// <summary>
        /// Where the hands reach and the crosshair aims: the handle sticks out of the tunnel's wall, its pivot is in the
        /// wall — a point there was behind the brick for the reach ray, and E never found the lever. 0.4 m out into the
        /// tunnel (towards the gate's middle), still on the exit's side: through the shut gate it cannot be reached.
        /// </summary>
        public override Vector3 Point
        {
            get
            {
                var gate = transform.parent;
                if (gate == null) return transform.position;
                float side = Mathf.Sign(gate.InverseTransformPoint(transform.position).z);
                return transform.position - gate.forward * (side * OffWall);
            }
        }

        public override float AimRadius => 0.5f;

        /// <summary>How far out of the wall <see cref="Point"/> is.</summary>
        public const float OffWall = 0.4f;

        public override string Prompt(PlayerController player, out bool available)
        {
            available = !SewerGate.IsOpen();
            return available ? Loc.T("use.lever") : null;
        }
    }
}
