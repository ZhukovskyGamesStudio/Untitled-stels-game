using Stealth.Enemies;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The run's danger (server): the house's clock is the danger — it runs by itself, jumps forward with what the team
    /// does (<see cref="Escalation"/>) and starts each house with a share of the last one's. The reinforcements, the zone
    /// their kinds come from and the armed guards all read that one clock.
    /// </summary>
    public partial class GameManager
    {
        float m_CarriedDanger;
        int m_ArmedGuards;
        bool m_RedAnnounced;

        /// <summary>Server: the team did something the house notices — its clock jumps forward (every peer sees it jump).</summary>
        public void RaiseDangerServer(DangerEvent what)
        {
            if (!m_IsServer || Phase != RunPhase.Playing || HouseClockAt <= 0) return;
            float seconds = Escalation.Seconds(what, m_Run.Settings.Escalation);
            if (seconds > 0f) HouseClockAt -= seconds;
        }

        /// <summary>Server: a new run starts calm.</summary>
        void ResetDangerServer() => m_CarriedDanger = 0f;

        /// <summary>Server: a house starts — nobody has come yet.</summary>
        void BeginHouseDangerServer()
        {
            m_ArmedGuards = 0;
            m_RedAnnounced = false;
        }

        /// <summary>
        /// Server: late on the house's clock armed guards come in, one after another (<see cref="EscalationSettings.ArmedAfter"/>):
        /// the sign that it is time to leave. Announced once when the red robots start coming, and with every armed guard.
        /// </summary>
        void UpdateEscalation(LevelLayout layout)
        {
            var s = m_Run.Settings.Escalation;
            float clock = HouseClock;
            if (!m_RedAnnounced && Escalation.Tier(clock, s) == DangerZone.Red)
            {
                m_RedAnnounced = true;
                Banner(Loc.Pack("banner.danger_red"));
            }
            if (m_ArmedGuards >= Escalation.ArmedDue(clock, s)) return;
            m_ArmedGuards++;
            var at = FurthestEntry(layout);
            if (SpawnRobotServer(RobotRoster.ArmedGuard, at, Quaternion.identity) == null) return;
            Banner(Loc.Pack("banner.armed_guard"));
            Debug.Log($"[Game] Armed guard #{m_ArmedGuards} at {at} (clock {clock:0} s)");
        }
    }
}
