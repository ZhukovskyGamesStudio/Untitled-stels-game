using System.Collections.Generic;

namespace Stealth.Game
{
    /// <summary>A friend hosting (or playing in) a session of this game that we can join.</summary>
    public struct FriendLobby
    {
        public ulong LobbyId;
        public ulong FriendId;
        public string FriendName;
        public int Players;
        public int MaxPlayers;
        /// <summary>Same build as ours: different builds cannot play together.</summary>
        public bool Compatible;
        public bool IsFull => MaxPlayers > 0 && Players >= MaxPlayers;
        public bool CanJoin => Compatible && !IsFull;
    }

    /// <summary>
    /// The list of friends' lobbies shown in the menu (pure, unit tested). The network layer feeds it:
    /// <see cref="BeginRefresh"/> with the friends currently in a lobby of this app, then <see cref="OnLobbyData"/>
    /// when Steam delivers each lobby's data. Lobbies of other games on the same AppID (480 is shared) are filtered
    /// out by the game tag; friends who left disappear on the next refresh.
    /// </summary>
    public sealed class FriendLobbyList
    {
        public const string GameTag = "stealth-it";

        readonly List<FriendLobby> m_Lobbies = new();
        readonly Dictionary<ulong, (ulong friend, string name)> m_Pending = new();

        public IReadOnlyList<FriendLobby> Lobbies => m_Lobbies;
        public int Pending => m_Pending.Count;

        /// <summary>Friends seen in a lobby right now (lobby id, friend id, friend name).</summary>
        public void BeginRefresh(IEnumerable<(ulong lobby, ulong friend, string name)> seen)
        {
            var present = new HashSet<ulong>();
            m_Pending.Clear();
            foreach (var (lobby, friend, name) in seen)
            {
                if (lobby == 0 || !present.Add(lobby)) continue;
                m_Pending[lobby] = (friend, name);
            }
            m_Lobbies.RemoveAll(l => !present.Contains(l.LobbyId));
        }

        /// <summary>
        /// Lobby data arrived. Returns true if the lobby belongs to this game and is listed. The host's name (lobby data)
        /// wins over the friend's: a friend who joined someone else's session shows that session's host.
        /// </summary>
        public bool OnLobbyData(ulong lobby, string gameTag, string build, string myBuild, int players, int maxPlayers, string hostName = null)
        {
            if (!m_Pending.TryGetValue(lobby, out var who)) return false;
            m_Pending.Remove(lobby);
            m_Lobbies.RemoveAll(l => l.LobbyId == lobby);
            if (gameTag != GameTag) return false;
            m_Lobbies.Add(new FriendLobby
            {
                LobbyId = lobby,
                FriendId = who.friend,
                FriendName = string.IsNullOrEmpty(hostName) ? who.name : hostName,
                Players = players,
                MaxPlayers = maxPlayers,
                Compatible = string.IsNullOrEmpty(build) || string.IsNullOrEmpty(myBuild) || build == myBuild,
            });
            m_Lobbies.Sort((a, b) => b.CanJoin.CompareTo(a.CanJoin) != 0 ? b.CanJoin.CompareTo(a.CanJoin) : string.CompareOrdinal(a.FriendName, b.FriendName));
            return true;
        }

        public void Clear()
        {
            m_Lobbies.Clear();
            m_Pending.Clear();
        }
    }
}
