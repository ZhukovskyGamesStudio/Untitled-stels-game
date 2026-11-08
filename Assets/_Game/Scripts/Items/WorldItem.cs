using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Stealth.Items
{
    /// <summary>An item lying in the world. Picked up by dragging it into the inventory grid.</summary>
    public class WorldItem : NetworkBehaviour
    {
        public static readonly List<WorldItem> All = new();

        public ItemVisuals Visuals;
        public MeshFilter MeshFilter;
        public MeshRenderer Renderer;
        public BoxCollider Collider;

        public readonly NetworkVariable<byte> Shape = new();
        public readonly NetworkVariable<byte> Color = new();

        public int Value => ItemCatalog.Value(Shape.Value, Color.Value);

        MaterialPropertyBlock m_Block;

        /// <summary>Local-only highlight while the item is under the crosshair / cursor.</summary>
        public void SetHighlighted(bool on)
        {
            if (Renderer == null || Visuals == null) return;
            if (!on) { Renderer.SetPropertyBlock(null); return; }
            m_Block ??= new MaterialPropertyBlock();
            var c = ItemCatalog.Colors[Mathf.Clamp(Color.Value, 0, ItemCatalog.Colors.Length - 1)].Color;
            m_Block.SetColor("_EmissionColor", c * 1.5f); // brighter, but keeps its colour (= value)
            Renderer.SetPropertyBlock(m_Block);
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            Apply();
            Shape.OnValueChanged += (_, _) => Apply();
            Color.OnValueChanged += (_, _) => Apply();
        }

        public override void OnNetworkDespawn() => All.Remove(this);

        void Apply()
        {
            if (Visuals == null) return;
            var mesh = Visuals.Meshes[Mathf.Clamp(Shape.Value, 0, Visuals.Meshes.Length - 1)];
            MeshFilter.sharedMesh = mesh;
            Renderer.sharedMaterial = Visuals.ColorMaterials[Mathf.Clamp(Color.Value, 0, Visuals.ColorMaterials.Length - 1)];
            if (mesh != null)
            {
                var b = mesh.bounds;
                Collider.center = b.center;
                Collider.size = b.size + Vector3.one * 0.12f; // a bit larger: easier to grab with the mouse
            }
        }
    }
}
