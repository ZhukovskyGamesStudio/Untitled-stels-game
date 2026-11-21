using System.Collections.Generic;
using Stealth.Game;
using Stealth.Level;
using Stealth.Player;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Stealth.Net
{
    /// <summary>
    /// Entry point for starting / stopping a session and the bridge between Netcode and the game:
    /// registers <see cref="Session.Backend"/> (NGO) and <see cref="Session.Launcher"/> (this) on start.
    /// Steam: real online play with friends. Local: UDP on 127.0.0.1 / LAN for testing two instances on one PC.
    /// </summary>
    public class GameNetwork : MonoBehaviour, ISessionLauncher
    {
        public static GameNetwork Instance { get; private set; }

        public NetworkManager Manager;
        public UnityTransport LocalTransport;
        public SteamNetworkingTransport SteamTransport;
        public GameObject SessionPrefab;
        public Camera MenuCamera;
        public ushort LocalPort = 7777;

        public string Status { get; private set; } = "";
        public bool IsRunning => Manager != null && (Manager.IsServer || Manager.IsClient);
        public bool UsingSteam { get; private set; }

        void Awake()
        {
            Instance = this;
            // -port N: another local UDP port (several test windows hosting at once must not share one)
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-port");
            if (i >= 0 && i + 1 < args.Length && ushort.TryParse(args[i + 1], out var port)) LocalPort = port;
            Session.Backend = new NgoSessionBackend(Manager);
            Session.Launcher = this;
            PlayerController.LocalChanged += OnLocalPlayerChanged;
            Application.runInBackground = true;
            if (QualitySettings.vSyncCount == 0) Application.targetFrameRate = 144;
            Manager.OnClientDisconnectCallback += OnClientDisconnect;
            VoiceNet.Attach(Manager);
            Manager.OnClientConnectedCallback += id =>
            {
                if (id == Manager.LocalClientId) SetStatus("");
            };
        }

        void OnDestroy()
        {
            PlayerController.LocalChanged -= OnLocalPlayerChanged;
            if (Session.Launcher == (ISessionLauncher)this) Session.Launcher = null;
            if (Instance == this) Session.Backend = null;
        }

        void OnLocalPlayerChanged(bool present) => ShowMenuCamera(!present);

        public void SetStatus(string status)
        {
            Status = status;
            if (!string.IsNullOrEmpty(status)) Debug.Log("[Net] " + status);
        }

        public void StartHostSteam()
        {
            UsingSteam = true;
            Manager.NetworkConfig.NetworkTransport = SteamTransport;
            if (Manager.StartHost()) OnHostStarted();
            else SetStatus(Loc.T("net.host_steam_failed"));
        }

        public void StartClientSteam(ulong hostSteamId)
        {
            UsingSteam = true;
            SteamTransport.ConnectToSteamId = hostSteamId;
            Manager.NetworkConfig.NetworkTransport = SteamTransport;
            SetStatus(Loc.T("net.connecting_steam"));
            if (!Manager.StartClient()) SetStatus(Loc.T("net.connect_failed"));
        }

        public void StartHostLocal()
        {
            UsingSteam = false;
            LocalTransport.SetConnectionData("127.0.0.1", LocalPort, "0.0.0.0");
            Manager.NetworkConfig.NetworkTransport = LocalTransport;
            if (Manager.StartHost()) OnHostStarted();
            else SetStatus(Loc.F("net.port_failed", LocalPort));
        }

        public void StartClientLocal(string address)
        {
            UsingSteam = false;
            LocalTransport.SetConnectionData(string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim(), LocalPort);
            Manager.NetworkConfig.NetworkTransport = LocalTransport;
            SetStatus(Loc.F("net.connecting", address, LocalPort));
            if (!Manager.StartClient()) SetStatus(Loc.T("net.connect_failed"));
        }

        void OnHostStarted()
        {
            SetStatus("");
            var session = Instantiate(SessionPrefab);
            session.GetComponent<NetworkObject>().Spawn();
        }

        public void Leave()
        {
            Manager.Shutdown();
            if (SteamLobby.Instance != null) SteamLobby.Instance.Leave();
            ActiveLevel.Clear();
            ShowMenuCamera(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnClientDisconnect(ulong clientId)
        {
            if (Manager.IsServer || clientId != Manager.LocalClientId) return;
            // We were disconnected from the host.
            SetStatus(string.IsNullOrEmpty(Manager.DisconnectReason) ? Loc.T("net.lost") : Manager.DisconnectReason);
            Leave();
        }

        public void ShowMenuCamera(bool show)
        {
            if (MenuCamera != null) MenuCamera.gameObject.SetActive(show);
        }

        void OnApplicationQuit()
        {
            if (IsRunning) Manager.Shutdown();
        }

        // ------------------------------------------------------------------ ISessionLauncher (menu)

        ushort ISessionLauncher.LocalPort => LocalPort;
        bool ISessionLauncher.IsHost => Manager.IsHost;
        bool ISessionLauncher.SteamAvailable => SteamManager.Initialized;
        string ISessionLauncher.SteamStatus => SteamManager.Initialized ? Loc.F("net.steam_on", SteamManager.PersonaName) : Loc.F("net.steam_off", SteamManager.Error);
        string ISessionLauncher.LocalPlayerName => SteamManager.Initialized ? SteamManager.PersonaName : null;
        string ISessionLauncher.LocalPlayerId
        {
            get
            {
                if (SteamManager.Initialized) return "steam:" + Steamworks.SteamUser.GetSteamID().m_SteamID;
                const string key = "stealth.player_id";
                if (!PlayerPrefs.HasKey(key)) PlayerPrefs.SetString(key, System.Guid.NewGuid().ToString("N"));
                return "local:" + PlayerPrefs.GetString(key) + ":" + LocalPort; // two windows on one computer are two players
            }
        }
        bool ISessionLauncher.CanInviteFriends => UsingSteam && SteamLobby.Instance != null && SteamLobby.Instance.InLobby;

        IReadOnlyList<FriendLobby> ISessionLauncher.FriendLobbies =>
            SteamLobby.Instance != null ? SteamLobby.Instance.Friends.Lobbies : System.Array.Empty<FriendLobby>();

        void ISessionLauncher.HostSteam() => SteamLobby.Instance.HostLobby();
        void ISessionLauncher.RefreshFriendLobbies() => SteamLobby.Instance?.RefreshFriends();

        void ISessionLauncher.JoinFriendLobby(ulong lobbyId)
        {
            if (IsRunning) Leave();
            SteamLobby.Instance.JoinLobby(lobbyId);
        }
        void ISessionLauncher.HostLocal() => StartHostLocal();
        void ISessionLauncher.JoinLocal(string address) => StartClientLocal(address);
        void ISessionLauncher.InviteFriends() => SteamLobby.Instance.OpenInviteDialog();
    }
}
