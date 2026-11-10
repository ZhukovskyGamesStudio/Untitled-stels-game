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
            c.CarriedItems = 2;
            StringAssert.Contains("люк", PromptRules.Choose(c).Text);
            c.CarriedItems = 0;
            c.Collected = 40;
            StringAssert.Contains("Уйти", PromptRules.Choose(c).Text);
            c.Collected = 100;
            Assert.AreEqual("Следующий дом", PromptRules.Choose(c).Text);
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
