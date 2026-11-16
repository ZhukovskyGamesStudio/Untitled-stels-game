using NUnit.Framework;
using Stealth.Player;
using Stealth.UI;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The F4 cheats stay on for a new player object (a new session, a reconnect) without switching them off and on.</summary>
    public class CheatPanelTests
    {
        GameObject m_Player;

        [TearDown]
        public void TearDown()
        {
            if (m_Player != null) Object.DestroyImmediate(m_Player);
        }

        PlayerController Fresh()
        {
            if (m_Player != null) Object.DestroyImmediate(m_Player);
            m_Player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/Player.prefab"));
            return m_Player.GetComponent<PlayerController>();
        }

        [Test]
        public void TogglesThatAreOnReachANewPlayer()
        {
            var first = Fresh();
            CheatPanel.Sync(first, true, true);
            Assert.IsTrue(first.Invulnerable && first.Invisible, "switched on");
            // the next session's player comes without them; the panel still shows them on
            var next = Fresh();
            Assert.IsFalse(next.Invulnerable || next.Invisible, "a fresh player starts clean");
            CheatPanel.Sync(next, true, true);
            Assert.IsTrue(next.Invulnerable && next.Invisible, "the panel's toggles carried over");
            CheatPanel.Sync(next, false, true);
            Assert.IsFalse(next.Invulnerable, "and off again");
            Assert.IsTrue(next.Invisible);
        }
    }
}
