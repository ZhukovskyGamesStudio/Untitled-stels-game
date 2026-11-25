using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>A line of the task note (UI/Elements/TaskRow): a checkbox, ticked once done, and the task in handwriting.</summary>
    public class TaskRowView : MonoBehaviour
    {
        public Image Box;
        public Image Tick;
        public Text Label;
    }
}
