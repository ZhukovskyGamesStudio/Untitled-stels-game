using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Procedural placeholder animation by part names (from the Blender models):
    /// rotors spin, wheels roll, legs swing while moving, flyers lean, walkers bob.
    /// Parts are animated through "*_Pivot" parents baked into the prefab by the asset builder
    /// (<see cref="BakePivots"/>), because mesh origins sit at the model origin.
    /// Runs on every peer from the replicated transform (speed is measured locally).
    /// </summary>
    public class RobotAnimator : MonoBehaviour
    {
        public const string PivotSuffix = "_Pivot";

        public Transform Model;

        /// <summary>From the robot's profile (DetectorProfile.Animation); defaults without one.</summary>
        AnimationSettings m_S = new();
        float RotorSpeed => m_S.RotorSpeed;
        float StepFrequency => m_S.StepFrequency;
        float LegSwing => m_S.LegSwing;
        float Lean => m_S.Lean;
        float Bob => m_S.Bob;

        Vector3 m_ModelBase;
        Vector3 m_Velocity;
        readonly List<Transform> m_Rotors = new();
        readonly List<Transform> m_Wheels = new();
        readonly List<(Transform t, float phase)> m_Legs = new();
        Vector3 m_LastPos;
        float m_Speed, m_Phase;

        public float Speed => m_Speed;

        /// <summary>Editor / builder: inserts a pivot above every moving part (rotors, wheels, legs, heads).</summary>
        public static void BakePivots(Transform root)
        {
            var parts = new List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (IsMovingPart(t.name) && !t.name.EndsWith(PivotSuffix) && (t.parent == null || !t.parent.name.EndsWith(PivotSuffix)))
                    parts.Add(t);
            foreach (var part in parts)
            {
                var r = part.GetComponent<Renderer>();
                if (r == null) continue;
                var b = r.bounds;
                var pivot = new GameObject(part.name + PivotSuffix).transform;
                pivot.SetParent(part.parent, true);
                // Legs swing from the hip; heads turn around the body's vertical axis; rotors and wheels spin in place.
                var axis = part.parent.position;
                pivot.position = part.name.StartsWith("AccentLeg") ? new Vector3(b.center.x, b.max.y, b.center.z)
                    : part.name.StartsWith("AccentHead") ? new Vector3(axis.x, b.center.y, axis.z)
                    : b.center;
                pivot.rotation = part.parent.rotation;
                part.SetParent(pivot, true);
            }
        }

        static bool IsMovingPart(string n) =>
            n.StartsWith("AccentRotor") || n.StartsWith("AccentWheel") || n.StartsWith("AccentLeg") || n.StartsWith("AccentHead");

        void Awake()
        {
            var detector = GetComponent<Detector>();
            if (detector != null && detector.ProfileBase != null) m_S = detector.ProfileBase.Animation;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.EndsWith(PivotSuffix)) continue;
                if (t.name.StartsWith("AccentRotor")) m_Rotors.Add(t);
                else if (t.name.StartsWith("AccentWheel")) m_Wheels.Add(t);
                else if (t.name.StartsWith("AccentLeg")) m_Legs.Add((t, m_Legs.Count % 2 == 0 ? 0f : Mathf.PI));
            }
            m_LastPos = transform.position;
            if (Model != null) m_ModelBase = Model.localPosition;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var d = transform.position - m_LastPos;
            m_LastPos = transform.position;
            d.y = 0;
            float speed = d.magnitude / dt;
            if (speed < 30f) m_Velocity = Vector3.Lerp(m_Velocity, d / dt, 1f - Mathf.Exp(-6f * dt));
            else speed = 0f; // teleport
            m_Speed = Mathf.Lerp(m_Speed, speed, 1f - Mathf.Exp(-8f * dt));
            m_Phase += m_Speed * StepFrequency * dt * Mathf.PI;

            foreach (var r in m_Rotors) r.Rotate(0f, RotorSpeed * dt, 0f, Space.Self);
            foreach (var w in m_Wheels) w.Rotate(m_Speed / 0.12f * Mathf.Rad2Deg * dt, 0f, 0f, Space.Self);
            float swing = Mathf.Clamp01(m_Speed / 2f) * LegSwing;
            foreach (var (leg, phase) in m_Legs)
                leg.localRotation = Quaternion.Euler(Mathf.Sin(m_Phase + phase) * swing, 0f, 0f);

            if (Model == null) return;
            if (Lean > 0f)
            {
                // Tilt towards the local velocity (forward speed pitches, sideways rolls).
                var local = transform.InverseTransformDirection(m_Velocity);
                Model.localRotation = Quaternion.Euler(Mathf.Clamp(local.z * Lean / 5f, -Lean, Lean), 0f, Mathf.Clamp(-local.x * Lean / 5f, -Lean, Lean));
                Model.localPosition = m_ModelBase + Vector3.up * Mathf.Sin(Time.time * 2.2f) * 0.05f; // hover wobble
            }
            else if (Bob > 0f)
                Model.localPosition = m_ModelBase + Vector3.up * Mathf.Abs(Mathf.Sin(m_Phase)) * Bob * Mathf.Clamp01(m_Speed / 2f);
        }
    }
}
