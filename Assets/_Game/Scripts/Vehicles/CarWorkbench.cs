using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>
    /// The workbench in the car's bed (Tools/Blender/car_bench.py): E opens it — the backpack opens as with Tab and the
    /// bench's 4 x 4 grid shows beside it (<see cref="UI.InventoryUI"/>); the bench's things are the team's
    /// (<see cref="Game.GameManager.Bench"/>). It rides with the car: in the sewer and in the house's basement alike.
    /// </summary>
    public class CarWorkbench : Interactable
    {
        public override UseKind Kind => UseKind.CarBench;
        public override float Reach => 2.4f; // from behind the tailgate too
        public override Vector3 Point => transform.position;
        public override float AimRadius => 0.55f;
        public override Transform ReachRoot => m_Root != null ? m_Root : m_Root = GetComponentInParent<Car>() is Car car ? car.transform : transform; // on the car's own bed
        Transform m_Root;

        void Awake() => Index = 0;

        public override string Prompt(PlayerController player, out bool available)
        {
            available = true;
            return Loc.T("use.bench");
        }
    }
}
