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
            run.BeginLevel(0, 1000);
            Assert.AreEqual(RunState.QuotaFor(1000, 0, 0), run.Quota);
            Assert.AreEqual(0, run.Collected);
            Assert.AreEqual(RunPhase.Playing, run.Phase);
            Assert.IsFalse(run.QuotaMet);
        }

        [Test]
        public void DepositsCountForTheHouseAndTheRun()
        {
            var run = new RunState();
            run.BeginLevel(0, 1000);
            run.Deposit(40);
            run.Deposit(25);
            Assert.AreEqual(65, run.Collected);
            Assert.AreEqual(65, run.RunTotal);
            run.BeginLevel(1, 1000);
            Assert.AreEqual(0, run.Collected, "the quota is per house");
            Assert.AreEqual(65, run.RunTotal, "the total is per run");
        }

        [Test]
        public void GoingOnWithoutTheQuotaEndsTheRunInTheSewer()
        {
            var run = new RunState();
            run.BeginLevel(0, 1000);
            run.Deposit(10);
            Assert.IsTrue(run.EnterSewer(0), "leaving a house is always possible");
            Assert.AreEqual(RunPhase.Sewer, run.Phase);
            Assert.IsTrue(run.CanTrade, "the rest can be sold down there");
            Assert.AreEqual(RunState.Exit.Failed, run.Advance());
            Assert.AreEqual(RunPhase.Failed, run.Phase);
            Assert.AreEqual(RunState.Exit.Ignored, run.Advance(), "nothing happens after the end");
            Assert.IsFalse(run.EnterSewer(0));
        }

        [Test]
        public void TheQuotaCanBePaidInTheSewer()
        {
            var run = new RunState();
            run.BeginLevel(0, 1000);
            run.EnterSewer(0);
            run.Deposit(run.Quota);
            Assert.AreEqual(RunState.Exit.NextLevel, run.Advance());
        }

        [Test]
        public void TheTeamLeavesWhenEveryoneWhoCanIsInTheHatch()
        {
            Assert.IsTrue(RunState.TeamInHatch(new[] { (true, false), (true, false) }));
            Assert.IsFalse(RunState.TeamInHatch(new[] { (true, false), (false, false) }), "one is still upstairs");
            Assert.IsTrue(RunState.TeamInHatch(new[] { (true, false), (false, true) }), "the knocked-out one stays behind");
            Assert.IsFalse(RunState.TeamInHatch(new[] { (false, true), (false, true) }), "nobody is in the hatch");
            Assert.IsFalse(RunState.TeamInHatch(new (bool, bool)[0]));
        }

        [Test]
        public void TeammatesLeftDownRaiseTheNextQuota()
        {
            var run = new RunState();
            run.BeginLevel(0, 1000);
            run.Deposit(run.Quota);
            run.EnterSewer(2);
            Assert.AreEqual(RunState.Exit.NextLevel, run.Advance());
            run.BeginLevel(1, 1000);
            Assert.AreEqual(RunState.QuotaFor(1000, 1, 2), run.Quota);
            Assert.Greater(run.Quota, RunState.QuotaFor(1000, 1, 0));
        }

        [Test]
        public void AWipeEndsTheRunAndARestartClearsIt()
        {
            var run = new RunState();
            Assert.IsFalse(run.Restart(), "no restart while playing");
            run.BeginLevel(0, 1000);
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
            run.BeginLevel(0, 1000);
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
        public void AHunterComesAfterThreeAlarmsOnTheFirstHouse()
        {
            var run = new RunState();
            run.BeginLevel(0, 1000);
            for (int i = 0; i < 3; i++) run.RaiseAlarm(i * 10.0);
            Assert.IsTrue(run.ShouldSendHunter(30f));
            run.HunterSent();
            Assert.IsFalse(run.ShouldSendHunter(30f), "one hunter per early house");
            run.BeginLevel(1, 1000);
            Assert.AreEqual(0, run.Hunters, "counters reset per house");
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
