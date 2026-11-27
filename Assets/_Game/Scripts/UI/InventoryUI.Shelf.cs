using Stealth.Audio;
using Stealth.Game;
using Stealth.Items;
using Stealth.Player;
using Stealth.Vehicles;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// The inventory and a shelf of the car's bed (<see cref="PlayerController.AtShelf"/>): just its grid
    /// (<see cref="CarShelves.Columns"/> × <see cref="CarShelves.Rows"/>) above the backpack, where the bench's panel is;
    /// things go on and off it by drag, as on the bench.
    /// </summary>
    public partial class InventoryUI
    {
        [Header("Shelf (the car's)")]
        [Tooltip("A shelf's panel (shown while the local player has a shelf of the car open).")]
        public GameObject ShelfPanel;
        [Tooltip("Its title (the shelf's number).")]
        public Text ShelfTitle;
        [Tooltip("The shelf's grid (top-left pivot, cells like the backpack's).")]
        public RectTransform ShelfGrid;
        public Image[] ShelfCells;
        public RectTransform ShelfItems, ShelfHighlights;

        UiPool<InventoryBlockView> m_ShelfItems;
        UiPool<Image> m_ShelfIcons, m_ShelfHighlights;
        InventoryEntry m_DragShelved;
        /// <summary>The shelf the dragged thing came from.</summary>
        int m_DragShelf = -1;
        int m_ShelfTitled = -1;

        static TeamStash ShelfStash(PlayerController me) =>
            GameManager.Instance != null && me != null ? GameManager.Instance.Shelf(me.AtShelf) : null;

        void AwakeShelf()
        {
            if (ShelfItems == null) return;
            m_ShelfItems = new UiPool<InventoryBlockView>(BlockPrefab, ShelfItems);
            m_ShelfIcons = new UiPool<Image>(HighlightPrefab, ShelfItems);
            m_ShelfHighlights = new UiPool<Image>(HighlightPrefab, ShelfHighlights);
        }

        bool ShelfOpen(PlayerController me) => ShelfPanel != null && me != null && me.AtShelf >= 0 && ShelfStash(me) != null;

        /// <summary>The shelf's grid under the cursor (and the cell), or nothing.</summary>
        Target ShelfTarget(PlayerController me, out Vector2Int cell)
        {
            cell = default;
            return ShelfOpen(me) && CellIn(ShelfGrid, null, MouseScreen, out cell) ? Target.Shelf : Target.None;
        }

        void BeginShelfDrag(PlayerController me, Vector2Int cell, bool press)
        {
            var shelf = ShelfStash(me);
            if (shelf == null) return;
            int idx = shelf.IndexAt(cell);
            if (idx < 0) return;
            var e = shelf.Items[idx];
            m_Tooltip = ItemCatalog.Describe(e.Shape, e.Color);
            if (!press) return;
            m_Source = DragSource.Shelf;
            m_DragShelved = e;
            m_DragShelf = me.AtShelf;
            StartShape(e, cell);
        }

        /// <summary>The dragged thing is still where it was picked up from (the shelf is open, the thing is on it).</summary>
        bool ShelvedStillThere(PlayerController me)
        {
            var shelf = ShelfStash(me);
            return shelf != null && me.AtShelf == m_DragShelf && shelf.IndexOf(m_DragShelved) >= 0;
        }

        /// <summary>The dragged thing let go over a cell of the open shelf.</summary>
        void DropOnShelf(PlayerController me, PlayerInventory inv, byte x, byte y, byte rot)
        {
            var shelf = ShelfStash(me);
            if (shelf == null) return;
            bool placed = true;
            if (m_Source == DragSource.Inventory && shelf.CanPlace(m_DragShape, rot, x, y)) inv.RequestShelf(me.AtShelf, m_DragIndex, x, y, rot);
            else if (m_Source == DragSource.Shelf && shelf.CanPlace(m_DragShape, rot, x, y, shelf.IndexOf(m_DragShelved))) inv.RequestShelfMove(m_DragShelf, m_DragShelved, x, y, rot);
            else placed = false;
            if (placed) Sfx.Ui(Sfx.Bank?.Stash, 0.8f, Sfx.Group.Items);
        }

        /// <summary>A thing from the shelf into a cell of the backpack (or the hand: <see cref="PlayerInventory.HandX"/>).</summary>
        void UnshelfTo(PlayerInventory inv, byte x, byte y, byte rot)
        {
            inv.RequestUnshelf(m_DragShelf, m_DragShelved, x, y, rot);
            Sfx.Ui(Sfx.Bank?.Pickup, 0.8f, Sfx.Group.Items);
        }

        /// <summary>A thing from the shelf let go off its grid: into the first free spot of the backpack.</summary>
        void ShelfToBackpack(PlayerInventory inv)
        {
            if (inv.FindSpot(m_DragShape, out var x, out var y, out var r)) UnshelfTo(inv, x, y, r);
            else m_FullUntil = Time.time + 1.5f;
        }

        void HighlightShelf(PlayerController me, Vector2Int target)
        {
            var shelf = ShelfStash(me);
            if (m_ShelfHighlights == null || shelf == null) return;
            int ignore = m_Source == DragSource.Shelf ? shelf.IndexOf(m_DragShelved) : -1;
            bool ok = shelf.CanPlace(m_DragShape, m_DragRot, target.x, target.y, ignore);
            foreach (var cc in ItemCatalog.RotatedCells(m_DragShape, m_DragRot))
            {
                var h = m_ShelfHighlights.Next();
                h.rectTransform.anchoredPosition = new Vector2((target.x + cc.x) * Cell + 1, -((target.y + cc.y) * Cell + 1));
                h.color = ok ? UiPalette.MarkGreen.A(0.45f) : UiPalette.Stamp.A(0.4f);
            }
        }

        void DrawShelf(PlayerController me)
        {
            bool open = ShelfOpen(me);
            if (ShelfPanel == null) return;
            if (ShelfPanel.activeSelf != open) ShelfPanel.SetActive(open);
            if (!open || m_ShelfItems == null) return;
            if (ShelfTitle != null && m_ShelfTitled != me.AtShelf)
            {
                m_ShelfTitled = me.AtShelf;
                ShelfTitle.text = Loc.F("shelf.title", me.AtShelf + 1);
            }
            var shelf = ShelfStash(me);
            bool dragging = m_Source != DragSource.None;
            foreach (var c in ShelfCells) Ui.Alpha(c, dragging ? 0.16f : 0.08f);
            m_ShelfItems.Begin();
            m_ShelfIcons.Begin();
            foreach (var e in shelf.Items)
            {
                if (m_Source == DragSource.Shelf && e.Equals(m_DragShelved)) continue;
                AddShape(m_ShelfItems, m_ShelfIcons, new Vector2Int(e.X, e.Y), e.Shape, e.Color, e.Rot, 1f);
            }
            m_ShelfItems.End();
            m_ShelfIcons.End();
        }

        void HideShelf()
        {
            if (ShelfPanel != null && ShelfPanel.activeSelf) ShelfPanel.SetActive(false);
        }
    }
}
