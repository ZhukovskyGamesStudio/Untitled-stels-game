using System;
using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using Stealth.UI;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// Texts whose keys are built from enums and data (robot kinds, house kinds, actions, colours, styles): every one is in
    /// the tables — the literal-key check of <see cref="LocalizationTests"/> cannot see them.
    /// </summary>
    public class LocalizedNamesTests
    {
        [OneTimeSetUp]
        public void Load() => Loc.Reload();

        static void AssertKey(string key) => Assert.IsTrue(Loc.Has(key), $"no '{key}' in Resources/Strings");

        [Test]
        public void EveryRobotKindHasANameAndSenses()
        {
            AssertKey("robot.unknown.name");
            foreach (RobotKind k in Enum.GetValues(typeof(RobotKind)))
            {
                AssertKey(RobotInfo.Key(k) + ".name");
                AssertKey(RobotInfo.Key(k) + ".senses");
            }
        }

        [Test]
        public void EveryHouseKindHasAName()
        {
            foreach (HouseKind k in Enum.GetValues(typeof(HouseKind)))
                AssertKey(HouseKinds.NameKey(k));
        }

        [Test]
        public void CompanyNameListsMatchInEveryLanguage()
        {
            foreach (var key in new[] { "house.forms", "house.name_starts", "house.name_ends" })
            {
                int ru = Loc.T(key, Language.Ru).Split(';').Length, en = Loc.T(key, Language.En).Split(';').Length;
                Assert.AreEqual(ru, en, $"{key}: the same company for every player whatever the language");
            }
        }

        [Test]
        public void EveryActionAndGroupHasAName()
        {
            foreach (GameAction a in Enum.GetValues(typeof(GameAction)))
                AssertKey(Controls.TitleKey(a));
            foreach (var info in Controls.All)
                AssertKey(Controls.GroupKey(info.Group));
            AssertKey(Controls.GroupKey("fixed"));
        }

        [Test]
        public void EveryLootColourDepartmentAndLightLevelHasAName()
        {
            var colors = Resources.Load<GameConfig>(GameConfig.ResourcePath)?.Items?.Colors ?? ItemCatalog.DefaultColors();
            for (int i = 0; i < colors.Length; i++) AssertKey(ItemCatalog.ColorKey(i));
            for (int i = 0; i < Enum.GetValues(typeof(Department)).Length; i++) AssertKey("department." + i);
            for (int i = 0; i < 4; i++) AssertKey("light." + i);
            Assert.AreEqual(4, UiMath.LightNames.Distinct().Count());
        }

        [Test]
        public void EveryVisualStyleHasAName()
        {
            var root = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            Assert.IsNotNull(root?.Styles);
            foreach (var style in root.Styles.Styles.Where(s => s != null))
                AssertKey(VisualStyles.TitleKey(style));
        }
    }
}
