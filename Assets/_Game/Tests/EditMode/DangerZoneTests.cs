using System.Linq;
using NUnit.Framework;
using Stealth.Level;

namespace Stealth.Tests
{
    /// <summary>The danger zones of a house (<see cref="DangerZones"/>): the way out, who stands where, where the traps are.</summary>
    public class DangerZoneTests
    {
        const int Houses = 24;

        [Test]
        public void TheWayOutNeverCrossesARedRoom()
        {
            foreach (var l in TestHouses.Corpus(Houses))
            {
                Assert.IsNotNull(l.Zones, $"seed {l.Seed}: zones");
                Assert.AreEqual(l.Plan.Rooms.Count, l.Zones.Length);
                Assert.AreEqual(DangerZone.Green, l.ZoneOf(l.EntryRoom), $"seed {l.Seed}: the team arrives in a green room");
                Assert.AreNotEqual(DangerZone.Red, l.ZoneOf(l.ExitRoom), $"seed {l.Seed}: the exit is not red");
                Assert.IsTrue(DangerZones.WayOutAvoidsRed(l), $"seed {l.Seed} house {l.LevelIndex}: the exit is reached without a red room");
            }
        }

        [Test]
        public void EveryKindOfHouseHasAWayOutPastTheRedRooms()
        {
            foreach (var (kind, level, l) in TestHouses.Kinds(3))
                Assert.IsTrue(DangerZones.WayOutAvoidsRed(l), $"{kind} at house {level}");
        }

        [Test]
        public void HousesHaveAllThreeZones()
        {
            int withRed = 0, withYellow = 0;
            var houses = TestHouses.Corpus(Houses);
            foreach (var l in houses)
            {
                if (l.Zones.Contains(DangerZone.Red)) withRed++;
                if (l.Zones.Contains(DangerZone.Yellow)) withYellow++;
                foreach (var r in l.Plan.Rooms)
                    if (RoomTraits.Circulation(r.Type)) Assert.AreNotEqual(DangerZone.Red, l.ZoneOf(r.Id), $"seed {l.Seed}: a corridor is never red");
            }
            Assert.GreaterOrEqual(withRed, houses.Count * 3 / 4, "most houses have red rooms");
            Assert.AreEqual(houses.Count, withYellow, "every house has yellow rooms");
        }

        [Test]
        public void TheMainJobIsNeverInAGreenRoom()
        {
            foreach (var l in TestHouses.Corpus(Houses))
                if (l.VaultRoom >= 0) Assert.AreNotEqual(DangerZone.Green, l.ZoneOf(l.VaultRoom), $"seed {l.Seed}");
        }

        [Test]
        public void ZonesAreTheSameForTheSameSeed()
        {
            var (seed, level, players) = TestHouses.CorpusKey(4);
            var a = TestHouses.Make(seed, level, players: players);
            var b = LevelGenerator.Generate(seed, level, players: players);
            CollectionAssert.AreEqual(a.Zones, b.Zones);
        }

        [Test]
        public void FightersDoNotStartInGreenRooms()
        {
            int staff = 0, fighters = 0;
            foreach (var l in TestHouses.Corpus(Houses))
                foreach (var r in l.RobotSpawns)
                {
                    if (RobotRoster.IsDevice(r.Kind) || r.Docked) continue; // (docked guards rest in the charging room, wherever it is)
                    var zone = l.ZoneAt(r.Position);
                    var own = RobotRoster.ZoneOf(r.Kind);
                    if (zone != DangerZone.Green) continue;
                    if (own == DangerZone.Green) staff++;
                    else fighters++;
                }
            Assert.Greater(staff, 0, "the green rooms have their staff");
            // (a kind a house introduces is shown even where no room is dangerous enough: next to none)
            Assert.LessOrEqual(fighters, staff / 10, $"robots of the yellow and red zones in green rooms: {fighters} against {staff} of the staff");
        }

        [Test]
        public void TrapsKeepOutOfGreenRooms()
        {
            int green = 0, other = 0;
            float greenArea = 0f, otherArea = 0f;
            foreach (var l in TestHouses.Corpus(Houses))
            {
                foreach (var r in l.Plan.Rooms)
                    if (l.ZoneOf(r.Id) == DangerZone.Green) greenArea += r.Area;
                    else otherArea += r.Area;
                foreach (var r in l.RobotSpawns)
                {
                    if (!RobotRoster.IsDevice(r.Kind)) continue;
                    if (l.ZoneAt(r.Position) == DangerZone.Green) green++;
                    else other++;
                }
                foreach (var c in l.CameraSpots)
                    if (l.ZoneAt(c.position - UnityEngine.Vector3.up * 2f) == DangerZone.Green) green++;
                    else other++;
            }
            Assert.Greater(other, 0);
            // per square metre a green room has well under half the devices of the rest of the house
            Assert.Less(green / greenArea, 0.5f * other / otherArea, $"devices: {green} on {greenArea:0} m² green, {other} on {otherArea:0} m² elsewhere");
        }

        [Test]
        public void PatrolsKeepToTheirZone()
        {
            Assert.IsTrue(DangerZones.Patrols(DangerZone.Green, DangerZone.Green));
            Assert.IsTrue(DangerZones.Patrols(DangerZone.Green, DangerZone.Red));
            Assert.IsFalse(DangerZones.Patrols(DangerZone.Yellow, DangerZone.Green));
            Assert.IsTrue(DangerZones.Patrols(DangerZone.Yellow, DangerZone.Red));
            Assert.IsFalse(DangerZones.Patrols(DangerZone.Red, DangerZone.Yellow));
        }

        [Test]
        public void AKindLivesOnlyInItsHouse()
        {
            var byHouse = new[] { new HouseRoster(RobotKind.Sorter, HouseKind.Hospital, 3f, true) };
            Assert.AreEqual(3f, RobotRoster.HouseScale(RobotKind.Sorter, HouseKind.Hospital, byHouse));
            Assert.AreEqual(0f, RobotRoster.HouseScale(RobotKind.Sorter, HouseKind.Garage, byHouse));
            Assert.AreEqual(1f, RobotRoster.HouseScale(RobotKind.Guard, HouseKind.Garage, byHouse));
        }

        [Test]
        public void ARoomGetsRobotsOfItsZoneOrCalmer()
        {
            var table = RobotRoster.DefaultRobots();
            var rng = new System.Random(5);
            for (int i = 0; i < 300; i++)
            {
                Assert.AreEqual(DangerZone.Green, RobotRoster.ZoneOf(RobotRoster.PickRobot(table, 5, rng, DangerZone.Green, HouseKind.Office, null)));
                Assert.AreNotEqual(DangerZone.Red, RobotRoster.ZoneOf(RobotRoster.PickRobot(table, 5, rng, DangerZone.Yellow, HouseKind.Office, null)));
            }
        }
    }
}
