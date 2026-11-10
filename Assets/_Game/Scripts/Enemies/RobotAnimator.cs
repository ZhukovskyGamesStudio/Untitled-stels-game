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
            n.StartsWith("AccentRotor") || n.StartsWith("AccentWheel") || (n.StartsWith("AccentLeg") && !IsJointedLeg(n)) || n.StartsWith("AccentHead");

        /// <summary>Thigh / shin of a leg that walks by inverse kinematics (<see cref="ProceduralLegs"/>), not by swinging.</summary>
        static bool IsJointedLeg(string n) => n.Contains(ProceduralLegs.UpperSuffix) || n.Contains(ProceduralLegs.LowerSuffix);

        Detector m_Detector;
        RobotController m_Robot;
        readonly List<(Transform t, Quaternion rest)> m_Eyes = new();
        readonly List<(Transform t, Quaternion rest)> m_Mics = new();
        readonly List<(Transform t, Vector3 rest)> m_Probes = new();
        readonly List<(Transform t, Quaternion rest)> m_Noses = new();
        Quaternion m_ModelRest;
        float m_Seed, m_LastYaw, m_TurnRate;
        float m_Recoil, m_Stomp, m_Slump, m_EyeYaw;
        RobotState m_LastState;

        /// <summary>Seismic stomp (every peer, from the network pulse): the body dips, the floor probes punch.</summary>
        public void Stomp() => m_Stomp = 1f;

        void Awake()
        {
            m_Detector = GetComponent<Detector>();
            m_Robot = m_Detector as RobotController;
            if (m_Detector != null && m_Detector.ProfileBase != null) m_S = m_Detector.ProfileBase.Animation;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("SensorEye")) m_Eyes.Add((t, t.localRotation));
                else if (t.name.StartsWith("SensorMic")) m_Mics.Add((t, t.localRotation));
                else if (t.name.StartsWith("SensorGround")) m_Probes.Add((t, t.localPosition));
                else if (t.name.StartsWith("SensorNose")) m_Noses.Add((t, t.localRotation));
                if (!t.name.EndsWith(PivotSuffix)) continue;
                if (t.name.StartsWith("AccentRotor")) m_Rotors.Add(t);
                else if (t.name.StartsWith("AccentWheel")) m_Wheels.Add(t);
                else if (t.name.StartsWith("AccentLeg") && !IsJointedLeg(t.name)) m_Legs.Add((t, m_Legs.Count % 2 == 0 ? 0f : Mathf.PI));
            }
            m_LastPos = transform.position;
            m_LastYaw = transform.eulerAngles.y;
            m_Seed = (transform.position.x * 7.1f + transform.position.z * 3.3f) % 11f; // robots out of step with each other
            if (Model != null)
            {
                m_ModelBase = Model.localPosition;
                m_ModelRest = Model.localRotation;
            }
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
            float yaw = transform.eulerAngles.y;
            m_TurnRate = Mathf.Lerp(m_TurnRate, Mathf.DeltaAngle(m_LastYaw, yaw) / dt, 1f - Mathf.Exp(-6f * dt));
            m_LastYaw = yaw;

            bool hacked = m_Detector != null && m_Detector.IsHacked;
            var state = m_Robot != null ? m_Robot.State : RobotState.Patrol;
            if (state == RobotState.Alerted && m_LastState != RobotState.Alerted) m_Recoil = 1f; // startled
            m_LastState = state;
            m_Recoil = Mathf.MoveTowards(m_Recoil, 0f, dt * 2.2f);
            m_Stomp = Mathf.MoveTowards(m_Stomp, 0f, dt * 3.5f);
            m_Slump = Mathf.MoveTowards(m_Slump, hacked ? 1f : 0f, dt * (hacked ? 1.5f : 3f));

            float power = 1f - m_Slump; // a hacked robot winds down: rotors, wheels and legs stop
            foreach (var r in m_Rotors) r.Rotate(0f, RotorSpeed * dt * Mathf.Max(power, 0.05f), 0f, Space.Self);
            foreach (var w in m_Wheels) w.Rotate(m_Speed / 0.12f * Mathf.Rad2Deg * dt, 0f, 0f, Space.Self);
            float swing = Mathf.Clamp01(m_Speed / 2f) * LegSwing;
            foreach (var (leg, phase) in m_Legs)
                leg.localRotation = Quaternion.Euler(Mathf.Sin(m_Phase + phase) * swing, 0f, 0f);

            AnimateSensors(dt, hacked, state);
            // Devices (turret, spider, vacuum…) move their bodies themselves; only mobile robots get the body motion.
            if (Model == null || m_Robot == null) return;

            var local = transform.InverseTransformDirection(m_Velocity);
            float t = Time.time + m_Seed;
            if (Lean > 0f)
            {
                // Flyers tilt towards the local velocity (forward speed pitches, sideways rolls) and wobble on the air.
                Model.localRotation = m_ModelRest * Quaternion.Euler(
                    Mathf.Clamp(local.z * Lean / 5f, -Lean, Lean) - m_Recoil * 18f + m_Slump * 25f, 0f,
                    Mathf.Clamp(-local.x * Lean / 5f - m_TurnRate * 0.05f, -Lean, Lean));
                Model.localPosition = m_ModelBase + Vector3.up * (Mathf.Sin(t * 2.2f) * 0.05f * power - m_Slump * 0.6f + m_Recoil * 0.15f);
                return;
            }
            // Ground robots: lean into speed and turns, startle back when they spot someone, sag when hacked,
            // breathe a little when standing, dip on a seismic stomp.
            bool chasing = state == RobotState.Chase;
            float pitch = Mathf.Clamp(local.z, -3f, 6f) * (chasing ? 1.6f : 0.9f) - m_Recoil * 10f + m_Slump * 14f;
            float roll = Mathf.Clamp(-m_TurnRate * 0.03f, -7f, 7f) * Mathf.Clamp01(m_Speed);
            Model.localRotation = m_ModelRest * Quaternion.Euler(pitch, 0f, roll);
            float bob = Bob > 0f ? Mathf.Abs(Mathf.Sin(m_Phase)) * Bob * Mathf.Clamp01(m_Speed / 2f) : 0f;
            float breathe = Mathf.Sin(t * 1.6f) * 0.012f * (1f - Mathf.Clamp01(m_Speed)) * power;
            Model.localPosition = m_ModelBase + Vector3.up * (bob + breathe + m_Recoil * 0.06f - m_Stomp * 0.14f - m_Slump * 0.08f);
        }

        /// <summary>
        /// The sensor attachments live: the camera eye sweeps while idle and tracks the player it notices, the microphones
        /// twitch, the floor probes punch on a stomp, the nose sniffs. Everything droops when hacked.
        /// </summary>
        void AnimateSensors(float dt, bool hacked, RobotState state)
        {
            float t = Time.time + m_Seed;
            float target = Mathf.Sin(t * 0.8f) * 35f; // idle sweep
            if (m_Detector != null && !hacked && m_Detector.Awareness > 0.05f && m_Detector.FocusClient != Detector.Nobody)
            {
                var p = Player.PlayerController.Find(m_Detector.FocusClient);
                if (p != null)
                {
                    var to = transform.InverseTransformDirection(p.transform.position - transform.position);
                    target = Mathf.Clamp(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, -70f, 70f);
                }
            }
            if (hacked) target = 0f;
            float turn = state is RobotState.Alerted or RobotState.Chase ? 400f : 120f;
            m_EyeYaw = Mathf.MoveTowardsAngle(m_EyeYaw, target, turn * dt);
            foreach (var (eye, rest) in m_Eyes)
                eye.localRotation = Quaternion.Euler(m_Slump * 35f, m_EyeYaw, 0f) * rest;
            foreach (var (mic, rest) in m_Mics)
                mic.localRotation = rest * Quaternion.Euler(Mathf.Sin(t * 3.1f) * 6f * (1f - m_Slump) - m_Slump * 30f, 0f, 0f);
            foreach (var (probe, rest) in m_Probes)
                probe.localPosition = rest + Vector3.down * (m_Stomp * 0.08f);
            foreach (var (nose, rest) in m_Noses)
                nose.localRotation = rest * Quaternion.Euler(Mathf.Sin(t * 7f) * 4f * (1f - m_Slump) + m_Slump * 25f, 0f, 0f);
        }
    }
}
