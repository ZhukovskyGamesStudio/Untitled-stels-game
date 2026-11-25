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
    /// The hospital is a building of its own: a slab with a corridor down its length and rooms on both sides, the
    /// admissions with X-ray and CT on the ground floor, patient rooms and a nurse station upstairs, the theatres on top.
    /// </summary>
    public class HospitalTests
    {
        static readonly List<LevelLayout> s_Hospitals = new();

        static List<LevelLayout> Hospitals()
        {
            if (s_Hospitals.Count > 0) return s_Hospitals;
            s_Hospitals.AddRange(TestHouses.First(HouseKind.Hospital, 4, seed => 1 + seed % 4));
            return s_Hospitals;
        }

        [Test]
        public void TheHospitalIsASlabWithACorridorAndRoomsOnBothSides()
        {
            foreach (var l in Hospitals())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                Assert.AreEqual("hospital", l.Mass, name);
                Assert.That(l.Floors, Is.InRange(2, 3), name);
                for (int f = 0; f < l.Floors; f++)
                {
                    var corridor = l.Rooms.Where(r => r.Floor == f && r.Type == RoomType.Corridor).OrderByDescending(r => r.Area).FirstOrDefault();
                    Assert.IsNotNull(corridor, $"{name}: a corridor on floor {f}");
                    Assert.Greater(corridor.Bounds.width, l.Plan.SizeX - 1f, $"{name}: it runs the building's length on floor {f}");
                }
                Assert.Greater(l.Rooms.Count(r => r.Type == RoomType.Imaging && r.Floor == 0), 0, $"{name}: X-ray and CT on the ground floor");
                Assert.Greater(l.Rooms.Count(r => r.Type == RoomType.Infirmary && r.Floor == 0), 1, $"{name}: examination rooms in the admissions");
                for (int f = 1; f < l.Floors; f++)
                {
                    Assert.GreaterOrEqual(l.Rooms.Count(r => r.Type == RoomType.Ward && r.Floor == f), 6, $"{name}: patient rooms on floor {f}");
                    Assert.AreEqual(1, l.Rooms.Count(r => r.Type == RoomType.NurseStation && r.Floor == f), $"{name}: one nurse station on floor {f}");
                }
                Assert.AreEqual(2, l.Rooms.Count(r => r.Type == RoomType.Operating && r.Floor == l.Floors - 1), $"{name}: two theatres on the top floor");
            }
        }

        [Test]
        public void TheHospitalIsSmallerThanItsAreaButAsGuarded()
        {
            // the same hospitals built at full size: the slab 40% shorter, the robots as many
            int full = 0, small = 0;
            foreach (var l in Hospitals())
            {
                var whole = TestHouses.Make(l.Seed, l.LevelIndex, new BuildingSettings { HospitalSize = 1f }, null, 1);
                Assert.AreEqual(HouseKind.Hospital, whole.PlanKind);
                Assert.Less(l.SizeX * l.SizeZ, whole.SizeX * whole.SizeZ * 0.75f, $"seed {l.Seed}: smaller than the full size");
                full += whole.RobotSpawns.Count;
                small += l.RobotSpawns.Count;
            }
            Assert.That(Mathf.Abs(full - small), Is.LessThanOrEqualTo(Hospitals().Count), $"robots {small} against {full} at full size (one either way a house)");
        }

        [Test]
        public void PatientRoomsOpenOntoTheCorridorAndTheStationIsOpenToIt()
        {
            foreach (var l in Hospitals())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                foreach (var r in l.Rooms.Where(r => r.Type is RoomType.Ward or RoomType.Operating or RoomType.Imaging))
                {
                    var ways = l.Doorways.Where(d => d.RoomA == r.Id || d.RoomB == r.Id).ToList();
                    Assert.AreEqual(1, ways.Count, $"{name}: one way into the {r.Type} at {r.Center}");
                    var other = l.Plan.Rooms[ways[0].RoomA == r.Id ? ways[0].RoomB : ways[0].RoomA];
                    Assert.AreEqual(RoomType.Corridor, other.Type, $"{name}: the {r.Type} at {r.Center} opens onto the corridor");
                    Assert.AreEqual(EdgeType.Door, ways[0].Kind, $"{name}: through a door");
                }
                foreach (var r in l.Rooms.Where(r => r.Type == RoomType.NurseStation))
                    Assert.IsTrue(l.Doorways.Any(d => (d.RoomA == r.Id || d.RoomB == r.Id) && d.Kind == EdgeType.Open), $"{name}: the station is open to the corridor");
            }
        }

        [Test]
        public void TheRoomsAreFurnishedAsAHospital()
        {
            foreach (var l in Hospitals())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                IEnumerable<PropKind> In(PlanRoom r) => l.Props.Where(p => l.Plan.RoomAt(p.Position + Vector3.up * 0.3f) == r).Select(p => p.Kind);
                var wards = l.Rooms.Where(r => r.Type == RoomType.Ward).ToList();
                int withBeds = wards.Count(r => In(r).Contains(PropKind.HospitalBed) && In(r).Contains(PropKind.BedHeadwall));
                Assert.GreaterOrEqual(withBeds, wards.Count * 0.8f, $"{name}: beds with their headwalls in the patient rooms ({withBeds} of {wards.Count})");
                Assert.IsTrue(l.Props.Any(p => p.Kind == PropKind.BedCurtain), $"{name}: privacy curtains round the beds");
                foreach (var r in l.Rooms.Where(r => r.Type == RoomType.NurseStation))
                    Assert.Contains(PropKind.NurseStationDesk, In(r).ToList(), $"{name}: the station's counter");
                foreach (var r in l.Rooms.Where(r => r.Type == RoomType.Operating))
                {
                    var kinds = In(r).ToList();
                    Assert.Contains(PropKind.SurgicalTable, kinds, $"{name}: the theatre's table");
                    Assert.Contains(PropKind.SurgicalLights, kinds, $"{name}: its lamps");
                }
                // (walls 0.3 m thick and the rule that no piece cuts a room in two leave a narrow imaging room without its machine now and then)
                var imaging = l.Rooms.Where(r => r.Type == RoomType.Imaging).ToList();
                int scanners = imaging.Count(r => In(r).Any(k => k is PropKind.CtScanner or PropKind.XrayMachine));
                Assert.GreaterOrEqual(scanners, imaging.Count * 0.6f, $"{name}: scanners in the imaging rooms ({scanners} of {imaging.Count})");
                Assert.IsTrue(l.Props.Any(p => p.Kind == PropKind.WaitingSeats), $"{name}: waiting seats");
            }
        }

        [Test]
        public void TheHospitalIsDressedAsOneInEveryStyle()
        {
            var style = new StyleGeometry { Doors = DoorTrim.Arch, Crown = true, WainscotHeight = 1f };
            var clinic = VisualStyles.LookOf(Hospitals()[0]).Dress(style);
            Assert.AreEqual(DoorTrim.Casing, clinic.Doors, "plain frames");
            Assert.IsFalse(clinic.Crown);
            var ward = clinic.RoomLooks[clinic.RoomLookOf(RoomType.Ward)];
            Assert.AreEqual("Clinic_WardWall", ward.Wall.name, "warm paint in the wards");
            Assert.AreEqual("Clinic_Guard", ward.Lower.name, "a wall-protection band");
            Assert.AreEqual(1f, ward.LowerHeight, 1e-4f, "to a metre, with its bumper rail on top");
            Assert.AreEqual("Theatre_Wall", clinic.RoomLooks[clinic.RoomLookOf(RoomType.Operating)].Wall.name, "white panels in the theatres");
            Assert.AreEqual("Clinic_Floor", clinic.RoomFloors[clinic.RoomFloorOf(RoomType.Corridor)].Material.name, "sheet vinyl in the corridors");
            Assert.AreSame(style, VisualStyles.LookOf(TestHouses.Get(42, 2)).Dress(style), "an office keeps the style (one that is not made of scans)");
        }
    }
}
