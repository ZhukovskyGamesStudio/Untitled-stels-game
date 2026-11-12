using Stealth.Game;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// Cheats for testing (prefab UI/HUD, F4 — <see cref="GameAction.Cheats"/>): switch off every robot, be invulnerable or
    /// invisible to the robots, fill the quota, jump to the hatch, see the whole map. The server carries them out
    /// (<see cref="PlayerController.RequestCheat"/>); the panel frees the cursor while it is open.
    /// </summary>
    public class CheatPanel : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        public static CheatPanel Instance { get; private set; }

        public GameObject Root;
        public Button KillRobots, Invulnerable, Invisible, Quota, ToHatch, RevealMap;

        bool m_Invulnerable, m_Invisible;

        void Awake()
        {
            Instance = this;
            KillRobots.onClick.AddListener(() => Me?.RequestCheat(Cheat.KillRobots, true));
            Invulnerable.onClick.AddListener(() => { m_Invulnerable = !m_Invulnerable; Me?.RequestCheat(Cheat.Invulnerable, m_Invulnerable); });
            Invisible.onClick.AddListener(() => { m_Invisible = !m_Invisible; Me?.RequestCheat(Cheat.Invisible, m_Invisible); });
            Quota.onClick.AddListener(() => Me?.RequestCheat(Cheat.FillQuota, true));
            ToHatch.onClick.AddListener(() =>
            {
                var layout = ActiveLevel.Layout;
                if (Me != null && layout != null) Me.TeleportOwner(layout.HatchPosition + new Vector3(1.5f, 0.05f, 0f), Me.transform.rotation);
            });
            RevealMap.onClick.AddListener(() => GameManager.Instance?.Exploration?.RevealAll());
            Root.SetActive(false);
        }

        static PlayerController Me => PlayerController.Local;

        void OnDestroy()
        {
            IsOpen = false;
            if (Instance == this) Instance = null;
        }

        /// <summary>Test hook (autopilot): open or close the panel.</summary>
        public void SetOpen(bool open) => Show(open);

        void Update()
        {
            if (Me == null) { Show(false); return; }
            if (!GameMenu.IsOpen && Controls.Pressed(GameAction.Cheats)) Show(!IsOpen);
            if (GameMenu.IsOpen && IsOpen) Show(false);
            if (!IsOpen) return;
            Label(Invulnerable, m_Invulnerable ? "Неуязвимость: ВКЛ" : "Неуязвимость: выкл");
            Label(Invisible, m_Invisible ? "Невидимость для роботов: ВКЛ" : "Невидимость для роботов: выкл");
        }

        void Show(bool on)
        {
            IsOpen = on;
            if (Root.activeSelf != on) Root.SetActive(on);
        }

        static void Label(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null && t.text != text) t.text = text;
        }
    }
}
