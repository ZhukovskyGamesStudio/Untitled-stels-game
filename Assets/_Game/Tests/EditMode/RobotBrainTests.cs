using NUnit.Framework;
using Stealth.Enemies;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Decision making of mobile robots, driven through a fake body (no navmesh, no networking).</summary>
    public class RobotBrainTests
    {
        const float Dt = 0.1f;

        FakeBody m_Body;
        FakeIntruder m_Player;
        RobotSenses m_Senses;
        RobotTraits m_Traits;
        RobotBrain m_Brain;
        float m_Now;

        [SetUp]
        public void SetUp()
        {
            m_Body = new FakeBody { Position = Vector3.zero };
            m_Player = new FakeIntruder { ClientId = 1, Position = new Vector3(0, 0, 8) };
            m_Senses = new RobotSenses { HearingMultiplier = 1f };
            m_Traits = new RobotTraits { PatrolSpeed = 2f, InvestigateSpeed = 3f, ChaseSpeed = 4.5f, LoseTargetAfter = 4f, AttackRange = 1.7f, AttackCooldown = 1.5f };
            m_Brain = new RobotBrain(m_Senses, m_Traits, m_Body, new System.Random(1));
            m_Now = 0f;
        }

        [Test]
        public void APauseReadsFromAfarStopLookAroundThenTurnToGo()
        {
            Assert.AreEqual(RobotBrain.PatrolPause.Settle, RobotBrain.PausePhase(0.2f, 4f, 1f), "just stopped");
            Assert.AreEqual(RobotBrain.PatrolPause.Scan, RobotBrain.PausePhase(1.5f, 4f, 1f), "standing a while: looking around");
            Assert.AreEqual(RobotBrain.PatrolPause.Ready, RobotBrain.PausePhase(3.3f, 4f, 1f), "about to go: turned to the way");
            Assert.Less(RobotBrain.PauseScanSide(1.6f), 0f, "first to the left");

            m_Traits.PatrolWaitMin = m_Traits.PatrolWaitMax = 4f;
            m_Traits.PauseReadyTime = 1f;
            m_Body.Arrived = true;
            for (float t = 0f; t < 2.9f; t += 0.1f) m_Brain.Tick(t, 0.1f);
            Assert.IsTrue(m_Body.Stopped);
            Assert.AreEqual(0, m_Body.Plans, "still looking around");
            Assert.AreEqual(0, m_Body.PatrolRequests);
            Assert.IsTrue(m_Body.Faced.Count > 0, "it turns while it stands");
            for (float t = 2.9f; t < 4.0f; t += 0.1f) m_Brain.Tick(t, 0.1f);
            Assert.AreEqual(1, m_Body.Plans, "chose where to go before going");
            Assert.AreEqual(m_Body.NextPoint, m_Body.Faced[m_Body.Faced.Count - 1], "and turned to it");
            Assert.AreEqual(0, m_Body.PatrolRequests, "not walking yet");
            m_Brain.Tick(4.2f, 0.1f);
            Assert.AreEqual(1, m_Body.PatrolRequests, "then it walks");
        }

        void Sense(float rate, float seconds, IIntruder target = null)
        {
            target ??= m_Player;
            for (float t = 0; t < seconds; t += Dt)
            {
                m_Now += Dt;
                m_Brain.OnSensed(new Sighting { Target = target, Rate = rate, ChasePoint = target.Position }, m_Now, Dt);
                m_Brain.Tick(m_Now, Dt);
            }
        }

        /// <summary>Seen clearly until it has warned and started chasing.</summary>
        void Spot(IIntruder target = null) => Sense(5f, 0.3f + m_Traits.WarningTime, target);

        void Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Dt)
            {
                m_Now += Dt;
                m_Brain.OnSensed(Sighting.None, m_Now, Dt);
                m_Brain.Tick(m_Now, Dt);
            }
        }

        [Test]
        public void SuspicionBuildsUpToAWarningAndThenAChase()
        {
            Sense(1f, 0.4f);
            Assert.AreEqual(RobotState.Suspicious, m_Brain.State, "stops and stares first");
            Assert.IsTrue(m_Body.Stopped);
            Sense(1f, 0.8f);
            Assert.AreEqual(RobotState.Alerted, m_Brain.State, "«Обнаружен нарушитель»: a moment to react");
            Assert.AreEqual(1f, m_Brain.Awareness);
            Assert.AreEqual(0, m_Body.BackupCalls, "does not act yet");
            Sense(1f, m_Traits.WarningTime - 0.3f);
            Assert.AreEqual(RobotState.Alerted, m_Brain.State, "still warning");
            Sense(1f, 0.3f);
            Assert.AreEqual(RobotState.Chase, m_Brain.State, "«Веду преследование»");
            Assert.AreSame(m_Player, m_Brain.Target);
            Assert.AreEqual(m_Traits.ChaseSpeed, m_Body.Speed, 1e-4f);
            Assert.AreEqual(1, m_Body.BackupCalls, "radios nearby robots once");
            Assert.AreEqual(0, m_Body.Alarms.Count, "only alarm robots raise building-wide alarms");
        }

        [Test]
        public void ARobotDetectsOnlyWithItsSensesAndStillWarns()
        {
            // touching it from behind is no sense of its own: a player right by it that its sensors do not pick up goes unnoticed
            Sense(0f, 2f);
            Assert.AreEqual(RobotState.Patrol, m_Brain.State, "touched, not noticed");
            Sense(100f, Dt);
            Assert.AreEqual(RobotState.Alerted, m_Brain.State, "seen: detected, the warning first");
            Sense(100f, m_Traits.WarningTime + Dt);
            Assert.AreEqual(RobotState.Chase, m_Brain.State);
        }

        [Test]
        public void HidingDuringTheWarningSendsItToTheLastSeenSpot()
        {
            Sense(5f, 0.3f);
            Assert.AreEqual(RobotState.Alerted, m_Brain.State);
            var seenAt = m_Player.Position;
            m_Player.Position = new Vector3(10, 0, 10); // ducked behind a wall
            Wait(m_Traits.WarningTime + Dt);
            Assert.AreEqual(RobotState.Chase, m_Brain.State);
            Assert.AreEqual(seenAt, m_Body.Destination, "runs to where it saw them, not to where they are");
            Wait(m_Traits.LoseTargetAfter + 0.3f);
            Assert.AreEqual(RobotState.Investigate, m_Brain.State, "and loses them");
        }

        [Test]
        public void ATargetDownedDuringTheWarningIsLeftAlone()
        {
            Sense(5f, 0.3f);
            m_Player.IsDown = true;
            Wait(Dt);
            Assert.AreEqual(RobotState.Patrol, m_Brain.State);
        }

        [Test]
        public void SuspicionFadesWhenThePlayerHides()
        {
            Sense(1f, 0.4f);
            Wait(3f);
            Assert.AreEqual(RobotState.Patrol, m_Brain.State);
            Assert.AreEqual(0f, m_Brain.Suspicion, 1e-4f);
        }

        [Test]
        public void ASuspiciousRobotWalksOverToCheckAfterAWhile()
        {
            m_Player.Position = new Vector3(3, 0, 5);
            Sense(0.3f, 0.95f); // suspicious, but never enough for a chase
            Assert.AreEqual(RobotState.Suspicious, m_Brain.State);
            Sense(0.15f, 4.2f);
            Assert.AreEqual(RobotState.Investigate, m_Brain.State);
            Assert.AreEqual(m_Player.Position, m_Body.Destination);
        }

        [Test]
        public void LosingTheTargetLeadsToInvestigatingItsLastKnownPosition()
        {
            Spot();
            Assert.AreEqual(RobotState.Chase, m_Brain.State);
            var lastSeen = m_Player.Position;
            m_Player.Position = new Vector3(20, 0, 20); // ran away, out of sight
            Wait(m_Traits.LoseTargetAfter + 0.3f);
            Assert.AreEqual(RobotState.Investigate, m_Brain.State, "goes to look, does not give up at once");
            Assert.AreEqual(lastSeen, m_Body.Destination);
            Assert.IsNull(m_Brain.Target);
        }

        [Test]
        public void ADownedTargetEndsTheChase()
        {
            Spot();
            m_Player.IsDown = true;
            Wait(Dt);
            Assert.AreEqual(RobotState.Patrol, m_Brain.State);
            Assert.Greater(m_Body.PatrolRequests, 0);
        }

        [Test]
        public void AttacksInRangeRespectTheCooldown()
        {
            m_Player.Position = new Vector3(0, 0, 1.2f);
            Spot();
            Sense(5f, 1.9f);
            Assert.AreEqual(2, m_Body.Attacks.Count, "a hit at once and one more after 1.5 s");
        }

        [Test]
        public void FlyingRobotsAttackFromAbove()
        {
            m_Body.Position = new Vector3(0, 2.1f, 0); // hovering drone
            m_Player.Position = new Vector3(0, 0, 1f);
            Spot();
            Assert.AreEqual(1, m_Body.Attacks.Count, "height does not count for the attack range");
        }

        [Test]
        public void AlarmRobotsRaiseAnAlarmWhenTheyStartChasing()
        {
            m_Traits.AlarmOnDetect = true;
            Sense(5f, 0.3f);
            Assert.AreEqual(0, m_Body.Alarms.Count, "not during the warning");
            Spot();
            Assert.AreEqual(1, m_Body.Alarms.Count);
            Assert.AreEqual(m_Traits.AlarmRadius, m_Body.Alarms[0].radius);
        }

        [Test]
        public void SnitchesReportInsteadOfFighting()
        {
            m_Traits.CanAttack = false;
            m_Player.Position = new Vector3(0, 0, 1f);
            Sense(5f, 0.3f);
            Assert.AreEqual(RobotState.Alerted, m_Brain.State, "warns first");
            Assert.AreEqual(0, m_Body.Alarms.Count);
            Sense(5f, m_Traits.WarningTime);
            Assert.AreEqual(1, m_Body.Alarms.Count, "reports once");
            Assert.AreNotEqual(RobotState.Chase, m_Brain.State);
            Assert.AreEqual(0, m_Body.Attacks.Count);
            Sense(5f, 5f);
            Assert.AreEqual(1, m_Body.Alarms.Count, "and not again during the cooldown");
            Assert.AreEqual(RobotState.Patrol, m_Brain.State, "goes back to work");
            Sense(5f, m_Traits.SnitchCooldown + m_Traits.WarningTime);
            Assert.AreEqual(2, m_Body.Alarms.Count);
        }

        [Test]
        public void AQuietNoiseMakesItGlanceRepeatedLouderOnesSendItToCheck()
        {
            var spot = new Vector3(0, 0, 9);
            m_Brain.OnNoise(spot, 0.1f, m_Player, m_Now);
            Assert.AreEqual(RobotState.Suspicious, m_Brain.State, "turns to the sound");
            Assert.AreEqual(spot, m_Brain.LookPoint);
            Wait(1f);
            Assert.AreEqual(RobotState.Patrol, m_Brain.State, "a single faint sound is forgotten");

            m_Brain.OnNoise(spot, 0.2f, m_Player, m_Now);
            m_Brain.OnNoise(spot, 0.2f, m_Player, m_Now);
            Assert.AreEqual(RobotState.Investigate, m_Brain.State, "several steps: goes to check");
            Assert.AreEqual(spot, m_Body.Destination);
        }

        [Test]
        public void ADroneThrowsItsOneNetRaisesTheAlarmAndFliesOffToRecharge()
        {
            m_Traits.CanAttack = true;
            m_Traits.Attack = AttackKind.Net;
            m_Traits.AttacksPerCharge = 1;
            m_Traits.RechargeTime = 10f;
            m_Traits.WarningTime = 0.5f;
            m_Player.Position = new Vector3(0, 0, 1f);
            Sense(5f, 2f);
            Assert.AreEqual(RobotState.Chase, m_Brain.State);
            int alarms = m_Body.Alarms.Count;
            var dock = new Vector3(20, 0, 5);
            m_Brain.Spend(dock, m_Now); // the net is thrown
            Assert.AreEqual(alarms + 1, m_Body.Alarms.Count, "the alarm goes up");
            Assert.AreEqual(RobotState.Patrol, m_Brain.State, "it leaves the netted player");
            Assert.AreEqual(RechargeStep.Going, m_Brain.Recharge);
            Assert.AreEqual(dock, m_Body.Destination, "off to the charging dock");

            m_Body.Arrived = false;
            Sense(5f, 3f); // a player right before it on the way: nothing — it has no net
            Assert.AreEqual(RobotState.Patrol, m_Brain.State, "blind and deaf on its way");
            Assert.AreEqual(0f, m_Brain.Suspicion);
            m_Brain.OnNoise(m_Player.Position, 1f, m_Player, m_Now);
            Assert.AreEqual(RobotState.Patrol, m_Brain.State);

            m_Body.Position = dock;
            m_Body.Arrived = true;
            Wait(1f);
            Assert.AreEqual(RechargeStep.Charging, m_Brain.Recharge, "at the dock: charging");
            int patrols = m_Body.PatrolRequests;
            Wait(9.5f);
            Assert.AreEqual(RechargeStep.None, m_Brain.Recharge, "a new net after RechargeTime");
            Assert.Greater(m_Body.PatrolRequests, patrols, "and back on its rounds");
            Sense(5f, 2f);
            Assert.AreNotEqual(RobotState.Patrol, m_Brain.State, "it notices players again");
        }

        [Test]
        public void ARobotWithoutAChargeLimitNeverRecharges()
        {
            m_Brain.Spend(new Vector3(9, 0, 9), m_Now);
            Assert.AreEqual(RechargeStep.None, m_Brain.Recharge);
        }

        [Test]
        public void ASlowEarNeedsFourTimesTheNoise()
        {
            var slow = new RobotBrain(new RobotSenses { HearingMultiplier = 1f, HearingSpeed = 0.25f }, m_Traits, new FakeBody(), new System.Random(1));
            var spot = new Vector3(0, 0, 3);
            for (int i = 0; i < 4; i++) slow.OnNoise(spot, 0.3f, m_Player, m_Now);
            Assert.AreEqual(0.3f, slow.Suspicion, 1e-4f, "four noises: what one does to a quick ear");
            Assert.AreNotEqual(RobotState.Alerted, slow.State);
            for (int i = 0; i < 12; i++) slow.OnNoise(spot, 0.3f, m_Player, m_Now);
            Assert.AreEqual(RobotState.Alerted, slow.State, "sixteen: sure (four times the quick ear's four)");
        }

        [Test]
        public void EnoughNoiseDetectsTheSourceWithAWarning()
        {
            for (int i = 0; i < 4; i++) m_Brain.OnNoise(new Vector3(0, 0, 3), 0.3f, m_Player, m_Now);
            Assert.AreEqual(RobotState.Alerted, m_Brain.State);
            Assert.AreSame(m_Player, m_Brain.Suspect);
        }

        [Test]
        public void NoisesWithoutASourceAreOnlyChecked()
        {
            for (int i = 0; i < 6; i++) m_Brain.OnNoise(new Vector3(0, 0, 3), 0.3f, null, m_Now);
            Assert.AreEqual(RobotState.Investigate, m_Brain.State);
        }

        [Test]
        public void DeafRobotsHearNothing()
        {
            m_Senses.HearingMultiplier = 0f;
            m_Brain.OnNoise(Vector3.zero, 0.9f, m_Player, 0f);
            Assert.AreEqual(RobotState.Patrol, m_Brain.State);
        }

        [Test]
        public void WhileChasingRadioCallsAreIgnored()
        {
            Sense(5f, 0.3f);
            m_Brain.Investigate(new Vector3(50, 0, 50), true, m_Now);
            Assert.AreEqual(RobotState.Alerted, m_Brain.State, "nor during the warning");
            Spot();
            m_Brain.Investigate(new Vector3(50, 0, 50), true, m_Now);
            Assert.AreEqual(RobotState.Chase, m_Brain.State);
        }

        [Test]
        public void AlarmsSendRobotsRunning()
        {
            m_Brain.Investigate(new Vector3(5, 0, 5), true, m_Now);
            Assert.AreEqual(RobotState.Investigate, m_Brain.State);
            Assert.AreEqual(m_Traits.ChaseSpeed * 0.85f, m_Body.Speed, 1e-4f);
        }

        [Test]
        public void InvestigatingRobotsLookAroundAndReturnToPatrol()
        {
            m_Brain.Investigate(new Vector3(5, 0, 5), false, m_Now);
            m_Body.Arrived = true;
            Wait(1f);
            Assert.AreNotEqual(0f, m_Body.Turned, "looks around");
            Wait(2.5f);
            Assert.AreEqual(RobotState.Patrol, m_Brain.State);
        }

        [Test]
        public void PatrollingRobotsPauseAtEachPoint()
        {
            m_Body.Arrived = true;
            Wait(0.5f);
            Assert.AreEqual(0, m_Body.PatrolRequests, "waits at least a second");
            Wait(4f);
            Assert.Greater(m_Body.PatrolRequests, 0);
        }

        [Test]
        public void HackingWipesTheMemory()
        {
            Spot();
            m_Brain.Reset(m_Now);
            Assert.AreEqual(RobotState.Patrol, m_Brain.State);
            Assert.IsNull(m_Brain.Target);
            Assert.AreEqual(0f, m_Brain.Awareness);
        }

        [Test]
        public void StateChangesAreReported()
        {
            int changes = 0;
            m_Brain.StateChanged += _ => changes++;
            Spot();
            Assert.AreEqual(3, changes, "suspicious, alerted, chase");
        }
    }
}
