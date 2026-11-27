using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Where the servants keep the players they knock out (pure): spots on the open floor of the robots' charging room —
    /// the guards rest right there, deaf and blind until an alarm — or, in a house without one, of the deepest room that
    /// is not green (never the vault, never the hidden room). A teammate walks in and brings the prisoner round.
    /// </summary>
    public static class Prison
    {
        /// <summary>How many prisoners lie apart from each other.</summary>
        public const int Spots = 4;

        /// <summary>Plans <see cref="LevelLayout.PrisonSpots"/> (after the furniture and the docks).</summary>
        public static void Plan(LevelLayout l)
        {
            l.PrisonSpots.Clear();
            l.PrisonRoom = Room(l);
            if (l.PrisonRoom < 0) return;
            var room = l.Plan.Rooms[l.PrisonRoom];
            float y = LevelLayout.FloorY(room.Floor);
            var b = room.Bounds;
            for (float margin = 0.9f; margin >= 0.5f && l.PrisonSpots.Count < Spots; margin -= 0.2f)
                for (float x = b.xMin + margin; x < b.xMax - margin && l.PrisonSpots.Count < Spots; x += 0.5f)
                    for (float z = b.yMin + margin; z < b.yMax - margin && l.PrisonSpots.Count < Spots; z += 0.5f)
                    {
                        var p = new Vector3(x, y, z);
                        if (l.Plan.SpaceAt(room.Floor, new Vector2(x, z)) != room.Id || !Items.LootPlanner.ClearOfFurniture(l, p, room.Floor, margin - 0.2f)) continue;
                        bool apart = true;
                        foreach (var d in l.ChargingDocks) apart &= (d.position - p).magnitude > 1.3f;
                        foreach (var q in l.PrisonSpots) apart &= (q - p).magnitude > 1.3f;
                        if (apart) l.PrisonSpots.Add(p);
                    }
            if (l.PrisonSpots.Count == 0) l.PrisonSpots.Add(new Vector3(room.Center.x, y, room.Center.z));
        }

        /// <summary>The prison's room: the charging room, else the deepest room that is not green.</summary>
        static int Room(LevelLayout l)
        {
            if (l.ChargingRoom >= 0) return l.ChargingRoom;
            int best = -1;
            float bestDepth = -1f;
            foreach (var r in l.Plan.Rooms)
            {
                if (r.Id == l.VaultRoom || r.Id == l.HiddenRoom || r.Id == l.LobbyRoom || RoomTraits.Circulation(r.Type)) continue;
                float depth = l.DepthShare(r.Id) + (l.ZoneOf(r.Id) == DangerZone.Green ? -2f : 0f);
                if (depth > bestDepth) { bestDepth = depth; best = r.Id; }
            }
            return best;
        }
    }
}
