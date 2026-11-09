using System.Collections.Generic;
using Stealth.Game;
using Stealth.Player;
using Unity.Netcode;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Laser barrier in a doorway. Kind 0: a single beam at chest height — crouch under it.
    /// Kind 1: three beams blinking on a timer — wait for the gap. Touching a beam hurts and raises an alarm.
    /// The beam cycle uses the shared server time, so every peer sees the same state.
    /// </summary>
    public class LaserGate : NetworkBehaviour
    {
        public Transform EmitterA;
        public Transform EmitterB;
        public Material BeamMaterial;

        public float OnTime = 2.4f;
        public float OffTime = 1.6f;
        public float AlarmRadius = 30f;

        public readonly NetworkVariable<byte> Kind = new();
        public readonly NetworkVariable<float> Width = new(1.8f);

        static readonly float[] LowBeam = { 1.25f };
        static readonly float[] Blinking = { 0.35f, 0.95f, 1.55f };

        readonly List<GameObject> m_Beams = new();
        readonly Dictionary<ulong, float> m_HitCooldown = new();
        float m_Phase;

        static readonly float[] Moving = { 0.8f };
        public const float MovePeriod = 3.2f;

        /// <summary>Kind 2: one beam sliding between 0.25 m (jump over) and 1.6 m (crouch under).</summary>
        public static float MovingHeight(float t, float phase = 0f) =>
            0.25f + 1.35f * (0.5f + 0.5f * Mathf.Sin((t + phase) * Mathf.PI * 2f / MovePeriod));

        float[] m_Current = new float[1];

        float[] Heights
        {
            get
            {
                if (Kind.Value == 0) return LowBeam;
                if (Kind.Value == 1) return Blinking;
                m_Current[0] = MovingHeight(NetworkManager.ServerTime.TimeAsFloat, m_Phase);
                return m_Current;
            }
        }

        public override void OnNetworkSpawn()
        {
            m_Phase = (NetworkObjectId % 5) * 0.8f;
            float w = Width.Value;
            EmitterA.localPosition = new Vector3(-w / 2f, 0, 0);
            EmitterB.localPosition = new Vector3(w / 2f, 0, 0);
            foreach (var h in Heights)
            {
                var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(beam.GetComponent<Collider>());
                beam.name = "Beam";
                beam.transform.SetParent(transform, false);
                beam.transform.localPosition = new Vector3(0, h, 0);
                beam.transform.localScale = new Vector3(w, 0.025f, 0.025f);
                var r = beam.GetComponent<MeshRenderer>();
                r.sharedMaterial = BeamMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                m_Beams.Add(beam);
            }
        }

        public bool BeamsOn
        {
            get
            {
                if (Kind.Value != 1) return true;
                float t = Mathf.Repeat(NetworkManager.ServerTime.TimeAsFloat + m_Phase, OnTime + OffTime);
                return t < OnTime;
            }
        }

        void Update()
        {
            if (!IsSpawned) return;
            bool on = BeamsOn;
            // Short flicker warning before the beams switch on.
            bool warn = false;
            if (Kind.Value == 1 && !on)
            {
                float t = Mathf.Repeat(NetworkManager.ServerTime.TimeAsFloat + m_Phase, OnTime + OffTime);
                warn = t > OnTime + OffTime - 0.45f && Mathf.Repeat(t * 12f, 1f) < 0.5f;
            }
            foreach (var b in m_Beams) b.SetActive(on || warn);
            if (Kind.Value == 2 && m_Beams.Count > 0)
                m_Beams[0].transform.localPosition = new Vector3(0, Heights[0], 0);

            if (!IsServer || !on) return;
            float half = Width.Value / 2f;
            foreach (var p in PlayerController.All)
            {
                if (p.Downed.Value) continue;
                var local = transform.InverseTransformPoint(p.transform.position);
                if (Mathf.Abs(local.x) > half || Mathf.Abs(local.z) > 0.35f) continue;
                bool touches = false;
                foreach (var h in Heights)
                    if (h > local.y && h < local.y + p.Height) { touches = true; break; }
                if (!touches) continue;
                if (m_HitCooldown.TryGetValue(p.OwnerClientId, out var until) && Time.time < until) continue;
                m_HitCooldown[p.OwnerClientId] = Time.time + 1.5f;
                p.TakeHitServer(transform.position);
                GameManager.Instance?.RaiseAlarmServer(transform.position, AlarmRadius);
            }
        }
    }
}
