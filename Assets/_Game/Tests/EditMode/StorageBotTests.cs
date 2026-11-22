using System.Linq;
using NUnit.Framework;
using Stealth.Game;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The team's storage robot: its stash is in reach next to it (not while it crawls away), it comes when called, it is
    /// sent to the exit only once the exit room is known, and its way there runs through the ducts — up through a grate,
    /// along the duct's floor, down into the exit room.
    /// </summary>
    public class StorageBotTests
    {
        [Test]
        public void ItsStashIsInReachNextToItWhileItIsInTheHouse()
        {
            var bot = new Vector3(0f, 0f, 0f);
            Assert.IsTrue(StorageBotRules.InReach(BotMode.Waiting, bot, new Vector3(1.5f, 0f, 0f)));
            Assert.IsFalse(StorageBotRules.InReach(BotMode.Waiting, bot, new Vector3(3f, 0f, 0f)), "too far");
            Assert.IsFalse(StorageBotRules.InReach(BotMode.Waiting, bot, new Vector3(0.5f, 4.1f, 0f)), "the floor above");
            Assert.IsFalse(StorageBotRules.InReach(BotMode.InDucts, bot, new Vector3(0.5f, 0f, 0f)), "crawling to the exit");
            Assert.IsFalse(StorageBotRules.InReach(BotMode.Gone, bot, new Vector3(0.5f, 0f, 0f)));
        }

        [Test]
        public void ItIsSentToTheExitOnlyOnceTheExitRoomIsKnown()
        {
            Assert.IsTrue(StorageBotRules.CanCall(BotMode.Waiting));
            Assert.IsFalse(StorageBotRules.CanCall(BotMode.ToExit), "on its way out");
            Assert.IsFalse(StorageBotRules.CanSendToExit(BotMode.Waiting, false), "the exit room not seen yet");
            Assert.IsTrue(StorageBotRules.CanSendToExit(BotMode.Coming, true));
            Assert.IsFalse(StorageBotRules.CanSendToExit(BotMode.ToGrate, true), "sent already");
        }

        [Test]
        public void ItsWayToTheExitRunsThroughTheDucts()
        {
            foreach (var l in TestHouses.Sample)
            {
                var net = l.Vents[0];
                int exit = l.Plan.RoomAt(l.ExitHatchPosition + Vector3.up * 0.3f).Id;
                var to = net.Grates.Find(g => g.Room == exit);
                Assert.AreEqual(exit, to.Room, $"seed {l.Seed}: no grate in the exit room");
                // from any grate of the ground floor
                foreach (var from in net.Grates)
                {
                    var route = StorageBotRules.DuctRoute(net, from.Cell, to.Cell);
                    Assert.IsNotNull(route, $"seed {l.Seed}: no duct from room {from.Room} to the exit room");
                    Assert.AreEqual(0f, route[0].y, 1e-4f, "starts on the floor under the grate");
                    Assert.AreEqual(0f, route[route.Count - 1].y, 1e-4f, "ends on the exit room's floor");
                    for (int i = 1; i < route.Count - 1; i++)
                        Assert.AreEqual(LevelMetrics.WallHeight + LevelMetrics.CeilingBoard + 0.02f, route[i].y, 1e-4f, "on the duct's floor");
                }
            }
        }

        [Test]
        public void ItMovesAlongItsRouteAtItsPace()
        {
            var route = new System.Collections.Generic.List<Vector3> { new(0f, 0f, 0f), new(0f, 2f, 0f), new(3f, 2f, 0f) };
            int at = 0;
            var p = StorageBotRules.Advance(route, ref at, Vector3.zero, 1f);
            Assert.AreEqual(new Vector3(0f, 1f, 0f), p);
            p = StorageBotRules.Advance(route, ref at, p, 2f);
            Assert.AreEqual(new Vector3(1f, 2f, 0f), p);
            p = StorageBotRules.Advance(route, ref at, p, 10f);
            Assert.AreEqual(route[2], p);
            Assert.AreEqual(route.Count, at, "the end of the route");
        }

        [Test]
        public void WaitingUpInTheDuctItsStashIsPassedUpThroughTheGrate()
        {
            var bot = new Vector3(0f, StorageBotRules.DuctFloor(0), 0f);
            Assert.IsTrue(StorageBotRules.InReach(BotMode.WaitingAbove, bot, new Vector3(0.6f, 0f, 0.3f)), "right under the grate");
            Assert.IsFalse(StorageBotRules.InReach(BotMode.WaitingAbove, bot, new Vector3(2.5f, 0f, 0f)), "across the room");
            Assert.IsFalse(StorageBotRules.InReach(BotMode.WaitingAbove, bot, new Vector3(0f, 4.1f, 0f)), "from the floor above");
        }

        [Test]
        public void ItComesDownOnlyIntoASafeRoomAndRobotsThatSeeItRaiseTheAlarm()
        {
            Assert.IsTrue(StorageBotRules.Safe(3, new[] { 1, 2 }, false));
            Assert.IsFalse(StorageBotRules.Safe(3, new[] { 1, 3 }, false), "a watcher in the room");
            Assert.IsFalse(StorageBotRules.Safe(3, new int[0], true), "an alarm going");
            var eye = new Vector3(0f, 1.5f, 0f);
            Assert.IsTrue(StorageBotRules.Sees(eye, Vector3.forward, new Vector3(0f, 0.3f, 6f), true));
            Assert.IsFalse(StorageBotRules.Sees(eye, Vector3.forward, new Vector3(0f, 0.3f, 6f), false), "a wall between");
            Assert.IsFalse(StorageBotRules.Sees(eye, Vector3.forward, new Vector3(0f, 0.3f, -6f), true), "behind its back");
            Assert.IsFalse(StorageBotRules.Sees(eye, Vector3.forward, new Vector3(0f, 0.3f, 20f), true), "too far");
        }

        [Test]
        public void CalledItHeadsForTheGrateNearestThePlayer()
        {
            var l = TestHouses.Get(1, 0);
            var net = l.Vents[0];
            foreach (var g in net.Grates)
            {
                var near = StorageBotRules.NearestGrate(net, g.Position + new Vector3(0.2f, -2f, 0.1f));
                Assert.AreEqual(g.Cell, near.Value.Cell, "under a grate: that one");
            }
            // every grate of the ground floor reaches every other one through the ducts
            var first = net.Grates[0].Cell;
            foreach (var g in net.Grates) Assert.IsNotNull(StorageBotRules.Crawl(net, first, g.Cell));
        }

        [Test]
        public void AGuardSoundsLikeWhatItIsDoing()
        {
            Assert.AreEqual(0, Stealth.Audio.GuardSounds.Pick(true, Stealth.Enemies.RobotState.Patrol, false).clip, "charging on its dock");
            Assert.AreEqual(1, Stealth.Audio.GuardSounds.Pick(false, Stealth.Enemies.RobotState.Patrol, false).clip, "on its rounds");
            Assert.AreEqual(1, Stealth.Audio.GuardSounds.Pick(false, Stealth.Enemies.RobotState.Investigate, false).clip);
            Assert.AreEqual(2, Stealth.Audio.GuardSounds.Pick(false, Stealth.Enemies.RobotState.Chase, false).clip, "chasing");
            Assert.AreEqual(-1, Stealth.Audio.GuardSounds.Pick(false, Stealth.Enemies.RobotState.Chase, true).clip, "hacked: silent");
        }
        [Test]
        public void InItsOwnRoomItWalksAndToAnotherRoomItTakesTheDucts()
        {
            Assert.AreEqual(StorageBotRules.Route.Walk, StorageBotRules.Way(true, 6f, true), "beside the caller: on foot");
            Assert.AreEqual(StorageBotRules.Route.Ducts, StorageBotRules.Way(false, 6f, true), "another room, however near: the ducts");
            Assert.AreEqual(StorageBotRules.Route.Ducts, StorageBotRules.Way(true, -1f, true), "up in the ducts already: no way on the floor");
            Assert.AreEqual(StorageBotRules.Route.Walk, StorageBotRules.Way(false, 20f, false), "no duct leads there: on foot after all");
            Assert.AreEqual(StorageBotRules.Route.None, StorageBotRules.Way(false, StorageBotRules.WalkFurthest + 5f, false), "and too far on foot");
            Assert.AreEqual(StorageBotRules.Route.None, StorageBotRules.Way(true, -1f, false));
        }

        [Test]
        public void OneRoomIsTheSameRoomOfOneStoreyOrTheBasementForBoth()
        {
            foreach (var l in TestHouses.Sample)
            {
                var rooms = l.Plan.Rooms.Where(r => r.Area > 9f && l.RoomAt(r.Center + Vector3.up * 0.1f) == r).Take(6).ToList();
                Assert.GreaterOrEqual(rooms.Count, 2);
                foreach (var r in rooms)
                {
                    Assert.IsTrue(StorageBotRules.SameRoom(l, r.Center, r.Center + new Vector3(0.3f, 0f, 0.2f)), $"{r.Type}: two steps apart");
                    foreach (var other in rooms)
                        if (other != r) Assert.IsFalse(StorageBotRules.SameRoom(l, r.Center, other.Center), $"{r.Type} and {other.Type}");
                }
                var cellar = Basement.StashSpot(l.Cellar);
                Assert.IsTrue(StorageBotRules.SameRoom(l, cellar, Basement.Spawns(l.Cellar)[0]), "both down in the basement");
                Assert.IsFalse(StorageBotRules.SameRoom(l, cellar, rooms[0].Center), "the basement and a room of the house");
            }
        }
    }
}
