using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// A fixed label of a prefab (a button, a caption): its text is the string table's <see cref="Key"/> in the current
    /// language, refreshed when the language changes. Texts the code sets every frame do not need it.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class LocText : MonoBehaviour
    {
        public string Key;

        Text m_Text;

        void OnEnable()
        {
            Loc.Changed += Apply;
            Apply();
        }

        void OnDisable() => Loc.Changed -= Apply;

        public void Apply()
        {
            if (string.IsNullOrEmpty(Key)) return;
            if (m_Text == null) m_Text = GetComponent<Text>();
            string t = Loc.T(Key);
            if (m_Text.text != t) m_Text.text = t;
        }
    }
}
