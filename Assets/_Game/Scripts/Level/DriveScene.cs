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

        const int Count = 12;
        readonly List<Transform> m_Segments = new();
        Transform m_Car;
        readonly List<Transform> m_Wheels = new();
        float m_Travel;

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
                m_Car.localPosition = new Vector3(-1.5f, 0.02f, 0.3f);
                m_Car.localRotation = Quaternion.Euler(0f, 90f, 0f); // driving along +X
                foreach (var t in car.GetComponentsInChildren<Transform>())
                    if (t.name.StartsWith("AccentWheel")) m_Wheels.Add(t);
            }
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

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            m_Travel += Speed * dt;
            float span = Count * SewerPlan.SegmentLength;
            foreach (var s in m_Segments)
            {
                var p = s.localPosition;
                p.x -= Speed * dt;
                if (p.x < -span / 2f - SewerPlan.SegmentLength) p.x += span;
                s.localPosition = p;
            }
            if (m_Car == null) return;
            // a light bounce on the grate's panels and the engine's shake
            float bounce = (Mathf.PerlinNoise(m_Travel * 1.4f, 0.2f) - 0.5f) * 0.07f + Mathf.Sin(Time.time * 41f) * 0.004f;
            float pitch = (Mathf.PerlinNoise(m_Travel * 0.9f, 3.1f) - 0.5f) * 1.6f;
            m_Car.localPosition = new Vector3(-1.5f + Mathf.Sin(Time.time * 0.4f) * 0.4f, 0.02f + bounce, 0.3f);
            m_Car.localRotation = Quaternion.Euler(pitch, 90f, 0f);
            foreach (var w in m_Wheels) w.Rotate(Speed * dt / 0.42f * Mathf.Rad2Deg, 0f, 0f, Space.Self);
        }
    }
}
