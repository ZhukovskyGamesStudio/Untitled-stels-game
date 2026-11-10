using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>A row of the controls panel: what it does and the key (a button: click and press another key to rebind).</summary>
    public class ControlRowView : MonoBehaviour
    {
        public Text Title;
        public Button Key;
        public Text KeyLabel;
        public Image KeyBackground;
    }
}
