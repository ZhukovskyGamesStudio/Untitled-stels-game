using Stealth.Items;
using Unity.Netcode;

namespace Stealth.Net
{
    /// <summary>Network twin of <see cref="WorldItem"/>: replicates its shape and colour.</summary>
    public class WorldItemNet : NetworkBehaviour, INetSync
    {
        readonly NetworkVariable<byte> m_Shape = new();
        readonly NetworkVariable<byte> m_Color = new();
        WorldItem m_Item;

        void Awake() => m_Item = GetComponent<WorldItem>();

        public void PushState()
        {
            NetSync.Set(m_Shape, m_Item.Shape);
            NetSync.Set(m_Color, m_Item.Color);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) { PushState(); return; }
            Pull();
            m_Shape.OnValueChanged += (_, _) => Pull();
            m_Color.OnValueChanged += (_, _) => Pull();
        }

        void Pull() => m_Item.SetLook(m_Shape.Value, m_Color.Value);
    }
}
