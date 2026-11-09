using Stealth.Enemies;
using Stealth.Level;
using Unity.Netcode;

namespace Stealth.Net
{
    /// <summary>Network twin of <see cref="LaserGate"/>: replicates its pattern and width (set once by the server).</summary>
    public class LaserGateNet : NetworkBehaviour, INetSync
    {
        readonly NetworkVariable<LaserPattern> m_Pattern = new();
        readonly NetworkVariable<float> m_Width = new(1.8f);
        LaserGate m_Gate;

        void Awake() => m_Gate = GetComponent<LaserGate>();

        public void PushState()
        {
            NetSync.Set(m_Pattern, m_Gate.Pattern);
            NetSync.Set(m_Width, m_Gate.Width);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) PushState();
            else m_Gate.Configure(m_Pattern.Value, m_Width.Value);
            m_Gate.Spawned(NetworkObjectId, IsServer);
        }
    }
}
