using System.Linq;
using NUnit.Framework;
using Stealth.Game;
using Stealth.Items;
using Stealth.Player;
using Stealth.UI;
using Stealth.Vehicles;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The F4 cheat panel: the cheats stay on for a new player object (a new session, a reconnect) without switching them off
    /// and on; its tabs, what a cheat travels with, an item in front of you, any upgrade level at once.
    /// </summary>
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

        [Test]
        public void ACheatsArgumentCarriesAnItemOrAnUpgradeLevel()
        {
            for (int shape = 0; shape < ItemCatalog.Shapes.Length; shape++)
                for (int color = 0; color < 8; color++)
                    Assert.AreEqual(((byte)shape, (byte)color), CheatArgs.ItemOf(CheatArgs.Item(shape, color)));
            foreach (CarUpgrade u in System.Enum.GetValues(typeof(CarUpgrade)))
                for (int level = 0; level <= CarUpgrades.MaxLevelled; level++)
                    Assert.AreEqual((u, level), CheatArgs.UpgradeOf(CheatArgs.Upgrade(u, level)));
            Assert.IsTrue(CheatArgs.ValidItem(ItemCatalog.Shovel, 4));
            Assert.IsFalse(CheatArgs.ValidItem(ItemCatalog.Shapes.Length, 0), "no such item");
            Assert.IsFalse(CheatArgs.ValidItem(ItemCatalog.Shovel, ItemCatalog.Colors.Length), "no such colour");
            Assert.IsTrue(CheatArgs.ValidItem(ItemCatalog.Blueprint, ItemCatalog.Colors.Length + 2), "a blueprint's byte is its recipe");
        }

        [Test]
        public void AnItemAppearsInFrontOfThePlayerAndNotInTheWall()
        {
            var feet = new Vector3(3f, 0f, -2f);
            var p = CheatArgs.InFront(feet, 90f);
            Assert.AreEqual(feet.x + CheatArgs.Ahead, p.x, 1e-4f, "facing +X: in front");
            Assert.AreEqual(feet.z, p.z, 1e-4f);
            Assert.AreEqual(CheatArgs.Height, p.y, 1e-4f, "chest high: it falls to the floor");
            var near = CheatArgs.InFront(feet, 0f, 0.8f);
            Assert.AreEqual(feet.z + 0.8f - CheatArgs.WallGap, near.z, 1e-4f, "a wall nearer: just before it");
            Assert.AreEqual(feet.z + CheatArgs.Ahead, CheatArgs.InFront(feet, 0f, 5f).z, 1e-4f, "a wall far away changes nothing");
            Assert.Greater(CheatArgs.InFront(feet, 0f, 0f).z, feet.z, "right at a wall: still in front, not behind");
        }

        [Test]
        public void AnyUpgradeLevelAtOnceUpOrDownWithinItsTop()
        {
            var u = new CarUpgrades();
            int v = u.Version;
            Assert.IsTrue(u.Set(CarUpgrade.Kennel, 3));
            Assert.AreEqual(3, u.Level(CarUpgrade.Kennel), "straight to the top, not a level at a time");
            Assert.Greater(u.Version, v);
            Assert.IsTrue(u.Set(CarUpgrade.Kennel, 1), "down again");
            Assert.AreEqual(1, u.Level(CarUpgrade.Kennel));
            Assert.IsFalse(u.Set(CarUpgrade.Kennel, 1), "the same level: nothing changes");
            u.Set(CarUpgrade.Teleport, 3);
            Assert.AreEqual(1, u.Level(CarUpgrade.Teleport), "a unique one: 1 at most");
            Assert.IsFalse(u.Set(CarUpgrade.Unique6, 2), "not designed yet: stays 0");
            u.Set(CarUpgrade.Engine, -4);
            Assert.AreEqual(0, u.Level(CarUpgrade.Engine));
            var peer = new CarUpgrades();
            peer.Apply(u.Pack());
            Assert.AreEqual(1, peer.Level(CarUpgrade.Kennel), "every peer gets it");
        }

        [Test]
        public void ThePanelHasFiveTabsAndEveryItemUpgradeAndStyle()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/HUD.prefab");
            var panel = hud != null ? hud.GetComponentInChildren<CheatPanel>(true) : null;
            Assert.IsNotNull(panel, "the cheat panel is in the HUD");
            int tabs = System.Enum.GetValues(typeof(CheatPanel.Tab)).Length;
            Assert.AreEqual(5, tabs);
            Assert.AreEqual(tabs, panel.Tabs.Length);
            Assert.AreEqual(tabs, panel.Pages.Length);
            Assert.IsTrue(panel.Tabs.All(t => t != null) && panel.Pages.All(p => p != null));
            Assert.IsTrue(panel.Pages[0].activeSelf && panel.Pages.Skip(1).All(p => !p.activeSelf), "the cheats first");
            Assert.AreEqual(ItemCatalog.Shapes.Length, panel.Items.Length, "a place for every item");
            for (int i = 0; i < panel.Items.Length; i++)
            {
                if (ItemCatalog.IsPiece(i)) { Assert.IsNull(panel.Items[i], "no old tetris pieces"); continue; }
                Assert.IsNotNull(panel.Items[i], ItemCatalog.Shapes[i].Id);
                Assert.IsTrue(panel.Items[i].transform.IsChildOf(panel.Pages[(int)CheatPanel.Tab.Items].transform));
                var icon = panel.Items[i].transform.Find("Icon")?.GetComponent<UnityEngine.UI.Image>();
                Assert.IsTrue(icon != null && icon.sprite != null, $"{ItemCatalog.Shapes[i].Id}: its icon left of its name");
            }
            Assert.IsNotNull(panel.OnlyInteractive, "the check box 'only the interactive'");
            Assert.IsTrue(CheatPanel.Shown(ItemCatalog.Screwdriver, true), "a tool stays");
            Assert.IsFalse(CheatPanel.Shown(ItemCatalog.FirstJunk, true), "junk is only fuel");
            Assert.IsTrue(CheatPanel.Shown(ItemCatalog.FirstJunk, false));
            Assert.IsFalse(CheatPanel.Shown(0, false), "never a tetris piece");
            int upgrades = System.Enum.GetValues(typeof(CarUpgrade)).Length;
            Assert.AreEqual(upgrades, panel.UpgradeRows.Length);
            Assert.AreEqual(upgrades * (CarUpgrades.MaxLevelled + 1), panel.UpgradeLevels.Length, "a button for every level of every upgrade");
            Assert.IsTrue(panel.UpgradeLevels.All(b => b != null && b.transform.IsChildOf(panel.Pages[(int)CheatPanel.Tab.Car].transform)));
            Assert.IsTrue(panel.Houses.All(b => b.transform.IsChildOf(panel.Pages[(int)CheatPanel.Tab.Houses].transform)));
            var styles = panel.Pages[(int)CheatPanel.Tab.Visual].GetComponent<StylePanel>();
            Assert.IsNotNull(styles, "the style picker is the last page (no F8 panel of its own)");
            Assert.IsNotNull(styles.List);
            Assert.IsNotNull(styles.Template);
            Assert.AreEqual(1, hud.GetComponentsInChildren<StylePanel>(true).Length);
        }
    }
}
