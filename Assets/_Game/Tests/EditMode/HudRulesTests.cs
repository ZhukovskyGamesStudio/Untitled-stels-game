using NUnit.Framework;
using Stealth.UI;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>What the HUD decides to show: the prompt, banners, "+N", hit feedback.</summary>
    public class HudRulesTests
    {
        static PromptContext Idle => new() { Playing = true, Quota = 100 };

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
            Assert.AreEqual("Охотник через 4:11 или после 3 тревог", text);
            Assert.AreEqual(HunterRules.Urgency.Calm, urgency);
            Assert.AreEqual(HunterRules.Urgency.Soon, HunterRules.Line(59f, 3, 0).urgency, "the last minute");
            Assert.AreEqual(HunterRules.Urgency.Soon, HunterRules.Line(300f, 1, 0).urgency, "one more alarm brings it");
            Assert.AreEqual("Охотник через 5:00 или после 1 тревоги", HunterRules.Line(300f, 1, 0).text);
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

        [Test]
        public void NothingToSayNoPrompt()
        {
            Assert.IsFalse(PromptRules.Choose(Idle).IsValid);
        }

        [Test]
        public void ReviveBeatsEverything()
        {
            var c = Idle;
            c.CanRevive = true;
            c.HackTarget = "Турель";
            c.NearHatch = true;
            c.HoverItem = "Красный";
            var p = PromptRules.Choose(c);
            Assert.AreEqual("E", p.Key);
            StringAssert.Contains("поднять", p.Text);
            Assert.GreaterOrEqual(p.Progress, 0f, "hold actions show a bar");
        }

        [Test]
        public void HatchPromptDependsOnCargoAndQuota()
        {
            var c = Idle;
            c.NearHatch = true;
            c.NearTrade = true;
            c.CarriedItems = 2;
            StringAssert.Contains("Разложить", PromptRules.Choose(c).Text);
            c.CarriedItems = 0;
            c.Collected = 40;
            StringAssert.Contains("доплатить", PromptRules.Choose(c).Text, "leaving without the quota is allowed: it is paid below");
            c.Collected = 100;
            StringAssert.Contains("канализацию", PromptRules.Choose(c).Text);
            c.InHatch = true;
            Assert.AreEqual("Вылезти из люка", PromptRules.Choose(c).Text);
        }

        [Test]
        public void DoorsSafesAndTheSewer()
        {
            var c = Idle;
            c.LockedDoor = true;
            c.DoorProgress = 0.4f;
            var p = PromptRules.Choose(c);
            StringAssert.Contains("замок", p.Text);
            Assert.AreEqual(0.4f, p.Progress, 1e-4f);
            c.LockedDoor = false;
            c.SafeNear = true;
            StringAssert.Contains("сейф", PromptRules.Choose(c).Text);
            c.Cracking = true;
            Assert.IsFalse(PromptRules.Choose(c).IsValid, "the dial has its own screen");
            var s = new PromptContext { InSewer = true, Quota = 100 };
            StringAssert.Contains("туннел", PromptRules.Choose(s).Text);
        }

        [Test]
        public void ItemPromptOnlyOutsideHandsMode()
        {
            var c = Idle;
            c.HoverItem = "Синий";
            Assert.AreEqual("ЛКМ", PromptRules.Choose(c).Key);
            c.CursorMode = true;
            Assert.IsFalse(PromptRules.Choose(c).IsValid);
        }

        [Test]
        public void RobotInfoYieldsToActions()
        {
            var c = Idle;
            c.LookedAt = "Охранник: глаза";
            Assert.AreEqual("Охранник: глаза", PromptRules.Choose(c).Text);
            c.HoverItem = "Синий";
            StringAssert.Contains("Взять", PromptRules.Choose(c).Text);
        }

        [Test]
        public void NoPromptsWhenDownedOrInTheMenu()
        {
            var c = Idle;
            c.CanRevive = true;
            c.Downed = true;
            Assert.IsFalse(PromptRules.Choose(c).IsValid);
            c.Downed = false;
            c.MenuOpen = true;
            Assert.IsFalse(PromptRules.Choose(c).IsValid);
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
