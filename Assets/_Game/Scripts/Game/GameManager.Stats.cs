using System.Collections.Generic;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The house's statistics: the server counts what each player does in a house (<see cref="HouseStats"/>); when the team
    /// drives off to the next one it sends everyone the titles, shown on the drive (<see cref="UI.GameHUD"/>).
    /// </summary>
    public partial class GameManager
    {
        readonly HouseStats m_Stats = new();
        readonly Dictionary<PlayerController, Vector3> m_StatLast = new();

        /// <summary>The last house's titles on this peer (empty until a house is left).</summary>
        public readonly List<HouseTitle> LastTitles = new();
        /// <summary>Which house (1-based) <see cref="LastTitles"/> are of.</summary>
        public int LastTitlesHouse { get; private set; }
        /// <summary>When <see cref="LastTitles"/> arrived (local time), -1 never.</summary>
        public float LastTitlesAt { get; private set; } = -1f;

        /// <summary>Server: a house begins — nothing counted yet.</summary>
        void BeginHouseStatsServer()
        {
            m_Stats.Clear();
            m_StatLast.Clear();
        }

        /// <summary>Server: a player did something worth counting in the house.</summary>
        public void NoteServer(PlayerController player, StatKind kind, float amount = 1f)
        {
            if (!m_IsServer || player == null || Phase != RunPhase.Playing || Stealth.Level.ActiveLevel.Layout == null) return;
            m_Stats.Add(player.ClientId, kind, amount);
            // what the house notices moves its clock forward (the run's danger)
            if (kind == StatKind.Hacks) RaiseDangerServer(DangerEvent.Hack);
            else if (kind == StatKind.Downs) RaiseDangerServer(DangerEvent.Down);
        }

        public void NoteFallServer(PlayerController player) => NoteServer(player, StatKind.Falls);
        public void NoteHackServer(PlayerController player) => NoteServer(player, StatKind.Hacks);

        /// <summary>Server, every frame: the walking and the crouching are counted here.</summary>
        void UpdateStatsServer()
        {
            if (Phase != RunPhase.Playing) return;
            float dt = Time.deltaTime;
            foreach (var p in PlayerController.All)
            {
                if (!p.IsSpawned) continue;
                var at = p.transform.position;
                if (m_StatLast.TryGetValue(p, out var last) && !p.Downed && p.SeatIndex < 0)
                {
                    var d = at - last;
                    d.y = 0f;
                    float m = d.magnitude;
                    if (m < 3f) m_Stats.Add(p.ClientId, StatKind.Distance, m); // (a teleport is not a walk)
                    if (p.Crouching) m_Stats.Add(p.ClientId, StatKind.Crouch, dt);
                }
                m_StatLast[p] = at;
            }
        }

        /// <summary>Server: the team drives off — everyone's titles to every peer.</summary>
        void SendHouseStatsServer()
        {
            var ids = new List<ulong>();
            foreach (var p in PlayerController.All) if (p.IsSpawned) ids.Add(p.ClientId);
            if (ids.Count == 0) return;
            ids.Sort();
            Link.BroadcastHouseStats(HouseStats.Pack(Level.Index + 1, m_Stats.Titles(ids)));
        }

        /// <summary>Every peer: the titles of the house just left.</summary>
        public void ReceiveHouseStats(string packed)
        {
            if (!HouseStats.Unpack(packed, out int house, LastTitles)) return;
            LastTitlesHouse = house;
            LastTitlesAt = Time.time;
        }
    }
}
