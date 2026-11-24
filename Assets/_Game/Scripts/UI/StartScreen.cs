using Stealth.Game;
using Stealth.Items;
using Stealth.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// The run's start screen (prefab UI/HUD, outside its content): while <see cref="GameManager.Picking"/> the team stands
    /// in a row behind the car (<see cref="PickingRules"/>), a column under each figure shows their class and extra item;
    /// the local player changes theirs with the arrows under their own figure (or ←/→ and ↑/↓) and says ready (button,
    /// Space or Enter). Everything is pre-built in the prefab; this only places, fills and wires it.
    /// </summary>
    public class StartScreen : MonoBehaviour
    {
        public GameObject Root;
        public RectTransform Canvas;
        public StartSlotView[] Slots;
        public Button ReadyButton;
        public Text ReadyButtonLabel;
        public Text Waiting;
        [Tooltip("The items' pictures (Data/ItemVisuals Icons).")]
        public ItemVisuals Visuals;

        bool m_Wired;

        void Wire()
        {
            if (m_Wired) return;
            m_Wired = true;
            foreach (var slot in Slots)
            {
                slot.ClassPrev.onClick.AddListener(() => Change(-1, 0));
                slot.ClassNext.onClick.AddListener(() => Change(1, 0));
                slot.ItemPrev.onClick.AddListener(() => Change(0, -1));
                slot.ItemNext.onClick.AddListener(() => Change(0, 1));
            }
            if (ReadyButton != null) ReadyButton.onClick.AddListener(ToggleReady);
        }

        static void Change(int cls, int item)
        {
            var me = PlayerController.Local;
            if (me == null || me.LoadoutReady) return;
            me.ChooseLoadout(me.LoadoutClass + cls, me.LoadoutItem + item, false);
            Audio.Sfx.Ui(Audio.Sfx.Bank != null ? Audio.Sfx.Bank.Pickup : null, 0.6f, Audio.Sfx.Group.Items);
        }

        static void ToggleReady()
        {
            var me = PlayerController.Local;
            if (me == null) return;
            me.ChooseLoadout(me.LoadoutClass, me.LoadoutItem, !me.LoadoutReady);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            var me = PlayerController.Local;
            bool on = gm != null && me != null && gm.Picking && !GameMenu.IsOpen;
            Ui.Show(Root, on);
            if (!on) return;
            Wire();
            Keys(me);
            var cam = me.Camera;
            var size = Canvas.rect.size;
            int ready = 0, count = 0;
            foreach (var slot in Slots) Ui.Show(slot, false);
            foreach (var p in PlayerController.All)
            {
                if (!p.IsSpawned) continue;
                count++;
                if (p.LoadoutReady) ready++;
                int s = PlayerLooks.SlotOf(p.ClientId);
                if (s < 0 || s >= Slots.Length) continue;
                var view = Slots[s];
                Ui.Show(view, true);
                // under the figure's feet
                if (cam != null)
                {
                    var vp = cam.WorldToViewportPoint(p.transform.position);
                    // (kept whole on the screen: on a narrow screen the figures at the ends stand near its edges)
                    var half = view.Rect.rect.size;
                    if (vp.z > 0f) view.Rect.anchoredPosition = UiMath.KeepOnScreen(new Vector2(vp.x * size.x, vp.y * size.y), size,
                        new Vector2(half.x * 0.5f, half.y), new Vector2(half.x * 0.5f, 0f), 8f);
                }
                Fill(view, p, p == me);
            }
            Ui.Set(Waiting, Loc.F("start.waiting", ready, count), UiPalette.Ink);
            if (ReadyButtonLabel != null) ReadyButtonLabel.text = Loc.T(me.LoadoutReady ? "start.not_ready" : "start.ready");
        }

        void Fill(StartSlotView v, PlayerController p, bool mine)
        {
            byte tool = ItemCatalog.ClassTools[Mathf.Clamp(p.LoadoutClass, 0, ItemCatalog.ClassTools.Length - 1)];
            byte extra = ItemCatalog.StartItems[Mathf.Clamp(p.LoadoutItem, 0, ItemCatalog.StartItems.Length - 1)];
            var team = PlayerLooks.TeamColor(PlayerLooks.SlotOf(p.ClientId));
            Ui.Set(v.Name, p.Nick, UiPalette.InkOf(team));
            Ui.Set(v.ClassName, Loc.T(ClassKey(p.LoadoutClass)), UiPalette.Ink);
            Ui.Set(v.ItemName, ItemCatalog.Describe(extra, ItemCatalog.FreshColor(extra, 0)), UiPalette.InkSoft);
            Picture(v.ClassIcon, tool);
            Picture(v.ItemIcon, extra);
            bool arrows = mine && !p.LoadoutReady;
            Ui.Show(v.ClassPrev, arrows);
            Ui.Show(v.ClassNext, arrows);
            Ui.Show(v.ItemPrev, arrows);
            Ui.Show(v.ItemNext, arrows);
            if (v.ReadyMark != null) v.ReadyMark.color = p.LoadoutReady ? UiPalette.MarkGreen : UiPalette.PaperShade;
            Ui.Set(v.ReadyLabel, Loc.T(p.LoadoutReady ? "start.is_ready" : "start.choosing"), p.LoadoutReady ? UiPalette.Good : UiPalette.InkFaint);
        }

        /// <summary>The table key of a class's name (<c>start.class.&lt;n&gt;</c>).</summary>
        static string ClassKey(int cls) => "start.class." + cls;

        void Picture(Image image, byte shape)
        {
            if (image == null) return;
            var sprite = Visuals != null && Visuals.Icons != null && shape < Visuals.Icons.Length ? Visuals.Icons[shape] : null;
            Ui.Show(image, sprite != null);
            if (sprite != null && image.sprite != sprite) image.sprite = sprite;
        }

        /// <summary>The keys: ←/→ the class, ↑/↓ the item, Space or Enter — ready.</summary>
        static void Keys(PlayerController me)
        {
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k == null) return;
            if (k.leftArrowKey.wasPressedThisFrame) Change(-1, 0);
            if (k.rightArrowKey.wasPressedThisFrame) Change(1, 0);
            if (k.upArrowKey.wasPressedThisFrame) Change(0, -1);
            if (k.downArrowKey.wasPressedThisFrame) Change(0, 1);
            if (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame) ToggleReady();
        }
    }
}
