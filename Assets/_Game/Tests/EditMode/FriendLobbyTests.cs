using NUnit.Framework;
using Stealth.Game;
using Stealth.UI;

namespace Stealth.Tests
{
    /// <summary>The menu's list of friends' sessions: what is shown, what can be joined.</summary>
    public class FriendLobbyTests
    {
        const string Build = "build-a";

        static FriendLobbyList WithFriends(params (ulong lobby, ulong friend, string name)[] seen)
        {
            var list = new FriendLobbyList();
            list.BeginRefresh(seen);
            return list;
        }

        [Test]
        public void OnlyLobbiesOfThisGameAreListed()
        {
            var list = WithFriends((10, 1, "Саша"), (20, 2, "Коля"));
            Assert.AreEqual(2, list.Pending);
            Assert.IsTrue(list.OnLobbyData(10, FriendLobbyList.GameTag, Build, Build, 2, 4));
            Assert.IsFalse(list.OnLobbyData(20, "spacewar", "", Build, 1, 8), "AppID 480 is shared with other games");
            Assert.AreEqual(1, list.Lobbies.Count);
            Assert.AreEqual("Саша", list.Lobbies[0].FriendName);
            Assert.IsTrue(list.Lobbies[0].CanJoin);
        }

        [Test]
        public void DataForLobbiesWeDidNotAskAboutIsIgnored()
        {
            var list = WithFriends((10, 1, "Саша"));
            Assert.IsFalse(list.OnLobbyData(99, FriendLobbyList.GameTag, Build, Build, 1, 4));
            Assert.AreEqual(0, list.Lobbies.Count);
        }

        [Test]
        public void TwoFriendsInOneSessionGiveOneRowNamedAfterTheHost()
        {
            var list = WithFriends((10, 1, "Саша"), (10, 2, "Коля"));
            list.OnLobbyData(10, FriendLobbyList.GameTag, Build, Build, 2, 4, "Дима");
            Assert.AreEqual(1, list.Lobbies.Count);
            Assert.AreEqual("Дима", list.Lobbies[0].FriendName);
        }

        [Test]
        public void FriendsWhoLeftDisappearOnTheNextRefresh()
        {
            var list = WithFriends((10, 1, "Саша"), (20, 2, "Коля"));
            list.OnLobbyData(10, FriendLobbyList.GameTag, Build, Build, 1, 4);
            list.OnLobbyData(20, FriendLobbyList.GameTag, Build, Build, 1, 4);
            list.BeginRefresh(new[] { (20UL, 2UL, "Коля") });
            Assert.AreEqual(1, list.Lobbies.Count);
            Assert.AreEqual(20UL, list.Lobbies[0].LobbyId);
        }

        [Test]
        public void OtherBuildsAndFullLobbiesCannotBeJoinedAndGoLast()
        {
            var list = WithFriends((10, 1, "Аня"), (20, 2, "Боря"), (30, 3, "Вова"));
            list.OnLobbyData(10, FriendLobbyList.GameTag, "build-b", Build, 1, 4);
            list.OnLobbyData(20, FriendLobbyList.GameTag, Build, Build, 4, 4);
            list.OnLobbyData(30, FriendLobbyList.GameTag, Build, Build, 1, 4);
            Assert.AreEqual("Вова", list.Lobbies[0].FriendName, "joinable first");
            Assert.IsFalse(list.Lobbies[1].CanJoin);
            Assert.IsFalse(list.Lobbies[2].CanJoin);
            StringAssert.Contains("другая версия", FriendLobbyRowView.Describe(list.Lobbies[0].FriendName == "Аня" ? list.Lobbies[0] : list.Lobbies[1]));
        }

        [Test]
        public void EditorBuildsJoinAnything()
        {
            var list = WithFriends((10, 1, "Аня"));
            list.OnLobbyData(10, FriendLobbyList.GameTag, "build-b", "", 1, 4);
            Assert.IsTrue(list.Lobbies[0].Compatible, "the editor has no build id");
        }

        [Test]
        public void RowTextShowsWhoAndHowMany()
        {
            var lobby = new FriendLobby { FriendName = "Саша", Players = 2, MaxPlayers = 4, Compatible = true };
            Assert.AreEqual("Саша · 2/4", FriendLobbyRowView.Describe(lobby));
            lobby.Players = 4;
            Assert.AreEqual("Саша · 4/4 · мест нет", FriendLobbyRowView.Describe(lobby));
        }
    }
}
