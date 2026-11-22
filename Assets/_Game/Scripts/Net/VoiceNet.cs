using System.Collections.Generic;
using Stealth.Audio;
using Stealth.Game;
using Unity.Collections;
using Unity.Netcode;

namespace Stealth.Net
{
    /// <summary>
    /// Voice frames over the session's transport (Steam or UDP) as unreliable named messages: a lost frame is better
    /// rebuilt by the codec than resent late. Client → server "stealth.voice.up", server → clients "stealth.voice.down"
    /// (<see cref="VoiceHub"/> decides what to do with them).
    /// </summary>
    public sealed class VoiceNet : IVoiceTransport
    {
        const string Up = "stealth.voice.up";
        const string Down = "stealth.voice.down";

        readonly NetworkManager m_Manager;
        readonly byte[] m_In = new byte[VoicePacket.MaxSize];
        readonly List<ulong> m_Targets = new();

        VoiceNet(NetworkManager manager) => m_Manager = manager;

        /// <summary>Hooks the voice messages into this manager's sessions (GameNetwork.Awake).</summary>
        public static void Attach(NetworkManager manager)
        {
            var net = new VoiceNet(manager);
            manager.OnServerStarted += net.Register;
            manager.OnClientStarted += net.Register;
            manager.OnServerStopped += _ => net.Unregister();
            manager.OnClientStopped += _ => net.Unregister();
        }

        void Register()
        {
            var cm = m_Manager.CustomMessagingManager;
            if (cm == null) return;
            cm.UnregisterNamedMessageHandler(Up);
            cm.UnregisterNamedMessageHandler(Down);
            if (m_Manager.IsServer) cm.RegisterNamedMessageHandler(Up, OnUp);
            else cm.RegisterNamedMessageHandler(Down, OnDown);
            VoiceHub.Transport = this;
            VoiceHub.Clear();
        }

        void Unregister()
        {
            if (VoiceHub.Transport == this) VoiceHub.Transport = null;
            VoiceHub.Clear();
            var cm = m_Manager.CustomMessagingManager;
            if (cm == null) return;
            cm.UnregisterNamedMessageHandler(Up);
            cm.UnregisterNamedMessageHandler(Down);
        }

        int Read(FastBufferReader reader)
        {
            int n = reader.Length - reader.Position;
            if (n <= 0 || n > m_In.Length) return 0;
            var buffer = m_In;
            reader.ReadBytesSafe(ref buffer, n);
            return n;
        }

        void OnUp(ulong sender, FastBufferReader reader)
        {
            int n = Read(reader);
            if (n > 0) VoiceHub.ReceiveServer(sender, m_In, 0, n);
        }

        void OnDown(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId) return;
            int n = Read(reader);
            if (n > 0) VoiceHub.ReceiveRelay(m_In, 0, n);
        }

        public void ToServer(byte[] frame, int length)
        {
            if (!m_Manager.IsConnectedClient) return;
            using var w = new FastBufferWriter(length, Allocator.Temp);
            w.WriteBytesSafe(frame, length);
            m_Manager.CustomMessagingManager.SendNamedMessage(Up, NetworkManager.ServerClientId, w, NetworkDelivery.Unreliable);
        }

        public void ToClients(byte[] relay, int length, ulong except)
        {
            if (!m_Manager.IsServer) return;
            m_Targets.Clear();
            foreach (ulong id in m_Manager.ConnectedClientsIds)
                if (id != NetworkManager.ServerClientId && id != except) m_Targets.Add(id);
            if (m_Targets.Count == 0) return;
            using var w = new FastBufferWriter(length, Allocator.Temp);
            w.WriteBytesSafe(relay, length);
            m_Manager.CustomMessagingManager.SendNamedMessage(Down, m_Targets, w, NetworkDelivery.Unreliable);
        }
    }
}
