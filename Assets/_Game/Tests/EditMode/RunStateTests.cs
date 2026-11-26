using NUnit.Framework;
using Stealth.Game;

namespace Stealth.Tests
{
    /// <summary>Rules of a run: quota, deposits, leaving a house, wipes, restarts, alarms.</summary>
    public class RunStateTests
    {
        [Test]
        public void ANewHouseStartsWithAQuotaAndNothingCollected()
        {
            var run = new RunState();
            run.BeginLevel(0);
            Assert.AreEqual(RunState.QuotaFor(0, 0), run.Quota);
            Assert.AreEqual(0, run.Collected);
            Assert.AreEqual(RunPhase.Playing, run.Phase);
            Assert.IsFalse(run.QuotaMet);
        }

        [Test]
        public void QuotasAreFixedNumbersThatGrowWithEveryHouse()
        {
            var s = new RunSettings();
            Assert.AreEqual(s.Quotas[0], RunState.QuotaFor(0, 0, s));
            Assert.AreEqual(s.Quotas[s.Quotas.Length - 1] + 2 * s.QuotaGrowth, RunState.QuotaFor(s.Quotas.Length + 1, 0, s), "past the list: so much more each");
            for (int house = 1; house < 12; house++) Assert.Greater(RunState.QuotaFor(house, 0), RunState.QuotaFor(house - 1, 0), $"house {house}");
            Assert.Greater(RunState.QuotaFor(0, 2), RunState.QuotaFor(0, 0), "teammates left down");
            Assert.AreEqual(0, RunState.QuotaFor(2, 1) % 5, "rounded to 5");
            var run = new RunState(s);
            run.BeginLevel(3);
            Assert.AreEqual(2 * run.Quota, run.LootFor(3), "a house holds its quota twice over");
            run.BeginLevel(3, 4);
            Assert.AreEqual(2 * run.Quota, run.LootFor(3, 4), "a team's too");
        }

        [Test]
        public void EveryPlayerAfterTheFirstAddsHalfTheQuota()
        {
            var s = new RunSettings { Quotas = new[] { 200 } };
            Assert.AreEqual(200, RunState.QuotaFor(0, 0, s));
            Assert.AreEqual(300, RunState.QuotaFor(0, 0, s, 2));
            Assert.AreEqual(400, RunState.QuotaFor(0, 0, s, 3));
            Assert.AreEqual(500, RunState.QuotaFor(0, 0, s, 4));
            var run = new RunState(s);
            run.BeginLevel(0);
            Assert.IsTrue(run.SetPlayers(3), "friends joined");
            Assert.AreEqual(400, run.Quota);
            Assert.IsFalse(run.SetPlayers(3), "no change");
            run.SetPlayers(1);
            Assert.AreEqual(200, run.Quota, "and back when they leave");
        }

        [Test]
        public void ABiggerHouseSendsItsRobotsSlowerAndNotWithoutEnd()
        {
            var s = new RunSettings();
            var small = s.ReinforcementsFor(1000f);
            Assert.AreEqual(s.ReinforceFirst, small.First, "never faster than the settings");
            Assert.AreEqual(0, small.Due(s.ReinforceFirst - 1f));
            Assert.AreEqual(1, small.Due(s.ReinforceFirst));
            Assert.AreEqual(2, small.Due(s.ReinforceFirst + s.ReinforceSecond));
            Assert.AreEqual(3, small.Due(s.ReinforceFirst + s.ReinforceSecond + s.ReinforceEvery));
            Assert.AreEqual(s.ReinforceMax, small.Due(99999f));
            Assert.AreEqual(-1f, small.NextAt(99999f), "no more");
            Assert.AreEqual(s.ReinforceFirst, small.NextAt(0f));
            Assert.AreEqual(0f, small.PreviousAt(0f));
            Assert.AreEqual(s.ReinforceFirst, small.PreviousAt(s.ReinforceFirst + 1f));
            var big = s.ReinforcementsFor(s.ReinforceArea * 2f);
            Assert.AreEqual(s.ReinforceFirst * 2f, big.First, 1e-3f);
            Assert.AreEqual(s.ReinforceEvery * s.ReinforceSlowest, s.ReinforcementsFor(99999f).Every, 1e-3f);
        }

        [Test]
        public void DepositsCountForTheHouseAndTheRun()
        {
            var run = new RunState();
            run.BeginLevel(0);
            run.Deposit(40);
            run.Deposit(25);
            Assert.AreEqual(65, run.Collected);
            Assert.AreEqual(65, run.RunTotal);
            run.BeginLevel(1);
            Assert.AreEqual(65, run.Collected, "the tank keeps its fuel from house to house");
            Assert.AreEqual(65, run.RunTotal, "the total is per run");
        }

        [Test]
        public void TheDriveOnBurnsTheQuotaOfFuel()
        {
            var run = new RunState();
            run.BeginRun();
            Assert.AreEqual(0, run.Quota, "the first drive is free");
            Assert.IsTrue(run.QuotaMet);
            run.BeginLevel(0);
            int cost = run.Quota;
            run.Deposit(cost + 30);
            Assert.IsTrue(run.EnterSewer(0));
            Assert.AreEqual(RunState.Exit.NextLevel, run.Advance(run.Quota));
            Assert.AreEqual(30, run.Collected, "what is left stays in the tank");
            Assert.IsTrue(RunState.CanStillGo(10, 50, 40));
            Assert.IsFalse(RunState.CanStillGo(10, 50, 39));
        }

