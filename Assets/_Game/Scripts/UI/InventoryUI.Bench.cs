using Stealth.Audio;
using Stealth.Game;
using Stealth.Items;
using Stealth.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// The inventory and the car's workbench (<see cref="PlayerController.AtBench"/>): its 4 x 4 grid above the backpack,
    /// «combine» right of it with what will come of it over the button — the result's picture and name if the team has
    /// made these things into it this run (<see cref="BenchRules.KnownResult"/>), «result unknown» if not — and an (i)
    /// that tells the rules on hover. Things go on and off the bench by drag as between the backpack and the stash.
    /// </summary>
    public partial class InventoryUI
    {
        [Header("Workbench (the car's)")]
        [Tooltip("The bench's panel (shown while the local player has the car's bench open).")]
        public GameObject BenchPanel;
        [Tooltip("The bench's 4 x 4 grid (top-left pivot, cells like the backpack's).")]
        public RectTransform BenchGrid;
        public Image[] BenchCells;
        public RectTransform BenchItems, BenchHighlights;
        [Tooltip("«Combine»: a press makes what lies on the bench into its recipe's result (or one thing may break).")]
        public RectTransform CombineButton;
        public Image CombineBackground;
        public Text CombineLabel;
        [Tooltip("Over the button: the known result's picture (hidden when unknown) and its name / «result unknown».")]
        public Image ResultIcon;
        public Text ResultLabel;
        [Tooltip("The (i): hovering it shows the rules (InfoTip).")]
        public RectTransform InfoZone;
        public GameObject InfoTip;
        [Tooltip("The «?» a blueprint shows for the thing it makes while the team has not made it yet.")]
        public Sprite QuestionIcon;

        static Sprite s_Question;

        UiPool<InventoryBlockView> m_BenchItems;
        UiPool<Image> m_BenchIcons, m_BenchHighlights;
        InventoryEntry m_DragBenched;
        int m_CombineSeen = -1;
        (int version, int mask, int result) m_KnownFor = (-1, 0, -1);
        float m_FeedbackUntil;
        string m_Feedback;
        int m_FeedbackShape = -1;

        static TeamStash BenchStash => GameManager.Instance != null ? GameManager.Instance.Bench : null;

        void AwakeBench()
        {
            if (QuestionIcon != null) s_Question = QuestionIcon;
            if (BenchItems == null) return;
            m_BenchItems = new UiPool<InventoryBlockView>(BlockPrefab, BenchItems);
            m_BenchIcons = new UiPool<Image>(HighlightPrefab, BenchItems);
            m_BenchHighlights = new UiPool<Image>(HighlightPrefab, BenchHighlights);
        }

        bool BenchOpen(PlayerController me) => BenchPanel != null && me != null && me.AtBench && BenchStash != null;

        /// <summary>What on the bench's panel the cursor is over: its grid (and the cell), «combine», the (i), or nothing.</summary>
        Target BenchTarget(PlayerController me, out Vector2Int cell)
        {
            cell = default;
            if (!BenchOpen(me)) return Target.None;
            if (CellIn(BenchGrid, null, MouseScreen, out cell)) return Target.Bench;
            if (CombineButton != null && RectTransformUtility.RectangleContainsScreenPoint(CombineButton, MouseScreen, null)) return Target.Combine;
            if (InfoZone != null && RectTransformUtility.RectangleContainsScreenPoint(InfoZone, MouseScreen, null)) return Target.Info;
            return Target.None;
        }

        void BeginBenchDrag(Target over, Vector2Int cell, PlayerInventory inv, bool press)
        {
            var bench = BenchStash;
            if (bench == null) return;
            if (over == Target.Combine)
            {
                if (press && BenchRules.CanCombine(bench.Items.Count))
                {
                    inv.RequestCombine();
                    Sfx.Ui(Sfx.Bank?.Click, 0.8f);
                }
                return;
            }
            if (over != Target.Bench) return;
            int idx = bench.IndexAt(cell);
            if (idx < 0) return;
            var e = bench.Items[idx];
            m_Tooltip = ItemCatalog.Describe(e.Shape, e.Color);
            if (!press) return;
            m_Source = DragSource.Bench;
            m_DragBenched = e;
            StartShape(e, cell);
        }

        /// <summary>The dragged thing let go over a cell of the bench.</summary>
        void DropOnBench(PlayerInventory inv, byte x, byte y, byte rot)
        {
            var bench = BenchStash;
            if (bench == null) return;
            bool placed = true;
            if (m_Source == DragSource.Inventory && bench.CanPlace(m_DragShape, rot, x, y)) inv.RequestBench(m_DragIndex, x, y, rot);
            else if (m_Source == DragSource.Bench && bench.CanPlace(m_DragShape, rot, x, y, bench.IndexOf(m_DragBenched))) inv.RequestBenchMove(m_DragBenched, x, y, rot);
            else placed = false;
            if (placed) Sfx.Ui(Sfx.Bank?.Stash, 0.8f, Sfx.Group.Items);
        }

        /// <summary>A thing from the bench let go off its grid: into the first free spot of the backpack.</summary>
        void BenchToBackpack(PlayerInventory inv)
        {
            if (inv.FindSpot(m_DragShape, out var x, out var y, out var r)) { inv.RequestUnbench(m_DragBenched, x, y, r); Sfx.Ui(Sfx.Bank?.Pickup, 0.8f, Sfx.Group.Items); }
            else m_FullUntil = Time.time + 1.5f;
        }

        void HighlightBench(Vector2Int target, bool ok)
        {
            if (m_BenchHighlights == null) return;
            foreach (var cc in ItemCatalog.RotatedCells(m_DragShape, m_DragRot))
            {
                var h = m_BenchHighlights.Next();
                h.rectTransform.anchoredPosition = new Vector2((target.x + cc.x) * Cell + 1, -((target.y + cc.y) * Cell + 1));
                h.color = ok ? new Color(0.3f, 1f, 0.4f, 0.35f) : new Color(1f, 0.25f, 0.2f, 0.4f);
            }
        }

        void DrawBench(PlayerController me, PlayerInventory inv)
        {
            bool open = BenchOpen(me);
            if (BenchPanel == null) return;
            if (BenchPanel.activeSelf != open) BenchPanel.SetActive(open);
            var gm = GameManager.Instance;
            if (!open && gm != null) m_CombineSeen = gm.LastCombine; // (what came of a press while it was shut is old news)
            if (!open || m_BenchItems == null) return;
            var bench = gm.Bench;
            bool dragging = m_Source != DragSource.None;
            foreach (var c in BenchCells) Ui.Alpha(c, dragging ? 0.16f : 0.08f);
            m_BenchItems.Begin();
            m_BenchIcons.Begin();
            for (int i = 0; i < bench.Items.Count; i++)
            {
                var e = bench.Items[i];
                if (m_Source == DragSource.Bench && e.Equals(m_DragBenched)) continue;
                AddShape(m_BenchItems, m_BenchIcons, new Vector2Int(e.X, e.Y), e.Shape, e.Color, e.Rot, 1f);
            }
            m_BenchItems.End();
            m_BenchIcons.End();

            // what the last press of «combine» did, for a moment: made / nothing came of it / one thing broke
            if (gm.LastCombine != m_CombineSeen)
            {
                bool first = m_CombineSeen < 0;
                m_CombineSeen = gm.LastCombine;
                var outcome = (GameManager.BenchOutcome)(gm.LastCombine & 0xFF);
                int shape = gm.LastCombine >> 8 & 0xFF;
                if (!first && outcome != GameManager.BenchOutcome.None)
                {
                    m_FeedbackUntil = Time.time + 2.5f;
                    m_FeedbackShape = outcome == GameManager.BenchOutcome.Failed ? -1 : shape;
                    m_Feedback = outcome switch
                    {
                        GameManager.BenchOutcome.Made => Loc.F("bench.made", Loc.T(ItemCatalog.NameKey(shape))),
                        GameManager.BenchOutcome.Broke => Loc.F("bench.broke", ItemName(shape)),
                        _ => Loc.T("bench.failed"),
                    };
                    var bank = Sfx.Bank;
                    Sfx.Ui(outcome == GameManager.BenchOutcome.Made ? bank?.Pickup : bank?.Drop, 0.9f, Sfx.Group.Items);
                }
            }

            if (bench.Version != m_KnownFor.version || gm.KnownRecipes != m_KnownFor.mask)
                m_KnownFor = (bench.Version, gm.KnownRecipes, BenchRules.KnownResult(bench.Items, gm.Book, gm.KnownRecipes));
            int known = m_KnownFor.result;
            string label;
            Sprite picture = null;
            if (Time.time < m_FeedbackUntil)
            {
                label = m_Feedback;
                if (m_FeedbackShape >= 0) picture = IconOf(m_FeedbackShape);
            }
            else if (known >= 0)
            {
                byte result = gm.Book.Get(known).Result;
                label = Loc.T(ItemCatalog.NameKey(result));
                picture = IconOf(result);
            }
            else label = Loc.T("bench.unknown");
            if (ResultIcon != null)
            {
                Ui.Show(ResultIcon, picture != null);
                if (picture != null && ResultIcon.sprite != picture) { ResultIcon.sprite = picture; ResultIcon.preserveAspect = true; }
            }
            SetIfChanged(ResultLabel, label);
            bool can = BenchRules.CanCombine(bench.Items.Count);
            bool over = !dragging && me.CursorMode && MouseTarget(me, out _) == Target.Combine;
            if (CombineBackground != null)
                CombineBackground.color = !can ? new Color(0.95f, 0.55f, 0.1f, 0.2f) : over ? new Color(1f, 0.62f, 0.15f, 0.95f) : new Color(0.95f, 0.55f, 0.1f, 0.6f);
            if (InfoTip != null)
            {
                bool tip = !dragging && me.CursorMode && MouseTarget(me, out _) == Target.Info;
                if (InfoTip.activeSelf != tip) InfoTip.SetActive(tip);
            }
        }

        static string ItemName(int shape) => ItemCatalog.ShortName(shape);

        void HideBench()
        {
            if (BenchPanel != null && BenchPanel.activeSelf) BenchPanel.SetActive(false);
        }

        /// <summary>
        /// A blueprint in a grid: over its sheet, the pictures of the things its recipe takes and, last, the thing it makes —
        /// a «?» until the team has made it this run. Laid out row by row in the blueprint's cells as they lie (turned or not).
        /// </summary>
        static void DrawBlueprint(UiPool<Image> icons, Vector2Int origin, int recipe, Vector2Int turned, float alpha)
        {
            var gm = GameManager.Instance;
            var book = gm != null ? gm.Book : CraftBook.Current;
            var r = book.Get(recipe);
            if (r == null) return;
            const float size = 22f, gap = 2f;
            float w = turned.x * Cell - 8f, h = turned.y * Cell - 8f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((w + gap) / (size + gap)));
            int n = r.Ingredients.Length + 1;
            int rows = Mathf.CeilToInt(n / (float)perRow);
            float y0 = (h - rows * size - (rows - 1) * gap) / 2f;
            for (int i = 0; i < n; i++)
            {
                bool result = i == n - 1;
                bool known = result && gm != null && BenchRules.IsKnown(gm.KnownRecipes, recipe);
                var sprite = !result ? IconOf(r.Ingredients[i]) : known ? IconOf(r.Result) : s_Question;
                if (sprite == null) continue;
                int row = i / perRow, col = i % perRow;
                int inRow = Mathf.Min(perRow, n - row * perRow);
                float x0 = (w - inRow * size - (inRow - 1) * gap) / 2f;
                var img = icons.Next();
                var rt = img.rectTransform;
                img.sprite = sprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = new Color(1f, 1f, 1f, alpha);
                rt.pivot = new Vector2(0f, 1f);
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(origin.x * Cell + 4f + x0 + col * (size + gap), -(origin.y * Cell + 4f + y0 + row * (size + gap)));
                rt.SetAsLastSibling();
            }
        }
    }
}
