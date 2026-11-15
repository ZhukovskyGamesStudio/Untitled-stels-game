using System.Collections.Generic;
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
    /// The special places of a house on the server (<see cref="HouseFeatures"/>): crates, key doors, the med table, the
    /// workbench, the stun cubes of the robot programming console, lure speakers and the wardens' posts.
    /// </summary>
    public partial class GameManager
    {
        public static int CrateKey(int prop) => 3_000_000 + prop;
        public static int MedKey(ulong client) => 4_000_000 + (int)(client % 1_000_000);
        public static int DryerKey(int prop) => 8_000_000 + prop;

        /// <summary>Server: the hand dryers running and until when (server time).</summary>
        readonly Dictionary<int, double> m_Dryers = new();

        /// <summary>Server time until which every robot and camera is stunned (the stun cube), 0 = never. Replicated.</summary>
        public double StunnedUntil { get; private set; }
        public void ApplyStun(double until) => StunnedUntil = until;

        GadgetSettings Gadgets => Tuning.Items.Gadgets;

        void HookFeatures()
        {
            SearchCrate.IsSearched = i => IsOpened(CrateKey(i));
            MedTable.UsedBy = c => IsOpened(MedKey(c));
            HandDryer.IsRunning = i => IsOpened(DryerKey(i));
            SewerGate.IsOpen = () => GateOpen;
            HookObjective();
        }

        /// <summary>What a crate holds (the same on every call: from the house's seed and the crate).</summary>
        public static List<(byte shape, byte color)> CrateContents(LevelLayout l, int prop, GadgetSettings g, LootSettings loot)
        {
            var rng = new System.Random(LevelRandom.Mix(l.Seed, 0xC7A8, prop));
            int count = rng.Next(g.CrateItems.x, g.CrateItems.y + 1);
            int bonus = l.LevelIndex * loot.BonusPerHouse + g.CrateBonus;
            var list = new List<(byte, byte)>();
            for (int i = 0; i < count; i++)
            {
                byte shape = ItemCatalog.LootShape(rng);
                list.Add((shape, ItemCatalog.FreshColor(shape, ItemCatalog.RandomColor(rng, bonus))));
            }
            return list;
        }

        /// <summary>Server: the tools of the house at the start — keys, lures, the stun cubes (one per player); the wardens' posts.
        /// Returns the value hidden in crates (it counts towards the house's loot).</summary>
        int SpawnFeatures(LevelLayout l)
        {
            foreach (var k in l.KeyDoors) SpawnItemServer(ItemCatalog.Key, 4, k.Key + Vector3.up * 0.02f, 0f);
            bool gadgets = Tuning.Level.Features.Gadgets;
            if (gadgets)
                for (int i = 0; i < l.LureSpots.Count; i++) SpawnItemServer(ItemCatalog.Speaker, 1, l.LureSpots[i] + Vector3.up * 0.02f, i * 70f);
            if (gadgets && l.PointOfInterest >= 0 && l.Props[l.PointOfInterest].Kind == PropKind.Programmer)
            {
                var p = l.Props[l.PointOfInterest];
                var rot = Quaternion.Euler(0f, p.RotationY, 0f);
                int cubes = Mathf.Clamp(TeamSize, 1, 4);
                float scale = p.Scale > 0f ? p.Scale : 1f;
                for (int i = 0; i < cubes; i++)
                {
                    // the console's shelf (Tools/Blender/poi.py): four slots in a row along its X
                    var slot = new Vector3((i - 1.5f) * 0.47f, 1.0f, -0.215f);
                    SpawnItemServer(ItemCatalog.Stun, 2, p.Position + rot * (slot * scale), p.RotationY);
                }
            }
            int value = 0;
            foreach (int c in l.Crates)
                foreach (var (shape, color) in CrateContents(l, c, Gadgets, Tuning.Level.Loot)) value += ItemCatalog.Value(shape, color);
            return value;
        }

        /// <summary>Server: the wardens just spawned get their posts (in the order of the layout).</summary>
        void PostWardens(LevelLayout l, List<GameObject> wardens)
        {
            for (int i = 0; i < wardens.Count && i < l.Wardens.Count; i++)
                if (wardens[i] != null && wardens[i].TryGetComponent<RobotController>(out var rc))
                    rc.SetPost(l.Wardens[i].A, l.Wardens[i].B, l.Wardens[i].Facing);
        }

        // ------------------------------------------------------------------ using things

        /// <summary>Server: a player used something with E (checked again here: in reach, allowed now).</summary>
        public void UseServer(PlayerController player, UseKind kind, int index)
        {
            if (player == null || player.Downed) return;
            var thing = Interactable.All.FirstOrDefault(i => i.Kind == kind && i.Index == index);
            if (thing == null) return;
            var flat = thing.Point - player.transform.position;
            flat.y = 0f;
            if (flat.magnitude > thing.Reach + 1.5f) return;
            switch (kind)
            {
                case UseKind.Crate: SearchCrateServer(index, thing.transform); break;
                case UseKind.MedTable: HealServer(player); break;
                case UseKind.Workbench: CraftServer(player); break;
                case UseKind.KeyDoor: UnlockWithKeyServer(player, index); break;
                case UseKind.VaultConsole: SolveConsoleServer(player, index); break;
                case UseKind.Cage: FreeHostageServer(player, index); break;
                case UseKind.HandDryer: RunDryerServer(player, index, thing.Point); break;
                default: UseSewerServer(player, kind, index); break;
            }
        }

        /// <summary>Server: a hand dryer roars for a few seconds — robots with ears around come to check (a lure on the wall).</summary>
        void RunDryerServer(PlayerController player, int prop, Vector3 at)
        {
            if (Phase != RunPhase.Playing || IsOpened(DryerKey(prop))) return;
            m_Opened.Add(DryerKey(prop));
            OpenedVersion++;
            m_Dryers[prop] = Session.ServerTime + Gadgets.DryerTime;
            NoiseSystem.EmitServer(at, Gadgets.DryerNoise, NoiseSystem.NoSource); // the dryer's noise, not the player's
        }

        /// <summary>Server: dryers whose time is up go quiet (and can be switched on again).</summary>
        void UpdateDryers()
        {
            if (m_Dryers.Count == 0) return;
            foreach (var (prop, until) in m_Dryers.ToArray())
            {
                if (Session.ServerTime < until) continue;
                m_Dryers.Remove(prop);
                m_Opened.Remove(DryerKey(prop));
                OpenedVersion++;
            }
        }

        void SearchCrateServer(int prop, Transform crate)
        {
            var l = ActiveLevel.Layout;
            if (Phase != RunPhase.Playing || l == null || IsOpened(CrateKey(prop))) return;
            m_Opened.Add(CrateKey(prop));
            OpenedVersion++;
            var items = CrateContents(l, prop, Gadgets, Tuning.Level.Loot);
            for (int i = 0; i < items.Count; i++)
            {
                var at = crate.position + crate.forward * 0.75f + crate.right * ((i - (items.Count - 1) / 2f) * 0.32f) + Vector3.up * 0.05f;
                SpawnItemServer(items[i].shape, items[i].color, at, crate.eulerAngles.y + i * 35f);
            }
        }

        void HealServer(PlayerController player)
        {
            if (Phase != RunPhase.Playing || IsOpened(MedKey(player.ClientId)) || player.Hp >= Gadgets.MedTableHp) return;
            m_Opened.Add(MedKey(player.ClientId));
            OpenedVersion++;
            player.HealServer(Gadgets.MedTableHp);
            Banner(Loc.Pack("banner.healed", player.Nick, Gadgets.MedTableHp));
        }

        void CraftServer(PlayerController player)
        {
            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null) return;
            int made = inventory.CraftServer(Gadgets);
            if (made > 0) Banner(Loc.Pack("banner.crafted", player.Nick, made));
        }

        void UnlockWithKeyServer(PlayerController player, int door)
        {
            var l = ActiveLevel.Layout;
            if (Phase != RunPhase.Playing || l == null || IsOpened(DoorKey(door)) || !l.KeyDoors.Any(k => k.Door == door)) return;
            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null || !inventory.TakeHeldServer(ItemCatalog.Key)) return;
            m_Opened.Add(DoorKey(door));
            OpenedVersion++;
        }

        /// <summary>Server: a player sets off the stun cube in the hand — every robot and camera of the house freezes.</summary>
        public void GadgetServer(PlayerController player)
        {
            if (player == null || player.Downed || Phase != RunPhase.Playing) return;
            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null || !inventory.TakeHeldServer(ItemCatalog.Stun)) return;
            float seconds = Gadgets.StunDuration;
            foreach (var d in Detector.All.ToArray()) d.HackServer(seconds);
            StunnedUntil = Session.ServerTime + seconds;
            Banner(Loc.Pack("banner.stun", player.Nick, Mathf.RoundToInt(seconds)));
        }

        // ------------------------------------------------------------------ lure speakers

        readonly Dictionary<WorldItem, RobotController> m_LureResponders = new();
        float m_NextLureCall;

        /// <summary>
        /// Server: a playing lure speaker draws the nearest robot within reach on its floor (it goes to switch it off and
        /// then looks around); the first robot to come close enough switches it off.
        /// </summary>
        void UpdateLures()
        {
            var g = Gadgets;
            bool call = Time.time >= m_NextLureCall;
            if (call) m_NextLureCall = Time.time + 1.5f;
            foreach (var item in WorldItem.All)
            {
                if (item.Shape != ItemCatalog.Speaker || !item.IsPlaying) continue;
                var at = item.transform.position;
                RobotController nearest = null;
                float best = g.LureRange;
                foreach (var r in RobotController.Robots)
                {
                    if (r.IsHacked || !Perception.SameFloor(r.transform.position, at)) continue;
                    float d = Vector3.Distance(r.transform.position, at);
                    if (d <= g.LureSwitchOffDistance)
                    {
                        item.StopPlayingServer();
                        nearest = null;
                        break;
                    }
                    if (d < best && r.State is not (RobotState.Chase or RobotState.Alerted)) { best = d; nearest = r; }
                }
                if (!item.IsPlaying) { m_LureResponders.Remove(item); continue; }
                if (nearest == null || !call) continue;
                if (!m_LureResponders.TryGetValue(item, out var responder) || responder == null || responder.IsHacked) m_LureResponders[item] = responder = nearest;
                responder.InvestigateServer(at, false);
            }
        }
    }
}
