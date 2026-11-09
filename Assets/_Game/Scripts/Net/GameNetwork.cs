using Stealth.Game;
using Stealth.Level;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Stealth.Net
{
    /// <summary>
    /// Entry point for starting / stopping a session.
    /// Steam: real online play with friends. Local: UDP on 127.0.0.1 / LAN for testing two instances on one PC.
    /// </summary>
    public class GameNetwork : MonoBehaviour
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
            Application.runInBackground = true;
            if (QualitySettings.vSyncCount == 0) Application.targetFrameRate = 144;
            Manager.OnClientDisconnectCallback += OnClientDisconnect;
            Manager.OnClientConnectedCallback += id =>
            {
                if (id == Manager.LocalClientId) SetStatus("");
            };
        }

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
    }
}
