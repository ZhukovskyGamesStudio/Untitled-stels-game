using System.Collections.Generic;
using Stealth.Items;
using Stealth.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// The safe minigame on screen (prefab UI/HUD): a metal combination dial that turns with the mouse (a tick for every
    /// number, numbers every five, a fixed gold pointer), three code windows that fill with the numbers found, and a
    /// "stethoscope" — a row of bars that rise and flash with every click the closer the dial is to the right number.
    /// A wrong try shakes the panel red, a right one rings green. Rules: <see cref="SafeLock"/>; input:
    /// <see cref="PlayerController.Cracking"/>; this only shows it.
    /// </summary>
    public class SafeCrackView : MonoBehaviour
    {
        public GameObject Root;
        [Tooltip("The panel (it shakes on a wrong try).")]
        public RectTransform Panel;
        [Tooltip("Rotates with the dial: ticks, numbers and the hub grip are its children.")]
        public RectTransform Dial;
        public Image TickPrefab;
        public Text NumberPrefab;
        public float TickRadius = 118f;
        public float NumberRadius = 96f;
        public Image Flash;
        [Tooltip("A ring around the dial that pulses green when a number is found.")]
        public Image Ring;
        public Image[] Slots;
        public Text[] SlotTexts;
        public Image[] Bars;
        public Text Readout;
        public Text Hint;

        static readonly Color Gold = new(1f, 0.78f, 0.32f), Green = new(0.35f, 1f, 0.5f), Red = new(1f, 0.25f, 0.18f);

        readonly List<GameObject> m_Marks = new();
        int m_Built = -1, m_LastNumber = -1;
        float m_Pulse, m_LastTryShown = -10f;
        Vector2 m_PanelHome;
        bool m_Home;

        void Update()
        {
            var me = PlayerController.Local;
            var dial = me != null ? me.Cracking : null;
            Ui.Show(Root, dial != null);
            if (dial == null) return;
            if (!m_Home) { m_PanelHome = Panel.anchoredPosition; m_Home = true; }
            Build(dial.DialSize);

            // The dial turns, the pointer stays on top.
            Dial.localRotation = Quaternion.Euler(0, 0, dial.Dial / dial.DialSize * 360f);
            Readout.text = dial.Number.ToString("00");

            // Stethoscope: every click is a pulse, as loud as the dial is close.
            float close = dial.Closeness();
            if (dial.Number != m_LastNumber)
            {
                m_LastNumber = dial.Number;
                m_Pulse = 0.35f + 0.65f * close;
            }
            m_Pulse = Mathf.MoveTowards(m_Pulse, 0f, Time.deltaTime * 2.5f);
            for (int i = 0; i < Bars.Length; i++)
            {
                float level = (i + 1f) / Bars.Length;
                bool on = level <= Mathf.Max(close, 0.06f);
                var c = Color.Lerp(new Color(0.45f, 0.55f, 0.6f), Green, close);
                c.a = on ? 0.55f + 0.45f * Mathf.Clamp01(m_Pulse * 1.5f) : 0.12f;
                Bars[i].color = c;
            }

            // The code windows: the numbers found so far.
            for (int i = 0; i < Slots.Length; i++)
            {
                bool used = i < dial.Digits;
                Ui.Show(Slots[i], used);
                if (!used) continue;
                bool found = i < dial.Found, current = i == dial.Found;
                Slots[i].color = found ? new Color(0.12f, 0.35f, 0.18f, 0.95f) : current ? new Color(0.22f, 0.2f, 0.12f, 0.95f) : new Color(0.08f, 0.09f, 0.1f, 0.9f);
                SlotTexts[i].text = found ? dial.CodeAt(i).ToString("00") : current ? "··" : "";
                SlotTexts[i].color = found ? Green : Gold;
            }

            // A try: red shake for a wrong number, a green ring for a right one.
            float age = Time.time - me.LastTryTime;
            bool wrong = me.LastTry == SafeLock.Result.Wrong && age < 0.45f;
            bool right = me.LastTry == SafeLock.Result.Digit && age < 0.6f;
            Panel.anchoredPosition = m_PanelHome + (wrong ? new Vector2(Mathf.Sin(age * 90f) * 9f * (1f - age / 0.45f), 0f) : Vector2.zero);
            Ui.Show(Flash, wrong);
            if (wrong) Flash.color = new Color(Red.r, Red.g, Red.b, 0.4f * (1f - age / 0.45f));
            Ui.Show(Ring, right);
            if (right)
            {
                float k = age / 0.6f;
                Ring.rectTransform.localScale = Vector3.one * (1f + 0.25f * k);
                Ring.color = new Color(Green.r, Green.g, Green.b, 0.8f * (1f - k));
            }
            if (me.LastTryTime != m_LastTryShown) m_LastTryShown = me.LastTryTime;

            Hint.text = $"<b>Мышь</b> — крутить    <b>ЛКМ</b> / <b>{Controls.Label(GameAction.Interact)}</b> — попробовать число    " +
                        $"<b>{Controls.Label(GameAction.Jump)}</b> — отойти";
        }

        /// <summary>A tick for every number, a longer one with the number every fifth (instances of the prefabs).</summary>
        void Build(int size)
        {
            if (m_Built == size) return;
            m_Built = size;
            foreach (var go in m_Marks) Destroy(go);
            m_Marks.Clear();
            for (int n = 0; n < size; n++)
            {
                float a = -n / (float)size * Mathf.PI * 2f + Mathf.PI / 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float rot = -n / (float)size * 360f;
                bool major = n % 5 == 0;
                var tick = Instantiate(TickPrefab, Dial, false);
                tick.rectTransform.anchoredPosition = dir * (TickRadius - (major ? 7f : 4f));
                tick.rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
                tick.rectTransform.sizeDelta = major ? new Vector2(3f, 14f) : new Vector2(1.5f, 7f);
                tick.color = major ? new Color(0.92f, 0.9f, 0.84f) : new Color(0.7f, 0.7f, 0.68f, 0.8f);
                m_Marks.Add(tick.gameObject);
                if (!major) continue;
                var t = Instantiate(NumberPrefab, Dial, false);
                t.rectTransform.anchoredPosition = dir * NumberRadius;
                t.rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
                t.text = n.ToString();
                m_Marks.Add(t.gameObject);
            }
        }
    }
}
