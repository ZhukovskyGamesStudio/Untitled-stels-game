using Stealth.Enemies;
using Stealth.Items;
using Stealth.Player;
using Stealth.Vehicles;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The cheats of the F4 panel that change the world (server): an item of any kind in front of a player, any car upgrade
    /// at any level. Starting a house of any kind is <see cref="StartHouseServer"/>.
    /// </summary>
    public partial class GameManager
    {
        static readonly RaycastHit[] s_CheatHits = new RaycastHit[8];

        /// <summary>Server, cheat: an item of <paramref name="shape"/> and <paramref name="color"/> appears in front of the player and falls.</summary>
        public void CheatItemServer(PlayerController player, byte shape, byte color)
        {
            if (player == null || !CheatArgs.ValidItem(shape, color)) return;
            var feet = player.transform.position;
            float yaw = player.transform.eulerAngles.y;
            var from = feet + Vector3.up * CheatArgs.Height;
            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            // not into the wall the player faces (the player's own body and teammates are no walls)
            float wall = -1f;
            int n = Physics.RaycastNonAlloc(from, dir, s_CheatHits, CheatArgs.Ahead + CheatArgs.WallGap, Perception.SightMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (s_CheatHits[i].collider.GetComponentInParent<PlayerController>() != null) continue;
                if (wall < 0f || s_CheatHits[i].distance < wall) wall = s_CheatHits[i].distance;
            }
            SpawnItemServer(shape, ItemCatalog.FreshColor(shape, color), CheatArgs.InFront(feet, yaw, wall), yaw + 90f);
        }

        /// <summary>Server, cheat: a car upgrade at any level at once (the storage robot's box follows the kennel).</summary>
        public void CheatUpgradeServer(CarUpgrade upgrade, int level)
        {
            if (!Upgrades.Set(upgrade, level)) return;
            FitStash();
        }
    }
}
