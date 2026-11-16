using System.Collections.Generic;
using NUnit.Framework;
using Stealth.Enemies;
using Stealth.Items;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The worker's lift: the tray swings down, scoops loot from the floor and brings it up; what it takes and where it stands.</summary>
    public class WorkerCargoTests
    {
        [Test]
        public void TheTrayRestsOverTheCrateAndLandsFlatOnTheFloorInFront()
        {
            var rest = CargoMotion.At(0f);
            Assert.AreEqual(0f, rest.Arm, 1e-4f);
            Assert.That(Vector3.Distance(rest.Tray, CargoMotion.PinRest), Is.LessThan(1e-4f));
            Assert.That(CargoMotion.PinRest.y - 0.015f, Is.GreaterThan(1.35f), "rests on top, over the crate (top at 1.35 m)");
            var down = CargoMotion.At((CargoMotion.DownAt + CargoMotion.UpFrom) / 2f);
            Assert.AreEqual(CargoMotion.ArmDown, down.Arm, 1e-3f);
            Assert.AreEqual(CargoMotion.PinOnFloor, down.Tray.y, 1e-3f, "the tray lies on the floor");
            Assert.AreEqual(CargoMotion.Reach, down.Tray.z, 1e-3f, "where the loot is set down");
            var end = CargoMotion.At(1f);
            Assert.AreEqual(0f, end.Arm, 1e-4f, "back at rest");
            Assert.AreEqual(0f, end.Bow, 1e-4f);
            Assert.AreEqual(0f, end.Dip, 1e-4f);
        }

        [Test]
        public void TheArmsKeepTheirLengthAndTheTrayNeverGoesUnderTheFloor()
        {
            for (int i = 0; i <= 50; i++)
            {
                var p = CargoMotion.At(i / 50f);
                Assert.AreEqual(CargoMotion.ArmLength, Vector3.Distance(p.Tray, CargoMotion.Hinge), 1e-3f);
                Assert.That(p.Tray.y, Is.GreaterThanOrEqualTo(CargoMotion.PinOnFloor - 1e-3f), $"t={i / 50f}");
                Assert.That(p.Arm, Is.InRange(-1e-3f, CargoMotion.ArmDown + 1e-3f));
            }
        }

        [Test]
        public void TheLootKeepsStillWhileTheTraySlidesUnderIt()
        {
            // picking up: the item lies StandOff ahead; the robot rolls Scoop forward while the tray slides under it
            Assert.AreEqual(CargoMotion.Reach + CargoMotion.Scoop, CargoMotion.StandOff(), 1e-5f);
            for (int i = 0; i <= 20; i++)
            {
                float t = Mathf.Lerp(CargoMotion.DownAt, CargoMotion.UpFrom, i / 20f);
                float rolled = CargoMotion.Slide(t) * CargoMotion.Scoop;
                float fromRobot = CargoMotion.StandOff() - rolled;                          // the item, still on the floor
                Assert.AreEqual(fromRobot, CargoMotion.Reach + CargoMotion.CargoAhead(t, SwingKind.Pickup), 1e-4f, "on the tray where it lay");
                // setting down: the robot rolls back, the loot stays where the tray put it
                Assert.AreEqual(CargoMotion.Reach + rolled, CargoMotion.Reach + CargoMotion.CargoAhead(t, SwingKind.PutDown), 1e-4f);
            }
            Assert.AreEqual(0f, CargoMotion.Slide(CargoMotion.DownAt), 1e-5f, "no rolling before the tray is down");
            Assert.AreEqual(1f, CargoMotion.Slide(CargoMotion.UpFrom), 1e-5f, "all rolled before it swings up");
            Assert.AreEqual(0f, CargoMotion.CargoAhead(CargoMotion.UpFrom, SwingKind.Pickup), 1e-5f, "loaded: in the middle of the tray");
            Assert.IsTrue(CargoMotion.Touched(CargoMotion.TouchAt - 0.01f, CargoMotion.TouchAt + 0.01f));
            Assert.IsFalse(CargoMotion.Touched(CargoMotion.TouchAt + 0.01f, CargoMotion.TouchAt + 0.02f));
        }

        [Test]
        public void ItShelvesOnTheBottomBoardWithoutHittingTheShelf()
        {
            var k = SwingKind.Shelve;
            float surface = CargoMotion.Surface(k);
            var on = CargoMotion.At((CargoMotion.DownAt + CargoMotion.UpFrom) / 2f, k);
            Assert.AreEqual(surface + CargoMotion.Clearance + CargoMotion.TrayHalf, on.Tray.y, 1e-3f, "the tray on the board");
            // it begins StandOff from the place for the item; the shelf's front is ShelfDepth nearer
            float shelfFront = CargoMotion.StandOff(k) - CargoMotion.ShelfDepth;
            for (int i = 0; i <= 40; i++)
            {
                float t = i / 40f;
                var p = CargoMotion.At(t, k);
                float front = p.Tray.z + 0.2f + CargoMotion.Rolled(t, k);                   // the tray's front edge, from where it began
                if (p.Tray.y > surface + 0.1f)
                    Assert.That(front, Is.LessThan(shelfFront), $"t={t}: swinging, the tray stays out of the shelf");
            }
            Assert.AreEqual(CargoMotion.ShelfScoop, CargoMotion.Rolled(CargoMotion.TouchAt, k), 1e-4f, "rolled all the way in when it leaves the item");
            Assert.AreEqual(0f, CargoMotion.Rolled(CargoMotion.UpFrom, k), 1e-4f, "and back out before it swings up");
            Assert.AreEqual(CargoMotion.StandOff(k) - CargoMotion.ShelfScoop, CargoMotion.ReachFor(surface), 1e-4f, "the item ends where it should lie");
            Assert.AreEqual(0.47f, CargoMotion.ShelfTop, 1e-5f, "the furnisher's lowest shelf level");
        }

        [Test]
        public void WorkersMoveLootButNeverTheTeamsTools()
        {
            for (byte s = 0; s < ItemCatalog.LootShapes; s++) Assert.IsTrue(LootErrands.CanTake(s), $"loot shape {s}");
            Assert.IsTrue(LootErrands.CanTake(ItemCatalog.Shovel));
            foreach (var tool in new[] { ItemCatalog.Key, ItemCatalog.Stun, ItemCatalog.Speaker, ItemCatalog.CageKey })
                Assert.IsFalse(LootErrands.CanTake(tool), $"tool {tool}");
        }

        [Test]
        public void ItTakesTheNearestLootOnItsOwnFloor()
        {
            var robot = new Vector3(0f, 3f, 0f);
            var items = new List<Vector3>
            {
                new(1f, 3f + 0.9f, 0f),   // on a desk: out of reach
                new(4f, 3.02f, 0f),
                new(0f, 0.02f, 1f),       // the floor below
                new(-2f, 3.02f, 1f),      // nearest on its floor
                new(9f, 3.02f, 0f),       // too far
            };
            Assert.AreEqual(3, LootErrands.Nearest(robot, items, 6f));
            Assert.AreEqual(-1, LootErrands.Nearest(robot, new List<Vector3> { new(9f, 3f, 0f) }, 6f));
        }

        [Test]
        public void ItStopsWithTheLootJustInFrontOfIt()
        {
            var robot = new Vector3(0f, 0f, 0f);
            var item = new Vector3(3f, 0.02f, 4f);
            var stop = LootErrands.Approach(robot, item);
            var flat = new Vector3(item.x - stop.x, 0f, item.z - stop.z);
            Assert.AreEqual(CargoMotion.StandOff(), flat.magnitude, 1e-4f, "the tray lands just behind it");
            Assert.AreEqual(robot.y, stop.y, 1e-5f, "stands on its own floor");
            Assert.That(Vector3.Dot(flat.normalized, new Vector3(3f, 0f, 4f).normalized), Is.GreaterThan(0.999f), "comes at it from where it is");
        }
    }
}
