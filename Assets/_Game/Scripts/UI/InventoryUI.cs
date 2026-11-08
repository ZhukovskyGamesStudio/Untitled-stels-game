using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stealth.UI
{
    /// <summary>
    /// Inventory grid at the bottom of the screen. Hold Tab: the camera freezes and the cursor appears.
    /// Drag a world item onto the grid to pick it up, drag a grid item into the world to drop / use it,
    /// drag it onto the hatch to hand it in for the quota. R rotates the dragged item.
    /// </summary>
    public class InventoryUI : NetworkBehaviour
    {
        public static InventoryUI Local { get; private set; }
        /// <summary>Test hook: behave as if Tab is held.</summary>
        public static bool ForceHandsModeForTest;
        public WorldItem Hover => m_Hover;
        WorldItem m_Highlighted;

        void LateUpdate()
        {
            if (!IsOwner) return;
            var want = m_Source == DragSource.World ? m_DragWorld : m_Hover;
            if (want == m_Highlighted) return;
            if (m_Highlighted != null) m_Highlighted.SetHighlighted(false);
            m_Highlighted = want;
            if (m_Highlighted != null) m_Highlighted.SetHighlighted(true);
        }

        public PlayerController Player;
        public PlayerInventory Inventory;

        enum DragSource { None, Inventory, World }

        DragSource m_Source;
        int m_DragIndex = -1;
        WorldItem m_DragWorld;
        int m_DragShape, m_DragColor, m_DragRot;
        Vector2Int m_GrabCell;
        WorldItem m_Hover;
        string m_Tooltip;

        public override void OnNetworkSpawn()
        {
            if (IsOwner) Local = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Local == this) Local = null;
        }

        float CellPx => Mathf.Round(Mathf.Clamp(UiDraw.H / 22f, 26f, 56f));

        Rect GridRect => GridRectFor(CellPx);

        static Rect GridRectFor(float c)
        {
            float w = InventoryLayout.Width * c, h = InventoryLayout.Height * c;
            return new Rect((UiDraw.W - w) / 2f, UiDraw.H - h - 18f, w, h);
        }

        float m_Expand; // 0 = compact (just carrying), 1 = hands mode (Tab)

        static Vector2 MouseGui
        {
            get
            {
                var m = Mouse.current.position.ReadValue() / UiDraw.ScaleFactor;
                return new Vector2(m.x, UiDraw.H - m.y);
            }
        }

        bool MouseCell(out Vector2Int cell)
        {
            var r = GridRect;
            var m = MouseGui;
            cell = new Vector2Int(Mathf.FloorToInt((m.x - r.x) / CellPx), Mathf.FloorToInt((m.y - r.y) / CellPx));
            return r.Contains(m);
        }

        void Update()
        {
            if (!IsOwner || Mouse.current == null) return;
            m_Tooltip = null;

            if (!Player.CursorMode)
            {
                CancelDrag();
                m_Hover = RaycastWorldItem(new Ray(Player.Camera.transform.position, Player.Camera.transform.forward));
                return;
            }

            var mouseRay = Player.Camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            bool overGrid = MouseCell(out var cell);
            m_Hover = overGrid ? null : RaycastWorldItem(mouseRay);

            if (m_Source == DragSource.None)
            {
                if (overGrid)
                {
                    int idx = Inventory.IndexAt(cell);
                    if (idx >= 0) m_Tooltip = ItemCatalog.Describe(Inventory.Items[idx].Shape, Inventory.Items[idx].Color);
                    if (idx >= 0 && Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        var e = Inventory.Items[idx];
                        m_Source = DragSource.Inventory;
                        m_DragIndex = idx;
                        m_DragShape = e.Shape; m_DragColor = e.Color; m_DragRot = e.Rot;
                        m_GrabCell = cell - new Vector2Int(e.X, e.Y);
                    }
                }
                else if (m_Hover != null)
                {
                    m_Tooltip = ItemCatalog.Describe(m_Hover.Shape.Value, m_Hover.Color.Value);
                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        m_Source = DragSource.World;
                        m_DragWorld = m_Hover;
                        m_DragShape = m_Hover.Shape.Value; m_DragColor = m_Hover.Color.Value; m_DragRot = 0;
                        m_GrabCell = Vector2Int.zero;
                    }
                }
                return;
            }

            if (m_Source == DragSource.World && (m_DragWorld == null || !m_DragWorld.IsSpawned)) { CancelDrag(); return; }

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                m_DragRot = (m_DragRot + 1) % 4;
                m_GrabCell = Vector2Int.zero;
            }

            if (!Mouse.current.leftButton.wasReleasedThisFrame) return;

            var target = cell - m_GrabCell;
            if (overGrid)
            {
                if (m_Source == DragSource.World && Inventory.CanPlace(m_DragShape, m_DragRot, target.x, target.y))
                    Inventory.PickupRpc(m_DragWorld.NetworkObject, (byte)target.x, (byte)target.y, (byte)m_DragRot);
                else if (m_Source == DragSource.Inventory && target.x >= 0 && target.y >= 0 &&
                         Inventory.CanPlace(m_DragShape, m_DragRot, target.x, target.y, m_DragIndex))
                    Inventory.MoveRpc(m_DragIndex, (byte)target.x, (byte)target.y, (byte)m_DragRot);
            }
            else if (m_Source == DragSource.Inventory)
            {
                DropIntoWorld(mouseRay);
            }
            CancelDrag();
        }

        void DropIntoWorld(Ray ray)
        {
            const int mask = ~(1 << LevelGenerator.CeilingLayer);
            if (Physics.Raycast(ray, out var hit, PlayerInventory.ReachDistance, mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<Hatch>() != null)
                {
                    Inventory.DepositRpc(m_DragIndex);
                    return;
                }
                var point = hit.point;
                if (hit.normal.y < 0.5f &&
                    Physics.Raycast(hit.point + hit.normal * 0.3f + Vector3.up * 0.2f, Vector3.down, out var floor, 3f, mask, QueryTriggerInteraction.Ignore))
                    point = floor.point;
                Inventory.DropRpc(m_DragIndex, point + Vector3.up * 0.01f, Player.transform.eulerAngles.y);
                return;
            }
            // Nothing in reach: drop at the feet.
            if (Hatch.IsNear(Player.transform.position) && Vector3.Dot(ray.direction, Vector3.down) > 0.5f)
            {
                Inventory.DepositRpc(m_DragIndex);
                return;
            }
            Inventory.DropRpc(m_DragIndex, Player.transform.position + Player.transform.forward * 0.8f, Player.transform.eulerAngles.y);
        }

        void CancelDrag()
        {
            m_Source = DragSource.None;
            m_DragIndex = -1;
            m_DragWorld = null;
        }

        WorldItem RaycastWorldItem(Ray ray)
        {
            var hits = Physics.RaycastAll(ray, PlayerInventory.ReachDistance, ~(1 << LevelGenerator.CeilingLayer), QueryTriggerInteraction.Collide);
            WorldItem best = null;
            float bestDist = float.MaxValue;
            float wallDist = float.MaxValue;
            foreach (var h in hits)
            {
                var item = h.collider.GetComponentInParent<WorldItem>();
                if (item != null)
                {
                    if (h.distance < bestDist) { best = item; bestDist = h.distance; }
                }
                else if (!h.collider.isTrigger && h.collider.GetComponentInParent<PlayerController>() != Player)
                {
                    wallDist = Mathf.Min(wallDist, h.distance);
                }
            }
            return best != null && bestDist <= wallDist + 0.05f ? best : null;
        }

        // ------------------------------------------------------------------ drawing


        void OnGUI()
        {
            if (!IsOwner || Inventory == null) return;
            UiDraw.Begin();
            if (Event.current.type == EventType.Repaint)
                m_Expand = Mathf.MoveTowards(m_Expand, Player.CursorMode ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            // Compact while walking around: small and quiet, it only shows what you carry.
            float c = Mathf.Round(Mathf.Lerp(CellPx * 0.55f, CellPx, m_Expand));
            var grid = GridRectFor(c);
            float panelAlpha = Mathf.Lerp(0.3f, 0.6f, m_Expand);
            float headerH = m_Expand > 0.5f || Inventory.Items.Count > 0 ? 22f : 0f;
            UiDraw.Rect(new Rect(grid.x - 5, grid.y - headerH - 4, grid.width + 10, grid.height + headerH + 9), new Color(0, 0, 0, panelAlpha));
            if (headerH > 0f)
            {
                UiDraw.Label(new Rect(grid.x, grid.y - headerH - 2, grid.width, 20), $"{Inventory.TotalValue} очк.", 13, TextAnchor.MiddleLeft, Color.white);
                if (m_Expand > 0.5f)
                    UiDraw.Label(new Rect(grid.x, grid.y - headerH - 2, grid.width, 20), "R — повернуть", 12, TextAnchor.MiddleRight, new Color(1, 1, 1, 0.6f));
            }

            var cellBg = new Color(1f, 1f, 1f, Mathf.Lerp(0.05f, 0.09f, m_Expand));
            for (int y = 0; y < InventoryLayout.Height; y++)
                for (int x = 0; x < InventoryLayout.Width; x++)
                    if (InventoryLayout.IsCell(x, y))
                        UiDraw.Rect(new Rect(grid.x + x * c + 1, grid.y + y * c + 1, c - 2, c - 2), cellBg);

            for (int i = 0; i < Inventory.Items.Count; i++)
            {
                if (m_Source == DragSource.Inventory && i == m_DragIndex) continue;
                var e = Inventory.Items[i];
                DrawShape(new Vector2(grid.x + e.X * c, grid.y + e.Y * c), e.Shape, e.Color, e.Rot, c, 1f);
            }

            if (m_Source != DragSource.None)
            {
                bool overGrid = MouseCell(out var cell);
                var target = cell - m_GrabCell;
                if (overGrid)
                {
                    bool ok = m_Source == DragSource.World
                        ? Inventory.CanPlace(m_DragShape, m_DragRot, target.x, target.y)
                        : Inventory.CanPlace(m_DragShape, m_DragRot, target.x, target.y, m_DragIndex);
                    foreach (var cc in ItemCatalog.RotatedCells(m_DragShape, m_DragRot))
                        UiDraw.Rect(new Rect(grid.x + (target.x + cc.x) * c + 1, grid.y + (target.y + cc.y) * c + 1, c - 2, c - 2),
                            ok ? new Color(0.3f, 1f, 0.4f, 0.35f) : new Color(1f, 0.25f, 0.2f, 0.4f));
                }
                var m = MouseGui - new Vector2(m_GrabCell.x * c + c / 2f, m_GrabCell.y * c + c / 2f);
                DrawShape(m, m_DragShape, m_DragColor, m_DragRot, c, 0.75f);
                if (!overGrid && m_Source == DragSource.Inventory)
                    UiDraw.Label(new Rect(MouseGui.x + 14, MouseGui.y + 10, 260, 22),
                        Hatch.IsNear(Player.transform.position) ? "Отпусти на люке — сдать на квоту" : "Отпусти — положить",
                        13, TextAnchor.MiddleLeft, Color.white);
            }

            if (!Player.CursorMode)
            {
                float cx = UiDraw.W / 2f, cy = UiDraw.H / 2f;
                if (m_Hover == null) UiDraw.Rect(new Rect(cx - 2, cy - 2, 4, 4), new Color(1, 1, 1, 0.8f));
                else
                {
                    // Corner brackets: "this can be taken".
                    var bc = new Color(1f, 1f, 1f, 0.95f);
                    const float r = 11f, l = 6f, t = 2f;
                    UiDraw.Rect(new Rect(cx - r, cy - r, l, t), bc); UiDraw.Rect(new Rect(cx - r, cy - r, t, l), bc);
                    UiDraw.Rect(new Rect(cx + r - l, cy - r, l, t), bc); UiDraw.Rect(new Rect(cx + r - t, cy - r, t, l), bc);
                    UiDraw.Rect(new Rect(cx - r, cy + r - t, l, t), bc); UiDraw.Rect(new Rect(cx - r, cy + r - l, t, l), bc);
                    UiDraw.Rect(new Rect(cx + r - l, cy + r - t, l, t), bc); UiDraw.Rect(new Rect(cx + r - t, cy + r - l, t, l), bc);
                }
            }
            else if (m_Tooltip != null && m_Source == DragSource.None)
            {
                UiDraw.Label(new Rect(MouseGui.x + 16, MouseGui.y + 8, 300, 22), m_Tooltip, 13, TextAnchor.MiddleLeft, Color.white);
            }
        }

        static void DrawShape(Vector2 origin, int shape, int color, int rot, float c, float alpha)
        {
            var col = ItemCatalog.Colors[color].Color;
            var dark = col * 0.55f; dark.a = alpha;
            col.a = alpha;
            foreach (var cell in ItemCatalog.RotatedCells(shape, rot))
            {
                var r = new Rect(origin.x + cell.x * c + 2, origin.y + cell.y * c + 2, c - 4, c - 4);
                UiDraw.Rect(r, dark);
                UiDraw.Rect(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), col);
            }
        }
    }
}
