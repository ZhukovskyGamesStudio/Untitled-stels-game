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
        [Tooltip("Seismic: feels walking players on the same floor within this range (0 = no seismic sensor).")]
        public float SeismicRange;
        public float SeismicSpeed = 1.2f;
        [Tooltip("Smell: follows scent trails found within this radius (0 = no nose).")]
        public float SmellRange;
        public float SmellSpeed = 1.5f;
        [Tooltip("Colour sorter: on spawn picks an item colour and only sees players carrying that colour.")]
        public bool ColorSorter;

        /// <summary>-1 = sees everyone; otherwise the ItemCatalog colour it reacts to.</summary>
        public readonly NetworkVariable<sbyte> ColorFilter = new(-1);

        [Header("Body")]
        [Tooltip("Flying robots hover this high above the navmesh.")]
        public float HoverHeight;
        [Tooltip("Pitch of the eye in degrees (drones look down).")]
        public float EyePitch;
        [Tooltip("Instead of (or before) attacking, raise a building-wide alarm on detection.")]
        public bool AlarmOnDetect;
        [Tooltip("Can this robot hit players at all?")]
        public bool CanAttack = true;

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
        public readonly NetworkVariable<bool> Hacked = new();
        public bool IsHacked => Hacked.Value;
        float m_HackedUntil;

        /// <summary>Server: switch the robot off for a while (it forgets what it was doing).</summary>
        public void HackServer(float seconds)
        {
            m_HackedUntil = Time.time + seconds;
            Hacked.Value = true;
            m_Target = null;
            m_Suspicion = 0f;
            SetState(RobotState.Patrol);
            m_Awareness?.ReportServer(0f, null);
        }

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
            Hacked.OnValueChanged += (_, _) => ApplyVisual(State.Value);
            ColorFilter.OnValueChanged += (_, _) => ApplyVisual(State.Value);
            ApplyVisual(State.Value);
            if (!IsServer) return;
            m_Agent.enabled = true;
            m_Agent.areaMask = CanUseStairs ? NavMesh.AllAreas : NavMesh.AllAreas & ~(1 << LevelGenerator.StairsArea);
            if (ColorSorter) ColorFilter.Value = (sbyte)Random.Range(0, Items.ItemCatalog.Colors.Length);
            m_Agent.baseOffset = HoverHeight;
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
            if (Hacked.Value)
            {
                m_Block ??= new MaterialPropertyBlock();
                m_Block.SetColor("_BaseColor", new Color(0.15f, 0.15f, 0.15f));
                m_Block.SetColor("_EmissionColor", Color.black);
                foreach (var r in StatusRenderers) if (r != null) r.SetPropertyBlock(m_Block);
                if (StatusLight != null) StatusLight.color = Color.black;
                return;
            }
            var calm = ColorFilter.Value >= 0 ? Items.ItemCatalog.Colors[ColorFilter.Value].Color : new Color(0.2f, 0.85f, 1f);
            var c = state switch
            {
                RobotState.Patrol => calm,
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
            if (GameManager.FreezeRobots) { m_Agent.isStopped = true; return; }
            if (IsHacked)
            {
                m_Agent.isStopped = true;
                if (Time.time >= m_HackedUntil) { Hacked.Value = false; m_Agent.isStopped = false; }
                return;
            }
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

        /// <summary>
        /// Every sensor channel gives a detection rate per player; the strongest one wins.
        /// Channels: eyes (optionally colour-filtered), floor vibrations (seismic).
        /// </summary>
        void Sense()
        {
            PlayerController best = null;
            float bestRate = 0f;
            foreach (var p in PlayerController.All)
            {
                if (p.Downed.Value) continue;
                float d = Vector3.Distance(p.transform.position, transform.position);
                if (d <= ContactDistance) { StartChase(p); return; } // bumped into the robot

                float rate = 0f;
                bool visibleToFilter = ColorFilter.Value < 0 || p.GetComponent<PlayerInventory>().Carries(ColorFilter.Value);
                if (HasEyes && visibleToFilter && CanSee(p, out var dist)) rate = Mathf.Max(rate, VisionRate(p, dist));
                if (SeismicRange > 0f) rate = Mathf.Max(rate, SeismicRate(p));
                if (SmellRange > 0f) rate = Mathf.Max(rate, ScentTrail.Smell(p.OwnerClientId, transform.position, SmellRange, Time.time) * SmellSpeed);
                if (rate > bestRate) { best = p; bestRate = rate; }
            }

            if (best != null)
            {
                if (State.Value == RobotState.Chase && best == m_Target)
                {
                    // A hound that only smells the target walks along the trail; anything else goes straight for it.
                    bool closeEnough = (best.transform.position - transform.position).magnitude < 4f;
                    if (SmellRange > 0f && !HasEyes && !closeEnough && ScentTrail.NextAlongTrail(best.OwnerClientId, transform.position, 7f, out var next))
                        m_LastKnown = next;
                    else
                        m_LastKnown = best.transform.position;
                    m_LastSenseTime = Time.time;
                    return;
                }
                m_Suspicion += bestRate * Time.deltaTime;
                m_Suspect = best;
                if (m_Suspicion >= 1f) StartChase(best);
                else if (m_Suspicion > 0.25f && State.Value != RobotState.Chase) SetState(RobotState.Suspicious);
            }
            else
            {
                m_Suspicion = Mathf.Max(0f, m_Suspicion - 0.35f * Time.deltaTime);
            }
        }

        float VisionRate(PlayerController p, float dist)
        {
            float closeness = 1f - Mathf.Clamp01(dist / ViewDistance);
            return DetectionSpeed * Mathf.Lerp(0.35f, 2.5f, closeness) * (p.Crouching.Value ? 0.6f : 1f)
                   * LightMath.VisionFactor(LevelGenerator.Current != null && LevelGenerator.Current.IsDarkAt(p.transform.position));
        }

        /// <summary>Footsteps through the floor: same storey, no line of sight needed; crouching or standing still is silent.</summary>
        float SeismicRate(PlayerController p)
        {
            if (!SeismicSense.Feels(transform.position, p.transform.position, p.ServerSpeed, p.Crouching.Value, SeismicRange)) return 0f;
            var d = p.transform.position - transform.position;
            d.y = 0;
            return SeismicSpeed * Mathf.Lerp(0.5f, 2f, 1f - Mathf.Clamp01(d.magnitude / SeismicRange));
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

        float m_SnitchCooldownUntil;

        void StartChase(PlayerController p)
        {
            if (!CanAttack)
            {
                // Snitch: cannot fight, so it reports the intruder to everyone and goes back to work.
                if (Time.time < m_SnitchCooldownUntil) return;
                m_SnitchCooldownUntil = Time.time + 12f;
                m_Suspicion = 0f;
                GameManager.Instance?.RaiseAlarmServer(p.transform.position, 40f);
                m_Awareness?.ReportServer(1f, p);
                SetState(RobotState.Suspicious);
                m_Agent.isStopped = true;
                m_Suspect = p;
                return;
            }
            if (AlarmOnDetect && State.Value != RobotState.Chase && GameManager.Instance != null)
                GameManager.Instance.RaiseAlarmServer(p.transform.position, 30f);
            if (State.Value != RobotState.Chase) CallForBackup(p.transform.position);
            m_Target = p;
            m_Suspicion = 1f;
            m_LastKnown = p.transform.position;
            m_LastSenseTime = Time.time;
            SetState(RobotState.Chase);
            m_Agent.isStopped = false;
            m_Agent.speed = ChaseSpeed;
        }

        public Material RadioMaterial;

        /// <summary>Radio: nearby robots on the same floor come to check the spot (they do not chase right away).</summary>
        void CallForBackup(Vector3 at)
        {
            var positions = new List<Vector3>();
            foreach (var r in All) positions.Add(r.transform.position);
            foreach (int i in RobotRadio.Recipients(transform.position, positions))
                if (All[i].State.Value != RobotState.Chase) All[i].InvestigateServer(at, false);
            RadioRpc();
        }

        [Rpc(SendTo.Everyone)]
        void RadioRpc()
        {
            if (RadioMaterial != null) RobotRadio.Ripple(transform.position, RadioMaterial);
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
                    if (!CanAttack && Time.time - m_StateTime > 3f) { SetState(RobotState.Patrol); m_Agent.isStopped = false; PickPatrolPoint(); }
                    else if (m_Suspicion <= 0.05f && CanAttack) { SetState(RobotState.Patrol); m_Agent.isStopped = false; }
                    else if (m_Suspect != null && Time.time - m_StateTime > 4f) InvestigateServer(m_Suspect.transform.position, false);
                    break;

                case RobotState.Investigate:
                    if (Arrived())
                    {
                        if (m_WaitUntil < 0f) m_WaitUntil = Time.time + 3f;
                        // Look around: sweep left and right instead of spinning on the spot.
                        float sweep = Mathf.Sin((Time.time - m_StateTime) * 1.8f) * 120f * Time.deltaTime;
                        transform.Rotate(0f, sweep, 0f);
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
                    var toTarget = m_Target.transform.position - transform.position;
                    toTarget.y = 0; // flying robots dive at the player
                    float dist = toTarget.magnitude;
                    if (CanAttack && dist <= AttackRange)
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
