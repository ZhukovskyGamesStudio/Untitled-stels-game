using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>The kinds of place a house can be; each has its own unique feature (append only: the byte travels).</summary>
    public enum Location : byte { Office, Hospital, Garage, Apartments, Abandoned }

    /// <summary>
    /// A warden's post (<see cref="RobotKind.Warden"/>): in front of the only doorway of a loot room, walking a few steps
    /// between <see cref="A"/> and <see cref="B"/> and watching <see cref="Facing"/>. Distract it to get past.
    /// </summary>
    public struct WardenPost
    {
        public Vector3 A, B, Facing;
        public int Room, Door;
    }

    /// <summary>A door opened only with a key block: it closes off a part of the house; the key lies somewhere else.</summary>
    public struct KeyDoor
    {
        /// <summary>Index in <see cref="LevelLayout.AutoDoors"/>.</summary>
        public int Door;
        public Vector3 Key;
        /// <summary>The rooms behind it (plan ids).</summary>
        public List<int> Rooms;
    }

    /// <summary>Tuning of the special places of a house (LevelConfig): hidden rooms, key doors, crates, wardens, lures.</summary>
    [System.Serializable]
    public class FeatureSettings
    {
        [Header("Hidden room (entrances covered by an illusion of a wall)")]
        [Tooltip("Chance that a house has a hidden room.")]
        public float HiddenRoomChance = 0.5f;
        [Tooltip("From which house (0 = the first).")]
        public int HiddenRoomsFromHouse = 1;
        public float HiddenRoomMinArea = 10f, HiddenRoomMaxArea = 45f;

        [Header("Key doors (lock off a part of the house; the key block lies elsewhere)")]
        public int KeyDoors = 1;
        [Tooltip("Lure speakers and stun cubes lie in the house (off: none of these tools spawn).")]
        public bool Gadgets = true;
        [Tooltip("One more key door every this many houses.")]
        public int HousesPerExtraKeyDoor = 3;
        public int MaxKeyDoors = 2;
        public int KeyDoorsFromHouse = 0;
        [Tooltip("At most this many rooms behind a key door (an isolated part, not half the house).")]
        public int KeyRegionRooms = 3;
        [Tooltip("The key lies at least this far from its door, metres.")]
        public float KeyMinDistance = 10f;

        [Header("Crates (search them: loot inside)")]
        public int Crates = 3;
        public float CratesPerHouse = 1f;
        public int MaxCrates = 8;

        [Header("Wardens (guard a loot room's doorway; distract them to get in)")]
        public int Wardens = 1;
        public float WardensPerHouse = 0.5f;
        public int MaxWardens = 3;
        [Tooltip("Lure speakers lying near each warden.")]
        public int LuresPerWarden = 2;
        [Tooltip("Lure speakers anywhere else in the house.")]
        public int ExtraLures = 1;
        [Tooltip("Lures lie this far from the warden's post, metres (min, max).")]
        public Vector2 LureDistance = new(6f, 14f);

        [Header("Main job (optional; shown on the tunnel's panel in the sewer)")]
        [Tooltip("From which house every house has one (0 = the first).")]
        public int ObjectivesFromHouse = 0;
        [Tooltip("Chance the job is the vault (otherwise the hostage); if one does not fit the house, the other.")]
        public float VaultShare = 0.5f;
        [Tooltip("The vault room's floor area, m² (min, max).")]
        public Vector2 VaultArea = new(8f, 45f);
        [Tooltip("The hostage's room: at least this many m² (the cage stands in its middle).")]
        public float HostageMinArea = 14f;

        [Header("Storage room (locked; opens from its terminal or from inside; workers haul loot to it)")]
        [Tooltip("Chance that a house has a storage room. Only then do workers come (they haul loot there).")]
        [Range(0f, 1f)] public float StorageChance = 0.6f;
        [Tooltip("From which house (0 = the first).")]
        public int StorageFromHouse = 0;
        [Tooltip("The door terminal stands at least this far from the storage room's door, metres (closer if no desk is that far).")]
        public float StorageTerminalDistance = 10f;
        [Tooltip("Spots on the floor of the pile where workers leave loot (and where the pile's first loot lies).")]
        public int StoragePileSpots = 6;
        [Tooltip("At least this many of the house's robots are workers when it has a storage room (if workers are on the roster yet).")]
        public int StorageWorkers = 1;
    }

    /// <summary>
    /// The special places of a house (pure, from the seed; each with its own random stream so the rest of the house does
    /// not shift): a hidden room behind illusion walls, key doors that close off a part of the house, searchable crates,
    /// wardens in front of loot rooms with lure speakers around them, and the location's point of interest (the robot
    /// programming console in an office, the med table in a hospital, the workbench in a garage; an apartment block has none —
    /// its find is the shelter map).
    /// </summary>
    public static class HouseFeatures
    {
        public static Location LocationOf(HouseKind kind) => HouseRules.For(kind).Location;

        /// <summary>The location's point of interest; none in an apartment block (its own find is the shelter map).</summary>
        public static PropKind? PointOfInterestKind(Location location) => HouseRules.For(location).PointOfInterest;

        /// <summary>
        /// The apartment block's find: the map to the survivors' shelter, hidden in one flat above the ground floor (its own
        /// stream of the house's seed: every peer agrees). The server puts it in a cupboard, wardrobe or chest of drawers of
        /// that flat (the bedroom's first); with none that holds it, it lies in the bedroom (or the living room) at <see cref="LevelLayout.MapSpot"/>.
        /// </summary>
        public static void PlanShelterMap(LevelLayout l)
        {
            l.MapFlat = l.MapRoom = -1;
            if (!HouseRules.Of(l).HasShelterMap) return;
            var flats = new SortedSet<int>();
            foreach (var r in l.Rooms) if (r.Flat >= 0 && (r.Floor > 0 || l.Floors == 1)) flats.Add(r.Flat);
            if (flats.Count == 0) return;
            var rng = new System.Random(LevelRandom.Mix(l.Seed, Salt.ShelterMap, 0));
            int pick = rng.Next(flats.Count), k = 0;
            foreach (int f in flats) { if (k++ == pick) { l.MapFlat = f; break; } }
            PlanRoom best = null;
            foreach (var r in l.Rooms)
            {
                if (r.Flat != l.MapFlat) continue;
                int rank = r.Type switch { RoomType.Bedroom => 3, RoomType.Living => 2, RoomType.Kitchen => 1, _ => 0 };
                int bestRank = best == null ? -1 : best.Type switch { RoomType.Bedroom => 3, RoomType.Living => 2, RoomType.Kitchen => 1, _ => 0 };
                if (rank > bestRank) best = r;
            }
            if (best == null) return;
            l.MapRoom = best.Id;
            l.MapSpot = best.Center;
        }

        /// <summary>The flat (−1: none) a piece of furniture of the house stands in.</summary>
        public static int FlatAt(LevelLayout l, Vector3 position)
        {
            var r = l.Plan.RoomAt(position + Vector3.up * 0.3f);
            return r != null ? r.Flat : -1;
        }

        /// <summary>
        /// Rooms reached from <paramref name="start"/> through the openings (players: every kind; robots: doors and open
        /// passages) and the stairs, leaving out the opening <paramref name="skip"/> and the rooms in <paramref name="closed"/>.
        /// </summary>
        public static HashSet<int> Reach(HousePlan plan, IReadOnlyList<Stair> stairs, int start, bool player, int skip = -1,
            ICollection<int> closed = null) =>
            new RoomGraph(plan, stairs, player ? Passage.Players : Passage.Robots).Reach(start, skip, closed);

        static bool HasStair(LevelLayout l, int room)
        {
            foreach (var s in l.Stairs) if (s.Room == room || s.RoomAbove == room) return true;
            return false;
        }

        static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ------------------------------------------------------------------ hidden room

        /// <summary>
        /// Sometimes one room becomes hidden: a room surrounded by other rooms on all sides (no outer wall, no atrium, no
        /// stairs) loses its doors, and every neighbour gets an entrance into it covered by an illusion of a wall
        /// (<see cref="EdgeType.Illusion"/>: players walk through, robots do not, the map does not see through). Only if
        /// the rest of the house stays connected for the robots. Returns the room id or -1. Before the furniture.
        /// </summary>
        public static int HideRoom(LevelLayout l, FeatureSettings s)
        {
            if (s == null || l.LevelIndex < s.HiddenRoomsFromHouse) return -1;
            var rng = new System.Random(LevelRandom.Mix(l.Seed, Salt.HideRoom, 0));
            if (rng.NextDouble() >= s.HiddenRoomChance) return -1;
            var plan = l.Plan;
            var candidates = new List<PlanRoom>();
            foreach (var r in plan.Rooms)
            {
                if (r.Id == l.LobbyRoom || r.Levels > 1 || RoomTraits.Circulation(r.Type) || r.Type == RoomType.Storage) continue;
                if (r.Area < s.HiddenRoomMinArea || r.Area > s.HiddenRoomMaxArea || HasStair(l, r.Id)) continue;
                bool enclosed = true;
                int walls = 0;
                foreach (var w in plan.Walls)
                {
                    if (w.RoomA != r.Id && w.RoomB != r.Id) continue;
                    int other = w.RoomA == r.Id ? w.RoomB : w.RoomA;
                    if (w.Exterior || w.Railing || other < 0) { enclosed = false; break; }
                    if (plan.Rooms[other].Type == RoomType.Storage) { enclosed = false; break; } // no second way into the storage room
                    walls++;
                }
                if (enclosed && walls > 0) candidates.Add(r);
            }
            Shuffle(candidates, rng);
            var robotsBefore = Reach(plan, l.Stairs, l.LobbyRoom, false);
            foreach (var r in candidates)
            {
                var openings = new List<PlanOpening>();
                foreach (var o in plan.Openings)
                {
                    var w = plan.Walls[o.Wall];
                    if (w.RoomA != r.Id && w.RoomB != r.Id) openings.Add(o);
                }
                // one entrance from each neighbour: in the middle of the longest straight wall they share
                var best = new Dictionary<int, int>();
                for (int i = 0; i < plan.Walls.Count; i++)
                {
                    var w = plan.Walls[i];
                    if ((w.RoomA != r.Id && w.RoomB != r.Id) || w.Shaped || w.Length < LevelMetrics.DoorWidth + 1.0f) continue;
                    int other = w.RoomA == r.Id ? w.RoomB : w.RoomA;
                    if (!best.TryGetValue(other, out int b) || plan.Walls[b].Length < w.Length) best[other] = i;
                }
                if (best.Count == 0) continue;
                foreach (var wall in best.Values)
                    openings.Add(new PlanOpening { Wall = wall, Kind = EdgeType.Illusion, At = plan.Walls[wall].Length / 2f, Width = LevelMetrics.DoorWidth });
                var saved = new List<PlanOpening>(plan.Openings);
                plan.Openings.Clear();
                plan.Openings.AddRange(openings);
                // every other room the robots reached before they still reach (the hidden one was not a corridor)
                var robotsAfter = Reach(plan, l.Stairs, l.LobbyRoom, false);
                bool connected = true;
                foreach (int id in robotsBefore) if (id != r.Id && !robotsAfter.Contains(id)) { connected = false; break; }
                if (connected) return r.Id;
                plan.Openings.Clear();
                plan.Openings.AddRange(saved);
            }
            return -1;
        }

        // ------------------------------------------------------------------ key doors

        /// <summary>
        /// Key doors: a doorway that is the only way (for players too: no vent, slot or illusion around it) into a small
        /// part of the house gets a door locked with a key block; the key lies in a room on this side, far from the door.
        /// After the security (it may turn a plain doorway into a door, takes away a laser there and a hackable lock).
        /// </summary>
        public static void KeyDoors(LevelLayout l, FeatureSettings s)
        {
            if (s == null || l.LevelIndex < s.KeyDoorsFromHouse) return;
            int want = Mathf.Min(s.MaxKeyDoors, s.KeyDoors + l.LevelIndex / Mathf.Max(1, s.HousesPerExtraKeyDoor));
            if (want <= 0) return;
            var plan = l.Plan;
            var rng = new System.Random(LevelRandom.Mix(l.Seed, Salt.KeyDoors, 0));
            var graph = new RoomGraph(plan, l.Stairs, Passage.Players);
            var all = graph.Reach(l.LobbyRoom);
            var order = new List<int>();
            for (int i = 0; i < plan.Openings.Count; i++)
            {
                var o = plan.Openings[i];
                var w = plan.Walls[o.Wall];
                if (o.Kind != EdgeType.Door || w.Exterior || w.RoomA < 0 || w.RoomB < 0) continue;
                if (w.RoomA == l.LobbyRoom || w.RoomB == l.LobbyRoom) continue;
                order.Add(i);
            }
            Shuffle(order, rng);
            var taken = new HashSet<int>();
            foreach (int i in order)
            {
                if (l.KeyDoors.Count >= want) break;
                var behind = new HashSet<int>(all);
                behind.ExceptWith(graph.Reach(l.LobbyRoom, i));
                if (behind.Count == 0 || behind.Count > s.KeyRegionRooms || behind.Contains(l.LobbyRoom) || behind.Overlaps(taken)) continue;
                if (i == l.VaultDoorway || (l.VaultRoom >= 0 && behind.Contains(l.VaultRoom))) continue; // the vault has a lock of its own
                if (i == l.StorageDoorway || (l.StorageRoom >= 0 && behind.Contains(l.StorageRoom))) continue; // so has the storage room
                // no room two storeys high behind it: from a gallery above a player drops in past the door — and is shut in
                bool atrium = false;
                foreach (int r in behind) atrium |= plan.Rooms[r].Levels > 1;
                if (atrium) continue;
                var d = l.Doorways[i];
                // the key: a room on this side, not the lobby, far from the door; on a table if there is one
                var keyRooms = new List<PlanRoom>();
                foreach (var r in plan.Rooms)
                    if (r.Id != l.LobbyRoom && r.Id != l.HiddenRoom && r.Id != l.VaultRoom && all.Contains(r.Id) && !behind.Contains(r.Id) && !taken.Contains(r.Id)
                        && (r.Center - d.Position).magnitude >= s.KeyMinDistance)
                        keyRooms.Add(r);
                if (keyRooms.Count == 0) continue;
                // deeper in than the door: the team goes past the locked door, further in, and back (RoomDepth)
                int nearSteps = int.MaxValue;
                if (l.RoomSteps != null)
                    foreach (int side in new[] { d.RoomA, d.RoomB })
                        if (side >= 0 && side < l.RoomSteps.Length && !behind.Contains(side)) nearSteps = l.RoomSteps[side];
                var deeper = keyRooms.FindAll(r => nearSteps != int.MaxValue && l.RoomSteps[r.Id] > nearSteps);
                if (deeper.Count > 0) keyRooms = deeper;
                var keyRoom = keyRooms[rng.Next(keyRooms.Count)];
                // on top of a table, a desk, a counter — never inside a cupboard or on a shelf of a rack; else on the floor
                Vector3? key = null;
                for (int k = 0; k < l.ItemSpots.Count; k++)
                {
                    if (l.Plan.RoomAt(l.ItemSpots[k] + Vector3.up * 0.3f) != keyRoom || !OnOpenTop(l, l.ItemSpots[k])) continue;
                    key = l.ItemSpots[k];
                    l.ItemSpots.RemoveAt(k);
                    break;
                }
                if (key == null)
                {
                    var floor = Stealth.Items.LootPlanner.FloorSpots(l, new HashSet<int> { keyRoom.Id }, 1, 0.6f);
                    key = floor.Count > 0 ? floor[0] : keyRoom.Center + Vector3.up * 0.02f;
                }
                int door = l.EnsureDoor(d);
                l.KeyDoors.Add(new KeyDoor { Door = door, Key = key.Value, Rooms = new List<int>(behind) });
                taken.UnionWith(behind);
            }
        }

        /// <summary>Furniture with an open top a thing lies on in plain sight.</summary>
        public static readonly HashSet<PropKind> OpenTops = new()
        {
            PropKind.Desk, PropKind.BarCounter, PropKind.Bench, PropKind.BenchIsland, PropKind.CoffeeTable, PropKind.ConferenceTable,
            PropKind.ConsoleDesk, PropKind.CorridorBench, PropKind.DiningTable, PropKind.DoctorDesk, PropKind.ExamTable, PropKind.ExecutiveDesk,
            PropKind.HallBench, PropKind.KitchenCounter, PropKind.KitchenIsland, PropKind.LabBench, PropKind.MeetingTable, PropKind.NightTable,
            PropKind.ReadingTable, PropKind.ReceptionDesk, PropKind.RoundTable, PropKind.SideTable, PropKind.StandingDesk,
            PropKind.SupervisorDesk, PropKind.WorkTable, PropKind.Workbench, PropKind.PingPongTable,
        };

        /// <summary>
        /// The spot lies on the open top of a table, a desk, a counter (pure): some furniture of <see cref="OpenTops"/> under it
        /// and nothing tall (a cupboard, a rack: <see cref="Furnisher.Tall"/>) over it.
        /// </summary>
        public static bool OnOpenTop(LevelLayout l, Vector3 spot)
        {
            bool open = false;
            foreach (var p in l.Props)
            {
                if (Mathf.Abs(p.Position.y - spot.y) > 2f) continue;
                var local = Quaternion.Inverse(Quaternion.Euler(0f, p.RotationY, 0f)) * (spot - p.Position);
                float scale = p.Scale > 0f ? p.Scale : 1f;
                if (!Furnisher.FootprintOf(p.Kind).Contains(new Vector2(local.x, local.z) / scale)) continue;
                if (Furnisher.Tall.Contains(p.Kind)) return false;
                open |= OpenTops.Contains(p.Kind) && spot.y > p.Position.y + 0.3f;
            }
            return open;
        }

        // ------------------------------------------------------------------ the main job

        /// <summary>A lock panel stands at hand height on the wall, facing into its room.</summary>
        public const float ConsoleHeight = 1.3f;
        /// <summary>The hostage's cage is this wide and deep (Prefabs/Props/Cage).</summary>
        public const float CageSize = 2.0f;

        /// <summary>
        /// The house's main job, planned before the furniture (its places are kept free): a vault — a room whose only way in
        /// is a door, locked until three lock panels on the walls of the nearest rooms outside are solved (one each:
        /// <see cref="VaultGame"/>), treasure in its middle — or a hostage — a cage in the middle of a big room, its key
        /// carried by one of the guards (<see cref="FinishObjective"/>). Which one comes from the seed; if it does not fit
        /// the house, the other. Own random stream.
        /// </summary>
        public static void PlanObjective(LevelLayout l, FeatureSettings s, Furnisher furnisher)
        {
            if (s == null || l.LevelIndex < s.ObjectivesFromHouse) return;
            var rng = ObjectiveRandom(l.Seed, l.LevelIndex, s, out bool vaultFirst);
            foreach (bool vault in vaultFirst ? new[] { true, false } : new[] { false, true })
            {
                if (vault && PlanVault(l, s, rng, furnisher)) { l.Objective = ObjectiveKind.Vault; return; }
                if (!vault && PlanCage(l, s, rng, furnisher)) { l.Objective = ObjectiveKind.Hostage; return; }
            }
        }

        /// <summary>Whether the house tries a vault before a hostage (the seed's choice, before the plan says what fits).</summary>
        public static bool VaultFirst(int seed, int house, FeatureSettings s)
        {
            ObjectiveRandom(seed, house, s, out bool vault);
            return vault;
        }

        static System.Random ObjectiveRandom(int seed, int house, FeatureSettings s, out bool vaultFirst)
        {
            // the house's number in too, and the first draw thrown away: close seeds give close first draws
            var rng = new System.Random(LevelRandom.Mix(seed, Salt.Objective, house + 1));
            rng.Next();
            vaultFirst = rng.NextDouble() < s.VaultShare;
            return rng;
        }

        static int ExitRoomId(LevelLayout l) => l.ExitRoom;

        static bool Plain(LevelLayout l, PlanRoom r) =>
            r.Id != l.LobbyRoom && r.Id != l.HiddenRoom && r.Id != ExitRoomId(l) && r.Levels == 1 &&
            !RoomTraits.Circulation(r.Type) && !RoomTraits.FloorTaken(r.Type) && !HasStair(l, r.Id);

        /// <summary>
        /// Makes the vault's doorway wide enough for the round bank door (<see cref="LevelMetrics.VaultDoorWidth"/>):
        /// where it is, or slid along its wall as little as possible — clear of the wall's ends, its other openings and the
        /// stairs on both sides. False (nothing changed) when the wall has no such place.
        /// </summary>
        public static bool WidenVaultDoorway(LevelLayout l, int opening) => WidenDoorway(l, opening, LevelMetrics.VaultDoorWidth);

        /// <summary>
        /// Makes a doorway <paramref name="width"/> wide (nominal): where it is, or slid along its wall as little as possible —
        /// clear of the wall's ends, its other openings and the stairs on both sides. False (nothing changed) when the wall has no such place.
        /// </summary>
        public static bool WidenDoorway(LevelLayout l, int opening, float width)
        {
            const float margin = 0.45f;
            var plan = l.Plan;
            var o = plan.Openings[opening];
            var w = plan.Walls[o.Wall];
            if (w.Railing || w.Shaped) return false;
            float lo = margin + width / 2f, hi = w.Length - margin - width / 2f;
            if (lo > hi) return false;
            var tries = new List<float>();
            float at0 = Mathf.Clamp(o.At, lo, hi);
            for (float step = 0f; step <= hi - lo + 0.001f; step += HousePlan.Res)
            {
                if (at0 + step <= hi + 0.001f) tries.Add(at0 + step);
                if (step > 0f && at0 - step >= lo - 0.001f) tries.Add(at0 - step);
            }
            var dir = w.Dir;
            var n = new Vector2(-dir.y, dir.x);
            foreach (float at in tries)
            {
                bool free = true;
                for (int k = 0; k < plan.Openings.Count && free; k++)
                {
                    var p = plan.Openings[k];
                    if (k == opening || p.Wall != o.Wall) continue;
                    if (p.Kind == EdgeType.Open || Mathf.Abs(p.At - at) < (p.Width + width) / 2f + margin) free = false;
                }
                // the way through stays clear of the stairs on both sides
                for (int k = 0; k < l.Stairs.Count && free; k++)
                {
                    var st = l.Stairs[k];
                    if (st.Floor != w.Floor && st.Floor + 1 != w.Floor) continue;
                    var area = PlanGenerator.StairArea(st, 0.1f);
                    for (float u = -width / 2f; u <= width / 2f + 0.001f && free; u += HousePlan.Res)
                        foreach (float side in new[] { -1f, 1f })
                            if (area.Contains(w.At(at + u) + n * (side * 0.8f))) free = false;
                }
                if (!free) continue;
                o.At = at;
                o.Width = width;
                plan.Openings[opening] = o;
                var d = l.Doorways[opening];
                var p3 = w.At(at);
                d.Position = new Vector3(p3.x, d.Position.y, p3.y);
                d.Width = width;
                l.Doorways[opening] = d;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The room has no opening but <paramref name="door"/>: no street door, vent or slot of its own (the reach test
        /// ignores the outside, so a vault with a door to the street passed it).
        /// </summary>
        public static bool OnlyWayIn(HousePlan plan, int room, int door)
        {
            for (int k = 0; k < plan.Openings.Count; k++)
            {
                if (k == door) continue;
                var w = plan.Walls[plan.Openings[k].Wall];
                if (w.RoomA == room || w.RoomB == room) return false;
            }
            return true;
        }

        static bool PlanVault(LevelLayout l, FeatureSettings s, System.Random rng, Furnisher furnisher)
        {
            var plan = l.Plan;
            var graph = new RoomGraph(plan, l.Stairs, Passage.Players);
            var all = graph.Reach(l.LobbyRoom);
            var order = new List<int>();
            for (int i = 0; i < plan.Openings.Count; i++)
            {
                var o = plan.Openings[i];
                var w = plan.Walls[o.Wall];
                if (o.Kind == EdgeType.Door && !w.Exterior && w.RoomA >= 0 && w.RoomB >= 0) order.Add(i);
            }
            Shuffle(order, rng);
            foreach (int i in order)
            {
                var behind = new HashSet<int>(all);
                behind.ExceptWith(graph.Reach(l.LobbyRoom, i));
                if (behind.Count != 1) continue;
                int v = 0;
                foreach (int id in behind) v = id;
                var room = plan.Rooms[v];
                if (!Plain(l, room) || room.Area < s.VaultArea.x || room.Area > s.VaultArea.y || !OnlyWayIn(plan, v, i)) continue;
                var w = plan.Walls[plan.Openings[i].Wall];
                int outside = w.RoomA == v ? w.RoomB : w.RoomA;
                var (oldOpening, oldDoorway) = (plan.Openings[i], l.Doorways[i]);
                if (!WidenVaultDoorway(l, i)) continue;
                var consoles = ConsolePlaces(l, v, outside);
                if (consoles.Count < 3)
                {
                    plan.Openings[i] = oldOpening;
                    l.Doorways[i] = oldDoorway;
                    continue;
                }
                l.VaultRoom = v;
                l.VaultDoorway = i;
                furnisher?.DoorwayMoved(l.Doorways[i]);
                foreach (var c in consoles)
                {
                    l.VaultConsoles.Add(c);
                    var front = c.position + c.rotation * Vector3.forward * 0.6f;
                    int floor = LevelLayout.FloorOf(c.position.y);
                    furnisher.Reserve(floor, new Vector3(front.x, LevelLayout.FloorY(floor), front.z), 0.55f);
                }
                // the treasure in a ring in the vault's middle (kept free of furniture)
                var centre = room.Center;
                furnisher.Reserve(room.Floor, centre, 1.0f);
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    var p = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (k % 2 == 0 ? 0.45f : 0.75f) + Vector3.up * 0.02f;
                    if (plan.RoomAt(p + Vector3.up * 0.3f)?.Id == v && plan.IsFloor(room.Floor, new Vector2(p.x, p.z))) l.VaultTreasure.Add(p);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// A place on a wall for a lock panel in each of the three rooms nearest to <paramref name="outside"/> (the room in
        /// front of the vault door, counted first): interior straight walls, away from openings, corners and stairs.
        /// </summary>
        static List<Pose> ConsolePlaces(LevelLayout l, int vault, int outside)
        {
            var plan = l.Plan;
            var result = new List<Pose>();
            // rooms by steps from the one in front of the vault (players' ways, not through the vault)
            var byDistance = new RoomGraph(plan, l.Stairs, Passage.Players).ByDistance(outside, new[] { vault });
            foreach (int r in byDistance)
            {
                if (result.Count >= 3) break;
                var room = plan.Rooms[r];
                if (r == l.LobbyRoom || r == l.HiddenRoom || r == ExitRoomId(l) || room.Levels > 1) continue;
                if (WallPlace(l, room, out var pose)) result.Add(pose);
            }
            return result;
        }

        /// <summary>The best spot on the room's walls for a panel: the middle-most free stretch of its longest interior wall.</summary>
        static bool WallPlace(LevelLayout l, PlanRoom room, out Pose pose)
        {
            var plan = l.Plan;
            pose = default;
            float best = -1f;
            float floorY = LevelLayout.FloorY(room.Floor);
            for (int wi = 0; wi < plan.Walls.Count; wi++)
            {
                var w = plan.Walls[wi];
                if (w.Floor != room.Floor || (w.RoomA != room.Id && w.RoomB != room.Id) || w.Exterior || w.Railing || w.Shaped || w.Length < 1.8f) continue;
                var d = w.Dir;
                var n2 = new Vector2(-d.y, d.x);
                var mid = w.At(w.Length / 2f);
                if (plan.RoomAt(new Vector3(mid.x + n2.x * 0.4f, floorY + 0.3f, mid.y + n2.y * 0.4f))?.Id != room.Id) n2 = -n2;
                for (float t = 0.8f; t <= w.Length - 0.8f; t += 0.25f)
                {
                    bool clear = true;
                    foreach (var o in plan.Openings)
                        if (o.Wall == wi && Mathf.Abs(o.At - t) < o.Width / 2f + 0.6f) { clear = false; break; }
                    if (!clear) continue;
                    var at = w.At(t) + n2 * (LevelMetrics.WallThickness / 2f);
                    var p = new Vector3(at.x, floorY + ConsoleHeight, at.y);
                    if (l.NearStair(room.Floor, p, 1.0f) || plan.RoomAt(new Vector3(p.x + n2.x * 0.6f, floorY + 0.3f, p.z + n2.y * 0.6f))?.Id != room.Id)
                        continue;
                    // the longest wall wins, then the spot nearest to its middle
                    float score = w.Length * 10f - Mathf.Abs(t - w.Length / 2f);
                    if (score <= best) continue;
                    best = score;
                    pose = new Pose(p, Quaternion.LookRotation(new Vector3(n2.x, 0f, n2.y)));
                }
            }
            return best >= 0f;
        }

        static bool PlanCage(LevelLayout l, FeatureSettings s, System.Random rng, Furnisher furnisher)
        {
            var plan = l.Plan;
            var options = new List<PlanRoom>();
            foreach (var r in plan.Rooms)
            {
                if (!Plain(l, r) || r.Area < s.HostageMinArea) continue;
                var c = r.Center;
                bool fits = plan.RoomAt(c + Vector3.up * 0.3f)?.Id == r.Id;
                float reach = (CageSize / 2f + 0.6f) * 1.42f;
                for (int k = 0; k < 8 && fits; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    var p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * reach;
                    fits = plan.RoomAt(p + Vector3.up * 0.3f)?.Id == r.Id && plan.IsFloor(r.Floor, new Vector2(p.x, p.z));
                }
                foreach (var d in l.Doorways)
                    if ((d.RoomA == r.Id || d.RoomB == r.Id) && (d.Position - c).magnitude < 2.4f) fits = false;
                if (fits) options.Add(r);
            }
            if (options.Count == 0) return false;
            // far from the way in: one of the three farthest from the entry hatch
            options.Sort((a, b) => (b.Center - l.HatchPosition).sqrMagnitude.CompareTo((a.Center - l.HatchPosition).sqrMagnitude));
            var room = options[rng.Next(Mathf.Min(3, options.Count))];
            var centre = room.Center;
            // its door towards the room's nearest doorway
            Vector3 toDoor = Vector3.forward;
            float near = float.MaxValue;
            foreach (var d in l.Doorways)
            {
                if (d.RoomA != room.Id && d.RoomB != room.Id) continue;
                float dist = (d.Position - centre).magnitude;
                if (dist < near) { near = dist; toDoor = d.Position - centre; }
            }
            toDoor.y = 0f;
            float yaw = Mathf.Round(Mathf.Atan2(toDoor.x, toDoor.z) * Mathf.Rad2Deg / 90f) * 90f;
            furnisher.Reserve(room.Floor, centre, CageSize / 2f + 0.7f);
            furnisher.Put(room, PropKind.Cage, centre, yaw);
            return true;
        }

        /// <summary>
        /// The main job after the security and the furniture: the vault's door becomes a door of its own (never locked for
        /// hacking, no laser in it: the panels open it); the cage's index; the guard who carries the cage's key (a guard
        /// that is there anyway, or one more on the ground floor).
        /// </summary>
        public static void FinishObjective(LevelLayout l)
        {
            var rng = new System.Random(LevelRandom.Mix(l.Seed, Salt.ObjectiveFinish, 1));
            if (l.Objective == ObjectiveKind.Vault)
            {
                var d = l.Doorways[l.VaultDoorway];
                int door = l.EnsureDoor(d);
                l.VaultDoor = door;
            }
            if (l.Objective != ObjectiveKind.Hostage) return;
            for (int i = 0; i < l.Props.Count; i++)
                if (l.Props[i].Kind == PropKind.Cage) { l.Cage = i; break; }
            // a guard carries it; with no guard, any robot that walks or rolls the house
            var carriers = new List<int>();
            for (int i = 0; i < l.RobotSpawns.Count; i++)
                if (l.RobotSpawns[i].Kind == RobotKind.Guard) carriers.Add(i);
            if (carriers.Count == 0)
                for (int i = 0; i < l.RobotSpawns.Count; i++)
                    if (!RobotRoster.IsDevice(l.RobotSpawns[i].Kind) && l.RobotSpawns[i].Kind != RobotKind.Hunter) carriers.Add(i);
            if (carriers.Count > 0) { l.KeyCarrier = carriers[rng.Next(carriers.Count)]; return; }
            // no robots at all (a calm house): the key lies in the room farthest from the cage
            var cage = l.Props[l.Cage].Position;
            float far = -1f;
            l.CageKeySpot = l.Lobby.Center;
            foreach (var p in l.ItemSpots)
            {
                float dist = (p - cage).magnitude;
                if (dist > far) { far = dist; l.CageKeySpot = p; }
            }
        }

        // ------------------------------------------------------------------ crates

        /// <summary>Some shelves, pallets and stacks of boxes become searchable crates (loot inside). After the furniture.</summary>
        public static void Crates(LevelLayout l, FeatureSettings s)
        {
            if (s == null) return;
            int want = Mathf.Min(s.MaxCrates, s.Crates + Mathf.FloorToInt(l.LevelIndex * s.CratesPerHouse));
            var rng = new System.Random(LevelRandom.Mix(l.Seed, Salt.Crates, 0));
            var options = new List<int>();
            for (int i = 0; i < l.Props.Count; i++)
                if (l.Props[i].Kind is PropKind.Pallet or PropKind.Shelf or PropKind.Boxes or PropKind.PaperStack
                    && (l.StorageRoom < 0 || l.Plan.RoomAt(l.Props[i].Position + Vector3.up * 0.3f)?.Id != l.StorageRoom)) // the storage room's shelves take the workers' loot
                    options.Add(i);
            // a house without a store room: a cabinet or a locker stands in (every house has something to search)
            if (options.Count == 0)
                for (int i = 0; i < l.Props.Count; i++)
                    if (l.Props[i].Kind is PropKind.Cabinet or PropKind.Lockers) options.Add(i);
            Shuffle(options, rng);
            foreach (int i in options)
            {
                if (l.Crates.Count >= want) break;
                var p = l.Props[i];
                // a stack of boxes leans to −X (its model): the crate stands in the middle of that footprint
                if (p.Kind == PropKind.Boxes) p.Position += Quaternion.Euler(0f, p.RotationY, 0f) * new Vector3(-0.24f, 0f, 0f);
                p.Kind = PropKind.Crate;
                l.Props[i] = p;
                l.Crates.Add(i);
            }
        }

        // ------------------------------------------------------------------ the point of interest

        /// <summary>
        /// The location's own place: a meeting table, a reception desk or a kitchen counter (big enough to stand in for it)
        /// becomes the robot programming console (office), the med table (hospital) or the workbench (garage); an apartment block
        /// has no such place (its find is the shelter map). After the furniture.
        /// </summary>
        /// <summary>The smallest the point of interest is made to fit a piece's place, while a bigger piece is to be had.</summary>
        public const float MinPointScale = 0.85f;

        /// <summary>
        /// Points of interest that are never made smaller (the garage's car upgrade station, 4.8 m with its gantry): put
        /// where they fit whole — against the wall of the piece they replace, inside its room, clear of the other furniture
        /// and of the doors. Shrunk to a tyre rack's footprint it was a toy among the furniture.
        /// </summary>
        public static readonly HashSet<PropKind> FullSize = new() { PropKind.Workbench };

        public static void PointOfInterest(LevelLayout l)
        {
            if (PointOfInterestKind(LocationOf(l.Kind)) is not PropKind kind) return;
            var rng = new System.Random(LevelRandom.Mix(l.Seed, Salt.PointOfInterest, 0));
            // a big piece of furniture with nothing standing on it stands in for the 2 m console / table / bench (not a desk
            // or a counter: lamps and appliances stand on those, and desks climb up to high vents); smaller than the
            // place — the place shrinks to its footprint
            var need = Furnisher.FootprintOf(kind);
            // the place is tall (Furnisher.Tall): never where it would stand in front of a window (a low piece may have)
            var windows = LevelBuilder.WindowSpots(l);
            bool AtWindow(Vector3 p)
            {
                foreach (var (w, _) in windows)
                    if (LevelLayout.FloorOf(w.y) == LevelLayout.FloorOf(p.y + 0.1f) && new Vector2(w.x - p.x, w.z - p.z).magnitude < Mathf.Max(need.width, need.height) / 2f + 1.2f) return true;
                return false;
            }
            // big enough first (a sofa's or a shelf's footprint shrank the medical table to a toy): at most a little smaller
            // than the place, then away from the hatch where the house allows it (a small first house has no "far")
            bool full = FullSize.Contains(kind);
            // the furnisher stood it whole against a wall already (Furnisher.Station)
            int placed = full ? l.Props.FindIndex(p => p.Kind == kind) : -1;
            if (placed >= 0) { l.PointOfInterest = placed; return; }
            if (full && PlaceWhole(l, kind, need, rng, AtWindow)) return;
            foreach (float least in new[] { MinPointScale, 0f })
            foreach (float away in new[] { 6f, 2.5f })
            // pieces that stand alone first: a table gives its place too, but its chairs stay around whatever replaces it
            foreach (var wanted in new[] { PropKind.PingPongTable, PropKind.PoolTable, PropKind.SectionalSofa, PropKind.AirHockey, PropKind.ReceptionDesk,
                         PropKind.Sofa, PropKind.StudioCouch, PropKind.Daybed, PropKind.BenchIsland, PropKind.MeetingTable, PropKind.ConferenceTable,
                         PropKind.Shelf, PropKind.MetalShelving,
                         PropKind.PokerTable, PropKind.ReadingTable, PropKind.DiningTable, PropKind.ConsoleDesk, PropKind.SupervisorDesk, PropKind.LabBench,
                         PropKind.RobotTable, PropKind.WorkTable, PropKind.BarCounter, PropKind.MixingDesk, PropKind.Foosball, PropKind.Credenza,
                         PropKind.AvCredenza, PropKind.HallBench, PropKind.DoctorDesk, PropKind.TireRack, PropKind.PaintRack })
            {
                var options = new List<int>();
                for (int i = 0; i < l.Props.Count; i++)
                    if (l.Props[i].Kind == wanted && !l.IsClimbPiece(l.Props[i].Position) && (l.Props[i].Position - l.HatchPosition).magnitude > away
                        && (!Furnisher.Tall.Contains(kind) || !AtWindow(l.Props[i].Position))) options.Add(i);
                var room = Furnisher.FootprintOf(wanted);
                if (options.Count == 0 || Mathf.Min(1f, room.width / need.width, room.height / need.height) < least) continue;
                int pick = options[rng.Next(options.Count)];
                var p = l.Props[pick];
                var have = Furnisher.FootprintOf(wanted);
                p.Kind = kind;
                p.Scale = Mathf.Min(1f, have.width / need.width, have.height / need.height);
                p.Position += Quaternion.Euler(0f, p.RotationY, 0f) * new Vector3(have.center.x - need.center.x * p.Scale, 0f, have.center.y - need.center.y * p.Scale);
                l.Props[pick] = p;
                l.PointOfInterest = pick;
                return;
            }
        }

        /// <summary>The pieces a whole point of interest may take the place of (any that stands against a wall).</summary>
        static readonly PropKind[] WholeOver =
        {
            PropKind.TireRack, PropKind.PaintRack, PropKind.WorkTable, PropKind.MetalShelving, PropKind.Shelf, PropKind.StorageRack,
            PropKind.Credenza, PropKind.LabBench, PropKind.ConsoleDesk, PropKind.SupervisorDesk, PropKind.HallBench, PropKind.Sofa,
            PropKind.ReceptionDesk, PropKind.BarCounter, PropKind.MeetingTable, PropKind.ConferenceTable, PropKind.DiningTable,
        };

        /// <summary>
        /// A <see cref="FullSize"/> point of interest at full size in the place of a piece (see <see cref="WholeAt"/>), away
        /// from the hatch where it can be; false: nowhere whole (the caller falls back to a smaller one).
        /// </summary>
        static bool PlaceWhole(LevelLayout l, PropKind kind, Rect need, System.Random rng, System.Func<Vector3, bool> atWindow)
        {
            foreach (float away in new[] { 6f, 2.5f, 0f })
            {
                var options = new List<(int index, Vector3 at)>();
                for (int i = 0; i < l.Props.Count; i++)
                {
                    var p = l.Props[i];
                    if (System.Array.IndexOf(WholeOver, p.Kind) < 0 || l.IsClimbPiece(p.Position) || (p.Position - l.HatchPosition).magnitude <= away) continue;
                    if (WholeAt(l, i, need) is not Vector3 at || (Furnisher.Tall.Contains(kind) && atWindow(at))) continue;
                    options.Add((i, at));
                }
                if (options.Count == 0) continue;
                var (pick, spot) = options[rng.Next(options.Count)];
                var piece = l.Props[pick];
                piece.Kind = kind;
                piece.Scale = 1f;
                piece.Position = spot;
                l.Props[pick] = piece;
                l.PointOfInterest = pick;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Where a whole piece of footprint <paramref name="need"/> stands in the place of prop <paramref name="index"/>, or
        /// null if it does not fit (pure): its back against the same wall (the footprints' backs, local −Z, lined up), its
        /// middle on the old piece's; every point of it in that piece's room, no other piece's footprint on it, no doorway
        /// or hatch within reach of its front.
        /// </summary>
        public static Vector3? WholeAt(LevelLayout l, int index, Rect need)
        {
            var p = l.Props[index];
            var have = Furnisher.FootprintOf(p.Kind);
            float s = p.Scale > 0f ? p.Scale : 1f;
            var turn = Quaternion.Euler(0f, p.RotationY, 0f);
            var at = p.Position + turn * new Vector3(have.center.x * s - need.center.x, 0f, have.yMin * s - need.yMin);
            var room = l.Plan?.RoomAt(p.Position + Vector3.up * 0.3f);
            if (room == null) return null;
            // every point of it in the room (an L-shaped room's box is not the room)
            const int n = 6;
            for (int i = 0; i <= n; i++)
                for (int k = 0; k <= 2; k++)
                {
                    var local = new Vector3(Mathf.Lerp(need.xMin, need.xMax, i / (float)n), 0.3f, Mathf.Lerp(need.yMin, need.yMax, k / 2f));
                    var q = l.Plan.RoomAt(at + turn * local);
                    if (q == null || q.Id != room.Id) return null;
                }
            var box = Box(at, turn, need, 1f);
            for (int j = 0; j < l.Props.Count; j++)
            {
                if (j == index) continue;
                var o = l.Props[j];
                if (Mathf.Abs(o.Position.y - p.Position.y) > 1.5f) continue;
                var ob = Box(o.Position, Quaternion.Euler(0f, o.RotationY, 0f), Furnisher.FootprintOf(o.Kind), o.Scale > 0f ? o.Scale : 1f);
                if (ob.Overlaps(Shrink(box, 0.03f))) return null;
            }
            var front = Shrink(box, -0.9f);                                // a step round it: doors open, hatches are reached
            foreach (var d in l.AutoDoors)
                if (Mathf.Abs(d.position.y - p.Position.y) < 1.5f && front.Contains(new Vector2(d.position.x, d.position.z))) return null;
            foreach (var h in new[] { l.HatchPosition, l.ExitHatchPosition })
                if (Mathf.Abs(h.y - p.Position.y) < 1.5f && front.Contains(new Vector2(h.x, h.z))) return null;
            return at;
        }

        /// <summary>A footprint turned and placed, as a box in world x/z (its turn is in steps of 90° in a plan).</summary>
        static Rect Box(Vector3 at, Quaternion turn, Rect foot, float scale)
        {
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var c in new[] { new Vector3(foot.xMin, 0f, foot.yMin), new Vector3(foot.xMax, 0f, foot.yMin), new Vector3(foot.xMin, 0f, foot.yMax), new Vector3(foot.xMax, 0f, foot.yMax) })
            {
                var w = at + turn * (c * scale);
                x0 = Mathf.Min(x0, w.x); x1 = Mathf.Max(x1, w.x);
                z0 = Mathf.Min(z0, w.z); z1 = Mathf.Max(z1, w.z);
            }
            return Rect.MinMaxRect(x0, z0, x1, z1);
        }

        static Rect Shrink(Rect r, float by) => Rect.MinMaxRect(r.xMin + by, r.yMin + by, r.xMax - by, r.yMax - by);

        // ------------------------------------------------------------------ wardens and lures

        /// <summary>
        /// Wardens: in front of the only doorway of a room with loot in it (a dead end), outside, a few steps either way
        /// along the wall, watching the way in. Lure speakers lie a little away from them (to switch on and draw them off),
        /// and a few more anywhere. After the furniture (it needs the loot spots).
        /// </summary>
        public static void Wardens(LevelLayout l, FeatureSettings s)
        {
            if (s == null) return;
            int want = Mathf.Min(s.MaxWardens, s.Wardens + Mathf.FloorToInt(l.LevelIndex * s.WardensPerHouse));
            var rng = new System.Random(LevelRandom.Mix(l.Seed, Salt.Wardens, 0));
            var plan = l.Plan;
            var rooms = new List<PlanRoom>();
            foreach (var r in plan.Rooms)
            {
                if (r.Id == l.LobbyRoom || r.Id == l.HiddenRoom || r.Levels > 1 || r.Type is RoomType.Corridor or RoomType.Hall) continue;
                int spots = 0;
                foreach (var p in l.ItemSpots) if (plan.RoomAt(p + Vector3.up * 0.3f) == r) spots++;
                if (spots >= 2) rooms.Add(r);
            }
            Shuffle(rooms, rng);
            var posts = new List<Vector3>();
            foreach (var r in rooms)
            {
                if (l.Wardens.Count >= want) break;
                int only = -1, count = 0;
                for (int i = 0; i < l.Doorways.Count; i++)
                {
                    var d = l.Doorways[i];
                    if (d.RoomA != r.Id && d.RoomB != r.Id) continue;
                    if (d.Kind is EdgeType.Door or EdgeType.Open or EdgeType.Breach) { only = i; count++; }
                }
                if (count != 1) continue;
                var door = l.Doorways[only];
                int outsideRoom = door.RoomA == r.Id ? door.RoomB : door.RoomA;
                if (outsideRoom < 0) continue;
                var across = door.Rotation * Vector3.forward;
                var outward = plan.RoomAt(door.Position + across * 0.8f + Vector3.up * 0.3f)?.Id == outsideRoom ? across : -across;
                var along = door.Rotation * Vector3.right;
                var post = door.Position + outward * 2.0f;
                bool Free(Vector3 p) => plan.RoomAt(p + Vector3.up * 0.3f)?.Id == outsideRoom && !l.NearStair(door.Floor, p, 0.6f) && plan.IsFloor(door.Floor, new Vector2(p.x, p.z));
                if (!Free(post) || (post - l.HatchPosition).magnitude < 13f || (post - l.ExitHatchPosition).magnitude < 9f) continue;
                bool overlaps = false;
                foreach (var other in posts) if ((other - post).magnitude < 6f) overlaps = true;
                if (overlaps) continue;
                var a = post - along * 1.3f;
                var b = post + along * 1.3f;
                if (!Free(a) || !Free(b)) a = b = post;
                posts.Add(post);
                l.Wardens.Add(new WardenPost { A = a, B = b, Facing = outward, Room = r.Id, Door = only });
                l.RobotSpawns.Add(new RobotSpawn { Kind = RobotKind.Warden, Floor = door.Floor, Position = post, Rotation = Quaternion.LookRotation(outward) });

                // lures around it on its floor, outside the room it guards
                var near = new List<Vector3>();
                foreach (var p in l.PatrolPoints)
                {
                    float dist = (p - post).magnitude;
                    if (LevelLayout.FloorOf(p.y) == door.Floor && dist >= s.LureDistance.x && dist <= s.LureDistance.y && plan.RoomAt(p + Vector3.up * 0.3f)?.Id != r.Id)
                        near.Add(p + Vector3.up * 0.02f);
                }
                Shuffle(near, rng);
                for (int k = 0; k < Mathf.Min(s.LuresPerWarden, near.Count); k++) l.LureSpots.Add(near[k] + Quaternion.Euler(0, k * 120f, 0) * Vector3.forward * 0.9f);
            }
            var anywhere = new List<Vector3>(l.ItemSpots);
            Shuffle(anywhere, rng);
            for (int k = 0; k < Mathf.Min(s.ExtraLures, anywhere.Count); k++)
            {
                l.LureSpots.Add(anywhere[k]);
                l.ItemSpots.Remove(anywhere[k]);
            }
        }
    }
}
