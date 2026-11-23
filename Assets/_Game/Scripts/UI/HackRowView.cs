using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>A row of the hacking terminal's list (GameHUD): the robot's kind icon, its name, its state, the hold's progress.</summary>
    public class HackRowView : MonoBehaviour
    {
        public Image Back;
        public Image Icon;
        public Image Fill;
        public Text Name;
        public Text State;
    }
}
