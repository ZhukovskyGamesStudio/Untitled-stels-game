using System.Linq;
using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The house's main job on the server (<see cref="HouseFeatures.PlanObjective"/>), optional: the vault — its treasure,
    /// its three lock panels (solved by players, each reported once), its door opening when all three are; the hostage —
    /// the guard who carries the cage's key, the cage opened with it and the reward left behind. Nothing of it counts
    /// towards the house's quota.
    /// </summary>
    public partial class GameManager
    {
        public static int ConsoleKey(int console) => 5_000_000 + console;
        public static int CageOpenKey(int prop) => 6_000_000 + prop;

        ObjectiveSettings Objectives => Tuning.Items.Objectives;

        /// <summary>How many of the vault's lock panels the team has solved (0 when the house has no vault).</summary>
        public int VaultSolved
        {
            get
            {
                var l = ActiveLevel.Layout;
                if (l == null || l.Objective != ObjectiveKind.Vault) return 0;
                int n = 0;
                for (int i = 0; i < l.VaultConsoles.Count; i++) if (IsOpened(ConsoleKey(i))) n++;
                return n;
            }
        }

        /// <summary>The job is done: the vault opened, the hostage freed.</summary>
        public bool ObjectiveDone
        {
            get
            {
                var l = ActiveLevel.Layout;
                if (l == null) return false;
                return l.Objective switch
                {
                    ObjectiveKind.Vault => l.VaultDoor >= 0 && IsOpened(DoorKey(l.VaultDoor)),
                    ObjectiveKind.Hostage => l.Cage >= 0 && IsOpened(CageOpenKey(l.Cage)),
                    _ => false,
                };
            }
        }

        void HookObjective()
        {
            VaultConsole.IsSolved = i => IsOpened(ConsoleKey(i));
            VaultPanel.Solved = () => VaultSolved;
            Cage.IsFreed = i => IsOpened(CageOpenKey(i));
        }

        /// <summary>Server: the vault's treasure (not part of the quota: the job is optional); the cage's key if no robot carries it.</summary>
        void SpawnObjective(LevelLayout l)
        {
            if (l.Objective == ObjectiveKind.Hostage && l.KeyCarrier < 0)
                SpawnItemServer(ItemCatalog.CageKey, 4, l.CageKeySpot + Vector3.up * 0.05f, 0f); // no robot to carry it
            if (l.Objective != ObjectiveKind.Vault) return;
            var s = Objectives;
            var rng = new System.Random(LevelRandom.Mix(l.Seed, 0x0B7E, 2));
            for (int i = 0; i < Mathf.Min(s.VaultTreasure, l.VaultTreasure.Count); i++)
            {
                byte shape = (byte)rng.Next(ItemCatalog.LootShapes);
                byte color = (byte)Mathf.Clamp(s.TreasureColors[rng.Next(s.TreasureColors.Length)], 0, ItemCatalog.Colors.Length - 1);
                SpawnItemServer(shape, color, l.VaultTreasure[i] + Vector3.up * 0.05f, rng.Next(4) * 90f);
            }
        }

        /// <summary>Server: the guard spawned as <see cref="LevelLayout.KeyCarrier"/> carries the cage's key on his back.</summary>
        void GiveCageKey(LevelLayout l, int spawn, GameObject robot)
        {
            if (l.Objective != ObjectiveKind.Hostage || spawn != l.KeyCarrier || robot == null) return;
            if (robot.TryGetComponent<RobotController>(out var rc)) rc.CarryServer(ItemCatalog.CageKey, 4);
        }

        /// <summary>Server: a player solved a lock panel of the vault; all three — its door opens.</summary>
        void SolveConsoleServer(PlayerController player, int console)
        {
            var l = ActiveLevel.Layout;
            if (Phase != RunPhase.Playing || l == null || l.Objective != ObjectiveKind.Vault || console < 0 || console >= l.VaultConsoles.Count
                || IsOpened(ConsoleKey(console))) return;
            m_Opened.Add(ConsoleKey(console));
            OpenedVersion++;
            int solved = VaultSolved;
            if (solved < l.VaultConsoles.Count)
            {
                Banner(Loc.Pack("banner.vault_lock", player.Nick, solved, l.VaultConsoles.Count));
                return;
            }
            m_Opened.Add(DoorKey(l.VaultDoor));
            OpenedVersion++;
            Banner(Loc.Pack("banner.vault_open"));
        }

        /// <summary>Server: a player with the cage's key opens the cage; the hostage slips away and leaves the reward.</summary>
        void FreeHostageServer(PlayerController player, int prop)
        {
            var l = ActiveLevel.Layout;
            if (Phase != RunPhase.Playing || l == null || l.Objective != ObjectiveKind.Hostage || prop != l.Cage || IsOpened(CageOpenKey(prop))) return;
            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null || !inventory.TakeServer(ItemCatalog.CageKey)) return;
            m_Opened.Add(CageOpenKey(prop));
            OpenedVersion++;
            var cage = Interactable.All.FirstOrDefault(i => i.Kind == UseKind.Cage && i.Index == prop);
            var at = cage != null ? cage.transform.position + cage.transform.forward * (HouseFeatures.CageSize / 2f + 0.5f) : l.Props[prop].Position;
            byte color = (byte)Mathf.Clamp(Objectives.HostageRewardColor, 0, ItemCatalog.Colors.Length - 1);
            SpawnItemServer(ItemCatalog.Diamond, color, at + Vector3.up * 0.05f, 0f);
            Banner(Loc.Pack("banner.hostage_free", player.Nick));
        }
    }
}
