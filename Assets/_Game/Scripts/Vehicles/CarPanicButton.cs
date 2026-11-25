using Stealth.Game;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>
    /// The teleport's big red button on the glovebox (shown once the unique upgrade is fitted, CarUpgradeLook): the driver
    /// or the passenger looks at it and presses E — everyone is brought to the car (<see cref="GameManager.TeleportServer"/>).
    /// Then it recharges: fuel put into the shredder over what the drive needs (<see cref="CarUpgradeSettings.TeleportRecharge"/>).
    /// </summary>
    public class CarPanicButton : Interactable
    {
        public Car Car;

        public override UseKind Kind => UseKind.CarPanic;
        public override bool FromSeat => true;
        public override float Reach => 1.5f;
        public override float AimRadius => 0.14f;
        public override Vector3 Point => transform.position;
        public override Transform ReachRoot => Car != null ? Car.transform : transform;

        void Awake() => Index = 0;

        /// <summary>From the front seats only (the bed is too far from the glovebox).</summary>
        public override bool Usable(PlayerController player)
        {
            if (Car == null || player == null) return false;
            int seat = Car.SeatOf(player.ClientId);
            return seat is 0 or 1;
        }

        public override string Prompt(PlayerController player, out bool available)
        {
            var gm = GameManager.Instance;
            available = Usable(player) && gm != null && gm.TeleportReady;
            if (!Usable(player)) return null;
            return available ? Loc.T("use.teleport") : Loc.F("use.teleport.charging", gm != null ? gm.TeleportChargePercent : 0);
        }
    }
}
