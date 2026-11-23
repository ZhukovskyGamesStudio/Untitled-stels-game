using System;
using System.Collections.Generic;
using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Player
{
    public struct InventoryEntry : IEquatable<InventoryEntry>
    {
        public byte Shape, Color, X, Y, Rot;

        public bool Equals(InventoryEntry o) => Shape == o.Shape && Color == o.Color && X == o.X && Y == o.Y && Rot == o.Rot;
        public int Value => ItemCatalog.Value(Shape, Color);
        public Vector2Int[] Cells => ItemCatalog.RotatedCells(Shape, Rot);
    }

    /// <summary>
    /// Tarkov-like grid inventory. Server-authoritative: the owner calls <c>Request…</c>, the server validates in
    /// <c>…Server</c> handlers. The network twin (InventoryNet) replicates <see cref="Items"/> and carries the requests.
    /// </summary>
    public partial class PlayerInventory : MonoBehaviour
    {
        public const float ReachDistance = 3.5f;
        /// <summary>The hand slot: an entry with this X is held in the hand, not in the backpack's grid (one at a time).</summary>
        public const byte HandX = 255;

        public static bool InHand(InventoryEntry e) => e.X == HandX;

        /// <summary>The belt bag being worn: an entry with this X (no cells of the backpack). It brings the belt's grid.</summary>
        public const byte WornX = 254;
        /// <summary>The belt's 4 x 4 grid: entries with X from here (X − BeltX0 is the belt's column).</summary>
        public const byte BeltX0 = 100;
        public const int BeltSize = 4;

        public static bool Worn(InventoryEntry e) => e.X == WornX;
        public static bool InBelt(InventoryEntry e) => e.X >= BeltX0 && e.X < BeltX0 + BeltSize;
        /// <summary>Off the backpack's and the belt's grids (the hand, the worn bag).</summary>
        static bool OffGrid(InventoryEntry e) => InHand(e) || Worn(e);

        readonly List<InventoryEntry> m_Items = new();
        IInventoryLink m_Link;
        IInventoryLink m_Offline;
        /// <summary>Where the owner's requests go: the network twin, or (no twin) straight to the server side.</summary>
        IInventoryLink Link => m_Link ?? (m_Offline ??= new OfflineInventoryLink(this));
        PlayerController m_Player;

        public IReadOnlyList<InventoryEntry> Items => m_Items;
        /// <summary>Bumped on every change (the network twin replicates on change).</summary>
        public int Version { get; private set; }

        void Awake() => m_Player = GetComponent<PlayerController>();

        public void Bind(IInventoryLink link) => m_Link = link;

        /// <summary>Client: contents as decided by the server.</summary>
        public void ApplyReplicated(IEnumerable<InventoryEntry> items)
        {
            m_Items.Clear();
            m_Items.AddRange(items);
            Version++;
        }

        public int TotalValue
        {
            get
            {
                int sum = 0;
                foreach (var e in m_Items) sum += e.Value;
                return sum;
            }
        }

        public bool Carries(int color)
        {
            foreach (var e in m_Items) if (ItemCatalog.ColorOf(e.Shape, e.Color) == color) return true; // (a blueprint's byte is its recipe)
            return false;
        }

        public HashSet<Vector2Int> Occupied(int ignoreIndex = -1)
        {
            var set = new HashSet<Vector2Int>();
            for (int i = 0; i < m_Items.Count; i++)
            {
                if (i == ignoreIndex || OffGrid(m_Items[i])) continue;
                var e = m_Items[i];
                foreach (var c in e.Cells) set.Add(new Vector2Int(e.X + c.x, e.Y + c.y));
            }
            return set;
        }

        public int IndexAt(Vector2Int cell)
        {
            for (int i = 0; i < m_Items.Count; i++)
            {
                var e = m_Items[i];
                if (OffGrid(e)) continue;
                foreach (var c in e.Cells)
                    if (e.X + c.x == cell.x && e.Y + c.y == cell.y) return i;
            }
            return -1;
        }

        public bool CanPlace(int shape, int rot, int x, int y, int ignoreIndex = -1)
        {
            // the worn bag stays on while its belt holds something
            if (ignoreIndex >= 0 && ignoreIndex < m_Items.Count && Worn(m_Items[ignoreIndex]) && x != WornX && BeltCount() > 0) return false;
            if (x == HandX) return ItemCatalog.Holdable(shape) && HandIndex(ignoreIndex) < 0;
            if (x == WornX) return shape == ItemCatalog.BeltBag && WornIndex(ignoreIndex) < 0;
            if (x >= BeltX0 && x < BeltX0 + BeltSize)
                return WornIndex(ignoreIndex) >= 0 && BeltFits(ItemCatalog.RotatedCells(shape, rot), x, y, Occupied(ignoreIndex));
            return InventoryLayout.Fits(ItemCatalog.RotatedCells(shape, rot), x, y, Occupied(ignoreIndex));
        }

        /// <summary>Cells inside the belt's 4 x 4 grid (in the inventory's columns: <see cref="BeltX0"/> on) and free.</summary>
        public static bool BeltFits(Vector2Int[] cells, int x, int y, HashSet<Vector2Int> occupied)
        {
            foreach (var c in cells)
            {
                int cx = x + c.x, cy = y + c.y;
                if (cx < BeltX0 || cx >= BeltX0 + BeltSize || cy < 0 || cy >= BeltSize || occupied.Contains(new Vector2Int(cx, cy))) return false;
            }
            return true;
        }

        /// <summary>The worn belt bag (index in <see cref="Items"/>), -1 without one.</summary>
        public int WornIndex(int ignoreIndex = -1)
        {
            for (int i = 0; i < m_Items.Count; i++) if (i != ignoreIndex && Worn(m_Items[i])) return i;
            return -1;
        }

        /// <summary>A belt bag is worn: the belt's grid is there.</summary>
        public bool HasBelt => WornIndex() >= 0;

        /// <summary>How many things are in the belt.</summary>
        public int BeltCount()
        {
            int n = 0;
            foreach (var e in m_Items) if (InBelt(e)) n++;
            return n;
        }

        /// <summary>The item held in the hand slot (index in <see cref="Items"/>), -1 if the hand is empty.</summary>
        public int HandIndex(int ignoreIndex = -1)
        {
            for (int i = 0; i < m_Items.Count; i++) if (i != ignoreIndex && InHand(m_Items[i])) return i;
            return -1;
        }

        /// <summary>A flashlight that a battery can recharge: a flashlight below full charge.</summary>
        public bool CanRecharge(int flashlight) =>
            flashlight >= 0 && flashlight < m_Items.Count && m_Items[flashlight].Shape == ItemCatalog.Flashlight && m_Items[flashlight].Color < ItemCatalog.FullCharge;

        /// <summary>First free position for an item, trying all rotations: the backpack, then the belt (if worn). Used by tests and quick pickup.</summary>
        public bool FindSpot(int shape, out byte x, out byte y, out byte rot)
        {
            if (shape == ItemCatalog.BeltBag && !HasBelt) { x = WornX; y = 0; rot = 0; return true; } // a belt bag is put on
            return FindSpotIn(shape, Occupied(), out x, out y, out rot);
        }

        /// <summary>The first free spot for a shape (backpack, then belt) among <paramref name="occupied"/> cells.</summary>
        bool FindSpotIn(int shape, HashSet<Vector2Int> occupied, out byte x, out byte y, out byte rot)
        {
            if (FindIn(shape, occupied, out x, out y, out rot)) return true;
            if (!HasBelt) return false;
            for (int r = 0; r < 4; r++)
            {
                var cells = ItemCatalog.RotatedCells(shape, r);
                for (int yy = 0; yy < BeltSize; yy++)
                    for (int xx = BeltX0; xx < BeltX0 + BeltSize; xx++)
                        if (BeltFits(cells, xx, yy, occupied))
                        {
                            x = (byte)xx; y = (byte)yy; rot = (byte)r;
                            return true;
                        }
            }
            return false;
        }

        static bool FindIn(int shape, HashSet<Vector2Int> occupied, out byte x, out byte y, out byte rot)
        {
            for (int r = 0; r < 4; r++)
            {
                var cells = ItemCatalog.RotatedCells(shape, r);
                for (int yy = 0; yy < InventoryLayout.Height; yy++)
                    for (int xx = 0; xx < InventoryLayout.Width; xx++)
                        if (InventoryLayout.Fits(cells, xx, yy, occupied))
                        {
                            x = (byte)xx; y = (byte)yy; rot = (byte)r;
                            return true;
                        }
            }
            x = y = rot = 0;
            return false;
        }

        /// <summary>
        /// Whether the thing at <paramref name="index"/> (in the backpack or the belt) can go into the occupied hand with the
        /// held one going back where it was — its place first (any turn), else the first free spot — and where that is.
        /// </summary>
        public bool CanSwapHand(int index, out byte x, out byte y, out byte rot)
        {
            x = y = rot = 0;
            int hand = HandIndex();
            if (index < 0 || index >= m_Items.Count || hand < 0 || hand == index) return false;
            var e = m_Items[index];
            var held = m_Items[hand];
            if (OffGrid(e) || !ItemCatalog.Holdable(e.Shape)) return false;
            var occupied = Occupied(index); // (the held one is off the grid)
            bool belt = e.X >= BeltX0 && e.X < BeltX0 + BeltSize;
            for (int r = 0; r < 4; r++)
            {
                var cells = ItemCatalog.RotatedCells(held.Shape, r);
                bool fits = belt ? HasBelt && BeltFits(cells, e.X, e.Y, occupied) : InventoryLayout.Fits(cells, e.X, e.Y, occupied);
                if (!fits) continue;
                x = e.X; y = e.Y; rot = (byte)r;
                return true;
            }
            return FindSpotIn(held.Shape, occupied, out x, out y, out rot);
        }

        // ------------------------------------------------------------------ requests (owner → server)

        public void RequestSwapHand(int index) => Link.RequestSwapHand(index);

        public void RequestPickup(WorldItem item, byte x, byte y, byte rot)
        {
            Link.RequestPickup(item, x, y, rot);
        }

        public void RequestTakeCargo(Vehicles.CargoItem item, byte x, byte y, byte rot)
        {
            Link.RequestTakeCargo(item.Pack(), x, y, rot);
        }

        /// <summary>Server: a thing from the car's bed into this cell (standing at the car in the sewer).</summary>
        public void TakeCargoServer(Vehicles.CargoItem item, byte x, byte y, byte rot)
        {
            var game = GameManager.Instance;
            var car = Vehicles.Car.Active;
            if (game == null || car == null || !CanAct || game.Phase is not (RunPhase.Sewer or RunPhase.Playing)) return;
            if ((car.transform.position - transform.position).magnitude > CarReach || !CanPlace(item.Shape, rot, x, y)) return;
            if (!game.Cargo.Remove(item)) return; // taken by somebody else meanwhile
            m_Items.Add(new InventoryEntry { Shape = item.Shape, Color = item.Color, X = x, Y = y, Rot = rot });
            Changed();
        }

        /// <summary>How far from the car's middle its bed and hopper are within reach.</summary>
        public const float CarReach = 5f;

        public void RequestMove(int index, byte x, byte y, byte rot)
        {
            Link.RequestMove(index, x, y, rot);
        }

        public void RequestDrop(int index, Vector3 position, float yaw)
        {
            Link.RequestDrop(index, position, yaw);
        }

        public void RequestDeposit(int index)
        {
            Link.RequestDeposit(index);
        }

        public void RequestStash(int index, byte x, byte y, byte rot)
        {
            Link.RequestStash(index, x, y, rot);
        }

        public void RequestUnstash(InventoryEntry from, byte x, byte y, byte rot)
        {
            Link.RequestUnstash(from, x, y, rot);
        }

        public void RequestStashMove(InventoryEntry from, byte x, byte y, byte rot)
        {
            Link.RequestStashMove(from, x, y, rot);
        }

        public void RequestSellStashed(InventoryEntry from)
        {
            Link.RequestSellStashed(from);
        }

        public void RequestStashWorld(WorldItem item, byte x, byte y, byte rot)
        {
            Link.RequestStashWorld(item, x, y, rot);
        }

        public void RequestSellWorld(WorldItem item)
        {
            Link.RequestSellWorld(item);
        }

        public void RequestStashAll()
        {
            Link.RequestStashAll();
        }

        /// <summary>A battery from the backpack onto a flashlight: the flashlight is full again, the battery is used up.</summary>
        public void RequestRecharge(int battery, int flashlight)
        {
            Link.RequestRecharge(battery, flashlight);
        }

        /// <summary>A battery lying in the world dragged onto a flashlight.</summary>
        public void RequestRechargeWorld(WorldItem battery, int flashlight)
        {
            Link.RequestRechargeWorld(battery, flashlight);
        }

        /// <summary>An item of the team stash dropped into the world (like from the backpack).</summary>
        public void RequestDropStashed(InventoryEntry from, Vector3 position, float yaw)
        {
            Link.RequestDropStashed(from, position, yaw);
        }

        // ------------------------------------------------------------------ server

        bool CanAct => m_Player == null || !m_Player.Downed;

        void Changed() => Version++;

        public void PickupServer(WorldItem item, byte x, byte y, byte rot)
        {
            if (!CanAct || item == null) return;
            if (!ServerReach.Within(transform.position, item.transform.position, ReachDistance)) return;
            // a belt bag is put on (its belt's grid appears), wherever it was dropped in the backpack
            if (item.Shape == ItemCatalog.BeltBag && !HasBelt) { x = WornX; y = 0; rot = 0; }
            if (!CanPlace(item.Shape, rot, x, y)) return;
            m_Items.Add(new InventoryEntry { Shape = item.Shape, Color = item.Color, X = x, Y = y, Rot = rot });
            Changed();
            Session.Despawn(item.gameObject);
            GameManager.Instance?.NoteServer(GetComponent<PlayerController>(), StatKind.Pickups);
        }

        public void MoveServer(int index, byte x, byte y, byte rot)
        {
            if (index < 0 || index >= m_Items.Count || (Locked(index) && x != WornX)) return;
            var e = m_Items[index];
            if (!CanPlace(e.Shape, rot, x, y, index)) return;
            e.X = x; e.Y = y; e.Rot = rot;
            m_Items[index] = e;
            Changed();
        }

        /// <summary>Server: the thing at <paramref name="index"/> into the occupied hand, the held one into the backpack (<see cref="CanSwapHand"/>).</summary>
        public void SwapHandServer(int index)
        {
            if (!CanSwapHand(index, out var x, out var y, out var rot)) return;
            int hand = HandIndex();
            var e = m_Items[index];
            var held = m_Items[hand];
            e.X = HandX; e.Y = 0; e.Rot = 0;
            held.X = x; held.Y = y; held.Rot = rot;
            m_Items[index] = e;
            m_Items[hand] = held;
            Changed();
        }

        /// <summary>The worn belt bag comes off only empty (its belt's things have nowhere to go).</summary>
        bool Locked(int index) => Worn(m_Items[index]) && BeltCount() > 0;

        public void DropServer(int index, Vector3 position, float yaw)
        {
            if (index < 0 || index >= m_Items.Count || Locked(index)) return;
            if (!ServerReach.Within(transform.position, position, ReachDistance))
                position = transform.position + transform.forward * 0.8f;
            var e = m_Items[index];
            m_Items.RemoveAt(index);
            Changed();
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.DropOnCarServer(e.Shape, e.Color, position, yaw)) return; // onto the car's bed: a heap, no cells
            var item = gm.SpawnItemServer(e.Shape, e.Color, position, yaw);
            // a lure speaker dropped on the floor starts playing after a moment (time to walk away)
            if (e.Shape == ItemCatalog.Speaker && item != null)
            {
                var g = Configs.Tuning.Items.Gadgets;
                item.PlayServer(Session.ServerTime + g.LureDelay, g.LureDuration);
            }
        }

        /// <summary>
        /// Server: one thing dropped into the car's hopper (in the sewer) or into the entry hatch (in a house) burns into fuel
        /// at once — tools too (a key, the stun, the speaker burn at the grey rate); anywhere else nothing happens.
        /// </summary>
        public void DepositServer(int index)
        {
            if (index < 0 || index >= m_Items.Count || !CanAct || Locked(index)) return;
            var game = GameManager.Instance;
            if (game == null || !game.CanBurnAt(transform.position)) return;
            var e = m_Items[index];
            if (ItemCatalog.Unburnable(e.Shape)) return;
            m_Items.RemoveAt(index);
            Changed();
            // at the car it goes into the shredder (fuel once chewed); in the entry hatch it burns at once
            if (game.AtCar(transform.position)) game.GrindServer(GetComponent<PlayerController>(), e.Shape, e.Color);
            else game.BurnServer(GetComponent<PlayerController>(), e.Value);
        }

        /// <summary>Server: a battery recharges a flashlight below full charge and is used up.</summary>
        public void RechargeServer(int battery, int flashlight)
        {
            if (!CanAct || battery < 0 || battery >= m_Items.Count || m_Items[battery].Shape != ItemCatalog.Battery || !CanRecharge(flashlight)) return;
            var e = m_Items[flashlight];
            e.Color = ItemCatalog.FullCharge;
            m_Items[flashlight] = e;
            m_Items.RemoveAt(battery);
            Changed();
        }

        /// <summary>Server: a battery from the world (in reach) recharges a flashlight below full charge.</summary>
        public void RechargeWorldServer(WorldItem battery, int flashlight)
        {
            if (!CanAct || !InReach(battery) || battery.Shape != ItemCatalog.Battery || !CanRecharge(flashlight)) return;
            var e = m_Items[flashlight];
            e.Color = ItemCatalog.FullCharge;
            m_Items[flashlight] = e;
            Changed();
            Session.Despawn(battery.gameObject);
        }

        /// <summary>Server: the held thing's colour byte (the upgraded screwdriver marks the house it was used in).</summary>
        public void SetHeldColorServer(byte color)
        {
            int h = HandIndex();
            if (h < 0 || m_Items[h].Color == color) return;
            var e = m_Items[h];
            e.Color = color;
            m_Items[h] = e;
            Changed();
        }

        /// <summary>Server: the charge of the flashlight at this index (it drains while it shines).</summary>
        public void SetChargeServer(int index, byte charge)
        {
            if (index < 0 || index >= m_Items.Count || m_Items[index].Shape != ItemCatalog.Flashlight || m_Items[index].Color == charge) return;
            var e = m_Items[index];
            e.Color = charge;
            m_Items[index] = e;
            Changed();
        }

        /// <summary>Server: an item of the stash at this hatch dropped into the world.</summary>
        public void DropStashedServer(InventoryEntry from, Vector3 position, float yaw)
        {
            if (!AtHatch(out var stash) || stash.IndexOf(from) < 0) return;
            if (!ServerReach.Within(transform.position, position, ReachDistance))
                position = transform.position + transform.forward * 0.8f;
            stash.Take(from);
            GameManager.Instance?.SpawnItemServer(from.Shape, from.Color, position, yaw);
        }


        // Team stash: at the hatch of a house, or at the board in the sewer.

        bool AtHatch(out TeamStash stash)
        {
            var gm = GameManager.Instance;
            stash = CanAct && gm != null ? gm.StashAt(transform.position) : null;
            return stash != null;
        }

        /// <summary>Server, the car's hopper: every piece of loot leaves the backpack into <paramref name="into"/> (tools stay).</summary>
        public void TakeLootServer(List<(byte shape, byte color)> into)
        {
            bool any = false;
            for (int i = m_Items.Count - 1; i >= 0; i--)
            {
                if (ItemCatalog.IsTool(m_Items[i].Shape)) continue;
                into.Add((m_Items[i].Shape, m_Items[i].Color));
                m_Items.RemoveAt(i);
                any = true;
            }
            if (any) Changed();
        }

        /// <summary>Server, the car's hopper: every piece of loot in the backpack becomes fuel (tools stay). Returns the fuel.</summary>
        public int BurnServer()
        {
            int fuel = 0;
            for (int i = m_Items.Count - 1; i >= 0; i--)
            {
                if (ItemCatalog.IsTool(m_Items[i].Shape)) continue;
                fuel += m_Items[i].Value;
                m_Items.RemoveAt(i);
            }
            if (fuel > 0) Changed();
            return fuel;
        }

        bool InReach(WorldItem item) => item != null && ServerReach.Within(transform.position, item.transform.position, ReachDistance);

        public void StashServer(int index, byte x, byte y, byte rot)
        {
            if (index < 0 || index >= m_Items.Count || !AtHatch(out var stash) || Locked(index)) return;
            var e = m_Items[index];
            if (!stash.Add(e.Shape, e.Color, x, y, rot)) return;
            m_Items.RemoveAt(index);
            Changed();
        }

        /// <summary>Every carried item into the first free spot of the stash, biggest first; what does not fit stays in the backpack.</summary>
        public void StashAllServer()
        {
            var game = GameManager.Instance;
            if (game != null && game.Phase is RunPhase.Sewer or RunPhase.Playing && Vehicles.Car.Active != null && CanAct &&
                (Vehicles.Car.Active.transform.position - transform.position).magnitude < 5f)
            {
                // at the car: everything from the backpack onto its bed
                var rng = new System.Random(m_Items.Count * 7919 + (int)(Time.time * 10f));
                // (the worn belt bag stays on)
                bool any = false;
                for (int i = m_Items.Count - 1; i >= 0; i--)
                {
                    var e = m_Items[i];
                    if (Worn(e)) continue;
                    var spot = game.Cargo.FreeSpot(rng);
                    game.Cargo.Add(e.Shape, e.Color, spot.x, spot.y, rng.Next(360));
                    m_Items.RemoveAt(i);
                    any = true;
                }
                if (any) Changed();
                return;
            }
            var carried = m_Items.FindAll(e => !Worn(e));
            if (!AtHatch(out var stash) || carried.Count == 0) return;
            if (stash.AddAll(carried, out var left) == 0) return;
            m_Items.RemoveAll(e => !Worn(e));
            m_Items.AddRange(left);
            Changed();
        }

        public void UnstashServer(InventoryEntry from, byte x, byte y, byte rot)
        {
            if (!AtHatch(out var stash) || stash.IndexOf(from) < 0 || !CanPlace(from.Shape, rot, x, y)) return;
            stash.Take(from);
            m_Items.Add(new InventoryEntry { Shape = from.Shape, Color = from.Color, X = x, Y = y, Rot = rot });
            Changed();
        }

        public void StashMoveServer(InventoryEntry from, byte x, byte y, byte rot)
        {
            if (AtHatch(out var stash)) stash.Move(from, x, y, rot);
        }

        /// <summary>Server: a thing from the entry hatch's stash dropped on its burn zone becomes fuel (tools too).</summary>
        public void SellStashedServer(InventoryEntry from)
        {
            var game = GameManager.Instance;
            if (ItemCatalog.Unburnable(from.Shape) || !AtHatch(out var stash) || game == null || !game.CanBurnStashAt(transform.position, stash) || !stash.Take(from)) return;
            game.BurnServer(GetComponent<PlayerController>(), from.Value);
        }

        public void StashWorldServer(WorldItem item, byte x, byte y, byte rot)
        {
            if (!InReach(item) || !AtHatch(out var stash) || !stash.Add(item.Shape, item.Color, x, y, rot)) return;
            Session.Despawn(item.gameObject);
        }

        /// <summary>Server: a thing on the floor dragged onto the entry hatch's burn zone becomes fuel.</summary>
        public void SellWorldServer(WorldItem item)
        {
            var game = GameManager.Instance;
            if (!InReach(item) || game == null || !game.CanBurnAt(transform.position) || ItemCatalog.Unburnable(item.Shape)) return;
            int fuel = item.Value;
            byte shape = item.Shape, color = item.Color;
            Session.Despawn(item.gameObject);
            if (game.AtCar(transform.position)) game.GrindServer(GetComponent<PlayerController>(), shape, color);
            else game.BurnServer(GetComponent<PlayerController>(), fuel);
        }

        /// <summary>Server: put an item straight into the first free spot (rewards, scripted checks). False if it does not fit.</summary>
        /// <summary>Server: put a holdable thing straight into the empty hand slot. False if the hand is taken or it cannot be held.</summary>
        public bool GiveToHandServer(byte shape, byte color)
        {
            if (!CanPlace(shape, 0, HandX, 0)) return false;
            m_Items.Add(new InventoryEntry { Shape = shape, Color = color, X = HandX, Y = 0, Rot = 0 });
            Changed();
            return true;
        }

        /// <summary>Anywhere on the player (backpack, belt, hand): the boots work while they are carried (an upgraded one counts as its base, <see cref="ItemCatalog.Works"/>).</summary>
        public bool Carries(byte shape)
        {
            foreach (var e in m_Items) if (ItemCatalog.Works(e.Shape, shape)) return true;
            return false;
        }

        /// <summary>Anywhere on the player, this very thing (not a thing it is the upgrade of).</summary>
        public bool CarriesExactly(byte shape)
        {
            foreach (var e in m_Items) if (e.Shape == shape) return true;
            return false;
        }

        public bool GiveServer(byte shape, byte color)
        {
            if (!FindSpot(shape, out var x, out var y, out var rot)) return false;
            m_Items.Add(new InventoryEntry { Shape = shape, Color = color, X = x, Y = y, Rot = rot });
            Changed();
            return true;
        }

        /// <summary>Server: use up one item of this shape (a key, a stun cube). False if there is none.</summary>
        /// <summary>Server: a copy of what is carried (a player who leaves keeps it for a reconnect).</summary>
        public List<InventoryEntry> SnapshotServer() => new(m_Items);

        /// <summary>Server: a player who came back gets their things again (what does not fit is dropped at their feet).</summary>
        public void RestoreServer(List<InventoryEntry> items)
        {
            foreach (var e in items)
            {
                if (CanPlace(e.Shape, e.Rot, e.X, e.Y)) m_Items.Add(e);
                else if (!GiveServer(e.Shape, e.Color)) GameManager.Instance?.SpawnItemServer(e.Shape, e.Color, transform.position + Vector3.up * 0.3f, 0f);
            }
            Changed();
        }

        /// <summary>Whether the hand slot holds an item of this shape (keys and the stun work only from the hand).</summary>
        public bool Holds(byte shape)
        {
            int h = HandIndex();
            return h >= 0 && ItemCatalog.Works(m_Items[h].Shape, shape);
        }

        /// <summary>Whether the hand slot holds this very thing (not a thing it is the upgrade of).</summary>
        public bool HoldsExactly(byte shape)
        {
            int h = HandIndex();
            return h >= 0 && m_Items[h].Shape == shape;
        }

        /// <summary>Whether the backpack or the hand has an item of this shape (or its upgrade).</summary>
        public bool Has(byte shape) => m_Items.Exists(e => ItemCatalog.Works(e.Shape, shape));

        /// <summary>Server: use up the item in the hand if it is of this shape.</summary>
        public bool TakeHeldServer(byte shape)
        {
            int h = HandIndex();
            if (h < 0 || m_Items[h].Shape != shape) return false;
            m_Items.RemoveAt(h);
            Changed();
            return true;
        }

        public bool TakeServer(byte shape)
        {
            int i = m_Items.FindIndex(e => e.Shape == shape);
            if (i < 0) return false;
            m_Items.RemoveAt(i);
            Changed();
            return true;
        }

        /// <summary>Server: the workbench turns groups of one colour into diamonds (<see cref="Crafting"/>). Returns how many were made.</summary>
        public int CraftServer(GadgetSettings settings)
        {
            var recipes = Crafting.Plan(m_Items, settings);
            if (recipes.Count == 0) return 0;
            var used = new HashSet<int>();
            foreach (var r in recipes) used.UnionWith(r.Items);
            var kept = new List<InventoryEntry>();
            for (int i = 0; i < m_Items.Count; i++) if (!used.Contains(i)) kept.Add(m_Items[i]);
            m_Items.Clear();
            m_Items.AddRange(kept);
            int made = 0;
            foreach (var r in recipes)
            {
                if (!FindSpot(ItemCatalog.Diamond, out var x, out var y, out var rot)) break; // one cell: it fits where its parts were
                m_Items.Add(new InventoryEntry { Shape = ItemCatalog.Diamond, Color = (byte)r.Color, X = x, Y = y, Rot = rot });
                made++;
            }
            Changed();
            return made;
        }

        public void ClearServer()
        {
            m_Items.Clear();
            Changed();
        }
    }
}
