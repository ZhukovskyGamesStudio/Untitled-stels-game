using System.Collections.Generic;
using Stealth.Items;
using Stealth.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// The safe minigame on screen (prefab UI/HUD): a chrome-bezelled combination dial that turns with the mouse (a tick
    /// for every number, numbers every five, a fluted knob turning with it, a fixed gold pointer, the number on the knob's
    /// cap), three recessed code windows that light up with the numbers found (a LED under each: waiting, listening,
    /// found), and an oscilloscope — every click of the dial is a pulse running across it, as tall and as green as the
    /// dial is close. The panel pops in; a wrong try shakes it red, a right one rings green. Rules: <see cref="SafeLock"/>;
    /// input: <see cref="PlayerController.Cracking"/>; this only shows it.
    /// </summary>
    public class SafeCrackView : MonoBehaviour
    {
        public GameObject Root;
        [Tooltip("What shakes on a wrong try and pops in on opening (the panel with its shadow).")]
        public RectTransform Panel;
        [Tooltip("Rotates with the dial: ticks, numbers and the knob are its children.")]
        public RectTransform Dial;
        public Image TickPrefab;
        public Text NumberPrefab;
        public float TickRadius = 126f;
        public float NumberRadius = 82f;
        [Tooltip("A red flash over the panel / over the dial's face on a wrong try.")]
        public Image Flash, FaceFlash;
        [Tooltip("A ring around the dial that pulses green when a number is found.")]
        public Image Ring;
        public Image[] Slots, SlotGlows, SlotLeds;
        public Text[] SlotTexts;
        public Text Readout;
        public Image ReadoutGlow;
        [Tooltip("The oscilloscope's drawing area and its trace: line segments with a glow under each.")]
        public RectTransform Trace;
        public Image[] TraceLine, TraceGlow;
        public Text Hint;

        static readonly Color Gold = new(1f, 0.78f, 0.32f), Green = new(0.35f, 1f, 0.5f), Red = new(1f, 0.25f, 0.18f),
            LedOff = new(0.22f, 0.24f, 0.27f), TraceCold = new(0.25f, 0.75f, 0.7f);

        /// <summary>How fast a pulse runs across the scope (its widths per second), and how many are remembered.</summary>
        const float ScopeSpeed = 0.55f;
        const int Pulses = 10;

        readonly List<GameObject> m_Marks = new();
        readonly float[] m_PulseTime = new float[Pulses], m_PulseAmp = new float[Pulses];
        int m_PulseNext;
        int m_Built = -1, m_LastNumber = -1;
        int m_HintControls = -1, m_HintLoc = -1;
        float m_OpenedAt = -10f;
        Vector2 m_PanelHome;
        bool m_Home, m_WasShown;

        void Update()
        {
            var me = PlayerController.Local;
            var dial = me != null ? me.Cracking : null;
            Ui.Show(Root, dial != null);
            if (dial == null) { m_WasShown = false; return; }
            float now = Time.time;
            if (!m_Home) { m_PanelHome = Panel.anchoredPosition; m_Home = true; }
            if (!m_WasShown)
            {
                m_WasShown = true;
                m_OpenedAt = now;
                m_LastNumber = dial.Number;
                for (int i = 0; i < Pulses; i++) m_PulseTime[i] = -100f;
            }
            Build(dial.DialSize);

            // Opening: the panel pops in.
            float open = Mathf.Clamp01((now - m_OpenedAt) / 0.18f);
            Panel.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, 1f - (1f - open) * (1f - open));

            // The dial turns (the knob with it), the pointer stays on top.
            Dial.localRotation = Quaternion.Euler(0, 0, dial.Dial / dial.DialSize * 360f);
            Readout.text = dial.Number.ToString("00");

            // The scope: every click of the dial is a pulse as tall as the dial is close.
            float close = dial.Closeness();
            if (dial.Number != m_LastNumber)
            {
                m_LastNumber = dial.Number;
                m_PulseTime[m_PulseNext] = now;
                m_PulseAmp[m_PulseNext] = SafeScope.PulseHeight(close);
                m_PulseNext = (m_PulseNext + 1) % Pulses;
            }
            DrawScope(now, close);
            ReadoutGlow.color = new Color(Gold.r, Gold.g, Gold.b, 0.12f + 0.25f * close);

            // The code windows: the numbers found so far; the one being listened for blinks its LED.
            for (int i = 0; i < Slots.Length; i++)
            {
                bool used = i < dial.Digits;
                Ui.Show(Slots[i], used);
                if (!used) continue;
                bool found = i < dial.Found, current = i == dial.Found;
                SlotTexts[i].text = found ? dial.CodeAt(i).ToString("00") : current ? "--" : "";
                SlotTexts[i].color = found ? Green : new Color(Gold.r, Gold.g, Gold.b, 0.35f + 0.25f * Mathf.Sin(now * 5f));
                SlotGlows[i].color = found ? new Color(Green.r, Green.g, Green.b, 0.35f) : current ? new Color(Gold.r, Gold.g, Gold.b, 0.1f) : Color.clear;
                SlotLeds[i].color = found ? Green : current ? Color.Lerp(LedOff, Gold, 0.5f + 0.5f * Mathf.Sin(now * 7f)) : LedOff;
            }

            // A try: red shake for a wrong number, a green ring for a right one.
            float age = now - me.LastTryTime;
            bool wrong = me.LastTry == SafeLock.Result.Wrong && age < 0.45f;
            bool right = me.LastTry == SafeLock.Result.Digit && age < 0.6f;
            Panel.anchoredPosition = m_PanelHome + (wrong ? new Vector2(Mathf.Sin(age * 90f) * 9f * (1f - age / 0.45f), 0f) : Vector2.zero);
            Ui.Show(Flash, wrong);
            Ui.Show(FaceFlash, wrong);
            if (wrong)
            {
                Flash.color = new Color(Red.r, Red.g, Red.b, 0.3f * (1f - age / 0.45f));
                FaceFlash.color = new Color(Red.r, Red.g, Red.b, 0.35f * (1f - age / 0.45f));
            }
            Ui.Show(Ring, right);
            if (right)
            {
                float k = age / 0.6f;
                Ring.rectTransform.localScale = Vector3.one * (1f + 0.2f * k);
                Ring.color = new Color(Green.r, Green.g, Green.b, 0.85f * (1f - k));
            }

            if (m_HintControls != Controls.Version || m_HintLoc != Loc.Version || Hint.text.Length == 0)
            {
                m_HintControls = Controls.Version;
                m_HintLoc = Loc.Version;
                Hint.text = Loc.F("safe.hint", Loc.T("key.mouse"), Loc.T("key.lmb"), Controls.Label(GameAction.Interact), Controls.Label(GameAction.Jump));
            }
        }

        /// <summary>
        /// The trace across the scope: a faint hum, and each remembered click a damped ripple that entered at the right edge
        /// and runs to the left. Its colour warms from cold teal to bright green as the dial gets close.
        /// </summary>
        void DrawScope(float now, float close)
        {
            int n = TraceLine.Length;
            var size = Trace.rect.size;
            var line = Color.Lerp(TraceCold, Green, close);
            var glow = new Color(line.r, line.g, line.b, 0.22f + 0.2f * close);
            Vector2 prev = default;
            for (int i = 0; i <= n; i++)
            {
                float x = i / (float)n;
                var p = new Vector2((x - 0.5f) * size.x, Mathf.Clamp(ScopeY(x, now), -1f, 1f) * size.y * 0.47f);
                if (i > 0)
                {
                    var d = p - prev;
                    var mid = (p + prev) * 0.5f;
                    float len = d.magnitude + 0.8f;
                    var turn = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                    var l = TraceLine[i - 1].rectTransform;
                    l.anchoredPosition = mid;
                    l.sizeDelta = new Vector2(len, 2.2f);
                    l.localRotation = turn;
                    var g = TraceGlow[i - 1].rectTransform;
                    g.anchoredPosition = mid;
                    g.sizeDelta = new Vector2(len + 8f, 10f);
                    g.localRotation = turn;
                    TraceLine[i - 1].color = line;
                    TraceGlow[i - 1].color = glow;
                }
                prev = p;
            }
        }

        /// <summary>The trace's height at <paramref name="x"/> (0 left .. 1 right), about -1..1.</summary>
        float ScopeY(float x, float now)
        {
            float y = (Mathf.PerlinNoise(x * 18f, now * 2.3f) - 0.5f) * 0.08f;
            for (int k = 0; k < Pulses; k++)
            {
                float age = now - m_PulseTime[k];
                if (age < 0f || age > 2.5f) continue;
                float u = 1f - age * ScopeSpeed - x;                 // how far behind the pulse's front this point is
                if (u < -0.01f || u > 0.16f) continue;
                u = Mathf.Max(0f, u);
                y += m_PulseAmp[k] * SafeScope.Ripple(u);
            }
            return y;
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
                tick.rectTransform.anchoredPosition = dir * (TickRadius - (major ? 8f : 4.5f));
                tick.rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
                tick.rectTransform.sizeDelta = major ? new Vector2(3f, 16f) : new Vector2(1.5f, 9f);
                tick.color = major ? new Color(1f, 0.86f, 0.5f) : new Color(0.78f, 0.8f, 0.82f, 0.85f);
                tick.transform.SetAsFirstSibling();                       // under the knob
                m_Marks.Add(tick.gameObject);
                if (!major) continue;
                var t = Instantiate(NumberPrefab, Dial, false);
                t.rectTransform.anchoredPosition = dir * NumberRadius;
                t.rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
                t.text = n.ToString();
                t.transform.SetAsFirstSibling();
                m_Marks.Add(t.gameObject);
            }
        }
    }

    /// <summary>
    /// The safe's oscilloscope (pure, unit tested): how tall a click's pulse is and its shape. A far number barely ripples
    /// the trace, a close one fills the scope to its edge: loud and quiet clicks are told apart at a glance.
    /// </summary>
    public static class SafeScope
    {
        /// <summary>The pulse's peak, a share of the scope's half height, for how close the dial is (0..1).</summary>
        public static float PulseHeight(float closeness)
        {
            float c = Mathf.Clamp01(closeness);
            return Mathf.Lerp(0.06f, 1f, c * c * c);
        }

        /// <summary>A damped ripple behind the pulse's front (<paramref name="u"/>: how far behind, in scope widths), its first peak 1.</summary>
        public static float Ripple(float u) => Mathf.Sin(u * 95f) * Mathf.Exp(-u * 26f) / 0.675f;
    }
}
