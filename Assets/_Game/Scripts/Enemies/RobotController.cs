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
    public partial class RobotController : Detector, IRobotBody
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
        [Tooltip("Ring running along the floor when a seismic robot stomps.")]
        public RadioRipple StompRipplePrefab;
        [Tooltip("Spinning floor lidar (robots with Senses.LidarRange > 0).")]
        public RobotLidar Lidar;
        [Tooltip("Where carried loot shows (workers): a mesh slot on the robot; looks from ItemVisuals.")]
        public MeshFilter CargoMesh;
        public MeshRenderer CargoRenderer;
        public ItemVisuals CargoVisuals;

        /// <summary>What it is doing (the replicated mode's low bits; the top bit: resting on its dock).</summary>
        public RobotState State => (RobotState)(Mode & 0x7F);
        /// <summary>Resting on its charging dock (every peer: the top bit of the replicated mode).</summary>
        public bool RestingShown => (Mode & 0x80) != 0;
        byte ModeOf(RobotState s) => (byte)((byte)s | (m_Brain != null && m_Brain.Resting ? 0x80 : 0));
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
            // Everything random about this robot comes from its own stream of the house seed (the n-th robot spawned).
            var rng = LevelRandom.Next(RandomChannel.Robots);
            if (Senses.ColorSorter) SetVariant((sbyte)rng.Next(ItemCatalog.Colors.Length));
            if (Traits.CargoChance > 0f && rng.Value() < Traits.CargoChance)
            {
                byte shape = ItemCatalog.LootShape(rng);
                SetCargo(EncodeCargo(shape, ItemCatalog.FreshColor(shape, ItemCatalog.RandomColor(rng, 0))));
            }
            m_NextStomp = Time.time + rng.Range(0.5f, Senses.SeismicPeriod);

            m_Brain = new RobotBrain(Senses, Traits, this, new System.Random(rng.Next()));
            m_Brain.StateChanged += s => SetMode(ModeOf(s));
            m_LootRng = new System.Random(rng.Next());
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
            EndLootErrand(); // whatever it was lifting or lowering falls in front of it
            DropCargoServer();
        }

        /// <summary>Server: a hacked worker drops what it carried in front of itself — take it.</summary>
        void DropCargoServer()
        {
            if (Cargo < 0 || GameManager.Instance == null) return;
            var (shape, color) = DecodeCargo(Cargo);
            SetCargo(-1);
            var at = transform.position + transform.forward * 0.8f;
            if (NavMesh.SamplePosition(at, out var hit, 1.5f, NavMesh.AllAreas)) at = hit.position;
            GameManager.Instance.SpawnItemServer(shape, color, at + Vector3.up * 0.02f, transform.eulerAngles.y);
        }

        protected override void OnStatusChanged() => ShowCargo();

        // the carried thing's own model (a real item or a tool), under the cargo slot; null: the tinted piece shows
        GameObject m_CargoModel;

        void ShowCargo()
        {
            if (CargoRenderer == null) return;
            bool has = Cargo >= 0 && CargoVisuals != null;
            var (shape, color) = has ? DecodeCargo(Cargo) : ((byte)0, (byte)0);
            // the same look as the item lying on the floor (WorldItem): its model if it has one, else the tinted piece
            var model = has && CargoVisuals.Models != null && shape < CargoVisuals.Models.Length ? CargoVisuals.Models[shape] : null;
            if (m_CargoModel != null && (model == null || m_CargoModel.name != model.name)) { Destroy(m_CargoModel); m_CargoModel = null; }
            if (model != null && m_CargoModel == null)
            {
                m_CargoModel = Instantiate(model, CargoMesh.transform);
                m_CargoModel.name = model.name;
            }
            CargoRenderer.enabled = has && m_CargoModel == null;
            if (!has) return;
            CargoMesh.sharedMesh = CargoVisuals.Meshes[Mathf.Clamp(shape, 0, CargoVisuals.Meshes.Length - 1)];
            CargoRenderer.sharedMaterial = CargoVisuals.ColorMaterials[Mathf.Clamp(color, 0, CargoVisuals.ColorMaterials.Length - 1)];
            VisualStyles.Dress(CargoMesh.gameObject);
        }

        /// <summary>Server: the robot carries this thing (the cage's key on a guard's back); hacked, it drops it.</summary>
        public void CarryServer(byte shape, byte color)
        {
            if (IsServer) SetCargo(EncodeCargo(shape, color));
        }

        /// <summary>Server: go and check a spot (alarm, radio).</summary>
        public void InvestigateServer(Vector3 position, bool alarm) => m_Brain?.Investigate(position, alarm, Time.time);

        /// <summary>Server: rests on a charging dock here until an alarm (<see cref="RobotBrain.SetDock"/>).</summary>
        public void DockServer(Vector3 dock, Vector3 facing) => m_Brain?.SetDock(dock, facing, Time.time);
        /// <summary>Server: an alarm somewhere in the house (a docked robot wakes and hunts).</summary>
        public void AlarmServer(Vector3 position) => m_Brain?.Alarm(position, Time.time);
        /// <summary>Server: the house's time is up — off its dock for good (<see cref="RobotBrain.WakeForGood"/>).</summary>
        public void WakeForGoodServer() => m_Brain?.WakeForGood(Time.time);
        /// <summary>Has a charging dock (a guard of the new enemy mode).</summary>
        public bool Docked => m_Brain != null && m_Brain.Docked;
        /// <summary>Resting on its dock right now.</summary>
        public bool Resting => m_Brain != null && m_Brain.Resting;

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
            if (GameManager.SensorsAllowed)
            {
                m_Brain.OnSensed(Sense(), now, dt);
                if (Senses.SeismicRange > 0f) Stomp(now, dt);
            }
            if (!Traits.MovesLoot || !UpdateLootErrandServer(now, dt)) m_Brain.Tick(now, dt);
            SetMode(ModeOf(m_Brain.State)); // (resting comes and goes without a change of state)
            if (m_HasPost && State == RobotState.Patrol && Arrived)
            {
                // a warden at its post watches the way in
                var face = Quaternion.LookRotation(m_PostFacing);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, face, 120f * dt);
            }
            if (DevFlags.HoldRobots) m_Agent.isStopped = true;
            ReportAwarenessServer(m_Brain.Awareness, m_Brain.AwarenessFocus);
        }

        // ------------------------------------------------------------------ sensors

        /// <summary>Every sensor channel gives a detection rate per player; the strongest one wins.</summary>
        Sighting Sense()
        {
            if (Senses.FollowsSightings) return Relayed();
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
                if (Lidar != null && Senses.LidarRange > 0f && Lidar.Touches(target.Position))
                    rate = Mathf.Max(rate, Senses.LidarRate);
                if (Senses.SmellRange > 0f)
                    rate = Mathf.Max(rate, ScentTrail.Smell(target.ClientId, transform.position, Senses.SmellRange, Time.time) * Senses.SmellSpeed);
                if (rate > best.Rate) best = new Sighting { Target = target, Rate = rate, ChasePoint = ChasePointFor(target) };
            }
            return best;
        }

        /// <summary>
        /// The hunter's only "sense": the nearest player whom some other robot or camera is seeing right now (alerted or
        /// chasing, focused on them) — the hunter knows where they are and runs there. Nothing else wakes it: standing next
        /// to it or hacking it does not.
        /// </summary>
        Sighting Relayed()
        {
            var best = Sighting.None;
            float nearest = float.MaxValue;
            foreach (var p in PlayerController.All)
            {
                IIntruder target = p;
                if (target.IsDown) continue;
                float d = Vector3.Distance(target.Position, transform.position);
                if (d >= nearest || !SpottedByOthers(target.ClientId)) continue;
                nearest = d;
                best = new Sighting { Target = target, Rate = 10f, ChasePoint = target.Position };
            }
            return best;
        }

        bool SpottedByOthers(ulong client)
        {
            foreach (var d in All)
                if (d != this && !d.IsHacked && d.FocusClient == client && (d.IsChasing || d.Awareness >= 0.999f)) return true;
            return false;
        }

        // ------------------------------------------------------------------ seismic stomps

        float m_NextStomp = float.MaxValue, m_StompAt = float.NegativeInfinity;
        readonly HashSet<ulong> m_Felt = new();

        /// <summary>
        /// Server: every <see cref="RobotSenses.SeismicPeriod"/> the robot stomps and a wave runs along its floor (through walls);
        /// whoever is moving when the front passes adds suspicion — standing still, you are not felt.
        /// </summary>
        void Stomp(float now, float dt)
        {
            if (now >= m_NextStomp)
            {
                m_NextStomp = now + Senses.SeismicPeriod;
                m_StompAt = now;
                m_Felt.Clear();
                Pulse(PulseStomp);
            }
            float radius = SeismicSense.WaveRadius(now - m_StompAt, Senses.SeismicWaveSpeed);
            if (radius > Senses.SeismicRange + 1f) return;
            var vibration = Tuning.Perception.Vibration;
            foreach (var p in PlayerController.All)
            {
                IIntruder target = p;
                if (target.IsDown || m_Felt.Contains(target.ClientId) || !Perception.SameFloor(transform.position, target.Position)) continue;
                var flat = target.Position - transform.position;
                flat.y = 0f;
                float d = flat.magnitude;
                if (!SeismicSense.WaveTouches(d, radius, Senses.SeismicWaveSpeed, dt)) continue;
                m_Felt.Add(target.ClientId);
                float s = SeismicSense.WaveSuspicion(Senses.SeismicSpeed, target.GroundSpeed, target.IsCrouching, d, Senses.SeismicRange, vibration);
                m_Brain.OnFelt(target.Position, s, target, now);
            }
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
                Light = Perception.LightFactor(p),
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
            if (effect == PulseStomp)
            {
                // The wave on every peer: a ring along the floor, a thump, the body dips.
                if (StompRipplePrefab != null)
                {
                    var ring = Instantiate(StompRipplePrefab, transform.position, Quaternion.identity);
                    ring.EndRadius = Senses.SeismicRange;
                    ring.Duration = Senses.SeismicRange / Mathf.Max(Senses.SeismicWaveSpeed, 0.1f);
                }
                Audio.Sfx.At(Audio.Sfx.Bank?.SeismicThump, transform.position, 1f, Audio.Sfx.Group.Robots, LevelRandom.Shared(RandomChannel.Audio).Range(0.92f, 1.05f));
                var anim = GetComponent<RobotAnimator>();
                if (anim != null) anim.Stomp();
                return;
            }
            if (effect >= PulseLiftFrom)
            {
                PlayCargoMotion(SwingKind.Lift, LiftSurface(effect)); // a lift off a table: the surface's height rides in the pulse
                return;
            }
            if (effect is PulseLift or PulseLower or PulseShelve)
            {
                PlayCargoMotion(effect == PulseLift ? SwingKind.Pickup : effect == PulseLower ? SwingKind.PutDown : SwingKind.Shelve);
                return;
            }
            if (effect != PulseRadio) return;
            if (RadioRipplePrefab != null) Instantiate(RadioRipplePrefab, transform.position, Quaternion.identity);
            Audio.Sfx.At(Audio.Sfx.Bank?.Radio, Position);
        }

        bool m_HasPost, m_PostAtB;
        Vector3 m_PostA, m_PostB, m_PostFacing;

        /// <summary>
        /// Server: a warden's post (<see cref="WardenPost"/>): instead of wandering the house it walks between two points in
        /// front of a doorway and watches <paramref name="facing"/>; after a distraction it comes back here.
        /// </summary>
        public void SetPost(Vector3 a, Vector3 b, Vector3 facing)
        {
            m_HasPost = true;
            m_PostA = a;
            m_PostB = b;
            m_PostFacing = facing.sqrMagnitude > 0.01f ? new Vector3(facing.x, 0f, facing.z).normalized : transform.forward;
            PatrolToNextPoint();
        }

        public Vector3 Forward => new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;

        Vector3? m_PlannedPoint;

        public Vector3 PlanNextPatrolPoint()
        {
            m_PlannedPoint ??= ChoosePatrolPoint();
            return m_PlannedPoint.Value;
        }

        public void PatrolToNextPoint()
        {
            var goal = m_PlannedPoint ?? ChoosePatrolPoint();
            m_PlannedPoint = null;
            if (m_Agent != null && m_Agent.isOnNavMesh) m_Agent.SetDestination(goal);
        }

        /// <summary>The next spot of its round: the other end of a warden's post, or a patrol point near "its" area.</summary>
        Vector3 ChoosePatrolPoint()
        {
            if (m_HasPost)
            {
                m_PostAtB = !m_PostAtB;
                var goal = m_PostAtB ? m_PostB : m_PostA;
                return m_Agent != null && m_Agent.isOnNavMesh && NavMesh.SamplePosition(goal, out var spot, 1.5f, m_Agent.areaMask) ? spot.position : transform.position;
            }
            var layout = ActiveLevel.Layout;
            if (layout == null || layout.PatrolPoints.Count == 0) return transform.position;
            int myFloor = LevelLayout.FloorOf(transform.position.y);
            // Robots without legs patrol only their floor; walkers mostly stay but sometimes change floors.
            var rng = LevelRandom.Shared(RandomChannel.Patrol);
            bool sameFloorOnly = !Traits.CanUseStairs || rng.Value() < 0.75f;
            var options = layout.PatrolPoints.FindAll(p => !sameFloorOnly || LevelLayout.FloorOf(p.y) == myFloor);
            if (options.Count == 0) options = layout.PatrolPoints;
            // Prefer nearby rooms so that robots keep "their" area.
            var here = transform.position;
            Vector3 best = options[rng.Next(options.Count)];
            for (int i = 0; i < 3; i++)
            {
                var p = options[rng.Next(options.Count)];
                if (Vector3.Distance(p, here) < Vector3.Distance(best, here) && Vector3.Distance(p, here) > 3f) best = p;
            }
            best += new Vector3(rng.Range(-2.5f, 2.5f), 0, rng.Range(-2.5f, 2.5f));
            return NavMesh.SamplePosition(best, out var hit, 3f, m_Agent.areaMask) ? hit.position : transform.position;
        }
    }
}
