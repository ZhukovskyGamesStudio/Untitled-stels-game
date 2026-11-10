using System.Collections.Generic;
using Stealth.Items;
using Stealth.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// The safe minigame on screen (prefab UI/HUD): a combination dial that turns with the mouse, a stethoscope bar that
    /// gets louder near the right number, the digits found so far. The rules are in <see cref="SafeLock"/>; the player
    /// drives it (<see cref="PlayerController.Cracking"/>); this only shows it.
    /// </summary>
    public class SafeCrackView : MonoBehaviour
    {
        public GameObject Root;
        [Tooltip("Rotates with the dial: the numbers are its children.")]
        public RectTransform Dial;
        public Text NumberPrefab;
        public Image Flash;
        public BarView Listen;
        public Text ListenLabel;
        public Text Digits;
        public Text Current;
        public Text Hint;
        public float NumberRadius = 96f;

        readonly List<Text> m_Numbers = new();
        int m_Built = -1;

        void Update()
        {
            var me = PlayerController.Local;
            var lockState = me != null ? me.Cracking : null;
            Ui.Show(Root, lockState != null);
            if (lockState == null) return;
            Build(lockState.DialSize);

            // The dial turns, the pointer stays on top.
            Dial.localRotation = Quaternion.Euler(0, 0, lockState.Dial / lockState.DialSize * 360f);
            float close = lockState.Closeness();
            Listen.Set(Mathf.Max(0.03f, close), Color.Lerp(new Color(0.5f, 0.6f, 0.7f), new Color(0.4f, 1f, 0.5f), close));
            Ui.Set(ListenLabel, close >= 0.99f ? "ЩЁЛК — здесь!" : close > 0.6f ? "громко" : close > 0.2f ? "слышно" : "тихо",
                close >= 0.99f ? new Color(0.5f, 1f, 0.6f) : Color.white);
            string digits = "";
            for (int i = 0; i < lockState.Digits; i++) digits += i < lockState.Found ? "● " : "○ ";
            Digits.text = $"Код: {digits.Trim()}";
            Current.text = lockState.Number.ToString("00");

            float age = Time.time - me.LastTryTime;
            bool flash = age < 0.35f && me.LastTry != SafeLock.Result.Nothing;
            Ui.Show(Flash, flash);
            if (flash)
                Flash.color = me.LastTry == SafeLock.Result.Wrong
                    ? new Color(1f, 0.2f, 0.15f, 0.35f * (1f - age / 0.35f))
                    : new Color(0.3f, 1f, 0.4f, 0.3f * (1f - age / 0.35f));
            Hint.text = $"Мышь — крутить диск · ЛКМ или {Controls.Label(GameAction.Interact)} — попробовать цифру · " +
                        $"{Controls.Label(GameAction.Jump)} — отойти\nОшибка громко лязгает: роботы услышат";
        }

        /// <summary>Numbers around the dial, every fifth (instances of the number prefab).</summary>
        void Build(int size)
        {
            if (m_Built == size) return;
            m_Built = size;
            foreach (var t in m_Numbers) Destroy(t.gameObject);
            m_Numbers.Clear();
            for (int n = 0; n < size; n += 5)
            {
                var t = Instantiate(NumberPrefab, Dial, false);
                float a = -n / (float)size * Mathf.PI * 2f + Mathf.PI / 2f;
                t.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * NumberRadius;
                t.rectTransform.localRotation = Quaternion.Euler(0, 0, -n / (float)size * 360f);
                t.text = n.ToString();
                m_Numbers.Add(t);
            }
        }
    }
}
