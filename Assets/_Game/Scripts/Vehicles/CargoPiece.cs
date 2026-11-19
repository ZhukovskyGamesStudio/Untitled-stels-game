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
        /// <summary>Its size in the shredder's mouth (CarShredder): a long thing shrunk to fit.</summary>
        public float FitScale { get; set; } = 1f;

        /// <summary>A piece in the bed is taken with E; one in the shredder's mouth (index −1) only shows what is being eaten.</summary>
        public override bool Usable(PlayerController player) => Index >= 0;

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
            // a tool with a model of its own (the lure speaker): that model, as on the floor, not the tinted piece
            var model = visuals.Models != null && item.Shape < visuals.Models.Length ? visuals.Models[item.Shape] : null;
            if (m_Special != null && (model == null || m_Special.name != model.name)) { Destroy(m_Special); m_Special = null; }
            if (model != null && m_Special == null)
            {
                m_Special = Instantiate(model, transform);
                m_Special.name = model.name;
                Level.VisualStyles.Dress(m_Special);
            }
            Renderer.forceRenderingOff = m_Special != null;
        }

        GameObject m_Special;

        public override string Prompt(PlayerController player, out bool available)
        {
            available = true;
            return Loc.T("use.cargo");
        }
    }
}
