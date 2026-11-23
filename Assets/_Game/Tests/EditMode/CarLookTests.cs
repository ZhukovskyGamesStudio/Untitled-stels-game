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
    }
}
