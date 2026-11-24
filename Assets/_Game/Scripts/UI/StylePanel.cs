using System.Collections.Generic;
using Stealth.Configs;
using Stealth.Game;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// The visual style picker (prefab UI/HUD, F8 — <see cref="GameAction.Style"/>): a button per style of the list in use
    /// (<see cref="VisualStyles.All"/>, made from <see cref="Template"/> when the panel opens), the current one highlighted.
    /// A click switches the whole team in a session (<see cref="GameManager.RequestStyle"/>), only this window in the menu,
    /// and closes the panel; F8 or Escape closes it too. The panel frees the cursor while it is open.
    /// </summary>
    public class StylePanel : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        public GameObject Root;
        [Tooltip("Where the style buttons go (a grid layout)")]
        public Transform List;
        [Tooltip("The button copied for every style (UI/Elements/Button)")]
        public Button Template;
        [Tooltip("Tint of the current style's button")]
        public Color CurrentColor = UiPalette.NoteYellow;

        readonly List<Button> m_Buttons = new();
        readonly List<VisualStyle> m_Styles = new();
        int m_Shown = -2, m_LocVersion = -1;

        void Awake() => Root.SetActive(false);

        void OnDestroy() => IsOpen = false;

        /// <summary>Test hook (autopilot): open or close the panel.</summary>
        public void SetOpen(bool open) => Show(open);

        void Update()
        {
            if (ControlsPanel.Rebinding) return;
            bool blocked = GameMenu.IsOpen || CheatPanel.IsOpen;
            if (IsOpen && blocked) { Show(false); return; }
            if (!blocked && Controls.Pressed(GameAction.Style)) Show(!IsOpen);
            if (!IsOpen) return;
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) { Show(false); return; }
            Refresh();
        }

        void Show(bool on)
        {
            if (on) Fill();
            IsOpen = on;
            if (Root.activeSelf != on) Root.SetActive(on);
        }

        /// <summary>A button for every style of the list in use (the test rooms have their own list).</summary>
        void Fill()
        {
            var all = VisualStyles.All;
            bool same = all.Count == m_Styles.Count;
            for (int i = 0; same && i < all.Count; i++) same = all[i] == m_Styles[i];
            if (same && m_Buttons.Count > 0) return;
            foreach (var b in m_Buttons) if (b != null) Destroy(b.gameObject);
            m_Buttons.Clear();
            m_Styles.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null) continue;
                int index = i;
                var b = Instantiate(Template, List);
                b.name = all[i].name;
                b.gameObject.SetActive(true);
                foreach (var loc in b.GetComponentsInChildren<LocText>(true)) Destroy(loc); // the label is the style's name
                b.onClick.AddListener(() => Choose(index));
                m_Buttons.Add(b);
                m_Styles.Add(all[i]);
            }
            m_Shown = -2;
            m_LocVersion = -1;
        }

        void Refresh()
        {
            int current = VisualStyles.CurrentIndex;
            if (current == m_Shown && m_LocVersion == Loc.Version) return;
            m_Shown = current;
            m_LocVersion = Loc.Version;
            for (int i = 0; i < m_Buttons.Count; i++)
            {
                var b = m_Buttons[i];
                bool on = m_Styles[i] == VisualStyles.Current;
                var t = b.GetComponentInChildren<Text>();
                if (t != null) t.text = VisualStyles.TitleOf(m_Styles[i]);
                if (b.image != null) b.image.color = on ? CurrentColor : Color.white;
            }
        }

        void Choose(int index)
        {
            // In a session the whole team switches (GameManager replicates the choice); in the menu only this window.
            var gm = GameManager.Instance;
            if (gm != null) gm.RequestStyle(index);
            else VisualStyles.Apply(VisualStyles.At(index));
            Show(false);
        }
    }
}
