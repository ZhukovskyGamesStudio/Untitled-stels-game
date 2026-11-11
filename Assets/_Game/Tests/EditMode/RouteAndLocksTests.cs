using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The route through the sewers, the vote for the next house, safes and locked doors.</summary>
    public class RouteAndLocksTests
    {
        [Test]
        public void TheRouteStartsInTheSewerAndAlwaysBranches()
        {
            var map = new RunMap(12345);
            Assert.AreEqual(1, map.LanesAt(RunMap.StartDepth), "one sewer at the start");
            Assert.That(map.LanesAt(0), Is.InRange(2, 3), "2–3 first houses: the sewer has three tunnels");
            CollectionAssert.AreEqual(Enumerable.Range(0, map.LanesAt(0)), map.Next(RunMap.StartDepth, 0), "every first house is open");
            bool sawOne = false, sawThree = false;
            for (int d = 0; d < 30; d++)
            {
                int n = map.LanesAt(d + 1);
                Assert.That(n, Is.InRange(RunMap.MinLanes, RunMap.MaxLanes));
                var reached = new HashSet<int>();
                for (int lane = 0; lane < map.LanesAt(d); lane++)
                {
                    var next = map.Next(d, lane);
                    Assert.That(next.Count, Is.InRange(1, 3), $"depth {d} lane {lane}: 1 to 3 ways on");
                    Assert.IsTrue(next.All(j => j >= 0 && j < n));
                    CollectionAssert.AllItemsAreUnique(next);
                    sawOne |= next.Count == 1;
                    sawThree |= next.Count == 3;
                    reached.UnionWith(next);
                }
                Assert.AreEqual(n, reached.Count, $"depth {d + 1}: every house can be reached");
            }
            Assert.IsTrue(sawOne && sawThree, "sometimes one way, sometimes three");
        }

        [Test]
        public void EveryPeerDrawsTheSameRoute()
        {
            var a = new RunMap(77);
            var b = new RunMap(77);
            for (int d = 0; d < 10; d++)
                for (int lane = 0; lane < a.LanesAt(d); lane++)
                {
                    CollectionAssert.AreEqual(a.Next(d, lane), b.Next(d, lane));
                    Assert.AreEqual(a.Node(d, lane).Seed, b.Node(d, lane).Seed);
                }
            Assert.AreEqual(LevelRandom.LevelSeed(77, 3), a.Node(3, 0).Seed, "row 0 keeps the classic house seed");
            Assert.AreNotEqual(a.Node(3, 0).Seed, a.Node(3, 1).Seed, "houses of a column differ");
            Assert.AreNotEqual(new RunMap(78).Node(2, 1).Seed, a.Node(2, 1).Seed);
        }

        [Test]
        public void TunnelsLeadToTheWaysInOrder()
        {
            CollectionAssert.AreEqual(new[] { 1 }, SewerBranches.Slots(1), "one way: the middle tunnel");
            CollectionAssert.AreEqual(new[] { 0, 2 }, SewerBranches.Slots(2), "two: the side ones");
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, SewerBranches.Slots(3));
            Assert.AreEqual(-1, SewerBranches.WayOf(1, 2), "the middle is barred with two ways");
            Assert.AreEqual(1, SewerBranches.WayOf(2, 2));
            Assert.AreEqual(-1, SewerBranches.WayOf(-1, 3), "not in any tunnel");
            for (int ways = 1; ways <= 3; ways++)
                Assert.That(new RunMap(ways).Next(0, 0).Count, Is.LessThanOrEqualTo(SewerBranches.Count), "never more ways than tunnels");
        }

        [Test]
        public void SteppingOutOfATunnelTakesTheVoteBack()
        {
            var votes = new RouteVotes();
            var options = new List<int> { 0, 1 };
            votes.Cast(1, 0);
            votes.Cast(2, 1);
            Assert.IsTrue(votes.Everyone(new ulong[] { 1, 2 }, options));
            int v = votes.Version;
            votes.Remove(2);
            Assert.Greater(votes.Version, v);
            Assert.IsFalse(votes.Everyone(new ulong[] { 1, 2 }, options));
            votes.Remove(2);
            Assert.AreEqual(v + 1, votes.Version, "removing twice changes nothing");
        }

        [Test]
        public void ARunStartsInTheSewerWithNothingToPay()
        {
            var run = new RunState();
            run.BeginRun();
            Assert.AreEqual(RunPhase.Sewer, run.Phase);
            Assert.AreEqual(RunMap.StartDepth, run.LevelIndex);
            Assert.IsTrue(run.QuotaMet, "no quota before the first house");
            Assert.AreEqual(RunState.Exit.NextLevel, run.Advance());
        }

        [Test]
        public void TheMajorityChoosesATieIsDecidedByChance()
        {
            var votes = new RouteVotes();
            var options = new List<int> { 0, 2 };
            ulong[] team = { 1, 2, 3 };
            votes.Cast(1, 0);
            votes.Cast(2, 2);
            Assert.IsFalse(votes.Everyone(team, options));
            votes.Cast(3, 2);
            Assert.IsTrue(votes.Everyone(team, options));
            Assert.AreEqual(2, votes.Winner(options, new System.Random(1)));
            votes.Cast(3, 1);
            Assert.IsFalse(votes.Everyone(team, options), "a vote for a house you cannot reach does not count");

            votes.Cast(3, 0);
            votes.Cast(4, 2);
            var winners = new HashSet<int>();
            for (int seed = 0; seed < 40; seed++) winners.Add(votes.Winner(options, new System.Random(seed)));
            CollectionAssert.AreEquivalent(new[] { 0, 2 }, winners, "2 : 2 — either can win");
        }

        [Test]
        public void TheSafeOpensWithTheRightNumbersAndClanksOnWrongOnes()
        {
            var s = new SafeSettings { DialSize = 40, Digits = 3, HearRange = 6 };
            var lockState = new SafeLock(99, s);
            Assert.AreEqual(3, lockState.Digits);
            for (int i = 1; i < 3; i++)
            {
                int d = Mathf.Abs(lockState.CodeAt(i) - lockState.CodeAt(i - 1));
                Assert.GreaterOrEqual(Mathf.Min(d, 40 - d), 10, "every digit needs a real search");
            }
            int code0 = lockState.CodeAt(0);
            lockState.SetDial(code0 + 20);
            Assert.AreEqual(0f, lockState.Closeness(), "far away: quiet");
            Assert.AreEqual(SafeLock.Result.Wrong, lockState.Try());
            Assert.AreEqual(0, lockState.Found);
            lockState.SetDial(code0 + 2);
            Assert.That(lockState.Closeness(), Is.InRange(0.3f, 0.99f), "close: louder");
            lockState.SetDial(code0);
            Assert.AreEqual(1f, lockState.Closeness(), 1e-4f, "on it");
            Assert.AreEqual(SafeLock.Result.Digit, lockState.Try());
            lockState.SetDial(lockState.CodeAt(1));
            Assert.AreEqual(SafeLock.Result.Digit, lockState.Try());
            lockState.SetDial(lockState.CodeAt(2));
            Assert.AreEqual(SafeLock.Result.Opened, lockState.Try());
            Assert.IsTrue(lockState.Open);
            Assert.AreEqual(SafeLock.Result.Nothing, lockState.Try());
        }

        [Test]
        public void TheDialWrapsAroundAndCountsTicks()
        {
            var lockState = new SafeLock(1, new SafeSettings { DialSize = 40, TurnSpeed = 1f });
            lockState.SetDial(39);
            Assert.AreEqual(2, lockState.Turn(2f), "39 → 1 passes two marks");
            Assert.AreEqual(1, lockState.Number);
            Assert.AreEqual(new SafeLock(5).CodeAt(0), new SafeLock(5).CodeAt(0), "the same safe has the same code on every peer");
        }

        [Test]
        public void SomeDoorsAreLockedButNeverAtTheLobby()
        {
            int locked = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                var l = LevelGenerator.Generate(seed, 3);
                foreach (int i in l.LockedDoors)
                {
                    Assert.That(i, Is.InRange(0, l.AutoDoors.Count - 1));
                    Assert.GreaterOrEqual((l.AutoDoors[i].position - l.HatchPosition).magnitude, LevelMetrics.Cell * 0.75f);
                    locked++;
                }
                Assert.IsEmpty(LevelGenerator.Generate(seed, 0).LockedDoors, "the first house has no locks");
            }
            Assert.Greater(locked, 3);
        }

        [Test]
        public void LockedDoorsDoNotChangeTheRestOfTheHouse()
        {
            var a = LevelGenerator.Generate(31, 3);
            var b = LevelGenerator.Generate(31, 3, new BuildingSettings { LockedDoorChance = 0f });
            Assert.IsEmpty(b.LockedDoors);
            CollectionAssert.AreEqual(a.Rooms, b.Rooms);
            Assert.AreEqual(a.AutoDoors.Count, b.AutoDoors.Count);
            Assert.AreEqual(a.RobotSpawns.Count, b.RobotSpawns.Count);
        }

        [Test]
        public void TheHatchIsOnlyNearOnItsOwnFloor()
        {
            var hatch = new Vector3(5f, 0f, 5f);
            Assert.IsTrue(Hatch.IsNear(new Vector3(6f, 0.05f, 6f), hatch));
            Assert.IsFalse(Hatch.IsNear(new Vector3(6f, LevelMetrics.FloorHeight + 0.05f, 6f), hatch), "right above it, upstairs");
            Assert.IsFalse(Hatch.IsNear(new Vector3(9.5f, 0.05f, 5f), hatch));
        }
    }
}
