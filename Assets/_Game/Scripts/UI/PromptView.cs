using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>The hint pill under the crosshair: [key] text and an optional hold-progress bar.</summary>
    public class PromptView : MonoBehaviour
    {
        public GameObject KeyBox;
        public Text Key;
        public Text Label;
        public BarView Progress;

        string m_Shown;

        public void Set(Prompt p)
        {
            Ui.Show(gameObject, p.IsValid);
            if (!p.IsValid) { m_Shown = null; return; }
            string id = p.Key + "|" + p.Text;
            if (id != m_Shown)
            {
                m_Shown = id;
                Ui.Show(KeyBox, !string.IsNullOrEmpty(p.Key));
                Key.text = p.Key;
                Label.text = p.Text;
            }
            Ui.Show(Progress, p.Progress >= 0f);
            if (p.Progress >= 0f) Progress.Set(p.Progress, Color.white);
        }
    }
}
