using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The crawl ducts in the ceiling voids (VentPlan): a hidden way from the lobby to the exit room on every house, most
    /// rooms reached, the tunnels only where the void is (no stair hole, atrium, vault or storage room), a player fits and a
    /// robot does not.
    /// </summary>
    public class VentPlanTests
    {
        static IEnumerable<LevelLayout> Houses()
        {
            for (int k = 0; k < 16; k++) yield return LevelGenerator.Generate(7001 + k * 131, k % 5, null, null, 1 + k % 3);
        }

        [Test]
        public void APlayerFitsInADuctAndARobotDoesNot()
        {
            Assert.Greater(LevelMetrics.VoidHeight, Player.PlayerController.CrouchHeight + 0.03f, "a crouched player fits");
            Assert.Less(LevelMetrics.VoidHeight, 1.6f, "no robot fits (its navmesh agent is 2 m tall)");
            Assert.LessOrEqual(LevelMetrics.FloorHeight / (2 * LevelMetrics.StepsPerFlight), 0.35f, "a stair step is still a step for the player");
        }

        [Test]
        public void TheDuctsLeadFromTheLobbyToTheExitRoom()
        {
            foreach (var l in Houses())
            {
                Assert.AreEqual(l.Floors, l.Vents.Count);
                var net = l.Vents[0];
                int exit = l.Plan.RoomAt(l.ExitHatchPosition + Vector3.up * 0.3f).Id;
                // the lobby, or (an atrium lobby has no ceiling of its own) the room with a grate nearest it
                var lobby = l.Lobby;
                var from = net.Grates.Where(g => g.Room == l.LobbyRoom).ToList();
                if (from.Count == 0 && lobby.Levels > 1)
                    from = net.Grates.OrderBy(g => (l.Plan.Rooms[g.Room].Center - lobby.Center).sqrMagnitude).Take(1).ToList();
                var to = net.Grates.Where(g => g.Room == exit).ToList();
                Assert.AreEqual(1, from.Count, $"seed {l.Seed}: no grate in the lobby");
                Assert.AreEqual(1, to.Count, $"seed {l.Seed}: no grate in the exit room");
                Assert.IsNotNull(VentPlan.Path(net, from[0].Cell, to[0].Cell), $"seed {l.Seed}: no duct from the lobby to the exit room");
            }
        }

        [Test]
        public void MostRoomsHaveAGrateAndEveryGrateIsOnTheNetwork()
        {
            int rooms = 0, grated = 0;
            foreach (var l in Houses())
                foreach (var net in l.Vents)
                {
                    rooms += l.Plan.Rooms.Count(r => r.Floor == net.Floor && r.Levels == 1 && r.Area >= 12f && r.Type != RoomType.Corridor
                        && r.Id != l.VaultRoom && r.Id != l.StorageRoom);
                    grated += net.Grates.Count(g => l.Plan.Rooms[g.Room].Type != RoomType.Corridor && l.Plan.Rooms[g.Room].Area >= 12f);
                    if (net.Grates.Count == 0) continue;
                    var first = net.Grates[0].Cell;
                    foreach (var g in net.Grates)
                    {
                        Assert.IsNotNull(VentPlan.Path(net, first, g.Cell), $"seed {l.Seed} floor {net.Floor}: a grate off the network");
                        Assert.AreEqual(g.Room, l.Plan.RoomAt(new Vector3(g.Position.x, LevelLayout.FloorY(net.Floor) + 0.3f, g.Position.z))?.Id, "the grate over its room");
                    }
                }
            Assert.Greater(grated, rooms * 0.6f, $"{grated} of {rooms} rooms have a grate");
        }

        [Test]
        public void TheDuctsRunOnlyWhereThereIsAVoid()
        {
            foreach (var l in Houses())
                foreach (var net in l.Vents)
                    foreach (var c in net.Cells)
                    {
                        Assert.IsTrue(VentPlan.Allowed(l, net.Floor, c), $"seed {l.Seed} floor {net.Floor}: a duct at {c}");
                        foreach (var s in l.Stairs)
                            if (s.Floor == net.Floor)
                                Assert.IsFalse(PlanGenerator.StairArea(s, 0f).Overlaps(new Rect(c.x * VentPlan.Res, c.y * VentPlan.Res, VentPlan.Res, VentPlan.Res)), $"seed {l.Seed}: a duct through a stair hole");
                    }
        }

        [Test]
        public void TheCeilingFinishGoesRoundTheGrates()
        {
            var pieces = ArchitecturePlanner.CutHoles(Vector2.zero, new Vector2(4f, 4f), new List<Rect> { new Rect(1f, 1f, 1f, 1f) });
            float area = pieces.Sum(p => (p.max.x - p.min.x) * (p.max.y - p.min.y));
            Assert.AreEqual(15f, area, 1e-4f);
            foreach (var (a, b) in pieces) Assert.IsFalse(new Rect(a, b - a).Overlaps(new Rect(1.001f, 1.001f, 0.998f, 0.998f)), "a piece over the hole");
            Assert.AreEqual(1, ArchitecturePlanner.CutHoles(Vector2.zero, Vector2.one, new List<Rect> { new Rect(5f, 5f, 1f, 1f) }).Count, "a hole elsewhere leaves it whole");
        }
    }
}
