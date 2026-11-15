using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>The three kinds of place a house can be; each has its own point of interest.</summary>
    public enum Location : byte { Office, Hospital, Garage }

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
    }

    /// <summary>
    /// The special places of a house (pure, from the seed; each with its own random stream so the rest of the house does
    /// not shift): a hidden room behind illusion walls, key doors that close off a part of the house, searchable crates,
    /// wardens in front of loot rooms with lure speakers around them, and the location's point of interest (the robot
    /// programming console in an office, the med table in a hospital, the workbench in a garage).
    /// </summary>
    public static class HouseFeatures
    {
        public static Location LocationOf(HouseKind kind) => kind switch
        {
            HouseKind.Hospital => Location.Hospital,
            HouseKind.Garage => Location.Garage,
            _ => Location.Office,
        };

        public static PropKind PointOfInterestKind(Location location) => location switch
        {
            Location.Hospital => PropKind.MedTable,
            Location.Garage => PropKind.Workbench,
            _ => PropKind.Programmer,
        };

        static bool PlayerPasses(EdgeType k) => k != EdgeType.Wall;
        static bool RobotPasses(EdgeType k) => k is EdgeType.Door or EdgeType.Open;

        /// <summary>
        /// Rooms reached from <paramref name="start"/> through the openings (players: every kind; robots: doors and open
        /// passages) and the stairs, leaving out the opening <paramref name="skip"/> and the rooms in <paramref name="closed"/>.
        /// </summary>
        public static HashSet<int> Reach(HousePlan plan, IReadOnlyList<Stair> stairs, int start, bool player, int skip = -1,
            ICollection<int> closed = null)
        {
            var adjacent = new Dictionary<int, List<int>>();
            void Link(int a, int b)
            {
                if (a < 0 || b < 0) return;
                if (!adjacent.TryGetValue(a, out var la)) adjacent[a] = la = new List<int>();
                if (!adjacent.TryGetValue(b, out var lb)) adjacent[b] = lb = new List<int>();
                la.Add(b);
                lb.Add(a);
            }
            for (int i = 0; i < plan.Openings.Count; i++)
            {
                var o = plan.Openings[i];
                if (i == skip || !(player ? PlayerPasses(o.Kind) : RobotPasses(o.Kind))) continue;
                var w = plan.Walls[o.Wall];
                Link(w.RoomA, w.RoomB);
            }
            foreach (var s in stairs) Link(s.Room, s.RoomAbove);
            var seen = new HashSet<int> { start };
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int r = queue.Dequeue();
                if (!adjacent.TryGetValue(r, out var next)) continue;
                foreach (var n in next)
                    if ((closed == null || !closed.Contains(n)) && seen.Add(n)) queue.Enqueue(n);
            }
            return seen;
        }

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
            var rng = new System.Random(LevelRandom.Mix(l.Seed, 0x41DE, 0));
            if (rng.NextDouble() >= s.HiddenRoomChance) return -1;
            var plan = l.Plan;
            var candidates = new List<PlanRoom>();
            foreach (var r in plan.Rooms)
            {
                if (r.Id == l.LobbyRoom || r.Levels > 1 || r.Type is RoomType.Corridor or RoomType.Hall or RoomType.Lobby) continue;
                if (r.Area < s.HiddenRoomMinArea || r.Area > s.HiddenRoomMaxArea || HasStair(l, r.Id)) continue;
                bool enclosed = true;
                int walls = 0;
                foreach (var w in plan.Walls)
                {
                    if (w.RoomA != r.Id && w.RoomB != r.Id) continue;
                    int other = w.RoomA == r.Id ? w.RoomB : w.RoomA;
                    if (w.Exterior || w.Railing || other < 0) { enclosed = false; break; }
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
            var rng = new System.Random(LevelRandom.Mix(l.Seed, 0x6E7D, 0));
            var all = Reach(plan, l.Stairs, l.LobbyRoom, true);
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
                behind.ExceptWith(Reach(plan, l.Stairs, l.LobbyRoom, true, i));
                if (behind.Count == 0 || behind.Count > s.KeyRegionRooms || behind.Contains(l.LobbyRoom) || behind.Overlaps(taken)) continue;
                if (i == l.VaultDoorway || (l.VaultRoom >= 0 && behind.Contains(l.VaultRoom))) continue; // the vault has a lock of its own
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
                var keyRoom = keyRooms[rng.Next(keyRooms.Count)];
                var key = keyRoom.Center + Vector3.up * 0.02f;
                for (int k = 0; k < l.ItemSpots.Count; k++)
                {
                    if (l.Plan.RoomAt(l.ItemSpots[k] + Vector3.up * 0.3f) != keyRoom) continue;
                    key = l.ItemSpots[k];
                    l.ItemSpots.RemoveAt(k);
                    break;
                }
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
                l.KeyDoors.Add(new KeyDoor { Door = door, Key = key, Rooms = new List<int>(behind) });
                taken.UnionWith(behind);
            }
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
            var rng = new System.Random(LevelRandom.Mix(seed, 0x0B7E, house + 1));
            rng.Next();
            vaultFirst = rng.NextDouble() < s.VaultShare;
            return rng;
        }

        static int ExitRoomId(LevelLayout l) => l.Plan.RoomAt(l.ExitHatchPosition + Vector3.up * 0.3f)?.Id ?? -1;

        static bool Plain(LevelLayout l, PlanRoom r) =>
            r.Id != l.LobbyRoom && r.Id != l.HiddenRoom && r.Id != ExitRoomId(l) && r.Levels == 1 &&
            r.Type is not (RoomType.Corridor or RoomType.Hall or RoomType.Lobby) && !HasStair(l, r.Id);

        /// <summary>
        /// Makes the vault's doorway wide enough for the round bank door (<see cref="LevelMetrics.VaultDoorWidth"/>):
        /// where it is, or slid along its wall as little as possible — clear of the wall's ends, its other openings and the
        /// stairs on both sides. False (nothing changed) when the wall has no such place.
        /// </summary>
        public static bool WidenVaultDoorway(LevelLayout l, int opening)
        {
            const float margin = 0.45f, width = LevelMetrics.VaultDoorWidth;
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

        static bool PlanVault(LevelLayout l, FeatureSettings s, System.Random rng, Furnisher furnisher)
        {
            var plan = l.Plan;
            var all = Reach(plan, l.Stairs, l.LobbyRoom, true);
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
                behind.ExceptWith(Reach(plan, l.Stairs, l.LobbyRoom, true, i));
                if (behind.Count != 1) continue;
                int v = 0;
                foreach (int id in behind) v = id;
                var room = plan.Rooms[v];
                if (!Plain(l, room) || room.Area < s.VaultArea.x || room.Area > s.VaultArea.y) continue;
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
            var adjacent = new Dictionary<int, List<int>>();
            void Link(int a, int b)
            {
                if (a < 0 || b < 0) return;
                if (!adjacent.TryGetValue(a, out var la)) adjacent[a] = la = new List<int>();
                if (!adjacent.TryGetValue(b, out var lb)) adjacent[b] = lb = new List<int>();
                la.Add(b);
                lb.Add(a);
            }
            foreach (var o in plan.Openings)
                if (PlayerPasses(o.Kind)) Link(plan.Walls[o.Wall].RoomA, plan.Walls[o.Wall].RoomB);
            foreach (var st in l.Stairs) Link(st.Room, st.RoomAbove);
            // rooms by steps from the one in front of the vault (players' ways, not through the vault)
            var seen = new HashSet<int> { outside, vault };
            var queue = new Queue<int>();
            queue.Enqueue(outside);
            var byDistance = new List<int>();
            while (queue.Count > 0)
            {
                int r = queue.Dequeue();
                byDistance.Add(r);
                if (!adjacent.TryGetValue(r, out var next)) continue;
                foreach (int n in next)
                    if (seen.Add(n)) queue.Enqueue(n);
            }
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
            var rng = new System.Random(LevelRandom.Mix(l.Seed, 0x0B7E, 1));
            if (l.Objective == ObjectiveKind.Vault)
            {
                var d = l.Doorways[l.VaultDoorway];
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
            var rng = new System.Random(LevelRandom.Mix(l.Seed, 0xC7A7, 0));
            var options = new List<int>();
            for (int i = 0; i < l.Props.Count; i++)
                if (l.Props[i].Kind is PropKind.Pallet or PropKind.Shelf or PropKind.Boxes or PropKind.PaperStack) options.Add(i);
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
        /// becomes the robot programming console (office), the med table (hospital) or the workbench (garage). After the furniture.
        /// </summary>
        public static void PointOfInterest(LevelLayout l)
        {
            var kind = PointOfInterestKind(LocationOf(l.Kind));
            var rng = new System.Random(LevelRandom.Mix(l.Seed, 0x9017, 0));
            // a big piece of furniture with nothing standing on it stands in for the 2 m console / table / bench (not a desk
            // or a counter: lamps and appliances stand on those, and desks climb up to high vents); smaller than the
            // place — the place shrinks to its footprint
            var need = Furnisher.FootprintOf(kind);
            // away from the hatch where the house allows it; a small first house has no "far"
            foreach (float away in new[] { 6f, 2.5f })
            foreach (var wanted in new[] { PropKind.MeetingTable, PropKind.ConferenceTable, PropKind.PingPongTable, PropKind.ReceptionDesk, PropKind.Sofa,
                         PropKind.SectionalSofa, PropKind.StudioCouch, PropKind.Shelf, PropKind.MetalShelving, PropKind.PoolTable, PropKind.AirHockey,
                         PropKind.PokerTable, PropKind.ReadingTable, PropKind.DiningTable, PropKind.ConsoleDesk, PropKind.SupervisorDesk, PropKind.LabBench,
                         PropKind.RobotTable, PropKind.WorkTable, PropKind.BarCounter, PropKind.MixingDesk, PropKind.Foosball, PropKind.Credenza,
                         PropKind.AvCredenza, PropKind.HallBench, PropKind.BenchIsland, PropKind.Daybed, PropKind.DoctorDesk })
            {
                var options = new List<int>();
                for (int i = 0; i < l.Props.Count; i++)
                    if (l.Props[i].Kind == wanted && (l.Props[i].Position - l.HatchPosition).magnitude > away) options.Add(i);
                if (options.Count == 0) continue;
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
            var rng = new System.Random(LevelRandom.Mix(l.Seed, 0x3A7D, 0));
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
                    if (d.Kind is EdgeType.Door or EdgeType.Open) { only = i; count++; }
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
