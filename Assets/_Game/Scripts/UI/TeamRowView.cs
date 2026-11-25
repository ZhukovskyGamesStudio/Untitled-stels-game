using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>A teammate's line on the HUD's roster card (UI/Elements/TeamRow): the name in their ink, then a state word or their hearts.</summary>
    public class TeamRowView : MonoBehaviour
    {
        public Text Name;
        [Tooltip("Down / in the hatch; empty while they are up (the hearts say how they are).")]
        public Text State;
        public Image[] Hearts;
    }
}
