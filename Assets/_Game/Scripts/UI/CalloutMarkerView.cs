using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>A ping or a marked robot on screen, seen through walls (clamped to the screen edge).</summary>
    public class CalloutMarkerView : MonoBehaviour
    {
        public RectTransform Rect;
        public Image Diamond;
        public Image Ring;
        public Text Label;
    }
}
