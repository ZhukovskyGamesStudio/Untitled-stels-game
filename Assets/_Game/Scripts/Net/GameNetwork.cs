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
            Session.Backend = new NgoSessionBackend(Manager);
            Session.Launcher = this;
            PlayerController.LocalChanged += OnLocalPlayerChanged;
            Application.runInBackground = true;
            if (QualitySettings.vSyncCount == 0) Application.targetFrameRate = 144;
            Manager.OnClientDisconnectCallback += OnClientDisconnect;
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
            else SetStatus("Не удалось запустить хост через Steam");
        }

        public void StartClientSteam(ulong hostSteamId)
        {
            UsingSteam = true;
            SteamTransport.ConnectToSteamId = hostSteamId;
            Manager.NetworkConfig.NetworkTransport = SteamTransport;
            SetStatus("Подключаемся к хосту через Steam…");
            if (!Manager.StartClient()) SetStatus("Не удалось подключиться через Steam");
        }

        public void StartHostLocal()
        {
            UsingSteam = false;
            LocalTransport.SetConnectionData("127.0.0.1", LocalPort, "0.0.0.0");
            Manager.NetworkConfig.NetworkTransport = LocalTransport;
            if (Manager.StartHost()) OnHostStarted();
            else SetStatus($"Не удалось открыть порт {LocalPort}");
        }

        public void StartClientLocal(string address)
        {
            UsingSteam = false;
            LocalTransport.SetConnectionData(string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim(), LocalPort);
            Manager.NetworkConfig.NetworkTransport = LocalTransport;
            SetStatus($"Подключаемся к {address}:{LocalPort}…");
            if (!Manager.StartClient()) SetStatus("Не удалось подключиться");
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
            SetStatus(string.IsNullOrEmpty(Manager.DisconnectReason) ? "Соединение с хостом потеряно" : Manager.DisconnectReason);
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
        string ISessionLauncher.SteamStatus => SteamManager.Initialized ? "подключён как " + SteamManager.PersonaName : "недоступен — " + SteamManager.Error;
        string ISessionLauncher.LocalPlayerName => SteamManager.Initialized ? SteamManager.PersonaName : null;
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
