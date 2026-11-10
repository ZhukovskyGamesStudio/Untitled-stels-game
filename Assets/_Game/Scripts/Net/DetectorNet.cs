using Stealth.Enemies;
using Stealth.Game;
using Unity.Netcode;

namespace Stealth.Net
{
    /// <summary>
    /// Network twin of every <see cref="Detector"/> (all robots, devices and the camera): replicates its
    /// <see cref="DetectorSnapshot"/> and carries one-shot effects (radio ripple, turret shot) to every peer.
    /// </summary>
    public class DetectorNet : NetworkBehaviour, INetSync, IDetectorLink
    {
        readonly NetworkVariable<byte> m_Awareness = new();
        readonly NetworkVariable<ulong> m_Focus = new(Detector.Nobody);
        readonly NetworkVariable<bool> m_Hacked = new();
        readonly NetworkVariable<byte> m_Mode = new();
        readonly NetworkVariable<sbyte> m_Variant = new(-1);
        readonly NetworkVariable<ulong> m_Target = new(Detector.Nobody);
        readonly NetworkVariable<short> m_Cargo = new(-1);
        Detector m_Detector;

        void Awake() => m_Detector = GetComponent<Detector>();

        public override void OnNetworkSpawn()
        {
            m_Detector.Spawned(this, NetworkObjectId, IsServer);
            if (IsServer) { PushState(); return; }
            Pull();
            m_Awareness.OnValueChanged += (_, _) => Pull();
            m_Focus.OnValueChanged += (_, _) => Pull();
            m_Hacked.OnValueChanged += (_, _) => Pull();
            m_Mode.OnValueChanged += (_, _) => Pull();
            m_Variant.OnValueChanged += (_, _) => Pull();
            m_Target.OnValueChanged += (_, _) => Pull();
            m_Cargo.OnValueChanged += (_, _) => Pull();
        }

        public override void OnNetworkDespawn() => m_Detector.Despawned();

        void LateUpdate()
        {
            if (IsSpawned && IsServer) PushState();
        }

        public void PushState()
        {
            var s = m_Detector.Snapshot;
            NetSync.Set(m_Awareness, s.Awareness);
            NetSync.Set(m_Focus, s.Focus);
            NetSync.Set(m_Hacked, s.Hacked);
            NetSync.Set(m_Mode, s.Mode);
            NetSync.Set(m_Variant, s.Variant);
            NetSync.Set(m_Target, s.Target);
            NetSync.Set(m_Cargo, s.Cargo);
        }

        void Pull() => m_Detector.ApplyReplicated(new DetectorSnapshot
        {
            Awareness = m_Awareness.Value,
            Focus = m_Focus.Value,
            Hacked = m_Hacked.Value,
            Mode = m_Mode.Value,
            Variant = m_Variant.Value,
            Target = m_Target.Value,
            Cargo = m_Cargo.Value,
        });

        public void BroadcastPulse(byte effect) => PulseRpc(effect);

        [Rpc(SendTo.Everyone)]
        void PulseRpc(byte effect) => m_Detector.ReceivePulse(effect);
    }
}
