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
        [Tooltip("The team's storage robot (StorageBot): the stash rides in it.")]
        public GameObject StorageBotPrefab;

        /// <summary>What lies in the car's bed (the whole run, replicated).</summary>
        public Cargo Cargo { get; } = new();
        /// <summary>The car's shredder: things thrown into its hopper heap up and are torn up a few at a time into fuel (replicated).</summary>
        public Shredder Shredder { get; } = new();
        readonly List<(byte shape, byte color)> m_Chewed = new();
        int m_ChewFuel;
        string m_ChewBy = "";

        /// <summary>Within reach of the car's hopper (a thing let go there goes into the shredder).</summary>
        public bool AtCar(Vector3 position) => Car.Active != null && (Car.Active.transform.position - position).magnitude <= PlayerInventory.CarReach;

        /// <summary>Server: a thing thrown into the car's hopper: onto the shredder's pile — its fuel comes once it is chewed.</summary>
        public void GrindServer(PlayerController player, byte shape, byte color)
        {
            if (ItemCatalog.Unburnable(shape)) return;
            Shredder.Add(shape, color, Session.ServerTime);
            if (player != null) m_ChewBy = player.Nick;
        }

        /// <summary>Server: what the blades have finished becomes fuel at once; the banner when the pile is gone.</summary>
        void UpdateShredderServer()
        {
            m_Chewed.Clear();
            if (Shredder.Tick(Session.ServerTime, m_Chewed) == 0) return;
            int fuel = 0;
            foreach (var (shape, color) in m_Chewed) fuel += ItemCatalog.Value(shape, color);
            if (fuel > 0)
            {
                m_Run.Deposit(fuel);
                m_StrandedAt = -1f;
                m_ChewFuel += fuel;
            }
            if (Shredder.Busy || m_ChewFuel <= 0) return;
            Banner(Loc.Pack("banner.fuel_added", m_ChewBy, m_ChewFuel, Collected, FuelNeeded));
            m_ChewFuel = 0;
        }
        /// <summary>The stash in the house's exit hatch: it goes down into the car with the team.</summary>
        public TeamStash ExitStash { get; } = new();
        /// <summary>Server time until which the drive to the next house is shown on every peer, 0 = not driving. Replicated.</summary>
        public double DriveUntil { get; set; }

        public bool GateOpen => IsOpened(OpenKeys.Gate);

        GameObject m_Car;
        float m_FuelNagAt, m_StrandedAt = -1f;

        // ------------------------------------------------------------------ stashes of the two hatches

        /// <summary>The stash of the hatch a player stands at (the entry or the exit of the house), null elsewhere.</summary>
        public TeamStash StashAt(Vector3 position)
        {
            var l = ActiveLevel.Layout;
            if (Phase != RunPhase.Playing || l == null) return null;
            if (AtEntry(l, position)) return Stash;
            if (AtBot(position)) return Stash;
            if (Hatch.IsNear(position, l.ExitHatchPosition)) return ExitStash;
            return null;
        }

        /// <summary>At the team's way in: down in the basement, while there is no storage robot (with one, the stash rides in it).</summary>
        static bool AtEntry(LevelLayout l, Vector3 position) => StorageBot.Active == null && Basement.Contains(l.Cellar, position);

        /// <summary>Next to the storage robot (its stash in reach).</summary>
        static bool AtBot(Vector3 position)
        {
            var bot = StorageBot.Active;
            return bot != null && StorageBotRules.InReach(bot.Mode, bot.transform.position, position);
        }

        // ------------------------------------------------------------------ the storage robot

        StorageBot m_Bot;

        /// <summary>Server, a house starts: the storage robot waits in the basement (with what the car brought).</summary>
        void SpawnStorageBotServer(LevelLayout l)
        {
            if (StorageBotPrefab == null) return;
            var at = Basement.StashSpot(l.Cellar);
            var go = Session.Spawn(StorageBotPrefab, at, Quaternion.LookRotation(Vector3.left));
            m_Spawned.Add(go);
            m_Bot = go.GetComponent<StorageBot>();
            m_Bot?.BeginServer();
        }

        /// <summary>Server: a player's command to the storage robot — 0: come to me; 1: go to the exit through the ducts.</summary>
        public void BotCommandServer(PlayerController player, byte command)
        {
            if (player == null || Phase != RunPhase.Playing || m_Bot == null) return;
            var l = ActiveLevel.Layout;
            if (command == 0)
            {
                var answer = m_Bot.CallServer(player.transform.position, l);
                if (answer != StorageBot.Answer.Busy) Banner(Loc.Pack(answer == StorageBot.Answer.Coming ? "banner.bot_coming" : "banner.bot_no_way"));
                return;
            }
            bool seen = Exploration != null && l != null && Exploration.IsRevealedAt(l.ExitHatchPosition + Vector3.up * 0.5f);
            if (!StorageBotRules.CanSendToExit(m_Bot.Mode, seen)) { if (!seen) Banner(Loc.Pack("banner.bot_exit_unknown")); return; }
            Banner(Loc.Pack(m_Bot.SendToExitServer(l) == StorageBot.Answer.Coming ? "banner.bot_exit" : "banner.bot_no_way"));
        }

        /// <summary>Server: the storage robot reached the grate over the caller but a watcher is in the room: it waits up there.</summary>
        public void StorageBotWaitsServer() => Banner(Loc.Pack("banner.bot_unsafe"));

        /// <summary>Server: the lid over the shaft is opened (the storage robot pushes it open on its way through).</summary>
        public void OpenShaftServer()
        {
            if (Phase != RunPhase.Playing || m_Opened.Contains(OpenKeys.Shaft)) return;
            m_Opened.Add(OpenKeys.Shaft);
            OpenedVersion++;
        }

        /// <summary>
        /// Server: the storage robot went down the exit hatch: what it carried goes to the exit's stash (it leaves with the
        /// car); what does not fit stays in it — and is lost with the house.
        /// </summary>
        public void StorageBotLeftServer(StorageBot bot)
        {
            var entries = new List<InventoryEntry>(Stash.Items);
            ExitStash.AddAll(entries, out var left);
            Stash.Clear();
            Stash.AddAll(left, out _);
            Banner(Loc.Pack("banner.bot_left"));
            if (bot != null) { m_Spawned.Remove(bot.gameObject); Session.Despawn(bot.gameObject); }
            m_Bot = null;
        }

        /// <summary>In the basement the team arrives in.</summary>
        public static bool InBasement(Vector3 position)
        {
            var l = ActiveLevel.Layout;
            return l != null && Basement.Contains(l.Cellar, position);
        }

        /// <summary>Where the stash board of the hatch near this player hangs.</summary>
        public Vector3? TradePositionFor(Vector3 position)
        {
            var l = ActiveLevel.Layout;
            if (Phase != RunPhase.Playing || l == null) return null;
            if (Hatch.IsNear(position, l.ExitHatchPosition)) return l.ExitHatchPosition;
            var bot = StorageBot.Active;
            if (bot != null) return bot.transform.position; // the board hangs over the robot
            return l.EntryHatch;
        }

        /// <summary>Server, a house starts: what lies in the car's bed is brought up into the entry hatch (as much as fits).</summary>
        void UnloadCarServer()
        {
            Stash.Clear();
            ExitStash.Clear();
            var entries = Cargo.Items.Select(c => new InventoryEntry { Shape = c.Shape, Color = c.Color }).ToList();
            Stash.AddAll(entries, out var left);
            Cargo.Clear();
            var rng = new System.Random(LevelRandom.Mix(m_RunSeed, Salt.CarUnload, Level.Index));
            foreach (var e in left)
            {
                var spot = Cargo.FreeSpot(rng);
                Cargo.Add(e.Shape, e.Color, spot.x, spot.y, rng.Next(360));
            }
        }

        /// <summary>Whether a thing dropped here burns into fuel: at the car (its hopper, the sewer or the basement) or at the entry in a house — never at the storage robot.</summary>
        public bool CanBurnAt(Vector3 position)
        {
            // at the car (its hopper): in the sewer and in the house's basement alike
            bool atCar = Car.Active != null && (Car.Active.transform.position - position).magnitude <= PlayerInventory.CarReach;
            if (Phase == RunPhase.Sewer || atCar) return atCar && Phase is RunPhase.Sewer or RunPhase.Playing;
            var l = ActiveLevel.Layout;
            return Phase == RunPhase.Playing && l != null && AtEntry(l, position); // (not at the storage robot: its crate is a stash, no fire)
        }

        /// <summary>Server: fuel a player put in (a thing into the hopper or the entry hatch).</summary>
        public void BurnServer(PlayerController player, int fuel)
        {
            if (fuel <= 0) return;
            m_Run.Deposit(fuel);
            m_StrandedAt = -1f;
            Banner(Loc.Pack("banner.fuel_added", player != null ? player.Nick : "", fuel, Collected, FuelNeeded));
        }

        /// <summary>Server, the team comes down: the exit hatch's things go into the car; the entry hatch's stay in the house.</summary>
        int LoadCarServer()
        {
            var rng = new System.Random(LevelRandom.Mix(m_RunSeed, Salt.CarLoad, Level.Index));
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

        /// <summary>
        /// Server, the car drives off from under the house: the house is left behind — the knocked out with it (each one raises
        /// the next quota, their things stay there), the exit hatch's stash rides in the bed, the entry's is lost.
        /// </summary>
        bool LeaveHouseServer()
        {
            int down = PlayerController.All.Count(p => p.KnockedOut);
            if (!m_Run.EnterSewer(down)) return false;
            int lost = LoadCarServer();
            foreach (var p in PlayerController.All)
            {
                if (p.KnockedOut) p.GetComponent<PlayerInventory>()?.ClearServer();
                p.LeaveHatchServer();
            }
            if (down > 0) Banner(Loc.Pack("banner.left_behind", down, Mathf.RoundToInt(m_Run.Settings.DeathPenalty * down * 100)));
            if (lost > 0) Banner(Loc.Pack("banner.hatch_lost", lost));
            return true;
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
                    if (Phase is RunPhase.Sewer or RunPhase.Playing && !GateOpen) { m_Opened.Add(OpenKeys.Gate); OpenedVersion++; Banner(Loc.Pack("banner.gate_open")); }
                    break;
                case UseKind.ExitLadder:
                    ExitUpServer(player);
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
            // the backpack's loot (or with an empty backpack the bed's) into the shredder: a pile it chews into fuel
            var inventory = player.GetComponent<PlayerInventory>();
            m_Chewed.Clear();
            if (inventory != null) inventory.TakeLootServer(m_Chewed);
            if (m_Chewed.Count == 0)
                foreach (var item in Cargo.Items.ToArray())
                {
                    if (ItemCatalog.IsTool(item.Shape)) continue;
                    m_Chewed.Add((item.Shape, item.Color));
                    Cargo.Remove(item);
                }
            foreach (var (shape, color) in m_Chewed) GrindServer(player, shape, color);
            m_Chewed.Clear();
        }

        /// <summary>Server: a thing dropped over the car's bed lies there (no cells, a heap). False if it was not over the bed.</summary>
        public bool DropOnCarServer(byte shape, byte color, Vector3 at, float yaw)
        {
            var car = Car.Active;
            if (car == null || Phase is not (RunPhase.Sewer or RunPhase.Playing) || !car.OverBed(at, out var spot)) return false;
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
                UpdateDriveServer();
                return;
            }
            UpdateRouteServer();
        }

        /// <summary>
        /// Server: the way on — in the sewer between houses or in the house's own sewer under it. The car standing in an open
        /// tunnel chooses its way; the team goes once it has the fuel for that way and everyone who can is in the car (a
        /// short countdown) — till then the way stays chosen and the team may go back up into the house for more.
        /// </summary>
        void UpdateRouteServer()
        {
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
            if (ChosenLane != lane)
            {
                ChosenLane = lane; // the way is chosen (the next house starts generating), fuel or not: the team may go back up for more
                GoAt = 0;
            }
            if (!AllAboard) { GoAt = 0; return; } // the car waits for the team
            if (Collected < CostOf(lane))
            {
                GoAt = 0;
                if (Time.time >= m_FuelNagAt)
                {
                    m_FuelNagAt = Time.time + 6f;
                    Banner(Loc.Pack("banner.fuel_short", Collected, CostOf(lane)));
                }
                return;
            }
            if (GoAt <= 0) { GoAt = Session.ServerTime + m_Run.Settings.VoteDelay; return; }
            if (Session.ServerTime >= GoAt) DepartServer();
        }

        /// <summary>Everyone who can sit is in the car (the knocked out stay behind: they come round in the next house with nothing).</summary>
        static bool AllAboard
        {
            get
            {
                bool any = false;
                foreach (var p in PlayerController.All)
                {
                    if (p.KnockedOut) continue;
                    if (p.SeatIndex < 0) return false;
                    any = true;
                }
                return any;
            }
        }

        /// <summary>Server: crouching in the exit hatch — down its ladder to the house's sewer (to the car, the gate, the ways on).</summary>
        void ExitDownServer(PlayerController player)
        {
            var sewer = m_Sewer;
            if (Phase != RunPhase.Playing || player == null || player.Downed || !player.InHatch || player.HatchIndex != 1) return;
            if (sewer == null || !IsHouseSewer(sewer) || !sewer.Built || sewer.Spawns.Length == 0) return;
            int slot = PlayerLooks.SlotOf(player.ClientId);
            player.LeaveHatchServer();
            player.PlaceServer(sewer.SpawnFor(slot), sewer.SpawnRotation(slot).eulerAngles.y);
        }

        /// <summary>Server: up the ladder under the exit hatch — the player comes out sitting in the hatch, up in the house.</summary>
        void ExitUpServer(PlayerController player)
        {
            var l = ActiveLevel.Layout;
            if (Phase != RunPhase.Playing || player == null || player.Downed || l == null || !ActiveLevel.Ready || !IsHouseSewer(m_Sewer)) return;
            player.EnterHatchServer(1);
            var away = l.ExitHatchPosition - l.Plan.RoomAt(l.ExitHatchPosition + Vector3.up * 0.3f)?.Center ?? Vector3.forward;
            player.PlaceServer(l.ExitHatchPosition + Vector3.up * 0.05f, Mathf.Atan2(-away.x, -away.z) * Mathf.Rad2Deg); // facing into the room
        }

        void CheckStranded()
        {
            int cheapest = CheapestCost;
            if (Collected >= cheapest) { m_StrandedAt = -1f; return; }
            int burnable = CarHopper.BedFuel(Cargo);
            foreach (var p in PlayerController.All) burnable += CarHopper.BackpackFuel(p.GetComponent<PlayerInventory>());
            foreach (var item in WorldItem.All) if (!ItemCatalog.IsTool(item.Shape)) burnable += item.Value;
            foreach (var hidden in m_Hidden.Values) burnable += ItemCatalog.Value(hidden.shape, hidden.color); // in the house's drawers still
            var house = ActiveLevel.Layout;
            if (Phase == RunPhase.Playing)
                foreach (var stash in new[] { Stash, ExitStash }) // the hatches' stashes are still there to fetch from
                    foreach (var e in stash.Items)
                        if (!ItemCatalog.IsTool(e.Shape)) burnable += ItemCatalog.Value(e.Shape, e.Color);
            if (Phase == RunPhase.Playing && house != null)
                foreach (int c in house.Crates)
                    if (!IsOpened(OpenKeys.Crate(c)))
                        foreach (var (shape, color) in CrateContents(house, c, Gadgets, Tuning.Level.Loot)) burnable += ItemCatalog.Value(shape, color);
            if (RunState.CanStillGo(Collected, cheapest, burnable)) { m_StrandedAt = -1f; return; }
            if (m_StrandedAt < 0f)
            {
                m_StrandedAt = Time.time;
                Banner(Loc.Pack("banner.stranded", Collected, cheapest));
                return;
            }
            if (Time.time - m_StrandedAt < m_Run.Settings.StrandedGrace || !m_Run.Strand()) return;
            SyncLocation();
            Banner(Loc.Pack("banner.run_over", RunTotal));
        }

        void DepartServer()
        {
            GoAt = 0;
            int cost = CostOf(ChosenLane);
            if (Collected < cost) return;
            if (Phase == RunPhase.Playing && !LeaveHouseServer()) return;
            if (m_Run.Advance(cost) != RunState.Exit.NextLevel) return;
            // the team stays in its seats: the car drives on down the tunnel to the next house's basement while the house is
            // built (GameManager.Arrival)
            DepartCarServer(Car.Active);
            DriveUntil = DepartAt + m_Run.Settings.DriveTime; // (provisional: the change into the basement is set once the house stands)
            ArriveAt = 0;
        }
    }
}
