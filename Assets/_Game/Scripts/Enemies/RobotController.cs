using System.Collections.Generic;
using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using Unity.Netcode;
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

        public RobotSenses Senses = new();
        public RobotTraits Traits = new();

        [Header("Parts")]
        public Transform Eye;
        [Tooltip("Expanding ring shown on every peer when the robot radios for backup.")]
        public RadioRipple RadioRipplePrefab;

        public readonly NetworkVariable<RobotState> State = new();
        /// <summary>-1 = sees everyone; otherwise the ItemCatalog colour it reacts to.</summary>
        public readonly NetworkVariable<sbyte> ColorFilter = new(-1);

        NavMeshAgent m_Agent;
        RobotBrain m_Brain;

        public RobotBrain Brain => m_Brain;
        public float Suspicion => m_Brain?.Suspicion ?? 0f;
        public override bool IsInvestigating => State.Value == RobotState.Investigate;
        public override bool IsChasing => State.Value == RobotState.Chase;

        protected override Color StatusColor =>
            SecurityPalette.ForState(State.Value, ColorFilter.Value >= 0 ? ItemCatalog.Colors[ColorFilter.Value].Color : SecurityPalette.Calm);

        // ------------------------------------------------------------------ lifecycle

        protected override void OnSpawned()
        {
            Robots.Add(this);
            m_Agent = GetComponent<NavMeshAgent>();
            State.OnValueChanged += OnStateChanged;
            ColorFilter.OnValueChanged += OnColorChanged;
            if (!IsServer) return;

            m_Agent.enabled = true;
            m_Agent.areaMask = Traits.CanUseStairs ? NavMesh.AllAreas : NavMesh.AllAreas & ~(1 << LevelMetrics.StairsArea);
            m_Agent.baseOffset = Traits.HoverHeight;
            m_Agent.Warp(transform.position);
            m_Agent.speed = Traits.PatrolSpeed;
            if (Senses.ColorSorter) ColorFilter.Value = (sbyte)Random.Range(0, ItemCatalog.Colors.Length);

            m_Brain = new RobotBrain(Senses, Traits, this, new System.Random((int)NetworkObjectId * 7919 + 17));
            m_Brain.StateChanged += s => State.Value = s;
            NoiseSystem.OnNoise += OnNoise;
            PatrolToNextPoint();
        }

        protected override void OnDespawned()
        {
            Robots.Remove(this);
            State.OnValueChanged -= OnStateChanged;
            ColorFilter.OnValueChanged -= OnColorChanged;
            if (IsServer) NoiseSystem.OnNoise -= OnNoise;
        }

        void OnStateChanged(RobotState _, RobotState __) => RefreshStatus();
        void OnColorChanged(sbyte _, sbyte __) => RefreshStatus();

        public override bool CanBeHackedFrom(Vector3 player) =>
            !IsHacked && Hacking.CanHackRobot(transform.position, transform.forward, player, State.Value == RobotState.Chase);

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
            ReportAwarenessServer(m_Brain.Awareness, m_Brain.AwarenessFocus);
        }

        // ------------------------------------------------------------------ sensors

        /// <summary>Every sensor channel gives a detection rate per player; the strongest one wins.</summary>
        Sighting Sense()
        {
            var best = Sighting.None;
            foreach (var p in PlayerController.All)
            {
                IIntruder target = p;
                if (target.IsDown) continue;
                float d = Vector3.Distance(target.Position, transform.position);
                if (d <= Senses.ContactDistance) return new Sighting { Target = target, Contact = true, ChasePoint = target.Position };

                float rate = 0f;
                bool visibleToFilter = ColorFilter.Value < 0 || target.Carries(ColorFilter.Value);
                if (Senses.HasEyes && visibleToFilter && CanSee(target, out var dist))
                    rate = Mathf.Max(rate, SensorMath.VisionRate(Senses.DetectionSpeed, dist, Senses.ViewDistance, target.IsCrouching, Perception.LightFactor(target.Position)));
                if (Senses.SeismicRange > 0f && SeismicSense.Feels(transform.position, target.Position, target.GroundSpeed, target.IsCrouching, Senses.SeismicRange))
                {
                    var flat = target.Position - transform.position;
                    flat.y = 0;
                    rate = Mathf.Max(rate, SensorMath.SeismicRate(Senses.SeismicSpeed, flat.magnitude, Senses.SeismicRange));
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

        public bool CanSee(IIntruder p, out float distance)
        {
            var eye = Eye != null ? Eye.position : transform.position + Vector3.up * 1.6f;
            var target = Perception.AimPoint(p);
            distance = Vector3.Distance(eye, target);
            float range = Senses.ViewDistance * (p.IsCrouching ? 0.7f : 1f);
            if (distance > range) return false;
            if (distance > Senses.PeripheralDistance)
            {
                bool inView = Senses.EyePitch > 0f
                    ? Perception.InCone(eye, Perception.PitchedForward(transform, Senses.EyePitch), target, range, Senses.ViewAngle)
                    : Perception.InFlatCone(eye, transform.forward, target, range, Senses.ViewAngle);
                if (!inView) return false;
            }
            return Perception.LineOfSight(eye, p);
        }

        void OnNoise(Vector3 position, float radius, ulong source)
        {
            if (m_Brain == null || IsHacked) return;
            m_Brain.OnNoise(position, SensorMath.HearingDistance(position, transform.position), radius, PlayerController.Find(source), Time.time);
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
            RadioRpc();
        }

        [Rpc(SendTo.Everyone)]
        void RadioRpc()
        {
            if (RadioRipplePrefab != null) Instantiate(RadioRipplePrefab, transform.position, Quaternion.identity);
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
