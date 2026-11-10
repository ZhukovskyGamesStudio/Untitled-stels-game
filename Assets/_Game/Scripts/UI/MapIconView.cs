using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>An icon on the map: a teammate's arrow, a ping, a marked robot (with an optional label).</summary>
    public class MapIconView : MonoBehaviour
    {
        public RectTransform Rect;
        public Image Arrow;
        public Image Dot;
        public Image Diamond;
        public Text Label;
    }
}
