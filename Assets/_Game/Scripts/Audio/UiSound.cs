using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Stealth.Audio
{
    /// <summary>Clicks and hover ticks of menu controls (on the button / toggle / slider element prefabs).</summary>
    public class UiSound : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
    {
        public Selectable Control;

        public void OnPointerEnter(PointerEventData e)
        {
            if (Control == null || Control.interactable) Sfx.Ui(Sfx.Bank?.Hover, 0.5f);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (Control == null || Control.interactable) Sfx.Ui(Sfx.Bank?.Click);
        }
    }
}
