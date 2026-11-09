using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Robots share sightings over the radio (pure rules, unit tested) + a visual ripple.</summary>
    public static class RobotRadio
    {
        public const float Range = 15f;

        /// <summary>Indices of robots that hear a call from <paramref name="sender"/>: same floor, within range, not the sender.</summary>
        public static List<int> Recipients(Vector3 sender, IReadOnlyList<Vector3> robots, float range = Range)
        {
            var result = new List<int>();
            for (int i = 0; i < robots.Count; i++)
            {
                var d = robots[i] - sender;
                if (d.sqrMagnitude < 0.01f) continue;
                // Same storey: hovering drones are ~2 m up, the next floor is 3.5 m up.
                if (Mathf.Abs(d.y) > 2.6f) continue;
                d.y = 0;
                if (d.magnitude <= range) result.Add(i);
            }
            return result;
        }

        /// <summary>Expanding ring at the sender (every peer).</summary>
        public static void Ripple(Vector3 at, Material material)
        {
            var go = new GameObject("RadioRipple");
            go.transform.position = at;
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = material;
            lr.loop = true;
            lr.positionCount = 32;
            lr.widthMultiplier = 0.04f;
            lr.useWorldSpace = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<RippleFade>();
        }

        class RippleFade : MonoBehaviour
        {
            float m_T;
            LineRenderer m_Line;

            void Awake() => m_Line = GetComponent<LineRenderer>();

            void Update()
            {
                m_T += Time.deltaTime;
                float r = Mathf.Lerp(0.3f, 4f, m_T / 0.6f);
                for (int i = 0; i < m_Line.positionCount; i++)
                {
                    float a = i / (float)m_Line.positionCount * Mathf.PI * 2f;
                    m_Line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0.3f, Mathf.Sin(a) * r));
                }
                m_Line.widthMultiplier = 0.06f * (1f - m_T / 0.6f);
                if (m_T > 0.6f) Destroy(gameObject);
            }
        }
    }
}
