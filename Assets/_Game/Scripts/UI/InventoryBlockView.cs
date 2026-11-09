using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>One inventory cell of an item: dark border + coloured core.</summary>
    public class InventoryBlockView : MonoBehaviour
    {
        public RectTransform Rect;
        public Image Border;
        public Image Core;

        public void Set(Vector2 topLeft, float cell, Color color, float alpha)
        {
            Rect.anchoredPosition = topLeft;
            Rect.sizeDelta = new Vector2(cell, cell);
            var dark = color * 0.55f;
            dark.a = alpha;
            color.a = alpha;
            Border.color = dark;
            Core.color = color;
        }
    }
}
