using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The robots' charging room (pure, own stream), made in two steps, as the robots made it: <see cref="Convert"/> picks a
    /// room the humans used for machines — a server room, a lab, a copy room (an office if the house has none of those)
    /// — and takes it over (<see cref="RoomType.ChargingStation"/>, <see cref="LevelLayout.ChargingFrom"/> remembers what
    /// it was); the furnisher then fills it with what the robots built — charging docks, battery stacks, a generator,
    /// cable masses — around one piece the humans left (<see cref="Remnant"/>). After the furniture <see cref="Docks"/>
    /// reads the docks: where the guards rest (<see cref="LevelLayout.ChargingDocks"/>). Every house has one.
    /// </summary>
    public static class ChargingRooms
    {
        /// <summary>The smallest room the robots take, m² (docks along a wall, the generator, a way through).</summary>
        public const float MinArea = 14f;

        /// <summary>How much the robots want a room of this type (0: never): the machine rooms first.</summary>
        public static float Preference(RoomType t) => t switch
        {
            RoomType.Server => 4f,
            RoomType.Lab => 3.5f,
            RoomType.CopyRoom => 3f,
            RoomType.Office => 1f,
            RoomType.OpenSpace => 0.6f,
            RoomType.Meeting => 0.4f,
            // the houses with plans of their own: the hospital's machine rooms, the shop's offices, a flat's rooms
            RoomType.Imaging => 3f,
            RoomType.Infirmary => 2f,
            RoomType.Ward => 0.8f,
            // a flat's room: only on the apartment block's ground floor (Convert), where nothing else is a machine room
            RoomType.Bedroom => 0.4f,
            RoomType.Living => 0.35f,
            RoomType.Kitchen => 0.3f,
            _ => 0f,
        };

        /// <summary>
        /// Picks the room and takes it over (after the zoning and the control room, before the patrols and the
        /// furniture). Not the lobby, the vault, the hidden room, the storage room, the exit's room, the hostage's room,
        /// nor a room on the stairs. Deep rooms first (the robots keep to the inside of the house). Returns the room or null.
        /// </summary>
        public static PlanRoom Convert(LevelLayout l, Vector3 exitHatch)
        {
            const float AwayFromHatch = 14f; // the guards rest there: nothing hostile starts by the team's way in
            var rng = new System.Random(LevelRandom.Mix(l.Seed, 0xC4A6, 0));
            PlanRoom best = null;
            float bestScore = 0f;
            // a second pass without keeping away from the team's way in (a small house: every room is near it)
            for (int pass = 0; pass < 2 && best == null; pass++)
            foreach (var r in l.Plan.Rooms)
            {
                float pref = Preference(r.Type);
                if (pref <= 0f || r.Area < MinArea || r.Levels != 1) continue;
                // the apartment block's floors above the ground are one typical floor: the robots take a room down below
                if (l.Kind == HouseKind.Apartments && r.Floor != 0) continue;
                if (r.Id == l.LobbyRoom || r.Id == l.VaultRoom || r.Id == l.HiddenRoom || r.Id == l.StorageRoom) continue;
                if (l.Plan.RoomAt(exitHatch + Vector3.up * 0.3f)?.Id == r.Id) continue;
                bool onStairs = false;
                foreach (var st in l.Stairs) onStairs |= st.Room == r.Id || st.RoomAbove == r.Id;
                if (onStairs || HasCage(l, r)) continue;
                if (pass == 0 && r.Floor == 0 && Vector3.Distance(r.Center, l.HatchPosition) < AwayFromHatch) continue;
                float aspect = Mathf.Max(r.Bounds.width, r.Bounds.height) / Mathf.Max(0.5f, Mathf.Min(r.Bounds.width, r.Bounds.height));
                if (aspect > 3.2f) continue; // a strip of a room: no place for the docks and a way past them
                float score = pref * (1f + l.DepthShare(r.Id)) * (1f + 0.2f * (float)rng.NextDouble()) * Mathf.Min(1.3f, Mathf.Sqrt(r.Area / 20f));
                if (score > bestScore) { bestScore = score; best = r; }
            }
            if (best == null) return null;
            l.ChargingFrom = best.Type;
            best.Type = RoomType.ChargingStation;
            l.ChargingRoom = best.Id;
            return best;
        }

        static bool HasCage(LevelLayout l, PlanRoom r)
        {
            foreach (var p in l.Props)
                if (p.Kind == PropKind.Cage && l.Plan.RoomAt(p.Position + Vector3.up * 0.3f)?.Id == r.Id) return true;
            return false;
        }

        /// <summary>The piece the humans left behind in the room they had (pushed aside, askew): what it was is still readable.</summary>
        public static PropKind Remnant(RoomType from) => from switch
        {
            RoomType.Server => PropKind.ServerRack,
            RoomType.Lab => PropKind.LabBench,
            RoomType.CopyRoom => PropKind.Plotter,
            RoomType.Meeting => PropKind.RoundTable,
            _ => PropKind.Desk,
        };

        /// <summary>A guard resting at a dock stands this far in front of it, facing the room.</summary>
        public const float DockStandOff = 0.85f;

        /// <summary>
        /// After the furniture: the docks in the charging room → where the guards rest, facing out into the room; then, up
        /// to <paramref name="atLeast"/>, resting spots on the room's open floor (a small room took fewer docks: the rest
        /// of the guards stand by, still in the room).
        /// </summary>
        public static void Docks(LevelLayout l, int atLeast = 0)
        {
            l.ChargingDocks.Clear();
            if (l.ChargingRoom < 0) return;
            foreach (var p in l.Props)
            {
                if (p.Kind != PropKind.ChargingDock || l.Plan.RoomAt(p.Position + Vector3.up * 0.3f)?.Id != l.ChargingRoom) continue;
                var facing = Quaternion.Euler(0f, p.RotationY, 0f) * Vector3.forward;
                var stand = p.Position + facing * DockStandOff;
                bool free = true; // docks round a corner: their guards would stand on each other
                foreach (var d in l.ChargingDocks) free &= (d.position - stand).magnitude > 1.0f;
                if (free) l.ChargingDocks.Add(new Pose(stand, Quaternion.LookRotation(facing)));
            }
            var room = l.Plan.Rooms[l.ChargingRoom];
            float y = LevelLayout.FloorY(room.Floor);
            var b = room.Bounds;
            for (float x = b.xMin + 0.9f; x < b.xMax - 0.8f && l.ChargingDocks.Count < atLeast; x += 0.5f)
                for (float z = b.yMin + 0.9f; z < b.yMax - 0.8f && l.ChargingDocks.Count < atLeast; z += 0.5f)
                {
                    var p = new Vector3(x, y, z);
                    if (l.Plan.SpaceAt(room.Floor, new Vector2(x, z)) != room.Id || !Items.LootPlanner.ClearOfFurniture(l, p, room.Floor, 0.5f)) continue;
                    bool apart = true;
                    foreach (var d in l.ChargingDocks) apart &= (d.position - p).magnitude > 1.1f;
                    if (!apart) continue;
                    var look = new Vector3(room.Center.x, y, room.Center.z) - p;
                    l.ChargingDocks.Add(new Pose(p, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward)));
                }
        }
    }
}
