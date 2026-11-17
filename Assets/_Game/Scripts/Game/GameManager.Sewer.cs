using System.Collections.Generic;
using System.Linq;
using Stealth.Configs;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using Stealth.Vehicles;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The sewer and the car on the server: the team comes down the house's exit, opens the gate for the car, feeds loot
    /// into its engine (fuel), heaps things in its bed and drives into a tunnel to choose the next house. The drive burns
    /// the house's quota of fuel; while the next house is built every peer shows the car driving on (<see cref="DriveUntil"/>).
    /// What the team brings up from the car lies in the hatch they come in through (<see cref="Stash"/>); what is to go on
    /// must be carried to the exit's hatch (<see cref="ExitStash"/>).
    /// </summary>
    public partial class GameManager
    {
        [Tooltip("The team's pickup (networked, spawned in the sewer).")]
        public GameObject CarPrefab;

        /// <summary>What lies in the car's bed (the whole run, replicated).</summary>
        public Cargo Cargo { get; } = new();
        /// <summary>The stash in the house's exit hatch: it goes down into the car with the team.</summary>
        public TeamStash ExitStash { get; } = new();
        /// <summary>Server time until which the drive to the next house is shown on every peer, 0 = not driving. Replicated.</summary>
        public double DriveUntil { get; set; }

        public const int GateKey = 5_000_000;
        public bool GateOpen => IsOpened(GateKey);

        GameObject m_Car;
        float m_FuelNagAt, m_StrandedAt = -1f;

        // ------------------------------------------------------------------ stashes of the two hatches

        /// <summary>The stash of the hatch a player stands at (the entry or the exit of the house), null elsewhere.</summary>
        public TeamStash StashAt(Vector3 position)
        {
            var l = ActiveLevel.Layout;
            if (Phase != RunPhase.Playing || l == null) return null;
            if (AtEntry(l, position)) return Stash;
            if (Hatch.IsNear(position, l.ExitHatchPosition)) return ExitStash;
            return null;
        }

        /// <summary>At the team's way in: anywhere in the basement (a house without one: at the entry hatch).</summary>
        static bool AtEntry(LevelLayout l, Vector3 position) =>
            l.HasBasement ? Basement.Contains(l.BasementOrigin, position) : Hatch.IsNear(position, l.HatchPosition);

        /// <summary>In the basement the team arrives in.</summary>
        public static bool InBasement(Vector3 position)
        {
            var l = ActiveLevel.Layout;
            return l != null && l.HasBasement && Basement.Contains(l.BasementOrigin, position);
        }

        /// <summary>Where the stash board of the hatch near this player hangs.</summary>
        public Vector3? TradePositionFor(Vector3 position)
        {
            var l = ActiveLevel.Layout;
            if (Phase != RunPhase.Playing || l == null) return null;
            if (Hatch.IsNear(position, l.ExitHatchPosition)) return l.ExitHatchPosition;
            return l.HasBasement ? l.EntryHatch : l.HatchPosition;
        }

        /// <summary>Server, a house starts: what lies in the car's bed is brought up into the entry hatch (as much as fits).</summary>
        void UnloadCarServer()
        {
            Stash.Clear();
            ExitStash.Clear();
            var entries = Cargo.Items.Select(c => new InventoryEntry { Shape = c.Shape, Color = c.Color }).ToList();
            Stash.AddAll(entries, out var left);
            Cargo.Clear();
            var rng = new System.Random(LevelRandom.Mix(m_RunSeed, 0xCA60, Level.Index));
            foreach (var e in left)
            {
                var spot = Cargo.FreeSpot(rng);
                Cargo.Add(e.Shape, e.Color, spot.x, spot.y, rng.Next(360));
            }
        }

        /// <summary>Whether a thing dropped here burns into fuel: at the car in the sewer (its hopper), at the entry hatch in a house.</summary>
        public bool CanBurnAt(Vector3 position)
        {
            if (Phase == RunPhase.Sewer) return Car.Active != null && (Car.Active.transform.position - position).magnitude <= PlayerInventory.CarReach;
            var l = ActiveLevel.Layout;
            return Phase == RunPhase.Playing && l != null && AtEntry(l, position);
        }

        /// <summary>Server: fuel a player put in (a thing into the hopper or the entry hatch).</summary>
        public void BurnServer(PlayerController player, int fuel)
        {
            if (fuel <= 0) return;
            m_Run.Deposit(fuel);
            m_StrandedAt = -1f;
            Banner(Loc.Pack("banner.fuel_added", player != null ? player.Nick : "", fuel, Collected, Quota));
        }

        /// <summary>Server, the team comes down: the exit hatch's things go into the car; the entry hatch's stay in the house.</summary>
        int LoadCarServer()
        {
            var rng = new System.Random(LevelRandom.Mix(m_RunSeed, 0xCA61, Level.Index));
            foreach (var e in ExitStash.Items)
            {
                var spot = Cargo.FreeSpot(rng);
                Cargo.Add(e.Shape, e.Color, spot.x, spot.y, rng.Next(360));
            }
            ExitStash.Clear();
            int lost = Stash.Items.Count;
            Stash.Clear();
            return lost;
        }

        // ------------------------------------------------------------------ the car

        void SpawnCarServer()
        {
            DespawnCarServer();
            var sewer = SewerLocation.Active;
            if (CarPrefab == null || sewer == null) return;
            m_Car = Session.Spawn(CarPrefab, sewer.CarSpot.position, sewer.CarSpot.rotation);
        }

        void DespawnCarServer()
        {
            if (m_Car != null) Session.Despawn(m_Car);
            m_Car = null;
        }

        /// <summary>Server: E at a seat, the hopper, a thing in the bed or the gate's lever.</summary>
        void UseSewerServer(PlayerController player, UseKind kind, int index)
        {
            var car = Car.Active;
            switch (kind)
            {
                case UseKind.GateLever:
                    if (Phase == RunPhase.Sewer && !GateOpen) { m_Opened.Add(GateKey); OpenedVersion++; Banner(Loc.Pack("banner.gate_open")); }
                    break;
                case UseKind.CarSeat:
                    if (car != null && DriveUntil <= 0) car.SitServer(player, index);
                    break;
                case UseKind.CarHopper:
                    FeedServer(player);
                    break;
                case UseKind.CarCargo:
                    if (index >= 0 && index < Cargo.Count)
                    {
                        var item = Cargo.Items[index];
                        if (player.GetComponent<PlayerInventory>().GiveServer(item.Shape, item.Color)) Cargo.Remove(item);
                    }
                    break;
            }
        }

        /// <summary>Server: loot into the engine — the backpack's, or with an empty backpack everything in the bed.</summary>
        void FeedServer(PlayerController player)
        {
            var inventory = player.GetComponent<PlayerInventory>();
            int fuel = inventory != null ? inventory.BurnServer() : 0;
            if (fuel == 0)
                foreach (var item in Cargo.Items.ToArray())
                {
                    if (ItemCatalog.IsTool(item.Shape)) continue;
                    fuel += item.Value;
                    Cargo.Remove(item);
                }
            if (fuel <= 0) return;
            m_Run.Deposit(fuel);
            m_StrandedAt = -1f;
            Banner(Loc.Pack("banner.fuel_added", player.Nick, fuel, Collected, Quota));
        }

        /// <summary>Server: a thing dropped over the car's bed lies there (no cells, a heap). False if it was not over the bed.</summary>
        public bool DropOnCarServer(byte shape, byte color, Vector3 at, float yaw)
        {
            var car = Car.Active;
            if (car == null || Phase != RunPhase.Sewer || !car.OverBed(at, out var spot)) return false;
            Cargo.Add(shape, color, spot.x, spot.y, yaw);
            return true;
        }

        // ------------------------------------------------------------------ choosing the way and driving on

        /// <summary>
        /// Server, in the sewer: the car standing in an open tunnel chooses it — with fuel for the drive a short countdown
        /// starts, without it the HUD says how much is missing. If the team cannot get the fuel any more (the tank, the bed
        /// and the backpacks together are not enough), the run ends after a warning. The drive itself: <see cref="DriveUntil"/>.
        /// </summary>
        void UpdateSewer()
        {
            if (DriveUntil > 0)
            {
                if (Session.ServerTime >= DriveUntil) ArriveServer();
                return;
            }
            var sewer = SewerLocation.Active;
            var car = Car.Active;
            var options = RouteOptions;
            if (car != null) car.ClearSeatsServer(c => PlayerController.Find(c) != null && !PlayerController.Find(c).Downed);
            if (sewer == null || car == null || options.Count == 0) return;
            CheckStranded();
            int way = SewerBranches.WayOf(sewer.BranchAt(car.transform.position), options.Count);
            if (way < 0)
            {
                GoAt = 0;
                ChosenLane = -1;
                return;
            }
            int lane = options[way];
            if (!QuotaMet)
            {
                GoAt = 0;
                ChosenLane = -1;
                if (Time.time >= m_FuelNagAt)
                {
                    m_FuelNagAt = Time.time + 6f;
                    Banner(Loc.Pack("banner.fuel_short", Collected, Quota));
                }
                return;
            }
            if (ChosenLane != lane)
            {
                ChosenLane = lane;
                GoAt = Session.ServerTime + m_Run.Settings.VoteDelay;
                return;
            }
            if (Session.ServerTime >= GoAt) DepartServer();
        }

        void CheckStranded()
        {
            if (QuotaMet) { m_StrandedAt = -1f; return; }
            int burnable = CarHopper.BedFuel(Cargo);
            foreach (var p in PlayerController.All) burnable += CarHopper.BackpackFuel(p.GetComponent<PlayerInventory>());
            foreach (var item in WorldItem.All) if (!ItemCatalog.IsTool(item.Shape)) burnable += item.Value;
            if (RunState.CanStillGo(Collected, Quota, burnable)) { m_StrandedAt = -1f; return; }
            if (m_StrandedAt < 0f)
            {
                m_StrandedAt = Time.time;
                Banner(Loc.Pack("banner.stranded", Collected, Quota));
                return;
            }
            if (Time.time - m_StrandedAt < m_Run.Settings.StrandedGrace || !m_Run.Strand()) return;
            SyncLocation();
            Banner(Loc.Pack("banner.run_over", RunTotal));
        }

        void DepartServer()
        {
            GoAt = 0;
            if (m_Run.Advance() != RunState.Exit.NextLevel) return;
            // the team stays in its seats: the drive scene starts from their eyes in the cab (out on arrival)
            DriveUntil = Session.ServerTime + m_Run.Settings.DriveTime;
        }

        void ArriveServer()
        {
            DriveUntil = 0;
            Car.Active?.ClearSeatsServer();
            int next = Level.Index + 1, lane = ChosenLane;
            StartLevel(next, lane);
            Banner(Loc.Pack("banner.house", next + 1));
        }
    }
}
