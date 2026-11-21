using Stealth.Audio;
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
        /// <summary>Aim assist: an item this close to the crosshair ray (meters) counts as aimed at.</summary>
        const float AimAssist = 0.12f;

        [Header("Layout")]
        public RectTransform Canvas;
        public RectTransform Panel;
        public Image GridBackground;
        public GameObject Header;
        public Image HeaderBackground;
        public Text ValueLabel;
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

        [Header("Belt bag")]
        [Tooltip("The belt bag's 4 x 4 grid left of the backpack (shown while one is worn): its cells' columns start at BeltUi in the grid's cells.")]
        public GameObject BeltPanel;
        public RectTransform BeltGrid;
        public Image[] BeltCells;
        /// <summary>The belt's first column in the backpack grid's cell space (left of it, a cell apart).</summary>
        public const int BeltUi = -(PlayerInventory.BeltSize + 1);

        /// <summary>Where an inventory column is drawn in the grid's cells: the belt's columns left of the backpack.</summary>
        static int UiColumn(int x) => x >= PlayerInventory.BeltX0 && x < PlayerInventory.BeltX0 + PlayerInventory.BeltSize ? x - PlayerInventory.BeltX0 + BeltUi : x;

        [Header("Hand slot")]
        [Tooltip("The hand slot next to the backpack: one item held (the flashlight, batteries), used with the hand key.")]
        public RectTransform HandSlot;
        public Image HandBackground;
        [Tooltip("Where the held item is drawn (top-left pivot, cells like the grid).")]
        public RectTransform HandItems;
        public Text HandLabel;

        enum DragSource { None, Inventory, World, Stash, Cargo }
        enum Target { None, Backpack, Stash, Sell, StashAll, Hand, Belt }

        DragSource m_Source;
        int m_DragIndex = -1;
        WorldItem m_DragWorld;
        InventoryEntry m_DragStashed;
        int m_DragShape, m_DragColor, m_DragRot;
        float m_DragTravel;
        Vector2Int m_GrabCell;
        WorldItem m_Hover, m_Highlighted;
        Vehicles.CargoPiece m_HoverCargo;
        Vehicles.CargoItem m_DragCargo;
        float m_FullUntil = -1f;
        string m_Tooltip;
        float m_Expand; // 0 = compact (just carrying), 1 = hands mode (Tab / dragging)
        UiPool<InventoryBlockView> m_Items, m_GhostBlocks, m_StashItems;
        UiPool<Image> m_Highlights, m_StashHighlights;
        UiPool<Image> m_Icons, m_GhostIcons, m_StashIcons; // pictures of real items over their cells (ItemVisuals.Icons)
        UiPool<InventoryBlockView> m_HandBlocks;
        UiPool<Image> m_HandIcons;
        float m_FullChargeUntil = -1f;

        /// <summary>World item under the crosshair / cursor (in reach), null if none.</summary>
        public WorldItem Hover => m_Hover;

        static PlayerController Me => PlayerController.Local;
        static StashBoard Board => StashBoard.Instance;
        /// <summary>The stash of the hatch the local player is at (the entry's or the exit's), null elsewhere.</summary>
        static TeamStash Stash => GameManager.Instance != null && PlayerController.Local != null
            ? GameManager.Instance.StashAt(PlayerController.Local.transform.position) : null;

        void Awake()
        {
            Instance = this;
            m_Items = new UiPool<InventoryBlockView>(BlockPrefab, ItemLayer);
            m_GhostBlocks = new UiPool<InventoryBlockView>(BlockPrefab, Ghost);
            m_Highlights = new UiPool<Image>(HighlightPrefab, HighlightLayer);
            m_Icons = new UiPool<Image>(HighlightPrefab, ItemLayer);
            m_GhostIcons = new UiPool<Image>(HighlightPrefab, Ghost);
            if (HandItems != null)
            {
                m_HandBlocks = new UiPool<InventoryBlockView>(BlockPrefab, HandItems);
                m_HandIcons = new UiPool<Image>(HighlightPrefab, HandItems);
            }
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
            return Board != null && gm != null && !DevFlags.HideStashBoard && gm.CanTradeAt(me.transform.position);
        }

        /// <summary>What the cursor is over: the backpack grid, the stash grid, the sell area or nothing.</summary>
        Target MouseTarget(PlayerController me, out Vector2Int cell)
        {
            if (MouseCell(out cell)) return Target.Backpack;
            if (BeltPanel != null && BeltPanel.activeSelf && CellIn(BeltGrid, null, MouseScreen, out cell))
            {
                cell = new Vector2Int(cell.x + PlayerInventory.BeltX0, cell.y); // the inventory's columns
                return Target.Belt;
            }
            if (HandSlot != null && RectTransformUtility.RectangleContainsScreenPoint(HandSlot, MouseScreen, null)) return Target.Hand;
            if (!StashActive(me) || !Board.Visible) return Target.None;
            if (CellIn(Board.Grid, me.Camera, MouseScreen, out cell)) return Target.Stash;
            if (Board.SellZone.gameObject.activeInHierarchy && RectTransformUtility.ScreenPointToLocalPointInRectangle(Board.SellZone, MouseScreen, me.Camera, out var local) &&
                Board.SellZone.rect.Contains(local)) return Target.Sell;
            if (Board.StashAllZone != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(Board.StashAllZone, MouseScreen, me.Camera, out var all) &&
                Board.StashAllZone.rect.Contains(all)) return Target.StashAll;
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
                m_Hover = null; m_HoverCargo = null;
                HideDynamic();
                if (Board != null) Board.Show(false);
                return;
            }
            if (me.Watching != null && me.Watching.TryGetComponent<PlayerInventory>(out var watched))
            {
                // knocked out, watching a teammate: their backpack, to look at only
                CancelDrag();
                m_Hover = null; m_HoverCargo = null;
                if (Board != null) Board.Show(false);
                Draw(me, watched);
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
            // the entry hatch is the tank's filler: its board has a zone that burns what is dropped on it; the exit's has none
            bool burn = GameManager.Instance.CanBurnStashAt(me.transform.position, Stash); // (the storage robot's crate: no fire)
            if (Board.SellZone != null && Board.SellZone.gameObject.activeSelf != burn) Board.SellZone.gameObject.SetActive(burn);
            // Turns towards the viewer while the camera moves freely; stays still under the cursor.
            if (!me.CursorMode || !was)
            {
                if (me.InHatch) Board.PlaceInFront(me.Camera.transform);
                else if (GameManager.Instance.TradePosition is Vector3 at) Board.Place(at, me.Camera.transform.position);
            }
            if (m_StashItems == null)
            {
                m_StashItems = new UiPool<InventoryBlockView>(BlockPrefab, Board.ItemLayer);
                m_StashHighlights = new UiPool<Image>(HighlightPrefab, Board.HighlightLayer);
                m_StashIcons = new UiPool<Image>(HighlightPrefab, Board.ItemLayer);
            }
        }

        void HandleInput(PlayerController me, PlayerInventory inv)
        {
            var mouse = Mouse.current;
            if (me.Downed || GameMenu.IsOpen || me.UsingTerminal != null) { CancelDrag(); m_Hover = null; m_HoverCargo = null; return; } // at a terminal the camera is far away
            if (Controls.Pressed(GameAction.StashAll)) StashAll(me, inv);

            if (!me.CursorMode && !PlayerController.HoldCursor)
            {
                CancelDrag();
                var look = new Ray(me.Camera.transform.position, me.Camera.transform.forward);
                m_Hover = RaycastWorldItem(me, look);
                m_HoverCargo = m_Hover == null ? RaycastCargo(me, look) : null;
                // Grab it: the camera freezes and the cursor appears until the button is released.
                if (m_Hover != null && mouse.leftButton.wasPressedThisFrame)
                {
                    BeginWorldDrag(m_Hover);
                    PlayerController.HoldCursor = true;
                }
                else if (m_HoverCargo != null && mouse.leftButton.wasPressedThisFrame)
                {
                    BeginCargoDrag(m_HoverCargo);
                    PlayerController.HoldCursor = true;
                }
                return;
            }

            var mouseRay = me.Camera.ScreenPointToRay(MouseScreen);
            var over = MouseTarget(me, out var cell);
            m_Hover = over != Target.None ? null : RaycastWorldItem(me, mouseRay);
            m_HoverCargo = over != Target.None || m_Hover != null ? null : RaycastCargo(me, mouseRay);

            if (m_Source == DragSource.None)
            {
                BeginDrag(over, cell, inv);
                return;
            }

            if (m_Source == DragSource.World && (m_DragWorld == null || !m_DragWorld.isActiveAndEnabled)) { CancelDrag(); return; }
            if (m_Source == DragSource.Stash && (Stash == null || Stash.IndexOf(m_DragStashed) < 0)) { CancelDrag(); return; }
            if (m_Source == DragSource.Cargo && !InBed(m_DragCargo)) { CancelDrag(); return; }

            m_DragTravel += mouse.delta.ReadValue().magnitude;
            if (Controls.Pressed(GameAction.Rotate))
            {
                m_DragRot = ItemCatalog.Turned(m_DragRot);
                m_GrabCell = Vector2Int.zero;
            }
            if (Controls.Pressed(GameAction.Mirror))
            {
                m_DragRot = ItemCatalog.Mirrored(m_DragRot);
                m_GrabCell = Vector2Int.zero;
            }
            if (!mouse.leftButton.isPressed) // released (or the press was lost)
            {
                Drop(me, inv, over, cell - m_GrabCell, mouseRay, cell);
                CancelDrag();
            }
        }

        void BeginDrag(Target over, Vector2Int cell, PlayerInventory inv)
        {
            bool press = Mouse.current.leftButton.wasPressedThisFrame;
            if (over is Target.Backpack or Target.Belt)
            {
                int idx = inv.IndexAt(cell);
                // an empty belt: the bag itself is taken off (into the backpack, the stash, the hopper's fire)
                if (idx < 0 && over == Target.Belt && inv.BeltCount() == 0) idx = inv.WornIndex();
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
                m_Tooltip = ItemCatalog.Describe(e.Shape, e.Color);
                if (!press) return;
                m_Source = DragSource.Stash;
                m_DragStashed = e;
                StartShape(e, cell);
            }
            else if (over == Target.Hand)
            {
                int idx = inv.HandIndex();
                if (idx < 0) return;
                var e = inv.Items[idx];
                m_Tooltip = ItemCatalog.Describe(e.Shape, e.Color);
                if (!press) return;
                m_Source = DragSource.Inventory;
                m_DragIndex = idx;
                m_DragShape = e.Shape; m_DragColor = e.Color; m_DragRot = 0;
                m_GrabCell = Vector2Int.zero;
                m_DragTravel = 0f;
            }
            else if (over == Target.Sell)
            {
                m_Tooltip = null;
            }
            else if (over == Target.StashAll)
            {
                m_Tooltip = null;
                if (press) StashAll(PlayerController.Local, inv);
            }
            else if (m_Hover != null)
            {
                m_Tooltip = ItemCatalog.Describe(m_Hover.Shape, m_Hover.Color);
                if (press) BeginWorldDrag(m_Hover);
            }
            else if (m_HoverCargo != null)
            {
                m_Tooltip = ItemCatalog.Describe(m_HoverCargo.Item.Shape, m_HoverCargo.Item.Color);
                if (press) BeginCargoDrag(m_HoverCargo);
            }
        }

        void StartShape(InventoryEntry e, Vector2Int cell)
        {
            m_DragShape = e.Shape; m_DragColor = e.Color; m_DragRot = e.Rot;
            m_GrabCell = PlayerInventory.Worn(e) ? Vector2Int.zero : cell - new Vector2Int(e.X, e.Y);
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

        void BeginCargoDrag(Vehicles.CargoPiece piece)
        {
            m_Source = DragSource.Cargo;
            m_DragCargo = piece.Item;
            m_DragShape = piece.Item.Shape; m_DragColor = piece.Item.Color; m_DragRot = 0;
            m_GrabCell = Vector2Int.zero;
            m_DragTravel = 0f;
        }

        static bool InBed(Vehicles.CargoItem item)
        {
            var gm = GameManager.Instance;
            if (gm == null) return false;
            foreach (var c in gm.Cargo.Items) if (c.Equals(item)) return true;
            return false;
        }

        /// <summary>The thing in the car's bed nearest to the ray (within its aim radius and the hands' reach), or null.</summary>
        static Vehicles.CargoPiece RaycastCargo(PlayerController me, Ray ray)
        {
            var car = Vehicles.Car.Active;
            if (car == null || (car.transform.position - me.transform.position).magnitude > PlayerInventory.CarReach) return null;
            Vehicles.CargoPiece best = null;
            float bestMiss = float.MaxValue;
            foreach (var piece in car.Pieces)
            {
                if (piece == null || !piece.gameObject.activeInHierarchy) continue;
                var v = piece.Point - ray.origin;
                float along = Vector3.Dot(v, ray.direction);
                if (along <= 0f || along > piece.Reach + 0.8f) continue;
                float miss = (v - ray.direction * along).magnitude;
                if (miss < piece.AimRadius && miss < bestMiss) { best = piece; bestMiss = miss; }
            }
            return best;
        }

        /// <summary>Whether the ray points at the car's hopper (a thing let go there burns into fuel).</summary>
        static bool AtHopper(PlayerController me, Ray ray)
        {
            var hopper = Vehicles.Car.Active != null ? Vehicles.Car.Active.Hopper : null;
            if (hopper == null || (hopper.Point - me.transform.position).magnitude > hopper.Reach + 0.8f) return false;
            var v = hopper.Point - ray.origin;
            float along = Vector3.Dot(v, ray.direction);
            return along > 0f && (v - ray.direction * along).magnitude < hopper.AimRadius + 0.25f;
        }

        void Drop(PlayerController me, PlayerInventory inv, Target over, Vector2Int target, Ray mouseRay, Vector2Int cell)
        {
            byte x = (byte)Mathf.Clamp(target.x, 0, 254), y = (byte)Mathf.Clamp(target.y, 0, 254), rot = (byte)m_DragRot;
            var bank = Sfx.Bank;
            bool inside = target.x >= 0 && target.y >= 0;
            // batteries dropped onto a flashlight (in the backpack or in the hand) recharge it
            if (m_DragShape == ItemCatalog.Battery && (over == Target.Backpack || over == Target.Hand) && m_Source != DragSource.Stash)
            {
                int flash = over == Target.Hand ? inv.HandIndex() : inv.IndexAt(cell);
                if (flash >= 0 && flash != m_DragIndex && inv.Items[flash].Shape == ItemCatalog.Flashlight)
                {
                    if (!inv.CanRecharge(flash)) { m_FullChargeUntil = Time.time + 1.5f; return; } // already full: the battery stays
                    if (m_Source == DragSource.Inventory) inv.RequestRecharge(m_DragIndex, flash);
                    else if (m_Source == DragSource.World) { inv.RequestRechargeWorld(m_DragWorld, flash); m_DragWorld.HideWhileTaking(Time.time); }
                    Sfx.Ui(bank?.Click, 0.7f);
                    return;
                }
            }
            switch (over)
            {
                case Target.Hand:
                    if (!ItemCatalog.Holdable(m_DragShape)) return;
                    if (m_Source == DragSource.World && inv.CanPlace(m_DragShape, 0, PlayerInventory.HandX, 0)) Take(inv, PlayerInventory.HandX, 0, 0);
                    else if (m_Source == DragSource.Inventory && inv.CanPlace(m_DragShape, 0, PlayerInventory.HandX, 0, m_DragIndex)) { inv.RequestMove(m_DragIndex, PlayerInventory.HandX, 0, 0); Sfx.Ui(bank?.Click, 0.5f); }
                    else if (m_Source == DragSource.Stash && inv.CanPlace(m_DragShape, 0, PlayerInventory.HandX, 0)) { inv.RequestUnstash(m_DragStashed, PlayerInventory.HandX, 0, 0); Sfx.Ui(bank?.Pickup, 0.8f, Sfx.Group.Items); }
                    return;
                case Target.Backpack:
                case Target.Belt:
                    if (!inside) return;
                    if (m_Source == DragSource.World && inv.CanPlace(m_DragShape, rot, x, y)) Take(inv, x, y, rot);
                    else if (m_Source == DragSource.Inventory && inv.CanPlace(m_DragShape, rot, x, y, m_DragIndex)) { inv.RequestMove(m_DragIndex, x, y, rot); Sfx.Ui(bank?.Click, 0.5f); }
                    else if (m_Source == DragSource.Stash && inv.CanPlace(m_DragShape, rot, x, y)) { inv.RequestUnstash(m_DragStashed, x, y, rot); Sfx.Ui(bank?.Pickup, 0.8f, Sfx.Group.Items); }
                    else if (m_Source == DragSource.Cargo && inv.CanPlace(m_DragShape, rot, x, y)) { inv.RequestTakeCargo(m_DragCargo, x, y, rot); Sfx.Ui(bank?.Pickup, 0.8f, Sfx.Group.Items); }
                    return;
                case Target.Stash:
                    if (!inside || Stash == null) return;
                    bool placed = true;
                    if (m_Source == DragSource.Inventory && Stash.CanPlace(m_DragShape, rot, x, y)) inv.RequestStash(m_DragIndex, x, y, rot);
                    else if (m_Source == DragSource.World && Stash.CanPlace(m_DragShape, rot, x, y)) { inv.RequestStashWorld(m_DragWorld, x, y, rot); m_DragWorld.HideWhileTaking(Time.time); }
                    else if (m_Source == DragSource.Stash && Stash.CanPlace(m_DragShape, rot, x, y, Stash.IndexOf(m_DragStashed))) inv.RequestStashMove(m_DragStashed, x, y, rot);
                    else placed = false;
                    if (placed) Sfx.At(bank?.Stash, Board.Root.position, 0.9f, Sfx.Group.Items);
                    return;
                case Target.Sell:
                    if (m_Source == DragSource.Inventory) inv.RequestDeposit(m_DragIndex);
                    else if (m_Source == DragSource.World) { inv.RequestSellWorld(m_DragWorld); m_DragWorld.HideWhileTaking(Time.time); }
                    else if (m_Source == DragSource.Stash) inv.RequestSellStashed(m_DragStashed);
                    return;
            }
            if (m_Source == DragSource.Inventory && AtHopper(me, mouseRay))
            {
                // into the shredder: this one thing burns into fuel
                inv.RequestDeposit(m_DragIndex);
                Sfx.At(bank?.Stash, Vehicles.Car.Active.Hopper.Point, 0.9f, Sfx.Group.Items);
            }
            else if (m_Source == DragSource.Inventory) DropIntoWorld(me, inv, mouseRay);
            // a thing from the bed let go anywhere but a cell: into the first free spot
            else if (m_Source == DragSource.Cargo)
            {
                if (inv.FindSpot(m_DragShape, out var cx, out var cy, out var cr)) { inv.RequestTakeCargo(m_DragCargo, cx, cy, cr); Sfx.Ui(bank?.Pickup, 0.8f, Sfx.Group.Items); }
                else m_FullUntil = Time.time + 1.5f;
            }
            // from the team stash straight onto the floor, the same way as from the backpack
            else if (m_Source == DragSource.Stash) DropIntoWorld(me, inv, mouseRay, stashed: true);
            // A world item released anywhere but a grid (a click, or a drag that missed): into the first free spot.
            else if (m_Source == DragSource.World)
            {
                if (inv.FindSpot(m_DragShape, out var fx, out var fy, out var fr)) Take(inv, fx, fy, fr);
                else m_FullUntil = Time.time + 1.5f;
            }
        }

        /// <summary>The whole backpack onto the team stash (the server puts in what fits).</summary>
        void StashAll(PlayerController me, PlayerInventory inv)
        {
            if (inv.Items.Count == 0 || !StashActive(me)) return;
            inv.RequestStashAll();
            Sfx.At(Sfx.Bank?.Stash, Board.Root.position, 0.9f, Sfx.Group.Items);
        }

        /// <summary>Pick the dragged world item up into the backpack; it vanishes at once (the server confirms).</summary>
        void Take(PlayerInventory inv, byte x, byte y, byte rot)
        {
            inv.RequestPickup(m_DragWorld, x, y, rot);
            m_DragWorld.HideWhileTaking(Time.time);
            Sfx.Ui(Sfx.Bank?.Pickup, 0.8f, Sfx.Group.Items);
        }

        void DropIntoWorld(PlayerController me, PlayerInventory inv, Ray ray, bool stashed = false)
        {
            const int mask = ~(1 << LevelMetrics.CeilingLayer);
            float yaw = me.transform.eulerAngles.y;
            var at = me.transform.position + me.transform.forward * 0.8f;
            if (Physics.Raycast(ray, out var hit, PlayerInventory.ReachDistance, mask, QueryTriggerInteraction.Ignore))
            {
                if (!stashed && hit.collider.GetComponentInParent<ShaftHatch>() is { Exit: true })
                {
                    inv.RequestDeposit(m_DragIndex);
                    return;
                }
                at = hit.point;
                if (hit.normal.y < 0.5f &&
                    Physics.Raycast(hit.point + hit.normal * 0.3f + Vector3.up * 0.2f, Vector3.down, out var floor, 3f, mask, QueryTriggerInteraction.Ignore))
                    at = floor.point;
                at += Vector3.up * 0.01f;
                Sfx.At(Sfx.Bank?.Drop, at, 0.8f, Sfx.Group.Items);
            }
            if (stashed) inv.RequestDropStashed(m_DragStashed, at, yaw);
            else inv.RequestDrop(m_DragIndex, at, yaw);
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
            if (best != null && bestDist <= wallDist + 0.05f) return best;

            // A near miss: the item closest to the ray within a few centimetres (small things among the clutter on desks).
            const int mask = ~(1 << LevelMetrics.CeilingLayer);
            WorldItem near = null;
            float nearMiss = AimAssist;
            foreach (var item in WorldItem.All)
            {
                if (item.Taking || item.Collider == null) continue;
                var bounds = item.Collider.bounds;
                var v = bounds.center - ray.origin;
                float along = Vector3.Dot(v, ray.direction);
                if (along <= 0f || along > PlayerInventory.ReachDistance || along > wallDist + 0.3f) continue;
                float miss = (v - ray.direction * along).magnitude - bounds.extents.magnitude * 0.5f;
                if (miss >= nearMiss) continue;
                if (Physics.Linecast(ray.origin, bounds.center, out var block, mask, QueryTriggerInteraction.Ignore) &&
                    block.collider.GetComponentInParent<WorldItem>() != item && block.collider.GetComponentInParent<PlayerController>() != me)
                    continue;
                near = item;
                nearMiss = miss;
            }
            return near;
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
            Ui.Alpha(GridBackground, Mathf.Lerp(0.7f, 1f, m_Expand)); // the backpack itself: solid when open
            Ui.Alpha(HeaderBackground, alpha);
            bool header = m_Expand > 0.5f || inv.Items.Count > 0;
            Ui.Show(Header, header);
            if (header) SetIfChanged(ValueLabel, Loc.F("common.points", inv.TotalValue));
            // the grid shows only while something is dragged: otherwise it is just the backpack with its things in it
            float cellAlpha = m_Source != DragSource.None ? 0.16f : 0f;
            foreach (var c in Cells) Ui.Alpha(c, cellAlpha);

            m_Items.Begin();
            m_Icons.Begin();
            m_HandBlocks?.Begin();
            m_HandIcons?.Begin();
            for (int i = 0; i < inv.Items.Count; i++)
            {
                if (m_Source == DragSource.Inventory && i == m_DragIndex) continue;
                var e = inv.Items[i];
                if (PlayerInventory.Worn(e)) continue; // the bag itself: its belt's grid shows it
                if (PlayerInventory.InHand(e))
                {
                    // the hand: the item upright in the slot, centred
                    if (m_HandBlocks == null) continue;
                    var size = ItemCatalog.Size(ItemCatalog.Shapes[e.Shape].Cells);
                    HandItems.anchoredPosition = new Vector2(-size.x * Cell / 2f, size.y * Cell / 2f);
                    AddShape(m_HandBlocks, m_HandIcons, Vector2Int.zero, e.Shape, e.Color, 0, 1f);
                    continue;
                }
                AddShape(m_Items, m_Icons, new Vector2Int(UiColumn(e.X), e.Y), e.Shape, e.Color, e.Rot, 1f);
            }
            m_Items.End();
            if (BeltPanel != null)
            {
                bool belt = inv.HasBelt;
                if (BeltPanel.activeSelf != belt) BeltPanel.SetActive(belt);
                if (belt) foreach (var c in BeltCells) Ui.Alpha(c, m_Source != DragSource.None ? 0.16f : 0.06f);
            }
            m_Icons.End();
            m_HandBlocks?.End();
            m_HandIcons?.End();
            if (HandSlot != null)
            {
                Ui.Alpha(HandBackground, Mathf.Lerp(0.55f, 0.85f, m_Expand));
                SetIfChanged(HandLabel, $"{Loc.T("inventory.hand")} [{Controls.Label(GameAction.UseHand)}]");
            }

            bool board = Board != null && Board.Visible && m_StashItems != null;
            if (board) DrawStashItems();

            m_Highlights.Begin();
            m_GhostBlocks.Begin();
            m_GhostIcons.Begin();
            if (board) m_StashHighlights.Begin();
            bool dragging = m_Source != DragSource.None;
            string hint = null;
            bool overSell = false;
            if (dragging)
            {
                var over = MouseTarget(me, out var cell);
                var target = cell - m_GrabCell;
                if (over is Target.Backpack or Target.Belt)
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
                }
                else if (over == Target.Sell)
                {
                    overSell = true;
                    hint = Loc.F("inventory.sell_value", ItemCatalog.Value(m_DragShape, m_DragColor));
                }
                else if (m_Source == DragSource.Inventory && AtHopper(me, me.Camera.ScreenPointToRay(MouseScreen)))
                    hint = Loc.F("use.hopper", ItemCatalog.Value(m_DragShape, m_DragColor));
                Ghost.anchoredPosition = MouseCanvas - new Vector2(m_GrabCell.x * Cell + Cell / 2f, -(m_GrabCell.y * Cell + Cell / 2f));
                AddShape(m_GhostBlocks, m_GhostIcons, Vector2Int.zero, m_DragShape, m_DragColor, m_DragRot, 0.75f);
            }
            m_Highlights.End();
            m_GhostBlocks.End();
            m_GhostIcons.End();
            if (board)
            {
                m_StashHighlights.End();
                Board.SellBackground.color = overSell ? new Color(1f, 0.75f, 0.1f, 0.85f) : new Color(1f, 0.75f, 0.1f, 0.45f);
                if (Board.StashAllZone != null)
                {
                    bool overAll = !dragging && me.CursorMode && MouseTarget(me, out _) == Target.StashAll;
                    bool any = inv.Items.Count > 0;
                    Board.StashAllBackground.color = !any ? new Color(0.3f, 0.75f, 1f, 0.15f) : overAll ? new Color(0.3f, 0.75f, 1f, 0.85f) : new Color(0.3f, 0.75f, 1f, 0.45f);
                    SetIfChanged(Board.StashAllLabel, Loc.F("stash.all", Controls.Label(GameAction.StashAll)));
                }
            }

            string tip = hint ?? (me.CursorMode && !dragging ? m_Tooltip : null);
            bool full = Time.time < m_FullUntil;
            if (full && tip == null) tip = Loc.T("inventory.full");
            if (tip == null && Time.time < m_FullChargeUntil) tip = Loc.T("inventory.full_charge");
            Ui.Show(Tooltip, tip != null);
            if (tip == null) return;
            Tooltip.text = tip;
            // Without a cursor the note sits under the crosshair.
            var at = me.CursorMode ? MouseCanvas : Canvas.rect.size / 2f;
            Tooltip.rectTransform.anchoredPosition = at + new Vector2(16f, -8f);
        }

        void DrawStashItems()
        {
            var stash = Stash;
            m_StashItems.Begin();
            m_StashIcons.Begin();
            if (stash != null)
                for (int i = 0; i < stash.Items.Count; i++)
                {
                    var e = stash.Items[i];
                    if (m_Source == DragSource.Stash && e.Equals(m_DragStashed)) continue;
                    AddShape(m_StashItems, m_StashIcons, new Vector2Int(e.X, e.Y), e.Shape, e.Color, e.Rot, 1f);
                }
            m_StashItems.End();
            m_StashIcons.End();
            SetIfChanged(Board.ValueLabel, Loc.F("stash.title", stash != null ? stash.TotalValue : 0));
        }

        static void SetIfChanged(Text t, string text)
        {
            if (t.text != text) t.text = text;
        }

        /// <summary>Marks the cells the dragged item would take at <paramref name="target"/>: green if it fits.</summary>
        void Highlight(UiPool<Image> pool, Vector2Int target, bool ok)
        {
            foreach (var cc in ItemCatalog.RotatedCells(m_DragShape, m_DragRot))
            {
                var h = pool.Next();
                h.rectTransform.anchoredPosition = new Vector2((UiColumn(target.x) + cc.x) * Cell + 1, -((target.y + cc.y) * Cell + 1));
                h.color = ok ? new Color(0.3f, 1f, 0.4f, 0.35f) : new Color(1f, 0.25f, 0.2f, 0.4f);
            }
        }

        static void AddShape(UiPool<InventoryBlockView> pool, UiPool<Image> icons, Vector2Int origin, int shape, int color, int rot, float alpha)
        {
            var c = ItemCatalog.UiColor(shape, color);
            foreach (var cell in ItemCatalog.RotatedCells(shape, rot))
                pool.Next().Set(new Vector2((origin.x + cell.x) * Cell + 2, -((origin.y + cell.y) * Cell + 2)), Cell - 4, c, alpha);
            var sprite = IconOf(shape);
            if (sprite == null) return;
            // the picture over the cells (made for the base turn), turned and mirrored with the item
            var size = ItemCatalog.Size(ItemCatalog.Shapes[shape].Cells);
            var turned = ItemCatalog.Size(ItemCatalog.RotatedCells(shape, rot));
            var img = icons.Next();
            var rt = img.rectTransform;
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, alpha);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size.x * Cell - 2, size.y * Cell - 2);
            rt.anchoredPosition = new Vector2((origin.x + turned.x / 2f) * Cell, -(origin.y + turned.y / 2f) * Cell);
            rt.localScale = new Vector3(ItemCatalog.IsMirrored(rot) ? -1f : 1f, 1f, 1f);
            rt.localRotation = Quaternion.Euler(0f, 0f, -90f * (rot & 3)); // a quarter turn clockwise per step (RotatedCells)
            rt.SetAsLastSibling();                                         // over the cells
            if (shape != ItemCatalog.Flashlight) return;
            // the flashlight's charge: a thin bar along the bottom of its cells, green when full, red when nearly empty
            float k = Mathf.Clamp01(color / (float)ItemCatalog.FullCharge);
            var bar = icons.Next();
            var b = bar.rectTransform;
            bar.sprite = null;
            bar.raycastTarget = false;
            bar.color = new Color(Mathf.Lerp(1f, 0.3f, k), Mathf.Lerp(0.25f, 0.95f, k), 0.2f, alpha);
            b.pivot = new Vector2(0f, 0f);
            b.localScale = Vector3.one;
            b.localRotation = Quaternion.identity;
            float w = turned.x * Cell - 8f, h = turned.y * Cell;
            b.sizeDelta = new Vector2(Mathf.Max(2f, w * k), 4f);
            b.anchoredPosition = new Vector2(origin.x * Cell + 4f, -(origin.y * Cell + h) + 4f);
            b.SetAsLastSibling();
        }

        static ItemVisuals s_Visuals;

        /// <summary>The backpack picture of a shape (the item prefab's visuals), null if it has none.</summary>
        static Sprite IconOf(int shape)
        {
            if (s_Visuals == null && GameManager.Instance != null && GameManager.Instance.ItemPrefab != null)
                s_Visuals = GameManager.Instance.ItemPrefab.GetComponent<WorldItem>().Visuals;
            var icons = s_Visuals != null ? s_Visuals.Icons : null;
            return icons != null && shape < icons.Length ? icons[shape] : null;
        }

        void HideDynamic()
        {
            m_Items.Begin(); m_Items.End();
            m_Icons.Begin(); m_Icons.End();
            m_GhostBlocks.Begin(); m_GhostBlocks.End();
            m_GhostIcons.Begin(); m_GhostIcons.End();
            m_HandBlocks?.Begin(); m_HandBlocks?.End();
            m_HandIcons?.Begin(); m_HandIcons?.End();
            m_Highlights.Begin(); m_Highlights.End();
            Ui.Show(Tooltip, false);
        }
    }
}
