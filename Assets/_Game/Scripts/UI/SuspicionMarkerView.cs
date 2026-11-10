using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// Marker over a robot / device (UI/Elements/SuspicionMarker): a suspicion bar that fills while it is noticing you,
    /// and a "?" / "!" icon above it.
    /// </summary>
    public class SuspicionMarkerView : MonoBehaviour
    {
        public RectTransform Root;
        public BarView Bar;
        public Text Icon;

        public void Show(Vector2 position, float scale, bool bar, float fill, Color barColor, string icon, Color iconColor)
        {
            Root.anchoredPosition = position;
            var s = Vector3.one * scale;
            if (Root.localScale != s) Root.localScale = s;
            Ui.Show(Bar, bar);
            if (bar) Bar.Set(fill, barColor);
            Ui.Show(Icon, !string.IsNullOrEmpty(icon));
            if (!string.IsNullOrEmpty(icon)) Ui.Set(Icon, icon, iconColor);
        }
    }
}
