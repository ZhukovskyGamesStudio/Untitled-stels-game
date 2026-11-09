using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>A soft arc around the crosshair pointing to a direction (0 = up, clockwise).</summary>
    public class ArcView : MonoBehaviour
    {
        public RectTransform Rotator;
        public RectTransform Arc;
        public Image Image;

        public void Set(float angle, float radius, Color color)
        {
            Rotator.localEulerAngles = new Vector3(0f, 0f, -angle);
            Arc.anchoredPosition = new Vector2(0f, radius + 2f);
            Arc.sizeDelta = new Vector2(radius * 1.4f, 36f);
            if (Image.color != color) Image.color = color;
        }
    }
}
