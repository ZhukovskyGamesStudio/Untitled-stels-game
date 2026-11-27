using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// Knocked-out and held players (server): a carrier is sent for a body the servants put down, a lone player comes
    /// round by themself a few times a house, a sleeper wakes in the ward, and the run is lost when everybody lies with
    /// nobody coming back (<see cref="CaptiveRules"/>).
    /// </summary>
    public partial class GameManager
    {
        int m_SoloRescues;
        float m_NextCaptiveCheck;

        /// <summary>Server: a house starts — the lone player's rescues are back.</summary>
        void BeginHouseCaptivesServer() => m_SoloRescues = 0;

        /// <summary>Server, every frame of a house: true when the run is lost (everybody is down for good).</summary>
        bool UpdateCaptivesServer()
        {
            var s = Tuning.Player.Captive;
            int players = PlayerController.All.Count, out_ = 0, asleep = 0;
            bool check = Time.time >= m_NextCaptiveCheck;
            if (check) m_NextCaptiveCheck = Time.time + 0.5f;
            foreach (var p in PlayerController.All)
            {
                if (!p.KnockedOut) continue;
                out_++;
                float lying = Time.time - p.HeldSince;
                if (p.Captive == CaptiveKind.Ward)
                {
                    asleep++;
                    if (lying >= s.WardSleep) WakeInWardServer(p);
                    continue;
                }
                float after = CaptiveRules.SoloRescueAfter(p.Captive, players, m_SoloRescues, s);
                if (after >= 0f && lying >= after)
                {
                    m_SoloRescues++;
                    p.FreeServer();
                    Banner(Loc.Pack("banner.came_round", s.SoloRescues - m_SoloRescues));
                    continue;
                }
                if (check && p.CanBeCarried && CaptiveRules.Carried(p.DownedBy) && lying >= s.CarrierDelay
                    && (p.Fetcher == null || !p.Fetcher.Hauling)) SendCarrierServer(p, s.CarrierRange);
            }
            return CaptiveRules.Wiped(players, out_, asleep, m_SoloRescues < s.SoloRescues);
        }

        /// <summary>Server: the nearest free servant that carries players goes for this body.</summary>
        void SendCarrierServer(PlayerController body, float range)
        {
            RobotController best = null;
            float bestDistance = range;
            foreach (var r in RobotController.Robots)
            {
                if (r == null || r.IsHacked || r.Hauling || r.Resting || !r.Traits.CarriesPlayers || r.Faction != Stealth.Level.Faction.Servant) continue;
                float d = Vector3.Distance(r.transform.position, body.transform.position);
                if (d < bestDistance) { bestDistance = d; best = r; }
            }
            best?.FetchServer(body);
        }

        /// <summary>Server: the sleeper in the hospital bed wakes by themself (with a muddled head: <see cref="PlayerController.MuddleServer"/>).</summary>
        void WakeInWardServer(PlayerController p)
        {
            p.FreeServer();
            p.MuddleServer(LevelRandom.Mix(ActiveLevel.Layout != null ? ActiveLevel.Layout.Seed : 0, Salt.Muddle, (int)p.ClientId + m_Run.Alarms * 31));
        }
    }
}
