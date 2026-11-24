using Stealth.Game;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>
    /// The car's upgrades as they look (every peer, by the replicated levels: <see cref="GameManager.Upgrades"/>): a shelf
    /// in the bed per level of the trunk (Tools/Blender/car_upgrades.py car_shelves), the storage robot's kennel from the
    /// kennel's first level, and the robot sitting in it while it rides along — whenever it is not out in a house.
    /// </summary>
    public class CarUpgradeLook : MonoBehaviour
    {
        [Tooltip("The trunk's shelves, level 1, 2 and 3.")]
        public GameObject[] Shelves;
        public GameObject Kennel;
        [Tooltip("The storage robot's model sitting in the kennel (shown while the real one is not out in a house).")]
        public GameObject KennelMule;

        void Update()
        {
            var gm = GameManager.Instance;
            int trunk = gm != null ? gm.Upgrades.Level(CarUpgrade.Trunk) : 0;
            int kennel = gm != null ? gm.Upgrades.Level(CarUpgrade.Kennel) : 0;
            for (int i = 0; Shelves != null && i < Shelves.Length; i++) Show(Shelves[i], i < trunk);
            Show(Kennel, kennel > 0);
            Show(KennelMule, CarUpgrades.MuleRides(kennel) && StorageBot.Active == null);
        }

        static void Show(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }
    }
}
