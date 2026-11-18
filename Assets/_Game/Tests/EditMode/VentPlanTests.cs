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
            Assert.LessOrEqual(LevelMetrics.FloorHeight / (2 * LevelMetrics.StepsPerFlight), 0.3f, "a stair step is walked up, with room under the player's 0.35 m");
            Assert.Greater(LevelMetrics.LandingEnd - 0.12f, 0.9f, "the landing is wide enough to turn on");
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

        [Test]
        public void SomeGratesHaveAWayUpButNeverTheFirstRoom()
        {
            int climbs = 0, grates = 0;
            foreach (var l in Houses())
            {
                Assert.AreEqual(-1, l.Vents[0].Grates[0].ClimbDir, $"seed {l.Seed}: a way up in the team's first room");
                foreach (var net in l.Vents)
                    foreach (var g in net.Grates)
                    {
                        // (a hospital's patient and treatment rooms give their walls to the beds and machines first: Furnisher.Climb)
                        if (g.Room >= 0 && l.Plan.Rooms[g.Room].Type is RoomType.Ward or RoomType.Operating or RoomType.Imaging or RoomType.NurseStation) continue;
                        grates++;
                        if (g.ClimbDir < 0) continue;
                        climbs++;
                        Assert.AreNotEqual(l.LobbyRoom, g.Room, "never in the lobby");
                        // the crates right under the grate, the desk beside them
                        var under = new Vector3(g.Position.x, LevelLayout.FloorY(net.Floor), g.Position.z);
                        var desk = under + VentPlan.ClimbSide(g.ClimbDir) * (VentPlan.StackHalf + VentPlan.DeskHalf + 0.02f);
                        bool stack = l.Props.Any(p => p.Kind == PropKind.CrateStack && (p.Position - under).sqrMagnitude < 0.01f);
                        bool table = l.Props.Any(p => p.Kind == PropKind.Desk && (p.Position - desk).sqrMagnitude < 0.01f);
                        if (stack) Assert.IsTrue(table, $"seed {l.Seed}: crates without their desk");
                    }
            }
            // (corridors, halls, the lobby, the exit and the robots' rooms never have one; a busy room may have no place for it)
            // (the whole-room walk check keeps a busy room's way up out more often than it used to: a twentieth at least)
            Assert.That(climbs, Is.InRange(grates / 20, grates * 3 / 4), $"{climbs} of {grates} grates have a way up");
        }

        [Test]
        public void ACrouchedJumpFromTheCratesGetsIntoTheDuct()
        {
            var grate = new Vector3(0f, LevelMetrics.WallHeight, 0f);
            float crates = 1.385f, jump = 1.05f;
            Assert.IsTrue(VentGrate.IntoDuct(new Vector3(0.1f, crates + jump, 0f), grate, true, true), "the top of the jump from the crates");
            Assert.IsFalse(VentGrate.IntoDuct(new Vector3(0.1f, crates + jump, 0f), grate, false, true), "standing: the head bumps");
            Assert.IsFalse(VentGrate.IntoDuct(new Vector3(0.1f, 0.92f + jump, 0f), grate, true, true), "from the desk: too low");
            Assert.IsFalse(VentGrate.IntoDuct(new Vector3(0.8f, crates + jump, 0f), grate, true, true), "beside the hole");
            Assert.IsFalse(VentGrate.IntoDuct(new Vector3(0f, crates + jump, 0f), grate, true, false), "falling back down");
        }

        [Test]
        public void TheStorageRobotWaddlesAndTiltsAsItClimbs()
        {
            var still = Stealth.Game.StorageBotAnimator.Pose(0f, 0f, 1f);
            Assert.AreEqual(0f, still.roll, 1e-4f);
            Assert.AreEqual(0f, still.bob, 1e-4f);
            var walk = Stealth.Game.StorageBotAnimator.Pose(2.5f, 0f, 1f);
            Assert.Greater(Mathf.Abs(walk.roll), 1f, "it sways walking");
            Assert.Less(Stealth.Game.StorageBotAnimator.Pose(0f, 1.8f, 0f).pitch, -30f, "nose up climbing up");
            Assert.Greater(Stealth.Game.StorageBotAnimator.Pose(0f, -1.8f, 0f).pitch, 30f, "nose down climbing down");
        }
    }
}
