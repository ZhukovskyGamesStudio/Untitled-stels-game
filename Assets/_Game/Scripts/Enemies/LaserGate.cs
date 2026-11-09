using System.Collections.Generic;
using Stealth.Game;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Laser barrier in a doorway (a hazard, not a detector). Patterns:
    ///  * Low — one beam at chest height: crouch under it;
    ///  * Blinking — three beams on a timer: wait for the gap;
    ///  * Moving — one beam sliding up and down: jump over or crouch under in time.
    /// Touching a beam hurts and raises an alarm. The cycle uses the shared server time, so every peer agrees.
    /// The beams are pre-built children of the prefab (<see cref="Beams"/>); unused ones stay hidden.
    /// </summary>
    public class LaserGate : MonoBehaviour
    {
        public Transform EmitterA;
        public Transform EmitterB;
        [Tooltip("Pre-built beam objects (unit length along X). Low / Moving use the first, Blinking uses three.")]
        public Transform[] Beams = new Transform[0];

        [Tooltip("Tuning (LaserGateProfile) — a separate asset next to the prefab.")]
        public LaserGateProfile Profile;

        public const float DefaultMovePeriod = 3.2f;
        public const float BeamThickness = 0.025f;

        /// <summary>Set by the server before spawning; replicated by the network twin (LaserGateNet).</summary>
        public LaserPattern Pattern { get; private set; }
        public float Width { get; private set; } = 1.8f;
        public bool IsServer { get; private set; } = true;
        public bool IsSpawned { get; private set; }

        static readonly float[] LowBeam = { 1.25f };
        static readonly float[] Blinking = { 0.35f, 0.95f, 1.55f };

        readonly float[] m_Moving = new float[1];
        readonly Dictionary<ulong, float> m_HitCooldown = new();
        float m_Phase;

        /// <summary>Moving pattern: one beam sliding between 0.25 m (jump over) and 1.6 m (crouch under).</summary>
        public static float MovingHeight(float t, float phase = 0f, float period = DefaultMovePeriod) =>
            0.25f + 1.35f * (0.5f + 0.5f * Mathf.Sin((t + phase) * Mathf.PI * 2f / period));

        /// <summary>Does a body standing at <paramref name="feetY"/> (local) with <paramref name="height"/> cross any beam?</summary>
        public static bool Touches(IReadOnlyList<float> beamHeights, float feetY, float height)
        {
            foreach (var h in beamHeights)
                if (h > feetY && h < feetY + height) return true;
            return false;
        }

        /// <summary>Blinking pattern: beams on in this moment of the cycle?</summary>
        public static bool BlinkOn(float cycleTime, float onTime) => cycleTime < onTime;

        /// <summary>Blinking pattern: short flicker just before the beams switch on.</summary>
        public static bool BlinkWarning(float cycleTime, float onTime, float offTime) =>
            cycleTime > onTime + offTime - 0.45f && Mathf.Repeat(cycleTime * 12f, 1f) < 0.5f;

        float Clock => Session.ServerTimeF;
        float CycleTime => Mathf.Repeat(Clock + m_Phase, Profile.OnTime + Profile.OffTime);

        float[] Heights
        {
            get
            {
                switch (Pattern)
                {
                    case LaserPattern.Low: return LowBeam;
                    case LaserPattern.Blinking: return Blinking;
                    default:
                        m_Moving[0] = MovingHeight(Clock, m_Phase, Profile.MovePeriod);
                        return m_Moving;
                }
            }
        }

        public bool BeamsOn => Pattern != LaserPattern.Blinking || BlinkOn(CycleTime, Profile.OnTime);

        public void Configure(LaserPattern pattern, float width)
        {
            Pattern = pattern;
            Width = width;
        }

        /// <summary>Called by the network twin once the pattern is known on this peer.</summary>
        public void Spawned(ulong netId, bool isServer)
        {
            IsServer = isServer;
            IsSpawned = true;
            m_Phase = netId % 5 * 0.8f;
            float w = Width;
            EmitterA.localPosition = new Vector3(-w / 2f, 0, 0);
            EmitterB.localPosition = new Vector3(w / 2f, 0, 0);
            foreach (var b in Beams) b.localScale = new Vector3(w, BeamThickness, BeamThickness);
            LayoutBeams();
        }

        void LayoutBeams()
        {
            var heights = Heights;
            for (int i = 0; i < Beams.Length; i++)
            {
                bool used = i < heights.Length;
                if (used) Beams[i].localPosition = new Vector3(0, heights[i], 0);
                else Beams[i].gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (!IsSpawned) return;
            bool on = BeamsOn;
            bool visible = on || Pattern == LaserPattern.Blinking && BlinkWarning(CycleTime, Profile.OnTime, Profile.OffTime);
            var heights = Heights;
            for (int i = 0; i < Beams.Length && i < heights.Length; i++)
            {
                Beams[i].gameObject.SetActive(visible);
                if (Pattern == LaserPattern.Moving) Beams[i].localPosition = new Vector3(0, heights[i], 0);
            }

            if (!IsServer || !on) return;
            float half = Width / 2f;
            foreach (var p in PlayerController.All)
            {
                if (p.IsDown) continue;
                var local = transform.InverseTransformPoint(p.Position);
                if (Mathf.Abs(local.x) > half || Mathf.Abs(local.z) > 0.35f) continue;
                if (!Touches(heights, local.y, p.Height)) continue;
                if (m_HitCooldown.TryGetValue(p.ClientId, out var until) && Time.time < until) continue;
                m_HitCooldown[p.ClientId] = Time.time + Profile.HitCooldown;
                p.HitServer(transform.position);
                if (GameManager.Instance != null) GameManager.Instance.RaiseAlarmServer(transform.position, Profile.AlarmRadius);
            }
        }
    }
}
