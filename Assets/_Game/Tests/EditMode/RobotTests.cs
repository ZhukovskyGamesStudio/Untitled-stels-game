using System.Collections.Generic;
using NUnit.Framework;
using Stealth.Enemies;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    public class RobotTests
    {
        [Test]
        public void RosterUnlocksNewRobotsDeeper()
        {
            HashSet<RobotKind> Kinds(int level)
            {
                var set = new HashSet<RobotKind>();
                var rng = new System.Random(1);
                for (int i = 0; i < 3000; i++) { set.Add(RobotRoster.PickRobot(level, rng)); set.Add(RobotRoster.PickDevice(level, rng)); }
                return set;
            }
            var first = Kinds(0);
            var deep = Kinds(6);
            Assert.IsTrue(first.Contains(RobotKind.Guard) && first.Contains(RobotKind.Listener));
            Assert.IsFalse(first.Contains(RobotKind.Hound), "hounds come later");
            Assert.Greater(deep.Count, first.Count);
            foreach (RobotKind k in System.Enum.GetValues(typeof(RobotKind)))
                if (k != RobotKind.Hunter) Assert.IsTrue(deep.Contains(k), $"{k} appears on deep levels");
            Assert.IsFalse(deep.Contains(RobotKind.Hunter), "hunters are never placed, they arrive");
        }

        [Test]
        public void StairsRulesMatchTheBody()
        {
            Assert.IsTrue(RobotRoster.CanUseStairs(RobotKind.Guard));
            Assert.IsTrue(RobotRoster.CanUseStairs(RobotKind.Drone));
            Assert.IsFalse(RobotRoster.CanUseStairs(RobotKind.Listener), "wheels");
            Assert.IsFalse(RobotRoster.CanUseStairs(RobotKind.Thumper), "tracks");
            Assert.IsFalse(RobotRoster.CanUseStairs(RobotKind.Spider), "stays on its walls");
            Assert.IsFalse(RobotRoster.CanUseStairs(RobotKind.Turret));
        }

        [Test]
        public void LevelsPlaceRobotsAndDevicesOfKnownKinds()
        {
            var l = LevelGenerator.Generate(3, 5);
            int devices = 0, robots = 0;
            foreach (var r in l.RobotSpawns)
            {
                Assert.IsTrue(RobotRoster.IsUnlocked(r.Kind, 5));
                if (RobotRoster.IsDevice(r.Kind)) devices++; else robots++;
            }
            Assert.Greater(robots, 4);
            Assert.Greater(devices, 1);
        }

        [Test]
        public void ConeChecks()
        {
            Assert.IsTrue(Perception.InCone(Vector3.zero, Vector3.forward, new Vector3(0, 0, 5), 10, 60));
            Assert.IsFalse(Perception.InCone(Vector3.zero, Vector3.forward, new Vector3(5, 0, 1), 10, 60));
            Assert.IsFalse(Perception.InCone(Vector3.zero, Vector3.forward, new Vector3(0, 0, 11), 10, 60));
            Assert.IsTrue(Perception.InFlatCone(Vector3.zero, Vector3.forward, new Vector3(0, 5, 3), 10, 60), "flat ignores height");
            Assert.IsFalse(Perception.InCone(Vector3.zero, Vector3.forward, new Vector3(0, 5, 3), 10, 60));
        }

        [Test]
        public void ScentTrailLeadsTowardsThePlayer()
        {
            ScentTrail.Clear();
            for (int i = 0; i <= 20; i++) ScentTrail.Record(7, new Vector3(i, 0, 0), i * 0.5f); // walked 20 m along +X
            Assert.Greater(ScentTrail.Smell(7, new Vector3(2, 0, 1), 3f, 10f), 0.5f, "fresh scent near the start");
            Assert.AreEqual(0f, ScentTrail.Smell(7, new Vector3(2, 0, 10), 3f, 10f), "nothing far from the trail");
            Assert.IsTrue(ScentTrail.NextAlongTrail(7, new Vector3(3, 0, 0), 7f, out var next));
            Assert.Greater(next.x, 3f, "the next breadcrumb is further along (newer)");
            Assert.AreEqual(0f, ScentTrail.Smell(7, Vector3.zero, 3f, 200f), "scent fades");
            ScentTrail.Clear();
        }

        [Test]
        public void ScanLineCrossing()
        {
            var c = Vector3.zero;
            var dir = ScanLine.Direction(0f, 90f); // +Z at t = 0
            Assert.AreEqual(1f, dir.z, 1e-4f);
            Assert.IsTrue(ScanLine.Crosses(c, dir, 3f, true, new Vector3(0.2f, 0, 2f)));
            Assert.IsTrue(ScanLine.Crosses(c, dir, 3f, true, new Vector3(0f, 0, -2f)), "two-sided");
            Assert.IsFalse(ScanLine.Crosses(c, dir, 3f, false, new Vector3(0f, 0, -2f)), "one-sided");
            Assert.IsFalse(ScanLine.Crosses(c, dir, 3f, true, new Vector3(1f, 0, 2f)), "beside the line");
            Assert.IsFalse(ScanLine.Crosses(c, dir, 3f, true, new Vector3(0f, 0, 4f)), "beyond its end");
            Assert.AreEqual(1f, ScanLine.Direction(1f, 90f).x, 1e-4f, "a quarter turn per second");
            Assert.IsTrue(ScanLine.Above(0.6f, 0.4f));
            Assert.IsFalse(ScanLine.Above(0.0f, 0.4f));
        }

        [Test]
        public void SeismicFeelsWalkersButNotSneakers()
        {
            var sensor = new Vector3(0, 0, 0);
            Assert.IsTrue(SeismicSense.Feels(sensor, new Vector3(6, 0, 0), 3.6f, false, 10f), "walking");
            Assert.IsFalse(SeismicSense.Feels(sensor, new Vector3(6, 0, 0), 1.8f, true, 10f), "crouching");
            Assert.IsFalse(SeismicSense.Feels(sensor, new Vector3(6, 0, 0), 0.2f, false, 10f), "standing still");
            Assert.IsFalse(SeismicSense.Feels(sensor, new Vector3(12, 0, 0), 6f, false, 10f), "too far");
            Assert.IsFalse(SeismicSense.Feels(sensor, new Vector3(2, 3.5f, 0), 6f, false, 10f), "other floor");
        }
    }
}
