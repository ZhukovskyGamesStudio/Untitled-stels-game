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
    }
}
