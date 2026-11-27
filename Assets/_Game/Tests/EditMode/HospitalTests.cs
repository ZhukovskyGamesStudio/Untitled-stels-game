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
    /// The hospital is a building of blocks (PlanGenerator.Blocks): wings round courtyards, corridors that turn and cross,
    /// the admissions with X-ray and CT on the ground floor, patient rooms and a nurse station upstairs, the theatres on top.
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
        public void TheHospitalHasItsDepartmentsOnCorridorsThatTurn()
        {
            int stations = 0, wardFloors = 0, pairs = 0, houses = 0;
            foreach (var l in Hospitals())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                Assert.IsTrue(new[] { "figure8", "comb", "deep", "pinwheel", "campus", "cross", "stagger" }.Contains(l.Mass), $"{name}: {l.Mass}");
                Assert.That(l.Floors, Is.InRange(3, 4), name);
                for (int f = 0; f < l.Floors; f++)
                {
                    var corridor = l.Rooms.Where(r => r.Floor == f && r.Type == RoomType.Corridor).OrderByDescending(r => r.Area).FirstOrDefault();
                    Assert.IsNotNull(corridor, $"{name}: a corridor on floor {f}");
                    Assert.Greater(Mathf.Min(corridor.Bounds.width, corridor.Bounds.height), 8f, $"{name}: it turns (it spreads both ways) on floor {f}");
                }
                Assert.Greater(l.Rooms.Count(r => r.Type == RoomType.Imaging && r.Floor == 0), 0, $"{name}: X-ray and CT on the ground floor");
                Assert.Greater(l.Rooms.Count(r => r.Type == RoomType.Infirmary && r.Floor == 0), 1, $"{name}: examination rooms in the admissions");
                for (int f = 1; f < l.Floors; f++)
                {
                    Assert.GreaterOrEqual(l.Rooms.Count(r => r.Type == RoomType.Ward && r.Floor == f), 4, $"{name}: patient rooms on floor {f}");
                    Assert.LessOrEqual(l.Rooms.Count(r => r.Type == RoomType.NurseStation && r.Floor == f), 1, $"{name}: one nurse station at most on floor {f}");
                    stations += l.Rooms.Count(r => r.Type == RoomType.NurseStation && r.Floor == f); wardFloors++;
                }
                // a theatre's one door must not shut a room in: now and then only one fits
                int theatres = l.Rooms.Count(r => r.Type == RoomType.Operating && r.Floor == l.Floors - 1);
                Assert.That(theatres, Is.InRange(1, 2), $"{name}: theatres on the top floor");
                houses++;
                if (theatres == 2) pairs++;
            }
            Assert.GreaterOrEqual(pairs, houses * 0.7f, $"two theatres on the top floor: {pairs} of {houses}");
            Assert.GreaterOrEqual(stations, wardFloors * 0.7f, $"nurse stations on the ward floors: {stations} of {wardFloors}");
        }

        [Test]
        public void PatientRoomsOpenOntoTheCorridorAndTheStationIsOpenToIt()
        {
            int second = 0, all = 0, stations = 0, open = 0;
            foreach (var l in Hospitals())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                foreach (var r in l.Rooms.Where(r => r.Type is RoomType.Ward or RoomType.Operating or RoomType.Imaging))
                {
                    all++;
                    var ways = l.Doorways.Where(d => d.RoomA == r.Id || d.RoomB == r.Id).ToList();
                    // (a building of blocks gives a one-door room a second door where a room behind it would be shut in)
                    if (ways.Count > 1) { second++; continue; }
                    var other = l.Plan.Rooms[ways[0].RoomA == r.Id ? ways[0].RoomB : ways[0].RoomA];
                    if (other.Type != RoomType.Corridor) { second++; continue; }
                    Assert.AreEqual(EdgeType.Door, ways[0].Kind, $"{name}: through a door");
                }
                foreach (var r in l.Rooms.Where(r => r.Type == RoomType.NurseStation))
                {
                    stations++;
                    if (l.Doorways.Any(d => (d.RoomA == r.Id || d.RoomB == r.Id) && d.Kind == EdgeType.Open)) open++;
                }
            }
            Assert.GreaterOrEqual(open, stations * 0.7f, $"stations open to their corridor: {open} of {stations}");
            Assert.LessOrEqual(second, all / 3, $"one-door rooms with a second door or off the corridor: {second} of {all}");
        }

        [Test]
        public void TheRoomsAreFurnishedAsAHospital()
        {
            int scanners = 0, rooms = 0, counters = 0, withCounter = 0, theatres = 0, tables = 0;
            foreach (var l in Hospitals())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                IEnumerable<PropKind> In(PlanRoom r) => l.Props.Where(p => l.Plan.RoomAt(p.Position + Vector3.up * 0.3f) == r).Select(p => p.Kind);
                var wards = l.Rooms.Where(r => r.Type == RoomType.Ward).ToList();
                int withBeds = wards.Count(r => In(r).Contains(PropKind.HospitalBed) && In(r).Contains(PropKind.BedHeadwall));
                Assert.GreaterOrEqual(withBeds, wards.Count * 0.5f, $"{name}: beds with their headwalls in the patient rooms ({withBeds} of {wards.Count})");
                Assert.IsTrue(l.Props.Any(p => p.Kind == PropKind.BedCurtain), $"{name}: privacy curtains round the beds");
                foreach (var r in l.Rooms.Where(r => r.Type == RoomType.NurseStation)) { counters++; if (In(r).Contains(PropKind.NurseStationDesk)) withCounter++; }
                foreach (var r in l.Rooms.Where(r => r.Type == RoomType.Operating))
                {
                    var kinds = In(r).ToList();
                    theatres++;
                    if (kinds.Contains(PropKind.SurgicalTable) && kinds.Contains(PropKind.SurgicalLights)) tables++;
                }
                // (walls 0.3 m thick and the rule that no piece cuts a room in two leave a narrow imaging room without its machine now and then)
                var imaging = l.Rooms.Where(r => r.Type == RoomType.Imaging).ToList();
                rooms += imaging.Count;
                scanners += imaging.Count(r => In(r).Any(k => k is PropKind.CtScanner or PropKind.XrayMachine));
                Assert.IsTrue(l.Props.Any(p => p.Kind == PropKind.WaitingSeats), $"{name}: waiting seats");
            }
            Assert.GreaterOrEqual(tables, theatres * 0.7f, $"theatres with their table and lamps: {tables} of {theatres}");
            Assert.GreaterOrEqual(withCounter, counters * 0.7f, $"the stations' counters: {withCounter} of {counters}");
            Assert.GreaterOrEqual(scanners, rooms * 0.6f, $"scanners in the imaging rooms ({scanners} of {rooms})");
        }

        /// <summary>
        /// A hospital shares the office's room types but not its rooms: the lab is a clinical one (no electronics bench, no
        /// robot, no kitchen fridge), one a floor; no meeting rooms; its offices are consulting rooms, the records and
        /// on-call rooms — not one desk room repeated.
        /// </summary>
        [Test]
        public void TheLabsAndOfficesAreTheHospitalsOwn()
        {
            int labs = 0, benched = 0, consulting = 0, withDesk = 0;
            var practices = new HashSet<Practice>();
            foreach (var l in Hospitals())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                List<PropKind> In(PlanRoom r) => l.Props.Where(p => l.Plan.RoomAt(p.Position + Vector3.up * 0.3f) == r).Select(p => p.Kind).ToList();
                Assert.AreEqual(0, l.Rooms.Count(r => r.Type == RoomType.Meeting), $"{name}: meeting rooms");
                for (int f = 0; f < l.Floors; f++)
                    Assert.LessOrEqual(l.Rooms.Count(r => r.Type == RoomType.Lab && r.Floor == f), 1, $"{name}: labs on floor {f}");
                foreach (var p in l.Props)
                    Assert.IsFalse(p.Kind is PropKind.LabBench or PropKind.RobotTable or PropKind.RobotArm or PropKind.ScopeCart or PropKind.ExecutiveDesk or PropKind.ConferenceTable,
                        $"{name}: {p.Kind} at {p.Position}");
                foreach (var r in l.Rooms.Where(r => r.Type == RoomType.Lab))
                {
                    var kinds = In(r);
                    labs++;
                    if (kinds.Contains(PropKind.ClinicBench)) benched++;
                    Assert.IsFalse(kinds.Contains(PropKind.Fridge), $"{name}: the kitchen's fridge in the lab {r.Id}");
                }
                foreach (var r in l.Rooms.Where(r => r.Type == RoomType.Office && r.Area >= 10f && r.Id != l.VaultRoom))
                {
                    var practice = Furnisher.PracticeOf(l.Seed, r.Id);
                    practices.Add(practice);
                    if (practice != Practice.Consulting) continue;
                    consulting++;
                    if (In(r).Contains(PropKind.DoctorDesk)) withDesk++;
                }
            }
            Assert.Greater(labs, 0, "labs");
            Assert.GreaterOrEqual(benched, labs * 0.9f, $"labs with a clinical bench: {benched} of {labs}");
            Assert.AreEqual(3, practices.Count, "every practice among the offices");
            Assert.GreaterOrEqual(withDesk, consulting * 0.8f, $"consulting rooms with the doctor's desk: {withDesk} of {consulting}");
        }

        /// <summary>The lobby's rows of waiting seats in the middle stand back to back, not seat into seat.</summary>
        [Test]
        public void NoOfficeDeskWithItsPcStandsInAMedicalRoom()
        {
            // the play's note: three desks with PCs in an X-ray room, PCs in the treatment rooms — a room's computer is the
            // workstation on wheels, one a room, as the doctor's desk is
            var medical = new[] { RoomType.Imaging, RoomType.Infirmary, RoomType.Ward, RoomType.Operating, RoomType.NurseStation, RoomType.Lab, RoomType.Office };
            var office = new[] { PropKind.Desk, PropKind.StandingDesk, PropKind.ExecutiveDesk, PropKind.SupervisorDesk };
            int carts = 0;
            foreach (var l in TestHouses.First(HouseKind.Hospital, 6, seed => 1 + seed % 4))
            {
                var inRoom = new Dictionary<int, List<PropKind>>();
                foreach (var p in l.Props)
                {
                    var room = l.Plan.RoomAt(p.Position + Vector3.up * 0.3f);
                    if (room == null || System.Array.IndexOf(medical, room.Type) < 0) continue;
                    if (!inRoom.TryGetValue(room.Id, out var list)) inRoom[room.Id] = list = new List<PropKind>();
                    // (the desk under a high vent or a ceiling grate is a way up, not the room's desk)
                    if (p.Kind == PropKind.DoctorDesk && l.IsClimbPiece(p.Position)) continue;
                    if (p.Kind == PropKind.DoctorDesk && l.Doorways.Any(d => d.High && new Vector2(d.Position.x - p.Position.x, d.Position.z - p.Position.z).magnitude < 0.9f
                                                                             && LevelLayout.FloorOf(d.Position.y) == LevelLayout.FloorOf(p.Position.y + 0.1f))) continue;
                    list.Add(p.Kind);
                }
                foreach (var (id, kinds) in inRoom)
                {
                    var room = l.Plan.Rooms[id];
                    foreach (var k in office) Assert.IsFalse(kinds.Contains(k), $"seed {l.Seed}: {k} in {room.Type} {id}");
                    int cap = room.Area > 70f ? 2 : 1; // (a hall of a room may have two of a kind: Furnisher's cap)
                    Assert.LessOrEqual(kinds.Count(k => k == PropKind.DoctorDesk), cap, $"seed {l.Seed}: doctor's desks in {room.Type} {id}");
                    Assert.LessOrEqual(kinds.Count(k => k == PropKind.MedWorkstation), cap, $"seed {l.Seed}: workstations in {room.Type} {id}");
                    carts += kinds.Count(k => k == PropKind.MedWorkstation);
                }
            }
            Assert.Greater(carts, 6, "the hospital's own computer hardly ever stands anywhere");
        }

        [Test]
        public void TheXraysColumnStandsClearOfTheLamps()
        {
            // the model: the part declared as reaching the ceiling is where the prefab's tall parts are
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Props/XrayMachine.prefab");
            var part = Furnisher.ToCeiling[PropKind.XrayMachine];
            int tall = 0;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                var to = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (var v in mesh.vertices)
                {
                    var p = to.MultiplyPoint3x4(v);
                    if (p.y < 2.6f) continue; // (the detector's wall stand is 2.2 m: well under a lamp)
                    tall++;
                    Assert.IsTrue(part.Contains(new Vector2(p.x, p.z)), $"{mf.name}: a point {p} above 2.6 m outside the part that reaches the ceiling");
                }
            }
            Assert.Greater(tall, 8, "the X-ray machine has no column to the ceiling");
            // the houses: no lamp over it
            int machines = 0;
            foreach (var l in TestHouses.First(HouseKind.Hospital, 8, seed => 1 + seed % 4))
                foreach (var p in l.Props)
                {
                    if (p.Kind != PropKind.XrayMachine) continue;
                    machines++;
                    var room = l.Plan.RoomAt(p.Position + Vector3.up * 0.3f);
                    var column = Furnisher.RectAt(part, p.Position, p.RotationY);
                    foreach (var lamp in LevelBuilder.LightSpots(l, room))
                        Assert.IsFalse(column.Overlaps(Rect.MinMaxRect(lamp.x - VentPlan.LampHalf, lamp.y - VentPlan.LampHalf, lamp.x + VentPlan.LampHalf, lamp.y + VentPlan.LampHalf)),
                            $"seed {l.Seed}: the X-ray's column in room {room.Id} runs up into a lamp");
                }
            Assert.Greater(machines, 2, "hardly an X-ray machine in eight hospitals");
        }

        [Test]
        public void TheMedicalTableStandsWherePatientsAreTreated()
        {
            // the play's note: the healing station stood in a staff lounge
            int houses = 0, treated = 0;
            foreach (var l in TestHouses.First(HouseKind.Hospital, 10, seed => 1 + seed % 4))
            {
                if (l.PointOfInterest < 0) continue;
                houses++;
                var p = l.Props[l.PointOfInterest];
                Assert.AreEqual(PropKind.MedTable, p.Kind);
                var room = l.Plan.RoomAt(p.Position + Vector3.up * 0.3f);
                if (room != null && HouseFeatures.Treatment(l, room))
                {
                    treated++;
                    Assert.AreEqual(1f, p.Scale > 0f ? p.Scale : 1f, 1e-3f, $"seed {l.Seed}: the table in a treatment room is not whole");
                }
            }
            Assert.Greater(houses, 7);
            Assert.GreaterOrEqual(treated, houses * 8 / 10, $"the medical table in a treatment room in {treated} of {houses} hospitals");
        }

        [Test]
        public void WaitingSeatsInTheMiddleStandBackToBack()
        {
            int pairs = 0;
            foreach (var l in Hospitals())
            {
                var seats = l.Props.Where(p => p.Kind == PropKind.WaitingSeats).ToList();
                for (int i = 0; i < seats.Count; i++)
                    for (int j = i + 1; j < seats.Count; j++)
                    {
                        var d = seats[j].Position - seats[i].Position;
                        if (Mathf.Abs(d.y) > 0.5f || d.magnitude > 0.75f) continue;
                        // the model's front is its +z in the game: each looks away from the other
                        var front = Quaternion.Euler(0f, seats[i].RotationY, 0f) * Vector3.forward;
                        Assert.Less(Vector3.Dot(front, d.normalized), -0.9f, $"{l.Seed}/{l.LevelIndex}: seats at {seats[i].Position} face the row behind them");
                        pairs++;
                    }
            }
            Assert.Greater(pairs, 0, "rows back to back in the corpus");
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
