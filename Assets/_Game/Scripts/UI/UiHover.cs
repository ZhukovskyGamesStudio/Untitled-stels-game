using UnityEngine;
using UnityEngine.EventSystems;

namespace Stealth.UI
{
    /// <summary>
    /// A control that answers the mouse: grows a little while the pointer is over it and dips while pressed (the button's
    /// own colour tint does the rest). Purely visual; unscaled time, so it works on the pause menu too.
    /// </summary>
    public class UiHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("Scale while the pointer is over the control.")]
        public float HoverScale = 1.15f;
        [Tooltip("Scale while it is pressed.")]
        public float PressScale = 0.92f;

        bool m_Over, m_Down;

        public void OnPointerEnter(PointerEventData e) => m_Over = true;
        public void OnPointerExit(PointerEventData e) { m_Over = false; m_Down = false; }
        public void OnPointerDown(PointerEventData e) => m_Down = true;
        public void OnPointerUp(PointerEventData e) => m_Down = false;

        void OnDisable()
        {
            m_Over = m_Down = false;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            float target = m_Down ? PressScale : m_Over ? HoverScale : 1f;
            float s = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 3f);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
