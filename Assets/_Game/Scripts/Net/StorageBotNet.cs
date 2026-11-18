using Stealth.Game;
using Unity.Netcode;

namespace Stealth.Net
{
    /// <summary>
    /// Network twin of <see cref="StorageBot"/>: the server writes what the robot is doing, every other peer applies it
    /// (its place comes with the server NetworkTransform on the same object).
    /// </summary>
    public class StorageBotNet : NetworkBehaviour
    {
        readonly NetworkVariable<byte> m_Mode = new();
        StorageBot m_Bot;

        void Awake() => m_Bot = GetComponent<StorageBot>();

        void LateUpdate()
        {
            if (!IsSpawned || m_Bot == null) return;
            if (IsServer) NetSync.Set(m_Mode, (byte)m_Bot.Mode);
            else m_Bot.ApplyReplicated((BotMode)m_Mode.Value);
        }
    }
}
