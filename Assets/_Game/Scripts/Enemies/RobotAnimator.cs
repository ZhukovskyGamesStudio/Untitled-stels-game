using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Procedural placeholder animation by part names (from the Blender models):
    /// AccentRotor* spin, AccentWheel* roll, AccentLeg* swing while moving, AccentHead turns (set by the owner script).
    /// Runs on every peer from the replicated transform (speed is measured locally).
    /// </summary>
    public class RobotAnimator : MonoBehaviour
    {
        public float RotorSpeed = 1800f;
        public float StepFrequency = 1.6f;
        public float LegSwing = 25f;

        readonly List<Transform> m_Rotors = new();
        readonly List<Transform> m_Wheels = new();
        readonly List<(Transform t, float phase)> m_Legs = new();
        Vector3 m_LastPos;
        float m_Speed, m_Phase;

        public float Speed => m_Speed;

        void Awake()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("AccentRotor")) m_Rotors.Add(Pivot(t));
                else if (t.name.StartsWith("AccentWheel")) m_Wheels.Add(Pivot(t));
                else if (t.name.StartsWith("AccentLeg")) m_Legs.Add((Pivot(t, top: true), m_Legs.Count % 2 == 0 ? 0f : Mathf.PI));
            }
            m_LastPos = transform.position;
        }

        /// <summary>Mesh pivots are at the model origin; insert a pivot at the part's centre (or its top for legs).</summary>
        static Transform Pivot(Transform part, bool top = false)
        {
            var r = part.GetComponent<Renderer>();
            if (r == null) return part;
            var b = r.bounds;
            var pivot = new GameObject(part.name + "_Pivot").transform;
            pivot.SetParent(part.parent, true);
            pivot.position = top ? new Vector3(b.center.x, b.max.y, b.center.z) : b.center;
            pivot.rotation = part.parent.rotation;
            part.SetParent(pivot, true);
            return pivot;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var d = transform.position - m_LastPos;
            m_LastPos = transform.position;
            d.y = 0;
            float speed = d.magnitude / dt;
            if (speed > 30f) speed = 0f;
            m_Speed = Mathf.Lerp(m_Speed, speed, 1f - Mathf.Exp(-8f * dt));
            m_Phase += m_Speed * StepFrequency * dt * Mathf.PI;

            foreach (var r in m_Rotors) r.Rotate(0f, RotorSpeed * dt, 0f, Space.Self);
            foreach (var w in m_Wheels) w.Rotate(m_Speed / 0.12f * Mathf.Rad2Deg * dt, 0f, 0f, Space.Self);
            float swing = Mathf.Clamp01(m_Speed / 2f) * LegSwing;
            foreach (var (leg, phase) in m_Legs)
                leg.localRotation = Quaternion.Euler(Mathf.Sin(m_Phase + phase) * swing, 0f, 0f);
        }
    }
}
