using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>Tuning of the traps on the house's valuable rooms (new enemy mode).</summary>
    [System.Serializable]
    public class WardSettings
    {
        [Tooltip("Valuable rooms guarded by traps in the first house (the vault and the hostage's room always are).")]
        public int Rooms = 2;
        [Tooltip("More guarded rooms per house.")]
        public float RoomsPerHouse = 0.5f;
        public int MaxRooms = 6;
        [Tooltip("Chance of a laser across a guarded room's door (else a pressure plate just inside it).")]
        [Range(0f, 1f)] public float LaserChance = 0.5f;
        [Tooltip("Chance of a camera watching the loot (else a motion sensor in a corner).")]
        [Range(0f, 1f)] public float CameraChance = 0.4f;
    }

    /// <summary>
    /// Traps on the valuable rooms (pure, own stream; the new enemy mode — the guards sleep in their charging room, so
    /// what is worth stealing is guarded by devices instead): the rooms are ranked by what lies in them — the vault, the
    /// hostage's cage, the hidden room first, then by the loot's value bonus (<see cref="RoomRules"/>), depth and the
    /// number of things to take — and the top ones get a ward: a laser across the way in or a pressure plate just inside
    /// it, and a camera or a motion sensor watching the room.
    /// </summary>
    public static class Wards
    {
        /// <summary>Nothing hostile closer than this to the entry hatch on the ground floor, metres (like every device).</summary>
        const float NearHatch = 12.5f, CameraNearHatch = 8.5f;

        /// <summary>A room's worth to a thief (pure): what decides which rooms are guarded.</summary>
        public static float Worth(LevelLayout l, PlanRoom r, int cageRoom)
        {
            if (r.Id == l.VaultRoom) return 1000f;
            if (r.Id == cageRoom) return 900f;
            if (r.Id == l.HiddenRoom) return 800f;
            int items = 0;
            foreach (var p in l.ItemSpots)
                if (l.Plan.RoomAt(p + Vector3.up * 0.3f)?.Id == r.Id) items++;
            if (items == 0) return 0f;
            return RoomRules.For(r.Type, l.RoomRules).LootBonus + 10f * l.DepthShare(r.Id) + Mathf.Min(items, 6) * 2f;
        }

        /// <summary>The rooms worth guarding, best first (no corridors, halls, the lobby, storage or charging room).</summary>
        public static List<PlanRoom> Ranked(LevelLayout l)
        {
            int cageRoom = -1;
            foreach (var p in l.Props)
                if (p.Kind == PropKind.Cage) { cageRoom = l.Plan.RoomAt(p.Position + Vector3.up * 0.3f)?.Id ?? -1; break; }
            var list = new List<(PlanRoom room, float worth)>();
            foreach (var r in l.Plan.Rooms)
            {
                if (r.Id == l.LobbyRoom || r.Id == l.StorageRoom || r.Id == l.ChargingRoom) continue;
                if (r.Type is RoomType.Corridor or RoomType.Hall or RoomType.Lobby) continue;
                float w = Worth(l, r, cageRoom);
                if (w > 0f) list.Add((r, w));
            }
            list.Sort((a, b) => b.worth.CompareTo(a.worth) != 0 ? b.worth.CompareTo(a.worth) : a.room.Id.CompareTo(b.room.Id));
            var result = new List<PlanRoom>();
            foreach (var (room, _) in list) result.Add(room);
            return result;
        }

        /// <summary>How many rooms get a ward in this house.</summary>
        public static int Count(WardSettings s, int house) => Mathf.Min(s.MaxRooms, s.Rooms + Mathf.FloorToInt(house * s.RoomsPerHouse));

        /// <summary>
        /// Places the wards: lasers (taken out of <paramref name="doors"/>, so no sliding door goes there), pressure plates
        /// and motion sensors (<see cref="LevelLayout.RobotSpawns"/>), cameras (<see cref="LevelLayout.CameraSpots"/>).
        /// Returns the guarded rooms.
        /// </summary>
        public static List<int> Place(LevelLayout l, List<LaserSpot> doors, WardSettings s, int movingLasersFromHouse)
        {
            s ??= new WardSettings();
            var rng = new System.Random(LevelRandom.Mix(l.Seed, 0x3A2D, 0));
            var guarded = new List<int>();
            var ranked = Ranked(l);
            int want = Count(s, l.LevelIndex);
            foreach (var room in ranked)
            {
                bool must = room.Id == l.VaultRoom || Worth(l, room, -1) >= 800f || IsCageRoom(l, room);
                if (guarded.Count >= want && !must) break;
                // by the team's way in nothing hostile starts (the vault and the hostage are never there)
                if (!must && room.Floor == 0 && Vector3.Distance(room.Center, l.HatchPosition) < NearHatch) continue;
                guarded.Add(room.Id);
                bool any = false;
                float y = LevelLayout.FloorY(room.Floor);

                // the way in: a laser across the doorway, or a plate just inside it
                int doorIndex = -1;
                for (int i = 0; i < doors.Count; i++)
                {
                    var d = l.Plan.RoomAt(doors[i].Position + doors[i].Rotation * Vector3.forward * 0.6f + Vector3.up * 0.3f);
                    var e = l.Plan.RoomAt(doors[i].Position - doors[i].Rotation * Vector3.forward * 0.6f + Vector3.up * 0.3f);
                    if (d?.Id == room.Id || e?.Id == room.Id) { doorIndex = i; break; }
                }
                bool vault = room.Id == l.VaultRoom; // its round door: no laser in it — the plate waits inside
                if (doorIndex >= 0 && !vault && rng.NextDouble() < s.LaserChance)
                {
                    var spot = doors[doorIndex];
                    doors.RemoveAt(doorIndex);
                    spot.Kind = (LaserPattern)rng.Next(l.LevelIndex >= movingLasersFromHouse ? 3 : 2);
                    l.LaserSpots.Add(spot);
                    any = true;
                }
                else
                {
                    var entry = EntryOf(l, room);
                    if (entry.HasValue)
                    {
                        var (at, inward) = entry.Value;
                        var plate = at + inward * 1.1f;
                        plate.y = y;
                        if (room.Floor != 0 || Vector3.Distance(plate, l.HatchPosition) > NearHatch) any = true;
                        if (room.Floor != 0 || Vector3.Distance(plate, l.HatchPosition) > NearHatch) l.RobotSpawns.Add(new RobotSpawn { Kind = RobotKind.PressurePlate, Floor = room.Floor, Position = plate, Rotation = Quaternion.LookRotation(inward) });
                    }
                }

                // a watcher over the loot: a camera under the ceiling or a motion sensor in a corner, looking in
                var (corner, look) = Corner(l, room, rng);
                float fromHatch = room.Floor == 0 ? Vector3.Distance(corner, l.HatchPosition) : float.MaxValue;
                bool near = fromHatch <= CameraNearHatch;
                if (near) { } // the team's way in: no watcher there (the trap at the door does it)
                else if (fromHatch <= NearHatch || rng.NextDouble() < s.CameraChance) // (a camera may hang closer than a sensor)
                    l.CameraSpots.Add(new Pose(corner + Vector3.up * (y + LevelMetrics.WallHeight - 0.3f), Quaternion.LookRotation(look)));
                else
                    l.RobotSpawns.Add(new RobotSpawn { Kind = RobotKind.MotionSensor, Floor = room.Floor, Position = corner + Vector3.up * (y + 2.2f), Rotation = Quaternion.LookRotation(look) });
                any |= !near;
                if (!any) guarded.Remove(room.Id); // all of it would have stood by the hatch: not guarded after all
            }
            l.WardedRooms.AddRange(guarded);
            return guarded;
        }

        static bool IsCageRoom(LevelLayout l, PlanRoom room)
        {
            foreach (var p in l.Props)
                if (p.Kind == PropKind.Cage && l.Plan.RoomAt(p.Position + Vector3.up * 0.3f)?.Id == room.Id) return true;
            return false;
        }

        /// <summary>The room's main way in (a door, an open passage or a breach) and the direction into the room.</summary>
        static (Vector3 at, Vector3 inward)? EntryOf(LevelLayout l, PlanRoom room)
        {
            foreach (var d in l.Doorways)
            {
                if (d.Kind is not (EdgeType.Door or EdgeType.Open or EdgeType.Breach) || (d.RoomA != room.Id && d.RoomB != room.Id)) continue;
                var across = d.Rotation * Vector3.forward;
                across.y = 0f;
                var to = room.Center - d.Position;
                var inward = Vector3.Dot(to, across) >= 0f ? across.normalized : -across.normalized;
                return (d.Position, inward);
            }
            return null;
        }

        /// <summary>A corner of the room (0.45 m in from both walls, inside the room itself, flat) and the direction to its middle.</summary>
        static (Vector3 corner, Vector3 look) Corner(LevelLayout l, PlanRoom room, System.Random rng)
        {
            var b = room.Bounds;
            int first = rng.Next(4);
            Vector3? best = null;
            float bestFar = -1f;
            for (int k = 0; k < 4; k++)
            {
                int c4 = (first + k) % 4;
                var c = new Vector3((c4 & 1) == 0 ? b.xMin + 0.45f : b.xMax - 0.45f, 0f, (c4 & 2) == 0 ? b.yMin + 0.45f : b.yMax - 0.45f);
                if (l.Plan.SpaceAt(room.Floor, new Vector2(c.x, c.z)) != room.Id) continue; // an L-shaped room: not this corner
                // upstairs any corner (the first found, by chance); on the ground floor the one farthest from the hatch
                float far = room.Floor == 0 ? Vector3.Distance(c, new Vector3(l.HatchPosition.x, 0f, l.HatchPosition.z)) : 0f;
                if (best == null || far > bestFar + 0.01f) { best = c; bestFar = far; }
                if (room.Floor != 0) break;
            }
            if (best is Vector3 at)
            {
                var look = new Vector3(room.Center.x, 0f, room.Center.z) - at;
                if (look.sqrMagnitude < 0.01f) look = Vector3.forward;
                return (at, look.normalized);
            }
            return (new Vector3(room.Center.x, 0f, room.Center.z), Vector3.forward);
        }
    }
}
