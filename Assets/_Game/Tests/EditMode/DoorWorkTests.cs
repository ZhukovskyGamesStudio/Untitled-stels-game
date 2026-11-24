using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Enemies;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Doors opened by hand, robots at them and in doorways, and the two kinds of locks.</summary>
    public class DoorWorkTests
    {
        static readonly DoorSettings S = new();

        static float Until(DoorState door, DoorInput at, bool open)
        {
            for (float t = 0f; t < 10f; t += 0.02f)
            {
                door.Step(at, 0.02f, S);
                if (door.Open == open) return t;
            }
            return -1f;
        }

        [Test]
        public void APlayerOpensAndShutsADoorAndItStaysAsLeft()
        {
            var door = new DoorState();
            Assert.IsTrue(door.Toggle(false));
            Assert.IsTrue(door.Open);
            Assert.AreEqual(-1f, Until(door, default, false), "a player's door does not shut by itself");
            Assert.AreEqual(-1f, Until(door, new DoorInput { RobotNear = true }, false), "nor because of a robot that passed");
            Assert.IsFalse(door.Toggle(true), "not on somebody in the doorway");
            Assert.IsTrue(door.Open);
            Assert.IsTrue(door.Toggle(false));
            Assert.IsFalse(door.Open);
        }

        [Test]
        public void AShutDoorDelaysARobotAndShutsBehindIt()
        {
            var door = new DoorState();
            Assert.AreEqual(-1f, Until(door, new DoorInput { RobotNear = true }, true), "a robot passing by does not open it");
            float opened = Until(door, new DoorInput { RobotWaits = true, RobotNear = true }, true);
            Assert.AreEqual(S.RobotOpenTime, opened, 0.1f, "delayed, not stopped");
            Assert.IsTrue(door.ByRobot);
            Assert.AreEqual(-1f, Until(door, new DoorInput { Occupied = true }, false), "not on somebody in the doorway");
            Assert.AreEqual(S.RobotShutDelay, Until(door, default, false), 0.1f, "shuts behind the robot");
        }

        [Test]
        public void AnInterruptedRobotStartsAnewAndAPlayerTakesADoorOver()
        {
            var door = new DoorState();
            door.Step(new DoorInput { RobotWaits = true }, S.RobotOpenTime * 0.9f, S);
            door.Step(default, 0.02f, S);
            Assert.AreEqual(0f, door.Work, "it went away: nothing is kept");
            Until(door, new DoorInput { RobotWaits = true }, true);
            door.Toggle(false); // the player shuts it in its face
            Assert.IsFalse(door.Open);
            door.Toggle(false); // and opens it again: now it is the player's
            Assert.AreEqual(-1f, Until(door, default, false));
            // one that locks again by itself (the storage room's) shuts even if a player had opened it
            Assert.GreaterOrEqual(Until(door, new DoorInput { Locked = true }, false), 0f);
        }

        [Test]
        public void APathIsSeenToGoThroughADoorway()
        {
            var center = new Vector3(10f, 0f, 5f);
            Vector3[] path = { new(10.2f, 0f, 2f), new(10.1f, 0f, 4f), new(9.8f, 0f, 8f) };
            Assert.AreEqual(3f, DoorwayMath.Crossing(center, Vector3.forward, 1.6f, path, 3, 5f, out int way), 0.1f);
            Assert.AreEqual(1, way);
            Assert.AreEqual(-1f, DoorwayMath.Crossing(center, Vector3.forward, 1.6f, path, 3, 2f, out _), "farther than it looks");
            Assert.AreEqual(-1f, DoorwayMath.Crossing(center, Vector3.forward, 1.6f, path, 2, 5f, out _), "it stops before it");
            Vector3[] beside = { new(13f, 0f, 2f), new(13f, 0f, 8f) };
            Assert.AreEqual(-1f, DoorwayMath.Crossing(center, Vector3.forward, 1.6f, beside, 2, 9f, out _), "past the wall beside it");
            Vector3[] above = { new(10f, 4.6f, 2f), new(10f, 4.6f, 8f) };
            Assert.AreEqual(-1f, DoorwayMath.Crossing(center, Vector3.forward, 1.6f, above, 2, 9f, out _), "a floor above");
            DoorwayMath.Crossing(center, Vector3.forward, 1.6f, path.Reverse().ToArray(), 3, 9f, out way);
            Assert.AreEqual(-1, way);
        }

        [Test]
        public void RobotsStandClearOfTheLeafAndAside()
        {
            Assert.Greater(DoorwayMath.StandOff(0.45f, 0.95f, true), 0.95f + 0.45f, "a leaf swinging towards it clears it");
            Assert.Less(DoorwayMath.StandOff(0.45f, 0f, false), 1.1f);
            var spot = DoorwayMath.WaitSpot(Vector3.zero, Vector3.forward, new Vector3(0.2f, 0f, 1f), 2.1f);
            Assert.Greater(spot.z, 1f, "back on its own side");
            Assert.Greater(spot.x, 1f, "and off the way through");
        }

        [Test]
        public void RobotsMeetingInADoorwayGoOneAfterTheOther()
        {
            var claims = new DoorwayClaims();
            Assert.IsTrue(claims.Ask(3, 1, 1, 0f, S));
            Assert.IsFalse(claims.Ask(3, 2, -1, 0.1f, S), "somebody comes the other way");
            Assert.IsTrue(claims.Ask(3, 4, 1, 0.1f, S), "one behind the first follows");
            Assert.IsTrue(claims.Ask(7, 2, -1, 0.1f, S), "another doorway is another matter");
            Assert.IsTrue(claims.Ask(3, 1, 1, 0.3f, S));
            Assert.IsFalse(claims.Ask(3, 2, -1, 0.4f, S));
            Assert.IsTrue(claims.Ask(3, 2, -1, 2f, S), "through: the other's turn");
            Assert.IsFalse(claims.Ask(3, 1, 1, 2.1f, S), "and now they wait");
            // a robot that stopped in the doorway does not hold it for ever
            for (float t = 2f; t < 2f + S.ClaimTime; t += 0.2f) claims.Ask(3, 2, -1, t, S);
            Assert.IsTrue(claims.Ask(3, 1, 1, 2f + S.ClaimTime + 0.1f, S));
        }

        [Test]
        public void AStuckRobotIsNoticed()
        {
            var meter = new StuckMeter();
            int stuck = 0;
            for (float t = 0f; t < S.StuckTime * 1.5f; t += 0.05f) if (meter.Step(new Vector3(Mathf.Sin(t * 20f) * 0.05f, 0f, 0f), true, 0.05f, S.StuckTime)) stuck++;
            Assert.AreEqual(1, stuck, "jittering on the spot is not walking");
            meter = new StuckMeter();
            for (float t = 0f; t < 10f; t += 0.05f) Assert.IsFalse(meter.Step(new Vector3(t, 0f, 0f), true, 0.05f, S.StuckTime), "walking");
            for (float t = 0f; t < 10f; t += 0.05f) Assert.IsFalse(meter.Step(Vector3.zero, false, 0.05f, S.StuckTime), "standing on purpose");
        }

        [Test]
        public void NobodyStopsInADoorwayOnPurpose()
        {
            var doorways = new List<Doorway> { new() { Position = new Vector3(4f, 0f, 4f) } };
            Assert.IsFalse(RobotController.ClearOfDoorways(doorways, new Vector3(4.5f, 0f, 4.5f)));
            Assert.IsTrue(RobotController.ClearOfDoorways(doorways, new Vector3(6f, 0f, 4.5f)));
            Assert.IsTrue(RobotController.ClearOfDoorways(doorways, new Vector3(4.5f, 4.6f, 4.5f)), "another floor's");
        }

        [Test]
        public void PeoplesDoorsArePickedAndTheRobotsLocksHacked()
        {
            Assert.AreEqual(LockKind.Mechanical, DoorRules.LockOf(HouseKind.Apartments, DoorKind.Entry));
            Assert.AreEqual(LockKind.Mechanical, DoorRules.LockOf(HouseKind.Apartments, DoorKind.Wood));
            Assert.AreEqual(LockKind.Electronic, DoorRules.LockOf(HouseKind.Apartments, DoorKind.Storage));
            foreach (HouseKind house in System.Enum.GetValues(typeof(HouseKind)))
                foreach (DoorKind door in System.Enum.GetValues(typeof(DoorKind)))
                    if (house != HouseKind.Apartments) Assert.AreEqual(LockKind.Electronic, DoorRules.LockOf(house, door), $"{house} {door}");
        }

        static readonly string[] Doors = { "AutoDoor", "ShopDoor", "VehicleDoor", "ClinicDoor", "ClinicSwingDoor", "WoodDoor", "WoodDoubleDoor", "EntranceDoor", "FlatDoor", "NarrowDoor" };

        [Test]
        public void EveryDoorHasItsHandleAndBothLocks([ValueSource(nameof(Doors))] string name)
        {
            var door = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Level/{name}.prefab").GetComponent<AutoDoor>();
            Assert.AreSame(door, door.GetComponent<DoorHandle>().Door);
            Assert.AreEqual(2, door.Padlocks.Length);
            Assert.AreEqual(2, door.LockUnits.Length);
            Assert.AreEqual(2, door.UnitLamps.Length);
            foreach (var unit in door.LockUnits)
            {
                Assert.IsFalse(unit.activeSelf, "shown only on a door with that lock");
                // on a leaf: it goes where the leaf goes; proud of it, not sunk in
                Assert.IsTrue(unit.transform.IsChildOf(door.Swing != null ? door.Swing : door.PanelA), name);
                var facing = door.transform.InverseTransformDirection(unit.transform.forward).z;
                Assert.Greater(Mathf.Abs(facing), 0.99f, $"{name}: looks out of its face");
                var at = door.transform.InverseTransformPoint(unit.transform.position);
                Assert.That(at.y, Is.InRange(0.8f, 1.5f), name);
                // its bolt (the model's −X) reaches towards the leaf's free edge: +X of the door
                Assert.Greater(Vector3.Dot(unit.transform.TransformVector(Vector3.left), door.transform.right), 0.5f, $"{name}: the bolt towards the seam");
            }
        }

        [Test]
        public void TheVaultIsShutToRobots()
        {
            var door = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Level/VaultDoor.prefab").GetComponent<AutoDoor>();
            Assert.IsNotNull(door.Barrier);
            Assert.IsTrue(door.Barrier.carving);
            Assert.Greater(door.Barrier.size.x, door.Width, "the whole doorway");
            Assert.IsNull(door.GetComponent<DoorHandle>(), "opened by its wheel");
        }

        [Test]
        public void NoRobotStartsShutInTheVaultOrTheHiddenRoom()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                var l = LevelGenerator.Generate(seed, 4);
                foreach (var r in l.RobotSpawns)
                {
                    if (RobotRoster.IsDevice(r.Kind)) continue;
                    int room = l.RoomAt(r.Position + Vector3.up * 0.3f)?.Id ?? -1;
                    if (room < 0) continue;
                    Assert.AreNotEqual(l.VaultRoom, room, $"seed {seed}: {r.Kind} in the vault");
                    Assert.AreNotEqual(l.HiddenRoom, room, $"seed {seed}: {r.Kind} in the hidden room");
                }
            }
        }

        [Test]
        public void AFlyingRobotIsAtTheDoorToo()
        {
            // drones hover over 2 m up: measured at the floor under them they are at the door like anyone
            var drone = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Robots/RobotDrone.prefab").GetComponent<RobotController>();
            Assert.Greater(drone.Traits.HoverHeight, 1.6f, "what made doors miss it");
            var go = Object.Instantiate(drone.gameObject, new Vector3(5f, drone.Traits.HoverHeight, 5f), Quaternion.identity);
            try { Assert.IsTrue(AutoDoor.ShouldOpen(new Vector3(5.5f, 0f, 5f), new[] { go.GetComponent<RobotController>().Feet })); }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
