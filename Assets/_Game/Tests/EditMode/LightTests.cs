using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    public class LightTests
    {
        [Test]
        public void SomeRoomsAreDarkButNeverTheLobby()
        {
            int dark = 0, total = 0;
            foreach (var seed in new[] { 1, 2, 3, 4, 5, 6, 7, 8 })
            {
                var l = LevelGenerator.Generate(seed, 3);
                foreach (var r in l.Rooms)
                {
                    total++;
                    if (r.Dark) dark++;
                    if (r.Type == RoomType.Lobby) Assert.IsFalse(r.Dark);
                    Assert.IsFalse(r.Dark && r.Flicker);
                }
            }
            float share = dark / (float)total;
            Assert.That(share, Is.InRange(0.1f, 0.4f), $"dark share {share:0.00}");
        }

        [Test]
        public void IsDarkAtMatchesRooms()
        {
            var l = LevelGenerator.Generate(9, 4);
            foreach (var r in l.Rooms)
                Assert.AreEqual(r.Dark, l.IsDarkAt(r.Center + Vector3.up));
            Assert.IsFalse(l.IsDarkAt(new Vector3(-5, 0, -5)), "outside is not a dark room");
        }

        [Test]
        public void AutoDoorOpensOnlyForNearbyAgentsOnTheSameFloor()
        {
            var door = new Vector3(10, 0, 5);
            Assert.IsFalse(AutoDoor.ShouldOpen(door, new Vector3[0]));
            Assert.IsTrue(AutoDoor.ShouldOpen(door, new[] { new Vector3(11.5f, 0, 5) }));
            Assert.IsFalse(AutoDoor.ShouldOpen(door, new[] { new Vector3(14f, 0, 5) }), "too far");
            Assert.IsFalse(AutoDoor.ShouldOpen(door, new[] { new Vector3(10.5f, 3.5f, 5) }), "a floor above");
        }

        [Test]
        public void DoorsAndLasersNeverShareADoorway()
        {
            foreach (var seed in new[] { 1, 2, 3, 4, 5 })
            {
                var l = LevelGenerator.Generate(seed, 5);
                Assert.Greater(l.AutoDoors.Count, 0);
                foreach (var d in l.AutoDoors)
                    foreach (var laser in l.LaserSpots)
                        Assert.Greater(Vector3.Distance(d.position, laser.Position), 0.5f);
            }
        }

        [Test]
        public void FlickerStaysInRangeAndActuallyFlickers()
        {
            float min = 1f, max = 0f;
            for (int i = 0; i < 2000; i++)
            {
                float k = LightMath.Flicker(i * 0.05f);
                Assert.That(k, Is.InRange(0f, 1f));
                min = Mathf.Min(min, k);
                max = Mathf.Max(max, k);
            }
            Assert.AreEqual(1f, max);
            Assert.Less(min, 0.5f);
        }

        [Test]
        public void LightFadesLikeURPAndEndsAtTheRange()
        {
            Assert.Greater(LightMath.Attenuation(1f, 9f), LightMath.Attenuation(3f, 9f));
            Assert.AreEqual(1f / 4f, LightMath.Attenuation(2f, 1000f), 1e-3f, "inverse square");
            Assert.AreEqual(0f, LightMath.Attenuation(9f, 9f));
            Assert.AreEqual(LightMath.Attenuation(0.5f, 9f), LightMath.Attenuation(0.1f, 9f), 1e-4f, "no infinite hot spot");
            Assert.AreEqual(1f, LightMath.SpotFactor(10f, 60f, 140f), 1e-4f, "inside the inner cone");
            Assert.AreEqual(0f, LightMath.SpotFactor(75f, 60f, 140f), 1e-4f, "outside the cone");
            Assert.That(LightMath.SpotFactor(50f, 60f, 140f), Is.InRange(0.01f, 0.99f), "soft edge");
        }

        [Test]
        public void DarkRoomsHaveTheirLampsOff()
        {
            Assert.AreEqual(0f, RoomLighting.For(RoomType.Office, true).intensity);
            Assert.Greater(RoomLighting.For(RoomType.Office, false).intensity, 1f);
        }

        [Test]
        public void ThePoorerTheLightTheHarderToSee()
        {
            var s = new Stealth.Enemies.IlluminationSettings();
            float dark = Stealth.Enemies.SensorMath.LightFactor(0f, s);
            float dim = Stealth.Enemies.SensorMath.LightFactor(0.05f, s);
            float lit = Stealth.Enemies.SensorMath.LightFactor(1f, s);
            Assert.AreEqual(s.DarkFactor, dark, 1e-4f);
            Assert.AreEqual(1f, lit, 1e-4f);
            Assert.That(dim, Is.InRange(dark + 0.05f, lit - 0.05f), "half-light is in between, not on / off");
            Assert.Less(Stealth.Enemies.SensorMath.LightFactor(0.03f, s), dim);
            Assert.AreEqual(0f, Stealth.Enemies.SensorMath.Visibility(dark, s), 1e-4f);
            Assert.AreEqual(1f, Stealth.Enemies.SensorMath.Visibility(lit, s), 1e-4f);
        }

        [Test]
        public void StyleLightScaleIsVisualOnly()
        {
            // first frame: whatever the game set is raw
            Assert.AreEqual(3f, Stealth.Level.LightMath.RawIntensity(3f, -1f, -1f), 1e-5f);
            // the light still holds what the style showed (3 × 0.4): raw stays 3
            Assert.AreEqual(3f, Stealth.Level.LightMath.RawIntensity(1.2f, 3f, 1.2f), 1e-5f);
            // the lamp logic wrote a new value (flicker, alarm): that is the new raw
            Assert.AreEqual(0.15f, Stealth.Level.LightMath.RawIntensity(0.15f, 3f, 1.2f), 1e-5f);

            var go = new UnityEngine.GameObject("lamp");
            try
            {
                var light = go.AddComponent<UnityEngine.Light>();
                light.intensity = 2f;
                light.color = UnityEngine.Color.white;
                var src = go.AddComponent<Stealth.Level.LightSource>();
                float before = src.Brightness;
                Stealth.Level.LightSource.VisualScale = 0.3f;
                Assert.AreEqual(before, src.Brightness, 1e-5f, "robots must see the same light in every style");
            }
            finally
            {
                Stealth.Level.LightSource.VisualScale = 1f;
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
