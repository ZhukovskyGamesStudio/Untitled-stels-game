using Stealth.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>A key drawn as a key cap (its name inside) or a mouse button drawn as a little mouse (element of a hint line).</summary>
    public class KeyCapView : MonoBehaviour
    {
        /// <summary>A code the hint rules use for "the mouse wheel" (not a bindable button).</summary>
        public const int WheelCode = -100;

        public Image Cap;
        public Text Key;
        public Image Mouse;
        public Sprite Lmb, Rmb, Mmb, Wheel;

        KeyBinding m_Shown = new(int.MinValue);
        int m_Version = -1;

        public void Set(KeyBinding b)
        {
            Ui.Show(gameObject, !b.IsNone);
            if (b.IsNone || (b.Equals(m_Shown) && m_Version == Loc.Version)) return;
            m_Shown = b;
            m_Version = Loc.Version;
            var mouse = b.Code == WheelCode ? Wheel : b.MouseIndex switch { 0 => Lmb, 1 => Rmb, 2 => Mmb, _ => null };
            Ui.Show(Cap, mouse == null);
            Ui.Show(Mouse, mouse != null);
            if (mouse != null) Mouse.sprite = mouse;
            else Key.text = Controls.Name(b);
        }
    }
}
