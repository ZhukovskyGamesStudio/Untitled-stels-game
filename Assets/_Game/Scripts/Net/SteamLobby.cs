using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace Stealth.Net
{
    /// <summary>
    /// Friends-only Steam lobby: the host creates it and starts the Netcode host, friends join through the
    /// Steam overlay ("Join game" / invite). Joining the lobby connects to the lobby owner's SteamID.
    /// </summary>
    public class SteamLobby : MonoBehaviour
    {
        public const int MaxPlayers = 4;
        public static SteamLobby Instance { get; private set; }

        public bool InLobby => LobbyId != 0;
        public ulong LobbyId { get; private set; }

#if !DISABLESTEAMWORKS
        CallResult<LobbyCreated_t> m_Created;
        Callback<GameLobbyJoinRequested_t> m_JoinRequested;
        Callback<LobbyEnter_t> m_Entered;

        void Awake() => Instance = this;

        void Start()
        {
            if (!SteamManager.Initialized) return;
            m_Created = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            m_JoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            m_Entered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);

            // Launched by accepting an invite while the game was closed: "+connect_lobby <id>".
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out var lobby))
                    JoinLobby(lobby);
        }

        public void HostLobby()
        {
            if (!SteamManager.Initialized) return;
            GameNetwork.Instance.SetStatus("Создаём лобби Steam…");
            m_Created.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MaxPlayers));
        }

        public void JoinLobby(ulong lobby)
        {
            GameNetwork.Instance.SetStatus("Входим в лобби…");
            SteamMatchmaking.JoinLobby(new CSteamID(lobby));
        }

        public void OpenInviteDialog()
        {
            if (InLobby) SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(LobbyId));
        }

        public void Leave()
        {
            if (InLobby && SteamManager.Initialized) SteamMatchmaking.LeaveLobby(new CSteamID(LobbyId));
            LobbyId = 0;
        }

        void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
        {
            if (ioFailure || result.m_eResult != EResult.k_EResultOK)
            {
                GameNetwork.Instance.SetStatus($"Не удалось создать лобби: {result.m_eResult}");
                return;
            }
            LobbyId = result.m_ulSteamIDLobby;
            var lobby = new CSteamID(LobbyId);
            SteamMatchmaking.SetLobbyData(lobby, "host", SteamUser.GetSteamID().ToString());
            SteamMatchmaking.SetLobbyData(lobby, "game", "stealth-it");
            Debug.Log($"[SteamLobby] Lobby {LobbyId} created");
            GameNetwork.Instance.StartHostSteam();
        }

        void OnJoinRequested(GameLobbyJoinRequested_t request)
        {
            Debug.Log($"[SteamLobby] Join requested for lobby {request.m_steamIDLobby}");
            if (GameNetwork.Instance.IsRunning) GameNetwork.Instance.Leave();
            JoinLobby(request.m_steamIDLobby.m_SteamID);
        }

        void OnLobbyEntered(LobbyEnter_t entered)
        {
            LobbyId = entered.m_ulSteamIDLobby;
            var lobby = new CSteamID(LobbyId);
            var owner = SteamMatchmaking.GetLobbyOwner(lobby);
            if (owner == SteamUser.GetSteamID()) return; // we are the host
            Debug.Log($"[SteamLobby] Entered lobby {LobbyId}, host {owner}");
            GameNetwork.Instance.StartClientSteam(owner.m_SteamID);
        }
#else
        void Awake() => Instance = this;
        public void HostLobby() { }
        public void JoinLobby(ulong lobby) { }
        public void OpenInviteDialog() { }
        public void Leave() { LobbyId = 0; }
#endif
    }
}
