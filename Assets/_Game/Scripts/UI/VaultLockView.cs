using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// The vault's switch and sequence locks on screen (prefab UI/HUD): a row of lamps with their number keys under
    /// them, progress dots for the sequence, a red flash on a wrong press, the keys to use. Rules:
    /// <see cref="Items.SwitchPuzzle"/>, <see cref="Items.SequenceLock"/>; input: <see cref="PlayerController.AtConsole"/>;
    /// this only shows it. (The dial lock is the safe's own screen.)
    /// </summary>
    public class VaultLockView : MonoBehaviour
    {
        public GameObject Root;
        public Text Title;
        [Tooltip("Lamps in a row (as many as the biggest lock has; the extra ones are hidden).")]
        public Image[] Lamps;
        public Text[] Keys;
        [Tooltip("The sequence's progress: one dot per step.")]
        public Image[] Dots;
        public Image Flash;
        public Text Hint;
        public float LampSpacing = 84f;

        static readonly Color Off = new(0.16f, 0.17f, 0.19f, 1f), On = new(1f, 0.82f, 0.3f), Done = new(0.35f, 1f, 0.5f), Red = new(1f, 0.25f, 0.18f);
        int m_HintControls = -1, m_HintLoc = -1, m_HintKeys = -1;

        void Update()
        {
            var me = PlayerController.Local;
            var switches = me != null ? me.Switches : null;
            var sequence = me != null ? me.Sequence : null;
            bool shown = switches != null || sequence != null;
            Ui.Show(Root, shown);
            if (!shown) return;
            int count = switches != null ? switches.Count : sequence.Lamps;
            float now = Time.time;
            int lit = sequence != null ? sequence.Shown(now) : -1;
            for (int i = 0; i < Lamps.Length; i++)
            {
                bool used = i < count;
                Ui.Show(Lamps[i], used);
                Ui.Show(Keys[i], used);
                if (!used) continue;
                Lamps[i].rectTransform.anchoredPosition = new Vector2((i - (count - 1) / 2f) * LampSpacing, 14f);
                Keys[i].rectTransform.anchoredPosition = new Vector2((i - (count - 1) / 2f) * LampSpacing, -46f);
                Lamps[i].color = switches != null ? (switches.Lit(i) ? On : Off) : (i == lit ? On : Off);
            }
            for (int i = 0; i < Dots.Length; i++)
            {
                bool used = sequence != null && i < sequence.Length;
                Ui.Show(Dots[i], used);
                if (!used) continue;
                Dots[i].rectTransform.anchoredPosition = new Vector2((i - (sequence.Length - 1) / 2f) * 22f, -90f);
                Dots[i].color = i < sequence.Done ? Done : sequence.Playing(now) ? new Color(1f, 1f, 1f, 0.15f) : new Color(1f, 1f, 1f, 0.4f);
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
                Hint.text = Loc.F("vault.hint", 1, count, Controls.Label(GameAction.Jump));
            }
        }
    }
}
