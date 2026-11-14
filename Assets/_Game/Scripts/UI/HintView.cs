using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>One line of the hint stack in the bottom right corner: key caps and a short word, quiet and half transparent.</summary>
    public class HintView : MonoBehaviour
    {
        public CanvasGroup Group;
        public KeyCapView First, Second;
        public Text Label;

        public void Set(in Hint h, float alpha)
        {
            First.Set(h.Key);
            Second.Set(h.Key2);
            if (Label.text != h.Text) Label.text = h.Text;
            float a = h.Dim ? alpha * 0.55f : alpha;
            if (!Mathf.Approximately(Group.alpha, a)) Group.alpha = a;
        }
    }
}
