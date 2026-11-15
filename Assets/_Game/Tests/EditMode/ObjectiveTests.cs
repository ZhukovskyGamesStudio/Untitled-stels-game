using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The house's main job: a vault behind three lock panels, or a hostage in a cage whose key a guard carries.</summary>
    public class ObjectiveTests
    {
        static IEnumerable<LevelLayout> Houses() => TestHouses.Sample;

        [Test]
        public void BothKindsOfJobComeUp()
        {
            // the seed's choice alone (no house): about half vaults, half hostages, the house's number mixed in
            var s = new FeatureSettings();
            int vaults = 0, total = 0;
            for (int seed = 1; seed <= 400; seed++)
                for (int house = 0; house < 5; house++, total++)
                    if (HouseFeatures.VaultFirst(seed, house, s)) vaults++;
            Assert.That(vaults / (float)total, Is.InRange(0.4f, 0.6f), $"{vaults} vaults first of {total}");
            Assert.AreEqual(HouseFeatures.VaultFirst(42, 3, s), HouseFeatures.VaultFirst(42, 3, s), "the same seed, the same job");
        }

        [Test]
        public void NearlyEveryHouseHasAMainJob()
        {
            var houses = Houses().ToList();
            int jobs = houses.Count(l => l.Objective is ObjectiveKind.Vault or ObjectiveKind.Hostage);
            Assert.GreaterOrEqual(jobs, houses.Count - 1, $"{jobs} jobs in {houses.Count} houses");
            // the planned job is the seed's choice when it fits
            int asChosen = houses.Count(l => (l.Objective == ObjectiveKind.Vault) == HouseFeatures.VaultFirst(l.Seed, l.LevelIndex, new FeatureSettings()));
            Assert.GreaterOrEqual(asChosen, houses.Count / 2, "mostly the seed's choice");
        }

        [Test]
        public void TheVaultIsOneRoomBehindItsOwnDoorWithThreePanelsOutside()
        {
            int checkedVaults = 0;
            foreach (var l in Houses().Where(h => h.Objective == ObjectiveKind.Vault))
            {
                checkedVaults++;
                string at = $"seed {l.Seed} house {l.LevelIndex}";
                var plan = l.Plan;
                var all = HouseFeatures.Reach(plan, l.Stairs, l.LobbyRoom, true);
                var without = HouseFeatures.Reach(plan, l.Stairs, l.LobbyRoom, true, l.VaultDoorway);
                var behind = new HashSet<int>(all);
                behind.ExceptWith(without);
                CollectionAssert.AreEquivalent(new[] { l.VaultRoom }, behind, $"{at}: the door is the only way into the vault, and only into it");
                Assert.AreEqual(EdgeType.Door, plan.Openings[l.VaultDoorway].Kind, at);
                Assert.That(l.VaultDoor, Is.InRange(0, l.AutoDoors.Count - 1), $"{at}: the vault's door is a door");
                Assert.IsFalse(l.LockedDoors.Contains(l.VaultDoor), $"{at}: not a lock to hack: the panels open it");
                Assert.IsFalse(l.KeyDoors.Any(k => k.Door == l.VaultDoor || k.Rooms.Contains(l.VaultRoom)), $"{at}: no key door there");

                Assert.AreEqual(3, l.VaultConsoles.Count, at);
                var rooms = new HashSet<int>();
                foreach (var c in l.VaultConsoles)
                {
                    var into = c.position + c.rotation * Vector3.forward * 0.5f;
                    var room = plan.RoomAt(new Vector3(into.x, LevelLayout.FloorY(LevelLayout.FloorOf(c.position.y)) + 0.3f, into.z));
                    Assert.IsNotNull(room, $"{at}: a panel faces into a room");
                    Assert.AreNotEqual(l.VaultRoom, room.Id, $"{at}: the panels are outside the vault");
                    Assert.IsTrue(rooms.Add(room.Id), $"{at}: one panel per room");
                    var behindWall = c.position - c.rotation * Vector3.forward * 0.4f;
                    Assert.AreNotEqual(room.Id, plan.RoomAt(new Vector3(behindWall.x, LevelLayout.FloorY(room.Floor) + 0.3f, behindWall.z))?.Id,
                        $"{at}: the panel hangs on a wall (the other side is not its room)");
                }
                Assert.Greater(l.VaultTreasure.Count, 2, $"{at}: treasure lies in the vault");
                foreach (var t in l.VaultTreasure) Assert.AreEqual(l.VaultRoom, plan.RoomAt(t + Vector3.up * 0.3f)?.Id, at);
            }
            Assert.Greater(checkedVaults, 0);
        }

        [Test]
        public void InACalmHouseTheCageKeyLiesSomewhere()
        {
            var calm = new SecuritySettings { Robots = 0, RobotsPerHouse = 0, RobotsPerExtraFloor = 0, Devices = 0, DevicesPerHouse = 0, Cameras = 0, CamerasPerHouse = 0, Lasers = 0, LasersPerHouse = 0 };
            int found = 0;
            // one calm house (its own generation): the first seed whose choice is the hostage
            for (int seed = 1; seed < 40 && found < 1; seed++)
            {
                if (HouseFeatures.VaultFirst(seed, 2, new FeatureSettings())) continue;
                var l = LevelGenerator.Generate(seed, 2, null, calm);
                if (l.Objective != ObjectiveKind.Hostage) continue;
                found++;
                if (l.RobotSpawns.Count > 0) continue; // wardens still walk
                Assert.AreEqual(-1, l.KeyCarrier);
                Assert.IsNotNull(l.Plan.RoomAt(l.CageKeySpot + Vector3.up * 0.3f), "the key lies in a room");
            }
            Assert.Greater(found, 0);
        }

        [Test]
        public void TheHostagesCageStandsInARoomAndAGuardCarriesItsKey()
        {
            int checkedCages = 0;
            foreach (var l in Houses().Where(h => h.Objective == ObjectiveKind.Hostage))
            {
                checkedCages++;
                string at = $"seed {l.Seed} house {l.LevelIndex}";
                Assert.That(l.Cage, Is.InRange(0, l.Props.Count - 1), at);
                var cage = l.Props[l.Cage];
                Assert.AreEqual(PropKind.Cage, cage.Kind, at);
                var room = l.Plan.RoomAt(cage.Position + Vector3.up * 0.3f);
                Assert.IsNotNull(room, at);
                Assert.AreNotEqual(l.LobbyRoom, room.Id, $"{at}: not in the lobby");
                Assert.That(l.KeyCarrier, Is.InRange(0, l.RobotSpawns.Count - 1), at);
                bool guards = l.RobotSpawns.Any(r => r.Kind == RobotKind.Guard);
                Assert.IsTrue(!guards || l.RobotSpawns[l.KeyCarrier].Kind == RobotKind.Guard, $"{at}: a guard carries the key when there is one");
                Assert.IsFalse(RobotRoster.IsDevice(l.RobotSpawns[l.KeyCarrier].Kind), $"{at}: a robot that moves carries it");
                // nothing else stands in the cage
                foreach (var p in l.Props)
                    if (p.Kind != PropKind.Cage && p.Kind != PropKind.DeskLamp)
                    {
                        var d = p.Position - cage.Position;
                        Assert.IsFalse(Mathf.Abs(d.x) < 0.9f && Mathf.Abs(d.z) < 0.9f && LevelLayout.FloorOf(p.Position.y) == room.Floor,
                            $"{at}: a {p.Kind} stands in the cage");
                    }
            }
            Assert.Greater(checkedCages, 0);
        }
    }
}
