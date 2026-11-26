using NUnit.Framework;
using Stealth.Level;

namespace Stealth.Tests
{
    /// <summary>The locations' rules (<see cref="HouseRules"/>): one per location, asked by the plan the house was built by.</summary>
    public class HouseRulesTests
    {
        [Test]
        public void EveryOfficeCharacterHasTheOfficesRules()
        {
            foreach (var k in new[] { HouseKind.Office, HouseKind.Business, HouseKind.DataCenter, HouseKind.Warehouse, HouseKind.Headquarters })
            {
                Assert.AreSame(HouseRules.Office, HouseRules.For(k), k.ToString());
                Assert.IsFalse(HouseKinds.OwnPlan(k), k.ToString());
            }
            Assert.AreSame(HouseRules.Hospital, HouseRules.For(HouseKind.Hospital));
            Assert.AreSame(HouseRules.Garage, HouseRules.For(HouseKind.Garage));
            Assert.AreSame(HouseRules.Apartments, HouseRules.For(HouseKind.Apartments));
        }

        [Test]
        public void EachLocationHasItsOwnUniqueFeature()
        {
            Assert.AreEqual(PropKind.Programmer, HouseFeatures.PointOfInterestKind(Location.Office));
            Assert.AreEqual(PropKind.MedTable, HouseFeatures.PointOfInterestKind(Location.Hospital));
            Assert.AreEqual(PropKind.Workbench, HouseFeatures.PointOfInterestKind(Location.Garage));
            Assert.IsNull(HouseFeatures.PointOfInterestKind(Location.Apartments), "the block's find is the shelter map");
            Assert.IsTrue(HouseRules.Apartments.HasShelterMap);
            foreach (var r in new[] { HouseRules.Office, HouseRules.Hospital, HouseRules.Garage }) Assert.IsFalse(r.HasShelterMap);
        }

        [Test]
        public void ALocationShapesItsOwnPlanButNotTheRoomsTest()
        {
            var b = new BuildingSettings();
            Assert.IsTrue(HouseKinds.Apply(HouseKind.Garage, b).Garage);
            Assert.IsTrue(HouseKinds.Apply(HouseKind.Hospital, b).Hospital);
            Assert.IsTrue(HouseKinds.Apply(HouseKind.Apartments, b).Apartments);
            var every = new BuildingSettings { EveryRoomType = true };
            foreach (var k in new[] { HouseKind.Garage, HouseKind.Hospital, HouseKind.Apartments })
            {
                var s = HouseKinds.Apply(k, every);
                Assert.IsFalse(s.Garage || s.Hospital || s.Apartments, k + ": the rooms test keeps the office's plan");
            }
            Assert.IsFalse(HouseRules.Apartments.RetypesRooms, "every room of the block is its flat's");
            Assert.IsTrue(HouseRules.Office.RetypesRooms);
        }

        [Test]
        public void StoragePartIsOneNumber()
        {
            for (int prop = 0; prop < 300; prop += 7)
                for (int part = 0; part < OpenKeys.StorageParts; part++)
                    Assert.AreEqual((prop, part), OpenKeys.StoragePartOf(OpenKeys.StoragePart(prop, part)));
        }
    }
}
