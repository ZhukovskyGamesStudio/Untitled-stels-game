using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Audio;
using Stealth.Items;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Furniture that opens (drawers, lockers), loot hidden in it, and music off in the houses.</summary>
    public class StorageTests
    {
        [Test]
        public void DoorsHingeOnTheirFrontLeftEdge()
        {
            var door = new Bounds(new Vector3(2f, 1f, 5f), new Vector3(0.28f, 1.8f, 0.02f));
            var hinge = StorageMath.Hinge(door, Vector3.right, Vector3.forward);
            Assert.AreEqual(2f - 0.14f, hinge.x, 1e-4f);
            Assert.AreEqual(5f + 0.01f, hinge.z, 1e-4f, "on the front face");
            // the same furniture turned to face +X: the left is now +Z
            var turned = new Bounds(new Vector3(2f, 1f, 5f), new Vector3(0.02f, 1.8f, 0.28f));
            var h2 = StorageMath.Hinge(turned, Vector3.back, Vector3.right);
            Assert.AreEqual(5f + 0.14f, h2.z, 1e-4f);
            Assert.AreEqual(2f + 0.01f, h2.x, 1e-4f);
        }

        [Test]
        public void HiddenLootAppearsInsideThePart()
        {
            var drawer = new Bounds(new Vector3(0f, 0.6f, 0f), new Vector3(0.86f, 0.4f, 0.42f));
            var inDrawer = StorageMath.LootSpot(drawer, Vector3.forward, 0f, drawer: true);
            Assert.Greater(inDrawer.z, 0.2f, "where the drawer is pulled out to");
            Assert.AreEqual(drawer.min.y + 0.08f, inDrawer.y, 1e-4f, "on its bottom");
            var door = new Bounds(new Vector3(0f, 0.97f, -0.245f), new Vector3(0.28f, 1.82f, 0.02f));
            var inLocker = StorageMath.LootSpot(door, Vector3.back, 0f, drawer: false);
            Assert.Greater(inLocker.z, door.center.z, "behind the door, inside the compartment");
            Assert.Less(inLocker.y, 0.3f, "on its floor");
        }

        [Test]
        public void AboutOneItemInTenHidesInFurnitureOnePerPart()
        {
            var rng = new System.Random(5);
            var free = Enumerable.Range(0, 400).ToList();
            int hidden = 0;
            var used = new HashSet<int>();
            for (int i = 0; i < 1000; i++)
            {
                int key = StorageMath.Hide(free, rng, 0.1f);
                if (key < 0) continue;
                hidden++;
                Assert.IsTrue(used.Add(key), "one item per part");
            }
            Assert.That(hidden, Is.InRange(70, 130), "about 10 %");
            Assert.AreEqual(-1, StorageMath.Hide(new List<int>(), rng, 1f), "no furniture, nothing hides");
            Assert.AreEqual(-1, StorageMath.Hide(new List<int> { 3 }, rng, 0f), "chance 0, nothing hides");
        }

        [Test]
        public void MusicCanBeOffInTheHouses()
        {
            Assert.AreEqual(MusicTrack.Menu, AudioRules.ChooseMusic(false, 99f, 5f, levelMusic: false), "the menu keeps its music");
            Assert.AreEqual(MusicTrack.None, AudioRules.ChooseMusic(true, 99f, 5f, levelMusic: false));
            Assert.AreEqual(MusicTrack.None, AudioRules.ChooseMusic(true, 0f, 5f, levelMusic: false), "no chase music either");
            Assert.AreEqual(MusicTrack.Explore, AudioRules.ChooseMusic(true, 99f, 5f));
            var audio = AssetDatabase.LoadAssetAtPath<Configs.AudioConfig>("Assets/_Game/Data/Config/RoomsTest/Audio.asset");
            Assert.IsNotNull(audio);
            Assert.IsFalse(audio.Mix.LevelMusic, "the test level is quiet");
        }

        [TestCase("Cabinet", "BodyDrawer", 3)]
        [TestCase("Lockers", "BodyDoor", 2)]
        public void TheFurnitureHasPartsThatOpen(string prefab, string part, int count)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Props/{prefab}.prefab");
            Assert.IsNotNull(go.GetComponent<Storage>(), $"{prefab} does not open");
            var parts = go.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(part)).ToArray();
            Assert.AreEqual(count, parts.Length);
            foreach (var p in parts) Assert.IsNotNull(p.GetComponent<Collider>(), $"{p.name}: can't be looked at");
        }

        [Test]
        public void TestHousesHaveFurnitureToOpen()
        {
            var level = AssetDatabase.LoadAssetAtPath<Configs.LevelConfig>("Assets/_Game/Data/Config/RoomsTest/Level.asset");
            int withStorage = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var l = LevelGenerator.Generate(seed * 104729, 0, level.Building, level.Security, 1);
                if (l.Props.Any(p => p.Kind is PropKind.Cabinet or PropKind.Lockers)) withStorage++;
            }
            Assert.Greater(withStorage, 10, "most small houses have drawers or lockers");
        }
    }
}
