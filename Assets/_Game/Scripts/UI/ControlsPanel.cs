using System.Collections.Generic;
using Stealth.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// The list of every control (prefab UI/Menu, on the pause page and from the main menu): click a key, press another
    /// one — it is saved at once. Esc cancels. Rows are instances of a pre-built row prefab, one per action.
    /// </summary>
    public class ControlsPanel : MonoBehaviour
    {
        /// <summary>Waiting for a key: Esc cancels instead of closing the menu, gameplay keys do nothing.</summary>
        public static bool Rebinding => s_Waiting != null;

        public RectTransform[] Columns;
        public ControlRowView RowPrefab;
        public Text GroupPrefab;
        public Text Hint;
        public Button Reset;

        static readonly Color KeyNormal = new(0.2f, 0.22f, 0.26f, 0.95f);
        static readonly Color KeyWaiting = new(0.95f, 0.75f, 0.2f, 1f);
        static readonly Color KeyConflict = new(0.75f, 0.2f, 0.18f, 0.95f);
        static ControlsPanel s_Waiting;

        readonly List<(GameAction action, ControlRowView row)> m_Rows = new();
        GameAction m_Target;
        bool m_Built;

        /// <summary>Not rebindable, listed so that the page holds every control of the game.</summary>
        static readonly (string title, string key)[] Fixed =
        {
            ("Осмотреться", "Мышь"),
            ("В руках: тащить предмет", "ЛКМ"),
            ("Меню, пауза", "Esc"),
        };

        void Awake()
        {
            Build();
            Reset.onClick.AddListener(() =>
            {
                Controls.ResetToDefaults();
                Controls.Save();
                Cancel();
            });
        }

        void Build()
        {
            if (m_Built) return;
            m_Built = true;
            // Movement and actions on the left, the team and the rest on the right.
            string group = null;
            foreach (var info in Controls.All)
            {
                var column = Columns[info.Group == "Движение" || info.Group == "Действия" ? 0 : Columns.Length - 1];
                if (info.Group != group)
                {
                    group = info.Group;
                    Instantiate(GroupPrefab, column, false).text = group;
                }
                var row = Instantiate(RowPrefab, column, false);
                row.Title.text = info.Title;
                var action = info.Action;
                row.Key.onClick.AddListener(() => Begin(action));
                m_Rows.Add((action, row));
            }
            var last = Columns[Columns.Length - 1];
            Instantiate(GroupPrefab, last, false).text = "Всегда";
            foreach (var (title, key) in Fixed)
            {
                var row = Instantiate(RowPrefab, last, false);
                row.Title.text = title;
                row.KeyLabel.text = key;
                row.Key.interactable = false;
                row.KeyBackground.color = new Color(0.12f, 0.13f, 0.15f, 0.9f);
                row.KeyLabel.color = new Color(1f, 1f, 1f, 0.8f);
            }
        }

        void Begin(GameAction action)
        {
            s_Waiting = this;
            m_Target = action;
            EventSystem.current?.SetSelectedGameObject(null); // Space / Enter must not "click" the button again
        }

        public void Cancel()
        {
            if (s_Waiting == this) s_Waiting = null;
        }

        void OnDisable() => Cancel();

        void Update()
        {
            if (s_Waiting == this && Controls.CapturePress(out var binding))
            {
                Controls.Set(m_Target, binding);
                Controls.Save();
                PlayerPrefs.Save();
                s_Waiting = null;
            }
            string conflict = null;
            foreach (var (action, row) in m_Rows)
            {
                bool waiting = s_Waiting == this && m_Target == action;
                bool clash = Controls.Conflicts(action, out var other);
                if (clash && conflict == null) conflict = $"«{Controls.Label(action)}» занята дважды: {Controls.All[(int)action].Title} и {Controls.All[(int)other].Title}";
                string label = waiting ? "нажми клавишу…" : Controls.Label(action);
                if (row.KeyLabel.text != label) row.KeyLabel.text = label;
                row.KeyBackground.color = waiting ? KeyWaiting : clash ? KeyConflict : KeyNormal;
                row.KeyLabel.color = waiting ? Color.black : Color.white;
            }
            string hint = s_Waiting == this
                ? $"Нажми новую клавишу или кнопку мыши для «{Controls.All[(int)m_Target].Title}» · Esc — отмена"
                : conflict ?? "Нажми на клавишу, чтобы переназначить. Сохраняется сразу.";
            if (Hint.text != hint) Hint.text = hint;
            Hint.color = s_Waiting != this && conflict != null ? new Color(1f, 0.55f, 0.45f) : new Color(1, 1, 1, 0.65f);
        }
    }
}
