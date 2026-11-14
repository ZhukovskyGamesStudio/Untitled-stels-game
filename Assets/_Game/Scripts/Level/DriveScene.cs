using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The drive to the next house (prefab Level/DriveScene), shown on every peer while the house is chosen and built:
    /// the pickup drives along an endless sewer tunnel seen from the side — the near wall is cut away (the kit's cut
    /// segments), the view is long and narrow, almost flat, the car bounces a little on the grate. The tunnel slides past
    /// under a car that stays in the middle of the picture. Its own camera draws over the player's; the game takes it
    /// away once the house is there (the screen fades through black, <see cref="Stealth.UI.GameHUD"/>).
    /// It does not cut in: its camera starts where the player's eyes are in the car (<see cref="Begin"/> — the same cab,
    /// the same lens) and glides out to the side view while the car picks up speed, so the drive follows on from the sewer.
    /// </summary>
    public class DriveScene : MonoBehaviour
    {
        public static DriveScene Active { get; private set; }

        [Tooltip("Straight tunnel segments without the near wall (6 m each).")]
        public GameObject[] CutSegments;
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

        const int Count = 12;
        readonly List<Transform> m_Segments = new();
        Transform m_Car;
        readonly List<Transform> m_Wheels = new();
        float m_Travel;

        static readonly Vector3 CarAt = new(-1.5f, 0.02f, 0.3f);
        Pose m_EyeInCar, m_SideView;
        float m_FromFov, m_ToFov, m_FromNear, m_ToNear, m_FromSpeed, m_Started = -1f;
        bool m_Glide;
        GameObject m_Mine;
        readonly List<GameObject> m_Riders = new();

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
            for (int i = 0; i < Count && CutSegments != null && CutSegments.Length > 0; i++)
            {
                var s = Instantiate(CutSegments[(i * 7 + 3) % CutSegments.Length], transform);
                s.transform.localPosition = new Vector3((i - Count / 2) * SewerPlan.SegmentLength, 0f, 0f);
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
                m_SideView = new Pose(Camera.transform.localPosition, Camera.transform.localRotation);
                m_ToFov = m_FromFov = Camera.fieldOfView;
                m_ToNear = m_FromNear = Camera.nearClipPlane;
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
            if (r.Mine && m_Glide) { m_Mine = copy; copy.SetActive(false); }
        }

        /// <summary>0..1 of the glide (linear in time; the path eases its two legs itself).</summary>
        public float Glide => m_Started < 0f || !m_Glide ? 1f : Mathf.Clamp01((Time.time - m_Started) / GlideTime);

        /// <summary>How far from the car's side the camera stops after leaving the cab (inside the tunnel), metres.</summary>
        const float CloseSide = 2.3f;

        static float Ease(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));

        /// <summary>
        /// The camera along its two legs: out of the cab through its window to a close side view of the car (turning to
        /// it), then back away to the side view while the lens narrows just as fast as the distance grows — the picture
        /// stays filled by the tunnel, never the void beside the cut-away scene.
        /// </summary>
        void Place(float t)
        {
            var from = new Pose(m_Car.TransformPoint(m_EyeInCar.position), m_Car.rotation * m_EyeInCar.rotation);
            var to = new Pose(transform.TransformPoint(m_SideView.position), transform.rotation * m_SideView.rotation);
            var car = m_Car.position;
            float a = Ease(t / 0.4f), b = Ease((t - 0.35f) / 0.65f);
            var close = new Vector3(car.x, Mathf.Lerp(from.position.y, to.position.y, 0.5f), car.z + CloseSide);
            var p = Vector3.Lerp(Vector3.Lerp(from.position, close, a), to.position, b);
            Camera.transform.SetPositionAndRotation(p, Quaternion.Slerp(from.rotation, to.rotation, a));
            // the window at the car: from the close view's to the side view's height, whatever the distance on the way
            float closeHalf = CloseSide * Mathf.Tan(m_FromFov * 0.5f * Mathf.Deg2Rad);
            float farHalf = Mathf.Max(0.5f, to.position.z - car.z) * Mathf.Tan(m_ToFov * 0.5f * Mathf.Deg2Rad);
            float half = Mathf.Lerp(closeHalf, farHalf, b);
            float fov = 2f * Mathf.Atan(half / Mathf.Max(0.3f, p.z - car.z)) * Mathf.Rad2Deg;
            Camera.fieldOfView = b <= 0f ? m_FromFov : Mathf.Clamp(fov, m_ToFov, m_FromFov);
            Camera.nearClipPlane = Mathf.Lerp(m_FromNear, m_ToNear, b * b);
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            // from the sewer's speed up to the drive's (the engine follows)
            float up = m_Started < 0f ? 1f : Mathf.Clamp01((Time.time - m_Started) / Mathf.Max(0.01f, SpeedUpTime));
            float speed = Mathf.Lerp(m_FromSpeed, Speed, up * up * (3f - 2f * up));
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
            if (m_Glide) Place(Glide);
            if (m_Mine != null && !m_Mine.activeSelf && Glide >= 0.22f) m_Mine.SetActive(true); // the camera has left the cab
            if (Camera != null)
            {
                // past the cut-away's edges: the sewer's haze, like the player's own camera (never Unity's default blue)
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = RenderSettings.fogColor;
            }
        }
    }
}
