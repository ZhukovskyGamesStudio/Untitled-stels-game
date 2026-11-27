using System.Collections.Generic;
using NUnit.Framework;
using Stealth.Enemies;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The roles and attacks of the danger zones' robots (<see cref="RoleRules"/>) and where each kind belongs (<see cref="RobotRoster"/>).</summary>
    public class RoleTests
    {
        [Test]
        public void AWardenShutsOpenDoorsAndSetsHackedLocksAgain()
        {
            Assert.AreEqual(DoorJob.Shut, RoleRules.Warden(open: true, byRobot: false, hasLock: false, lockHacked: false, special: false, locksLeft: 0));
            Assert.AreEqual(DoorJob.None, RoleRules.Warden(true, byRobot: true, false, false, false, 3), "a robot holds it open");
            Assert.AreEqual(DoorJob.Relock, RoleRules.Warden(false, false, hasLock: true, lockHacked: true, false, 1));
            Assert.AreEqual(DoorJob.None, RoleRules.Warden(false, false, true, true, false, locksLeft: 0), "out of locks");
            Assert.AreEqual(DoorJob.None, RoleRules.Warden(false, false, true, lockHacked: false, false, 3), "still locked");
            Assert.AreEqual(DoorJob.None, RoleRules.Warden(true, false, true, true, special: true, 3), "the vault is not its business");
        }

        [Test]
        public void TheNearestSpotOnTheFloorInReach()
        {
            var spots = new List<Vector3> { new(10f, 0f, 0f), new(3f, 0f, 0f), new(1f, 3.2f, 0f) };
            Assert.AreEqual(1, RoleRules.Nearest(Vector3.zero, spots, 8f), "the one upstairs does not count");
            Assert.AreEqual(-1, RoleRules.Nearest(Vector3.zero, spots, 2f));
        }

        [Test]
        public void AManiacTakesTheNextPlayerWhoIsUp()
        {
            Assert.AreEqual(2, RoleRules.NextPrey(new[] { true, false, true }, 1));
            Assert.AreEqual(0, RoleRules.NextPrey(new[] { true, false, false }, 1), "round to the first again");
            Assert.AreEqual(-1, RoleRules.NextPrey(new[] { false, false }, 0));
        }

        [Test]
        public void AttacksFromAfarStandAndSomeGoRoundCorners()
        {
            Assert.IsFalse(RoleRules.Ranged(AttackKind.Hit));
            Assert.IsFalse(RoleRules.Ranged(AttackKind.Sedate));
            Assert.IsTrue(RoleRules.Ranged(AttackKind.Shoot));
            Assert.IsTrue(RoleRules.Ranged(AttackKind.Net));
            Assert.IsTrue(RoleRules.NeedsLine(AttackKind.Shoot), "a shot does not pass a wall");
            Assert.IsFalse(RoleRules.NeedsLine(AttackKind.Shriek), "a shriek does");
            Assert.IsFalse(RoleRules.NeedsLine(AttackKind.Gas));
        }

        [Test]
        public void AShriekReachesItsFloorOnly()
        {
            Assert.IsTrue(RoleRules.Reaches(Vector3.zero, new Vector3(5f, 0.2f, 0f), 8f));
            Assert.IsFalse(RoleRules.Reaches(Vector3.zero, new Vector3(9f, 0f, 0f), 8f));
            Assert.IsFalse(RoleRules.Reaches(Vector3.zero, new Vector3(1f, 3.2f, 0f), 8f));
        }

        [Test]
        public void TheRoleWaitsItsPeriod()
        {
            Assert.IsFalse(RoleRules.Due(5f, 0f, 10f));
            Assert.IsTrue(RoleRules.Due(10f, 0f, 10f));
            Assert.IsFalse(RoleRules.Due(100f, 0f, 0f), "no period: never");
        }

        [Test]
        public void EveryKindHasItsZoneAndFaction()
        {
            foreach (var k in new[] { RobotKind.Worker, RobotKind.Warden, RobotKind.Repairman, RobotKind.Smoker, RobotKind.Sweeper, RobotKind.Listener, RobotKind.Lidar })
                Assert.AreEqual(DangerZone.Green, RobotRoster.ZoneOf(k), k.ToString());
            foreach (var k in new[] { RobotKind.Guard, RobotKind.Drone, RobotKind.Relay, RobotKind.Nurse, RobotKind.Mimic, RobotKind.Spider, RobotKind.Salesman })
                Assert.AreEqual(DangerZone.Yellow, RobotRoster.ZoneOf(k), k.ToString());
            foreach (var k in new[] { RobotKind.Gunner, RobotKind.Driller, RobotKind.Exterminator, RobotKind.Banshee, RobotKind.Maniac })
                Assert.AreEqual(DangerZone.Red, RobotRoster.ZoneOf(k), k.ToString());
            foreach (var k in new[] { RobotKind.Mimic, RobotKind.Spider, RobotKind.Salesman, RobotKind.Banshee, RobotKind.Maniac })
                Assert.AreEqual(Faction.Renegade, RobotRoster.FactionOf(k), k.ToString());
            foreach (var k in new[] { RobotKind.Guard, RobotKind.Gunner, RobotKind.Nurse, RobotKind.Warden, RobotKind.Driller, RobotKind.Exterminator })
                Assert.AreEqual(Faction.Servant, RobotRoster.FactionOf(k), k.ToString());
        }

        [Test]
        public void NursesWorkInHospitalsOnly()
        {
            var roster = new RosterSettings();
            Assert.Greater(RobotRoster.HouseScale(RobotKind.Nurse, HouseKind.Hospital, roster.ByHouse), 0f);
            foreach (HouseKind h in System.Enum.GetValues(typeof(HouseKind)))
                if (h != HouseKind.Hospital) Assert.AreEqual(0f, RobotRoster.HouseScale(RobotKind.Nurse, h, roster.ByHouse), h.ToString());
            var rng = new System.Random(3);
            for (int i = 0; i < 400; i++)
                Assert.AreNotEqual(RobotKind.Nurse, RobotRoster.PickRobot(roster.Robots, 9, rng, DangerZone.Red, HouseKind.Office, roster));
            bool nurse = false;
            for (int i = 0; i < 400 && !nurse; i++) nurse = RobotRoster.PickRobot(roster.Robots, 9, rng, DangerZone.Yellow, HouseKind.Hospital, roster) == RobotKind.Nurse;
            Assert.IsTrue(nurse, "a hospital has its nurses");
        }

        [Test]
        public void TheArmedGuardComesOnlyByTheClock()
        {
            Assert.AreEqual(RobotKind.Gunner, RobotRoster.ArmedGuard);
            var table = RobotRoster.DefaultRobots();
            for (int i = 0; i < 300; i++)
                Assert.AreNotEqual(RobotKind.Gunner, RobotRoster.PickReinforcement(table, 9, new System.Random(i), DangerZone.Red));
        }

        [Test]
        public void CarriersTakeTheirBodiesToTheirOwnPlaces()
        {
            Assert.AreEqual(CaptiveKind.Prison, HaulRules.PlaceOf(RobotKind.Guard));
            Assert.AreEqual(CaptiveKind.Prison, HaulRules.PlaceOf(RobotKind.Gunner));
            Assert.AreEqual(CaptiveKind.Ward, HaulRules.PlaceOf(RobotKind.Nurse));
            Assert.AreEqual(CaptiveKind.Lair, HaulRules.PlaceOf(RobotKind.Maniac));
        }

        [Test]
        public void AHospitalHasBedsForTheNurse()
        {
            var l = TestHouses.OfKind(HouseKind.Hospital, 3);
            Assert.IsTrue(HaulRules.NearestBed(l, l.Lobby.Center).HasValue, "a bed to put the sleeper in");
        }

        [Test]
        public void ARelayCallsWhenSomeoneIsNearButNotLooking()
        {
            Assert.IsTrue(LureRelay.Calls(nearest: 12f, earshot: 22f, tooClose: 5f));
            Assert.IsFalse(LureRelay.Calls(3f, 22f, 5f), "they would see who calls");
            Assert.IsFalse(LureRelay.Calls(40f, 22f, 5f), "nobody to hear it");
        }

        [Test]
        public void APatchGrowsInAndShrinksAway()
        {
            Assert.AreEqual(0f, HazardView.Share(0f, 10f));
            Assert.AreEqual(1f, HazardView.Share(5f, 5f));
            Assert.Less(HazardView.Share(9.8f, 0.2f), 0.5f);
        }
    }
}
