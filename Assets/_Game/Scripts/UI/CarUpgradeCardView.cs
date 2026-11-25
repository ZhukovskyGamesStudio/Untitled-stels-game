using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// One upgrade's index card on the car upgrade bench's screen (prefab UI/HUD, built by the asset builder): its picture,
    /// name and level boxes, what it gives and has given, the things of its next level (ticked when the backpack has
    /// them), the «upgrade» button — or a stamp when it is at its top, a note when it is not made yet.
    /// </summary>
    public class CarUpgradeCardView : MonoBehaviour
    {
        public Image Icon;
        public Text Name;
        [Tooltip("The level boxes (3; a unique upgrade uses the first) and the ticks in them.")]
        public Image[] Boxes;
        public Image[] Ticks;
        public Text What;
        public Text Now;
        [Tooltip("The next level's recipe: a caption, the things' pictures, and a tick or a cross on each.")]
        public GameObject Recipe;
        public Image[] Things;
        public Image[] Marks;
        public Button Raise;
        public Text RaiseLabel;
        [Tooltip("Stamped over the card at its top level.")]
        public GameObject Done;
        [Tooltip("Shown on an upgrade not made yet (instead of the recipe and the button).")]
        public GameObject Planned;
    }
}
