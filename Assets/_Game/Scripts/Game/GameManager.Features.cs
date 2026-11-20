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

        /// <summary>Server: the hand dryers running and until when (server time).</summary>
        readonly Dictionary<int, double> m_Dryers = new();

        /// <summary>Server time until which every robot and camera is stunned (the stun cube), 0 = never. Replicated.</summary>
        public double StunnedUntil { get; private set; }
        public void ApplyStun(double until) => StunnedUntil = until;

        GadgetSettings Gadgets => Tuning.Items.Gadgets;


        /// <summary>What a crate holds (the same on every call: from the house's seed and the crate).</summary>
        public static List<(byte shape, byte color)> CrateContents(LevelLayout l, int prop, GadgetSettings g, LootSettings loot)
        {
            var rng = new System.Random(LevelRandom.Mix(l.Seed, Salt.CrateContents, prop));
            int count = rng.Next(g.CrateItems.x, g.CrateItems.y + 1);
            int bonus = l.LevelIndex * loot.BonusPerHouse + g.CrateBonus;
            var list = new List<(byte, byte)>();
            for (int i = 0; i < count; i++)
            {
                // only what lies inside the crate (a shovel is longer than the crate: drawn again)
                byte shape = ItemCatalog.LootShape(rng);
                for (int tries = 0; tries < 8 && ItemCatalog.ModelSize(shape).x > SearchCrate.InsideLength; tries++) shape = ItemCatalog.LootShape(rng);
                if (ItemCatalog.ModelSize(shape).x > SearchCrate.InsideLength) shape = ItemCatalog.Battery;
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
            switch (kind)
            {
                // worked at with the hands, but not Interactables: their handlers check reach themselves
                case UseKind.DoorLock: UnlockDoorServer(player, index); return;
                case UseKind.Safe: OpenSafeServer(player, index); return;
                case UseKind.ExitDown: ExitDownServer(player); return;
                case UseKind.StoragePart:
                    var (prop, part) = OpenKeys.StoragePartOf(index);
                    ToggleStorageServer(player, prop, part);
                    return;
            }
            var thing = Interactable.All.FirstOrDefault(i => i.Kind == kind && i.Index == index);
            if (thing == null) return;
            if (!ServerReach.WithinFlat(player.transform.position, thing.Point, thing.Reach)) return;
            if (!Perception.ReachClear(player.Hands, thing.Point, thing.ReachRoot)) return; // not through a wall
            switch (kind)
            {
                case UseKind.Crate: SearchCrateServer(index, thing.transform); break;
                case UseKind.MedTable: HealServer(player); break;
                case UseKind.Workbench: CraftServer(player); break;
                case UseKind.KeyDoor: UnlockWithKeyServer(player, index); break;
                case UseKind.VaultConsole: SolveConsoleServer(player, index); break;
                case UseKind.VaultDoor: OpenVaultServer(index); break;
                case UseKind.Cage: FreeHostageServer(player, index); break;
                case UseKind.HandDryer: RunDryerServer(player, index, thing.Point); break;
                case UseKind.LightSwitch: if (thing is LightSwitch s) SwitchLightsServer(s.Room); break;
                case UseKind.LiftCall: LiftServer(player, index, false); break;
                case UseKind.LiftFloor: LiftServer(player, index, true); break;
                case UseKind.VentGrille:
                    if (Phase == RunPhase.Playing && m_Opened.Add(OpenKeys.Crawl(index))) OpenedVersion++;
                    break;
                case UseKind.ShaftHatch: ToggleShaftServer(); break;
                case UseKind.VentGrate: UnscrewServer(player, index); break;
                default: UseSewerServer(player, kind, index); break;
            }
        }

        /// <summary>Server: a player with a screwdriver in hand takes the lid off a shut vent grate (for good, for everyone).</summary>
        void UnscrewServer(PlayerController player, int grate)
        {
            if (Phase != RunPhase.Playing || grate < 0 || IsOpened(OpenKeys.Vent(grate))) return;
            var inv = player.GetComponent<PlayerInventory>();
            if (inv == null || !inv.Holds(ItemCatalog.Screwdriver)) return;
            m_Opened.Add(OpenKeys.Vent(grate));
            OpenedVersion++;
            Audio.Sfx.At(Audio.Sfx.Bank != null ? Audio.Sfx.Bank.Unlock : null, player.transform.position + Vector3.up * 2f, 0.8f, Audio.Sfx.Group.Items);
        }

        /// <summary>Server: the lid over the shaft to the basement opens (or shuts) for everyone.</summary>
        void ToggleShaftServer()
        {
            if (Phase != RunPhase.Playing) return;
            if (!m_Opened.Remove(OpenKeys.Shaft)) m_Opened.Add(OpenKeys.Shaft);
            OpenedVersion++;
        }

        /// <summary>Server: a room's ceiling lights go off (or on, in a room that started dark) for everyone.</summary>
        void SwitchLightsServer(int room)
        {
            if (Phase != RunPhase.Playing || room < 0) return;
            if (!m_Opened.Remove(OpenKeys.Lights(room))) m_Opened.Add(OpenKeys.Lights(room));
            OpenedVersion++;
        }

        /// <summary>Server: a hand dryer roars for a few seconds — robots with ears around come to check (a lure on the wall).</summary>
        void RunDryerServer(PlayerController player, int prop, Vector3 at)
        {
            if (Phase != RunPhase.Playing || IsOpened(OpenKeys.Dryer(prop))) return;
            m_Opened.Add(OpenKeys.Dryer(prop));
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
                m_Opened.Remove(OpenKeys.Dryer(prop));
                OpenedVersion++;
            }
        }

        void SearchCrateServer(int prop, Transform crate)
        {
            var l = ActiveLevel.Layout;
            if (Phase != RunPhase.Playing || l == null || IsOpened(OpenKeys.Crate(prop))) return;
            m_Opened.Add(OpenKeys.Crate(prop));
            OpenedVersion++;
            var items = CrateContents(l, prop, Gadgets, Tuning.Level.Loot);
            // inside the crate, on its floor: rows front to back, layers up (lengthwise, along the crate)
            for (int i = 0; i < items.Count; i++)
            {
                var local = SearchCrate.SlotInside(i);
                var at = crate.TransformPoint(local);
                SpawnItemServer(items[i].shape, items[i].color, at, crate.eulerAngles.y);
            }
        }

        void HealServer(PlayerController player)
        {
            if (Phase != RunPhase.Playing || IsOpened(OpenKeys.Med(player.ClientId)) || player.Hp >= Gadgets.MedTableHp) return;
            m_Opened.Add(OpenKeys.Med(player.ClientId));
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
            if (Phase != RunPhase.Playing || l == null || IsOpened(OpenKeys.Door(door)) || !l.KeyDoors.Any(k => k.Door == door)) return;
            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null || !inventory.TakeHeldServer(ItemCatalog.Key)) return;
            m_Opened.Add(OpenKeys.Door(door));
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
