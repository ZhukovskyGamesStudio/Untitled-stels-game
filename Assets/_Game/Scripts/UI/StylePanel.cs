using System.Collections.Generic;
using Stealth.Configs;
using Stealth.Game;
using Stealth.Level;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// The visual style picker — the «Визуал» page of the cheat panel (<see cref="CheatPanel"/>, F4): a button per style of
    /// the list (<see cref="VisualStyles.All"/>, made from <see cref="Template"/> when the page first shows), the current one
    /// highlighted. A click switches the whole team (<see cref="GameManager.RequestStyle"/>).
    /// </summary>
    public class StylePanel : MonoBehaviour
    {
        [Tooltip("Where the style buttons go (a grid layout)")]
        public Transform List;
        [Tooltip("The button copied for every style (UI/Elements/Button)")]
        public Button Template;
        [Tooltip("Tint of the current style's button")]
        public Color CurrentColor = UiPalette.NoteYellow;

        readonly List<Button> m_Buttons = new();
        readonly List<VisualStyle> m_Styles = new();
        int m_Shown = -2, m_LocVersion = -1;

        void OnEnable() => Fill();

        void Update() => Refresh();

        /// <summary>A button for every style of the list.</summary>
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
            // in a session the whole team switches (GameManager replicates the choice)
            var gm = GameManager.Instance;
            if (gm != null) gm.RequestStyle(index);
            else VisualStyles.Apply(VisualStyles.At(index));
        }
    }
}
