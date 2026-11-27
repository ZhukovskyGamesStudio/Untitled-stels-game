using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>What a robot that carries players is doing with a body (pure, unit tested).</summary>
    public enum HaulStep : byte { None, Fetch, Lift, Carry, Put }

    /// <summary>The steps of carrying a knocked-out player away (pure): walk to the body, lift it, carry it, put it down.</summary>
    public static class HaulRules
    {
        /// <summary>How close to the body it stops to lift it, metres.</summary>
        public const float Reach = 1.4f;
        /// <summary>How close to the place it puts the body down, metres.</summary>
        public const float PutReach = 1.3f;

        /// <summary>
        /// The next step. <paramref name="liftable"/>: the body lies free; <paramref name="held"/>: it is on this robot's back;
        /// <paramref name="near"/>: at the body (fetching) or at the place (carrying); <paramref name="timeUp"/>: the lift or
        /// the put-down has taken its time. None: it gives up (the body is gone, freed or taken).
        /// </summary>
        public static HaulStep Next(HaulStep step, bool liftable, bool held, bool near, bool timeUp) => step switch
        {
            HaulStep.Fetch => !liftable ? HaulStep.None : near ? HaulStep.Lift : HaulStep.Fetch,
            HaulStep.Lift => !liftable ? HaulStep.None : timeUp ? HaulStep.Carry : HaulStep.Lift,
            HaulStep.Carry => !held ? HaulStep.None : near ? HaulStep.Put : HaulStep.Carry,
            HaulStep.Put => !held ? HaulStep.None : timeUp ? HaulStep.None : HaulStep.Put,
            _ => HaulStep.None,
        };

        /// <summary>Where a kind takes its bodies: the nurse to a hospital bed, a renegade to its lair, the servants to the prison.</summary>
        public static CaptiveKind PlaceOf(RobotKind kind) => RobotRoster.FactionOf(kind) == Faction.Renegade ? CaptiveKind.Lair : CaptiveKind.Prison;
    }

    /// <summary>
    /// Carrying knocked-out players (server): a robot with <see cref="RobotTraits.CarriesPlayers"/> walks to a body, lifts
    /// it on its back and takes it to its place (<see cref="HaulRules.PlaceOf"/>). Hacked on the way, it drops the body
    /// where it stands — the team's chance.
    /// </summary>
    public partial class RobotController
    {
        HaulStep m_Haul;
        PlayerController m_Hauled;
        float m_HaulUntil;
        Vector3 m_HaulTo;

        /// <summary>Busy with a body: walking to it, lifting, carrying, putting down.</summary>
        public bool Hauling => m_Haul != HaulStep.None;
        public HaulStep HaulStep => m_Haul;

        /// <summary>Server: go and take this body away. False if it cannot (hacked, busy, not a carrier, the body is not free).</summary>
        public bool FetchServer(PlayerController body)
        {
            if (!IsServer || IsHacked || Hauling || !Traits.CarriesPlayers || body == null || !body.CanBeCarried) return false;
            if (body.Fetcher != null && body.Fetcher != this && body.Fetcher.Hauling) return false;
            m_Hauled = body;
            body.Fetcher = this;
            m_Haul = HaulStep.Fetch;
            MoveTo(body.transform.position, Traits.InvestigateSpeed);
            return true;
        }

        /// <summary>Server: the body's place for this robot — a spot of the prison by the body's number, or where it stands.</summary>
        Vector3 HaulPlace(PlayerController body)
        {
            var layout = ActiveLevel.Layout;
            if (layout == null || layout.PrisonSpots.Count == 0) return transform.position;
            return layout.PrisonSpots[(int)(body.ClientId % (ulong)layout.PrisonSpots.Count)];
        }

        /// <summary>Server: the haul's part of a frame; true while it drives the body (the brain waits).</summary>
        bool UpdateHaulServer(float now, float dt)
        {
            if (m_Haul == HaulStep.None) return false;
            var body = m_Hauled;
            bool there = body != null && body.IsSpawned;
            bool liftable = there && body.CanBeCarried;
            bool held = there && body.Captive == CaptiveKind.Carried && body.CaptorId == NetId;
            var goal = m_Haul is HaulStep.Carry or HaulStep.Put ? m_HaulTo : there ? body.transform.position : transform.position;
            var flat = goal - transform.position;
            flat.y = 0f;
            bool near = flat.magnitude <= (m_Haul == HaulStep.Carry ? HaulRules.PutReach : HaulRules.Reach) && Mathf.Abs(goal.y - Feet.y) < 1.6f;
            if (m_Haul == HaulStep.Carry && Arrived && flat.magnitude < 3f) near = true; // (as close as the navmesh lets it)
            var next = HaulRules.Next(m_Haul, liftable, held, near, now >= m_HaulUntil);
            if (next != m_Haul)
            {
                switch (next)
                {
                    case HaulStep.Lift:
                        Stop();
                        m_HaulUntil = now + Traits.LiftTime;
                        break;
                    case HaulStep.Carry:
                        if (!body.PickUpServer(this)) { EndHaul(); return false; }
                        m_HaulTo = HaulPlace(body);
                        MoveTo(m_HaulTo, Traits.CarrySpeed);
                        break;
                    case HaulStep.Put:
                        Stop();
                        m_HaulUntil = now + Traits.LiftTime * 0.6f;
                        break;
                    case HaulStep.None:
                        if (m_Haul == HaulStep.Put && held) body.ImprisonServer(HaulRules.PlaceOf(Kind), m_HaulTo);
                        EndHaul();
                        return false;
                }
                m_Haul = next;
            }
            switch (m_Haul)
            {
                case HaulStep.Fetch: MoveTo(body.transform.position, Traits.InvestigateSpeed); break;
                case HaulStep.Lift: Stop(); Face(body.transform.position, dt); break;
                case HaulStep.Carry: Move(Traits.CarrySpeed); break;
                case HaulStep.Put: Stop(); break;
            }
            return true;
        }

        void EndHaul()
        {
            if (m_Hauled != null && m_Hauled.Fetcher == this) m_Hauled.Fetcher = null;
            m_Hauled = null;
            m_Haul = HaulStep.None;
            PatrolToNextPoint();
        }

        /// <summary>Server: stopped (hacked): the body on its back falls where it stands.</summary>
        void DropHauledServer()
        {
            if (m_Haul == HaulStep.None) return;
            if (m_Hauled != null && m_Hauled.Captive == CaptiveKind.Carried && m_Hauled.CaptorId == NetId) m_Hauled.DropServer();
            if (m_Hauled != null && m_Hauled.Fetcher == this) m_Hauled.Fetcher = null;
            m_Hauled = null;
            m_Haul = HaulStep.None;
        }
    }
}
