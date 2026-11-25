using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The garage's unique feature, the car upgrade bench (prefab Props/Workbench, Tools/Blender/poi.py model_workbench):
    /// E opens its screen on this peer (UI.CarUpgradeScreen) — the car's upgrades, what each gives and the things its next
    /// level takes this run; the server raises them (Game.GameManager.CarUpgradeServer).
    /// </summary>
    public class Workbench : Interactable
    {
        /// <summary>The house's upgrade bench (only the garage has one), or null.</summary>
        public static Workbench Active { get; private set; }

        public override UseKind Kind => UseKind.Workbench;
        public override float Reach => 2.6f;
        public override float AimRadius => 1.6f; // a whole station, 4.8 m wide
        public override Vector3 Point => transform.position + Vector3.up * 1f;

        // (overrides, calling the base: a method of its own hid Interactable's, which puts it in Interactable.All — E never found it)
        protected override void OnEnable()
        {
            base.OnEnable();
            Active = this;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Active == this) Active = null;
        }

        public override string Prompt(PlayerController player, out bool available)
        {
            available = true;
            return Loc.T("use.upgrades");
        }
    }
}
