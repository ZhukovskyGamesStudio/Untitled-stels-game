using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The drive to the next house (prefab Level/DriveScene), shown on every peer while the house is chosen and built:
    /// the pickup drives along an endless sewer tunnel — whole segments, walls all round, so whichever way the camera
    /// looks there is the tunnel and its haze, never the void — and bounces a little on the grate. The tunnel slides past
    /// under a car that stays put. Its own camera draws over the player's; the game takes it away once the house is
    /// there (the screen fades through black, <see cref="Stealth.UI.GameHUD"/>).
    /// It does not cut in: its camera starts where the player's eyes are in the car (<see cref="Begin"/> — the same cab,
    /// the same lens), leaves through the side window and settles ahead of the car by the walkway, looking back at it as
    /// the tunnel rushes by, swaying slowly; it never leaves the tunnel. Nor does it cut out: once the house stands the
    /// camera glides back the same way into the player's eyes in the real car (<see cref="Return"/>, <see cref="ReturnTo"/>)
    /// while the car slows to the speed the real one rolls into the basement at, and there the game takes the scene away.
    /// The player's own camera is off meanwhile (the house is being built behind it).
    /// </summary>
    public class DriveScene : MonoBehaviour
    {
        public static DriveScene Active { get; private set; }

        [Tooltip("Straight tunnel segments, whole (6 m each).")]
        [UnityEngine.Serialization.FormerlySerializedAs("CutSegments")]
        public GameObject[] Segments;
        [Tooltip("The pickup's model (visual only).")]
        public GameObject CarModel;
        public Camera Camera;
        public AudioSource Engine;
        public Light[] Lamps;
        [Tooltip("Metres per second the tunnel slides past.")]
        public float Speed = 9f;
        [Tooltip("Seconds the camera glides from the player's eyes in the car out to the side view.")]
        public float GlideTime = 3.2f;
        [Tooltip("Seconds the car takes to reach full speed from the speed it had in the sewer.")]
        public float SpeedUpTime = 3f;

        const int Count = 14;
        readonly List<Transform> m_Segments = new();
        Transform m_Car;
        readonly List<Transform> m_Wheels = new();
        float m_Travel;

        static readonly Vector3 CarAt = new(-1.5f, 0.02f, 0.3f);
        Pose m_EyeInCar;
        float m_FromFov, m_ToFov, m_FromNear, m_ToNear, m_FromSpeed, m_Started = -1f;
        bool m_Glide;
        GameObject m_Mine;
        readonly List<GameObject> m_Riders = new();
        Camera m_PlayerCamera;
        bool m_Returning, m_EyeKnown;
        float m_ReturnStart, m_ReturnTime = 1f, m_ReturnFrom, m_ReturnTo;

        /// <summary>Someone in the car: what they look like now (cloned) and where they sit, in the car's own space.</summary>
        public struct Rider
        {
            public Transform Visual;
            public Pose InCar;
            /// <summary>This peer's own player: shown once the camera is out of the cab (it starts inside their head).</summary>
            public bool Mine;
        }

        void Awake()
        {
            Active = this;
            // the player's own camera rests: the scene's draws the whole screen, and the next house is put up behind it
            var me = Player.PlayerController.Local;
            if (me != null && me.Camera != null && me.Camera.enabled) { m_PlayerCamera = me.Camera; m_PlayerCamera.enabled = false; }
            for (int i = 0; i < Count && Segments != null && Segments.Length > 0; i++)
            {
                var s = Instantiate(Segments[(i * 7 + 3) % Segments.Length], transform);
                s.transform.localPosition = new Vector3((i - Count / 2) * SewerPlan.SegmentLength, 0f, 0f);
                foreach (var c in s.GetComponentsInChildren<Collider>()) c.enabled = false; // only a picture: it slides every frame
                m_Segments.Add(s.transform);
            }
            if (CarModel != null)
            {
                var car = Instantiate(CarModel, transform);
                m_Car = car.transform;
                m_Car.localPosition = CarAt;
                m_Car.localRotation = Quaternion.Euler(0f, 90f, 0f); // driving along +X
                if (Vehicles.CarLook.NeedsMirror(Configs.Tuning.Run.Car)) Vehicles.CarLook.Mirror(m_Car);
                foreach (var t in car.GetComponentsInChildren<Transform>())
                    if (t.name.StartsWith("AccentWheel")) m_Wheels.Add(t);
            }
            if (Camera != null)
            {
                m_ToFov = m_FromFov = ViewFov;
                m_ToNear = m_FromNear = 0.1f;
                Camera.fieldOfView = ViewFov;
                Camera.nearClipPlane = 0.1f;
            }
            m_FromSpeed = Speed;
            Level.VisualStyles.Dress(gameObject);
            if (Engine != null && Audio.Sfx.Bank != null && Audio.Sfx.Bank.CarEngine != null)
            {
                Engine.clip = Audio.Sfx.Bank.CarEngine;
                Engine.loop = true;
                Engine.pitch = 1.55f;
                Engine.volume = 0.7f;
                Engine.Play();
            }
        }

        /// <summary>
        /// Start from the player's eyes: <paramref name="eyeInCar"/> — the camera's pose in the car's own space (null: the
        /// player is not at the car, the scene starts in its side view), its lens, and the car's speed in the sewer.
        /// </summary>
        public void Begin(Pose? eyeInCar, float fov, float near, float carSpeed, IEnumerable<Rider> riders = null)
        {
            m_Started = Time.time;
            m_FromSpeed = Mathf.Clamp(Mathf.Abs(carSpeed), 0f, Speed);
            m_Glide = eyeInCar.HasValue && Camera != null && m_Car != null;
            if (riders != null && m_Car != null)
                foreach (var r in riders) Seat(r);
            if (!m_Glide) return;
            m_EyeInCar = eyeInCar.Value;
            m_FromFov = fov;
            m_FromNear = Mathf.Min(near, m_ToNear);
            Place(0f);
        }

        /// <summary>A copy of a rider in its seat of this car: just the picture (its scripts stay off), lit and with a shadow.</summary>
        void Seat(Rider r)
        {
            if (r.Visual == null) return;
            var copy = Instantiate(r.Visual.gameObject, m_Car);
            copy.name = "Rider";
            copy.transform.SetLocalPositionAndRotation(r.InCar.position, r.InCar.rotation);
            copy.transform.localScale = r.Visual.lossyScale;
            foreach (var b in copy.GetComponentsInChildren<Behaviour>(true)) b.enabled = false; // scripts, any camera, listener or light
            foreach (var rend in copy.GetComponentsInChildren<Renderer>(true))
            {
                rend.forceRenderingOff = false;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; // the owner's own body was a shadow only
            }
            m_Riders.Add(copy);
            if (r.Mine) { m_Mine = copy; copy.SetActive(!m_Glide); }
        }

        /// <summary>0..1 of the glide (linear in time; the path eases its two legs itself): out of the cab, and back in on the return.</summary>
        public float Glide => m_Returning ? 1f - ReturnShare : m_Started < 0f || !m_Glide ? 1f : Mathf.Clamp01((Time.time - m_Started) / GlideTime);

        float ReturnShare => Mathf.Clamp01((Time.time - m_ReturnStart) / m_ReturnTime);

        /// <summary>The camera is gliding back into the player's eyes (<see cref="Return"/>).</summary>
        public bool Returning => m_Returning;
        /// <summary>Back in the eyes (or, for a player not in the car, the return's time is up): the scene can go.</summary>
        public bool Returned => m_Returning && ReturnShare >= 1f;

        /// <summary>
        /// The house stands: the camera glides back into the cab over <paramref name="seconds"/> while the car slows to
        /// <paramref name="speed"/> (the real car's, rolling into the basement). Where the eyes are comes every frame (<see cref="ReturnTo"/>).
        /// </summary>
        public void Return(float seconds, float speed)
        {
            if (m_Returning) return;
            m_Returning = true;
            m_ReturnStart = Time.time;
            m_ReturnTime = Mathf.Max(0.1f, seconds);
            m_ReturnFrom = m_Speed;
            m_ReturnTo = Mathf.Min(speed, Speed);
        }

        /// <summary>
        /// Where the player's eyes are now in the real car (its own space, null: not in the car — the scene holds its view),
        /// and their lens: the return glides there, so it ends exactly on the player's own view.
        /// </summary>
        public void ReturnTo(Pose? eyeInCar, float fov, float near)
        {
            m_EyeKnown = eyeInCar.HasValue && Camera != null && m_Car != null;
            if (!m_EyeKnown) { m_Glide = false; return; }
            m_Glide = true;
            m_EyeInCar = eyeInCar.Value;
            m_FromFov = fov;
            m_FromNear = Mathf.Min(near, m_ToNear);
        }

        /// <summary>Where the camera settles, in the scene's space: ahead of the car, by the right walkway, eye height.</summary>
        static readonly Vector3 View = new(CarAt.x + 4.6f, 1.45f, 2.35f);
        /// <summary>What it looks at: the car's middle, a little above the bed (not the bouncing body: the picture stays calm).</summary>
        static readonly Vector3 Focus = new(CarAt.x - 0.4f, 0.95f, CarAt.z);
        /// <summary>The settled lens: wide enough for the car and the tunnel running away behind it.</summary>
        const float ViewFov = 52f;

        static float Ease(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));

        /// <summary>
        /// The camera: out of the cab through the side window (a point just outside it), then forward along the car to
        /// its settled view, turning to the car on the way, the lens widening from the player's to the drive's. Once
        /// there it sways slowly along the walkway. Inside the tunnel all the way.
        /// </summary>
        void Place(float t)
        {
            var focus = transform.TransformPoint(Focus);
            float sway = Mathf.Sin((Time.time - Mathf.Max(0f, m_Started)) * 0.35f);
            var view = transform.TransformPoint(View + new Vector3(sway * 0.9f, sway * 0.12f, -Mathf.Abs(sway) * 0.25f));
            if (!m_Glide)
            {
                Camera.transform.SetPositionAndRotation(view, Quaternion.LookRotation(focus - view));
                Camera.fieldOfView = ViewFov;
                return;
            }
            var from = new Pose(m_Car.TransformPoint(m_EyeInCar.position), m_Car.rotation * m_EyeInCar.rotation);
            var window = transform.TransformPoint(new Vector3(CarAt.x + 0.5f, 1.35f, CarAt.z + 1.7f));
            float a = Ease(t / 0.35f), b = Ease((t - 0.25f) / 0.75f);
            var p = Vector3.Lerp(Vector3.Lerp(from.position, window, a), view, b);
            var look = Quaternion.LookRotation(focus - p);
            Camera.transform.SetPositionAndRotation(p, Quaternion.Slerp(from.rotation, look, Ease(t / 0.45f)));
            Camera.fieldOfView = Mathf.Lerp(m_FromFov, ViewFov, Ease(t / 0.6f));
            Camera.nearClipPlane = Mathf.Lerp(m_FromNear, m_ToNear, a);
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            if (m_PlayerCamera != null) m_PlayerCamera.enabled = true;
        }

        float m_Speed;

        void Update()
        {
            float dt = Time.deltaTime;
            // from the sewer's speed up to the drive's (the engine follows)
            float up = m_Started < 0f ? 1f : Mathf.Clamp01((Time.time - m_Started) / Mathf.Max(0.01f, SpeedUpTime));
            float speed = Mathf.Lerp(m_FromSpeed, Speed, up * up * (3f - 2f * up));
            if (m_Returning) speed = Mathf.Lerp(m_ReturnFrom, m_ReturnTo, Ease(ReturnShare)); // braking to the real car's roll
            m_Speed = speed;
            if (Engine != null && Engine.isPlaying)
            {
                Engine.pitch = Mathf.Lerp(0.9f, 1.55f, speed / Mathf.Max(0.01f, Speed));
                Engine.volume = 0.7f;
            }
            m_Travel += speed * dt;
            float span = Count * SewerPlan.SegmentLength;
            foreach (var s in m_Segments)
            {
                var p = s.localPosition;
                p.x -= speed * dt;
                if (p.x < -span / 2f - SewerPlan.SegmentLength) p.x += span;
                s.localPosition = p;
            }
            if (m_Car == null) return;
            // a light bounce on the grate's panels and the engine's shake
            float bounce = (Mathf.PerlinNoise(m_Travel * 1.4f, 0.2f) - 0.5f) * 0.07f + Mathf.Sin(Time.time * 41f) * 0.004f;
            float pitch = (Mathf.PerlinNoise(m_Travel * 0.9f, 3.1f) - 0.5f) * 1.6f;
            // the bounce and the drift grow with the speed (standing, the car stands still)
            float k = speed / Mathf.Max(0.01f, Speed);
            float drift = m_Started < 0f ? Mathf.Sin(Time.time * 0.4f) : Mathf.Sin((Time.time - m_Started) * 0.4f);
            m_Car.localPosition = CarAt + new Vector3(drift * 0.4f * k, bounce * k, 0f);
            m_Car.localRotation = Quaternion.Euler(pitch * k, 90f, 0f);
            foreach (var w in m_Wheels) w.Rotate(speed * dt / 0.42f * Mathf.Rad2Deg, 0f, 0f, Space.Self);
            if (Camera != null) Place(Glide);
            // the camera out of the cab: the player's own body sits there; back in their head: it goes
            if (m_Mine != null && m_Glide && m_Mine.activeSelf != (Glide >= 0.22f)) m_Mine.SetActive(Glide >= 0.22f);
            if (Camera != null)
            {
                // far down the tunnel: the sewer's haze, like the player's own camera (never Unity's default blue)
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = RenderSettings.fogColor;
            }
        }
    }
}
