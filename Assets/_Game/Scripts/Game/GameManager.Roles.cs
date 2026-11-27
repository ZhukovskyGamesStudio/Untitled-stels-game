using System.Collections.Generic;
using Stealth.Enemies;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// What the robots' roles change in the house (server): a warden shuts a door and sets its hacked lock again, a
    /// repairman screws a grille back on, a driller breaks a door open for good, a relay calls in a teammate's name,
    /// a mimic is woken by the player who tries to search it.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>Doors a driller broke: they stand open, no lock, nobody shuts them again.</summary>
        readonly HashSet<int> m_BrokenDoors = new();

        void BeginHouseRolesServer() => m_BrokenDoors.Clear();

        /// <summary>Server: the warden at a door — shuts it if it stands open, else sets its hacked lock again. True if it set a lock.</summary>
        public bool WardenDoorServer(AutoDoor door)
        {
            if (!m_IsServer || door == null || door.Index < 0 || door.Vault || door.Storage || m_BrokenDoors.Contains(door.Index)) return false;
            if (door.State.Open && !door.State.ByRobot)
            {
                if (door.Spot >= 0 && door.ToggleServer(door.transform.position)) PublishDoor(door);
                return false;
            }
            if (!door.HasLock || door.NeedsKey || !m_Opened.Remove(OpenKeys.Door(door.Index))) return false;
            OpenedVersion++;
            return true;
        }

        /// <summary>Server: the repairman screws a crawl way's grille back on.</summary>
        public void ShutGrilleServer(int index)
        {
            if (m_IsServer && m_Opened.Remove(OpenKeys.Crawl(index))) OpenedVersion++;
        }

        /// <summary>Server: a driller breaks a door — unlocked and open from now on. True if it was shut or locked.</summary>
        public bool BreakDoorServer(AutoDoor door)
        {
            if (!m_IsServer || door == null || door.Index < 0 || door.Vault || !m_BrokenDoors.Add(door.Index)) return false;
            bool changed = false;
            if (door.HasLock && !door.NeedsKey) changed |= m_Opened.Add(OpenKeys.Door(door.Index));
            if (door.Storage) changed |= m_Opened.Add(OpenKeys.Escape(door.Index));
            if (door.Spot >= 0 && !door.State.Open && door.ToggleServer(door.transform.position)) { PublishDoor(door); changed = true; }
            if (changed) OpenedVersion++;
            return changed;
        }

        /// <summary>
        /// Server: a relay calls for help in a teammate's name — the team sees a "help!" ping at the relay as if a player
        /// had sent it (the lure). In the name of a player who is not standing next to it; alone — in nobody's (no ping).
        /// </summary>
        public void LureCallServer(Vector3 at, int roll)
        {
            if (!m_IsServer || Phase != RunPhase.Playing) return;
            var names = new List<PlayerController>();
            foreach (var p in PlayerController.All)
                if (!p.Downed && Vector3.Distance(p.transform.position, at) > 6f) names.Add(p);
            if (names.Count < 2) return; // (the one player who would see it knows they did not call)
            var owner = names[Mathf.Abs(roll) % names.Count];
            Link.BroadcastPing(owner.ClientId, PingKind.Help, at, 0);
        }

        /// <summary>Server: a player pressed E at a "crate" that is a mimic.</summary>
        void UseMimicServer(PlayerController player, Interactable thing)
        {
            if (player == null || player.Downed || thing is not MimicBait bait || bait.Robot == null) return;
            if (!ServerReach.Within(player.transform.position, bait.transform.position, bait.Reach + 0.6f)) return;
            bait.Robot.StirServer(player);
        }
    }
}
