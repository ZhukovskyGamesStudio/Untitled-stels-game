using System.Collections.Generic;
using Stealth.Configs;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using Stealth.Vehicles;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The car's upgrades (server-decided, replicated by the network twin): the garage's upgrade bench
    /// (<see cref="Workbench"/>) raises them one level at a time for the things of the run's recipe
    /// (<see cref="CarUpgradeServer"/>, <see cref="UpgradeBook"/>); the engine cuts every drive's fuel (<see cref="CostOf"/>),
    /// Linus's backpack grows his box (<see cref="TeamStash.SetSize"/>), the trunk's shelves
    /// and the kennel show on the car (Vehicles.CarUpgradeLook). A new run: all back to nothing.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The car's upgrades this run.</summary>
        public CarUpgrades Upgrades { get; } = new();

        static CarUpgradeSettings UpgradeSettings => Tuning.Run.Upgrades;

        /// <summary>This run's upgrade recipes (every peer makes the same from the run's seed).</summary>
        public CarUpgradeBook UpgradeBook => CarUpgradeBook.Use(Level.RunSeed, UpgradeSettings, Tuning.Items.Crafting);

        /// <summary>Peers: the server's levels; the storage robot's box follows the kennel.</summary>
        public void ApplyUpgrades(uint packed)
        {
            Upgrades.Apply(packed);
            FitStash();
        }

        /// <summary>The storage robot's box: as big as its backpack's level gives.</summary>
        void FitStash()
        {
            var size = CarUpgrades.StashSize(Upgrades.Level(CarUpgrade.Kennel), UpgradeSettings);
            Stash.SetSize(size.x, size.y);
            ExitStash.SetSize(size.x, size.y); // (what it carried down the exit hatch: the same box)
        }

        /// <summary>Whether a player at <paramref name="position"/> can work at the garage's upgrade bench.</summary>
        public bool AtUpgradeBench(Vector3 position)
        {
            var bench = Workbench.Active;
            return bench != null && Phase == RunPhase.Playing && ServerReach.WithinFlat(position, bench.Point, bench.Reach + 0.6f)
                   && Mathf.Abs(position.y - bench.Point.y) < 2.2f;
        }

        readonly List<int> m_Carried = new(), m_Take = new();

        /// <summary>
        /// Server: a player at the upgrade bench raises an upgrade one level — if it has one more, and their backpack holds
        /// every thing of its recipe (those are taken).
        /// </summary>
        public void CarUpgradeServer(PlayerController player, CarUpgrade upgrade)
        {
            if (player == null || player.Downed || !AtUpgradeBench(player.transform.position) || !Upgrades.CanRaise(upgrade)) return;
            var inv = player.GetComponent<PlayerInventory>();
            int next = Upgrades.Level(upgrade) + 1;
            var recipe = UpgradeBook.Recipe(upgrade, next);
            if (inv == null || recipe == null) return;
            inv.CarriedForRecipe(m_Carried);
            if (!CarUpgrades.Gather(m_Carried, recipe, m_Take) || !inv.TakeServer(m_Take)) return;
            Upgrades.Raise(upgrade);
            FitStash();
            if (upgrade == CarUpgrade.Teleport) FillTeleport(); // (it comes charged)
            NoteServer(player, StatKind.Pickups);
            Banner(Loc.Pack("banner.car_upgraded", player.Nick, "@" + UpgradeKey(upgrade), next));
            SoundServer(Audio.GameSound.Craft, player.transform.position + Vector3.up); // (every peer hears it)
        }

        /// <summary>The table key of an upgrade's name (<c>upgrade.&lt;name&gt;</c>; its effect: the same key + <c>.what</c>).</summary>
        public static string UpgradeKey(CarUpgrade u) => "upgrade." + u.ToString().ToLowerInvariant();

        /// <summary>Server: a new run — no upgrades, the storage robot's box back to its own size.</summary>
        void ClearUpgradesServer()
        {
            Upgrades.Clear();
            FitStash();
            FillTeleport();
            ClearRadioServer();
        }

        /// <summary>Server: Linus rides in the arriving car — he is spawned asleep in its trunk once it stops (<see cref="ReleaseMuleServer"/>).</summary>
        bool m_MuleInKennel;

        /// <summary>Server: the car stopped in the basement — Linus, asleep in its trunk, is there.</summary>
        void ReleaseMuleServer()
        {
            if (!m_MuleInKennel) return;
            m_MuleInKennel = false;
            var layout = ActiveLevel.Layout;
            if (layout != null) SpawnStorageBotServer(layout);
        }
    }
}
