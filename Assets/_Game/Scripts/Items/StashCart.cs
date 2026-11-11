using System.Collections.Generic;
using Stealth.Game;
using Stealth.Level;
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
                var cells = ItemCatalog.RotatedCells(e.Shape, e.Rot);
                var size = ItemCatalog.Size(cells);
                // the middle of the cells it covers, grid rows towards +Z
                var centre = new Vector3((e.X + size.x / 2f) * Cell, 0f, (TeamStash.Height - e.Y - size.y / 2f) * Cell);
                var item = Instantiate(ItemPrefab, Bed, false);
                item.transform.localPosition = centre;
                item.transform.localRotation = Quaternion.Euler(0f, -90f * (e.Rot & 3), 0f);
                item.GetComponent<MeshFilter>().sharedMesh = Visuals.Meshes[Mathf.Clamp(e.Shape, 0, Visuals.Meshes.Length - 1)];
                item.sharedMaterial = Visuals.ColorMaterials[Mathf.Clamp(e.Color, 0, Visuals.ColorMaterials.Length - 1)];
                VisualStyles.Dress(item.gameObject);
                m_Shown.Add(item.gameObject);
            }
        }
    }
}
