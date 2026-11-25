using Stealth.Game;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>
    /// The car's upgrades as they look (every peer, by the replicated levels: <see cref="GameManager.Upgrades"/>): the
    /// shredder on the hood grows with the engine (Tools/Blender/car_upgrades.py car_shredder: a riveted funnel, an extra
    /// motor, a heavy housing with stacks), a shelf in the bed per level of the trunk (car_shelves), the red panic button on the glovebox once the teleport is fitted (car_panic), and Linus asleep in the
    /// trunk whenever he is not out in a house — from the run's very start.
    /// </summary>
    public class CarUpgradeLook : MonoBehaviour
    {
        [Tooltip("The trunk's shelves, level 1, 2 and 3.")]
        public GameObject[] Shelves;
        [Tooltip("What the engine adds to the shredder, level 1, 2 and 3 (each shown from its level up).")]
        public GameObject[] Shredder;
        [Tooltip("The teleport's red button on the glovebox.")]
        public GameObject PanicButton;
        [Tooltip("The car radio in the dash (its songs: CarRadio).")]
        public GameObject Radio;
        [Tooltip("Linus's model curled up asleep in the trunk where the real one sleeps (shown while the real one is not out in a house).")]
        public GameObject KennelMule;

        void Update()
        {
            var gm = GameManager.Instance;
            var up = gm != null ? gm.Upgrades : null;
            int trunk = up != null ? up.Level(CarUpgrade.Trunk) : 0;
            int engine = up != null ? up.Level(CarUpgrade.Engine) : 0;
            for (int i = 0; Shelves != null && i < Shelves.Length; i++) Show(Shelves[i], i < trunk);
            for (int i = 0; Shredder != null && i < Shredder.Length; i++) Show(Shredder[i], i < engine);
            Show(PanicButton, up != null && up.Level(CarUpgrade.Teleport) > 0);
            Show(Radio, up != null && up.Level(CarUpgrade.Radio) > 0);
            Show(KennelMule, StorageBot.Active == null);
        }

        static void Show(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }
    }
}
