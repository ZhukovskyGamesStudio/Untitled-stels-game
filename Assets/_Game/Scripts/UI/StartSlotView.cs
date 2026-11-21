using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// One player's column on the run's start screen, under their figure in the row: their name, the class (its tool) and
    /// the extra item, each with arrows to change it (only the local player's own arrows work), and "ready".
    /// </summary>
    public class StartSlotView : MonoBehaviour
    {
        public RectTransform Rect;
        public Text Name;
        public Image ClassIcon;
        public Text ClassName;
        public Button ClassPrev, ClassNext;
        public Image ItemIcon;
        public Text ItemName;
        public Button ItemPrev, ItemNext;
        [Tooltip("Lit when this player is ready.")]
        public Image ReadyMark;
        public Text ReadyLabel;
    }
}
