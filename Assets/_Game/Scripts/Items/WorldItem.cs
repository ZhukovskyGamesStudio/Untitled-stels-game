using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Items
{
    /// <summary>
    /// An item lying in the world. Picked up by dragging it into the inventory grid.
    /// Its look (<see cref="Shape"/>, <see cref="Color"/>) is set by the server before spawning and replicated by the
    /// network twin (WorldItemNet) through <see cref="SetLook"/>.
    /// </summary>
    public class WorldItem : MonoBehaviour
    {
        public static readonly List<WorldItem> All = new();

        public ItemVisuals Visuals;
        public MeshFilter MeshFilter;
        public MeshRenderer Renderer;
        public BoxCollider Collider;

        public byte Shape { get; private set; }
        public byte Color { get; private set; }

        public int Value => ItemCatalog.Value(Shape, Color);

        MaterialPropertyBlock m_Block;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void SetLook(byte shape, byte color)
        {
            Shape = shape;
            Color = color;
            Apply();
        }

        /// <summary>Local-only highlight while the item is under the crosshair / cursor.</summary>
        public void SetHighlighted(bool on)
        {
            if (Renderer == null || Visuals == null) return;
            if (!on) { Renderer.SetPropertyBlock(null); return; }
            m_Block ??= new MaterialPropertyBlock();
            var c = ItemCatalog.Colors[Mathf.Clamp(Color, 0, ItemCatalog.Colors.Length - 1)].Color;
            m_Block.SetColor("_EmissionColor", c * 1.5f); // brighter, but keeps its colour (= value)
            Renderer.SetPropertyBlock(m_Block);
        }

        void Apply()
        {
            if (Visuals == null) return;
            var mesh = Visuals.Meshes[Mathf.Clamp(Shape, 0, Visuals.Meshes.Length - 1)];
            MeshFilter.sharedMesh = mesh;
            Renderer.sharedMaterial = Visuals.ColorMaterials[Mathf.Clamp(Color, 0, Visuals.ColorMaterials.Length - 1)];
            if (mesh != null)
            {
                var b = mesh.bounds;
                Collider.center = b.center;
                Collider.size = b.size + Vector3.one * 0.12f; // a bit larger: easier to grab with the mouse
            }
        }
    }
}
