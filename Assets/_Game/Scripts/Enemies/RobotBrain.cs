using System;
using UnityEngine;

namespace Stealth.Enemies
{
    public enum RobotState : byte { Patrol, Suspicious, Investigate, Chase }

    /// <summary>How a robot senses players. Set per prefab.</summary>
    [Serializable]
    public class RobotSenses
    {
        [Header("Eyes")]
        public bool HasEyes = true;
        public float ViewDistance = 13f;
        public float ViewAngle = 100f;
        [Tooltip("Players this close are noticed regardless of the view angle (if visible).")]
        public float PeripheralDistance = 2.5f;
        public float DetectionSpeed = 1.6f;
        [Tooltip("Pitch of the eye in degrees (drones look down-forward). 0 = flat cone on its floor.")]
        public float EyePitch;
        [Tooltip("Colour sorter: on spawn picks an item colour and only sees players carrying that colour.")]
        public bool ColorSorter;

        [Header("Hearing")]
        public float HearingMultiplier = 0.8f;
        [Tooltip("Share of the hearing radius at which the robot chases the noise source directly.")]
        public float ChaseHearingShare;

        [Header("Other")]
        [Tooltip("Bumping into the robot this close starts a chase.")]
        public float ContactDistance = 1.3f;
        [Tooltip("Seismic: feels walking players on the same floor within this range (0 = no seismic sensor).")]
        public float SeismicRange;
        public float SeismicSpeed = 1.2f;
        [Tooltip("Smell: follows scent trails found within this radius (0 = no nose).")]
        public float SmellRange;
        public float SmellSpeed = 1.5f;
    }

    /// <summary>How a robot moves and reacts. Set per prefab.</summary>
    [Serializable]
    public class RobotTraits
    {
        [Tooltip("Legs / propellers: can use stairs. Wheels / tracks: stays on its floor.")]
        public bool CanUseStairs = true;
        [Tooltip("Flying robots hover this high above the navmesh.")]
        public float HoverHeight;
        public float PatrolSpeed = 2f;
        public float InvestigateSpeed = 3f;
        public float ChaseSpeed = 4.6f;
        [Tooltip("Without any sensor contact for this long the chase turns into an investigation.")]
        public float LoseTargetAfter = 4f;

        [Header("Reaction")]
        [Tooltip("Can this robot hit players? If not, it snitches: raises an alarm and goes back to work.")]
        public bool CanAttack = true;
        [Tooltip("Raise a building-wide alarm when it starts chasing.")]
        public bool AlarmOnDetect;
        [Tooltip("Radius of its alarms (alarm robots, snitches).")]
        public float AlarmRadius = 30f;
        [Tooltip("A snitch reports again only after this many seconds.")]
        public float SnitchCooldown = 12f;
        public float AttackRange = 1.7f;
        public float AttackCooldown = 1.7f;

        [Header("Attention")]
        [Tooltip("Above this suspicion a patrolling robot stops and stares.")]
        public float SuspiciousAt = 0.25f;
        [Tooltip("Suspicion lost per second while it senses nobody.")]
        public float SuspicionDecay = 0.35f;
        [Tooltip("Pause at a patrol point, seconds (random between min and max).")]
        public float PatrolWaitMin = 1f;
        public float PatrolWaitMax = 3.5f;
        [Tooltip("Seconds it looks around after reaching a spot it went to check.")]
        public float InvestigateLookTime = 3f;
        [Tooltip("Seconds of staring before it walks over to check.")]
        public float StareTime = 4f;
    }

    /// <summary>Strongest sensor contact of one frame (computed by the robot's sensors).</summary>
    public struct Sighting
    {
        public IIntruder Target;
        /// <summary>Suspicion per second.</summary>
        public float Rate;
        /// <summary>Bumped into the robot: chase at once.</summary>
        public bool Contact;
        /// <summary>Where to run while chasing (the target, or the next point of its scent trail).</summary>
        public Vector3 ChasePoint;

        public static Sighting None => default;
    }

