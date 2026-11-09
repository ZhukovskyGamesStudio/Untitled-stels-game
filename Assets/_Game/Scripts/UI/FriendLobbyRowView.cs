using System;
using Stealth.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>A row of the main menu's "friends in game" list: who, how many players, and a join button.</summary>
    public class FriendLobbyRowView : MonoBehaviour
    {
        public Text Label;
        public Button Join;

        ulong m_Lobby;
        Action<ulong> m_OnJoin;

        void Awake() => Join.onClick.AddListener(() => m_OnJoin?.Invoke(m_Lobby));

        public void Set(FriendLobby lobby, Action<ulong> onJoin)
        {
            m_Lobby = lobby.LobbyId;
            m_OnJoin = onJoin;
            Label.text = Describe(lobby);
            Ui.Show(Join, lobby.CanJoin); // the label says why not
        }

        /// <summary>"Саша · 2/4", with the reason when joining is impossible (pure).</summary>
        public static string Describe(FriendLobby l)
        {
            string count = l.MaxPlayers > 0 ? $" · {l.Players}/{l.MaxPlayers}" : "";
            string why = !l.Compatible ? " · другая версия игры" : l.IsFull ? " · мест нет" : "";
            return $"{l.FriendName}{count}{why}";
        }
    }
}
