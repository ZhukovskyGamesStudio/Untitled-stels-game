using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// Inventory grid at the bottom of the screen (prefab UI/Inventory) and the drag-and-drop of loot.
    /// <list type="bullet">
    /// <item>Hold the left mouse button on an item in the world and drag it into the backpack: the camera stays put
    /// until the button is released (a click without dragging puts it into the first free spot).</item>
    /// <item>Hold Tab: the cursor appears; drag items between the backpack, the world, and at the hatch — the team
    /// stash grid hanging above it (<see cref="StashBoard"/>) and its "sell" area. R rotates the dragged item.</item>
    /// </list>
    /// Works on the local player's <see cref="PlayerInventory"/>; the server validates every request.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        public static InventoryUI Instance { get; private set; }

        public const float Cell = 32f;
        public const float CompactScale = 0.55f;
        /// <summary>Mouse travel (pixels) below which a world drag counts as a click.</summary>
        const float ClickTravel = 12f;

        [Header("Layout")]
        public RectTransform Canvas;
        public RectTransform Panel;
        public Image GridBackground;
        public GameObject Header;
        public Image HeaderBackground;
        public Text ValueLabel;
        public GameObject RotateHint;
        public RectTransform Grid;
        [Tooltip("Pre-built cell backgrounds, one per InventoryLayout cell.")]
        public Image[] Cells;

        [Header("Dynamic elements")]
        public RectTransform ItemLayer;
        public RectTransform HighlightLayer;
        public RectTransform Ghost;
        public InventoryBlockView BlockPrefab;
        public Image HighlightPrefab;
        public Text Tooltip;

        enum DragSource { None, Inventory, World, Stash }
        enum Target { None, Backpack, Stash, Sell }

        DragSource m_Source;
        int m_DragIndex = -1;
        WorldItem m_DragWorld;
        InventoryEntry m_DragStashed;
        int m_DragShape, m_DragColor, m_DragRot;
        float m_DragTravel;
        Vector2Int m_GrabCell;
        WorldItem m_Hover, m_Highlighted;
        string m_Tooltip;
        float m_Expand; // 0 = compact (just carrying), 1 = hands mode (Tab / dragging)
        UiPool<InventoryBlockView> m_Items, m_GhostBlocks, m_StashItems;
        UiPool<Image> m_Highlights, m_StashHighlights;

        /// <summary>World item under the crosshair / cursor (in reach), null if none.</summary>
        public WorldItem Hover => m_Hover;

        static PlayerController Me => PlayerController.Local;
        static StashBoard Board => StashBoard.Instance;
        static TeamStash Stash => GameManager.Instance != null ? GameManager.Instance.Stash : null;

        void Awake()
        {
            Instance = this;
            m_Items = new UiPool<InventoryBlockView>(BlockPrefab, ItemLayer);
            m_GhostBlocks = new UiPool<InventoryBlockView>(BlockPrefab, Ghost);
            m_Highlights = new UiPool<Image>(HighlightPrefab, HighlightLayer);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            PlayerController.HoldCursor = false;
        }

        // ------------------------------------------------------------------ input

        Vector2 MouseScreen => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        static bool CellIn(RectTransform grid, Camera cam, Vector2 screen, out Vector2Int cell)
        {
            bool hit = RectTransformUtility.ScreenPointToLocalPointInRectangle(grid, screen, cam, out var local);
            cell = new Vector2Int(Mathf.FloorToInt(local.x / Cell), Mathf.FloorToInt(-local.y / Cell));
            return hit && grid.rect.Contains(local);
        }

        bool MouseCell(out Vector2Int cell) => CellIn(Grid, null, MouseScreen, out cell);

        bool StashActive(PlayerController me)
        {
            var gm = GameManager.Instance;
            return Board != null && gm != null && gm.Phase == RunPhase.Playing && ActiveLevel.Layout != null && Hatch.IsNear(me.transform.position);
        }

        /// <summary>What the cursor is over: the backpack grid, the stash grid, the sell area or nothing.</summary>
        Target MouseTarget(PlayerController me, out Vector2Int cell)
        {
            if (MouseCell(out cell)) return Target.Backpack;
            if (!StashActive(me) || !Board.Visible) return Target.None;
            if (CellIn(Board.Grid, me.Camera, MouseScreen, out cell)) return Target.Stash;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(Board.SellZone, MouseScreen, me.Camera, out var local) &&
                Board.SellZone.rect.Contains(local)) return Target.Sell;
            return Target.None;
        }

        Vector2 MouseCanvas
        {
            get
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(Canvas, MouseScreen, null, out var local);
                return local + Canvas.rect.size * Canvas.pivot; // bottom-left origin
            }
        }

        void Update()
        {
            var me = Me;
            Ui.Show(Panel, me != null);
            m_Tooltip = null;
            if (me == null || Mouse.current == null)
            {
                CancelDrag();
                m_Hover = null;
                HideDynamic();
                if (Board != null) Board.Show(false);
                return;
            }
            var inv = me.GetComponent<PlayerInventory>();
            UpdateBoard(me);
            HandleInput(me, inv);
            Draw(me, inv);
        }

        void UpdateBoard(PlayerController me)
        {
            if (Board == null) return;
            bool on = StashActive(me), was = Board.Visible;
            Board.Show(on);
            if (!on) return;
            // Turns towards the viewer while the camera moves freely; stays still under the cursor.
            if (!me.CursorMode || !was) Board.Place(ActiveLevel.Layout.HatchPosition, me.Camera.transform.position);
            if (m_StashItems == null)
            {
                m_StashItems = new UiPool<InventoryBlockView>(BlockPrefab, Board.ItemLayer);
                m_StashHighlights = new UiPool<Image>(HighlightPrefab, Board.HighlightLayer);
            }
        }

        void HandleInput(PlayerController me, PlayerInventory inv)
        {
            var mouse = Mouse.current;
            if (me.Downed || GameMenu.IsOpen) { CancelDrag(); m_Hover = null; return; }

            if (!me.CursorMode && !PlayerController.HoldCursor)
            {
                CancelDrag();
                m_Hover = RaycastWorldItem(me, new Ray(me.Camera.transform.position, me.Camera.transform.forward));
                // Grab it: the camera freezes and the cursor appears until the button is released.
                if (m_Hover != null && mouse.leftButton.wasPressedThisFrame)
                {
                    BeginWorldDrag(m_Hover);
                    PlayerController.HoldCursor = true;
                }
                return;
            }

            var mouseRay = me.Camera.ScreenPointToRay(MouseScreen);
            var over = MouseTarget(me, out var cell);
            m_Hover = over != Target.None ? null : RaycastWorldItem(me, mouseRay);

            if (m_Source == DragSource.None)
            {
                BeginDrag(over, cell, inv);
                return;
            }

            if (m_Source == DragSource.World && (m_DragWorld == null || !m_DragWorld.isActiveAndEnabled)) { CancelDrag(); return; }
            if (m_Source == DragSource.Stash && (Stash == null || Stash.IndexOf(m_DragStashed) < 0)) { CancelDrag(); return; }

            m_DragTravel += mouse.delta.ReadValue().magnitude;
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                m_DragRot = (m_DragRot + 1) % 4;
                m_GrabCell = Vector2Int.zero;
            }
            if (!mouse.leftButton.isPressed) // released (or the press was lost)
            {
                Drop(me, inv, over, cell - m_GrabCell, mouseRay);
                CancelDrag();
            }
        }

        void BeginDrag(Target over, Vector2Int cell, PlayerInventory inv)
        {
            bool press = Mouse.current.leftButton.wasPressedThisFrame;
            if (over == Target.Backpack)
            {
                int idx = inv.IndexAt(cell);
                if (idx < 0) return;
                var e = inv.Items[idx];
                m_Tooltip = ItemCatalog.Describe(e.Shape, e.Color);
                if (!press) return;
                m_Source = DragSource.Inventory;
                m_DragIndex = idx;
                StartShape(e, cell);
            }
            else if (over == Target.Stash && Stash != null)
            {
                int idx = Stash.IndexAt(cell);
                if (idx < 0) return;
                var e = Stash.Items[idx];
                m_Tooltip = ItemCatalog.Describe(e.Shape, e.Color) + " · на складе команды";
                if (!press) return;
                m_Source = DragSource.Stash;
                m_DragStashed = e;
                StartShape(e, cell);
            }
            else if (over == Target.Sell)
            {
                m_Tooltip = "Перетащи сюда — продать на квоту";
            }
            else if (m_Hover != null)
            {
                m_Tooltip = ItemCatalog.Describe(m_Hover.Shape, m_Hover.Color);
                if (press) BeginWorldDrag(m_Hover);
            }
        }

        void StartShape(InventoryEntry e, Vector2Int cell)
        {
            m_DragShape = e.Shape; m_DragColor = e.Color; m_DragRot = e.Rot;
            m_GrabCell = cell - new Vector2Int(e.X, e.Y);
            m_DragTravel = 0f;
        }

        void BeginWorldDrag(WorldItem item)
        {
            m_Source = DragSource.World;
            m_DragWorld = item;
            m_DragShape = item.Shape; m_DragColor = item.Color; m_DragRot = 0;
            m_GrabCell = Vector2Int.zero;
            m_DragTravel = 0f;
        }

        void Drop(PlayerController me, PlayerInventory inv, Target over, Vector2Int target, Ray mouseRay)
        {
            byte x = (byte)Mathf.Clamp(target.x, 0, 255), y = (byte)Mathf.Clamp(target.y, 0, 255), rot = (byte)m_DragRot;
            bool inside = target.x >= 0 && target.y >= 0;
            switch (over)
            {
                case Target.Backpack:
                    if (!inside) return;
                    if (m_Source == DragSource.World && inv.CanPlace(m_DragShape, rot, x, y)) inv.RequestPickup(m_DragWorld, x, y, rot);
                    else if (m_Source == DragSource.Inventory && inv.CanPlace(m_DragShape, rot, x, y, m_DragIndex)) inv.RequestMove(m_DragIndex, x, y, rot);
                    else if (m_Source == DragSource.Stash && inv.CanPlace(m_DragShape, rot, x, y)) inv.RequestUnstash(m_DragStashed, x, y, rot);
                    return;
                case Target.Stash:
                    if (!inside || Stash == null) return;
                    if (m_Source == DragSource.Inventory && Stash.CanPlace(m_DragShape, rot, x, y)) inv.RequestStash(m_DragIndex, x, y, rot);
                    else if (m_Source == DragSource.World && Stash.CanPlace(m_DragShape, rot, x, y)) inv.RequestStashWorld(m_DragWorld, x, y, rot);
                    else if (m_Source == DragSource.Stash && Stash.CanPlace(m_DragShape, rot, x, y, Stash.IndexOf(m_DragStashed))) inv.RequestStashMove(m_DragStashed, x, y, rot);
                    return;
                case Target.Sell:
                    if (m_Source == DragSource.Inventory) inv.RequestDeposit(m_DragIndex);
                    else if (m_Source == DragSource.World) inv.RequestSellWorld(m_DragWorld);
                    else if (m_Source == DragSource.Stash) inv.RequestSellStashed(m_DragStashed);
                    return;
            }
            if (m_Source == DragSource.Inventory) DropIntoWorld(me, inv, mouseRay);
            // A click on a world item (no drag): straight into the first free spot of the backpack.
            else if (m_Source == DragSource.World && m_DragTravel < ClickTravel && inv.FindSpot(m_DragShape, out var fx, out var fy, out var fr))
                inv.RequestPickup(m_DragWorld, fx, fy, fr);
        }

        void DropIntoWorld(PlayerController me, PlayerInventory inv, Ray ray)
        {
            const int mask = ~(1 << LevelMetrics.CeilingLayer);
            if (Physics.Raycast(ray, out var hit, PlayerInventory.ReachDistance, mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<Hatch>() != null)
                {
                    inv.RequestDeposit(m_DragIndex);
                    return;
                }
                var point = hit.point;
                if (hit.normal.y < 0.5f &&
                    Physics.Raycast(hit.point + hit.normal * 0.3f + Vector3.up * 0.2f, Vector3.down, out var floor, 3f, mask, QueryTriggerInteraction.Ignore))
                    point = floor.point;
                inv.RequestDrop(m_DragIndex, point + Vector3.up * 0.01f, me.transform.eulerAngles.y);
                return;
            }
            inv.RequestDrop(m_DragIndex, me.transform.position + me.transform.forward * 0.8f, me.transform.eulerAngles.y);
        }

        void CancelDrag()
        {
            m_Source = DragSource.None;
            m_DragIndex = -1;
            m_DragWorld = null;
            PlayerController.HoldCursor = false;
        }

        static WorldItem RaycastWorldItem(PlayerController me, Ray ray)
        {
            var hits = Physics.RaycastAll(ray, PlayerInventory.ReachDistance, ~(1 << LevelMetrics.CeilingLayer), QueryTriggerInteraction.Collide);
            WorldItem best = null;
            float bestDist = float.MaxValue, wallDist = float.MaxValue;
            foreach (var h in hits)
            {
                var item = h.collider.GetComponentInParent<WorldItem>();
                if (item != null)
                {
                    if (h.distance < bestDist) { best = item; bestDist = h.distance; }
                }
                else if (!h.collider.isTrigger && h.collider.GetComponentInParent<PlayerController>() != me)
                {
                    wallDist = Mathf.Min(wallDist, h.distance);
                }
            }
            return best != null && bestDist <= wallDist + 0.05f ? best : null;
        }

        void LateUpdate()
        {
            var want = m_Source == DragSource.World ? m_DragWorld : m_Hover;
            if (want == m_Highlighted) return;
            if (m_Highlighted != null) m_Highlighted.SetHighlighted(false);
            m_Highlighted = want;
            if (m_Highlighted != null) m_Highlighted.SetHighlighted(true);
        }

        // ------------------------------------------------------------------ drawing

        void Draw(PlayerController me, PlayerInventory inv)
        {
            m_Expand = Mathf.MoveTowards(m_Expand, me.CursorMode ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            // Compact while walking around: small and quiet, it only shows what you carry.
            Panel.localScale = Vector3.one * Mathf.Lerp(CompactScale, 1f, m_Expand);
            float alpha = Mathf.Lerp(0.3f, 0.6f, m_Expand);
            Ui.Alpha(GridBackground, alpha);
            Ui.Alpha(HeaderBackground, alpha);
            bool header = m_Expand > 0.5f || inv.Items.Count > 0;
            Ui.Show(Header, header);
            if (header) ValueLabel.text = $"{inv.TotalValue} очк.";
            Ui.Show(RotateHint, m_Expand > 0.5f);
            float cellAlpha = Mathf.Lerp(0.04f, 0.07f, m_Expand);
            foreach (var c in Cells) Ui.Alpha(c, cellAlpha);

            m_Items.Begin();
            for (int i = 0; i < inv.Items.Count; i++)
            {
                if (m_Source == DragSource.Inventory && i == m_DragIndex) continue;
                var e = inv.Items[i];
                AddShape(m_Items, new Vector2Int(e.X, e.Y), e.Shape, e.Color, e.Rot, 1f);
            }
            m_Items.End();

            bool board = Board != null && Board.Visible && m_StashItems != null;
            if (board) DrawStashItems();

            m_Highlights.Begin();
            m_GhostBlocks.Begin();
            if (board) m_StashHighlights.Begin();
            bool dragging = m_Source != DragSource.None;
            string hint = null;
            bool overSell = false;
            if (dragging)
            {
                var over = MouseTarget(me, out var cell);
                var target = cell - m_GrabCell;
                if (over == Target.Backpack)
                {
                    bool ok = m_Source == DragSource.Inventory
                        ? inv.CanPlace(m_DragShape, m_DragRot, target.x, target.y, m_DragIndex)
                        : inv.CanPlace(m_DragShape, m_DragRot, target.x, target.y);
                    Highlight(m_Highlights, target, ok);
                }
                else if (over == Target.Stash && board)
                {
                    int ignore = m_Source == DragSource.Stash ? Stash.IndexOf(m_DragStashed) : -1;
                    Highlight(m_StashHighlights, target, Stash.CanPlace(m_DragShape, m_DragRot, target.x, target.y, ignore));
                    hint = m_Source == DragSource.Stash ? null : "Отпусти — оставить на складе команды";
                }
                else if (over == Target.Sell)
                {
                    overSell = true;
                    hint = $"Отпусти — продать за {ItemCatalog.Value(m_DragShape, m_DragColor)} очк. на квоту";
                }
                else if (m_Source == DragSource.Inventory)
                    hint = "Отпусти — положить";
                else if (m_Source == DragSource.World)
                    hint = m_DragTravel < ClickTravel ? null : "Тащи в рюкзак внизу экрана";
                Ghost.anchoredPosition = MouseCanvas - new Vector2(m_GrabCell.x * Cell + Cell / 2f, -(m_GrabCell.y * Cell + Cell / 2f));
                AddShape(m_GhostBlocks, Vector2Int.zero, m_DragShape, m_DragColor, m_DragRot, 0.75f);
            }
            m_Highlights.End();
            m_GhostBlocks.End();
            if (board)
            {
                m_StashHighlights.End();
                Board.SellBackground.color = overSell ? new Color(1f, 0.75f, 0.1f, 0.85f) : new Color(1f, 0.75f, 0.1f, 0.45f);
            }

            string tip = hint ?? (me.CursorMode && !dragging ? m_Tooltip : null);
            Ui.Show(Tooltip, tip != null);
            if (tip == null) return;
            Tooltip.text = tip;
            Tooltip.rectTransform.anchoredPosition = MouseCanvas + new Vector2(16f, -8f);
        }

        void DrawStashItems()
        {
            var stash = Stash;
            m_StashItems.Begin();
            if (stash != null)
                for (int i = 0; i < stash.Items.Count; i++)
                {
                    var e = stash.Items[i];
                    if (m_Source == DragSource.Stash && e.Equals(m_DragStashed)) continue;
                    AddShape(m_StashItems, new Vector2Int(e.X, e.Y), e.Shape, e.Color, e.Rot, 1f);
                }
            m_StashItems.End();
            Board.ValueLabel.text = $"Склад команды · {(stash != null ? stash.TotalValue : 0)} очк.";
        }

        /// <summary>Marks the cells the dragged item would take at <paramref name="target"/>: green if it fits.</summary>
        void Highlight(UiPool<Image> pool, Vector2Int target, bool ok)
        {
            foreach (var cc in ItemCatalog.RotatedCells(m_DragShape, m_DragRot))
            {
                var h = pool.Next();
                h.rectTransform.anchoredPosition = new Vector2((target.x + cc.x) * Cell + 1, -((target.y + cc.y) * Cell + 1));
                h.color = ok ? new Color(0.3f, 1f, 0.4f, 0.35f) : new Color(1f, 0.25f, 0.2f, 0.4f);
            }
        }

        static void AddShape(UiPool<InventoryBlockView> pool, Vector2Int origin, int shape, int color, int rot, float alpha)
        {
            var c = ItemCatalog.Colors[color].Color;
            foreach (var cell in ItemCatalog.RotatedCells(shape, rot))
                pool.Next().Set(new Vector2((origin.x + cell.x) * Cell + 2, -((origin.y + cell.y) * Cell + 2)), Cell - 4, c, alpha);
        }

        void HideDynamic()
        {
            m_Items.Begin(); m_Items.End();
            m_GhostBlocks.Begin(); m_GhostBlocks.End();
            m_Highlights.Begin(); m_Highlights.End();
            Ui.Show(Tooltip, false);
        }
    }
}
