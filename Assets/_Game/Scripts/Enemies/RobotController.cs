using System.Collections.Generic;
using Stealth.Game;
using Stealth.Level;
using Stealth.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Stealth.Enemies
{
    public enum RobotState : byte { Patrol, Suspicious, Investigate, Chase }

    /// <summary>
    /// Server-side robot AI. Sensors are configured per prefab:
    ///  * Guard — camera eye (vision cone + LOS) and weak hearing;
    ///  * Listener — blind, but hears footsteps from far away and chases the sound.
    /// When a robot detects a player it walks up to them and hits.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class RobotController : NetworkBehaviour
    {
        public static readonly List<RobotController> All = new();

        [Header("Sensors")]
        public bool HasEyes = true;
        public float ViewDistance = 13f;
        public float ViewAngle = 100f;
        [Tooltip("Players this close are noticed regardless of the view angle (if visible).")]
        public float PeripheralDistance = 2.5f;
        public float DetectionSpeed = 1.6f;
        public float HearingMultiplier = 0.8f;
        [Tooltip("Share of the hearing radius at which the robot chases the noise source directly.")]
        public float ChaseHearingShare = 0f;
        public float ContactDistance = 1.3f;

        [Header("Movement")]
        [Tooltip("Legs: can use stairs and move between floors. Wheels / tracks: stays on its floor.")]
        public bool CanUseStairs = true;
        public float PatrolSpeed = 2f;
        public float InvestigateSpeed = 3f;
        public float ChaseSpeed = 4.6f;
        public float LoseTargetAfter = 4f;

        [Header("Attack")]
        public float AttackRange = 1.7f;
        public float AttackCooldown = 1.7f;

        [Header("Visuals")]
        public Transform Eye;
        public Renderer[] StatusRenderers;
        public Light StatusLight;

        public readonly NetworkVariable<RobotState> State = new();

        NavMeshAgent m_Agent;
        PlayerController m_Target;
        PlayerController m_Suspect;
        float m_Suspicion;
        Vector3 m_LastKnown;
        float m_LastSenseTime;
        float m_WaitUntil = -1f;
        float m_NextAttack;
        float m_StateTime;
        MaterialPropertyBlock m_Block;
        DetectorAwareness m_Awareness;

        public float Suspicion => m_Suspicion;

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            m_Agent = GetComponent<NavMeshAgent>();
            m_Awareness = GetComponent<DetectorAwareness>();
            State.OnValueChanged += (_, s) => ApplyVisual(s);
            ApplyVisual(State.Value);
            if (!IsServer) return;
            m_Agent.enabled = true;
            m_Agent.areaMask = CanUseStairs ? NavMesh.AllAreas : NavMesh.AllAreas & ~(1 << LevelGenerator.StairsArea);
            m_Agent.Warp(transform.position);
            m_Agent.speed = PatrolSpeed;
            NoiseSystem.OnNoise += OnNoise;
            PickPatrolPoint();
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (IsServer) NoiseSystem.OnNoise -= OnNoise;
        }

        void ApplyVisual(RobotState state)
        {
            var c = state switch
            {
                RobotState.Patrol => new Color(0.2f, 0.85f, 1f),
                RobotState.Suspicious => new Color(1f, 0.85f, 0.1f),
                RobotState.Investigate => new Color(1f, 0.5f, 0.1f),
                _ => new Color(1f, 0.1f, 0.05f),
            };
            m_Block ??= new MaterialPropertyBlock();
            m_Block.SetColor("_BaseColor", c);
            m_Block.SetColor("_EmissionColor", c * 2.5f);
            foreach (var r in StatusRenderers) if (r != null) r.SetPropertyBlock(m_Block);
            if (StatusLight != null) StatusLight.color = c;
        }

        void SetState(RobotState s)
        {
            if (State.Value == s) return;
            State.Value = s;
            m_StateTime = Time.time;
        }

        // ------------------------------------------------------------------ AI

        void Update()
        {
            if (!IsServer || m_Agent == null || !m_Agent.isOnNavMesh) return;
            if (GameManager.Instance != null && GameManager.Instance.Phase.Value != RunPhase.Playing)
            {
                m_Agent.isStopped = true;
                return;
            }
            if (!GameManager.Peaceful && !GameManager.InGracePeriod) Sense();
            Think();
            if (m_Awareness != null)
            {
                bool chasing = State.Value == RobotState.Chase && m_Target != null;
                m_Awareness.ReportServer(chasing ? 1f : m_Suspicion, chasing ? m_Target : m_Suspect);
            }
        }

        void Sense()
        {
            PlayerController seen = null;
            float seenDist = float.MaxValue;
            foreach (var p in PlayerController.All)
            {
                if (p.Downed.Value) continue;
                float d = Vector3.Distance(p.transform.position, transform.position);
                if (d <= ContactDistance) { StartChase(p); return; } // bumped into the robot

                if (HasEyes && CanSee(p, out var dist) && dist < seenDist) { seen = p; seenDist = dist; }
            }

            if (seen != null)
            {
                if (State.Value == RobotState.Chase && seen == m_Target)
                {
                    m_LastKnown = seen.transform.position;
                    m_LastSenseTime = Time.time;
                    return;
                }
                float closeness = 1f - Mathf.Clamp01(seenDist / ViewDistance);
                float rate = DetectionSpeed * Mathf.Lerp(0.35f, 2.5f, closeness) * (seen.Crouching.Value ? 0.6f : 1f)
                             * LightMath.VisionFactor(LevelGenerator.Current != null && LevelGenerator.Current.IsDarkAt(seen.transform.position));
                m_Suspicion += rate * Time.deltaTime;
                m_Suspect = seen;
                if (m_Suspicion >= 1f) StartChase(seen);
                else if (m_Suspicion > 0.25f && State.Value != RobotState.Chase) SetState(RobotState.Suspicious);
            }
            else
            {
                m_Suspicion = Mathf.Max(0f, m_Suspicion - 0.35f * Time.deltaTime);
            }
        }

        public bool CanSee(PlayerController p, out float distance)
        {
            var eye = Eye != null ? Eye.position : transform.position + Vector3.up * 1.6f;
            var targetPoint = p.transform.position + Vector3.up * (p.Height * 0.8f);
            var dir = targetPoint - eye;
            distance = dir.magnitude;
            float range = ViewDistance * (p.Crouching.Value ? 0.7f : 1f);
            if (distance > range) return false;
            var flat = new Vector3(dir.x, 0, dir.z);
            if (distance > PeripheralDistance && Vector3.Angle(transform.forward, flat) > ViewAngle / 2f) return false;
            const int mask = ~(1 << LevelGenerator.CeilingLayer);
            if (Physics.Raycast(eye, dir.normalized, out var hit, distance + 0.5f, mask, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<PlayerController>() == p;
            return true;
        }

        void OnNoise(Vector3 position, float radius, ulong source)
        {
            float hearing = radius * HearingMultiplier;
            float d = Vector3.Distance(position, transform.position);
            // Sound through a slab is muffled: other floors hear only half as far.
            if (LevelLayout.FloorOf(position.y) != LevelLayout.FloorOf(transform.position.y)) d *= 2f;
            if (d > hearing) return;
            var player = PlayerController.Find(source);

            if (State.Value == RobotState.Chase)
            {
                if (player != null && player == m_Target)
                {
                    m_LastKnown = position;
                    m_LastSenseTime = Time.time;
                }
                return;
            }
            if (player != null && !player.Downed.Value && d <= hearing * ChaseHearingShare)
            {
                StartChase(player);
                m_LastKnown = position;
                return;
            }
            InvestigateServer(position, false);
        }

        public void InvestigateServer(Vector3 position, bool alarm)
        {
            if (State.Value == RobotState.Chase) return;
            m_LastKnown = position;
            SetState(RobotState.Investigate);
            m_Agent.isStopped = false;
            m_Agent.speed = alarm ? ChaseSpeed * 0.85f : InvestigateSpeed;
            m_Agent.SetDestination(position);
            m_WaitUntil = -1f;
        }

        void StartChase(PlayerController p)
        {
            m_Target = p;
            m_Suspicion = 1f;
            m_LastKnown = p.transform.position;
            m_LastSenseTime = Time.time;
            SetState(RobotState.Chase);
            m_Agent.isStopped = false;
            m_Agent.speed = ChaseSpeed;
        }

        void Think()
        {
            switch (State.Value)
            {
                case RobotState.Patrol:
                    m_Agent.isStopped = false;
                    m_Agent.speed = PatrolSpeed;
                    if (Arrived())
                    {
                        if (m_WaitUntil < 0f) m_WaitUntil = Time.time + Random.Range(1f, 3.5f);
                        else if (Time.time > m_WaitUntil) PickPatrolPoint();
                    }
                    break;

                case RobotState.Suspicious:
                    m_Agent.isStopped = true;
                    if (m_Suspect != null) FaceTowards(m_Suspect.transform.position);
                    if (m_Suspicion <= 0.05f) { SetState(RobotState.Patrol); m_Agent.isStopped = false; }
                    else if (m_Suspect != null && Time.time - m_StateTime > 4f) InvestigateServer(m_Suspect.transform.position, false);
                    break;

                case RobotState.Investigate:
                    if (Arrived())
                    {
                        if (m_WaitUntil < 0f) m_WaitUntil = Time.time + 3f;
                        transform.Rotate(0f, 90f * Time.deltaTime, 0f); // look around
                        if (Time.time > m_WaitUntil) { SetState(RobotState.Patrol); PickPatrolPoint(); }
                    }
                    break;

                case RobotState.Chase:
                    if (m_Target == null || !m_Target.IsSpawned || m_Target.Downed.Value)
                    {
                        m_Target = null;
                        m_Suspicion = 0f;
                        SetState(RobotState.Patrol);
                        PickPatrolPoint();
                        break;
                    }
                    if (Time.time - m_LastSenseTime > LoseTargetAfter)
                    {
                        m_Target = null;
                        m_Suspicion = 0.3f;
                        InvestigateServer(m_LastKnown, false);
                        break;
                    }
                    m_Agent.speed = ChaseSpeed;
                    m_Agent.SetDestination(m_LastKnown);
                    float dist = Vector3.Distance(m_Target.transform.position, transform.position);
                    if (dist <= AttackRange)
                    {
                        FaceTowards(m_Target.transform.position);
                        if (Time.time >= m_NextAttack)
                        {
                            m_NextAttack = Time.time + AttackCooldown;
                            m_Target.TakeHitServer(transform.position);
                        }
                    }
                    break;
            }
        }

        bool Arrived() => !m_Agent.pathPending && m_Agent.remainingDistance <= m_Agent.stoppingDistance + 0.4f;

        void FaceTowards(Vector3 position)
        {
            var dir = position - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 240f * Time.deltaTime);
        }

        void PickPatrolPoint()
        {
            m_WaitUntil = -1f;
            var layout = LevelGenerator.Current;
            if (layout == null || layout.PatrolPoints.Count == 0) return;
            int myFloor = LevelLayout.FloorOf(transform.position.y);
            // Robots without legs patrol only their floor; walkers mostly stay but sometimes change floors.
            bool sameFloorOnly = !CanUseStairs || Random.value < 0.75f;
            var options = layout.PatrolPoints.FindAll(p => !sameFloorOnly || LevelLayout.FloorOf(p.y) == myFloor);
            if (options.Count == 0) options = layout.PatrolPoints;
            // Prefer nearby rooms so that robots keep "their" area.
            Vector3 best = options[Random.Range(0, options.Count)];
            for (int i = 0; i < 3; i++)
            {
                var p = options[Random.Range(0, options.Count)];
                if (Vector3.Distance(p, transform.position) < Vector3.Distance(best, transform.position) && Vector3.Distance(p, transform.position) > 3f) best = p;
            }
            best += new Vector3(Random.Range(-2.5f, 2.5f), 0, Random.Range(-2.5f, 2.5f));
            if (NavMesh.SamplePosition(best, out var hit, 3f, m_Agent.areaMask)) m_Agent.SetDestination(hit.position);
        }
    }
}
