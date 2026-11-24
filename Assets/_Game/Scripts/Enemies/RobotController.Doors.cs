using System.Collections.Generic;
using Stealth.Configs;
using Stealth.Level;
using UnityEngine;
using UnityEngine.AI;

namespace Stealth.Enemies
{
    /// <summary>
    /// The robot at doorways (server; rules in <see cref="DoorwayMath"/>, <see cref="DoorwayClaims"/>, <see cref="StuckMeter"/>):
    /// it stands at a shut door until it is open, lets one coming the other way through first — standing aside — and, when
    /// it has got stuck in a crowd after all, takes the place it is at for its goal or pushes through for a moment.
    /// </summary>
    public partial class RobotController
    {
        /// <summary>Where it stands on the floor (a flying robot hovers above it).</summary>
        public Vector3 Feet => Profile != null && Traits.HoverHeight > 0f ? transform.position - Vector3.up * Traits.HoverHeight : transform.position;

        static readonly DoorwayClaims s_Claims = new();
        static LevelLayout s_ClaimsOf;

        static int s_NextClaimId;
        readonly int m_ClaimId = ++s_NextClaimId; // who claims a doorway
        readonly StuckMeter m_Stuck = new();
        readonly Vector3[] m_Corners = new Vector3[16];
        int m_CornerCount;
        ObstacleAvoidanceType m_Avoidance;
        float m_PushUntil = -1f;
        bool m_GaveUp;
        Vector3 m_GaveUpGoal;

        /// <summary>Standing for a door or for another robot right now (server).</summary>
        public bool WaitingAtDoorway { get; private set; }

        /// <summary>Server, once: every robot makes way differently, so two meeting head-on do not both dodge the same way for ever.</summary>
        void SetUpAvoidance()
        {
            m_Avoidance = m_Agent.obstacleAvoidanceType;
            m_Agent.avoidancePriority = 35 + (Robots.Count * 7) % 45;
        }

        /// <summary>It gave up getting closer to where it was going (a crowd is there already): that counts as arrived.</summary>
        bool GaveUp => m_GaveUp && (m_Agent.destination - m_GaveUpGoal).sqrMagnitude < 0.04f;

        /// <summary>
        /// Server, before the decisions of this frame: the path it walks. (A chase asks for a new path every frame, and until
        /// it is found there is none to read: the one read here is the one it is walking.)
        /// </summary>
        void ReadPath()
        {
            if (!m_Agent.hasPath && !m_Agent.pathPending) m_CornerCount = 0;
            else if (!NearDoorway()) m_CornerCount = 0;
            else if (!m_Agent.pathPending) m_CornerCount = m_Agent.path.GetCornersNonAlloc(m_Corners);
            else if (m_CornerCount < 2) // the first steps of a new path, near a doorway: straight to where it goes
            {
                m_Corners[1] = m_Agent.destination;
                m_CornerCount = 2;
            }
            if (m_CornerCount > 0) m_Corners[0] = Feet;
        }

        /// <summary>Server, after the decisions of this frame: may hold the body back.</summary>
        void UpdateDoorways(float now, float dt)
        {
            WaitingAtDoorway = false;
            if (m_Brain.Resting || m_Agent.isStopped)
            {
                m_Stuck.Step(Feet, false, dt, 1f);
                return;
            }
            var settings = Tuning.Level.Doors;
            int count = m_CornerCount;
            if (count > 1 && !MayPass(count, now, settings)) WaitAside(dt, settings);
            else if (count > 1) StandAtShutDoor(count, dt);
            UpdateStuck(now, dt, settings);
        }

        bool NearDoorway()
        {
            var layout = ActiveLevel.Layout;
            if (layout == null) return false;
            var feet = Feet;
            foreach (var d in layout.Doorways)
                if (Mathf.Abs(d.Position.y - feet.y) < 1.6f && (d.Position - feet).sqrMagnitude < 16f) return true;
            return false;
        }

        Doorway m_AtDoorway;

