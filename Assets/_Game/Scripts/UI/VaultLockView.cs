using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// The vault's switch and sequence locks on screen (prefab UI/HUD): a row of glass lamps in chrome bezels — a lit one
    /// glows and throws a halo, and lamps fade up fast and down slowly like real bulbs — with a key cap under each, the
    /// sequence's progress as LEDs, a red flash on a wrong press, the keys to use. The panel pops in. Rules:
    /// <see cref="Items.SwitchPuzzle"/>, <see cref="Items.SequenceLock"/>; input: <see cref="PlayerController.AtConsole"/>;
    /// this only shows it. (The dial lock is the safe's own screen.)
    /// </summary>
    public class VaultLockView : MonoBehaviour
    {
        public GameObject Root;
        public Text Title;
        [Tooltip("A lamp's place (the lens, bezel, halo and key cap are its children); as many as the biggest lock has.")]
        public RectTransform[] Slots;
        [Tooltip("The lamps' glass lenses (tinted off / on).")]
        public Image[] Lamps;
        public Image[] Halos;
        public Text[] Keys;
        [Tooltip("The sequence's progress: one LED per step, a glow under each.")]
        public Image[] Dots, DotGlows;
        public Image Flash;
        public Text Hint;
        public float LampSpacing = 100f;

        // round stickers on the sheet: a pale one off, highlighter yellow lit, green once a step is done
        static readonly Color Off = UiPalette.PaperShade, On = UiPalette.MarkYellow, Done = UiPalette.MarkGreen, Red = UiPalette.Stamp,
            LedOff = UiPalette.PaperShade;
        int m_HintControls = -1, m_HintLoc = -1, m_HintKeys = -1;
        float[] m_Glow;
        float m_OpenedAt;
        bool m_WasShown;

        void Update()
        {
            var me = PlayerController.Local;
            var switches = me != null ? me.Switches : null;
            var sequence = me != null ? me.Sequence : null;
            bool shown = switches != null || sequence != null;
            Ui.Show(Root, shown);
            if (!shown) { m_WasShown = false; return; }
            float now = Time.time;
            m_Glow ??= new float[Slots.Length];
            if (!m_WasShown)
            {
                m_WasShown = true;
                m_OpenedAt = now;
                System.Array.Clear(m_Glow, 0, m_Glow.Length);
            }
            float open = Mathf.Clamp01((now - m_OpenedAt) / 0.18f);
            Root.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, 1f - (1f - open) * (1f - open));

            int count = switches != null ? switches.Count : sequence.Lamps;
            int lit = sequence != null ? sequence.Shown(now) : -1;
            float spacing = Mathf.Min(LampSpacing, 520f / Mathf.Max(1, count));
            for (int i = 0; i < Slots.Length; i++)
            {
                bool used = i < count;
                Ui.Show(Slots[i], used);
                if (!used) continue;
                Slots[i].anchoredPosition = new Vector2((i - (count - 1) / 2f) * spacing, Slots[i].anchoredPosition.y);
                bool on = switches != null ? switches.Lit(i) : i == lit;
                // a bulb: up fast, down slowly
                m_Glow[i] = Mathf.MoveTowards(m_Glow[i], on ? 1f : 0f, Time.deltaTime * (on ? 14f : 4f));
                float g = m_Glow[i];
                Lamps[i].color = Color.Lerp(Off, On, g);
                Halos[i].color = On.A(0.55f * g);
                Halos[i].rectTransform.localScale = Vector3.one * (0.85f + 0.15f * g);
            }
            for (int i = 0; i < Dots.Length; i++)
            {
                bool used = sequence != null && i < sequence.Length;
                Ui.Show(Dots[i], used);
                Ui.Show(DotGlows[i], used);
                if (!used) continue;
                var at = new Vector2((i - (sequence.Length - 1) / 2f) * 26f, Dots[i].rectTransform.anchoredPosition.y);
                Dots[i].rectTransform.anchoredPosition = at;
                DotGlows[i].rectTransform.anchoredPosition = at;
                bool done = i < sequence.Done, next = i == sequence.Done && !sequence.Playing(now);
                Dots[i].color = done ? Done : next ? Color.Lerp(LedOff, On, 0.5f + 0.5f * Mathf.Sin(now * 6f)) : LedOff;
                DotGlows[i].color = done ? new Color(Done.r, Done.g, Done.b, 0.5f) : Color.clear;
            }
            float age = now - me.ConsoleWrongTime;
            bool wrong = age < 0.45f;
            Ui.Show(Flash, wrong);
            if (wrong) Flash.color = new Color(Red.r, Red.g, Red.b, 0.35f * (1f - age / 0.45f));
            string title = Loc.T(switches != null ? "vault.switches.title" : "vault.sequence.title");
            if (Title.text != title) Title.text = title;
            if (m_HintControls != Controls.Version || m_HintLoc != Loc.Version || m_HintKeys != count)
            {
                m_HintControls = Controls.Version;
                m_HintLoc = Loc.Version;
                m_HintKeys = count;
                Hint.text = Loc.F("vault.hint", 1, count, Loc.T("key.esc"));
            }
        }
    }
}
