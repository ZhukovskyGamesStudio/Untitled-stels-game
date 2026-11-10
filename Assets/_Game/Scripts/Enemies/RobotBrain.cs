using System;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// What a mobile robot is doing. <see cref="Alerted"/> is the warning moment between noticing a player
    /// («Обнаружен нарушитель») and the chase («Веду преследование»). Values are replicated: append only.
    /// </summary>
    public enum RobotState : byte { Patrol, Suspicious, Investigate, Chase, Alerted }

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
        [Tooltip("Suspicion per second for a fully visible, moving player ~5 m away in the middle of the view in a lit room. " +
                 "Farther, partly hidden, crouching, still, in the dark or at the edge of the view — slower (Data/Config/Perception).")]
        public float DetectionSpeed = 0.6f;
        [Tooltip("Pitch of the eye in degrees (drones look down-forward). 0 = flat cone on its floor.")]
        public float EyePitch;
        [Tooltip("Colour sorter: on spawn picks an item colour and only sees players carrying that colour.")]
        public bool ColorSorter;

        [Header("Hearing")]
        [Tooltip("Hears a noise this many times its radius away (0 = deaf). Each heard noise adds suspicion by its loudness " +
                 "at the robot; walls, doors and slabs muffle it (Data/Config/Perception).")]
        public float HearingMultiplier;

        [Header("Other")]
        [Tooltip("Bumping into the robot this close starts a chase.")]
        public float ContactDistance = 1.3f;
        [Tooltip("Seismic: stomps the floor and a wave runs out to this range along its floor, through walls (0 = no seismic sensor). " +
                 "A player moving when the wave passes is felt; one standing still is not.")]
        public float SeismicRange;
        [Tooltip("Suspicion one wave adds for a player walking at the reference speed close by (faster — more, crouching or far — less).")]
        public float SeismicSpeed = 0.8f;
        [Tooltip("Seconds between stomps.")]
        public float SeismicPeriod = 2.6f;
        [Tooltip("How fast the wave runs, m/s.")]
        public float SeismicWaveSpeed = 9f;

        [Header("Floor lidar")]
        [Tooltip("A laser line spinning around the robot at ankle height, this long (0 = none). Feet on the floor that cross it " +
                 "are noticed; standing on a chair or a desk, or jumping over it, is not.")]
        public float LidarRange;
        [Tooltip("Height of the line above the floor, m.")]
        public float LidarHeight = 0.22f;
        [Tooltip("Turn speed of the line, degrees per second.")]
        public float LidarTurnSpeed = 140f;
        [Tooltip("Suspicion per second while the line touches someone's feet: a tripwire, one pass of the line is enough.")]
        public float LidarRate = 15f;
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
        [Tooltip("Seconds between «Обнаружен нарушитель» and the chase: the player's moment to react.")]
        public float WarningTime = 1.5f;

        [Header("Reaction")]
        [Tooltip("Can this robot hit players? If not, it snitches: raises an alarm and goes back to work.")]
        public bool CanAttack = true;
        [Tooltip("Chance to carry a piece of loot (hack it to make it drop the cargo).")]
        [Range(0f, 1f)] public float CargoChance;
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
    /// patrol → suspicious (stops and stares) → investigate (walks to the spot, looks around) →
    /// alerted (stops: «Обнаружен нарушитель», a moment to react) → chase (hits).
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
        /// <summary>Where the last thing it noticed was (a seen player, a heard noise): it looks and walks there.</summary>
        public Vector3 LookPoint { get; private set; }

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

        bool Detected => State == RobotState.Alerted || State == RobotState.Chase && Target != null;

        /// <summary>Awareness for the HUD: full once it has detected someone, otherwise the suspicion.</summary>
        public float Awareness => Detected ? 1f : Suspicion;
        /// <summary>Whom the awareness refers to.</summary>
        public IIntruder AwarenessFocus => State == RobotState.Chase && Target != null ? Target : Suspect;

        // ------------------------------------------------------------------ input

        /// <summary>The strongest sensor contact of this frame (eyes, floor, nose) — or none.</summary>
        public void OnSensed(in Sighting s, float now, float dt)
        {
            if (s.Target == null)
            {
                if (State != RobotState.Alerted) Suspicion = Mathf.Max(0f, Suspicion - m_Traits.SuspicionDecay * dt);
                return;
            }
            if (State == RobotState.Chase)
            {
                if (s.Target != Target) return; // busy with someone else
                LastKnown = s.ChasePoint;
                m_LastSense = now;
                return;
            }
            if (State == RobotState.Alerted)
            {
                if (s.Target == Suspect) LookPoint = s.Target.Position;
                return;
            }
            Suspect = s.Target;
            LookPoint = s.Target.Position;
            if (s.Contact)
            {
                Detect(s.Target, now);
                return;
            }
            Suspicion = Mathf.Min(1f, Suspicion + s.Rate * dt);
            if (Suspicion >= 1f) Detect(s.Target, now);
            // Only a patrolling robot stops to stare; one already walking over to check keeps walking.
            else if (Suspicion > m_Traits.SuspiciousAt && State == RobotState.Patrol) SetState(RobotState.Suspicious, now);
        }

        /// <summary>
        /// A noise at <paramref name="position"/> that adds <paramref name="suspicion"/> (by its loudness at the robot,
        /// see <see cref="SensorMath.NoiseSuspicion"/>). Quiet ones make it glance, louder ones send it to check,
        /// enough of them — it has detected the source.
        /// </summary>
        public void OnNoise(Vector3 position, float suspicion, IIntruder source, float now)
        {
            if (m_Senses.HearingMultiplier <= 0f) return;
            OnStimulus(position, suspicion, source, now);
        }

        /// <summary>A floor wave caught a moving player (seismic): like a noise, but felt through walls.</summary>
        public void OnFelt(Vector3 position, float suspicion, IIntruder source, float now) => OnStimulus(position, suspicion, source, now);

        void OnStimulus(Vector3 position, float suspicion, IIntruder source, float now)
        {
            if (suspicion <= 0f) return;
            if (State == RobotState.Chase)
            {
                if (source != null && source == Target)
                {
                    LastKnown = position;
                    m_LastSense = now;
                }
                return;
            }
            if (State == RobotState.Alerted) return;
            bool person = source != null && !source.IsDown;
            if (person) Suspect = source;
            LookPoint = position;
            Suspicion = Mathf.Min(1f, Suspicion + suspicion);
            if (Suspicion >= 1f && person) Detect(source, now);
            else if (Suspicion > m_Traits.SuspiciousAt) BeginInvestigation(position, false, now);
            else if (State == RobotState.Patrol) SetState(RobotState.Suspicious, now); // turns to the sound
        }

        /// <summary>Go and check a spot (radio call, alarm, noise). Ignored once it has detected someone.</summary>
        public void Investigate(Vector3 position, bool alarm, float now)
        {
            if (State == RobotState.Chase || State == RobotState.Alerted) return;
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
            LookPoint = position;
            SetState(RobotState.Investigate, now);
            m_Body.MoveTo(position, alarm ? m_Traits.ChaseSpeed * 0.85f : m_Traits.InvestigateSpeed);
            m_WaitUntil = -1f;
        }

        /// <summary>Noticed for sure: stop, face the intruder and warn («Обнаружен нарушитель») before acting.</summary>
        void Detect(IIntruder p, float now)
        {
            if (State == RobotState.Chase || State == RobotState.Alerted) return;
            if (!m_Traits.CanAttack && now < m_SnitchUntil) return; // reported recently
            Suspect = p;
            Suspicion = 1f;
            LookPoint = p.Position;
            m_Body.Stop();
            SetState(RobotState.Alerted, now);
        }

        /// <summary>The warning is over: a fighter chases («Веду преследование»), a snitch raises the alarm.</summary>
        void Act(IIntruder p, float now)
        {
            if (!m_Traits.CanAttack)
            {
                // Snitch: cannot fight, so it reports the intruder to everyone and goes back to work.
                m_SnitchUntil = now + m_Traits.SnitchCooldown;
                Suspicion = 0f;
                m_Body.RaiseAlarm(LookPoint, m_Traits.AlarmRadius);
                SetState(RobotState.Suspicious, now);
                m_Body.Stop();
                return;
            }
            if (m_Traits.AlarmOnDetect) m_Body.RaiseAlarm(LookPoint, m_Traits.AlarmRadius);
            m_Body.CallForBackup(LookPoint);
            Target = p;
            Suspicion = 1f;
            // Runs to where it last noticed them: hiding during the warning pays off.
            LastKnown = LookPoint;
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
                    m_Body.Face(LookPoint, dt);
                    if (!m_Traits.CanAttack && now < m_SnitchUntil)
                    {
                        if (now - m_StateTime > 3f) BackToPatrol(now);
                    }
                    else if (Suspicion <= 0.05f) SetState(RobotState.Patrol, now);
                    else if (now - m_StateTime > m_Traits.StareTime) BeginInvestigation(LookPoint, false, now);
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

                case RobotState.Alerted:
                    m_Body.Stop();
                    m_Body.Face(LookPoint, dt);
                    if (Suspect == null || Suspect.IsDown)
                    {
                        Suspect = null;
                        Suspicion = 0f;
                        BackToPatrol(now);
                    }
                    else if (now - m_StateTime >= m_Traits.WarningTime) Act(Suspect, now);
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
