using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Netcode;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace Stealth.Net
{
    /// <summary>
    /// Netcode for GameObjects transport over Steam Networking Sockets (P2P through Valve relays, no port forwarding).
    /// The host listens on virtual port 0; clients connect to the host's SteamID taken from the lobby owner.
    /// </summary>
    public class SteamNetworkingTransport : NetworkTransport
    {
        [Tooltip("SteamID of the host to connect to (set by SteamLobby before StartClient).")]
        public ulong ConnectToSteamId;

        public override ulong ServerClientId => 0;
        public override bool IsSupported => Application.platform != RuntimePlatform.WebGLPlayer;

        readonly Queue<(NetworkEvent type, ulong clientId, ArraySegment<byte> data)> m_Events = new();

#if !DISABLESTEAMWORKS
        readonly Dictionary<ulong, HSteamNetConnection> m_ClientToConn = new();
        readonly Dictionary<HSteamNetConnection, ulong> m_ConnToClient = new();
        readonly List<ulong> m_ClientIds = new();
        readonly IntPtr[] m_Messages = new IntPtr[256];
        Callback<SteamNetConnectionStatusChangedCallback_t> m_StatusCallback;
        HSteamListenSocket m_Listen = HSteamListenSocket.Invalid;
        HSteamNetConnection m_ServerConn = HSteamNetConnection.Invalid;
        bool m_IsServer;
        ulong m_NextClientId = 1;

        public override void Initialize(NetworkManager networkManager = null)
        {
            m_Events.Clear();
            if (SteamManager.Initialized && m_StatusCallback == null)
                m_StatusCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);
        }

        public override bool StartServer()
        {
            if (!SteamManager.Initialized) { Debug.LogError("[SteamTransport] Steam is not initialized"); return false; }
            SteamNetworkingUtils.InitRelayNetworkAccess();
            m_IsServer = true;
            m_Listen = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);
            Debug.Log($"[SteamTransport] Listening as {SteamUser.GetSteamID()}");
            return m_Listen != HSteamListenSocket.Invalid;
        }

        public override bool StartClient()
        {
            if (!SteamManager.Initialized) { Debug.LogError("[SteamTransport] Steam is not initialized"); return false; }
            SteamNetworkingUtils.InitRelayNetworkAccess();
            m_IsServer = false;
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(new CSteamID(ConnectToSteamId));
            m_ServerConn = SteamNetworkingSockets.ConnectP2P(ref identity, 0, 0, null);
            Debug.Log($"[SteamTransport] Connecting to {ConnectToSteamId}");
            return m_ServerConn != HSteamNetConnection.Invalid;
        }

        void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t cb)
        {
            var conn = cb.m_hConn;
            switch (cb.m_info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    if (m_IsServer && cb.m_info.m_hListenSocket == m_Listen)
                    {
                        var result = SteamNetworkingSockets.AcceptConnection(conn);
                        if (result != EResult.k_EResultOK) Debug.LogWarning($"[SteamTransport] Accept failed: {result}");
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    if (m_IsServer)
                    {
                        if (m_ConnToClient.ContainsKey(conn)) break;
                        ulong id = m_NextClientId++;
                        m_ClientToConn[id] = conn;
                        m_ConnToClient[conn] = id;
                        m_ClientIds.Add(id);
                        m_Events.Enqueue((NetworkEvent.Connect, id, default));
                        Debug.Log($"[SteamTransport] Client {id} connected ({cb.m_info.m_identityRemote.GetSteamID()})");
                    }
                    else if (conn == m_ServerConn)
                    {
                        m_Events.Enqueue((NetworkEvent.Connect, ServerClientId, default));
                        Debug.Log("[SteamTransport] Connected to host");
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    Debug.Log($"[SteamTransport] Connection closed: {cb.m_info.m_eState} {cb.m_info.m_szEndDebug}");
                    SteamNetworkingSockets.CloseConnection(conn, 0, "closed", false);
                    if (m_IsServer)
                    {
                        if (m_ConnToClient.TryGetValue(conn, out var clientId))
                        {
                            RemoveClient(clientId);
                            m_Events.Enqueue((NetworkEvent.Disconnect, clientId, default));
                        }
                    }
                    else if (conn == m_ServerConn)
                    {
                        m_ServerConn = HSteamNetConnection.Invalid;
                        m_Events.Enqueue((NetworkEvent.Disconnect, ServerClientId, default));
                    }
                    break;
            }
        }

        void RemoveClient(ulong clientId)
        {
            if (m_ClientToConn.TryGetValue(clientId, out var conn)) m_ConnToClient.Remove(conn);
            m_ClientToConn.Remove(clientId);
            m_ClientIds.Remove(clientId);
        }

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
        {
            HSteamNetConnection conn;
            if (m_IsServer)
            {
                if (!m_ClientToConn.TryGetValue(clientId, out conn)) return;
            }
            else conn = m_ServerConn;
            if (conn == HSteamNetConnection.Invalid) return;

            int flags = networkDelivery == NetworkDelivery.Unreliable || networkDelivery == NetworkDelivery.UnreliableSequenced
                ? Constants.k_nSteamNetworkingSend_UnreliableNoDelay
                : Constants.k_nSteamNetworkingSend_ReliableNoNagle;

            var handle = GCHandle.Alloc(payload.Array, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject() + payload.Offset;
                var result = SteamNetworkingSockets.SendMessageToConnection(conn, ptr, (uint)payload.Count, flags, out _);
                if (result != EResult.k_EResultOK && result != EResult.k_EResultIgnored)
                    Debug.LogWarning($"[SteamTransport] Send failed: {result}");
            }
            finally { handle.Free(); }
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            if (m_Events.Count == 0) Pump();
            receiveTime = Time.realtimeSinceStartup;
            if (m_Events.Count > 0)
            {
                var e = m_Events.Dequeue();
                clientId = e.clientId;
                payload = e.data;
                return e.type;
            }
            clientId = 0;
            payload = default;
            return NetworkEvent.Nothing;
        }

        void Pump()
        {
            if (m_IsServer)
            {
                for (int i = 0; i < m_ClientIds.Count; i++)
                    Receive(m_ClientToConn[m_ClientIds[i]], m_ClientIds[i]);
            }
            else if (m_ServerConn != HSteamNetConnection.Invalid)
            {
                Receive(m_ServerConn, ServerClientId);
            }
        }

        void Receive(HSteamNetConnection conn, ulong clientId)
        {
            int count = SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, m_Messages, m_Messages.Length);
            for (int i = 0; i < count; i++)
            {
                var msg = SteamNetworkingMessage_t.FromIntPtr(m_Messages[i]);
                var data = new byte[msg.m_cbSize];
                Marshal.Copy(msg.m_pData, data, 0, msg.m_cbSize);
                SteamNetworkingMessage_t.Release(m_Messages[i]);
                m_Events.Enqueue((NetworkEvent.Data, clientId, new ArraySegment<byte>(data)));
            }
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            if (m_ClientToConn.TryGetValue(clientId, out var conn))
                SteamNetworkingSockets.CloseConnection(conn, 0, "kicked", false);
            RemoveClient(clientId);
        }

        public override void DisconnectLocalClient()
        {
            if (m_ServerConn != HSteamNetConnection.Invalid)
                SteamNetworkingSockets.CloseConnection(m_ServerConn, 0, "disconnect", false);
            m_ServerConn = HSteamNetConnection.Invalid;
        }

        public override ulong GetCurrentRtt(ulong clientId)
        {
            var conn = m_IsServer ? (m_ClientToConn.TryGetValue(clientId, out var c) ? c : HSteamNetConnection.Invalid) : m_ServerConn;
            if (conn == HSteamNetConnection.Invalid) return 0;
            var status = new SteamNetConnectionRealTimeStatus_t();
            var lanes = new SteamNetConnectionRealTimeLaneStatus_t();
            SteamNetworkingSockets.GetConnectionRealTimeStatus(conn, ref status, 0, ref lanes);
            return (ulong)Mathf.Max(0, status.m_nPing);
        }

        public override void Shutdown()
        {
            if (!SteamManager.Initialized) return;
            foreach (var conn in m_ClientToConn.Values) SteamNetworkingSockets.CloseConnection(conn, 0, "shutdown", false);
            m_ClientToConn.Clear();
            m_ConnToClient.Clear();
            m_ClientIds.Clear();
            DisconnectLocalClient();
            if (m_Listen != HSteamListenSocket.Invalid) SteamNetworkingSockets.CloseListenSocket(m_Listen);
            m_Listen = HSteamListenSocket.Invalid;
            m_Events.Clear();
            m_NextClientId = 1;
        }
#else
        public override void Initialize(NetworkManager networkManager = null) { }
        public override bool StartServer() => false;
        public override bool StartClient() => false;
        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery) { }
        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        { clientId = 0; payload = default; receiveTime = 0; return NetworkEvent.Nothing; }
        public override void DisconnectRemoteClient(ulong clientId) { }
        public override void DisconnectLocalClient() { }
        public override ulong GetCurrentRtt(ulong clientId) => 0;
        public override void Shutdown() { }
#endif
    }
}
