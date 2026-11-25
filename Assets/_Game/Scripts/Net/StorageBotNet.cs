using Stealth.Game;
using Unity.Netcode;

namespace Stealth.Net
{
    /// <summary>
    /// Network twin of <see cref="StorageBot"/>: the server writes what the mule is doing and what its body does, every
    /// other peer applies it (its place comes with the server NetworkTransform on the same object); a pat is shown to
    /// everyone (<see cref="IStorageBotLink"/>).
    /// </summary>
    public class StorageBotNet : NetworkBehaviour, IStorageBotLink
    {
        readonly NetworkVariable<byte> m_Mode = new();
        readonly NetworkVariable<byte> m_Motion = new();
        StorageBot m_Bot;

        void Awake() => m_Bot = GetComponent<StorageBot>();

        public override void OnNetworkSpawn()
        {
            if (m_Bot != null) m_Bot.Bind(this);
        }

        public override void OnNetworkDespawn()
        {
            if (m_Bot != null) m_Bot.Bind(null);
        }

        void LateUpdate()
        {
            if (!IsSpawned || m_Bot == null) return;
            if (IsServer)
            {
                NetSync.Set(m_Mode, (byte)m_Bot.Mode);
                NetSync.Set(m_Motion, (byte)m_Bot.Motion);
            }
            else m_Bot.ApplyReplicated((BotMode)m_Mode.Value, (BotMotion)m_Motion.Value);
        }

        public void BroadcastPet() => PetRpc();

        [Rpc(SendTo.Everyone)]
        void PetRpc() => m_Bot?.ReceivePet();
    }
}
