using Stealth.Items;
using Unity.Netcode;

namespace Stealth.Net
{
    /// <summary>Network twin of <see cref="WorldItem"/>: replicates its shape and colour.</summary>
    public class WorldItemNet : NetworkBehaviour, INetSync
    {
        readonly NetworkVariable<byte> m_Shape = new();
        readonly NetworkVariable<byte> m_Color = new();
        readonly NetworkVariable<double> m_PlayFrom = new(), m_PlayUntil = new();
        WorldItem m_Item;

        void Awake() => m_Item = GetComponent<WorldItem>();

        public void PushState()
        {
            NetSync.Set(m_Shape, m_Item.Shape);
            NetSync.Set(m_Color, m_Item.Color);
            NetSync.Set(m_PlayFrom, m_Item.PlayFrom);
            NetSync.Set(m_PlayUntil, m_Item.PlayUntil);
        }

        void LateUpdate()
        {
            if (IsSpawned && IsServer && m_Item.Shape == ItemCatalog.Speaker) PushState();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) { PushState(); return; }
            Pull();
            m_Shape.OnValueChanged += (_, _) => Pull();
            m_Color.OnValueChanged += (_, _) => Pull();
            m_PlayFrom.OnValueChanged += (_, _) => m_Item.ApplyPlaying(m_PlayFrom.Value, m_PlayUntil.Value);
            m_PlayUntil.OnValueChanged += (_, _) => m_Item.ApplyPlaying(m_PlayFrom.Value, m_PlayUntil.Value);
        }

        void Pull()
        {
            m_Item.SetLook(m_Shape.Value, m_Color.Value);
            m_Item.ApplyPlaying(m_PlayFrom.Value, m_PlayUntil.Value);
        }
    }
}
