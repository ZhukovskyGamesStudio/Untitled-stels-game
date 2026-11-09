using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>A bar: background + fill stretched by a 0..1 value (fill's pivot on the left).</summary>
    public class BarView : MonoBehaviour
    {
        public RectTransform Fill;
        public Image FillImage;

        public void Set(float value, Color color)
        {
            Fill.anchorMax = new Vector2(Mathf.Clamp01(value), Fill.anchorMax.y);
            if (FillImage != null && FillImage.color != color) FillImage.color = color;
        }
    }
}
