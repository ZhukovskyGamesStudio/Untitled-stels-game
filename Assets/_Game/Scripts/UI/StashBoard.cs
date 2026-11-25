using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// The team stash — Linus's backpack — shown in the world just over him (prefab UI/StashBoard, a world-space canvas):
    /// a grid of the shared stash and a "burn" note next to it where burning is allowed (it narrows without it). Its
    /// paper is sized to what is on it (<see cref="Size"/>: the columns and rows the stash has, the burn note or not) — never left
    /// smaller than the grid. "Put everything in" is a key in the hint stack (<see cref="HintRules"/>), not a button here.
    /// A dumb view: <see cref="InventoryUI"/> places it, draws the items and handles the drag-and-drop onto it.
    /// </summary>
    public class StashBoard : MonoBehaviour
    {
        public static StashBoard Instance { get; private set; }

        /// <summary>The board's lower edge over Linus, metres: just above his backpack.</summary>
        public const float Lift = 0.85f;

        public Canvas Canvas;
        public RectTransform Root;
        [Tooltip("Cells of the stash grid (pivot top-left, InventoryUI.Cell units per cell).")]
        public RectTransform Grid;
        public Image[] Cells;
        public RectTransform ItemLayer;
        public RectTransform HighlightLayer;
        public RectTransform SellZone;
        public Image SellBackground;
        public Text ValueLabel;
        [Tooltip("The grid's graph paper and its frame: sized to the columns and rows the stash has now (Fit).")]
        public RectTransform GraphPaper, GridBox;
        [Tooltip("Columns and rows the board is built for (the biggest box: the backpack's top level); Cells row by row.")]
        public int BuiltColumns = Game.TeamStash.MaxWidth, BuiltRows = Game.TeamStash.MaxHeight;
        [Tooltip("The rule under the title (as wide as the board's content).")]
        public RectTransform TitleRule;
        [Tooltip("How much wider the board is with the burn note (its gap and width), canvas units.")]
        public float SellSpan;
        [Tooltip("The paper's margin right of and under its content, canvas units.")]
        public float Pad = 10f;

        int m_Rows = -1, m_Columns = -1;
        bool m_Sell = true;

        public bool Visible => Canvas.enabled;

        void Awake()
        {
            Instance = this;
            Canvas.enabled = false;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show(bool on)
        {
            if (Canvas.enabled != on) Canvas.enabled = on;
        }

        /// <summary>
        /// The board for a stash of <paramref name="columns"/> × <paramref name="rows"/> (the storage robot's box grows with
        /// its backpack): the cells past them hidden, the grid, its paper and frame, the burn note and the board's paper laid
        /// out for them.
        /// </summary>
        public void Fit(int columns, int rows)
        {
            columns = Mathf.Clamp(columns, 1, BuiltColumns);
            rows = Mathf.Clamp(rows, 1, BuiltRows);
            if (rows == m_Rows && columns == m_Columns) return;
            m_Rows = rows;
            m_Columns = columns;
            int built = Mathf.Max(1, BuiltColumns);
            for (int i = 0; i < Cells.Length; i++)
                if (Cells[i] != null) Cells[i].gameObject.SetActive(i % built < columns && i / built < rows);
            Layout();
        }

        /// <summary>The burn note shown or not (only where burning is allowed): without it the board and its rule are that much narrower.</summary>
        public void SetSell(bool on)
        {
            if (SellZone != null && SellZone.gameObject.activeSelf != on) SellZone.gameObject.SetActive(on);
            if (on == m_Sell) return;
            m_Sell = on;
            Layout();
        }

        /// <summary>
        /// Every size set anew from the rows and the burn note (not changed by steps from the last: a board whose sizes
        /// were saved differently — an old scene's override — used to keep its old paper, and the grid hung out under it).
        /// </summary>
        void Layout()
        {
            int rows = m_Rows < 0 ? BuiltRows : m_Rows, columns = m_Columns < 0 ? BuiltColumns : m_Columns;
            float gw = columns * InventoryUI.Cell, gh = rows * InventoryUI.Cell;
            Set(Grid, gw, gh);
            Set(GraphPaper, gw, gh);
            Set(GridBox, gw + 4f, gh + 4f);
            if (SellZone != null && Grid != null)
            {
                Set(SellZone, SellZone.sizeDelta.x, gh);
                // (right of the grid, the gap the board was built with)
                SellZone.anchoredPosition = new Vector2(Grid.anchoredPosition.x + gw + (SellSpan - SellZone.sizeDelta.x), SellZone.anchoredPosition.y);
            }
            if (TitleRule != null) Set(TitleRule, gw + (m_Sell ? SellSpan : 0f), TitleRule.sizeDelta.y);
            if (ValueLabel != null) Set(ValueLabel.rectTransform, gw + (m_Sell ? SellSpan : 0f), ValueLabel.rectTransform.sizeDelta.y);
            if (Grid != null) Root.sizeDelta = Size(Grid.anchoredPosition, gw, gh, m_Sell ? SellSpan : 0f, Pad);
        }

        static void Set(RectTransform rt, float w, float h)
        {
            if (rt != null) rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>
        /// The board's paper for its content (pure): the grid placed at <paramref name="gridAt"/> (its top-left corner from
        /// the paper's, y down negative), <paramref name="gridWidth"/> × <paramref name="gridHeight"/>, the burn note's span
        /// beside it (0 without), and the margin right of and under it all.
        /// </summary>
        public static Vector2 Size(Vector2 gridAt, float gridWidth, float gridHeight, float sellSpan, float pad) =>
            new(gridAt.x + gridWidth + sellSpan + pad, -gridAt.y + gridHeight + pad);

        /// <summary>Hangs just over Linus (its lower edge above his backpack), facing the viewer's eyes.</summary>
        public void Place(Vector3 bot, Vector3 viewer)
        {
            float half = Root.rect.height * Root.lossyScale.y * 0.5f;
            var pos = bot + Vector3.up * (Lift + half);
            var dir = pos - viewer;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            var flat = new Vector3(dir.x, 0f, dir.z);
            // turned to the eyes, but never tipped further than 35° off upright (looked at from right over it)
            var look = Quaternion.LookRotation(Vector3.RotateTowards(flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward, dir.normalized, 35f * Mathf.Deg2Rad, 0f));
            Root.SetPositionAndRotation(pos, look);
        }

        /// <summary>Metres in front of the eyes while sitting in the hatch.</summary>
        public const float HatchDistance = 1.9f;

        /// <summary>
        /// From inside the hatch the eyes are at the floor and the board above the hatch is right overhead: it floats
        /// in front of the view instead, a little above the floor, tilted to face the eyes.
        /// </summary>
        public void PlaceInFront(Transform eyes)
        {
            var flat = eyes.forward;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) flat = eyes.up;
            flat.y = 0f;
            flat.Normalize();
            var pos = eyes.position + flat * HatchDistance + Vector3.up * 0.75f; // above the backpack at the bottom of the screen
            Root.SetPositionAndRotation(pos, Quaternion.LookRotation(pos - eyes.position));
        }
    }
}
