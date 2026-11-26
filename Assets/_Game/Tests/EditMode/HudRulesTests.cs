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
        public void TheScreenStaysBlackUntilThePlayerStandsInPlace()
        {
            Assert.IsTrue(LoadingRules.StillLoading(hasPlayer: false, placed: false, inSewer: false, carHere: false, waited: 0f), "no player yet");
            Assert.IsTrue(LoadingRules.StillLoading(true, placed: false, inSewer: true, carHere: true, waited: 0.5f), "still at the network's spawn point");
            Assert.IsTrue(LoadingRules.StillLoading(true, true, inSewer: true, carHere: false, waited: 0.5f), "the sewer without its car");
            Assert.IsFalse(LoadingRules.StillLoading(true, true, inSewer: true, carHere: true, waited: 0.5f), "in place by the car: open");
            Assert.IsFalse(LoadingRules.StillLoading(true, true, inSewer: false, carHere: false, waited: 0.5f), "a house needs no car");
            Assert.IsFalse(LoadingRules.StillLoading(false, false, true, false, LoadingRules.GiveUp + 0.1f), "a lost message never leaves it black");
        }

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
        public void TheNextHackSlotToGoIsOnTheLeft()
        {
            // two bolts; a robot hacked: bolt / robot; a camera: robot / camera; a sensor wakes the robot: camera / sensor
            CollectionAssert.AreEqual(new[] { -1, -1 }, HackSlotRules.Layout(2, new int[0]));
            CollectionAssert.AreEqual(new[] { -1, 0 }, HackSlotRules.Layout(2, new[] { 0 }));
            CollectionAssert.AreEqual(new[] { 0, 14 }, HackSlotRules.Layout(2, new[] { 0, 14 }));
            CollectionAssert.AreEqual(new[] { 14, 12 }, HackSlotRules.Layout(2, new[] { 14, 12 }));
            CollectionAssert.AreEqual(new[] { 14, 12 }, HackSlotRules.Layout(2, new[] { 0, 14, 12 }), "more than fit: the newest");
        }

        [Test]
        public void TheSafesLoudClickTowersOverAQuietOne()
        {
            Assert.AreEqual(1f, SafeScope.PulseHeight(1f), 1e-4f, "on the number: the scope's full height");
            Assert.Less(SafeScope.PulseHeight(0f), 0.1f, "far off: a ripple");
            Assert.Greater(SafeScope.PulseHeight(1f) / SafeScope.PulseHeight(0.5f), 3f, "half way there is still clearly lower");
            float peak = 0f;
            for (float u = 0f; u < 0.16f; u += 0.0005f) peak = Mathf.Max(peak, SafeScope.Ripple(u));
            Assert.AreEqual(1f, peak, 0.02f, "the ripple's first peak is the pulse's height");
        }

        [Test]
        public void TheHackingListScrollsWithThePickAndNumbersAKind()
        {
            Assert.AreEqual(0, HackListRules.FirstShown(5, 8, 12), "all fit: from the top");
            Assert.AreEqual(0, HackListRules.FirstShown(2, 30, 12));
            Assert.AreEqual(16, HackListRules.FirstShown(20, 30, 12), "the pick a few rows from the top");
            Assert.AreEqual(18, HackListRules.FirstShown(29, 30, 12), "at the end: the last rows");
            var kinds = new[] { 1, 1, 2, 3, 3, 3 };
            Assert.AreEqual("Охранник 2", HackListRules.Label("Охранник", kinds, 1, k => k));
            Assert.AreEqual("Дрон", HackListRules.Label("Дрон", kinds, 2, k => k), "one of its kind: no number");
            Assert.AreEqual("Пылесос 3", HackListRules.Label("Пылесос", kinds, 5, k => k));
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
        public void PuttingEverythingInIsAKeyInTheStackEvenWithTheCursorOut()
        {
            var c = Idle;
            c.NearTrade = true;
            c.CarriedItems = 3;
            c.CursorMode = true; // the backpack's board open under the cursor: it has no button for it any more
            var hints = Hints(c);
            var all = hints.FirstOrDefault(h => h.Key.Equals(Controls.Get(GameAction.StashAll)));
            Assert.AreEqual("сложить всё", all.Text, "[G] сложить всё");
            CollectionAssert.DoesNotContain(hints.Select(h => h.Key), Controls.Get(GameAction.Hands), "the cursor is out already");
            c.NearTrade = false;
            CollectionAssert.DoesNotContain(Hints(c).Select(h => h.Key), Controls.Get(GameAction.StashAll), "away from Linus");
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
            CollectionAssert.Contains(keys, Controls.Get(GameAction.Interact), "and stand up: the use key again");
            // (a tap of it; a hold is the hacking terminal's hack)
            Assert.IsTrue(TerminalRules.IsTap(0.1f));
            Assert.IsFalse(TerminalRules.IsTap(0f), "the key was not down");
            Assert.IsFalse(TerminalRules.IsTap(-1f), "that press sat us down, or hacked");
            Assert.IsFalse(TerminalRules.IsTap(TerminalRules.Tap + 0.2f), "a hold");
            var keyless = new PromptContext { CanHack = true, NeedsKeyboard = true };
            Assert.AreEqual(ReticleIcon.Hack, ReticleRules.Choose(keyless).Icon);
            Assert.IsFalse(ReticleRules.Choose(keyless).Available, "robots and sensors: not without the keyboard");
            // the upgraded screwdriver takes a robot or a sensor apart: not a hack — no keyboard, its own word
            var apart = new PromptContext { CanHack = true, NeedsKeyboard = true, Unscrew = true };
            Assert.IsTrue(ReticleRules.Choose(apart).Available, "taking apart needs no keyboard");
            CollectionAssert.Contains(Hints(apart).Select(h => h.Text), Loc.T("hint.take_apart"));
            CollectionAssert.DoesNotContain(Hints(apart).Select(h => h.Text), Loc.T("hint.hack"));
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
            var watching = Hints(c).ToList();
            Assert.AreEqual(2, watching.Count, "switch whose eyes, call for help");
            Assert.IsTrue(watching.Exists(h => h.Key.IsMouse), "switch whose eyes");
            Assert.IsTrue(watching.Exists(h => h.Key.Equals(Controls.Get(GameAction.CallHelp))), "call for help");
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
    
        [Test]
        public void TheTaskListPutsTheExitFirstAndTheSewersStepsInOrder()
        {
            Loc.Reload();
            var vault = new ObjectiveState { Kind = ObjectiveKind.Vault, Found = true, Solved = 1, Locks = 3 };
            var house = ObjectiveRules.Tasks(false, false, vault, false, false);
            Assert.AreEqual(2, house.Count);
            Assert.AreEqual(Loc.T("hud.task.exit"), house[0].text, "find the exit comes first");
            Assert.IsFalse(house[0].done);
            StringAssert.Contains(Loc.F("hud.job.vault_unlock", 1, 3), house[1].text, "then the main job's step");
            Assert.IsTrue(ObjectiveRules.Tasks(false, true, default, false, false).Single().done, "the exit seen: ticked; no job: one line");

            var shut = ObjectiveRules.Tasks(true, false, default, false, false);
            CollectionAssert.AreEqual(new[] { Loc.T("hud.task.gate"), Loc.T("hud.task.fuel") }, shut.Select(t => t.text), "open the way, then fuel the car");
            var ready = ObjectiveRules.Tasks(true, false, default, true, true);
            Assert.AreEqual(3, ready.Count, "both done: choose a tunnel");
            Assert.IsTrue(ready[0].done && ready[1].done && !ready[2].done);

            var start = ObjectiveRules.Tasks(true, false, default, true, false, runStart: true);
            CollectionAssert.AreEqual(new[] { Loc.T("hud.task.tunnel") }, start.Select(t => t.text), "the run's first sewer: only choose a tunnel");
        }

        [Test]
        public void TheMainJobShowsOneStepAtATime()
        {
            Loc.Reload();
            string Step(ObjectiveState s) => ObjectiveRules.Step(s)?.text;
            var v = new ObjectiveState { Kind = ObjectiveKind.Vault, Locks = 3 };
            Assert.AreEqual(Loc.T("hud.job.vault_find"), Step(v));
            v.Found = true;
            for (int k = 0; k < 3; k++) { v.Solved = k; Assert.AreEqual(Loc.F("hud.job.vault_unlock", k, 3), Step(v)); }
            v.Solved = 3;
            Assert.AreEqual(Loc.T("hud.job.vault_open"), Step(v), "all unlocked: go in");
            v.Done = true;
            Assert.IsTrue(ObjectiveRules.Step(v).Value.done);

            var h = new ObjectiveState { Kind = ObjectiveKind.Hostage };
            Assert.AreEqual(Loc.T("hud.job.hostage_find"), Step(h));
            h.KeyHeld = true;
            Assert.AreEqual(Loc.T("hud.job.hostage_find"), Step(h), "the key taken first: still find the hostage");
            h.KeyHeld = false; h.Found = true;
            Assert.AreEqual(Loc.T("hud.job.hostage_key"), Step(h), "spoken to: take the key");
            h.KeyHeld = true;
            Assert.AreEqual(Loc.T("hud.job.hostage_free"), Step(h));
            h.Done = true;
            Assert.IsTrue(ObjectiveRules.Step(h).Value.done);
            Assert.IsNull(ObjectiveRules.Step(default), "no job, no line");
        }
    }
}
