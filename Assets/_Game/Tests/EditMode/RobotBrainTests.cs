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

        void Sense(float rate, float seconds, IIntruder target = null, bool contact = false)
        {
            target ??= m_Player;
            for (float t = 0; t < seconds; t += Dt)
            {
                m_Now += Dt;
                m_Brain.OnSensed(new Sighting { Target = target, Rate = rate, Contact = contact, ChasePoint = target.Position }, m_Now, Dt);
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
        public void BumpingIntoARobotDetectsAtOnceButStillWarns()
        {
            Sense(0f, Dt, contact: true);
            Assert.AreEqual(RobotState.Alerted, m_Brain.State);
            Sense(0f, m_Traits.WarningTime + Dt, contact: true);
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
