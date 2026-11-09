using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// Inventory grid at the bottom of the screen (prefab UI/Inventory). Hold Tab: the camera freezes and the cursor
    /// appears. Drag a world item onto the grid to pick it up, drag a grid item into the world to drop it, drag it
    /// onto the hatch to hand it in for the quota. R rotates the dragged item.
    /// Works on the local player's <see cref="PlayerInventory"/>; the server validates every request.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        public static InventoryUI Instance { get; private set; }

        public const float Cell = 32f;
        public const float CompactScale = 0.55f;

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

        enum DragSource { None, Inventory, World }

        DragSource m_Source;
        int m_DragIndex = -1;
        WorldItem m_DragWorld;
        int m_DragShape, m_DragColor, m_DragRot;
        Vector2Int m_GrabCell;
        WorldItem m_Hover, m_Highlighted;
        string m_Tooltip;
        float m_Expand; // 0 = compact (just carrying), 1 = hands mode (Tab)
        UiPool<InventoryBlockView> m_Items, m_GhostBlocks;
        UiPool<Image> m_Highlights;

        /// <summary>World item under the crosshair / cursor (in reach), null if none.</summary>
        public WorldItem Hover => m_Hover;

        static PlayerController Me => PlayerController.Local;

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
        }

        // ------------------------------------------------------------------ input

        Vector2 MouseScreen => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        bool MouseCell(out Vector2Int cell)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Grid, MouseScreen, null, out var local);
            cell = new Vector2Int(Mathf.FloorToInt(local.x / Cell), Mathf.FloorToInt(-local.y / Cell));
            return Grid.rect.Contains(local);
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
                return;
            }
            var inv = me.GetComponent<PlayerInventory>();
            HandleInput(me, inv);
            Draw(me, inv);
        }

        void HandleInput(PlayerController me, PlayerInventory inv)
        {
            if (!me.CursorMode)
            {
                CancelDrag();
                m_Hover = RaycastWorldItem(me, new Ray(me.Camera.transform.position, me.Camera.transform.forward));
                return;
            }

            var mouseRay = me.Camera.ScreenPointToRay(MouseScreen);
            bool overGrid = MouseCell(out var cell);
            m_Hover = overGrid ? null : RaycastWorldItem(me, mouseRay);
            var mouse = Mouse.current;

            if (m_Source == DragSource.None)
            {
                if (overGrid)
                {
                    int idx = inv.IndexAt(cell);
                    if (idx >= 0) m_Tooltip = ItemCatalog.Describe(inv.Items[idx].Shape, inv.Items[idx].Color);
                    if (idx >= 0 && mouse.leftButton.wasPressedThisFrame)
                    {
                        var e = inv.Items[idx];
                        m_Source = DragSource.Inventory;
                        m_DragIndex = idx;
                        m_DragShape = e.Shape; m_DragColor = e.Color; m_DragRot = e.Rot;
                        m_GrabCell = cell - new Vector2Int(e.X, e.Y);
                    }
                }
                else if (m_Hover != null)
                {
                    m_Tooltip = ItemCatalog.Describe(m_Hover.Shape, m_Hover.Color);
                    if (mouse.leftButton.wasPressedThisFrame)
                    {
                        m_Source = DragSource.World;
                        m_DragWorld = m_Hover;
                        m_DragShape = m_Hover.Shape; m_DragColor = m_Hover.Color; m_DragRot = 0;
                        m_GrabCell = Vector2Int.zero;
                    }
                }
                return;
            }

            if (m_Source == DragSource.World && (m_DragWorld == null || !m_DragWorld.isActiveAndEnabled)) { CancelDrag(); return; }

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                m_DragRot = (m_DragRot + 1) % 4;
                m_GrabCell = Vector2Int.zero;
            }
            if (!mouse.leftButton.wasReleasedThisFrame) return;

            var target = cell - m_GrabCell;
            if (overGrid)
            {
                if (m_Source == DragSource.World && inv.CanPlace(m_DragShape, m_DragRot, target.x, target.y))
                    inv.RequestPickup(m_DragWorld, (byte)target.x, (byte)target.y, (byte)m_DragRot);
                else if (m_Source == DragSource.Inventory && target.x >= 0 && target.y >= 0 &&
                         inv.CanPlace(m_DragShape, m_DragRot, target.x, target.y, m_DragIndex))
                    inv.RequestMove(m_DragIndex, (byte)target.x, (byte)target.y, (byte)m_DragRot);
            }
            else if (m_Source == DragSource.Inventory)
            {
                DropIntoWorld(me, inv, mouseRay);
            }
            CancelDrag();
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
            // Nothing in reach: drop at the feet (or into the hatch when looking down at it).
            if (Hatch.IsNear(me.transform.position) && Vector3.Dot(ray.direction, Vector3.down) > 0.5f)
            {
                inv.RequestDeposit(m_DragIndex);
                return;
            }
            inv.RequestDrop(m_DragIndex, me.transform.position + me.transform.forward * 0.8f, me.transform.eulerAngles.y);
        }

        void CancelDrag()
        {
            m_Source = DragSource.None;
            m_DragIndex = -1;
            m_DragWorld = null;
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

            m_Highlights.Begin();
            m_GhostBlocks.Begin();
            bool dragging = m_Source != DragSource.None;
            string hint = null;
            if (dragging)
            {
                bool overGrid = MouseCell(out var cell);
                var target = cell - m_GrabCell;
                if (overGrid)
                {
                    bool ok = m_Source == DragSource.World
                        ? inv.CanPlace(m_DragShape, m_DragRot, target.x, target.y)
                        : inv.CanPlace(m_DragShape, m_DragRot, target.x, target.y, m_DragIndex);
                    foreach (var cc in ItemCatalog.RotatedCells(m_DragShape, m_DragRot))
                    {
                        var h = m_Highlights.Next();
                        h.rectTransform.anchoredPosition = new Vector2((target.x + cc.x) * Cell + 1, -((target.y + cc.y) * Cell + 1));
                        h.color = ok ? new Color(0.3f, 1f, 0.4f, 0.35f) : new Color(1f, 0.25f, 0.2f, 0.4f);
                    }
                }
                Ghost.anchoredPosition = MouseCanvas - new Vector2(m_GrabCell.x * Cell + Cell / 2f, -(m_GrabCell.y * Cell + Cell / 2f));
                AddShape(m_GhostBlocks, Vector2Int.zero, m_DragShape, m_DragColor, m_DragRot, 0.75f);
                if (!overGrid && m_Source == DragSource.Inventory)
                    hint = Hatch.IsNear(me.transform.position) ? "Отпусти на люке — сдать на квоту" : "Отпусти — положить";
            }
            m_Highlights.End();
            m_GhostBlocks.End();

            string tip = hint ?? (me.CursorMode && !dragging ? m_Tooltip : null);
            Ui.Show(Tooltip, tip != null);
            if (tip == null) return;
            Tooltip.text = tip;
            Tooltip.rectTransform.anchoredPosition = MouseCanvas + new Vector2(16f, -8f);
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
