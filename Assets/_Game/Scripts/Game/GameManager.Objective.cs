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
        /// <summary>Stages of the job (in <see cref="IsOpened"/>, replicated): the vault seen, somebody inside it; the hostage spoken to, its key in hand.</summary>
        public const int VaultFoundKey = 9_000_001, VaultEnteredKey = 9_000_002, CageFoundKey = 9_000_003, CageKeyHeldKey = 9_000_004;

        /// <summary>How long the hostage's pointing marks the guard with the key, seconds.</summary>
        const float KeyCarrierMark = 90f;

        /// <summary>Where the job stands, for the task list (every peer: from the replicated keys and the layout).</summary>
        public UI.ObjectiveState ObjectiveStage
        {
            get
            {
                var l = ActiveLevel.Layout;
                if (l == null) return default;
                return new UI.ObjectiveState
                {
                    Kind = l.Objective,
                    Found = l.Objective == ObjectiveKind.Vault ? IsOpened(VaultFoundKey) || VaultSolved > 0 : IsOpened(CageFoundKey),
                    Solved = VaultSolved,
                    Locks = l.VaultConsoles.Count,
                    KeyHeld = IsOpened(CageKeyHeldKey),
                    Done = ObjectiveDone,
                };
            }
        }

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
                    ObjectiveKind.Vault => IsOpened(VaultEnteredKey),
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
                byte shape = ItemCatalog.LootShape(rng);
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

        /// <summary>
        /// Server, every frame of a house: the stages that are not a player's action — the vault seen (its door revealed on
        /// the team's map), somebody inside the opened vault, the cage's key in somebody's backpack (dropped again: not).
        /// </summary>
        void UpdateObjective()
        {
            var l = ActiveLevel.Layout;
            if (l == null || l.Objective == ObjectiveKind.None) return;
            if (l.Objective == ObjectiveKind.Vault)
            {
                if (!IsOpened(VaultFoundKey) && l.VaultDoorway >= 0 && Exploration != null)
                {
                    var d = l.Doorways[l.VaultDoorway].Position;
                    if (MapSketch.AnyRevealed(Exploration, LevelLayout.FloorOf(d.y), new Rect(d.x - 0.75f, d.z - 0.75f, 1.5f, 1.5f))) SetStage(VaultFoundKey, true);
                }
                if (!IsOpened(VaultEnteredKey) && l.VaultDoor >= 0 && IsOpened(DoorKey(l.VaultDoor)))
                    foreach (var p in PlayerController.All)
                        if (!p.Downed && l.Plan.RoomAt(p.transform.position + Vector3.up * 0.3f)?.Id == l.VaultRoom)
                        {
                            SetStage(VaultEnteredKey, true);
                            Banner(Loc.Pack("banner.vault_entered", p.Nick));
                            break;
                        }
                return;
            }
            bool held = false;
            foreach (var p in PlayerController.All)
                if (Cage.HasKey(p)) { held = true; break; }
            SetStage(CageKeyHeldKey, held && !ObjectiveDone);
        }

        void SetStage(int key, bool on)
        {
            if (on == IsOpened(key)) return;
            if (on) m_Opened.Add(key); else m_Opened.Remove(key);
            OpenedVersion++;
        }

        /// <summary>
        /// Server: E at the cage. With its key: free the hostage. Without: the hostage points out the guard who carries it —
        /// marked for the whole team (seen through walls) — or, if it lies about already, where it lies.
        /// </summary>
        void FreeHostageServer(PlayerController player, int prop)
        {
            var l = ActiveLevel.Layout;
            if (Phase != RunPhase.Playing || l == null || l.Objective != ObjectiveKind.Hostage || prop != l.Cage || IsOpened(CageOpenKey(prop))) return;
            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null) return;
            if (!inventory.TakeHeldServer(ItemCatalog.CageKey))
            {
                if (inventory.Has(ItemCatalog.CageKey)) return; // in the backpack: take it in hand first
                SetStage(CageFoundKey, true);
                PointOutKeyServer(player);
                return;
            }
            SetStage(CageFoundKey, true);
            m_Opened.Add(CageOpenKey(prop));
            OpenedVersion++;
            var cage = Interactable.All.FirstOrDefault(i => i.Kind == UseKind.Cage && i.Index == prop);
            var at = cage != null ? cage.transform.position + cage.transform.forward * (HouseFeatures.CageSize / 2f + 0.5f) : l.Props[prop].Position;
            byte color = (byte)Mathf.Clamp(Objectives.HostageRewardColor, 0, ItemCatalog.Colors.Length - 1);
            SpawnItemServer(ItemCatalog.Diamond, color, at + Vector3.up * 0.05f, 0f);
            Banner(Loc.Pack("banner.hostage_free", player.Nick));
        }

        /// <summary>Server: the guard with the cage's key marked for everyone (a long mark), or the key's spot pinged if it lies about.</summary>
        void PointOutKeyServer(PlayerController player)
        {
            var carrier = Detector.All.FirstOrDefault(d => d.Cargo >= 0 && Detector.DecodeCargo(d.Cargo).shape == ItemCatalog.CageKey);
            if (carrier != null)
            {
                carrier.MarkServer(Session.ServerTimeF + KeyCarrierMark);
                if (m_Link != null) m_Link.BroadcastPing(player.ClientId, PingKind.Enemy, carrier.Position, carrier.NetId);
                else ReceivePing(player.ClientId, PingKind.Enemy, carrier.Position, carrier.NetId);
                Banner(Loc.Pack("banner.hostage_points"));
                return;
            }
            var key = WorldItem.All.FirstOrDefault(i => i.Shape == ItemCatalog.CageKey);
            if (key == null) return;
            if (m_Link != null) m_Link.BroadcastPing(player.ClientId, PingKind.Thing, key.transform.position, 0);
            else ReceivePing(player.ClientId, PingKind.Thing, key.transform.position, 0);
            Banner(Loc.Pack("banner.hostage_points"));
        }
    }
}
