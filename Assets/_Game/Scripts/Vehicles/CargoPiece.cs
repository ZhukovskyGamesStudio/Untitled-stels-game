using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>A thing lying in the car's bed (prefab Vehicles/CargoPiece, shown by <see cref="Car"/>): E takes it into the backpack.</summary>
    public class CargoPiece : Interactable
    {
        public MeshFilter MeshFilter;
        public MeshRenderer Renderer;

        public CargoItem Item { get; private set; }

        public override UseKind Kind => UseKind.CarCargo;
        public override float Reach => 2.2f;
        public override Vector3 Point => transform.position;
        public override float AimRadius => 0.3f; // pieces lie close together: the one looked at
        public override Transform ReachRoot => GetComponentInParent<Car>() is Car car ? car.transform : transform; // inside the bed's walls

        public void Show(int index, CargoItem item, ItemVisuals visuals)
        {
            Index = index;
            if (Item.Equals(item) && MeshFilter.sharedMesh != null) return;
            Item = item;
            if (visuals == null) return;
            MeshFilter.sharedMesh = visuals.Meshes[Mathf.Clamp(item.Shape, 0, visuals.Meshes.Length - 1)];
            Renderer.sharedMaterial = visuals.ColorMaterials[Mathf.Clamp(item.Color, 0, visuals.ColorMaterials.Length - 1)];
        }

        public override string Prompt(PlayerController player, out bool available)
        {
            available = true;
            return Loc.T("use.cargo");
        }
    }
}
