using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The auto repair shop is a building of its own: a double-height workshop hall on the street with a bay for every
    /// sectional door, the reception beside it, the paint booth behind, a gallery over the hall upstairs.
    /// </summary>
    public class GarageTests
    {
        static readonly List<LevelLayout> s_Garages = new();

        /// <summary>A few garages from different runs and houses (the kind comes from the seed).</summary>
        static List<LevelLayout> Garages()
        {
            if (s_Garages.Count > 0) return s_Garages;
            for (int seed = 1; s_Garages.Count < 5 && seed < 400; seed++)
            {
                int house = 1 + seed % 4;
                if (HouseKinds.Of(seed, house) != HouseKind.Garage) continue;
                s_Garages.Add(TestHouses.Get(seed, house));
            }
            return s_Garages;
        }

        [Test]
        public void TheShopIsAHallOnTheStreetWithTheReceptionBesideIt()
        {
            Assert.AreEqual(HouseKind.Garage, TestHouses.Get(7, 1).Kind, "one of the shared test houses is a shop");
            foreach (var l in Garages())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                Assert.AreEqual("garage", l.Mass, name);
                Assert.AreEqual(2, l.Floors, $"{name}: the hall and its gallery");
                var halls = l.Rooms.Where(r => r.Type == RoomType.Workshop).ToList();
                Assert.AreEqual(1, halls.Count, $"{name}: one workshop hall");
                var hall = halls[0];
                Assert.AreEqual(0, hall.Floor, name);
                Assert.AreEqual(2, hall.Levels, $"{name}: two storeys high");
                Assert.Less(hall.Bounds.yMin, 0.6f, $"{name}: the hall is on the street");
                Assert.GreaterOrEqual(hall.Bounds.width, 3 * PlanGenerator.BayWidth, $"{name}: room for three bays at least");
                Assert.Less(l.Lobby.Bounds.yMin, 0.6f, $"{name}: the reception is on the street too");
                Assert.AreEqual(1, l.Rooms.Count(r => r.Type == RoomType.PaintBooth), $"{name}: a paint booth");
                // upstairs the gallery looks down into the hall over a railing
                bool railing = l.Plan.Walls.Any(w => w.Railing && w.Floor == 1); // (the void's side of a railing is no room: −1)
                Assert.IsTrue(railing, $"{name}: a gallery over the hall");
                Assert.IsTrue(l.Rooms.Any(r => r.Floor == 1 && r.Type == RoomType.Corridor), $"{name}: the gallery is a walkway");
            }
        }

        [Test]
        public void TheBaysHaveCarsLiftsAndDoors()
        {
            var cars = new HashSet<PropKind> { PropKind.LiftCar, PropKind.LiftEmpty, PropKind.CarParked, PropKind.CarHoodOpen, PropKind.CarOnStands };
            var gear = new HashSet<PropKind> { PropKind.ToolChest, PropKind.TireRack, PropKind.TireChanger, PropKind.WheelBalancer, PropKind.AirCompressor,
                PropKind.OilDrums, PropKind.WeldingCart, PropKind.EngineHoist, PropKind.WorkTable, PropKind.HoseReel, PropKind.MetalShelving };
            foreach (var l in Garages())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                var hall = l.Rooms.First(r => r.Type == RoomType.Workshop);
                bool In(PropPlacement p) => l.Plan.RoomAt(p.Position + Vector3.up * 0.5f) == hall;
                int inBays = l.Props.Count(p => cars.Contains(p.Kind) && In(p));
                Assert.GreaterOrEqual(inBays, 2, $"{name}: cars and lifts in the bays ({inBays})");
                Assert.GreaterOrEqual(l.Props.Count(p => p.Kind == PropKind.GarageDoor), 2, $"{name}: sectional doors on the street wall");
                Assert.GreaterOrEqual(l.Props.Count(p => p.Kind == PropKind.BayLines), 2, $"{name}: the bays' lines on the floor");
                Assert.GreaterOrEqual(l.Props.Count(p => gear.Contains(p.Kind) && In(p)), 3, $"{name}: the shop's equipment along the walls");
                // nose in: the cars face away from the doors (into the hall, +z)
                foreach (var p in l.Props.Where(p => cars.Contains(p.Kind) && In(p)))
                    Assert.AreEqual(0f, Mathf.DeltaAngle(p.RotationY, 0f), 1f, $"{name}: {p.Kind} at {p.Position} faces into the hall, nose in");
                var booth = l.Rooms.First(r => r.Type == RoomType.PaintBooth);
                Assert.IsTrue(l.Props.Any(p => p.Kind == PropKind.CarParked && l.Plan.RoomAt(p.Position + Vector3.up * 0.5f) == booth), $"{name}: a car in the paint booth");
            }
        }

        [Test]
        public void TheCarIsDrivenIntoThePaintBoothThroughItsWideDoor()
        {
            foreach (var l in Garages())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                var booth = l.Rooms.First(r => r.Type == RoomType.PaintBooth);
                var doors = l.Doorways.Where(d => d.RoomA == booth.Id || d.RoomB == booth.Id).ToList();
                Assert.AreEqual(1, doors.Count, $"{name}: a booth is sealed but for its door");
                Assert.That(l.BoothDoorway, Is.GreaterThanOrEqualTo(0), $"{name}: the booth's doorway is the vehicle door");
                var d = l.Doorways[l.BoothDoorway];
                Assert.AreEqual(LevelMetrics.VehicleDoorWidth, d.Width, 1e-4f, $"{name}: wide enough for a car");
                Assert.Greater(LevelMetrics.ClearWidth(d.Width), Furnisher.FootprintOf(PropKind.CarParked).width + 0.8f, $"{name}: with room to spare");
                var other = l.Plan.Rooms[d.RoomA == booth.Id ? d.RoomB : d.RoomA];
                Assert.AreEqual(RoomType.Workshop, other.Type, $"{name}: it opens onto the workshop");
                Assert.That(l.BoothDoor, Is.GreaterThanOrEqualTo(0), $"{name}: a big sliding door in it");
                Assert.Less((l.AutoDoors[l.BoothDoor].position - d.Position).magnitude, 0.05f, name);
                Assert.IsFalse(l.LockedDoors.Contains(l.BoothDoor), $"{name}: never locked");
            }
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            Assert.IsNotNull(art.VehicleDoor, "the vehicle door prefab");
            Assert.AreEqual(LevelMetrics.VehicleDoorWidth, art.VehicleDoor.GetComponent<AutoDoor>().Width, 1e-4f);
            Assert.IsNotNull(art.ShopDoor, "the shop's steel door");
        }

        [Test]
        public void TheShopIsDressedAsAWorkshopInEveryStyle()
        {
            var style = new StyleGeometry { Doors = DoorTrim.Arch, Crown = true, WainscotHeight = 1f, RailHeight = 0.9f };
            var shop = VisualStyles.LookOf(TestHouses.Get(7, 1)).Dress(style);
            Assert.AreNotSame(style, shop, "a copy: the style itself stays as it is");
            Assert.AreEqual(DoorTrim.Arch, style.Doors);
            Assert.AreEqual(DoorTrim.Casing, shop.Doors, "plain steel frames, no arches or pilasters");
            Assert.IsFalse(shop.Crown);
            Assert.AreEqual(0f, shop.WainscotHeight);
            Assert.AreEqual(0f, shop.RailHeight);
            Assert.IsTrue(shop.Pipes, "services in the open");
            var hall = shop.RoomLooks[shop.RoomLookOf(RoomType.Workshop)];
            Assert.AreEqual("Shop_Wall", hall.Wall.name, "painted block");
            Assert.AreEqual("Shop_WallLower", hall.Lower.name, "a dark band low");
            Assert.AreEqual("Shop_Ceiling", hall.Ceiling.name, "a metal deck overhead");
            Assert.AreEqual("Shop_Floor", shop.RoomFloors[shop.RoomFloorOf(RoomType.Workshop)].Material.name, "an epoxy floor");
            Assert.AreEqual("Booth_Wall", shop.RoomLooks[shop.RoomLookOf(RoomType.PaintBooth)].Wall.name, "filter panels in the booth");
            Assert.AreEqual("Booth_Floor", shop.RoomFloors[shop.RoomFloorOf(RoomType.PaintBooth)].Material.name, "a grating underfoot");
            Assert.AreSame(style, VisualStyles.LookOf(TestHouses.Get(42, 2)).Dress(style), "an office keeps the style (one that is not made of scans)");
        }

        [Test]
        public void OfficesKeepTheirOwnPlan()
        {
            var office = TestHouses.Get(42, 2);
            Assert.AreNotEqual(HouseKind.Garage, office.Kind);
            Assert.IsFalse(office.Rooms.Any(r => r.Type is RoomType.Workshop or RoomType.PaintBooth), "the shop's rooms stay in the shop");
            Assert.AreNotEqual("garage", office.Mass);
        }

        /// <summary>
        /// The checks while furnishing let in only what the whole-house pass keeps. 31676/0 and 31680/4 have the hostage's
        /// cage in the middle of an office: the room's check started its flood under the cage, dropped every doorway and
        /// let anything in, and the final pass took out 8 and 12 groups (bays' gear across the wall, the storage room).
        /// </summary>
        [Test]
        public void TheFinalWalkabilityCheckHasLittleToTakeOut()
        {
            var keys = Garages().Select(l => (l.Seed, l.LevelIndex, l.Players)).ToList();
            keys.Add((31676, 0, 2));
            keys.Add((31680, 4, 2));
            int removed = 0;
            var notes = new List<string>();
            foreach (var (seed, house, players) in keys)
            {
                var l = TestHouses.Get(seed, house, players);
                Assert.AreEqual(HouseKind.Garage, l.Kind, $"{seed}/{house}");
                int cut = TestHouses.Removed(seed, house, players);
                removed += cut;
                if (cut > 0) notes.Add($"{seed}/{house}: {cut}");
            }
            Assert.That(removed, Is.LessThanOrEqualTo(keys.Count / 2), $"groups taken out: {string.Join(", ", notes)}");
        }
    }
}
