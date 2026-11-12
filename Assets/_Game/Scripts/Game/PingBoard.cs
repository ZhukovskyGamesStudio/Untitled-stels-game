using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>"Here!" — a spot; Enemy — a robot marked for the team (it is also lit through walls while marked).</summary>
    public enum PingKind : byte { Here, Enemy }

    public struct TeamPing
    {
        public ulong Owner;
        public PingKind Kind;
        public Vector3 Position;
        /// <summary>The marked detector's NetId (Enemy), otherwise 0.</summary>
        public ulong Target;
        /// <summary>Local time it appeared and disappears.</summary>
        public float Born, Until;

        public float Age(float now) => now - Born;
    }

    /// <summary>Tuning of team callouts (PlayerConfig.Team): pings, marking robots, how far the map reveals.</summary>
    [System.Serializable]
    public class TeamSettings
    {
        [Tooltip("How far a player reveals the map around them (not through walls), metres.")]
        public float RevealRadius = 14f;
        [Tooltip("Seconds a ping stays on screen and on the map.")]
        public float PingDuration = 8f;
        [Tooltip("How far a ping reaches along the view, metres.")]
        public float PingRange = 80f;
        [Tooltip("Seconds a marked robot is shown through walls to the whole team.")]
        public float MarkDuration = 12f;
        [Tooltip("How far a robot can be marked, metres.")]
        public float MarkRange = 45f;
        [Tooltip("Minimum seconds between two callouts of one player (no spamming).")]
        public float Cooldown = 0.5f;
    }

    /// <summary>
    /// The team's pings on this peer (pure, unit tested). One "here" ping per player: a new one replaces the old.
    /// A marked robot keeps one entry per robot. Expired pings go away.
    /// </summary>
    public sealed class PingBoard
    {
        readonly List<TeamPing> m_Pings = new();
        readonly Dictionary<ulong, float> m_LastCallout = new();

        public IReadOnlyList<TeamPing> Active => m_Pings;
        public int Version { get; private set; }

        public void Add(TeamPing p)
        {
            for (int i = m_Pings.Count - 1; i >= 0; i--)
            {
                var o = m_Pings[i];
                bool same = p.Kind == PingKind.Here ? o.Kind == PingKind.Here && o.Owner == p.Owner : o.Kind == PingKind.Enemy && o.Target == p.Target;
                if (same) m_Pings.RemoveAt(i);
            }
            m_Pings.Add(p);
            Version++;
        }

        public void Expire(float now)
        {
            int removed = m_Pings.RemoveAll(p => now >= p.Until);
            if (removed > 0) Version++;
        }

        public void Clear()
        {
            if (m_Pings.Count == 0) return;
            m_Pings.Clear();
            Version++;
        }

        /// <summary>Server: may this player call out again (not faster than <paramref name="cooldown"/>)? Records the call if so.</summary>
        public bool TryCallout(ulong owner, float now, float cooldown)
        {
            if (m_LastCallout.TryGetValue(owner, out var last) && now - last < cooldown) return false;
            m_LastCallout[owner] = now;
            return true;
        }
    }
}