    /// <summary>
    /// Decision making of a mobile robot (pure C#, unit tested with a fake body):
    /// patrol → suspicious (stops and stares) → investigate (walks to the spot, looks around) → chase (hits).
    /// Sensors feed <see cref="OnSensed"/> / <see cref="OnNoise"/>; <see cref="Tick"/> runs once per frame.
    /// </summary>
    public sealed class RobotBrain
    {

        readonly RobotSenses m_Senses;
        readonly RobotTraits m_Traits;
        readonly IRobotBody m_Body;
        readonly System.Random m_Rng;

        public RobotState State { get; private set; }
        public float Suspicion { get; private set; }
        public IIntruder Target { get; private set; }
        public IIntruder Suspect { get; private set; }
        public Vector3 LastKnown { get; private set; }

        float m_LastSense;
        float m_WaitUntil = -1f;
        float m_NextAttack;
        float m_StateTime;
        float m_SnitchUntil = float.NegativeInfinity;

        public event Action<RobotState> StateChanged;

        public RobotBrain(RobotSenses senses, RobotTraits traits, IRobotBody body, System.Random rng)
        {
            m_Senses = senses;
            m_Traits = traits;
            m_Body = body;
            m_Rng = rng;
        }

        /// <summary>Awareness for the HUD: full while chasing, otherwise the suspicion.</summary>
        public float Awareness => State == RobotState.Chase && Target != null ? 1f : Suspicion;
        /// <summary>Whom the awareness refers to.</summary>
        public IIntruder AwarenessFocus => State == RobotState.Chase && Target != null ? Target : Suspect;

        // ------------------------------------------------------------------ input

        public void OnSensed(in Sighting s, float now, float dt)
        {
            if (s.Target == null)
            {
                Suspicion = Mathf.Max(0f, Suspicion - m_Traits.SuspicionDecay * dt);
                return;
            }
            if (s.Contact)
            {
                StartChase(s.Target, now);
                return;
            }
            if (State == RobotState.Chase && s.Target == Target)
            {
                LastKnown = s.ChasePoint;
                m_LastSense = now;
                return;
            }
            Suspicion += s.Rate * dt;
            Suspect = s.Target;
            if (Suspicion >= 1f) StartChase(s.Target, now);
            // Only a patrolling robot stops to stare; one already walking over to check keeps walking.
            else if (Suspicion > m_Traits.SuspiciousAt && State == RobotState.Patrol) SetState(RobotState.Suspicious, now);
        }

        /// <summary>A noise at <paramref name="position"/>, <paramref name="distance"/> away (already muffled by floors).</summary>
        public void OnNoise(Vector3 position, float distance, float radius, IIntruder source, float now)
        {
            float hearing = radius * m_Senses.HearingMultiplier;
            if (distance > hearing) return;
            if (State == RobotState.Chase)
            {
                if (source != null && source == Target)
                {
                    LastKnown = position;
                    m_LastSense = now;
                }
                return;
            }
            if (source != null && !source.IsDown && distance <= hearing * m_Senses.ChaseHearingShare)
            {
                StartChase(source, now);
                LastKnown = position;
                return;
            }
            Investigate(position, false, now);
        }

        /// <summary>Go and check a spot (radio call, alarm, noise). Ignored while chasing.</summary>
        public void Investigate(Vector3 position, bool alarm, float now)
        {
            if (State == RobotState.Chase) return;
            BeginInvestigation(position, alarm, now);
        }

        /// <summary>Hacked: forget everything.</summary>
        public void Reset(float now)
        {
            Target = null;
            Suspect = null;
            Suspicion = 0f;
            SetState(RobotState.Patrol, now);
        }

        // ------------------------------------------------------------------ decisions

        void BeginInvestigation(Vector3 position, bool alarm, float now)
        {
            LastKnown = position;
            SetState(RobotState.Investigate, now);
            m_Body.MoveTo(position, alarm ? m_Traits.ChaseSpeed * 0.85f : m_Traits.InvestigateSpeed);
            m_WaitUntil = -1f;
        }

