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
    public class Car : MonoBehaviour
    {
        public static Car Active { get; private set; }
        public const int SeatCount = 4;
        public const ulong Nobody = ulong.MaxValue;

        [Header("Model")]
        [Tooltip("Everything that bounces, pitches and rolls on the suspension.")]
        public Transform Body;
        [Tooltip("Front left, front right, rear left, rear right (each pivoted at its centre).")]
        public Transform[] Wheels = new Transform[4];
        public Transform Steering;
        public Transform Grinder;
        public Light[] Headlights;
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
        bool m_WasEngine;
        int m_CargoShown = -1;
        readonly List<CargoPiece> m_Pieces = new();
        static readonly RaycastHit[] s_Hits = new RaycastHit[16];

        public IReadOnlyList<ulong> Occupants => m_Occupants;
        public ulong Driver => m_Occupants[0];
        public bool EngineOn => Driver != Nobody;
        public float Speed => HasAuthority ? m_Physics.Speed : m_RemoteSpeed;
        public float SteerAngle => HasAuthority ? m_Physics.SteerAngle : m_RemoteSteer;
        /// <summary>How much the car turned since the last frame (seated players turn with it), degrees.</summary>
        public float YawDelta { get; private set; }
        public CarSettings Settings => m_Physics.Settings;

        void Awake()
        {
            Active = this;
            m_Physics = new CarPhysics(Tuning.Run.Car);
            m_WheelRest = new Quaternion[Wheels.Length];
            for (int i = 0; i < Wheels.Length; i++) if (Wheels[i] != null) m_WheelRest[i] = Wheels[i].localRotation;
            if (Steering != null) m_SteeringRest = Steering.localRotation;
            if (Grinder != null) m_GrinderRest = Grinder.localRotation;
            if (Body != null) m_BodyRest = Body.localPosition;
            m_LastPos = transform.position;
            m_LastYaw = transform.eulerAngles.y;
            if (Engine != null) { Engine.loop = true; Engine.volume = 0f; if (Engine.clip != null) Engine.Play(); }
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
            bool drive = LocalDriver && !GameMenu.IsOpen && !CheatPanel.IsOpen;
            m_Input = drive
                ? new CarInput
                {
                    Throttle = (Controls.Held(GameAction.Forward) ? 1f : 0f) - (Controls.Held(GameAction.Back) ? 1f : 0f),
                    Steer = (Controls.Held(GameAction.Right) ? 1f : 0f) - (Controls.Held(GameAction.Left) ? 1f : 0f),
                    Brake = Controls.Held(GameAction.Jump),
                }
                : new CarInput { Brake = !EngineOn };
            if (DevFlags.CarInput is CarInput forced && (LocalDriver || !Session.IsRunning)) m_Input = forced;
            if (drive && Controls.Pressed(GameAction.Horn)) Horn();


            if (HasAuthority) Simulate(dt);
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
            // on the floor: the grate under the wheels (not the car itself, a player, a thing lying around)
            if (Ground(pos, out float floor)) pos.y = Mathf.MoveTowards(pos.y, floor, 4f * dt);
            transform.SetPositionAndRotation(pos, rot);
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
            if (Steering != null) Steering.localRotation = m_SteeringRest * Quaternion.AngleAxis(-steer * 9f, new Vector3(0f, 0.42f, -0.9f).normalized);
            m_Grind = (m_Grind + (EngineOn ? 540f : 0f) * dt) % 360f;
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
            bool lights = EngineOn;
            if (Headlights != null) foreach (var l in Headlights) if (l != null && l.enabled != lights) l.enabled = lights;
        }

        void Sound()
        {
            if (EngineOn != m_WasEngine)
            {
                m_WasEngine = EngineOn;
                Sfx.At(EngineOn ? Sfx.Bank?.CarStart : Sfx.Bank?.CarStop, transform.position + transform.forward * 1.5f, 1f, Sfx.Group.Items);
            }
            if (Engine == null) return;
            if (Engine.clip == null && Sfx.Bank != null) { Engine.clip = Sfx.Bank.CarEngine; if (Engine.clip != null) Engine.Play(); }
            float revs = Mathf.Clamp01(Mathf.Abs(Speed) / Mathf.Max(1f, m_Physics.Settings.MaxSpeed));
            float target = EngineOn ? 0.55f + 0.35f * revs : 0f;
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
