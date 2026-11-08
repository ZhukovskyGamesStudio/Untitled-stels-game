using System.Collections.Generic;
using Stealth.Player;
using Unity.Netcode;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Replicates "how much is this sensor noticing someone, and whom" so that the noticed player sees an
    /// indicator. Added to every robot / camera; their server logic calls <see cref="ReportServer"/>.
    /// </summary>
    public class DetectorAwareness : NetworkBehaviour
    {
        public const ulong Nobody = ulong.MaxValue;
        public static readonly List<DetectorAwareness> All = new();

        /// <summary>0..255 = 0..1 awareness; 255 = fully detected (chasing / alarm).</summary>
        public readonly NetworkVariable<byte> Level = new();
        public readonly NetworkVariable<ulong> Focus = new(Nobody);

        [Tooltip("World point the indicator should point to (eye / lens). Defaults to the transform.")]
        public Transform Point;

        float m_NextSend;

        public float Value => Level.Value / 255f;
        public Vector3 Position => Point != null ? Point.position : transform.position + Vector3.up;

        public override void OnNetworkSpawn() => All.Add(this);
        public override void OnNetworkDespawn() => All.Remove(this);

        /// <summary>Server: report current awareness (0..1) of <paramref name="focus"/>. Throttled to ~12 Hz.</summary>
        public void ReportServer(float level, PlayerController focus)
        {
            if (!IsServer) return;
            byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(level) * 255f), 0, 255);
            ulong f = focus != null && b > 0 ? focus.OwnerClientId : Nobody;
            bool important = (b == 255) != (Level.Value == 255) || f != Focus.Value || (b == 0) != (Level.Value == 0);
            if (!important && (Time.time < m_NextSend || Mathf.Abs(b - Level.Value) < 6)) return;
            m_NextSend = Time.time + 0.08f;
            Level.Value = b;
            Focus.Value = f;
        }

        /// <summary>Awareness of a specific player (0 if focused on someone else).</summary>
        public float AwarenessOf(ulong clientId) => Focus.Value == clientId ? Value : 0f;
    }
}
