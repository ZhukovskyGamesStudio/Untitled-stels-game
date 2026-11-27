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
    /// (<see cref="Senses"/>, <see cref="Traits"/>): guard, listener, drone, thumper, sorter, worker, sweeper.
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
        public RobotState State => (RobotState)(Mode & 0x3F);
        /// <summary>Resting on its charging dock (every peer: the top bit of the replicated mode).</summary>
        public bool RestingShown => (Mode & 0x80) != 0;
        /// <summary>Up a wall at its work (the repairman at a high grille; every peer: a bit of the replicated mode).</summary>
        public bool ClimbingShown => (Mode & 0x40) != 0;
        byte ModeOf(RobotState s) => (byte)((byte)s | (m_Brain != null && m_Brain.Resting ? 0x80 : 0) | (m_Climbing ? 0x40 : 0));
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

        protected override bool AtEase => State == RobotState.Patrol;
        /// <summary>Its eye glows only when it sees; its ear lamps only when it hears.</summary>
        public override bool GlowEyes => Profile != null && Senses.HasEyes;
        public override bool GlowEars => Profile != null && Senses.HearingMultiplier > 0f;

        protected override Color StatusColor =>
            SecurityPalette.ForState(State, ColorFilter >= 0 ? ItemCatalog.Colors[ColorFilter].Color : SecurityPalette.Calm);

        // ------------------------------------------------------------------ lifecycle

        protected override void OnSpawned()
        {
            Robots.Add(this);
            m_Agent = GetComponent<NavMeshAgent>();
            if (!IsServer) return;

            m_Agent.enabled = true;
            // robots do not climb ladders (the shaft's link is the storage robot's alone); stairs only on legs
            int mask = NavMesh.AllAreas & ~(1 << LevelMetrics.LadderArea);
            m_Agent.areaMask = Traits.CanUseStairs ? mask : mask & ~(1 << LevelMetrics.StairsArea);
            m_Agent.baseOffset = Traits.HoverHeight;
            m_Agent.Warp(transform.position);
            m_Agent.speed = Traits.PatrolSpeed;
            SetUpAvoidance();
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
            BeginRoleServer();
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
            DropHauledServer(); // the body on its back falls where it stands
            EndRoleErrand();
            EndLootErrand(); // whatever it was lifting or lowering falls in front of it
            DropCargoServer();
        }

        /// <summary>Server: searched by a player with no keyboard: what it carries (the cage's key, loot) falls off; it is not hacked.</summary>
        public void SearchServer() => DropCargoServer();

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
        /// <summary>Has a charging dock (a guard of the new enemy mode).</summary>
        public bool Docked => m_Brain != null && m_Brain.Docked;
        /// <summary>Resting on its dock right now.</summary>
        public bool Resting => m_Brain != null && m_Brain.Resting;

        // ------------------------------------------------------------------ update

        /// <summary>
        /// Server: resting, it backs right up to its charging dock and stands on the dock's spot exactly (the navmesh ends
        /// short of the dock, it would stand off it and to the side); off the dock the agent takes the body again.
        /// </summary>
        void HoldOnDock(float dt)
        {
            bool rest = m_Brain.Resting;
            if (rest)
            {
                if (m_Agent.updatePosition) m_Agent.updatePosition = false;
                var spot = m_Brain.DockSpot;
                spot.y = transform.position.y;
                transform.position = Vector3.MoveTowards(transform.position, spot, dt * 1.2f);
            }
            else if (!m_Agent.updatePosition)
            {
                m_Agent.updatePosition = true;
                m_Agent.nextPosition = transform.position; // off the dock from where it stands
            }
        }

        void Update()
        {
            if (!IsServer || m_Brain == null || !m_Agent.isOnNavMesh) return;
            if (DevFlags.FreezeRobots || UpdateHackServer() || !GameManager.IsPlaying)
            {
                m_Agent.isStopped = true;
                return;
            }
            float now = Time.time, dt = Time.deltaTime;
            ReadPath();
            if (GameManager.SensorsAllowed)
            {
                m_Brain.OnSensed(Sense(), now, dt);
                if (Senses.SeismicRange > 0f) Stomp(now, dt);
            }
            if (UpdateHaulServer(now, dt)) { }
            else if (UpdateRoleServer(now, dt)) { }
            else if (!Traits.MovesLoot || !UpdateLootErrandServer(now, dt)) m_Brain.Tick(now, dt);
            UpdateDoorways(now, dt);
            HoldOnDock(dt);
            SetMode(ModeOf(m_Brain.State)); // (resting comes and goes without a change of state)
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
                if (Traits.Role == RobotRole.Maniac && m_Prey != Nobody && target.ClientId != m_Prey) continue; // it has eyes for its prey only
                // (no "contact": a robot touched from behind does not notice it — it notices only with its sensors)

                float rate = 0f;
                bool visibleToFilter = ColorFilter < 0 || target.Carries(ColorFilter);
                if (Senses.HasEyes && visibleToFilter && Look(target, perception.Vision, out var glimpse))
                    rate = Mathf.Max(rate, Senses.DetectionSpeed * SensorMath.VisionFactor(glimpse, -1f, perception.Vision));
                if (Lidar != null && Senses.LidarRange > 0f && Lidar.Touches(target.Position))
                    rate = Mathf.Max(rate, Senses.LidarRate);
                if (rate > best.Rate) best = new Sighting { Target = target, Rate = rate, ChasePoint = target.Position };
            }
            return best;
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

        public bool Arrived => !m_Agent.pathPending && (m_Agent.remainingDistance <= m_Agent.stoppingDistance + 0.4f || GaveUp);

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

        public void Attack(IIntruder target)
        {
            // not through a shut door (or a wall): what it reaches with is at the chest, what it hits too
            AttackServer(target); // by its kind of attack: a hit (not through a shut door or a wall), a net, a shot…
        }

        public void RaiseAlarm(Vector3 at, float radius)
        {
            if (Faction == Stealth.Level.Faction.Renegade) return; // on its own: it calls nobody
            if (GameManager.Instance != null) GameManager.Instance.RaiseAlarmServer(at, radius);
        }

        /// <summary>Radio: nearby robots on the same floor come to check the spot (they do not chase right away).</summary>
        public void CallForBackup(Vector3 at)
        {
            if (Faction == Stealth.Level.Faction.Renegade) return;
            var positions = new List<Vector3>(Robots.Count);
            foreach (var r in Robots) positions.Add(r.transform.position);
            foreach (int i in RobotRadio.Recipients(transform.position, positions))
                if (Robots[i].Faction == Stealth.Level.Faction.Servant) Robots[i].InvestigateServer(at, false);
            Pulse(PulseRadio);
        }

        protected override void OnPulse(byte effect)
        {
            if (OnRolePulse(effect)) return;
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

        /// <summary>The next spot of its round: a patrol point near "its" area.</summary>
        Vector3 ChoosePatrolPoint()
        {
            var layout = ActiveLevel.Layout;
            if (layout == null || layout.PatrolPoints.Count == 0) return transform.position;
            int myFloor = LevelLayout.FloorOf(transform.position.y);
            // Robots without legs patrol only their floor; walkers mostly stay but sometimes change floors.
            var rng = LevelRandom.Shared(RandomChannel.Patrol);
            bool sameFloorOnly = !Traits.CanUseStairs || rng.Value() < 0.75f;
            var options = layout.PatrolPoints.FindAll(p => (!sameFloorOnly || LevelLayout.FloorOf(p.y) == myFloor) && MayVisit(layout, p));
            // its rounds keep to rooms dangerous enough for it: the staff goes anywhere, a guard not into the green rooms
            var own = RobotRoster.ZoneOf(Kind);
            var zoned = own == DangerZone.Green ? options : options.FindAll(p => DangerZones.Patrols(own, layout.ZoneAt(p)));
            if (zoned.Count > 0) options = zoned;
            if (options.Count == 0) options = layout.PatrolPoints;
            // Prefer nearby rooms so that robots keep "their" area.
            var here = transform.position;
            Vector3 best = options[rng.Next(options.Count)];
            for (int i = 0; i < 3; i++)
            {
                var p = options[rng.Next(options.Count)];
                if (Vector3.Distance(p, here) < Vector3.Distance(best, here) && Vector3.Distance(p, here) > 3f) best = p;
            }
            // a step aside from the point, but never a stop in a doorway
            for (int i = 0; i < 4; i++)
            {
                var aside = best + new Vector3(rng.Range(-2.5f, 2.5f), 0, rng.Range(-2.5f, 2.5f));
                if (!ClearOfDoorways(layout.Doorways, aside)) continue;
                best = aside;
                break;
            }
            return NavMesh.SamplePosition(best, out var hit, 3f, m_Agent.areaMask) ? hit.position : transform.position;
        }
    }
}
