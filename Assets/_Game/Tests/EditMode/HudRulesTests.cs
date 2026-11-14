using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using Stealth.Player;
using Stealth.UI;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>What the HUD decides to show: the prompt, banners, "+N", hit feedback.</summary>
    public class HudRulesTests
    {
        static PromptContext Idle => new() { Playing = true };

        [Test]
        public void StyleBannersReplaceEachOther()
        {
            var q = new BannerQueue();
            q.Push("Стиль: A", "style", 0f, 1.6f);
            q.Update(0f);
            Assert.AreEqual("Стиль: A", q.Current);
            q.Push("Стиль: B", "style", 0.2f, 1.6f);
            q.Push("Стиль: C", "style", 0.3f, 1.6f);
            Assert.AreEqual("Стиль: C", q.Current, "a quick F8 replaces the banner on screen");
            Assert.AreEqual(0, q.Waiting, "nothing piles up");
            Assert.AreEqual(1f, q.Alpha(0.3f), 1e-4f, "fully visible at once");
            q.Update(1.0f);
            Assert.AreEqual("Стиль: C", q.Current);
            q.Update(2.0f);
            Assert.IsNull(q.Current, "gone soon after the last press");

            q.Push("Квота выплачена");
            q.Update(3f);
            q.Push("Стиль: D", "style", 3.1f, 1.6f);
            q.Push("Стиль: E", "style", 3.2f, 1.6f);
            Assert.AreEqual(1, q.Waiting, "behind another banner only the last style waits");
        }

        [Test]
        public void TheHunterIsNeverASurprise()
        {
            var (text, urgency) = HunterRules.Line(250.2f, 3, 0);
            Assert.AreEqual("Охотник через 4:11 · тревог: 3", text);
            Assert.AreEqual(HunterRules.Urgency.Calm, urgency);
            Assert.AreEqual(HunterRules.Urgency.Soon, HunterRules.Line(59f, 3, 0).urgency, "the last minute");
            Assert.AreEqual(HunterRules.Urgency.Soon, HunterRules.Line(300f, 1, 0).urgency, "one more alarm brings it");
            Assert.AreEqual(HunterRules.Urgency.Inside, HunterRules.Line(-1f, -1, 1).urgency);
            Assert.IsNull(HunterRules.Line(-1f, -1, 0).text, "no more hunters in this house: no line");
        }

        [Test]
        public void HunterForecastCountsDownToTheRule()
        {
            var s = new Stealth.Game.RunSettings();
            var (seconds, alarms) = Stealth.Game.RunState.HunterForecast(1, 100f, 0, 0, s);
            Assert.AreEqual(s.HunterTime - 100f, seconds, 1e-3f);
            Assert.AreEqual(s.HunterAlarmsFirstHouse - 1, alarms);
            Assert.IsFalse(Stealth.Game.RunState.HunterDue(1, 100f, 0, 0, s));
            Assert.IsTrue(Stealth.Game.RunState.HunterDue(s.HunterAlarmsFirstHouse, 100f, 0, 0, s), "enough alarms");
            Assert.IsTrue(Stealth.Game.RunState.HunterDue(0, s.HunterTime, 0, 0, s), "lingered too long");
            Assert.AreEqual(-1, Stealth.Game.RunState.HunterForecast(0, 0f, 0, 1, s).alarms, "the only hunter of the first house came");
        }

        static List<Hint> Hints(in PromptContext c) => HintRules.Collect(c, new List<Hint>());

        [Test]
        public void NothingToDoADotAndNoHints()
        {
            var r = ReticleRules.Choose(Idle);
            Assert.IsTrue(r.Visible);
            Assert.AreEqual(ReticleIcon.Dot, r.Icon);
            CollectionAssert.IsEmpty(Hints(Idle), "no text on the screen when there is nothing to do");
        }

        [Test]
        public void ReviveBeatsEverythingAndShowsTheHold()
        {
            var c = Idle;
            c.CanRevive = true;
            c.ReviveProgress = 0.3f;
            c.CanHack = true;
            c.HoverItem = true;
            var r = ReticleRules.Choose(c);
            Assert.AreEqual(ReticleIcon.Revive, r.Icon);
            Assert.AreEqual(0.3f, r.Progress, 1e-4f, "a ring fills while E is held");
            var last = Hints(c)[^1];
            Assert.AreEqual(Controls.Get(GameAction.Interact), last.Key, "the key is drawn, not written");
            Assert.AreEqual("поднять", last.Text, "one short word");
        }

        [Test]
        public void ThingsToUseShowTheirGlyphAndDimWhenNotPossible()
        {
            var c = Idle;
            c.HasUse = true;
            c.UseKind = UseKind.KeyDoor;
            c.UseText = "нужен ключ";
            c.UseAvailable = false;
            var r = ReticleRules.Choose(c);
            Assert.AreEqual(ReticleIcon.Key, r.Icon);
            Assert.IsFalse(r.Available, "dimmed: no key");
            Assert.IsTrue(Hints(c)[^1].Dim);
            c.UseKind = UseKind.CarHopper;
            c.UseAvailable = true;
            Assert.AreEqual(ReticleIcon.Fuel, ReticleRules.Choose(c).Icon);
            foreach (UseKind k in System.Enum.GetValues(typeof(UseKind)))
                Assert.AreNotEqual(ReticleIcon.Dot, ReticleRules.IconOf(k), $"{k} has a glyph");
        }

        [Test]
        public void TheHatchAndTheStashHintTheirKeys()
        {
            var c = Idle;
            c.NearHatch = true;
            c.NearTrade = true;
            c.CarriedItems = 2;
            var keys = Hints(c).Select(h => h.Key).ToList();
            CollectionAssert.Contains(keys, Controls.Get(GameAction.Hands));
            CollectionAssert.Contains(keys, Controls.Get(GameAction.StashAll));
            Assert.AreEqual(Controls.Get(GameAction.Hatch), Hints(c)[^1].Key, "into the hatch: the nearest thing to do");
            c.InHatch = true;
            Assert.IsFalse(ReticleRules.Choose(c).Visible, "no reticle while hiding");
            Assert.AreEqual("вылезти", Hints(c)[^1].Text);
            c.CarriedItems = 0;
            CollectionAssert.DoesNotContain(Hints(c).Select(h => h.Key), Controls.Get(GameAction.StashAll), "nothing to stash");
        }

        [Test]
        public void DrivingHintsOnlyWhatIsNotObvious()
        {
            var c = Idle;
            c.Driving = true;
            var hints = Hints(c);
            CollectionAssert.AreEquivalent(new[] { Controls.Get(GameAction.Horn), Controls.Get(GameAction.Interact) }, hints.Select(h => h.Key));
            CollectionAssert.DoesNotContain(hints.Select(h => h.Key), Controls.Get(GameAction.Forward), "no WASD — that is obvious");
            c.Driving = false;
            c.Passenger = true;
            Assert.AreEqual(1, Hints(c).Count, "a passenger only gets out");
        }

        [Test]
        public void DoorsSafesAndTerminals()
        {
            var c = Idle;
            c.LockedDoor = true;
            c.DoorProgress = 0.4f;
            var r = ReticleRules.Choose(c);
            Assert.AreEqual(ReticleIcon.Lock, r.Icon);
            Assert.AreEqual(0.4f, r.Progress, 1e-4f);
            c.LockedDoor = false;
            c.SafeNear = true;
            Assert.AreEqual(ReticleIcon.Safe, ReticleRules.Choose(c).Icon);
            c.Cracking = true;
            Assert.IsFalse(ReticleRules.Choose(c).Visible, "the dial has its own screen");
            CollectionAssert.IsEmpty(Hints(c));
            var t = new PromptContext { AtTerminal = true, SittingKind = TerminalKind.Cameras };
            var keys = Hints(t).Select(h => h.Key).ToList();
            CollectionAssert.Contains(keys, Controls.Get(GameAction.Mark), "cameras: mark a robot");
            CollectionAssert.Contains(keys, Controls.Get(GameAction.Jump), "and stand up");
        }

        [Test]
        public void ItemsAreTakenOnlyOutsideHandsMode()
        {
            var c = Idle;
            c.HoverItem = true;
            Assert.AreEqual(ReticleIcon.Hand, ReticleRules.Choose(c).Icon);
            Assert.IsTrue(Hints(c)[^1].Key.IsMouse);
            c.CursorMode = true;
            Assert.IsFalse(ReticleRules.Choose(c).Visible);
            CollectionAssert.Contains(Hints(c).Select(h => h.Key), Controls.Get(GameAction.Rotate), "hands out: rotate and mirror");
        }

        [Test]
        public void LyingDown()
        {
            var c = new PromptContext { Downed = true, Hp = 1, CanGetUp = true };
            Assert.IsFalse(ReticleRules.Choose(c).Visible);
            Assert.AreEqual(Controls.Get(GameAction.Jump), Hints(c).Single().Key);
            c.Hp = 0;
            c.CanGetUp = false;
            CollectionAssert.IsEmpty(Hints(c), "out cold: nothing to press");
            c.Watching = true;
            Assert.IsTrue(Hints(c).Single().Key.IsMouse, "switch whose eyes");
            var menu = Idle;
            menu.MenuOpen = true;
            menu.CanRevive = true;
            Assert.IsFalse(ReticleRules.Choose(menu).Visible);
            CollectionAssert.IsEmpty(Hints(menu));
        }

        [Test]
        public void BannersQueueUpWithoutRepeats()
        {
            var q = new BannerQueue();
            q.Push("A");
            q.Push("A");
            q.Push("B");
            q.Update(0f);
            Assert.AreEqual("A", q.Current);
            Assert.AreEqual(1, q.Waiting, "the duplicate was dropped");
            q.Update(1f);
            Assert.AreEqual("A", q.Current, "shown for its whole duration");
            q.Update(BannerQueue.Duration + 0.01f);
            Assert.AreEqual("B", q.Current);
            q.Update(2 * BannerQueue.Duration + 0.1f);
            Assert.IsNull(q.Current);
            Assert.AreEqual(0f, q.Alpha(100f));
        }

        [Test]
        public void BannerQueueIsBounded()
        {
            var q = new BannerQueue();
            for (int i = 0; i < 10; i++) q.Push($"m{i}");
            Assert.AreEqual(BannerQueue.Capacity, q.Waiting);
        }

        [Test]
        public void QuickDepositsAddUp()
        {
            var g = new GainTracker();
            g.Observe(0, 0f);
            Assert.IsFalse(g.Visible(0f), "nothing deposited yet");
            g.Observe(20, 1f);
            g.Observe(35, 2f);
            Assert.AreEqual(35, g.Amount);
            g.Observe(45, 10f);
            Assert.AreEqual(10, g.Amount, "a later deposit starts a new sum");
            Assert.IsTrue(g.Visible(11f));
            Assert.IsFalse(g.Visible(12.5f));
        }

        [Test]
        public void HitArcPointsToTheAttackerAndFades()
        {
            var h = new HitFeedback();
            h.Hit(Vector3.left, 5f);
            Assert.AreEqual(Vector3.right, h.Direction, "towards the source");
            Assert.AreEqual(1f, h.ArcAlpha(5f));
            Assert.AreEqual(0f, h.ArcAlpha(5f + HitFeedback.ArcTime + 0.1f));
            h.Update(1f);
            Assert.Less(h.Flash, 1f);
        }
    }
}
