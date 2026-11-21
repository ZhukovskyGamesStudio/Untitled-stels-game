using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>A row of the drive's panel of titles: who, their title for the house, what they did, a few numbers.</summary>
    public class StatsRowView : MonoBehaviour
    {
        public Text Name;
        public Text Title;
        public Text Did;
        public Text Numbers;
        [Tooltip("A strip in the player's team colour.")]
        public Image Bar;
    }
}
