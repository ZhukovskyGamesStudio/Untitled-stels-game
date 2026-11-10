using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>A house on the route map: a round button, what it is, who voted for it.</summary>
    public class RouteNodeView : MonoBehaviour
    {
        public RectTransform Rect;
        public Button Button;
        public Image Ring;
        public Image Fill;
        public Text Label;
        public Text Votes;
    }
}
