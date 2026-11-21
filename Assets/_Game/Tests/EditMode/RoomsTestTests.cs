using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The rooms-test level: a house of one floor with every room type at least once, its size unlimited; workers only.</summary>
    public class RoomsTestTests
    {
        const string LevelPath = "Assets/_Game/Data/Config/RoomsTest/Level.asset";
        const string RootPath = "Assets/_Game/Data/Config/RoomsTest/GameConfig.asset";

        [Test]
        public void RoomCountLimitsAreChecked()
        {
            var b = new BuildingSettings { RoomCountMin = 3, RoomCountMax = 4 };
            Assert.IsFalse(b.RoomCountOk(2));
            Assert.IsTrue(b.RoomCountOk(3));
            Assert.IsTrue(b.RoomCountOk(4));
            Assert.IsFalse(b.RoomCountOk(5));
            Assert.IsTrue(new BuildingSettings().RoomCountOk(40), "no limits by default: the game's houses are as before");
        }

        [Test]
        public void TheMainGameIsTheDefaultTheTestRoomsAreASwitch()
        {
            var scenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray();
            CollectionAssert.AreEqual(new[] { "Assets/_Game/Scenes/Game.unity" }, scenes, "one scene: the main game");
            var root = AssetDatabase.LoadAssetAtPath<GameConfig>(RootPath);
            var main = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            Assert.IsNotNull(root);
            Assert.AreEqual(root, main.TestRooms, "the switch plays this root");
            Assert.IsFalse(Tuning.TestRooms, "the main game by default");
            try
            {
                Tuning.SetTestRooms(true);
                Assert.AreEqual(root.Level, Tuning.Level, "the switch on: the test rooms' level");
                Tuning.SetTestRooms(false);
                Assert.AreEqual(main.Level, Tuning.Level, "off again: the main game");
            }
            finally { Tuning.SetTestRooms(false); }
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelPath), root.Level, "the test scene plays its own level");
            Assert.AreNotEqual(main.Level, root.Level);
            Assert.AreEqual(main.Player, root.Player, "the rest of the tuning is the game's own");
            Assert.AreNotEqual(main.Styles, root.Styles, "the test rooms have their own style list…");
            CollectionAssert.AreEqual(new[] { "S16_Derelict" }, root.Styles.Styles.Select(s => s.name).ToArray(), "…with the abandoned facility only");
            Assert.AreEqual("S16_Derelict", root.Styles.Default.name);
            Assert.Greater(main.Styles.Styles.Length, 1, "the main game has all the styles (F8)");
        }

        [Category("Slow")] // generates many houses (Tools/dev.sh: SLOW=1 runs it)
        [Test]
        public void HousesHaveEveryRoomTypeOnOneFloorAndOnlyWorkers()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelPath);
            Assert.IsNotNull(level, "no rooms-test level config");
            Assert.IsTrue(level.Building.EveryRoomType);
            Assert.AreEqual(0, level.Building.RoomCountMin + level.Building.RoomCountMax, "no room count limits: the house is as big as its rooms");
            Assert.AreEqual(0f, level.Building.RoomAreaMin + level.Building.RoomAreaMax, "no size limits but each type's own");
            for (int seed = 1; seed <= 6; seed++)
                for (int house = 0; house < 3; house++)
                    for (int players = 1; players <= 4; players += 3)
                    {
                        var l = LevelGenerator.Generate(seed * 7919 + house, house, level.Building, level.Security, players, level.Features);
                        string at = $"seed {seed} house {house} players {players}";
                        foreach (var t in RoomRules.Programmed)
                            Assert.IsTrue(l.Rooms.Any(r => r.Type == t), $"{at}: a {t}");
                        Assert.AreEqual(1, l.Floors);
                        Assert.That(l.StorageRoom, Is.GreaterThanOrEqualTo(0), $"{at}: the storage room, locked");
                        Assert.IsNotEmpty(l.RobotSpawns, $"{at}: its workers");
                        foreach (var r in l.RobotSpawns) Assert.AreEqual(Stealth.Level.RobotKind.Worker, r.Kind, $"{at}: workers only");
                        Assert.IsEmpty(l.CameraSpots);
                        Assert.IsEmpty(l.LaserSpots);
                        foreach (var t in l.Terminals) Assert.That(t.kind, Is.EqualTo(TerminalKind.Doors).Or.EqualTo(TerminalKind.Map), "only the storage room's door terminal and the map terminals");
                    }
        }

        [Test]
        public void EveryRoomIsAtLeastItsTypesSmallestAndCorridorsJoinSome()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelPath);
            int withCorridor = 0, houses = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                var l = LevelGenerator.Generate(seed * 104729, 0, level.Building, level.Security, 1, level.Features);
                houses++;
                if (BuildingSettings.CountJoiningCorridors(l) > 0) withCorridor++;
                foreach (var r in l.Rooms)
                {
                    if (r.Type is RoomType.Corridor or RoomType.Hall or RoomType.Lobby) continue;
                    Assert.That(r.Area, Is.GreaterThanOrEqualTo(RoomRules.MinArea(r.Type, level.Building.RoomTypeRules) - 0.01f), $"seed {seed}: {r.Type} {r.Area:0} m²");
                }
            }
            Assert.AreEqual(houses, withCorridor, $"every house has a corridor between its rooms ({withCorridor} of {houses})");
            Assert.IsTrue(new BuildingSettings { RoomAreaMin = 30f }.HasRoomLimits);
        }

        [Test]
        public void NoHunterAndNoCountdownInTheTestLevel()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameConfig>(RootPath);
            var main = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            Assert.AreNotEqual(main.Run, root.Run, "the test level has its own run rules");
            Assert.IsFalse(root.Run.Rules.Hunter);
            for (int house = 0; house < 4; house++)
            {
                Assert.AreEqual((-1f, -1), Stealth.Game.RunState.HunterForecast(0, 9999f, house, 0, root.Run.Rules), "no countdown");
                Assert.IsFalse(Stealth.Game.RunState.HunterDue(10, 9999f, house, 0, root.Run.Rules), "the hunter never comes");
            }
            Assert.IsTrue(main.Run.Rules.Hunter, "the game keeps its hunter");
            Assert.IsTrue(Stealth.Game.RunState.HunterDue(0, 9999f, 0, 0, main.Run.Rules));
        }
    }
}
