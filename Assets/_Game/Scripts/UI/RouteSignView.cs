using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>A panel over a sewer tunnel (world-space canvas): which house it leads to, who stands in it, the countdown.</summary>
    public class RouteSignView : MonoBehaviour
    {
        public CanvasGroup Group;
        public Text Title;
        public Text Body;
        public Text Votes;
        public Image Frame;
    }
}
