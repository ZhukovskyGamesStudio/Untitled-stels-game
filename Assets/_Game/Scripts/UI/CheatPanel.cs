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

        /// <summary>
        /// The toggles of the panel onto the player (owner side): whatever differs is asked for again. The panel lives in
        /// the scene, the player object does not — after a new session or a reconnect a fresh player came without the
        /// cheats while the panel still showed them on, and they had to be switched off and on to work.
        /// </summary>
        public static void Sync(PlayerController me, bool invulnerable, bool invisible)
        {
            if (me == null) return;
            if (me.Invulnerable != invulnerable) me.RequestCheat(Cheat.Invulnerable, invulnerable);
            if (me.Invisible != invisible) me.RequestCheat(Cheat.Invisible, invisible);
        }

        void Update()
        {
            if (Me == null) { Show(false); return; }
            Sync(Me, m_Invulnerable, m_Invisible);
            if (!GameMenu.IsOpen && Controls.Pressed(GameAction.Cheats)) Show(!IsOpen);
            if (GameMenu.IsOpen && IsOpen) Show(false);
            if (!IsOpen) return;
            Label(Invulnerable, Loc.F("cheats.invulnerable", Loc.T(m_Invulnerable ? "cheats.on" : "cheats.off")));
            Label(Invisible, Loc.F("cheats.invisible", Loc.T(m_Invisible ? "cheats.on" : "cheats.off")));
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
