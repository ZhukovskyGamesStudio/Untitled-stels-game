using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The rooms-test scene: a new game starts in it, with a small house of three or four rooms.</summary>
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
            Assert.AreEqual(main.Styles, root.Styles);
        }

        [Test]
        public void HousesHaveThreeOrFourRoomsOnOneFloor()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelPath);
            Assert.IsNotNull(level, "no rooms-test level config");
            for (int seed = 1; seed <= 12; seed++)
                for (int house = 0; house < 3; house++)
                    for (int players = 1; players <= 4; players += 3)
                    {
                        var l = LevelGenerator.Generate(seed * 7919 + house, house, level.Building, level.Security, players);
                        int rooms = BuildingSettings.CountRooms(l);
                        Assert.That(rooms, Is.InRange(level.Building.RoomCountMin, level.Building.RoomCountMax),
                            $"seed {seed} house {house} players {players}: {rooms} rooms");
                        Assert.AreEqual(1, l.Floors);
                        Assert.IsEmpty(l.RobotSpawns, "no guards in the test level");
                        Assert.IsEmpty(l.CameraSpots);
                        Assert.IsEmpty(l.LaserSpots);
                        Assert.IsEmpty(l.Terminals);
                    }
        }

        [Test]
        public void EveryRoomIsThirtyToTwoHundredSquareMetresAndCorridorsJoinSome()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/_Game/Data/Config/RoomsTest/Level.asset");
            Assert.AreEqual(30f, level.Building.RoomAreaMin);
            Assert.AreEqual(200f, level.Building.RoomAreaMax);
            int withCorridor = 0, houses = 0;
            for (int seed = 1; seed <= 16; seed++)
            {
                var l = LevelGenerator.Generate(seed * 104729, 0, level.Building, level.Security, 1);
                houses++;
                if (BuildingSettings.CountJoiningCorridors(l) > 0) withCorridor++;
                foreach (var r in l.Rooms)
                {
                    if (r.Type == RoomType.Corridor) continue;
                    if (r.Type == RoomType.Hall) continue;
                    Assert.That(r.Area, Is.InRange(30f, 200f), $"seed {seed}: {r.Type} {r.Area:0} m²");
                }
                Assert.That(BuildingSettings.CountRooms(l), Is.InRange(3, 4), $"seed {seed}");
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
