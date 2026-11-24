using Stealth.Game;

namespace Stealth.Player
{
    /// <summary>The garage's car upgrade bench, the owner's side: E opens its screen here (UI.CarUpgradeScreen), the server raises the upgrades.</summary>
    public partial class PlayerController
    {
        /// <summary>
        /// Owner: the car upgrade bench's screen is open (E at the garage's bench): the cursor is free, the player stands.
        /// Closes on E or Tab again, the menu, a fall, or walking away from the bench.
        /// </summary>
        public bool AtUpgrades { get; private set; }
        bool m_UpgradesOpenedNow;

        /// <summary>Owner: the bench's screen opens (its upgrades are asked of the server).</summary>
        void OpenUpgrades() => AtUpgrades = true;

        /// <summary>Owner, every frame: the screen closes when it should.</summary>
        void UpdateUpgrades(bool menu)
        {
            if (!AtUpgrades) { m_UpgradesOpenedNow = false; return; }
            var gm = GameManager.Instance;
            bool shut = menu || Downed || gm == null || !gm.AtUpgradeBench(transform.position) || SeatIndex >= 0;
            // (the press that opened it is not the press that shuts it)
            bool pressed = Controls.Pressed(GameAction.Interact);
            if (m_UpgradesOpenedNow && (pressed || Controls.Pressed(GameAction.Hands))) shut = true;
            m_UpgradesOpenedNow = true;
            if (!shut) return;
            CloseUpgrades();
            if (pressed) m_UseWaitRelease = true; // (the press that shut it does not open it again)
        }

        /// <summary>Owner: the screen's own close button, or the bench is gone.</summary>
        public void CloseUpgrades()
        {
            AtUpgrades = false;
            m_UpgradesOpenedNow = false;
        }
    }
}
