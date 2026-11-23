using System.Collections.Generic;
using Stealth.Audio;
using Stealth.Configs;
using Stealth.Game;
using Stealth.Items;
using Stealth.Player;
using Stealth.UI;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>The car → network: things everyone must hear.</summary>
    public interface ICarLink
    {
        void SendHorn();
    }

    /// <summary>
    /// The team's pickup in the sewer (prefab Vehicles/Car, networked): four seats — the driver's, the passenger's and two
    /// in the open bed — an engine that eats anything (<see cref="CarHopper"/>: loot becomes fuel), a bed where things
    /// are heaped without cells (<see cref="Cargo"/>, kept by the game for the whole run). The driver drives it
    /// (<see cref="CarPhysics"/>): the network twin hands the car to the driver's peer, which moves it; everyone else
    /// sees it move and spin its wheels. The server decides who sits where (<see cref="SitServer"/>); a car driven into a
    /// tunnel of the sewer chooses the way on.
    /// </summary>
    [DefaultExecutionOrder(-20)] // the car moves before the players seated in it are put on their seats
    public class Car : MonoBehaviour
    {
        public static Car Active { get; private set; }
        public const int SeatCount = 4;
        public const ulong Nobody = ulong.MaxValue;

        [Header("Model")]
        [Tooltip("Everything that bounces, pitches and rolls on the suspension.")]
        public Transform Body;
        [Tooltip("The painted model under Body (mirrored for a left-hand car, CarLook).")]
        public Transform Model;
        [Tooltip("Front left, front right, rear left, rear right (each pivoted at its centre).")]
        public Transform[] Wheels = new Transform[4];
        public Transform Steering;
        public Transform Grinder;
        public Light[] Headlights;
        [Tooltip("The cab's dome light (a warm point light under the roof), on like the beams.")]
        public Light DomeLight;
        public Renderer[] LightGlow;

        [Header("Use")]
        [Tooltip("Driver, passenger, bed left, bed right: where a sitting player's feet go (facing forward).")]
        public Transform[] Seats = new Transform[SeatCount];
        [Tooltip("The bed's floor at the tailgate, in the middle: +Z towards the cab, +X across.")]
        public Transform Bed;
        public CargoPiece CargoPrefab;
        public ItemVisuals Visuals;
        public Transform CargoRoot;
        [Tooltip("The solid hull: what stops at walls.")]
        public BoxCollider Hull;
        [Tooltip("The solid chassis: pushed out of walls it ended up in (Physics.ComputePenetration).")]
        public BoxCollider Chassis;
        public AudioSource Engine;

        /// <summary>This peer moves the car (the network twin: it owns it). Without a session — always.</summary>
        public bool HasAuthority { get; set; } = true;
        public bool IsServer { get; private set; } = true;

        readonly ulong[] m_Occupants = { Nobody, Nobody, Nobody, Nobody };
        ICarLink m_Link;
        CarPhysics m_Physics;
        CarInput m_Input;
        Quaternion[] m_WheelRest;
        Quaternion m_SteeringRest, m_GrinderRest;
        Vector3 m_BodyRest;
        float m_Spin, m_Distance, m_Grind, m_RemoteSpeed, m_RemoteSteer;
        Vector3 m_LastPos;
        float m_LastYaw;
        Pose m_DrivePose;
        float m_DriveSpeed;
        bool m_Scripted;
        // the drive takes over from where this peer saw the car (the server's start lags the driver's a little): eased out
        Vector3 m_DriveOffset;
        Quaternion m_DriveTurn = Quaternion.identity;
        float m_DriveSince;
        bool m_WasEngine;
        int m_CargoShown = -1;
        readonly List<CargoPiece> m_Pieces = new();
        /// <summary>The things shown in the bed (inactive ones are spare).</summary>
        public IReadOnlyList<CargoPiece> Pieces => m_Pieces;
        /// <summary>The engine's hopper on the hood (things dragged onto it burn into fuel).</summary>
        public CarHopper Hopper => m_Hopper != null ? m_Hopper : m_Hopper = GetComponentInChildren<CarHopper>();
        CarHopper m_Hopper;
        /// <summary>The workbench in the bed (the team's bench: <see cref="Game.GameManager.Bench"/>).</summary>
        public CarWorkbench Bench => m_Bench != null ? m_Bench : m_Bench = GetComponentInChildren<CarWorkbench>();
        CarWorkbench m_Bench;
        static readonly RaycastHit[] s_Hits = new RaycastHit[16];
        static readonly Collider[] s_Overlaps = new Collider[16];

        public IReadOnlyList<ulong> Occupants => m_Occupants;
        public ulong Driver => m_Occupants[0];
        public bool EngineOn => Driver != Nobody;
        /// <summary>The shredder has things to chew (CarShredder): its blades spin fast.</summary>
        public bool Grinding { get; set; }
        /// <summary>On the drive to the next house: moved by the clock (Level.DrivePath), not by a driver.</summary>
        public bool Scripted => m_Scripted;
        public float Speed => HasAuthority ? m_Physics.Speed : m_RemoteSpeed;
        public float SteerAngle => HasAuthority ? m_Physics.SteerAngle : m_RemoteSteer;
        /// <summary>How much the car turned since the last frame (seated players turn with it), degrees.</summary>
        public float YawDelta { get; private set; }
        public CarSettings Settings => m_Physics.Settings;

        void Awake()
        {
            Active = this;
            m_Physics = new CarPhysics(Tuning.Run.Car);
            if (CarLook.NeedsMirror(m_Physics.Settings)) MirrorSides();
            m_WheelRest = new Quaternion[Wheels.Length];
            for (int i = 0; i < Wheels.Length; i++) if (Wheels[i] != null) m_WheelRest[i] = Wheels[i].localRotation;
            if (Steering != null) m_SteeringRest = Steering.localRotation;
            if (Grinder != null) m_GrinderRest = Grinder.localRotation;
            if (Body != null) m_BodyRest = Body.localPosition;
            m_LastPos = transform.position;
            m_LastYaw = transform.eulerAngles.y;
            if (Engine != null) { Engine.loop = true; Engine.volume = 0f; if (Engine.clip != null) Engine.Play(); }
        }

        /// <summary>The steering wheel to the other side: the model mirrored, the driver's and the passenger's seats swapped.</summary>
        void MirrorSides()
        {
            CarLook.Mirror(Model);
            for (int i = 0; i < 2 && i < Seats.Length; i++)
            {
                if (Seats[i] == null) continue;
                var p = Seats[i].localPosition;
                Seats[i].localPosition = new Vector3(-p.x, p.y, p.z);
            }
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        /// <summary>The network twin at spawn.</summary>
        public void Bind(ICarLink link, bool isServer)
        {
            m_Link = link;
            IsServer = isServer;
        }

        // ------------------------------------------------------------------ seats

        public int SeatOf(ulong client)
        {
            for (int i = 0; i < SeatCount; i++) if (m_Occupants[i] == client) return i;
            return -1;
        }

        public ulong OccupantOf(int seat) => seat >= 0 && seat < SeatCount ? m_Occupants[seat] : Nobody;

        /// <summary>Client: the seats as the server has them.</summary>
        public void ApplySeats(IReadOnlyList<ulong> seats)
        {
            for (int i = 0; i < SeatCount && i < seats.Count; i++) m_Occupants[i] = seats[i];
        }

        /// <summary>Server: a player gets in (onto a free seat) or out (pressing on their own seat).</summary>
        public void SitServer(PlayerController player, int seat)
        {
            if (player == null || seat < 0 || seat >= SeatCount) return;
            int now = SeatOf(player.ClientId);
            if (now == seat) { m_Occupants[seat] = Nobody; return; }
            if (m_Occupants[seat] != Nobody || player.Downed) return;
            if (now >= 0) m_Occupants[now] = Nobody;
            m_Occupants[seat] = player.ClientId;
        }

        /// <summary>Server: everybody out (the drive to the next house, a player who left the session).</summary>
        public void ClearSeatsServer(System.Predicate<ulong> keep = null)
        {
            for (int i = 0; i < SeatCount; i++)
                if (m_Occupants[i] != Nobody && (keep == null || !keep(m_Occupants[i]))) m_Occupants[i] = Nobody;
        }

        /// <summary>Where a player gets out of a seat: beside the car on its side (or behind the tailgate for the bed).</summary>
        public Vector3 ExitPoint(int seat)
        {
            var s = Seats[Mathf.Clamp(seat, 0, SeatCount - 1)];
            if (s == null) return transform.position + transform.right * 1.8f;
            float side = Mathf.Sign(transform.InverseTransformPoint(s.position).x);
            if (side == 0f) side = 1f;
            var at = s.position + transform.right * side * 1.7f;
            return new Vector3(at.x, transform.position.y + 0.1f, at.z);
        }

        // ------------------------------------------------------------------ the bed

        /// <summary>Is this point (where a thing was dropped) over the bed? Its spot on the bed in cm if so.</summary>
        public bool OverBed(Vector3 world, out Vector2 spot)
        {
            spot = default;
            if (Bed == null) return false;
            var local = Bed.InverseTransformPoint(world);
            spot = new Vector2(local.x * 100f, local.z * 100f);
            return Mathf.Abs(local.x) <= 1.2f && local.z >= -0.5f && local.z <= 2.4f && local.y > -0.9f && local.y < 2.2f;
        }

        void ShowCargo()
        {
            var gm = GameManager.Instance;
            if (gm == null || CargoPrefab == null || Bed == null || gm.Cargo.Version == m_CargoShown) return;
            m_CargoShown = gm.Cargo.Version;
            var items = gm.Cargo.Items;
            var heights = gm.Cargo.Heights();
            while (m_Pieces.Count < items.Count) m_Pieces.Add(Instantiate(CargoPrefab, CargoRoot != null ? CargoRoot : Bed));
            for (int i = 0; i < m_Pieces.Count; i++)
            {
                bool on = i < items.Count;
                if (m_Pieces[i].gameObject.activeSelf != on) m_Pieces[i].gameObject.SetActive(on);
                if (!on) continue;
                var e = items[i];
                var piece = m_Pieces[i];
                piece.Show(i, e, Visuals);
                piece.transform.SetPositionAndRotation(Bed.TransformPoint(new Vector3(e.X / 100f, heights[i] / 100f, e.Z / 100f)),
                    Bed.rotation * Quaternion.Euler(0f, e.Yaw, 0f));
            }
        }

        // ------------------------------------------------------------------ driving

        bool LocalDriver => Driver != Nobody && Driver == Session.LocalClientId;

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            // the drive on to the next house: every peer moves the car by the server's clock (Level.DrivePath), nobody steers it
            bool was = m_Scripted;
            bool scripted = m_Scripted = GameManager.Instance != null && GameManager.Instance.Driving(out m_DrivePose, out m_DriveSpeed, out _);
            if (scripted && !was)
            {
                m_DriveSince = Time.time;
                m_DriveOffset = transform.position - m_DrivePose.position;
                m_DriveTurn = transform.rotation * Quaternion.Inverse(m_DrivePose.rotation);
                if (m_DriveOffset.magnitude > 4f) { m_DriveOffset = Vector3.zero; m_DriveTurn = Quaternion.identity; } // (far off: no glide)
            }
            if (scripted)
            {
                float k = 1f - Mathf.Clamp01((Time.time - m_DriveSince) / 1.2f);
                k = k * k * (3f - 2f * k);
                m_DrivePose = new Pose(m_DrivePose.position + m_DriveOffset * k, Quaternion.Slerp(Quaternion.identity, m_DriveTurn, k) * m_DrivePose.rotation);
            }
            bool drive = LocalDriver && !GameMenu.IsOpen && !CheatPanel.IsOpen && !StylePanel.IsOpen && !scripted;
            m_Input = drive
                ? new CarInput
                {
                    Throttle = (Controls.Held(GameAction.Forward) ? 1f : 0f) - (Controls.Held(GameAction.Back) ? 1f : 0f),
                    Steer = (Controls.Held(GameAction.Right) ? 1f : 0f) - (Controls.Held(GameAction.Left) ? 1f : 0f),
                    Brake = Controls.Held(GameAction.Jump),
                }
                : new CarInput { Brake = !EngineOn };
            if (DevFlags.CarInput is CarInput forced && (LocalDriver || !Session.IsRunning) && !scripted) m_Input = forced;
            if (drive && Controls.Pressed(GameAction.Horn)) Horn();

            if (scripted)
            {
                transform.SetPositionAndRotation(m_DrivePose.position, m_DrivePose.rotation);
                m_Physics.Speed = m_DriveSpeed;
                m_RemoteSpeed = m_DriveSpeed;
                m_RemoteSteer = 0f;
            }
            else if (HasAuthority) Simulate(dt);
            else
            {
                var delta = transform.position - m_LastPos;
                float along = Vector3.Dot(delta, transform.forward) / dt;
                m_RemoteSpeed = Mathf.Lerp(m_RemoteSpeed, Mathf.Abs(along) < 30f ? along : 0f, 1f - Mathf.Exp(-10f * dt));
                float turn = Mathf.DeltaAngle(m_LastYaw, transform.eulerAngles.y) / dt;
                float wanted = Mathf.Abs(m_RemoteSpeed) > 0.3f ? Mathf.Atan(turn * Mathf.Deg2Rad * m_Physics.Settings.Wheelbase / m_RemoteSpeed) * Mathf.Rad2Deg : 0f;
                m_RemoteSteer = Mathf.Lerp(m_RemoteSteer, Mathf.Clamp(wanted, -40f, 40f), 1f - Mathf.Exp(-8f * dt));
            }
            YawDelta = Mathf.DeltaAngle(m_LastYaw, transform.eulerAngles.y);
            m_LastPos = transform.position;
            m_LastYaw = transform.eulerAngles.y;

            Animate(dt);
            Sound();
            ShowCargo();
        }

        /// <summary>On the drive the pose is the clock's on every peer, whatever the network's transform set meanwhile.</summary>
        void LateUpdate()
        {
            if (m_Scripted) transform.SetPositionAndRotation(m_DrivePose.position, m_DrivePose.rotation);
        }

        /// <summary>The driver's peer moves the car: the bicycle model, stopped by walls, kept on the floor.</summary>
        void Simulate(float dt)
        {
            var (forward, yaw) = m_Physics.Step(m_Input, dt);
            var rot = transform.rotation * Quaternion.Euler(0f, yaw, 0f);
            var dir = (transform.rotation * Quaternion.Euler(0f, yaw / 2f, 0f)) * Vector3.forward * Mathf.Sign(forward);
            float distance = Mathf.Abs(forward);
            if (distance > 1e-4f && Blocked(dir, distance, out float free))
            {
                if (Mathf.Abs(m_Physics.Speed) > 2.5f) Sfx.At(Sfx.Bank?.CarBump, transform.position + dir * 2f, Mathf.Clamp01(Mathf.Abs(m_Physics.Speed) / 8f), Sfx.Group.Items);
                m_Physics.Bump();
                distance = Mathf.Max(0f, free - 0.05f);
            }
            var pos = transform.position + dir * distance;
            // The cast above sweeps the old heading only: turning beside a wall would swing the hull into it. A pose that
            // ends inside something keeps the old heading (slides on), or turns on the spot, or stays where it was.
            if (Overlaps(pos, rot))
            {
                if (!Overlaps(pos, transform.rotation)) rot = transform.rotation;
                else if (!Overlaps(transform.position, rot)) pos = transform.position;
                else { pos = transform.position; rot = transform.rotation; }
                m_Physics.Bump();
            }
            pos = PushedOut(pos, rot); // already in a wall (spawned so, pushed by the network): out of it, never through
            // on the floor: the grate under the wheels (not the car itself, a player, a thing lying around)
            if (Ground(pos, out float floor)) pos.y = Mathf.MoveTowards(pos.y, floor, 4f * dt);
            transform.SetPositionAndRotation(pos, rot);
        }

        /// <summary>The hull's box for wall checks at a pose: a little lower than the hull and lifted off the floor.</summary>
        (Vector3 center, Vector3 half) HullBox(Vector3 pos, Quaternion rot)
        {
            var half = Vector3.Scale(Hull.size, Hull.transform.lossyScale) * 0.49f;
            half.y *= 0.7f; // the floor under the wheels is not a wall
            var local = transform.InverseTransformPoint(Hull.transform.TransformPoint(Hull.center)) + Vector3.up * half.y * 0.35f;
            return (pos + rot * local, half);
        }

        /// <summary>Is this collider something the car cannot drive through (not itself, a player or a thing lying around)?</summary>
        bool Solid(Collider c) =>
            c != null && !c.isTrigger && !c.transform.IsChildOf(transform)
            && c.GetComponentInParent<PlayerController>() == null && c.GetComponentInParent<WorldItem>() == null;

        bool Overlaps(Vector3 pos, Quaternion rot)
        {
            if (Hull == null) return false;
            var (center, half) = HullBox(pos, rot);
            int n = Physics.OverlapBoxNonAlloc(center, half, s_Overlaps, rot, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (Solid(s_Overlaps[i])) return true;
            return false;
        }

        /// <summary>The pose moved out of whatever the chassis sits in, sideways only (a few passes for corners).</summary>
        Vector3 PushedOut(Vector3 pos, Quaternion rot)
        {
            if (Chassis == null) return pos;
            var t = Chassis.transform;
            var localPos = transform.InverseTransformPoint(t.position);
            var localRot = Quaternion.Inverse(transform.rotation) * t.rotation;
            for (int pass = 0; pass < 3; pass++)
            {
                var at = pos + rot * localPos;
                var turn = rot * localRot;
                var center = at + turn * Vector3.Scale(Chassis.center, t.lossyScale);
                var half = Vector3.Scale(Chassis.size, t.lossyScale) * 0.5f;
                int n = Physics.OverlapBoxNonAlloc(center, half, s_Overlaps, turn, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                bool moved = false;
                for (int i = 0; i < n; i++)
                {
                    var c = s_Overlaps[i];
                    if (!Solid(c)) continue;
                    if (!Physics.ComputePenetration(Chassis, at, turn, c, c.transform.position, c.transform.rotation, out var away, out float depth)) continue;
                    away.y = 0f;
                    if (away.sqrMagnitude < 1e-6f || depth <= 0f) continue;
                    pos += away.normalized * (depth + 0.01f);
                    moved = true;
                }
                if (!moved) break;
            }
            return pos;
        }

        bool Ground(Vector3 at, out float y)
        {
            y = at.y;
            int n = Physics.RaycastNonAlloc(at + Vector3.up * 1.5f, Vector3.down, s_Hits, 3.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = s_Hits[i].collider;
                if (c == null || c.transform.IsChildOf(transform) || c.GetComponentInParent<PlayerController>() != null) continue;
                if (s_Hits[i].distance < best) { best = s_Hits[i].distance; y = s_Hits[i].point.y; }
            }
            return best < float.MaxValue;
        }

        /// <summary>Would the hull hit something solid (not the car itself, a player or a thing lying around) moving this far?</summary>
        bool Blocked(Vector3 dir, float distance, out float free)
        {
            free = distance;
            if (Hull == null) return false;
            var center = Hull.transform.TransformPoint(Hull.center);
            var half = Vector3.Scale(Hull.size, Hull.transform.lossyScale) * 0.49f;
            half.y *= 0.7f; // the floor under the wheels is not a wall
            center += Vector3.up * half.y * 0.35f;
            int n = Physics.BoxCastNonAlloc(center, half, dir, s_Hits, transform.rotation, distance + 0.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool hit = false;
            for (int i = 0; i < n; i++)
            {
                var c = s_Hits[i].collider;
                if (c == null || c.transform.IsChildOf(transform) || s_Hits[i].distance <= 0f) continue;
                if (c.GetComponentInParent<PlayerController>() != null || c.GetComponentInParent<WorldItem>() != null) continue;
                if (s_Hits[i].distance < free) { free = s_Hits[i].distance; hit = true; }
            }
            return hit;
        }

        void Animate(float dt)
        {
            float speed = Speed, steer = SteerAngle;
            var s = m_Physics.Settings;
            m_Distance += Mathf.Abs(speed) * dt;
            m_Spin = (m_Spin + speed * dt / 0.42f * Mathf.Rad2Deg) % 360f;
            for (int i = 0; i < Wheels.Length; i++)
            {
                if (Wheels[i] == null) continue;
                float yaw = i < 2 ? steer : 0f;
                Wheels[i].localRotation = m_WheelRest[i] * Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(m_Spin, 0f, 0f);
            }
            if (Steering != null) Steering.localRotation = m_SteeringRest * CarLook.SteeringWheel(steer, s.SteeringRatio);
            m_Grind = (m_Grind + (Grinding ? 1800f : EngineOn ? 540f : 0f) * dt) % 360f;
            if (Grinder != null) Grinder.localRotation = m_GrinderRest * Quaternion.Euler(m_Grind, 0f, 0f);
            if (Body != null)
            {
                // the suspension: a light bounce on the grate's panels, a nod when speeding up / braking, a lean in turns
                float moving = Mathf.Clamp01(Mathf.Abs(speed) / 3f);
                float bounce = (Mathf.PerlinNoise(m_Distance * 1.7f, 0.37f) - 0.5f) * 2f * s.Bounce * moving
                               + (EngineOn ? Mathf.Sin(Time.time * 47f) * 0.0025f : 0f); // the engine shakes it a little
                float pitch = Mathf.Clamp(-(HasAuthority ? m_Physics.Accel : 0f) * s.PitchPerAccel, -3f, 3f);
                float roll = steer / Mathf.Max(1f, s.MaxSteer) * Mathf.Clamp01(Mathf.Abs(speed) / s.MaxSpeed) * s.Roll;
                Body.localPosition = Vector3.Lerp(Body.localPosition, m_BodyRest + Vector3.up * bounce, 1f - Mathf.Exp(-20f * dt));
                Body.localRotation = Quaternion.Slerp(Body.localRotation, Quaternion.Euler(pitch, 0f, -roll), 1f - Mathf.Exp(-6f * dt));
            }
            const bool lights = true; // high beams on all the time: the sewer is pitch dark
            if (Headlights == null) return;
            // (their strength as built, without the style's visual scale: LightSource writes that over the light)
            m_HeadlightFull ??= System.Array.ConvertAll(Headlights, l => l == null ? 0f : l.TryGetComponent<Stealth.Level.LightSource>(out var source) ? source.RawIntensity : l.intensity);
            m_HeadlightNow ??= (float[])m_HeadlightFull.Clone();
            for (int i = 0; i < Headlights.Length; i++)
            {
                var l = Headlights[i];
                if (l == null) continue;
                if (l.enabled != lights) l.enabled = lights;
                // close to a wall the beam dims (CarLook.Headlight): what it shines at is never burnt out
                float want = CarLook.Headlight(m_HeadlightFull[i], AheadOf(l.transform, s.HeadlightReach), s.HeadlightGlare);
                m_HeadlightNow[i] = Mathf.Lerp(m_HeadlightNow[i], want, 1f - Mathf.Exp(-s.HeadlightEase * dt));
                l.intensity = m_HeadlightNow[i];
            }
        }

        float[] m_HeadlightFull, m_HeadlightNow;
        static readonly RaycastHit[] s_Ahead = new RaycastHit[8];

        /// <summary>How far before a lamp the first thing it shines at is (not the car itself, not a trigger), or infinity within <paramref name="reach"/>.</summary>
        float AheadOf(Transform lamp, float reach)
        {
            int n = Physics.RaycastNonAlloc(lamp.position, lamp.forward, s_Ahead, reach, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
                if (!s_Ahead[i].collider.transform.IsChildOf(transform) && s_Ahead[i].distance < nearest) nearest = s_Ahead[i].distance;
            return nearest;
        }

        void Sound()
        {
            if (EngineOn != m_WasEngine)
            {
                m_WasEngine = EngineOn;
                if (!m_Scripted) // (arriving, the engine has been running all the way)
                    Sfx.At(EngineOn ? Sfx.Bank?.CarStart : Sfx.Bank?.CarStop, transform.position + transform.forward * 1.5f, 1f, Sfx.Group.Items);
            }
            if (Engine == null) return;
            if (Engine.clip == null && Sfx.Bank != null) { Engine.clip = Sfx.Bank.CarEngine; if (Engine.clip != null) Engine.Play(); }
            float revs = Mathf.Clamp01(Mathf.Abs(Speed) / Mathf.Max(1f, m_Physics.Settings.MaxSpeed));
            float target = EngineOn || m_Scripted ? 0.55f + 0.35f * revs : 0f;
            float muffle = AudioDirector.Instance != null ? AudioDirector.Instance.Muffle(transform.position) : 1f;
            Engine.volume = Mathf.MoveTowards(Engine.volume, target * muffle, Time.deltaTime * 1.5f);
            Engine.pitch = Mathf.Lerp(Engine.pitch, 0.8f + revs * 1.1f + Mathf.Abs(m_Input.Throttle) * 0.15f, 1f - Mathf.Exp(-5f * Time.deltaTime));
        }

        /// <summary>The driver honks (everyone hears it).</summary>
        public void Horn()
        {
            if (m_Link != null) m_Link.SendHorn();
            else PlayHorn();
        }

        public void PlayHorn() => Sfx.At(Sfx.Bank?.CarHorn, transform.position + transform.forward * 2f, 1f, Sfx.Group.Items);

        /// <summary>Test hook: put the car somewhere (its owner moves it, the twin syncs it).</summary>
        public void Place(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            m_Physics.Speed = 0f;
        }
    }
}