        [Test]
        public void GoingOnWithoutTheQuotaEndsTheRunInTheSewer()
        {
            var run = new RunState();
            run.BeginLevel(0);
            run.Deposit(10);
            Assert.IsTrue(run.EnterSewer(0), "leaving a house is always possible");
            Assert.AreEqual(RunPhase.Sewer, run.Phase);
            Assert.IsTrue(run.CanTrade, "the rest can be fed into the engine down there");
            Assert.AreEqual(RunState.Exit.Ignored, run.Advance(run.Quota), "not enough fuel: the car does not go");
            Assert.AreEqual(RunPhase.Sewer, run.Phase);
            Assert.IsTrue(run.Strand(), "nothing left to burn: the run ends here");
            Assert.AreEqual(RunPhase.Failed, run.Phase);
            Assert.AreEqual(RunState.Exit.Ignored, run.Advance(run.Quota), "nothing happens after the end");
            Assert.IsFalse(run.EnterSewer(0));
        }

        [Test]
        public void TheQuotaCanBePaidInTheSewer()
        {
            var run = new RunState();
            run.BeginLevel(0);
            run.EnterSewer(0);
            run.Deposit(run.Quota);
            Assert.AreEqual(RunState.Exit.NextLevel, run.Advance(run.Quota));
        }

        [Test]
        public void EveryWayOnCostsItsOwnFuel()
        {
            var shares = new RunSettings().RouteFuel;
            for (int seed = 1; seed < 40; seed++)
                for (int depth = 0; depth < 4; depth++)
                {
                    var ways = new System.Collections.Generic.HashSet<float>();
                    for (int lane = 0; lane < shares.Length; lane++) ways.Add(RunState.RouteShare(seed, depth, lane, shares));
                    Assert.AreEqual(shares.Length, ways.Count, $"seed {seed} depth {depth}: the ways out of one sewer all differ");
                    Assert.AreEqual(RunState.RouteShare(seed, depth, 1, shares), RunState.RouteShare(seed, depth, 1, shares), "the same on every peer");
                }
            Assert.AreEqual(0, RunState.RouteCost(0, 1.35f), "the first drive is free whichever way");
            Assert.AreEqual(70, RunState.RouteCost(100, 0.7f));
            Assert.AreEqual(135, RunState.RouteCost(100, 1.35f));
            Assert.AreEqual(5, RunState.RouteCost(3, 0.7f), "never free once there is a quota");
            Assert.AreEqual(0, RunState.RouteCost(100, 0.7f) % 5, "in fives");
        }

        [Test]
        public void TheRunCanEndUnderTheHouseWhenNothingCanPayAnyWay()
        {
            var run = new RunState();
            run.BeginLevel(0);
            Assert.IsTrue(run.Strand(), "the house and the sewer under it hold too little: flooded there");
            Assert.AreEqual(RunPhase.Failed, run.Phase);
        }

        [Test]
        public void TeammatesLeftDownRaiseTheNextQuota()
        {
            var run = new RunState();
            run.BeginLevel(0);
            run.Deposit(run.Quota);
            run.EnterSewer(2);
            Assert.AreEqual(RunState.Exit.NextLevel, run.Advance(run.Quota));
            run.BeginLevel(1);
            Assert.AreEqual(RunState.QuotaFor(1, 2), run.Quota);
            Assert.Greater(run.Quota, RunState.QuotaFor(1, 0));
        }

        [Test]
        public void AWipeEndsTheRunAndARestartClearsIt()
        {
            var run = new RunState();
            Assert.IsFalse(run.Restart(), "no restart while playing");
            run.BeginLevel(0);
            run.Deposit(30);
            Assert.IsTrue(run.Wipe());
            Assert.IsFalse(run.Wipe(), "only once");
            Assert.IsTrue(run.Restart());
            Assert.AreEqual(0, run.RunTotal);
            Assert.AreEqual(0, run.Penalties);
        }

        [Test]
        public void AlarmsCloseTogetherCountOnce()
        {
            var run = new RunState(new RunSettings { AlarmDuration = 6f });
            run.BeginLevel(0);
            run.RaiseAlarm(10.0);
            run.RaiseAlarm(11.0);
            Assert.AreEqual(1, run.Alarms);
            Assert.IsTrue(run.AlarmActive(16.5), "the second alarm extends the first");
            run.RaiseAlarm(20.0);
            Assert.AreEqual(2, run.Alarms);
            run.CalmDown();
            Assert.IsFalse(run.AlarmActive(20.1));
        }

        [Test]
        public void ClientsTakeTheServersNumbers()
        {
            var run = new RunState();
            run.ApplyReplicated(3, 400, 120, 900, RunPhase.Failed, 55.0);
            Assert.AreEqual(3, run.LevelIndex);
            Assert.AreEqual(400, run.Quota);
            Assert.AreEqual(120, run.Collected);
            Assert.AreEqual(900, run.RunTotal);
            Assert.AreEqual(RunPhase.Failed, run.Phase);
            Assert.IsTrue(run.AlarmActive(50.0));
        }
    }
}