        /// <summary>Nobody comes the other way through the next doorway on its path (it is claimed by asking).</summary>
        bool MayPass(int count, float now, DoorSettings settings)
        {
            var layout = ActiveLevel.Layout;
            if (s_ClaimsOf != layout) { s_ClaimsOf = layout; s_Claims.Clear(); }
            var feet = Feet;
            int found = -1, foundWay = 0;
            float nearest = settings.ClaimDistance;
            for (int i = 0; i < layout.Doorways.Count; i++)
            {
                var d = layout.Doorways[i];
                if (d.Exterior || Mathf.Abs(d.Position.y - feet.y) > 1.6f || (d.Position - feet).sqrMagnitude > 16f) continue;
                float at = DoorwayMath.Crossing(d.Position, d.Rotation * Vector3.forward, d.Width, m_Corners, count, nearest, out int way);
                if (at < 0f) continue;
                nearest = at;
                found = i;
                foundWay = way;
            }
            if (found < 0) return true;
            m_AtDoorway = layout.Doorways[found];
            return s_Claims.Ask(found, m_ClaimId, foundWay, now, settings);
        }

        /// <summary>Steps out of the way of whoever comes through, and looks at the doorway.</summary>
        void WaitAside(float dt, DoorSettings settings)
        {
            WaitingAtDoorway = true;
            m_Agent.isStopped = true;
            var feet = Feet;
            var forward = m_AtDoorway.Rotation * Vector3.forward;
            var flat = m_AtDoorway.Position - feet;
            flat.y = 0f;
            if (flat.magnitude < settings.WaitGap)
            {
                var spot = DoorwayMath.WaitSpot(m_AtDoorway.Position, forward, feet, settings.WaitGap);
                if (NavMesh.SamplePosition(spot, out var hit, 1f, m_Agent.areaMask))
                    m_Agent.Move(Vector3.ClampMagnitude(new Vector3(hit.position.x - feet.x, 0f, hit.position.z - feet.z), Traits.PatrolSpeed * dt));
            }
            if (State != RobotState.Chase) Face(m_AtDoorway.Position, dt * 0.5f);
        }

        /// <summary>A shut door on its way: it stands before it, clear of the leaf, until the door is open.</summary>
        void StandAtShutDoor(int count, float dt)
        {
            if (AutoDoor.Automatic) return;
            var feet = Feet;
            AutoDoor door = null;
            float nearest = 3.2f;
            foreach (var d in AutoDoor.All)
            {
                if (d == null || d.Spot < 0 || d.Vault || d.Passable || Mathf.Abs(d.transform.position.y - feet.y) > 1.6f) continue;
                if ((d.transform.position - feet).sqrMagnitude > 16f) continue;
                float at = DoorwayMath.Crossing(d.transform.position, d.transform.forward, d.Width, m_Corners, count, nearest, out _);
                if (at < 0f) continue;
                nearest = at;
                door = d;
            }
            if (door == null) return;
            float stand = DoorwayMath.StandOff(m_Agent.radius, door.LeafTowards(feet), door.LeafTowards(feet) > 0f);
            if (nearest > stand + 0.2f) return;
            WaitingAtDoorway = true;
            door.RobotAtServer(feet);
            m_Agent.isStopped = true;
            m_Agent.velocity = Vector3.zero; // not a step into the leaves
            Face(door.transform.position, dt);
        }

        void UpdateStuck(float now, float dt, DoorSettings settings)
        {
            bool wants = !WaitingAtDoorway && !m_Agent.isStopped && m_Agent.desiredVelocity.sqrMagnitude > 0.04f && !Arrived;
            if (m_Stuck.Step(Feet, wants, dt, settings.StuckTime))
            {
                if (State != RobotState.Chase && m_Agent.remainingDistance < settings.StuckNearGoal)
                {
                    m_GaveUp = true;
                    m_GaveUpGoal = m_Agent.destination;
                }
                else m_PushUntil = now + settings.PushTime;
            }
            var avoidance = now < m_PushUntil ? ObstacleAvoidanceType.NoObstacleAvoidance : m_Avoidance;
            if (m_Agent.obstacleAvoidanceType != avoidance) m_Agent.obstacleAvoidanceType = avoidance;
        }

        /// <summary>A robot may go there: not into the vault while it is shut.</summary>
        static bool MayVisit(LevelLayout l, Vector3 p)
        {
            if (l.VaultRoom < 0 || World.IsOpened(OpenKeys.Stage(JobStage.VaultOpen))) return true;
            return !l.IsVault(p);
        }

        /// <summary>Nobody stops in a doorway on purpose (pure): the point is at least this far from every one of them on its floor.</summary>
        public static bool ClearOfDoorways(IReadOnlyList<Doorway> doorways, Vector3 p, float clear = 1.4f)
        {
            foreach (var d in doorways)
                if (Mathf.Abs(d.Position.y - p.y) < 1.6f && new Vector2(d.Position.x - p.x, d.Position.z - p.z).sqrMagnitude < clear * clear) return false;
            return true;
        }
    }
}
