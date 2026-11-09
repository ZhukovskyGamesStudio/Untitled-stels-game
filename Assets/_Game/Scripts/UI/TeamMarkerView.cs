using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>Marker over a teammate (name, or a red cross through walls when downed).</summary>
    public class TeamMarkerView : MonoBehaviour
    {
        public RectTransform Rect;
        public GameObject Cross;
        public Image[] CrossBars;
        public Text Label;
    }
}
