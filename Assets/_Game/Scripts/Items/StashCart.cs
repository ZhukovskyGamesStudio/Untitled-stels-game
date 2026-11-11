using System.Collections.Generic;
using Stealth.Game;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Items
{
    /// <summary>
    /// The team's cart in the sewer: every item in the team stash lies in its bed, laid out like the stash grid
    /// (one grid cell = one item cell, 0.22 m). Rebuilt when the stash changes; purely visual, on every peer.
    /// </summary>
    public class StashCart : MonoBehaviour
    {
        /// <summary>Size of an item cell in metres (the item models are built on it).</summary>
        public const float Cell = 0.22f;

        [Tooltip("The inner corner of the bed: +X along the grid's columns, +Z along its rows, +Y up.")]
        public Transform Bed;
        [Tooltip("A plain model of an item (mesh and material are set from the visuals).")]
        public MeshRenderer ItemPrefab;
        public ItemVisuals Visuals;

        readonly List<GameObject> m_Shown = new();
        int m_Version = -1;

        /// <summary>
        /// Where a stashed item lies in the bed (in the bed's space): over the middle of the grid cells it covers, turned and
        /// mirrored like in the stash grid. The item meshes are built with grid x along −X and grid y along +Z (Blender X
        /// becomes Unity −X), the bed runs grid x along +X and rows along −Z: an unturned piece lies half a turn around,
        /// each quarter turn of the grid is +90° of yaw, and a mirrored piece is flipped along the mesh's X first.
        /// </summary>
        public static (Vector3 position, Quaternion rotation, Vector3 scale) Place(InventoryEntry e)
        {
            var size = ItemCatalog.Size(ItemCatalog.RotatedCells(e.Shape, e.Rot));
            var centre = new Vector3((e.X + size.x / 2f) * Cell, 0f, (TeamStash.Height - e.Y - size.y / 2f) * Cell);
            var rotation = Quaternion.Euler(0f, 180f + 90f * (e.Rot & 3), 0f);
            var scale = ItemCatalog.IsMirrored(e.Rot) ? new Vector3(-1f, 1f, 1f) : Vector3.one;
            return (centre, rotation, scale);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || Bed == null || ItemPrefab == null || Visuals == null) return;
            if (gm.Stash.Version == m_Version) return;
            m_Version = gm.Stash.Version;
            foreach (var go in m_Shown) Destroy(go);
            m_Shown.Clear();
            foreach (var e in gm.Stash.Items)
            {
                var (position, rotation, scale) = Place(e);
                var item = Instantiate(ItemPrefab, Bed, false);
                item.transform.localPosition = position;
                item.transform.localRotation = rotation;
                item.transform.localScale = scale;
                item.GetComponent<MeshFilter>().sharedMesh = Visuals.Meshes[Mathf.Clamp(e.Shape, 0, Visuals.Meshes.Length - 1)];
                item.sharedMaterial = Visuals.ColorMaterials[Mathf.Clamp(e.Color, 0, Visuals.ColorMaterials.Length - 1)];
                VisualStyles.Dress(item.gameObject);
                m_Shown.Add(item.gameObject);
            }
        }
    }
}