        void StartChase(IIntruder p, float now)
        {
            if (!m_Traits.CanAttack)
            {
                // Snitch: cannot fight, so it reports the intruder to everyone and goes back to work.
                if (now < m_SnitchUntil) return;
                m_SnitchUntil = now + m_Traits.SnitchCooldown;
                Suspicion = 0f;
                m_Body.RaiseAlarm(p.Position, m_Traits.AlarmRadius);
                SetState(RobotState.Suspicious, now);
                m_Body.Stop();
                Suspect = p;
                return;
            }
            if (State != RobotState.Chase)
            {
                if (m_Traits.AlarmOnDetect) m_Body.RaiseAlarm(p.Position, m_Traits.AlarmRadius);
                m_Body.CallForBackup(p.Position);
            }
            Target = p;
            Suspicion = 1f;
            LastKnown = p.Position;
            m_LastSense = now;
            SetState(RobotState.Chase, now);
            m_Body.MoveTo(LastKnown, m_Traits.ChaseSpeed);
        }

        public void Tick(float now, float dt)
        {
            switch (State)
            {
                case RobotState.Patrol:
                    m_Body.Move(m_Traits.PatrolSpeed);
                    if (m_Body.Arrived)
                    {
                        if (m_WaitUntil < 0f) m_WaitUntil = now + Range(m_Traits.PatrolWaitMin, m_Traits.PatrolWaitMax);
                        else if (now > m_WaitUntil) NextPatrolPoint();
                    }
                    break;

                case RobotState.Suspicious:
                    m_Body.Stop();
                    if (Suspect != null) m_Body.Face(Suspect.Position, dt);
                    if (!m_Traits.CanAttack)
                    {
                        if (now - m_StateTime > 3f) BackToPatrol(now);
                    }
                    else if (Suspicion <= 0.05f) SetState(RobotState.Patrol, now);
                    else if (Suspect != null && now - m_StateTime > m_Traits.StareTime) BeginInvestigation(Suspect.Position, false, now);
                    break;

                case RobotState.Investigate:
                    if (m_Body.Arrived)
                    {
                        if (m_WaitUntil < 0f) m_WaitUntil = now + m_Traits.InvestigateLookTime;
                        // Look around: sweep left and right instead of spinning on the spot.
                        m_Body.Turn(Mathf.Sin((now - m_StateTime) * 1.8f) * 120f * dt);
                        if (now > m_WaitUntil) BackToPatrol(now);
                    }
                    break;

                case RobotState.Chase:
                    if (Target == null || Target.IsDown)
                    {
                        Target = null;
                        Suspicion = 0f;
                        BackToPatrol(now);
                        break;
                    }
                    if (now - m_LastSense > m_Traits.LoseTargetAfter)
                    {
                        // Lost them: go to the last known spot and look around.
                        Target = null;
                        Suspicion = 0.3f;
                        BeginInvestigation(LastKnown, false, now);
                        break;
                    }
                    m_Body.MoveTo(LastKnown, m_Traits.ChaseSpeed);
                    var to = Target.Position - m_Body.Position;
                    to.y = 0; // flying robots dive at the player
                    if (m_Traits.CanAttack && to.magnitude <= m_Traits.AttackRange)
                    {
                        m_Body.Face(Target.Position, dt);
                        if (now >= m_NextAttack)
                        {
                            m_NextAttack = now + m_Traits.AttackCooldown;
                            m_Body.Attack(Target);
                        }
                    }
                    break;
            }
        }

        void BackToPatrol(float now)
        {
            SetState(RobotState.Patrol, now);
            NextPatrolPoint();
        }

        void NextPatrolPoint()
        {
            m_WaitUntil = -1f;
            m_Body.PatrolToNextPoint();
        }

        void SetState(RobotState s, float now)
        {
            if (State == s) return;
            State = s;
            m_StateTime = now;
            StateChanged?.Invoke(s);
        }

        float Range(float min, float max) => min + (float)m_Rng.NextDouble() * (max - min);
    }
}
