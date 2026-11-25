using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>
    /// A front door of the car seen from inside (child of the Car prefab, <see cref="Interactable.Index"/> = its seat): the
    /// one who sits on that seat looks at the door and presses E to get out — E anywhere else no longer drops them out of
    /// the car (the panic button on the glovebox is pressed the same way). The bed's seats have no door: E gets out there.
    /// </summary>
    public class CarDoor : Interactable
    {
        public Car Car;
        [Tooltip("Whose door: 0 the driver's, 1 the passenger's.")]
        public int Seat;

        public override UseKind Kind => UseKind.CarSeat;   // (the server's seat toggle: pressed on one's own seat, one gets out)
        public override bool FromSeat => true;
        public override float Reach => 1.6f;
        public override float AimRadius => 0.32f;
        public override Transform ReachRoot => Car != null ? Car.transform : transform;

        /// <summary>The door's inside handle: beside the seat on the car's side (it follows the seat when the wheel changes sides).</summary>
        public override Vector3 Point
        {
            get
            {
                if (Car == null || Seat >= Car.Seats.Length || Car.Seats[Seat] == null) return transform.position;
                var seat = Car.Seats[Seat].position;
                float side = Mathf.Sign(Car.transform.InverseTransformPoint(seat).x);
                return seat + Car.transform.right * ((side == 0f ? 1f : side) * DoorOut) + Car.transform.up * DoorUp;
            }
        }

        /// <summary>The handle from the seat's point: out to the door, up to the arm's height.</summary>
        public const float DoorOut = 0.5f, DoorUp = 0.42f;

        void Awake() => Index = Seat;

        public override bool Usable(PlayerController player) => Car != null && player != null && Car.SeatOf(player.ClientId) == Seat;

        public override string Prompt(PlayerController player, out bool available)
        {
            available = Usable(player);
            return available ? Loc.T("use.seat.out") : null;
        }
    }
}
