using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>A seat of the car (child of the Car prefab, <see cref="Interactable.Index"/> = the seat): E gets in; E again gets out.</summary>
    public class CarSeat : Interactable
    {
        public Car Car;
        [Tooltip("Which seat: 0 the driver's, 1 the passenger's, 2–3 the bed.")]
        public int Seat;

        public override UseKind Kind => UseKind.CarSeat;
        public override float Reach => 2.1f;
        public override Vector3 Point => transform.position + Vector3.up * 0.4f;
        // the seat is inside the cab: its walls must not hide it from the hands at the door
        public override Transform ReachRoot => Car != null ? Car.transform : transform;

        void Awake() => Index = Seat;

        public override string Prompt(PlayerController player, out bool available)
        {
            available = false;
            if (Car == null) return null;
            ulong who = Car.OccupantOf(Seat);
            if (who != Car.Nobody) return who == player.ClientId ? null : Loc.T("use.seat.taken");
            available = true;
            return Seat switch
            {
                0 => Loc.T("use.seat.driver"),
                1 => Loc.T("use.seat"),
                _ => Loc.T("use.seat"),
            };
        }
    }
}
