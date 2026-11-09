using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The tuning assets exist and are wired, and every rule actually follows its settings.</summary>
    public class TuningTests
    {
        [Test]
        public void GameConfigIsFoundAtRuntimeAndComplete()
        {
            var root = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            Assert.IsNotNull(root, "Resources/GameConfig.asset");
            Assert.IsNotNull(root.Player);
            Assert.IsNotNull(root.Run);
            Assert.IsNotNull(root.Level);
            Assert.IsNotNull(root.Items);
            Assert.AreSame(root.Player, Tuning.Player);
            Assert.Greater(root.Items.Colors.Length, 1);
        }

        [Test]
        public void EveryRobotAndDeviceHasItsConfigNextToThePrefab()
        {
            int count = 0;
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                ScriptableObject profile = go.GetComponent<Detector>()?.ProfileBase;
                if (go.GetComponent<LaserGate>() != null) profile = go.GetComponent<LaserGate>().Profile;
                if (go.GetComponent<Detector>() == null && go.GetComponent<LaserGate>() == null) continue;
                count++;
                Assert.IsNotNull(profile, $"{path} has a config");
                var configPath = AssetDatabase.GetAssetPath(profile);
                Assert.AreEqual(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetDirectoryName(configPath), $"{path}: config lies next to the prefab");
                Assert.AreEqual(System.IO.Path.GetFileNameWithoutExtension(path) + "Config", System.IO.Path.GetFileNameWithoutExtension(configPath));
            }
            Assert.AreEqual(RobotRoster.KindCount + 1, count, "every kind and the laser gate");
        }

        [Test]
        public void PrefabsHoldNoTuningNumbers()
        {
            // Robots and devices keep only references on the prefab; numbers belong to their config.
            foreach (var type in new[] { typeof(SentryTurret), typeof(SpiderBot), typeof(CeilingVacuum), typeof(LidarScanner),
                         typeof(MotionSensor), typeof(PressurePlate), typeof(SecurityCamera), typeof(LaserGate), typeof(RobotController), typeof(RobotAnimator) })
            {
                var numbers = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .Where(f => f.FieldType == typeof(float) || f.FieldType == typeof(int)).Select(f => f.Name).ToArray();
                CollectionAssert.IsEmpty(numbers, $"{type.Name} has tuning fields on the prefab: {string.Join(", ", numbers)}");
            }
        }

        [Test]
        public void EveryMobileRobotHasItsOwnProfile()
        {
            var profiles = AssetDatabase.FindAssets("t:RobotProfile", new[] { "Assets/_Game/Prefabs/Robots" });
            Assert.GreaterOrEqual(profiles.Length, 8, "guard, listener, drone, thumper, hound, sorter, worker, hunter");
            var guard = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Robots/RobotGuard.prefab").GetComponent<RobotController>();
            var hound = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Robots/RobotHound.prefab").GetComponent<RobotController>();
            Assert.AreNotSame(guard.Profile, hound.Profile);
            Assert.IsTrue(guard.Senses.HasEyes);
            Assert.Greater(hound.Senses.SmellRange, 0f);
        }

        [Test]
        public void BuildingFollowsItsSettings()
        {
            var b = new BuildingSettings { FloorsPerHouse = new[] { 4 }, BaseWidth = 2, MaxExtraWidth = 0, VentChance = 0f, AutoDoorChance = 0f };
            var l = LevelGenerator.Generate(5, 0, b);
            Assert.AreEqual(4, l.Floors);
            Assert.AreEqual(2, l.Width);
            Assert.IsFalse(l.EdgesX.Cast<EdgeType>().Concat(l.EdgesZ.Cast<EdgeType>()).Contains(EdgeType.Vent), "no vents");
            Assert.AreEqual(0, l.AutoDoors.Count);

            var onlyServers = new BuildingSettings { RoomWeights = new[] { new RoomWeight(RoomType.Server, 1) } };
            var s = LevelGenerator.Generate(5, 1, onlyServers);
            foreach (var room in s.Rooms) Assert.IsTrue(room == RoomType.Server || room == RoomType.Lobby);
        }

        [Test]
        public void DefaultSettingsKeepTheBalanceOfTheCode()
        {
            // The same seed with explicit defaults and without settings: identical houses.
            var a = LevelGenerator.Generate(77, 3);
            var b = LevelGenerator.Generate(77, 3, new BuildingSettings(), new SecuritySettings());
            Assert.AreEqual(a.Floors, b.Floors);
            CollectionAssert.AreEqual(a.Rooms, b.Rooms);
            Assert.AreEqual(a.RobotSpawns.Count, b.RobotSpawns.Count);
        }

        [Test]
        public void SecurityCountsAndRosterFollowTheSettings()
        {
            var calm = new SecuritySettings { Robots = 0, RobotsPerHouse = 0, RobotsPerExtraFloor = 0, Devices = 0, DevicesPerHouse = 0, Cameras = 0, CamerasPerHouse = 0, Lasers = 0, LasersPerHouse = 0 };
            var l = LevelGenerator.Generate(9, 4, null, calm);
            Assert.AreEqual(0, l.RobotSpawns.Count);
            Assert.AreEqual(0, l.CameraSpots.Count);
            Assert.AreEqual(0, l.LaserSpots.Count);

            var drones = new SecuritySettings { Roster = new RosterSettings { Robots = new[] { new RosterEntry(RobotKind.Drone, 0, 1f) } } };
            var d = LevelGenerator.Generate(9, 0, null, drones);
            foreach (var spawn in d.RobotSpawns.Where(s => !RobotRoster.IsDevice(s.Kind))) Assert.AreEqual(RobotKind.Drone, spawn.Kind);
        }

        [Test]
        public void LootCountFollowsTheSettings()
        {
            var l = LevelGenerator.Generate(3, 0);
            Assert.Greater(LootPlanner.ItemCount(l, new LootSettings { BaseItems = 20 }), LootPlanner.ItemCount(l));
            Assert.AreEqual(0, LootPlanner.Plan(l, new System.Random(1), new LootSettings { BaseItems = 0, ItemsPerHouse = 0, RoomsPerExtraItem = 1000 }).Count);
        }

        [Test]
        public void RunRulesFollowTheSettings()
        {
            var impatient = new RunSettings { HunterAlarmsFirstHouse = 1 };
            Assert.IsTrue(RunState.HunterDue(1, 0f, 0, 0, impatient));
            Assert.IsFalse(RunState.HunterDue(1, 0f, 0, 0));
            Assert.Greater(RunState.QuotaFor(1000, 0, 0, new RunSettings { QuotaShare = 0.6f }), RunState.QuotaFor(1000, 0, 0));
            var run = new RunState(new RunSettings { AlarmDuration = 1f });
            run.RaiseAlarm(0.0);
            Assert.IsFalse(run.AlarmActive(1.5));
        }

        [Test]
        public void PlayerRulesFollowTheSettings()
        {
            var stamina = new Stamina(new StaminaSettings { SprintDuration = 1f });
            for (int i = 0; i < 11; i++) stamina.Tick(true, 0.1f);
            Assert.IsTrue(stamina.Exhausted, "a short sprint bar empties in a second");

            var gas = new GasExposure(new GasSettings { Limit = 2f });
            var last = GasExposure.Effect.None;
            for (int i = 0; i < 25 && last != GasExposure.Effect.Knockout; i++) last = gas.Tick(true, 0.1f);
            Assert.AreEqual(GasExposure.Effect.Knockout, last);

            var h = HealthState.Full(0f, new HealthSettings { LevelStartGrace = 0f, HitGrace = 0f });
            Assert.IsTrue(h.TryHit(0.1f));
            Assert.IsTrue(h.TryHit(0.2f), "no invulnerability");
            Assert.IsTrue(h.Downed);

            Assert.AreEqual(20f, new FootstepNoise(new NoiseSettings { SprintRadius = 20f }).Radius(false, true));
        }

        [Test]
        public void ItemValuesComeFromTheConfig()
        {
            var original = ItemCatalog.Colors;
            int points = ItemCatalog.PointsPerCell;
            try
            {
                var colors = ItemCatalog.DefaultColors();
                colors[0].Multiplier = 10;
                ItemCatalog.Configure(colors, 2);
                Assert.AreEqual(1 * 10 * 2, ItemCatalog.Value(0, 0), "a single grey cell");
            }
            finally
            {
                ItemCatalog.Configure(original, points);
            }
        }
    }
}
