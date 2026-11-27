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
        [Tooltip("How fast what it hears makes it suspicious: each heard noise adds this share of its suspicion " +
                 "(1 = as loud as it is; 0.25 = four times as many noises before it is sure).")]
        public float HearingSpeed = 1f;

        [Header("Other")]
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

        [Header("Role and attack")]
        [Tooltip("What it does besides its rounds (RobotRole): the numbers below are the role's.")]
        public RobotRole Role;
        [Tooltip("Seconds between the role's uses (a puff of smoke, a puddle, a look for a door to shut, news of the maniac's prey).")]
        public float RolePeriod = 20f;
        [Tooltip("The role's or the attack's radius, metres (a cloud, a puddle, a shriek, how loud the talk is, how far a door or a grille is noticed).")]
        public float RoleRadius = 4f;
        [Tooltip("Seconds the role's or the attack's effect lasts (a cloud, a puddle, a stun).")]
        public float RoleDuration = 10f;
        [Tooltip("Uses of the role per house, 0 = no limit (the locks a warden sets again).")]
        public int RoleLimit;
        [Tooltip("What it does to the player it has caught up with (AttackKind): AttackRange and AttackCooldown are its reach and pace.")]
        public AttackKind Attack;
        [Tooltip("Attacks it has before it must recharge (0 = no limit): a drone carries one net — thrown, it raises the alarm and flies off to a charging dock.")]
        public int AttacksPerCharge;
        [Tooltip("Seconds it stays at the charging dock to make its attacks again (a new net).")]
        public float RechargeTime = 10f;

        [Header("Carrying players")]
        [Tooltip("A player it knocks out (or one lying near, put down by its side) it lifts and carries away: a servant to the prison, a renegade to its lair.")]
        public bool CarriesPlayers;
        [Tooltip("How fast it walks with a body on its back.")]
        public float CarrySpeed = 2.2f;
        [Tooltip("Seconds it takes to lift a body.")]
        public float LiftTime = 1.5f;

        [Header("Moving loot (workers)")]
        [Tooltip("On patrol it picks up loot lying on the floor nearby and puts what it carries down somewhere else.")]
        public bool MovesLoot;
        [Tooltip("How far it notices loot lying on the floor, metres (in a straight line over the navmesh).")]
        public float LootSeekRadius = 6f;
        [Tooltip("Chance to put down what it carries at a patrol stop.")]
        [Range(0f, 1f)] public float PutDownChance = 0.3f;
        [Tooltip("Seconds after a pickup or put-down before the next one.")]
        public float LootErrandCooldown = 12f;
        [Tooltip("How long lifting or lowering takes, seconds (the animation and the stop).")]
        public float LootHandleTime = 1.8f;

        [Header("Attention")]
        [Tooltip("Above this suspicion a patrolling robot stops and stares.")]
        public float SuspiciousAt = 0.25f;
        [Tooltip("Suspicion lost per second while it senses nobody.")]
        public float SuspicionDecay = 0.35f;
        [Tooltip("Pause at a patrol point, seconds (random between min and max).")]
        public float PatrolWaitMin = 2.5f;
        public float PatrolWaitMax = 5f;
        [Tooltip("Pausing it looks around: turns this far to each side of where it came from, degrees.")]
        public float PauseScanAngle = 55f;
        [Tooltip("The end of a pause: it turns to where it goes next this many seconds before it walks (a player sees it is about to move).")]
        public float PauseReadyTime = 1.1f;
        [Tooltip("Seconds it looks around after reaching a spot it went to check.")]
        public float InvestigateLookTime = 3f;
        [Tooltip("Seconds of staring before it walks over to check.")]
        public float StareTime = 4f;

        [Header("Docked (guards)")]
        [Tooltip("Woken by an alarm, a docked guard hunts through the house for this long after the last alarm, then goes back to its charging dock and sleeps.")]
        public float AlarmHuntTime = 60f;
    }

    /// <summary>
    /// A docked robot's shift (a guard): it rests at its charging dock, deaf and blind; an alarm
    /// wakes it — it runs to the alarm and hunts through the house; <see cref="RobotTraits.AlarmHuntTime"/> after the last
    /// alarm it walks back to the dock and rests again.
    /// </summary>
    public enum GuardShift : byte { Resting, Hunting, Returning }

    /// <summary>
    /// A robot whose attacks ran out (<see cref="RobotTraits.AttacksPerCharge"/>: a drone's net thrown): flying to a charging
    /// dock, deaf and blind to everything on the way; charging there for <see cref="RobotTraits.RechargeTime"/>; or neither.
    /// </summary>
    public enum RechargeStep : byte { None, Going, Charging }

    /// <summary>Strongest sensor contact of one frame (computed by the robot's sensors).</summary>
    public struct Sighting
    {
        public IIntruder Target;
        /// <summary>Suspicion per second.</summary>
        public float Rate;
        /// <summary>Where to run while chasing (the target).</summary>
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
        float m_PauseStart;
        Vector3 m_PauseForward, m_PauseNext;
        bool m_PauseReady;
        float m_NextAttack;
        float m_StateTime;
        float m_SnitchUntil = float.NegativeInfinity;

        public event Action<RobotState> StateChanged;

        /// <summary>Its attacks ran out and it is off to recharge (<see cref="RechargeStep"/>).</summary>
        public RechargeStep Recharge { get; private set; }
        public bool Recharging => Recharge != RechargeStep.None;
        Vector3 m_ChargeSpot;
        float m_ChargeAt;
        int m_Spent;

        /// <summary>The longest it flies to its dock, seconds: a way it cannot finish — it charges where it got to.</summary>
        public const float RechargeTrip = 40f;

        /// <summary>
        /// Its body made an attack that spends a charge (a net thrown). The last one of the charge: it raises the alarm where
        /// the target is, forgets whom it was after and flies off to recharge at <paramref name="dock"/>.
        /// </summary>
        public void Spend(Vector3 dock, float now)
        {
            if (m_Traits.AttacksPerCharge <= 0 || Recharging || ++m_Spent < m_Traits.AttacksPerCharge) return;
            m_Spent = 0;
            m_Body.RaiseAlarm(Target != null ? Target.Position : LastKnown, m_Traits.AlarmRadius);
            Target = null;
            Suspect = null;
            Suspicion = 0f;
            SetState(RobotState.Patrol, now);
            Recharge = RechargeStep.Going;
            m_ChargeSpot = dock;
            m_ChargeAt = now;
            m_Body.MoveTo(dock, m_Traits.InvestigateSpeed);
        }

        /// <summary>The recharge's part of a tick; true while it drives the body (flying to the dock, charging there).</summary>
        bool TickRecharge(float now, float dt)
        {
            switch (Recharge)
            {
                case RechargeStep.Going:
                    var flat = m_ChargeSpot - m_Body.Position;
                    flat.y = 0f;
                    bool there = m_Body.Arrived && (flat.magnitude < 1.5f || now - m_ChargeAt > 1f); // (as near as its way goes)
                    if (there || now - m_ChargeAt > RechargeTrip)
                    {
                        Recharge = RechargeStep.Charging;
                        m_ChargeAt = now;
                        m_Body.Stop();
                    }
                    else m_Body.Move(m_Traits.InvestigateSpeed);
                    return true;
                case RechargeStep.Charging:
                    m_Body.Stop();
                    if (now - m_ChargeAt < m_Traits.RechargeTime) return true;
                    Recharge = RechargeStep.None; // charged: its rounds again
                    NextPatrolPoint();
                    return true;
            }
            return false;
        }

        /// <summary>Docked (a charging dock to rest at): the shift below drives it; otherwise it patrols as always.</summary>
        public bool Docked { get; private set; }
        public GuardShift Shift { get; private set; }
        Vector3 m_Dock, m_DockFacing;
        float m_HuntUntil;
        bool m_HomeBound;

        /// <summary>Gives it a charging dock: it rests there (from now) until an alarm wakes it.</summary>
        public void SetDock(Vector3 dock, Vector3 facing, float now)
        {
            Docked = true;
            m_Dock = dock;
            m_DockFacing = facing.sqrMagnitude > 1e-4f ? facing.normalized : Vector3.forward;
            Rest(now);
        }

        /// <summary>Where it rests at its dock (its back to the station).</summary>
        public Vector3 DockSpot => m_Dock;

        /// <summary>
        /// Woken by hand (a mimic a player tried to search): it goes for <paramref name="who"/> at once, and when the hunt
        /// is over it settles at <paramref name="dock"/> instead of where it stood.
        /// </summary>
        public void Wake(IIntruder who, Vector3 dock, float now)
        {
            if (!Docked) return;
            m_Dock = dock;
            m_HuntUntil = now + m_Traits.AlarmHuntTime;
            Shift = GuardShift.Hunting;
            if (who != null && !who.IsDown) Detect(who, now);
        }

        /// <summary>Resting at its dock: no patrol, no senses — only an alarm wakes it.</summary>
        public bool Resting => Docked && Shift == GuardShift.Resting;

        /// <summary>
        /// An alarm anywhere in the house: a resting (or returning) docked robot wakes and runs to it; one already hunting
        /// hunts longer (and goes to check, unless it is busy with someone).
        /// </summary>
        public void Alarm(Vector3 position, float now)
        {
            if (!Docked) { Investigate(position, true, now); return; }
            if (Recharging) return;
            m_HuntUntil = now + m_Traits.AlarmHuntTime;
            Shift = GuardShift.Hunting;
            if (State == RobotState.Chase || State == RobotState.Alerted) return;
            BeginInvestigation(position, true, now);
        }

        void Rest(float now)
        {
            Shift = GuardShift.Resting;
            Target = null;
            Suspect = null;
            Suspicion = 0f;
            SetState(RobotState.Patrol, now);
            m_Body.Stop();
        }

        /// <summary>The shift's part of a tick; true when it took the body over (resting or walking home).</summary>
        bool TickShift(float now, float dt)
        {
            if (!Docked) return false;
            switch (Shift)
            {
                case GuardShift.Resting:
                    m_Body.Stop();
                    m_Body.Face(m_Body.Position + m_DockFacing, dt * 0.5f);
                    return true;
                case GuardShift.Hunting:
                    // the hunt is over once it has nobody in sight and no spot to check: it heads home
                    if (now >= m_HuntUntil && State == RobotState.Patrol)
                    {
                        Shift = GuardShift.Returning;
                        m_Body.MoveTo(m_Dock, m_Traits.PatrolSpeed);
                        m_HomeBound = true;
                        return true;
                    }
                    return false;
                case GuardShift.Returning:
                    if (State != RobotState.Patrol) { m_HomeBound = false; return false; } // noticed something on the way: that first
                    var flat = m_Dock - m_Body.Position;
                    flat.y = 0f;
                    if (m_HomeBound && m_Body.Arrived && flat.magnitude < 1.5f) { Rest(now); return true; }
                    if (!m_HomeBound) { m_Body.MoveTo(m_Dock, m_Traits.PatrolSpeed); m_HomeBound = true; }
                    else m_Body.Move(m_Traits.PatrolSpeed);
                    return true;
            }
            return false;
        }

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

        /// <summary>The strongest sensor contact of this frame (eyes, floor) — or none.</summary>
        public void OnSensed(in Sighting s, float now, float dt)
        {
            if (Resting) return; // asleep on its dock
            if (s.Target == null || Recharging) // (off to recharge: nothing else matters)
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
            OnStimulus(position, suspicion * Mathf.Max(0f, m_Senses.HearingSpeed), source, now);
        }

        /// <summary>A floor wave caught a moving player (seismic): like a noise, but felt through walls.</summary>
        public void OnFelt(Vector3 position, float suspicion, IIntruder source, float now) => OnStimulus(position, suspicion, source, now);

        void OnStimulus(Vector3 position, float suspicion, IIntruder source, float now)
        {
            if (suspicion <= 0f || Resting || Recharging) return;
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
            if (State == RobotState.Chase || State == RobotState.Alerted || Resting || Recharging) return;
            BeginInvestigation(position, alarm, now);
        }

        /// <summary>Hacked: forget everything (a docked robot: back to resting where it is woken next).</summary>
        public void Reset(float now)
        {
            Target = null;
            Suspect = null;
            Suspicion = 0f;
            SetState(RobotState.Patrol, now);
            if (Docked && Shift == GuardShift.Hunting) m_HuntUntil = now; // its hunt is over: home when it wakes
        }

        // ------------------------------------------------------------------ decisions

        void BeginInvestigation(Vector3 position, bool alarm, float now)
        {
            LastKnown = position;
            LookPoint = position;
            SetState(RobotState.Investigate, now);
            m_InvestigateSpeed = alarm ? m_Traits.ChaseSpeed * 0.85f : m_Traits.InvestigateSpeed;
            m_Body.MoveTo(position, m_InvestigateSpeed);
            m_WaitUntil = -1f;
        }

        float m_InvestigateSpeed;

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
            if (TickRecharge(now, dt)) return;
            if (TickShift(now, dt)) return;
            switch (State)
            {
                case RobotState.Patrol:
                    if (!m_Body.Arrived)
                    {
                        m_Body.Move(m_Traits.PatrolSpeed);
                        break;
                    }
                    if (m_WaitUntil < 0f)
                    {
                        m_WaitUntil = now + Range(m_Traits.PatrolWaitMin, m_Traits.PatrolWaitMax);
                        m_PauseStart = now;
                        m_PauseForward = m_Body.Forward;
                        m_PauseReady = false;
                    }
                    m_Body.Stop();
                    switch (PausePhase(now - m_PauseStart, m_WaitUntil - m_PauseStart, m_Traits.PauseReadyTime))
                    {
                        case PatrolPause.Scan:
                        {
                            // slowly to one side, then the other: a robot that has stood a while
                            float side = PauseScanSide(now - m_PauseStart);
                            var look = Quaternion.Euler(0f, side * m_Traits.PauseScanAngle, 0f) * m_PauseForward;
                            m_Body.Face(m_Body.Position + look, dt * 0.3f);
                            break;
                        }
                        case PatrolPause.Ready:
                            // about to go: it picks the next point and turns to it first
                            if (!m_PauseReady) { m_PauseNext = m_Body.PlanNextPatrolPoint(); m_PauseReady = true; }
                            m_Body.Face(m_PauseNext, dt * 0.6f);
                            break;
                    }
                    if (now > m_WaitUntil) NextPatrolPoint();
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
                    var to = Target.Position - m_Body.Position;
                    to.y = 0; // flying robots dive at the player
                    bool inReach = m_Traits.CanAttack && to.magnitude <= m_Traits.AttackRange;
                    // an attack made from afar (a shot, a net, a shriek): it stands while it has them in reach and in its senses
                    if (inReach && RoleRules.Ranged(m_Traits.Attack) && now - m_LastSense < 1f) m_Body.Stop();
                    else m_Body.MoveTo(LastKnown, m_Traits.ChaseSpeed);
                    if (inReach)
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

        /// <summary>The phases of a pause at a patrol point, readable from afar: it stops, looks around, turns to go.</summary>
        public enum PatrolPause { Settle, Scan, Ready }

        /// <summary>Which phase a pause is in, <paramref name="t"/> seconds into a pause of <paramref name="length"/> (pure).</summary>
        public static PatrolPause PausePhase(float t, float length, float ready)
        {
            if (t >= length - ready) return PatrolPause.Ready;
            return t < 0.6f ? PatrolPause.Settle : PatrolPause.Scan;
        }

        /// <summary>Where the look-around is: −1 (left) … 1 (right), a slow swing starting to the left (pure).</summary>
        public static float PauseScanSide(float t) => -Mathf.Sin((t - 0.6f) * 1.1f);

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
