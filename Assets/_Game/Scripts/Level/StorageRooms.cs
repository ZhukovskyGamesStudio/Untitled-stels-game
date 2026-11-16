using System.Collections.Generic;
using Stealth.Items;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The storage room (pure, from the seed, own random stream): a room whose only way in is a door (no vent, slot or
    /// open passage into it), of the size the storage rule allows (<see cref="RoomRules"/>). Its door is locked for players
    /// — robots pass, so a player may slip in behind a worker — and opens from a door terminal elsewhere in the house, or
    /// from inside (nobody is shut in). Shelves along its walls and a pile on its floor; the workers haul loot there
    /// (<see cref="LevelLayout.StorageDrops"/>), and only a house with a storage room has workers.
    /// </summary>
    public static class StorageRooms
    {
        /// <summary>
        /// Before the furniture: the storage room of the house's room program (<see cref="RoomProgram"/>; the plan gave it
        /// one door and nothing else), checked: its only way in is that door. A storage room that is not (the plan could not
        /// keep it a dead end) — or a second one — becomes an office.
        /// </summary>
        public static void Plan(LevelLayout l, FeatureSettings s, bool wanted = false)
        {
            var plan = l.Plan;
            var rule = RoomRules.For(RoomType.Storage, l.RoomRules);
            var all = HouseFeatures.Reach(plan, l.Stairs, l.LobbyRoom, true);
            foreach (var room in plan.Rooms)
            {
                if (room.Type != RoomType.Storage) continue;
                int door = l.StorageRoom < 0 && Fits(l, room, rule) && all.Contains(room.Id) ? OnlyDoor(l, room.Id, all) : -1;
                if (door < 0) { room.Type = RoomType.Office; continue; }
                l.StorageRoom = room.Id;
                l.StorageDoorway = door;
            }
            if (!wanted || l.StorageRoom >= 0) return;
            // no storage room planned (the classic generator guesses the types, or the planned one did not hold): a dead end
            // of the plan becomes one — behind a single door, nothing else into it; the ground floor first, then about 20 m²
            PlanRoom best = null;
            int bestDoor = -1;
            float bestScore = float.MaxValue;
            foreach (var room in plan.Rooms)
            {
                if (!all.Contains(room.Id) || !Fits(l, room, rule) || Exterior(l, room.Id)) continue;
                int door = OnlyDoor(l, room.Id, all);
                if (door < 0) continue;
                float score = room.Floor * 1000f + Mathf.Abs(room.Area - 20f);
                if (score < bestScore) { bestScore = score; best = room; bestDoor = door; }
            }
            if (best == null) return;
            best.Type = RoomType.Storage;
            l.StorageRoom = best.Id;
            l.StorageDoorway = bestDoor;
        }

        /// <summary>The room has an opening in the building's outer wall (a way in from the yard).</summary>
        static bool Exterior(LevelLayout l, int room)
        {
            foreach (var o in l.Plan.Openings)
            {
                var w = l.Plan.Walls[o.Wall];
                if (w.Exterior && (w.RoomA == room || w.RoomB == room)) return true;
            }
            return false;
        }

        /// <summary>The doorway that is the only way into the room (players: any opening counts), or −1.</summary>
        static int OnlyDoor(LevelLayout l, int room, HashSet<int> all)
        {
            var plan = l.Plan;
            for (int i = 0; i < plan.Openings.Count; i++)
            {
                var o = plan.Openings[i];
                var w = plan.Walls[o.Wall];
                if (o.Kind != EdgeType.Door || w.Exterior || (w.RoomA != room && w.RoomB != room) || i == l.VaultDoorway) continue;
                var behind = new HashSet<int>(all);
                behind.ExceptWith(HouseFeatures.Reach(plan, l.Stairs, l.LobbyRoom, true, i));
                if (behind.Count == 1 && behind.Contains(room)) return i;
            }
            return -1;
        }

        static bool Fits(LevelLayout l, PlanRoom r, in RoomRule rule)
        {
            if (r.Id == l.LobbyRoom || r.Id == l.HiddenRoom || r.Id == l.VaultRoom || r.Levels != 1) return false;
            if (r.Type is RoomType.Corridor or RoomType.Hall or RoomType.Lobby) return false;
            if (l.Plan.RoomAt(l.ExitHatchPosition + Vector3.up * 0.3f)?.Id == r.Id) return false;
            foreach (var st in l.Stairs) if (st.Room == r.Id || st.RoomAbove == r.Id) return false;
            float aspect = Mathf.Max(r.Bounds.width, r.Bounds.height) / Mathf.Max(0.5f, Mathf.Min(r.Bounds.width, r.Bounds.height));
            return RoomRules.Fits(rule, r.Area, aspect, float.MaxValue, upTo: false);
        }

        /// <summary>
        /// After the security and the other terminals: the door (locked; a laser or a hackable lock there goes), the pile's
        /// spots on the floor (some get loot), the door terminal on a desk far from the door. Without a desk anywhere the
        /// door stays open.
        /// </summary>
        public static void Finish(LevelLayout l, FeatureSettings s)
        {
            if (l.StorageRoom < 0 || s == null) return;
            var rng = new System.Random(LevelRandom.Mix(l.Seed, 0x5702, 1));
            var d = l.Doorways[l.StorageDoorway];
            int door = -1;
            for (int k = 0; k < l.AutoDoors.Count; k++)
                if ((l.AutoDoors[k].position - d.Position).sqrMagnitude < 0.1f) { door = k; break; }
            if (door < 0)
            {
                l.AutoDoors.Add(new Pose(d.Position, d.Rotation));
                door = l.AutoDoors.Count - 1;
            }
            l.LockedDoors.Remove(door);
            l.LaserSpots.RemoveAll(x => (x.Position - d.Position).sqrMagnitude < 1.5f);
            l.StorageDoor = door;

            // the pile: open floor in the room's middle
            var room = l.Plan.Rooms[l.StorageRoom];
            foreach (var p in LootPlanner.FloorSpots(l, new HashSet<int> { room.Id }, s.StoragePileSpots, 0.9f))
            {
                l.StorageDrops.Add(new StorageDrop { Spot = p, Facing = Vector3.zero, Shelf = false });
                l.ItemSpots.Add(p);
            }

            // the door terminal: a desk out of the room, far from its door (the nearest such if none is that far)
            int best = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < l.Props.Count; i++)
            {
                var p = l.Props[i];
                if (p.Kind != PropKind.Desk) continue;
                if (l.Plan.RoomAt(p.Position + Vector3.up * 0.3f)?.Id == room.Id) continue;
                if ((p.Position - l.HatchPosition).magnitude <= 7.5f) continue; // like the other terminals: not in the lobby
                float dist = (p.Position - d.Position).magnitude + Mathf.Abs(p.Position.y - d.Position.y) * 2f;
                // far enough: any of them (by chance); none far enough: the farthest
                float score = dist >= s.StorageTerminalDistance ? 1000f + (float)rng.NextDouble() * 100f : dist;
                if (score > bestScore) { bestScore = score; best = i; }
            }
            if (best < 0) return; // no desk: the door stays open (StorageTerminal −1)
            var t = l.Props[best];
            t.Kind = PropKind.Terminal;
            l.Props[best] = t;
            l.Terminals.Add((best, TerminalKind.Doors));
            l.StorageTerminal = best;
        }

        /// <summary>The storage room's door is locked (it has a door terminal).</summary>
        public static bool Locked(LevelLayout l) => l.StorageDoor >= 0 && l.StorageTerminal >= 0;
    }
}
