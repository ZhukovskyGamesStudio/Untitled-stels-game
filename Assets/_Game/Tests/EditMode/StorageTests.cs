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
        public void TwoDoorsSideBySideOpenFromTheMiddle()
        {
            // a two-door cupboard (0.9 m wide), a bank of three lockers, a door over a drawer
            var pair = new List<Bounds> { new(new Vector3(-0.22f, 1f, 0f), new Vector3(0.44f, 1.8f, 0.02f)), new(new Vector3(0.22f, 1f, 0f), new Vector3(0.44f, 1.8f, 0.02f)) };
            CollectionAssert.AreEqual(new[] { false, true }, StorageMath.RightHinged(pair, Vector3.right), "the right leaf on its right");
            CollectionAssert.AreEqual(new[] { true, false }, StorageMath.RightHinged(pair, Vector3.left), "seen from behind (turned 180°) the other way round");
            var three = new List<Bounds>();
            for (int i = 0; i < 3; i++) three.Add(new Bounds(new Vector3(i * 0.4f, 1f, 0f), new Vector3(0.38f, 1.8f, 0.02f)));
            Assert.AreEqual(1, StorageMath.RightHinged(three, Vector3.right).Count(x => x), "one pair, the third alone");
            var stacked = new List<Bounds> { new(new Vector3(0f, 1.4f, 0f), new Vector3(0.5f, 0.6f, 0.02f)), new(new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 0.6f, 0.02f)) };
            CollectionAssert.AreEqual(new[] { false, false }, StorageMath.RightHinged(stacked, Vector3.right), "one above the other: no pair");
            var hinge = StorageMath.Hinge(pair[1], Vector3.right, Vector3.forward, true);
            Assert.AreEqual(0.44f, hinge.x, 1e-4f, "the right leaf's hinge on its right edge");
        }

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
            Assert.AreEqual(StorageMath.DrawerFloor(drawer, 0f) + 0.05f, inDrawer.y, 1e-4f, "on its floor");
            var door = new Bounds(new Vector3(0f, 0.97f, -0.245f), new Vector3(0.28f, 1.82f, 0.02f));
            var inLocker = StorageMath.LootSpot(door, Vector3.back, 0f, drawer: false);
            Assert.Greater(inLocker.z, door.center.z, "behind the door, inside the compartment");
            Assert.Less(inLocker.y, 0.3f, "on its floor");
        }

        [Test]
        public void AHighDrawersFloorIsNearItsTopSoTheLootIsSeenOverTheFront()
        {
            // the office cabinet's three drawers: fronts 0.34 m tall, their tops at 0.41, 0.78 and 1.15 m
            Bounds Front(float top) => new(new Vector3(0f, top - 0.17f, 0f), new Vector3(0.86f, 0.34f, 0.42f));
            Assert.AreEqual(Front(0.41f).min.y + 0.03f, StorageMath.DrawerFloor(Front(0.41f), 0f), 1e-4f, "the bottom drawer: the model's own floor");
            float top = StorageMath.DrawerFloor(Front(1.15f), 0f);
            Assert.Greater(top, Front(1.15f).min.y + 0.12f, "the top drawer: a floor raised towards its front's top");
            Assert.LessOrEqual(top, 1.15f - StorageMath.ShallowDrawer + 1e-4f, "still a drawer: something lies in it");
            // looking over the front from a step away (0.4 m), no more than a hand's width of the floor behind it is hidden
            foreach (float h in new[] { 0.41f, 0.78f, 1.15f, 1.3f })
            {
                float floor = StorageMath.DrawerFloor(Front(h), 0f);
                float hidden = (h - floor) * 0.4f / (StorageMath.EyeHeight - h);
                Assert.Less(hidden, 0.17f, $"front top at {h}: {hidden:0.00} m of the floor hidden");
            }
            Assert.AreEqual(2f + 0.41f - 0.34f + 0.03f, StorageMath.DrawerFloor(new Bounds(new Vector3(0f, 2.24f, 0f), new Vector3(0.86f, 0.34f, 0.42f)), 2f), 1e-4f, "on an upper storey: from its floor");
            var pullOut = new Bounds(new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.8f, 0.5f));
            Assert.AreEqual(pullOut.min.y + 0.03f, StorageMath.DrawerFloor(pullOut, 0f), 1e-4f, "a dishwasher's pull-out is no drawer");
            // the loot lies on that floor, where the drawer is pulled out to
            var (pos, _) = StorageMath.LootPose(new Vector3(0.22f, 0.1f, 0.22f), Front(1.15f), Vector3.forward, 0.44f, drawer: true);
            Assert.AreEqual(top + 0.01f, pos.y, 1e-4f);
            Assert.AreEqual(StorageMath.Travel(0.44f), pos.z, 1e-4f);
            Assert.AreEqual(0.3f, StorageMath.Travel(0.34f), 1e-4f, "a shallow piece: the drawer's back stays in the carcass");
        }

        [Test]
        public void LootPutInADrawerFitsUnderItsFront()
        {
            var front = new Bounds(new Vector3(0f, 0.55f, 0f), new Vector3(0.39f, 0.11f, 0.49f)); // a pedestal's top drawer
            float room = StorageMath.DrawerRoom(front, 0f);
            Assert.That(room, Is.InRange(0.03f, 0.11f));
            Assert.IsTrue(StorageMath.Fits(new Vector3(0.2f, room - 0.01f, 0.15f), front, Vector3.forward, 0.49f, true));
            Assert.IsFalse(StorageMath.Fits(new Vector3(0.2f, room + 0.01f, 0.15f), front, Vector3.forward, 0.49f, true), "too tall: the drawer would not shut over it");
            Assert.IsFalse(StorageMath.Fits(new Vector3(0.36f, 0.02f, 0.15f), front, Vector3.forward, 0.49f, true), "too long for the tray");
        }

        [Test]
        public void EveryDrawerOfTheHousesTakesSomeLootAndWhatItTakesLiesInside()
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            int drawers = 0;
            foreach (PropKind kind in System.Enum.GetValues(typeof(PropKind)))
            {
                var prefab = art.Prop(kind);
                if (prefab == null || prefab.GetComponent<Storage>() == null) continue;
                var go = Object.Instantiate(prefab, new Vector3(900f, 4.1f, 900f), Quaternion.Euler(0f, 90f, 0f));
                try
                {
                    var storage = go.GetComponent<Storage>();
                    storage.Configure(0);
                    for (int part = 0; part < storage.PartCount; part++)
                    {
                        if (!storage.Drawers) continue;
                        drawers++;
                        int takes = 0;
                        for (int shape = 0; shape < ItemCatalog.Shapes.Length; shape++)
                        {
                            if (!ItemCatalog.IsLoot(shape) || !storage.Fits(part, shape)) continue;
                            takes++;
                            var (pos, rot) = storage.LootPose(part, shape);
                            var size = ItemCatalog.ModelSize(shape);
                            var turned = rot * size;
                            float top = pos.y + size.y, front = Mathf.Abs(turned.x) / 2f, side = Mathf.Abs(turned.z) / 2f; // (the piece looks along +X)
                            var inside = storage.OpenInside(part);
                            float frontTop = inside.max.y - 0.3f;
                            Assert.LessOrEqual(top, frontTop + 1e-3f, $"{kind} drawer {part}: item {shape} sticks out over its front");
                            Assert.LessOrEqual(side * 2f, inside.size.z - 0.1f, $"{kind} drawer {part}: item {shape} is wider than it");
                            Assert.LessOrEqual(front * 2f, inside.size.x - 0.1f, $"{kind} drawer {part}: item {shape} is longer than it");
                        }
                        Assert.GreaterOrEqual(takes, 6, $"{kind} drawer {part}: nothing to find in it — leave the piece out of the houses");
                    }
                }
                finally { Object.DestroyImmediate(go); }
            }
            Assert.Greater(drawers, 15);
        }

        [Test]
        public void TheDrawerLookedAtIsTheOneThatOpens([Values("FilePedestal", "ExecutiveDesk", "FilingCabinet", "Cabinet", "Dresser")] string name)
        {
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Props/{name}.prefab"), new Vector3(950f, 0f, 950f), Quaternion.identity);
            try
            {
                var storage = go.GetComponent<Storage>();
                storage.Configure(0);
                for (int part = 0; part < storage.PartCount; part++)
                {
                    var b = storage.OpenInside(part); // (the drawer pulled out, with room over it: back to where its front is)
                    var front = new Vector3(b.center.x, b.center.y - 0.15f, b.center.z - storage.Travel + (b.size.z - 0.1f) / 2f);
                    var eye = front + new Vector3(0.1f, Mathf.Max(0.2f, 1.6f - front.y), 0.9f);
                    Assert.AreEqual(part, storage.PartUnder(eye, front - eye), $"{name}: looked right at drawer {part}");
                }
                Assert.AreEqual(-1, storage.PartUnder(new Vector3(950f, 1.6f, 952f), Vector3.up), "looking away");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void DrawersOfOneColumnAreOneOverAnother()
        {
            Bounds D(float x, float y) => new(new Vector3(x, y, 0f), new Vector3(0.4f, 0.2f, 0.4f));
            Assert.IsTrue(StorageMath.SameColumn(D(0f, 0.2f), D(0f, 0.8f), Vector3.right));
            Assert.IsTrue(StorageMath.SameColumn(D(0f, 0.2f), D(0.05f, 0.5f), Vector3.right), "a little askew");
            Assert.IsFalse(StorageMath.SameColumn(D(-0.5f, 0.5f), D(0.5f, 0.5f), Vector3.right), "the two pedestals of a desk");
            Assert.IsTrue(StorageMath.SameColumn(D(0f, 0.2f), D(0f, 0.8f), Vector3.forward), "the furniture turned");
            Assert.AreEqual(90f, StorageMath.DoorAngle, "an open door stands square to the front: it leans across no compartment");
            // a searched, empty drawer pushed back by another one stays a little out
            Assert.AreEqual(0f, StorageMath.Slide(0f, 0f, 0.38f), "shut");
            Assert.AreEqual(0.38f, StorageMath.Slide(1f, 0f, 0.38f), 1e-5f, "open");
            Assert.AreEqual(StorageMath.AjarGap, StorageMath.Slide(0f, 1f, 0.38f), 1e-5f, "left ajar");
            Assert.AreEqual(0.38f, StorageMath.Slide(1f, 1f, 0.38f), 1e-5f, "opened again: all the way out");
            Assert.AreEqual(0.19f, StorageMath.Slide(0.5f, 1f, 0.38f), 1e-5f, "sliding in: not there yet");
            Assert.Less(StorageMath.AjarGap, 0.12f, "ajar: nothing in it can be seen or reached");
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

        [Category("Slow")] // generates many houses (Tools/dev.sh: SLOW=1 runs it)
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
