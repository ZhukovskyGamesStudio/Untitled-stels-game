using System.Collections.Generic;
using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>
    /// A shelf of the trunk upgrade in the car's bed (Tools/Blender/car_upgrades.py car_shelves; one per level, shown with
    /// it: <see cref="CarUpgradeLook"/>): E at it opens its grid beside the backpack (<see cref="PlayerController.AtShelf"/>,
    /// <see cref="UI.InventoryUI"/>), and what the team keeps there (<see cref="GameManager.Shelves"/>) lies on its board in
    /// its own size, as on the grid (<see cref="CarShelves.Place"/>) — on every peer, rebuilt when the shelf changes.
    /// </summary>
    public class CarShelf : Interactable
    {
        [Tooltip("Which shelf: 0 the low one (trunk level 1), 1 the one at the wall's top (2), 2 the rack's top (3).")]
        public int Number;
        [Tooltip("The grid's corner on the board's top: +X along the board, rows along −Z, +Y up (CarShelves.Place).")]
        public Transform Board;
        [Tooltip("The board's pieces (each a MeshFilter with a MeshRenderer under Board, hidden until a thing lies there): one per cell of the grid at most.")]
        public MeshFilter[] Pieces;
        public ItemVisuals Visuals;

        public override UseKind Kind => UseKind.CarShelf;
        public override float Reach => 2.2f; // from beside the car over its wall, or standing in the bed
        public override Vector3 Point => Board != null ? Board.TransformPoint(new Vector3(CarShelves.GridSize.x / 2f, 0.08f, -CarShelves.GridSize.y / 2f)) : transform.position;
        public override float AimRadius => 0.3f;
        /// <summary>The crosshair anywhere along the board's length takes it.</summary>
        protected override (Vector3 from, Vector3 to, float radius) Span
        {
            get
            {
                if (Board == null) return base.Span;
                float y = 0.08f, z = -CarShelves.GridSize.y / 2f;
                return (Board.TransformPoint(new Vector3(0.1f, y, z)), Board.TransformPoint(new Vector3(CarShelves.GridSize.x - 0.1f, y, z)), AimRadius);
            }
        }
        public override Transform ReachRoot => m_Root != null ? m_Root : m_Root = GetComponentInParent<Car>() is Car car ? car.transform : transform; // on the car's own bed
        Transform m_Root;

        readonly GameObject[] m_Models = new GameObject[CarShelves.Columns * CarShelves.Rows];
        int m_Version = -1;

        void Awake() => Index = Number;

        public override string Prompt(PlayerController player, out bool available)
        {
            available = true;
            return Loc.T("use.shelf");
        }

        void Update()
        {
            var gm = GameManager.Instance;
            var shelf = gm != null ? gm.Shelf(Number) : null;
            if (shelf == null || Board == null || Pieces == null || Visuals == null || shelf.Version == m_Version) return;
            m_Version = shelf.Version;
            Show(shelf.Items);
        }

        /// <summary>The things of the shelf on its board: each a plain piece, or its own model laid flat on the board.</summary>
        void Show(IReadOnlyList<InventoryEntry> items)
        {
            for (int i = 0; i < Pieces.Length && i < m_Models.Length; i++)
            {
                var filter = Pieces[i];
                if (filter == null) continue;
                var piece = filter.GetComponent<MeshRenderer>();
                bool on = i < items.Count;
                var model = on && Visuals.Models != null && items[i].Shape < Visuals.Models.Length ? Visuals.Models[items[i].Shape] : null;
                if (m_Models[i] != null && (model == null || m_Models[i].name != model.name)) { Destroy(m_Models[i]); m_Models[i] = null; }
                if (!on) { piece.enabled = false; continue; }
                var e = items[i];
                var (position, rotation, scale) = CarShelves.Place(e);
                filter.transform.SetLocalPositionAndRotation(position + Vector3.up * 0.003f, rotation);
                filter.transform.localScale = scale;
                filter.sharedMesh = Visuals.Meshes[Mathf.Clamp(e.Shape, 0, Visuals.Meshes.Length - 1)];
                piece.sharedMaterial = Visuals.ColorMaterials[Mathf.Clamp(e.Color, 0, Visuals.ColorMaterials.Length - 1)];
                if (model != null && m_Models[i] == null)
                {
                    m_Models[i] = Instantiate(model, filter.transform);
                    m_Models[i].name = model.name;
                    foreach (var c in m_Models[i].GetComponentsInChildren<Collider>(true)) c.enabled = false; // (only to look at)
                }
                if (m_Models[i] != null)
                {
                    // lying on its thinnest side, its length along the grid's (as on Linus's pack)
                    var rest = RaccoonPack.RestOf(e.Shape, model, filter.sharedMesh);
                    m_Models[i].transform.SetLocalPositionAndRotation(rest.Offset, rest.Turn);
                }
                piece.enabled = m_Models[i] == null;
                VisualStyles.Dress(filter.gameObject);
            }
        }
    }
}
