using System.Collections.Generic;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// Reconnects: a player who drops out keeps their backpack, and their spot while the house they left is still on.
    /// Coming back (the same <see cref="PlayerController.Identity"/>) gives the things back: in the same house — where they
    /// stood, otherwise (the team moved on) — next to the host.
    /// </summary>
    public partial class GameManager
    {
        struct Leaver
        {
            public List<InventoryEntry> Items;
            public Vector3 Position;
            public float Yaw;
            public int House;
            public RunPhase Phase;
        }

        readonly Dictionary<string, Leaver> m_Leavers = new();

        /// <summary>Server: a remote player's object goes away (disconnect).</summary>
        public void PlayerLeftServer(PlayerController player)
        {
            if (player == null || string.IsNullOrEmpty(player.Identity) || !player.TryGetComponent<PlayerInventory>(out var inventory)) return;
            m_Leavers[player.Identity] = new Leaver
            {
                Items = inventory.SnapshotServer(),
                Position = player.transform.position,
                Yaw = player.transform.eulerAngles.y,
                House = ActiveLevel.Layout != null ? ActiveLevel.Layout.Seed : 0,
                Phase = Phase,
            };
        }

        /// <summary>Server: a player's identity is known — someone who left before gets their things and their place.</summary>
        public void PlayerJoinedServer(PlayerController player)
        {
            if (player == null || !m_Leavers.TryGetValue(player.Identity, out var left)) return;
            m_Leavers.Remove(player.Identity);
            if (player.TryGetComponent<PlayerInventory>(out var inventory)) inventory.RestoreServer(left.Items);
            bool sameHouse = Phase == RunPhase.Playing && left.Phase == RunPhase.Playing && ActiveLevel.Layout != null && ActiveLevel.Layout.Seed == left.House;
            if (sameHouse)
            {
                player.PlaceServer(left.Position + Vector3.up * 0.05f, left.Yaw);
                return;
            }
            var host = PlayerController.Local;
            if (host == null || host == player || host.SeatIndex >= 0 || host.InHatch) return; // the usual spawn is right there
            player.PlaceServer(BesideHost(host), host.transform.eulerAngles.y);
        }

        /// <summary>A free spot next to the host (a step to a side that has no wall), or behind them.</summary>
        static Vector3 BesideHost(PlayerController host)
        {
            var t = host.transform;
            var chest = t.position + Vector3.up * 1f;
            foreach (var dir in new[] { t.right, -t.right, -t.forward, t.forward })
                if (!Physics.SphereCast(chest, 0.3f, dir, out _, 1.1f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    return t.position + dir * 0.8f + Vector3.up * 0.05f;
            return t.position + Vector3.up * 0.05f;
        }

        /// <summary>A new run: nobody's old things come back.</summary>
        void ForgetLeavers() => m_Leavers.Clear();
    }
}
