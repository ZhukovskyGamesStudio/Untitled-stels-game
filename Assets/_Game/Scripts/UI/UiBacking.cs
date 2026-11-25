using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// A backing (a paper slip, a shade) drawn under an element that presenters show and hide on its own: it is a sibling
    /// placed before the element (a child would cover it) and comes and goes with it. With <see cref="Fit"/> it also wraps
    /// the text of this object: as wide as the words (plus <see cref="Pad"/>), wherever the text is moved.
    /// </summary>
    [ExecuteAlways]
    public class UiBacking : MonoBehaviour
    {
        public RectTransform Backing;
        [Tooltip("Wrap the Text on this object (its preferred width, the rect's height), following it when it moves.")]
        public bool Fit;
        [Tooltip("The margin round the words when fitting, in canvas units.")]
        public Vector2 Pad = new(8f, 3f);
        [Tooltip("When fitting: never narrower than this.")]
        public float MinWidth;

        Text m_Text;

        void OnEnable()
        {
            if (Backing != null && !Backing.gameObject.activeSelf) Backing.gameObject.SetActive(true);
        }

        void OnDisable()
        {
            if (Backing != null && Backing.gameObject.activeSelf) Backing.gameObject.SetActive(false);
        }

        void LateUpdate() => Refit();

        /// <summary>Wrap the words now (LateUpdate does it every frame; editor tools that render the prefab call it).</summary>
        public void Refit()
        {
            if (!Fit || Backing == null) return;
            if (m_Text == null && !TryGetComponent(out m_Text)) return;
            var rt = m_Text.rectTransform;
            var rect = rt.rect;
            bool empty = string.IsNullOrEmpty(m_Text.text);
            if (Backing.gameObject.activeSelf == empty) Backing.gameObject.SetActive(!empty);
            if (empty) return;
            float words = Mathf.Min(m_Text.preferredWidth, rect.width);
            float x = UiMath.AlignedLeft(rect.xMin, rect.width, words, m_Text.alignment);
            var centre = (Vector2)rt.localPosition + new Vector2(x + words / 2f, rect.center.y);
            var size = new Vector2(Mathf.Max(MinWidth, words + 2f * Pad.x), rect.height + 2f * Pad.y);
            // (the backing's parent is the text's: a sibling, in the same space)
            Backing.anchorMin = Backing.anchorMax = new Vector2(0.5f, 0.5f);
            Backing.pivot = new Vector2(0.5f, 0.5f);
            if ((Vector2)Backing.localPosition != centre) Backing.localPosition = new Vector3(centre.x, centre.y, 0f);
            if (Backing.sizeDelta != size) Backing.sizeDelta = size;
            if (Backing.localRotation != rt.localRotation) Backing.localRotation = rt.localRotation;
        }
    }
}
