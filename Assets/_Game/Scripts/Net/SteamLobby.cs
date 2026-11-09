using System.Collections.Generic;
using Stealth.Game;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace Stealth.Net
{
    /// <summary>
    /// Friends-only Steam lobby: the host creates it and starts the Netcode host; friends join by invite, through
    /// "Join game" in the Steam friends list (rich presence "connect") or from the menu's list of friends' sessions
    /// (<see cref="RefreshFriends"/>). Joining the lobby connects to the lobby owner's SteamID.
    /// </summary>
    public class SteamLobby : MonoBehaviour
    {
        public const int MaxPlayers = 4;
        public static SteamLobby Instance { get; private set; }

        public bool InLobby => LobbyId != 0;
        public ulong LobbyId { get; private set; }

        /// <summary>Friends' sessions of this game, filled by <see cref="RefreshFriends"/>.</summary>
        public FriendLobbyList Friends { get; } = new();

        /// <summary>Builds must match to play together; the build GUID is empty in the editor.</summary>
        static string BuildId => Application.buildGUID;

#if !DISABLESTEAMWORKS
        CallResult<LobbyCreated_t> m_Created;
        Callback<GameLobbyJoinRequested_t> m_JoinRequested;
        Callback<LobbyEnter_t> m_Entered;
        Callback<LobbyDataUpdate_t> m_DataUpdated;
        float m_NextPlayerCount;

        void Awake() => Instance = this;

        void Start()
        {
            if (!SteamManager.Initialized) return;
            m_Created = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            m_JoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            m_Entered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            m_DataUpdated = Callback<LobbyDataUpdate_t>.Create(OnLobbyData);

            // Launched by accepting an invite while the game was closed: "+connect_lobby <id>".
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out var lobby))
                    JoinLobby(lobby);
        }

        void Update()
        {
            // The host keeps the player count in the lobby data: friends see "2/4" before joining.
            if (!SteamManager.Initialized || !InLobby || Time.unscaledTime < m_NextPlayerCount) return;
            m_NextPlayerCount = Time.unscaledTime + 2f;
            var lobby = new CSteamID(LobbyId);
            if (SteamMatchmaking.GetLobbyOwner(lobby) == SteamUser.GetSteamID())
                SteamMatchmaking.SetLobbyData(lobby, "players", SteamMatchmaking.GetNumLobbyMembers(lobby).ToString());
        }

        public void HostLobby()
        {
            if (!SteamManager.Initialized) return;
            GameNetwork.Instance.SetStatus("Создаём лобби Steam…");
            m_Created.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MaxPlayers));
        }

        public void JoinLobby(ulong lobby)
        {
            if (!SteamManager.Initialized) return;
            GameNetwork.Instance.SetStatus("Входим в лобби…");
            SteamMatchmaking.JoinLobby(new CSteamID(lobby));
        }

        public void OpenInviteDialog()
        {
            if (InLobby) SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(LobbyId));
        }

        public void Leave()
        {
            if (SteamManager.Initialized)
            {
                if (InLobby) SteamMatchmaking.LeaveLobby(new CSteamID(LobbyId));
                SteamFriends.ClearRichPresence();
            }
            LobbyId = 0;
        }

        /// <summary>Which friends are in a lobby of this app right now? Their lobby data arrives in <see cref="OnLobbyData"/>.</summary>
        public void RefreshFriends()
        {
            if (!SteamManager.Initialized) return;
            var app = SteamUtils.GetAppID();
            var seen = new List<(ulong, ulong, string)>();
            int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            for (int i = 0; i < count; i++)
            {
                var friend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                if (!SteamFriends.GetFriendGamePlayed(friend, out var game)) continue;
                if (game.m_gameID.AppID() != app || !game.m_steamIDLobby.IsValid()) continue;
                if (game.m_steamIDLobby.m_SteamID == LobbyId) continue; // our own session
                seen.Add((game.m_steamIDLobby.m_SteamID, friend.m_SteamID, SteamFriends.GetFriendPersonaName(friend)));
            }
            Friends.BeginRefresh(seen);
            foreach (var (lobby, _, _) in seen) SteamMatchmaking.RequestLobbyData(new CSteamID(lobby));
        }

        void OnLobbyData(LobbyDataUpdate_t update)
        {
            if (update.m_ulSteamIDLobby != update.m_ulSteamIDMember) return; // a member's data, not the lobby's
            var lobby = new CSteamID(update.m_ulSteamIDLobby);
            int.TryParse(SteamMatchmaking.GetLobbyData(lobby, "players"), out int players);
            Friends.OnLobbyData(update.m_ulSteamIDLobby,
                SteamMatchmaking.GetLobbyData(lobby, "game"),
                SteamMatchmaking.GetLobbyData(lobby, "build"), BuildId,
                players, SteamMatchmaking.GetLobbyMemberLimit(lobby),
                SteamMatchmaking.GetLobbyData(lobby, "name"));
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
            SteamMatchmaking.SetLobbyData(lobby, "game", FriendLobbyList.GameTag);
            SteamMatchmaking.SetLobbyData(lobby, "build", BuildId);
            SteamMatchmaking.SetLobbyData(lobby, "name", SteamFriends.GetPersonaName());
            SteamMatchmaking.SetLobbyData(lobby, "players", "1");
            AdvertiseLobby();
            Debug.Log($"[SteamLobby] Lobby {LobbyId} created");
            GameNetwork.Instance.StartHostSteam();
        }

        /// <summary>Rich presence: friends get "Join game" in the Steam friends list and see what we are doing.</summary>
        void AdvertiseLobby()
        {
            SteamFriends.SetRichPresence("connect", $"+connect_lobby {LobbyId}");
            SteamFriends.SetRichPresence("status", "Вылазка в офис");
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
            if ((EChatRoomEnterResponse)entered.m_EChatRoomEnterResponse != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                GameNetwork.Instance.SetStatus($"Не удалось войти в лобби: {(EChatRoomEnterResponse)entered.m_EChatRoomEnterResponse}");
                LobbyId = 0;
                return;
            }
            string build = SteamMatchmaking.GetLobbyData(lobby, "build");
            if (!string.IsNullOrEmpty(build) && !string.IsNullOrEmpty(BuildId) && build != BuildId)
            {
                GameNetwork.Instance.SetStatus("У друга другая версия игры — обновитесь до одного билда");
                Leave();
                return;
            }
            Debug.Log($"[SteamLobby] Entered lobby {LobbyId}, host {owner}");
            AdvertiseLobby();
            GameNetwork.Instance.StartClientSteam(owner.m_SteamID);
        }
#else
        void Awake() => Instance = this;
        public void HostLobby() { }
        public void JoinLobby(ulong lobby) { }
        public void OpenInviteDialog() { }
        public void Leave() { LobbyId = 0; }
        public void RefreshFriends() { }
#endif
    }
}
