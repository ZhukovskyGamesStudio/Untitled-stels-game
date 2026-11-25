using NUnit.Framework;
using Stealth.Vehicles;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The pickup's driver side: the model is right-hand, a left-hand car is that model mirrored with the seats swapped.</summary>
    public class CarLookTests
    {
        [Test]
        public void ByDefaultTheSteeringWheelIsOnTheLeft()
        {
            Assert.IsTrue(new CarSettings().LeftHandDrive);
            Assert.IsTrue(CarLook.NeedsMirror(new CarSettings()), "the model itself is right-hand");
            // the headlights: full down the tunnel, dimmed before a wall so that it gets no more than the glare allows
            Assert.AreEqual(45f, CarLook.Headlight(45f, float.PositiveInfinity, 0.2f), "nothing ahead");
            Assert.AreEqual(45f, CarLook.Headlight(45f, 16f, 0.2f), "far enough: full");
            Assert.AreEqual(0.2f * 4f, CarLook.Headlight(45f, 2f, 0.2f), 1e-4f, "a gate 2 m ahead");
            Assert.AreEqual(0.2f, CarLook.Headlight(45f, 2f, 0.2f) / 4f, 1e-4f, "…gets the glare's worth, not eleven");
            Assert.AreEqual(0.2f, CarLook.Headlight(45f, 0.2f, 0.2f), 1e-4f, "nose to the wall: as at a metre");
            Assert.AreEqual(45f, CarLook.Headlight(45f, 2f, 0f), "no limit set");
            var car = new CarSettings();
            Assert.Less(CarLook.Headlight(45f, 2f, car.HeadlightGlare), 2f, "as set: a gate before the parked car is not burnt out");
            Assert.IsFalse(CarLook.NeedsMirror(new CarSettings { LeftHandDrive = false }));
        }

        [Test]
        public void TheModelIsRightHandAndTheDriverSitsBehindItsWheel()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Vehicles/Car.prefab");
            var car = prefab.GetComponent<Car>();
            Assert.IsNotNull(car.Model, "the model is referenced (it is what gets mirrored)");
            float wheel = car.transform.InverseTransformPoint(car.Steering.position).x;
            Assert.AreEqual(CarLook.ModelIsLeftHand, wheel < 0f, "the steering wheel is where CarLook.ModelIsLeftHand says");
            Assert.AreEqual(wheel, car.Seats[0].localPosition.x, 0.05f, "the driver's seat is behind it");
            Assert.AreEqual(-wheel, car.Seats[1].localPosition.x, 0.05f, "the passenger's beside");
        }

        [Test]
        public void TheSteeringWheelTurnsTheWayTheCarTurnsAndSlower()
        {
            // the top of the wheel's rim (perpendicular to the column, up): goes to the driver's right (+X) in a right turn
            var top = Vector3.Cross(CarLook.SteeringAxis, Vector3.right).normalized;
            if (top.y < 0f) top = -top;
            Assert.Greater((CarLook.SteeringWheel(20f, 4f) * top).x, 0.5f, "right turn: clockwise for the driver");
            Assert.Less((CarLook.SteeringWheel(-20f, 4f) * top).x, -0.5f, "left turn: anticlockwise");
            float full = Quaternion.Angle(Quaternion.identity, CarLook.SteeringWheel(new CarSettings().MaxSteer, new CarSettings().SteeringRatio));
            Assert.Less(full, 180f, "full lock is less than half a turn of the wheel");
        }

        [Test]
        public void MirroringMovesTheWheelToTheOtherSideAndKeepsRotationsProper()
        {
            var root = new GameObject("Model");
            var part = new GameObject("AccentSteering").transform;
            part.SetParent(root.transform, false);
            part.localPosition = new Vector3(0.42f, 1.12f, 0.45f);
            part.localRotation = Quaternion.Euler(-25f, 10f, 5f);
            var before = part.localToWorldMatrix;
            CarLook.Mirror(root.transform);
            var mirror = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
            var expected = mirror * before; // the part seen in a mirror
            var after = part.localToWorldMatrix;
            for (int i = 0; i < 16; i++) Assert.AreEqual(expected[i], after[i], 1e-4f, $"matrix element {i}");
            Assert.AreEqual(-0.42f, part.position.x, 1e-4f);
            Assert.AreEqual(1f, Quaternion.Dot(part.localRotation, part.localRotation), 1e-4f, "a unit quaternion");
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// The driver's hands follow the wheel up to a quarter turn, then it slides on in the palms (full lock is 136°: hands
        /// that followed crossed the arms); the points they hold lie on the rim of the wheel's own model.
        /// </summary>
        [Test]
        public void TheHandsHoldTheRimOfTheWheelsModel()
        {
            Assert.AreEqual(40f, CarLook.HandTurn(40f));
            Assert.AreEqual(CarLook.HandsFollow, CarLook.HandTurn(136f));
            Assert.AreEqual(-CarLook.HandsFollow, CarLook.HandTurn(-136f));
            Assert.Greater(CarLook.RimPoint(90f).x, 0.17f, "+90: the driver's right (the model's +X)");
            Assert.Greater(CarLook.RimPoint(0f).y, 0.15f, "0: the top");
            Assert.AreEqual(0f, Vector3.Dot(CarLook.RimPoint(37f), CarLook.SteeringAxis), 1e-5f, "across the column");

            var car = AssetDatabase.LoadAssetAtPath<Car>("Assets/_Game/Prefabs/Vehicles/Car.prefab");
            Assert.IsNotNull(car.Steering, "the car's steering wheel");
            var toWheel = car.Steering.worldToLocalMatrix;
            var rim = new System.Collections.Generic.List<Vector3>();
            foreach (var f in car.Steering.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = toWheel * f.transform.localToWorldMatrix;
                foreach (var v in f.sharedMesh.vertices) rim.Add(m.MultiplyPoint3x4(v));
            }
            Assert.IsNotEmpty(rim, "the wheel's mesh");
            for (float deg = -180f; deg < 180f; deg += 15f)
            {
                var p = CarLook.RimPoint(deg);
                float nearest = float.PositiveInfinity;
                foreach (var v in rim) nearest = Mathf.Min(nearest, (v - p).magnitude);
                // (the ring is a 2.2 cm tube of 20 sections: its vertices lie within a few centimetres of its centre line)
                Assert.Less(nearest, 0.045f, $"{deg}°: no rim there ({nearest * 100f:0.0} cm to the mesh)");
            }
        }

        /// <summary>
        /// Where a hand holds the wheel (<see cref="Car.WheelGrip"/>) is the same point of the rim as the turned model shows
        /// it, in the right-hand car and in the mirrored left-hand one; the hands at ten to two are either side of the hub.
        /// </summary>
        [Test]
        public void TheGripTurnsWithTheWheelAsItIsShown([Values(false, true)] bool leftHand)
        {
            var car = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Car>("Assets/_Game/Prefabs/Vehicles/Car.prefab"));
            try
            {
                car.ApplySides(new CarSettings { LeftHandDrive = leftHand });
                car.WheelGrip(0f, 0f, out var hub, out var column); // (reads the wheel's rest pose)
                var rest = car.Steering.localRotation;
                var left = car.WheelGrip(-CarLook.HandsAt, 0f, out _, out _);
                var right = car.WheelGrip(CarLook.HandsAt, 0f, out _, out _);
                Assert.AreEqual(CarLook.RimRadius, (left - hub).magnitude, 0.01f, "on the rim");
                Assert.Greater(Vector3.Dot(left - hub, Vector3.up), 0f, "ten to two: over the hub");
                Assert.Greater(Mathf.Abs(Vector3.Dot(left - right, car.transform.right)), 0.25f, "either side of the hub");
                Assert.Less(Vector3.Dot(column, car.transform.forward), -0.8f, "the column points back at the driver");
                foreach (float turn in new[] { -70f, 25f, 90f })
                {
                    car.Steering.localRotation = rest * CarLook.SteeringWheel(turn, 1f);
                    var shown = car.Steering.TransformPoint(CarLook.RimPoint(CarLook.HandsAt));
                    Assert.Less((car.WheelGrip(CarLook.HandsAt, turn, out _, out _) - shown).magnitude, 1e-3f, $"turned {turn}°: the hand holds the rim where it is");
                }
            }
            finally
            {
                Object.DestroyImmediate(car.gameObject);
            }
        }
    }
}
