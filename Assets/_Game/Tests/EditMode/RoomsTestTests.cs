using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The test rooms (<see cref="HouseKind.TestRooms"/>): a kind of house with its own level — one floor with every room type
    /// at least once, its size unlimited, workers only — started from the cheat panel, its kind carried by its seed.
    /// </summary>
    public class RoomsTestTests
    {
        const string LevelPath = "Assets/_Game/Data/Config/RoomsTest/Level.asset";

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
        public void TheTestRoomsAreAKindOfHouseCarriedByItsSeed()
        {
            // the sign bit marks a test rooms house: every seed a run makes is non-negative, so no route ever rolls one
            for (int run = 1; run < 40; run++)
            {
                var map = new Stealth.Game.RunMap(run * 7919);
                for (int depth = 0; depth < 6; depth++)
                {
                    Assert.That(LevelRandom.LevelSeed(run, depth), Is.GreaterThan(0));
                    for (int lane = 0; lane < map.LanesAt(depth); lane++)
                        Assert.AreNotEqual(HouseKind.TestRooms, map.Node(depth, lane).Kind, $"run {run}: the route never has the test rooms");
                }
            }
            foreach (int seed in new[] { 1, 42, 7919, int.MaxValue })
            {
                int test = HouseKinds.TestRoomsSeed(seed);
                Assert.AreNotEqual(0, test);
                Assert.IsTrue(HouseKinds.IsTestRooms(test));
                for (int house = 0; house < 6; house++) Assert.AreEqual(HouseKind.TestRooms, HouseKinds.Of(test, house), "at any depth");
                Assert.AreNotEqual(HouseKind.TestRooms, HouseKinds.Of(seed, 2));
            }
            Assert.IsFalse(HouseKinds.OwnPlan(HouseKind.TestRooms), "laid out by the office generator");
            Assert.AreSame(HouseRules.Office, HouseRules.For(HouseKind.TestRooms));
            var b = new BuildingSettings();
            Assert.AreSame(b, HouseKinds.Apply(HouseKind.TestRooms, b), "the test rooms' own settings as they are");
        }

        [Test]
        public void TheTestRoomsAreBuiltByTheirOwnLevelTheRestIsTheGames()
        {
            var scenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray();
            CollectionAssert.AreEqual(new[] { "Assets/_Game/Scenes/Game.unity" }, scenes, "one scene: the main game");
            var main = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelPath);
            Assert.IsNotNull(level);
            Assert.AreEqual(level, main.TestRoomsLevel);
            Assert.AreNotEqual(main.Level, level);
            Assert.AreEqual(level, Tuning.LevelOf(HouseKind.TestRooms), "a test rooms house: its own level");
            foreach (HouseKind k in System.Enum.GetValues(typeof(HouseKind)))
                if (k != HouseKind.TestRooms) Assert.AreEqual(main.Level, Tuning.LevelOf(k), $"{k}: the game's level");
            foreach (var gone in new[] { "GameConfig", "Run", "Audio" })
                Assert.IsNull(AssetDatabase.LoadMainAssetAtPath($"Assets/_Game/Data/Config/RoomsTest/{gone}.asset"), $"no {gone} of their own: the game's");
            Assert.IsNull(AssetDatabase.LoadMainAssetAtPath("Assets/_Game/Data/Styles/StylesTestRooms.asset"), "every style, like any house");
            var menu = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/Menu.prefab");
            Assert.IsNotNull(menu);
            Assert.IsFalse(menu.GetComponentsInChildren<Transform>(true).Any(t => t.name == "TestRooms"), "no switch in the main menu: the cheat panel starts them");
        }

        [Test]
        public void HousesHaveEveryRoomTypeOnOneFloorAndOnlyWorkers()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelPath);
            Assert.IsNotNull(level, "no rooms-test level config");
            Assert.IsTrue(level.Building.EveryRoomType);
            Assert.AreEqual(0, level.Building.RoomCountMin + level.Building.RoomCountMax, "no room count limits: the house is as big as its rooms");
            Assert.AreEqual(0f, level.Building.RoomAreaMin + level.Building.RoomAreaMax, "no size limits but each type's own");
            TestHouses.Many(from seed in Enumerable.Range(1, 4) from house in Enumerable.Range(0, 3) from players in new[] { 1, 4 }
                select TestHouses.RoomsTestSpec(seed - 1, house, players)); // (made at once)
            for (int seed = 1; seed <= 4; seed++)
                for (int house = 0; house < 3; house++)
                    for (int players = 1; players <= 4; players += 3)
                    {
                        var l = TestHouses.RoomsTest(seed - 1, house, players);
                        string at = $"seed {seed} house {house} players {players}";
                        Assert.AreEqual(HouseKind.TestRooms, l.Kind, at);
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
        public void APlanMissingAProgrammedTypeIsRedrawn()
        {
            var every = new BuildingSettings { EveryRoomType = true };
            Assert.IsTrue(every.TypesOk(TestHouses.RoomsTest(0, 2)), "the test rooms house that once lost its server room");
            var office = TestHouses.OfKind(HouseKind.Office, 0);
            Assert.IsFalse(RoomRules.Programmed.All(t => office.Plan.Rooms.Any(r => r.Type == t)), "(a small office has not every type)");
            Assert.IsFalse(every.TypesOk(office), "a plan without every type: redrawn");
            Assert.IsTrue(new BuildingSettings().TypesOk(office), "the game's houses: any types");
        }

        [Test]
        public void EveryRoomIsAtLeastItsTypesSmallestAndCorridorsJoinSome()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelPath);
            int withCorridor = 0, houses = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                var l = TestHouses.RoomsTest(seed - 1);
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
        public void NoHunterAndNoCountdown()
        {
            // the test rooms play by the game's run rules: its hunter by time and alarms is off (the reinforcements took its place)
            var main = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            Assert.IsFalse(main.Run.Rules.Hunter, "no hunter in the game");
            for (int house = 0; house < 4; house++)
            {
                Assert.AreEqual((-1f, -1), Stealth.Game.RunState.HunterForecast(0, 9999f, house, 0, main.Run.Rules), "no countdown");
                Assert.IsFalse(Stealth.Game.RunState.HunterDue(10, 9999f, house, 0, main.Run.Rules), "the hunter never comes");
            }
            // the rule still works when on
            Assert.IsTrue(Stealth.Game.RunState.HunterDue(0, 9999f, 0, 0, new Stealth.Game.RunSettings()), "on, it would come");
        }
    }
}
