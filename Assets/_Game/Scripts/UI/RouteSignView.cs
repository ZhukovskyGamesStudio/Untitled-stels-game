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
        [Tooltip("What the house is (office, hospital, garage…) and its main job (the vault, the hostage).")]
        public Image KindIcon, JobIcon;
        [Tooltip("One per HouseKind, in its order.")]
        public Sprite[] KindIcons;
        public Sprite VaultIcon, HostageIcon;

        /// <summary>Shows the house's kind and its main job (none: no icon).</summary>
        public void ShowIcons(Stealth.Level.HouseKind kind, Stealth.Level.ObjectiveKind job)
        {
            int k = (int)kind;
            if (KindIcon != null)
            {
                var s = KindIcons != null && k < KindIcons.Length ? KindIcons[k] : null;
                if (KindIcon.sprite != s) KindIcon.sprite = s;
                KindIcon.enabled = s != null;
            }
            if (JobIcon != null)
            {
                var s = job == Stealth.Level.ObjectiveKind.Vault ? VaultIcon : job == Stealth.Level.ObjectiveKind.Hostage ? HostageIcon : null;
                if (JobIcon.sprite != s) JobIcon.sprite = s;
                JobIcon.enabled = s != null;
            }
        }
    }
}
