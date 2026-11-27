using NUnit.Framework;
using Stealth.Game;
using Stealth.Level;

namespace Stealth.Tests
{
    /// <summary>The run's danger (<see cref="Escalation"/>) and the factions (<see cref="RobotRoster.FactionOf"/>).</summary>
    public class EscalationTests
    {
        [Test]
        public void WhatTheTeamDoesMovesTheClock()
        {
            var s = new EscalationSettings();
            Assert.Greater(Escalation.Seconds(DangerEvent.Alarm, s), Escalation.Seconds(DangerEvent.Hack, s), "an alarm costs more than a quiet hack");
            foreach (DangerEvent e in System.Enum.GetValues(typeof(DangerEvent)))
                Assert.Greater(Escalation.Seconds(e, s), 0f, e.ToString());
        }

        [Test]
        public void TheRedRobotsComeLate()
        {
            var s = new EscalationSettings { RedAfter = 300f };
            Assert.AreEqual(DangerZone.Yellow, Escalation.Tier(0f, s));
            Assert.AreEqual(DangerZone.Yellow, Escalation.Tier(299f, s));
            Assert.AreEqual(DangerZone.Red, Escalation.Tier(300f, s));
        }

        [Test]
        public void ArmedGuardsComeAfterTheirMinuteOneByOne()
        {
            var s = new EscalationSettings { ArmedAfter = 480f, ArmedEvery = 60f, ArmedMax = 3 };
            Assert.AreEqual(0, Escalation.ArmedDue(479f, s));
            Assert.AreEqual(1, Escalation.ArmedDue(480f, s));
            Assert.AreEqual(2, Escalation.ArmedDue(545f, s));
            Assert.AreEqual(3, Escalation.ArmedDue(5000f, s), "never more than the limit");
            s.ArmedMax = 0;
            Assert.AreEqual(0, Escalation.ArmedDue(5000f, s));
        }

        [Test]
        public void TheNextHouseStartsWithAShareOfTheClock()
        {
            var s = new EscalationSettings { CarryOver = 0.25f, CarryMax = 100f };
            Assert.AreEqual(50f, Escalation.Carry(200f, s), 0.01f);
            Assert.AreEqual(100f, Escalation.Carry(2000f, s), 0.01f);
            Assert.AreEqual(0f, Escalation.Carry(0f, s), 0.01f);
        }

        [Test]
        public void ReinforcementsAreServantsOfTheZoneTheClockAllows()
        {
            var table = RobotRoster.DefaultRobots();
            for (int i = 0; i < 300; i++)
            {
                var early = RobotRoster.PickReinforcement(table, 9, new System.Random(i), DangerZone.Yellow);
                Assert.AreEqual(DangerZone.Yellow, RobotRoster.ZoneOf(early), $"{early}: early ones are of the yellow zone");
                Assert.AreEqual(Faction.Servant, RobotRoster.FactionOf(early), $"{early}: a renegade does not come when called");
                var late = RobotRoster.PickReinforcement(table, 9, new System.Random(i), DangerZone.Red);
                Assert.AreNotEqual(DangerZone.Green, RobotRoster.ZoneOf(late));
                Assert.AreEqual(Faction.Servant, RobotRoster.FactionOf(late));
            }
        }

        [Test]
        public void TheSpiderIsARenegade()
        {
            Assert.AreEqual(Faction.Renegade, RobotRoster.FactionOf(RobotKind.Spider));
            Assert.AreEqual(Faction.Servant, RobotRoster.FactionOf(RobotKind.Guard));
            Assert.AreEqual(Faction.Servant, RobotRoster.FactionOf(RobotKind.Worker));
        }
    }
}
