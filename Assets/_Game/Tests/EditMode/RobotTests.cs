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
        public void SensorMountsFollowTheSenses()
        {
            var guard = new RobotSenses { HasEyes = true, HearingMultiplier = 0.8f };
            Assert.AreEqual((true, true, false, false), SensorRig.Mounts(guard));
            var thumper = new RobotSenses { HasEyes = false, HearingMultiplier = 0f, SeismicRange = 10f };
            Assert.AreEqual((false, false, true, false), SensorRig.Mounts(thumper));
            var hound = new RobotSenses { HasEyes = false, HearingMultiplier = 0.6f, SmellRange = 3f };
            Assert.AreEqual((false, true, false, true), SensorRig.Mounts(hound));
        }

        [Test]
        public void EveryMobileRobotPrefabWearsItsSensors()
        {
            foreach (var path in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs/Robots" }))
            {
                var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(path));
                var rc = go.GetComponent<RobotController>();
                if (rc == null) continue;
                var rig = go.GetComponent<SensorRig>();
                Assert.IsNotNull(rig, $"{go.name} has a sensor rig");
                Assert.AreSame(rc, rig.Robot);
                var (eyes, ears, ground, nose) = SensorRig.Mounts(rc.Senses);
                bool lidar = rc.Senses.LidarRange > 0f && rc.Lidar != null && rc.Lidar.Head != null; // the spinning puck shows itself
                Assert.IsTrue(eyes || ears || ground || nose || lidar, $"{go.name} shows at least one sensor");
                foreach (var r in rig.Eyes[0].GetComponentsInChildren<Renderer>())
                    if (r.name.StartsWith("Eye")) CollectionAssert.Contains(rc.Status.Renderers, r, $"{go.name}: the eye lens glows in the status colour");
            }
        }

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
                if (k != RobotKind.Hunter && k != RobotKind.Camera) Assert.IsTrue(deep.Contains(k), $"{k} appears on deep levels");
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
        public void TheCeilingFanCannotBeJumped()
        {
            var apex = new Vector3(0f, 2.9f, 0f);
            var dir = Vector3.forward;
            // standing in the sheet, 2 m out: caught; jumping there: still caught (the body is in the light)
            Assert.IsTrue(ScanLine.CutsFan(apex, dir, 0f, 3f, 3f, new Vector3(0.1f, 0f, 2f), 1.8f));
            Assert.IsTrue(ScanLine.CutsFan(apex, dir, 0f, 3f, 3f, new Vector3(0.1f, 0.8f, 2f), 1.8f), "in the air");
            Assert.IsTrue(ScanLine.CutsFan(apex, dir, 0f, 3f, 3f, new Vector3(0f, 0f, -2.5f), 1.1f), "both halves");
            Assert.IsFalse(ScanLine.CutsFan(apex, dir, 0f, 3f, 3f, new Vector3(0.6f, 0f, 2f), 1.8f), "beside the sheet");
            Assert.IsFalse(ScanLine.CutsFan(apex, dir, 0f, 3f, 3f, new Vector3(0f, 0f, 3.6f), 1.8f), "beyond its foot");
            // a wall cut this half short
            Assert.IsFalse(ScanLine.CutsFan(apex, dir, 0f, 1f, 3f, new Vector3(0f, 0f, 2f), 1.8f), "behind the wall");
            // the fan narrows upwards: high up (on a shelf) the edge is closer
            Assert.IsFalse(ScanLine.CutsFan(apex, dir, 0f, 3f, 3f, new Vector3(0f, 2f, 2.5f), 0.8f), "above its slope");
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
        public void MotionSensorAndPressurePlateRules()
        {
            Assert.IsTrue(MotionSensor.Triggers(3.6f, 2.2f), "walking");
            Assert.IsFalse(MotionSensor.Triggers(1.8f, 2.2f), "crouch-walking");
            var plate = new Vector3(5, 0, 5);
            Assert.IsTrue(PressurePlate.Steps(plate, 0.45f, new Vector3(5.3f, 0.02f, 4.8f)));
            Assert.IsFalse(PressurePlate.Steps(plate, 0.45f, new Vector3(6f, 0.02f, 5f)), "beside the plate");
            Assert.IsFalse(PressurePlate.Steps(plate, 0.45f, new Vector3(5f, 0.6f, 5f)), "jumping over");
            Assert.IsFalse(PressurePlate.Steps(plate, 0.45f, new Vector3(5f, 3.5f, 5f)), "a floor above");
        }

        [Test]
        public void MovingLaserSweepsBetweenJumpAndCrouchHeights()
        {
            float min = 10f, max = 0f;
            for (float t = 0; t < LaserGate.DefaultMovePeriod; t += 0.01f)
            {
                float h = LaserGate.MovingHeight(t);
                min = Mathf.Min(min, h);
                max = Mathf.Max(max, h);
            }
            Assert.Less(min, 0.3f, "low enough to jump over (jump ~0.9 m)");
            Assert.Greater(max, 1.5f, "high enough to crouch under (crouched 1.1 m)");
            Assert.AreEqual(LaserGate.MovingHeight(0.3f), LaserGate.MovingHeight(0.3f + LaserGate.DefaultMovePeriod), 1e-4f);
        }

        [Test]
        public void RadioReachesNearbyRobotsOnTheSameFloor()
        {
            var sender = new Vector3(0, 0, 0);
            var robots = new[]
            {
                sender,                       // itself
                new Vector3(8, 0, 0),         // near
                new Vector3(20, 0, 0),        // too far
                new Vector3(4, 3.5f, 0),      // floor above
                new Vector3(5, 2.1f, 5),      // a drone hovering on this floor
            };
            CollectionAssert.AreEquivalent(new[] { 1, 4 }, RobotRadio.Recipients(sender, robots));
        }

        [Test]
        public void HackingRules()
        {
            var robot = Vector3.zero;
            Assert.IsTrue(Hacking.CanHackRobot(robot, Vector3.forward, new Vector3(0, 0, -1.2f), false), "from behind");
            Assert.IsFalse(Hacking.CanHackRobot(robot, Vector3.forward, new Vector3(0, 0, 1.2f), false), "not from the front");
            Assert.IsFalse(Hacking.CanHackRobot(robot, Vector3.forward, new Vector3(0, 0, -1.2f), true), "not while it chases you");
            Assert.IsFalse(Hacking.CanHackRobot(robot, Vector3.forward, new Vector3(0, 0, -3f), false), "too far");
            Assert.IsTrue(Hacking.CanHackDevice(new Vector3(0, 2.2f, 0), new Vector3(1f, 0, 0)), "wall device from below");
            Assert.IsFalse(Hacking.CanHackDevice(new Vector3(0, 0, 0), new Vector3(3f, 0, 0)));
            Assert.IsTrue(Hacking.CanHackRobot(robot, Vector3.forward, new Vector3(-1.2f, 0, -0.2f), false), "from the side, a little behind");
        }

        [Test]
        public void HackProgressIsKeptWhenYouWalkAway()
        {
            var held = new HoldProgress<string>();
            string camera = "camera", guard = "guard";
            Assert.IsFalse(held.Hold(camera, 1f, 1.75f));
            Assert.IsFalse(held.Hold(guard, 0.5f, 1.75f), "another target has its own progress");
            Assert.AreEqual(1f, held.Of(camera), 1e-5f, "walking off to the guard kept the camera's progress");
            Assert.IsTrue(held.Hold(camera, 0.8f, 1.75f), "coming back finishes it");
            Assert.AreEqual(0f, held.Of(camera), "a finished target starts from zero next time");
            Assert.AreEqual(0.5f, held.Of(guard), 1e-5f);
        }

        [Test]
        public void ASeismicWaveFeelsWhoeverMovesWhenItPasses()
        {
            Assert.AreEqual(4.5f, SeismicSense.WaveRadius(0.5f, 9f), 1e-4f);
            Assert.IsTrue(SeismicSense.WaveTouches(4.6f, 4.5f, 9f, 0.016f), "the front is at the player");
            Assert.IsFalse(SeismicSense.WaveTouches(8f, 4.5f, 9f, 0.016f), "not there yet");
            float walk = SeismicSense.WaveSuspicion(0.45f, 3.6f, false, 5f, 10f);
            Assert.Greater(walk, 0.1f, "walking: felt");
            Assert.AreEqual(0f, SeismicSense.WaveSuspicion(0.45f, 0f, false, 5f, 10f), "standing still: the wave passes by");
            Assert.Less(SeismicSense.WaveSuspicion(0.45f, 1.8f, true, 5f, 10f), walk * 0.3f, "crouching: barely");
            Assert.Greater(SeismicSense.WaveSuspicion(0.45f, 6.5f, false, 5f, 10f), walk, "running: more");
            Assert.Greater(walk, SeismicSense.WaveSuspicion(0.45f, 3.6f, false, 9.5f, 10f), "weaker far out");
            Assert.AreEqual(0f, SeismicSense.WaveSuspicion(0.45f, 3.6f, false, 11f, 10f), "out of range");
        }

        [Test]
        public void TheFloorLidarSeesFeetOnTheFloorNotOnAChair()
        {
            var robot = Vector3.zero;
            var dir = Vector3.forward;
            var start = robot + Vector3.up * 0.22f;
            Assert.IsTrue(ScanLine.Crosses(start, dir, 6f, false, new Vector3(0.1f, 0f, 3f)) && !ScanLine.Above(0f, start.y), "standing on the floor in the line");
            Assert.IsTrue(ScanLine.Above(0.48f, start.y), "on a chair: above the line");
            Assert.IsTrue(ScanLine.Above(0.77f, start.y), "on a desk: above the line");
            Assert.IsFalse(ScanLine.Crosses(start, dir, 2.5f, false, new Vector3(0f, 0f, 3f)), "the line stops at the desk in between");
        }

        [Test]
        public void NewKindsAppearInTheHouseThatIntroducesThem()
        {
            var roster = new RosterSettings();
            Assert.IsEmpty(RobotRoster.Newcomers(roster.Robots, 0), "the first house has no showcase");
            var second = RobotRoster.Newcomers(roster.Robots, 1);
            CollectionAssert.Contains(second, RobotKind.Spider);
            CollectionAssert.Contains(second, RobotKind.Sweeper);
            for (int seed = 1; seed <= 20; seed++)
            {
                var l = LevelGenerator.Generate(seed, 1);
                foreach (var kind in second)
                    Assert.IsTrue(l.RobotSpawns.Exists(r => r.Kind == kind), $"seed {seed}: no {kind} in the house that introduces it");
            }
        }

        [Test]
        public void CargoPacksShapeAndColour()
        {
            for (byte shape = 0; shape < 8; shape++)
                for (byte color = 0; color < 6; color++)
                    Assert.AreEqual((shape, color), Detector.DecodeCargo(Detector.EncodeCargo(shape, color)));
        }
    }
}
