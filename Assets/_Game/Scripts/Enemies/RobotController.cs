using System.Collections.Generic;
using Stealth.Configs;
using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Stealth.Enemies
{
    /// <summary>
    /// Mobile robot on the navmesh. This component is the robot's body and sensors (server side);
    /// decisions live in <see cref="RobotBrain"/>. Which sensors it has and how it moves is configured per prefab
    /// (<see cref="Senses"/>, <see cref="Traits"/>): guard, listener, drone, thumper, hound, sorter, worker, hunter.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class RobotController : Detector, IRobotBody
    {
        public static readonly List<RobotController> Robots = new();

        [Tooltip("How this kind senses, moves and is animated (Robot<Kind>Config next to the prefab).")]
        public RobotProfile Profile;

        public override DetectorProfile ProfileBase => Profile;
        public RobotSenses Senses => Profile.Senses;
        public RobotTraits Traits => Profile.Traits;

        [Header("Parts")]
        public Transform Eye;
        [Tooltip("Expanding ring shown on every peer when the robot radios for backup.")]
        public RadioRipple RadioRipplePrefab;

        public RobotState State => (RobotState)Mode;
        /// <summary>-1 = sees everyone; otherwise the ItemCatalog colour it reacts to.</summary>
        public sbyte ColorFilter => Variant;

        NavMeshAgent m_Agent;
        RobotBrain m_Brain;

        public RobotBrain Brain => m_Brain;
        public float Suspicion => m_Brain?.Suspicion ?? 0f;
        public override bool IsInvestigating => State == RobotState.Investigate;
        public override bool IsChasing => State == RobotState.Chase;
        /// <summary>The warning moment: it has noticed someone and is about to chase («Обнаружен нарушитель»).</summary>
        public bool IsAlerted => State == RobotState.Alerted;

        protected override Color StatusColor =>
            SecurityPalette.ForState(State, ColorFilter >= 0 ? ItemCatalog.Colors[ColorFilter].Color : SecurityPalette.Calm);

        // ------------------------------------------------------------------ lifecycle

        protected override void OnSpawned()
        {
            Robots.Add(this);
            m_Agent = GetComponent<NavMeshAgent>();
            if (!IsServer) return;

            m_Agent.enabled = true;
            m_Agent.areaMask = Traits.CanUseStairs ? NavMesh.AllAreas : NavMesh.AllAreas & ~(1 << LevelMetrics.StairsArea);
            m_Agent.baseOffset = Traits.HoverHeight;
            m_Agent.Warp(transform.position);
            m_Agent.speed = Traits.PatrolSpeed;
            if (Senses.ColorSorter) SetVariant((sbyte)Random.Range(0, ItemCatalog.Colors.Length));

            m_Brain = new RobotBrain(Senses, Traits, this, new System.Random((int)NetId * 7919 + 17));
            m_Brain.StateChanged += s => SetMode((byte)s);
            NoiseSystem.OnNoise += OnNoise;
            PatrolToNextPoint();
        }

        protected override void OnDespawned()
        {
            Robots.Remove(this);
            if (IsServer) NoiseSystem.OnNoise -= OnNoise;
        }

        public override bool CanBeHackedFrom(Vector3 player, float reach) =>
            !IsHacked && Hacking.CanHackRobot(transform.position, transform.forward, player, State is RobotState.Chase or RobotState.Alerted, reach);

        protected override void OnHackedServer()
        {
            m_Brain?.Reset(Time.time);
            if (m_Agent != null && m_Agent.isOnNavMesh) m_Agent.isStopped = true;
        }

        /// <summary>Server: go and check a spot (alarm, radio).</summary>
        public void InvestigateServer(Vector3 position, bool alarm) => m_Brain?.Investigate(position, alarm, Time.time);

        // ------------------------------------------------------------------ update

        void Update()
        {
            if (!IsServer || m_Brain == null || !m_Agent.isOnNavMesh) return;
            if (DevFlags.FreezeRobots || UpdateHackServer() || !GameManager.IsPlaying)
            {
                m_Agent.isStopped = true;
                return;
            }
            float now = Time.time, dt = Time.deltaTime;
            if (GameManager.SensorsAllowed) m_Brain.OnSensed(Sense(), now, dt);
            m_Brain.Tick(now, dt);
            if (DevFlags.HoldRobots) m_Agent.isStopped = true;
            ReportAwarenessServer(m_Brain.Awareness, m_Brain.AwarenessFocus);
        }

        // ------------------------------------------------------------------ sensors

        /// <summary>Every sensor channel gives a detection rate per player; the strongest one wins.</summary>
        Sighting Sense()
        {
            var best = Sighting.None;
            var perception = Tuning.Perception;
            foreach (var p in PlayerController.All)
            {
                IIntruder target = p;
                if (target.IsDown) continue;
                float d = Vector3.Distance(target.Position, transform.position);
                if (d <= Senses.ContactDistance) return new Sighting { Target = target, Contact = true, ChasePoint = target.Position };

                float rate = 0f;
                bool visibleToFilter = ColorFilter < 0 || target.Carries(ColorFilter);
                if (Senses.HasEyes && visibleToFilter && Look(target, perception.Vision, out var glimpse))
                    rate = Mathf.Max(rate, Senses.DetectionSpeed * SensorMath.VisionFactor(glimpse, -1f, perception.Vision));
                if (Senses.SeismicRange > 0f && SeismicSense.Feels(transform.position, target.Position, target.GroundSpeed, target.IsCrouching, Senses.SeismicRange))
                {
                    var flat = target.Position - transform.position;
                    flat.y = 0;
                    rate = Mathf.Max(rate, SensorMath.SeismicRate(Senses.SeismicSpeed, flat.magnitude, Senses.SeismicRange, target.GroundSpeed, perception.Vibration));
                }
                if (Senses.SmellRange > 0f)
                    rate = Mathf.Max(rate, ScentTrail.Smell(target.ClientId, transform.position, Senses.SmellRange, Time.time) * Senses.SmellSpeed);
                if (rate > best.Rate) best = new Sighting { Target = target, Rate = rate, ChasePoint = ChasePointFor(target) };
            }
            return best;
        }

        /// <summary>A hound that only smells its target walks along the trail; anything else goes straight for it.</summary>
        Vector3 ChasePointFor(IIntruder target)
        {
            bool close = (target.Position - transform.position).magnitude < 4f;
            if (Senses.SmellRange > 0f && !Senses.HasEyes && !close &&
                ScentTrail.NextAlongTrail(target.ClientId, transform.position, 7f, out var next))
                return next;
            return target.Position;
        }

        Vector3 EyePosition => Eye != null ? Eye.position : transform.position + Vector3.up * 1.6f;

        /// <summary>
        /// How well the eye sees a player right now: in range and in the cone (or close by — peripheral vision),
        /// and how much of the body is not hidden. False when nothing of them is visible.
        /// </summary>
        public bool Look(IIntruder p, VisionSettings v, out Glimpse glimpse)
        {
            var eye = EyePosition;
            var aim = Perception.AimPoint(p, 0.6f);
            glimpse = default;
            float distance = Vector3.Distance(eye, aim);
            float range = Senses.ViewDistance * (p.IsCrouching ? v.CrouchRangeFactor : 1f);
            if (distance > range) return false;
            var forward = Senses.EyePitch > 0f ? Perception.PitchedForward(transform, Senses.EyePitch) : transform.forward;
            var to = aim - eye;
            if (Senses.EyePitch <= 0f)
            {
                // Robots on the floor look around flat: height differences (stairs) do not leave the cone.
                forward.y = 0f;
                to.y = 0f;
            }
            float half = Senses.ViewAngle / 2f;
            float angle = to.sqrMagnitude < 1e-4f ? 0f : Vector3.Angle(forward, to);
            if (angle > half && distance > Senses.PeripheralDistance) return false;
            float exposure = Perception.Exposure(eye, p);
            if (exposure <= 0f) return false;
            glimpse = new Glimpse
            {
                Distance = distance,
                Range = range,
                Exposure = exposure,
                OffCentre = angle / Mathf.Max(half, 1f), // above 1: outside the cone, sensed close by
                Crouching = p.IsCrouching,
                Moving = p.GroundSpeed > v.StillSpeed,
                Light = Perception.LightFactor(p.Position),
            };
            return true;
        }

        /// <summary>
        /// A noise somewhere on the level: loudness here = distance within its radius × hearing, muffled by every wall,
        /// door and slab on the way. Quiet noises only add a little suspicion.
        /// </summary>
        void OnNoise(Vector3 position, float radius, ulong source)
        {
            if (m_Brain == null || IsHacked || Senses.HearingMultiplier <= 0f) return;
            float hearing = radius * Senses.HearingMultiplier;
            var from = position + Vector3.up * Perception.EarHeight;
            var ear = transform.position + Vector3.up * Perception.EarHeight;
            float distance = Vector3.Distance(from, ear);
            if (distance >= hearing) return;
            var settings = Tuning.Perception.Hearing;
            float loudness = SensorMath.Loudness(distance, hearing, Perception.SoundTransmission(from, ear, settings));
            float suspicion = SensorMath.NoiseSuspicion(loudness, settings);
            if (suspicion > 0f) m_Brain.OnNoise(position, suspicion, PlayerController.Find(source), Time.time);
        }

        // ------------------------------------------------------------------ IRobotBody

        Vector3 IRobotBody.Position => transform.position;

        public bool Arrived => !m_Agent.pathPending && m_Agent.remainingDistance <= m_Agent.stoppingDistance + 0.4f;

        public void MoveTo(Vector3 destination, float speed)
        {
            m_Agent.isStopped = false;
            m_Agent.speed = speed;
            m_Agent.SetDestination(destination);
        }

        public void Move(float speed)
        {
            m_Agent.isStopped = false;
            m_Agent.speed = speed;
        }

        public void Stop() => m_Agent.isStopped = true;

        public void Face(Vector3 point, float dt)
        {
            var dir = point - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 240f * dt);
        }

        public void Turn(float degrees) => transform.Rotate(0f, degrees, 0f);

        public void Attack(IIntruder target) => target.HitServer(transform.position);

        public void RaiseAlarm(Vector3 at, float radius)
        {
            if (GameManager.Instance != null) GameManager.Instance.RaiseAlarmServer(at, radius);
        }

        /// <summary>Radio: nearby robots on the same floor come to check the spot (they do not chase right away).</summary>
        public void CallForBackup(Vector3 at)
        {
            var positions = new List<Vector3>(Robots.Count);
            foreach (var r in Robots) positions.Add(r.transform.position);
            foreach (int i in RobotRadio.Recipients(transform.position, positions))
                Robots[i].InvestigateServer(at, false);
            Pulse(PulseRadio);
        }

        protected override void OnPulse(byte effect)
        {
            if (effect != PulseRadio) return;
            if (RadioRipplePrefab != null) Instantiate(RadioRipplePrefab, transform.position, Quaternion.identity);
            Audio.Sfx.At(Audio.Sfx.Bank?.Radio, Position);
        }

        public void PatrolToNextPoint()
        {
            var layout = ActiveLevel.Layout;
            if (layout == null || layout.PatrolPoints.Count == 0) return;
            int myFloor = LevelLayout.FloorOf(transform.position.y);
            // Robots without legs patrol only their floor; walkers mostly stay but sometimes change floors.
            bool sameFloorOnly = !Traits.CanUseStairs || Random.value < 0.75f;
            var options = layout.PatrolPoints.FindAll(p => !sameFloorOnly || LevelLayout.FloorOf(p.y) == myFloor);
            if (options.Count == 0) options = layout.PatrolPoints;
            // Prefer nearby rooms so that robots keep "their" area.
            var here = transform.position;
            Vector3 best = options[Random.Range(0, options.Count)];
            for (int i = 0; i < 3; i++)
            {
                var p = options[Random.Range(0, options.Count)];
                if (Vector3.Distance(p, here) < Vector3.Distance(best, here) && Vector3.Distance(p, here) > 3f) best = p;
            }
            best += new Vector3(Random.Range(-2.5f, 2.5f), 0, Random.Range(-2.5f, 2.5f));
            if (NavMesh.SamplePosition(best, out var hit, 3f, m_Agent.areaMask)) m_Agent.SetDestination(hit.position);
        }
    }
}
