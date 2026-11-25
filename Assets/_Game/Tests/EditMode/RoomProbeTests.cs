using System.Collections.Generic;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stealth.Tests
{
    /// <summary>The rooms' own reflections (<see cref="RoomProbes"/>): which probe is taken next, how a picture fades with its lamps, where a probe stands.</summary>
    public class RoomProbeTests
    {
        static RoomProbes.State P(float distance, bool taken = true, bool stale = false, float age = 0f) =>
            new() { Distance = distance, Taken = taken, Stale = stale, Age = age };

        [Test]
        public void ARoomWithNoPictureComesFirstThenAStaleOneThenTheOldOneTheCameraStandsIn()
        {
            Assert.AreEqual(-1, RoomProbes.Next(new List<RoomProbes.State> { P(0f), P(3f) }), "all fresh: nothing to take");
            Assert.AreEqual(1, RoomProbes.Next(new List<RoomProbes.State> { P(0f, stale: true), P(6f, taken: false) }), "no picture beats a stale one");
            Assert.AreEqual(2, RoomProbes.Next(new List<RoomProbes.State> { P(0f, age: 99f), P(8f, stale: true), P(2f, stale: true) }), "the nearest stale one");
            Assert.AreEqual(0, RoomProbes.Next(new List<RoomProbes.State> { P(0f, age: RoomProbes.RefreshEvery + 1f), P(2f, age: 99f) }), "only the room the camera stands in is refreshed by age");
            Assert.AreEqual(-1, RoomProbes.Next(new List<RoomProbes.State> { P(RoomProbes.Reach + 1f, taken: false) }), "too far: its lamps are not drawn");
            var dark = P(0f, taken: false);
            dark.Dark = true;
            Assert.AreEqual(-1, RoomProbes.Next(new List<RoomProbes.State> { dark }), "its lamps are out: no picture is taken");
        }

        [Test]
        public void APictureFadesWithItsLampsAndGoesStaleWhenTheyChange()
        {
            Assert.AreEqual(1f, RoomProbes.Share(2f, 2f));
            Assert.AreEqual(0f, RoomProbes.Share(0f, 2f), "the lamps are out: the room mirrors nothing, at once");
            Assert.AreEqual(0.5f, RoomProbes.Share(1f, 2f), 1e-4f);
            Assert.AreEqual(1f, RoomProbes.Share(3f, 2f), "never brighter than it was taken");
            Assert.AreEqual(0f, RoomProbes.Share(0f, 0f), "a dark room mirrors nothing: its picture would be of whoever walked through with a flashlight");
            Assert.IsFalse(RoomProbes.Stale(2f, 2.02f));
            Assert.IsTrue(RoomProbes.Stale(0f, 2f));
            Assert.IsTrue(RoomProbes.Stale(2f, 0f), "a dark room lit up");
            Assert.IsFalse(RoomProbes.Stale(0f, 0f));
        }

        [Test]
        public void AProbeStandsInsideItsRoomAndFillsItFloorToCeiling()
        {
            foreach (int seed in new[] { 7, 42 })
            {
                var l = LevelGenerator.Generate(seed, 2);
                foreach (var r in l.Plan.Rooms)
                {
                    var (eye, centre, size) = RoomProbes.Box(r);
                    var box = new Bounds(centre, size);
                    Assert.IsTrue(box.Contains(eye), $"seed {seed}, room {r.Id}: the eye is outside its box");
                    Assert.AreEqual(r.Id, l.Plan.SpaceAt(r.Floor, new Vector2(eye.x, eye.z)), $"seed {seed}, room {r.Id}: the eye is not in its room");
                    Assert.AreEqual(LevelLayout.FloorY(r.Floor), box.min.y, 1e-3f);
                    Assert.GreaterOrEqual(size.y, LevelMetrics.WallHeight - 1e-3f);
                    Assert.AreEqual(r.Bounds.width, size.x, 1e-3f);
                    Assert.AreEqual(r.Bounds.height, size.z, 1e-3f);
                }
            }
        }

        [Test]
        public void ThePrefabIsTakenOnDemandAndProjectedAsABox()
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            Assert.IsNotNull(art.RoomProbe, "no RoomProbe prefab");
            var probe = art.RoomProbe.GetComponent<RoomProbe>();
            Assert.IsNotNull(probe);
            Assert.IsNotNull(probe.Probe);
            Assert.AreEqual(ReflectionProbeMode.Realtime, probe.Probe.mode);
            Assert.AreEqual(ReflectionProbeRefreshMode.ViaScripting, probe.Probe.refreshMode, "never every frame: a cube picture is six renders of the house");
            Assert.AreEqual(ReflectionProbeTimeSlicingMode.IndividualFaces, probe.Probe.timeSlicingMode);
            Assert.IsTrue(probe.Probe.boxProjection);
            Assert.AreEqual(0, probe.Probe.cullingMask & LayerMask.GetMask("OwnBody", "Marked", "WorldText"), "the player's own body and the overlays are not mirrored");
        }

        [Test]
        public void OnlyGlossAndMetalMirrorTheirRoom()
        {
            Assert.IsFalse(RoomProbes.Mirrors(0.3f, 0f), "matt paint, cloth, carpet: a veil of light and grain, no reflection");
            Assert.IsTrue(RoomProbes.Mirrors(0.8f, 0f), "glaze, glass, gloss paint");
            Assert.IsTrue(RoomProbes.Mirrors(0.3f, 1f), "metal, however rough: it has no colour but what it mirrors");
            int mirrors = 0, matt = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".mat")) continue; // (inside an imported model: the importer's, not ours to switch)
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!MaterialGloss.Lit(m) || m.name == "CarGlass") continue;
                var (smoothness, metal) = MaterialGloss.Of(m);
                bool should = RoomProbes.Mirrors(smoothness, metal);
                // (a material right at the line may read a hair differently on another graphics card)
                if (Mathf.Abs(smoothness - RoomProbes.MirrorFrom) < 0.02f || Mathf.Abs(metal - RoomProbes.MirrorMetal) < 0.02f) continue;
                Assert.AreEqual(!should, m.IsKeywordEnabled(MaterialGloss.NoMirrorKeyword), $"{path}: smoothness {smoothness:0.00}, metal {metal:0.00} — run the asset build (AssetBuilder.OnlyGlossMirrors)");
                if (should) mirrors++; else matt++;
            }
            Assert.Greater(mirrors, 10, "nothing mirrors its room");
            Assert.Greater(matt, 10, "everything mirrors its room");
        }

        [Test]
        public void OnlyTheDefaultStyleMirrorsItsRooms()
        {
            var config = AssetDatabase.LoadAssetAtPath<StyleConfig>("Assets/_Game/Data/Styles/Styles.asset");
            foreach (var style in config.Styles)
                if (style.name == "S16_Derelict") Assert.That(style.Grade.Probes, Is.InRange(0.3f, 2f));
                else Assert.AreEqual(0f, style.Grade.Probes, $"{style.name}: a drawn style has its own shading");
        }
    }
}
