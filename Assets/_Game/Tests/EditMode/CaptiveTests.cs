using NUnit.Framework;
using Stealth.Enemies;
using Stealth.Level;
using Stealth.Player;
using Stealth.UI;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Knocked out, carried and held (<see cref="CaptiveRules"/>, <see cref="HaulRules"/>, <see cref="Prison"/>) and the ailments (<see cref="AilmentRules"/>, <see cref="Hazards"/>).</summary>
    public class CaptiveTests
    {
        [Test]
        public void ATeammateFreesFromAPlaceButNotOffARobotsBack()
        {
            Assert.IsTrue(CaptiveRules.CanFree(CaptiveKind.None), "lying on the floor");
            Assert.IsTrue(CaptiveRules.CanFree(CaptiveKind.Prison));
            Assert.IsTrue(CaptiveRules.CanFree(CaptiveKind.Cocoon));
            Assert.IsTrue(CaptiveRules.CanFree(CaptiveKind.Lair));
            Assert.IsFalse(CaptiveRules.CanFree(CaptiveKind.Carried), "stop the robot first");
            Assert.IsFalse(CaptiveRules.CanFree(CaptiveKind.Ward), "the sleeper wakes by themself");
        }

        [Test]
        public void FreeingFromAPlaceTakesLonger()
        {
            var s = new CaptiveSettings { FreeTime = 4f };
            Assert.AreEqual(2f, CaptiveRules.FreeTime(CaptiveKind.None, 2f, s));
            Assert.AreEqual(4f, CaptiveRules.FreeTime(CaptiveKind.Prison, 2f, s));
            Assert.AreEqual(4f, CaptiveRules.FreeTime(CaptiveKind.Cocoon, 2f, s));
        }

        [Test]
        public void ALonePlayerComesRoundAFewTimes()
        {
            var s = new CaptiveSettings { SoloRescues = 2, SoloGetUpDelay = 10f, SoloEscapeDelay = 25f };
            Assert.AreEqual(10f, CaptiveRules.SoloRescueAfter(CaptiveKind.None, 1, 0, s));
            Assert.AreEqual(25f, CaptiveRules.SoloRescueAfter(CaptiveKind.Prison, 1, 1, s));
            Assert.Less(CaptiveRules.SoloRescueAfter(CaptiveKind.None, 1, 2, s), 0f, "the rescues are used up");
            Assert.Less(CaptiveRules.SoloRescueAfter(CaptiveKind.None, 2, 0, s), 0f, "in a team a teammate must come");
            Assert.Less(CaptiveRules.SoloRescueAfter(CaptiveKind.Carried, 1, 0, s), 0f, "not off a robot's back");
        }

        [Test]
        public void TheRunIsLostWhenNobodyComesBack()
        {
            Assert.IsFalse(CaptiveRules.Wiped(2, 1, 0, false), "one is still up");
            Assert.IsTrue(CaptiveRules.Wiped(2, 2, 0, true), "a team gets no rescue of its own");
            Assert.IsFalse(CaptiveRules.Wiped(2, 2, 1, false), "the sleeper in the ward wakes");
            Assert.IsFalse(CaptiveRules.Wiped(1, 1, 0, true), "a lone player with a rescue left comes round");
            Assert.IsTrue(CaptiveRules.Wiped(1, 1, 0, false));
            Assert.IsFalse(CaptiveRules.Wiped(0, 0, 0, false), "nobody in the session");
        }

        [Test]
        public void OnlyTheServantsCarryTheirVictimsAway()
        {
            Assert.IsTrue(CaptiveRules.Carried(DownCause.Servant));
            Assert.IsFalse(CaptiveRules.Carried(DownCause.Renegade));
            Assert.IsFalse(CaptiveRules.Carried(DownCause.Self), "a fall, the gas");
            Assert.AreEqual(CaptiveKind.Prison, HaulRules.PlaceOf(RobotKind.Guard));
        }

        [Test]
        public void ACarrierWalksLiftsCarriesAndPutsDown()
        {
            var step = HaulStep.Fetch;
            step = HaulRules.Next(step, liftable: true, held: false, near: false, timeUp: false);
            Assert.AreEqual(HaulStep.Fetch, step, "still walking to the body");
            step = HaulRules.Next(step, true, false, near: true, false);
            Assert.AreEqual(HaulStep.Lift, step);
            Assert.AreEqual(HaulStep.Lift, HaulRules.Next(step, true, false, true, timeUp: false), "lifting takes its time");
            step = HaulRules.Next(step, true, false, true, timeUp: true);
            Assert.AreEqual(HaulStep.Carry, step);
            Assert.AreEqual(HaulStep.Carry, HaulRules.Next(step, false, held: true, near: false, false));
            step = HaulRules.Next(step, false, true, near: true, false);
            Assert.AreEqual(HaulStep.Put, step);
            Assert.AreEqual(HaulStep.None, HaulRules.Next(step, false, true, true, timeUp: true), "put down: done");
        }

        [Test]
        public void ACarrierGivesUpWhenTheBodyIsGone()
        {
            Assert.AreEqual(HaulStep.None, HaulRules.Next(HaulStep.Fetch, liftable: false, false, false, false), "revived before it came");
            Assert.AreEqual(HaulStep.None, HaulRules.Next(HaulStep.Lift, liftable: false, false, true, false));
            Assert.AreEqual(HaulStep.None, HaulRules.Next(HaulStep.Carry, false, held: false, false, false), "the body was dropped");
        }

        [Test]
        public void EveryHouseHasAPrisonOffTheWayOut()
        {
            foreach (var l in TestHouses.Corpus(12))
            {
                Assert.GreaterOrEqual(l.PrisonRoom, 0, $"seed {l.Seed}: a room for the prisoners");
                Assert.Greater(l.PrisonSpots.Count, 0, $"seed {l.Seed}");
                Assert.AreNotEqual(l.EntryRoom, l.PrisonRoom, $"seed {l.Seed}: not where the team arrives");
                Assert.AreNotEqual(l.VaultRoom, l.PrisonRoom, $"seed {l.Seed}: not in the vault (shut to the robots)");
                foreach (var p in l.PrisonSpots)
                    Assert.AreEqual(l.PrisonRoom, l.RoomAt(p + Vector3.up * 0.3f)?.Id ?? -1, $"seed {l.Seed}: the spot {p} is in the prison's room");
            }
        }

        [Test]
        public void AMuddledHeadTurnsTheKeysRound()
        {
            var move = new Vector2(1f, 1f);
            Assert.AreEqual(move, AilmentRules.Move(move, Muddle.None));
            Assert.AreEqual(-move, AilmentRules.Move(move, Muddle.Inverted));
            Assert.AreEqual(new Vector2(-1f, 1f), AilmentRules.Move(move, Muddle.Mirrored));
            Assert.AreNotEqual(Muddle.None, AilmentRules.Pick(4));
            Assert.AreNotEqual(AilmentRules.Pick(4), AilmentRules.Pick(5), "both muddles come up");
        }

        [Test]
        public void GooSlowsNetsAndStunsHold()
        {
            var s = new AilmentSettings { GooSpeed = 0.5f };
            Assert.AreEqual(1f, AilmentRules.Speed(false, false, false, s));
            Assert.AreEqual(0.5f, AilmentRules.Speed(true, false, false, s));
            Assert.AreEqual(0f, AilmentRules.Speed(true, true, false, s));
            Assert.AreEqual(0f, AilmentRules.Speed(false, false, true, s));
        }

        [Test]
        public void APatchIsFeltInsideItOnItsFloorUntilItIsGone()
        {
            Hazards.Clear();
            Hazards.Add(HazardKind.Goo, new Vector3(5f, 0f, 5f), 2f, 100.0);
            Assert.IsTrue(Hazards.At(HazardKind.Goo, new Vector3(6f, 0.1f, 5f), 10.0));
            Assert.IsFalse(Hazards.At(HazardKind.Smoke, new Vector3(6f, 0.1f, 5f), 10.0), "another kind");
            Assert.IsFalse(Hazards.At(HazardKind.Goo, new Vector3(8f, 0f, 5f), 10.0), "outside it");
            Assert.IsFalse(Hazards.At(HazardKind.Goo, new Vector3(6f, 4f, 5f), 10.0), "the floor above");
            Assert.IsFalse(Hazards.At(HazardKind.Goo, new Vector3(6f, 0f, 5f), 100.0), "gone");
            Hazards.Expire(100.0);
            Assert.AreEqual(0, Hazards.All.Count);
        }

        [Test]
        public void AHeldPlayerReadsWhereTheyAre()
        {
            Assert.AreEqual("hud.out_cold", DownedRules.Key(CaptiveKind.None));
            foreach (CaptiveKind k in System.Enum.GetValues(typeof(CaptiveKind)))
                Assert.IsTrue(Loc.Has(DownedRules.Key(k)), $"{k}: {DownedRules.Key(k)} has its text");
        }
    }
}
