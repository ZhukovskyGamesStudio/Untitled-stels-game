using Stealth.Enemies;
using NUnit.Framework;
using UnityEngine;
using Stealth.Audio;
using Stealth.Configs;
using UnityEditor;

namespace Stealth.Tests
{
    public class AudioTests
    {
        /// <summary>Feeds a height track (one value per 60 fps frame) and counts the events.</summary>
        static (int jumps, int lands) Fly(params float[] heights)
        {
            var air = new AirState();
            int jumps = 0, lands = 0;
            const float dt = 1f / 60f;
            for (int i = 1; i < heights.Length; i++)
            {
                var e = air.Step((heights[i] - heights[i - 1]) / dt, dt, i * dt);
                if (e == AirEvent.Jump) jumps++;
                if (e == AirEvent.Land) lands++;
            }
            return (jumps, lands);
        }

        static float[] Track(float start, float v0, float floor, int frames = 120)
        {
            var h = new float[frames];
            float y = start, v = v0;
            for (int i = 0; i < frames; i++)
            {
                h[i] = y;
                v -= 9.81f / 60f;
                float next = y + v / 60f;
                // the surface catches the body coming down onto it (a desk: once it is above the desk's top)
                if (v < 0f && y >= floor && next <= floor) { next = floor; v = 0f; }
                if (v == 0f && y == floor) next = floor;
                y = next;
            }
            return h;
        }

        [Test]
        public void TheGasMakesYouBreatheHardThenLaughEverHarder()
        {
            const float laughAt = 0.32f;
            Assert.AreEqual(-2, PeopleSounds.Gas(0f, laughAt).clip, "clear air: silence");
            var breath = PeopleSounds.Gas(0.2f, laughAt);
            Assert.AreEqual(-1, breath.clip, "first heavy breathing");
            Assert.AreEqual(0, PeopleSounds.Gas(0.4f, laughAt).clip, "then a giggle");
            Assert.AreEqual(1, PeopleSounds.Gas(0.65f, laughAt).clip, "a laugh");
            var fit = PeopleSounds.Gas(0.95f, laughAt);
            Assert.AreEqual(2, fit.clip, "a helpless fit near passing out");
            Assert.Greater(fit.volume, PeopleSounds.Gas(0.4f, laughAt).volume, "louder");
            Assert.Less(fit.gap, PeopleSounds.Gas(0.4f, laughAt).gap, "and more often");
        }

        [Test]
        public void TheHostageCallsForHelpFirstAndNeverRepeatsItself()
        {
            Assert.AreEqual(0, PeopleSounds.HostageLine(0, -1, false, 7), "a call for help first");
            int last = 0;
            for (int i = 1; i < 30; i++)
            {
                int line = PeopleSounds.HostageLine(i, last, i > 15, i * 37);
                Assert.AreNotEqual(last, line, "never the same line twice in a row");
                if (i > 15) Assert.AreNotEqual(1, line, "no talk of the key once somebody has it");
                last = line;
            }
            Assert.IsFalse(PeopleSounds.HostageSpeaks(5f, false, 10f, 0f), "not seen: quiet");
            Assert.IsFalse(PeopleSounds.HostageSpeaks(PeopleSounds.HostageReach + 1f, true, 10f, 0f), "too far");
            Assert.IsFalse(PeopleSounds.HostageSpeaks(5f, true, 10f, 12f), "its last line is still sinking in");
            Assert.IsTrue(PeopleSounds.HostageSpeaks(5f, true, 10f, 9f));
        }

        [Test]
        public void JumpsAndLandingsAreHeardStepsUpAndJitterAreNot()
        {
            Assert.AreEqual((1, 1), Fly(Track(0f, 4.2f, 0f)), "a jump: one whoosh, one thud");
            Assert.AreEqual((1, 1), Fly(Track(0f, 4.6f, 0.92f)), "a jump onto a desk: the same");
            Assert.AreEqual((0, 1), Fly(Track(0.92f, 0f, 0f)), "stepping off a desk: a thud, no jump");
            var step = new float[40];
            for (int i = 0; i < step.Length; i++) step[i] = i < 20 ? 0f : 0.35f;
            Assert.AreEqual((0, 0), Fly(step), "a step up (one frame): nothing");
            var edge = new float[60];
            for (int i = 0; i < edge.Length; i++) edge[i] = 0.92f + (i % 2 == 0 ? 0f : 0.04f);
            Assert.AreEqual((0, 0), Fly(edge), "jitter on a furniture edge: nothing");
        }

        [Test]
        public void MusicFollowsTheDanger()
        {
            Assert.AreEqual(MusicTrack.Menu, AudioRules.ChooseMusic(false, 0f, 5f));
            Assert.AreEqual(MusicTrack.Chase, AudioRules.ChooseMusic(true, 0f, 5f), "chased right now");
            Assert.AreEqual(MusicTrack.Chase, AudioRules.ChooseMusic(true, 4f, 5f), "a moment after the chase");
            Assert.AreEqual(MusicTrack.Explore, AudioRules.ChooseMusic(true, 6f, 5f));
            Assert.AreEqual(MusicTrack.Explore, AudioRules.ChooseMusic(true, float.PositiveInfinity, 5f));
        }

        [Test]
        public void StepsQuickenWithSpeedAndHushWhenCrouching()
        {
            var mix = new AudioMix();
            Assert.IsTrue(float.IsPositiveInfinity(AudioRules.StepInterval(0.1f, mix.StepLength)), "standing still");
            Assert.Less(AudioRules.StepInterval(6f, mix.StepLength), AudioRules.StepInterval(3f, mix.StepLength));
            Assert.Less(AudioRules.StepVolume(2f, true, mix), AudioRules.StepVolume(3f, false, mix));
            Assert.Less(AudioRules.StepVolume(3f, false, mix), AudioRules.StepVolume(7f, false, mix));
        }

        [Test]
        public void DetectorsBeepWhenTheyNoticeAndWhenTheyGiveUp()
        {
            Assert.AreEqual(0, AudioRules.AlertLevel(0.1f, false, false));
            Assert.AreEqual(1, AudioRules.AlertLevel(0.5f, false, false));
            Assert.AreEqual(1, AudioRules.AlertLevel(0f, true, false));
            Assert.AreEqual(2, AudioRules.AlertLevel(0.2f, false, true));
            Assert.AreEqual(AlertCue.Suspicious, AudioRules.Cue(0, 1));
            Assert.AreEqual(AlertCue.Alert, AudioRules.Cue(1, 2));
            Assert.AreEqual(AlertCue.Alert, AudioRules.Cue(0, 2));
            Assert.AreEqual(AlertCue.Lost, AudioRules.Cue(2, 0));
            Assert.AreEqual(AlertCue.None, AudioRules.Cue(2, 1), "calming down a step is quiet");
            Assert.AreEqual(AlertCue.None, AudioRules.Cue(1, 1));
        }

        [Test]
        public void SoundBankHasEveryClip()
        {
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>("Assets/_Game/Data/SoundBank.asset");
            Assert.IsNotNull(bank);
            var so = new SerializedObject(bank);
            var p = so.GetIterator();
            while (p.NextVisible(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference || p.name == "m_Script") continue;
                Assert.IsNotNull(p.objectReferenceValue, $"SoundBank.{p.propertyPath}");
            }
            Assert.AreEqual(4, bank.Footsteps.Length);
        }

        [Test]
        public void ACameraBeepsFasterAsItNoticesYou()
        {
            Assert.IsTrue(float.IsInfinity(AudioRules.CameraBeepInterval(0f)), "silent while it notices nobody");
            Assert.Greater(AudioRules.CameraBeepInterval(0.2f), AudioRules.CameraBeepInterval(0.6f));
            Assert.Greater(AudioRules.CameraBeepInterval(0.6f), AudioRules.CameraBeepInterval(0.95f));
            Assert.Less(AudioRules.CameraBeepInterval(0.95f), 0.15f, "a trill just before the alarm");
            Assert.IsTrue(float.IsInfinity(AudioRules.CameraBeepInterval(1f)), "the siren takes over");
            Assert.Greater(AudioRules.CameraBeepPitch(0.9f), AudioRules.CameraBeepPitch(0.1f));
        }

        [Test]
        public void RobotsWarnThenAnnounceTheChaseAndTheLoss()
        {
            Assert.AreEqual(RobotVoice.Spotted, AudioRules.Voice(RobotState.Suspicious, RobotState.Alerted));
            Assert.AreEqual(RobotVoice.Spotted, AudioRules.Voice(RobotState.Patrol, RobotState.Alerted));
            Assert.AreEqual(RobotVoice.Chase, AudioRules.Voice(RobotState.Alerted, RobotState.Chase));
            Assert.AreEqual(RobotVoice.Lost, AudioRules.Voice(RobotState.Chase, RobotState.Investigate));
            Assert.AreEqual(RobotVoice.None, AudioRules.Voice(RobotState.Patrol, RobotState.Investigate), "checking a noise is silent");
            Assert.AreEqual(RobotVoice.Eliminated, AudioRules.Voice(RobotState.Chase, RobotState.Patrol), "target knocked down");
            Assert.AreEqual(RobotVoice.None, AudioRules.Voice(RobotState.Alerted, RobotState.Alerted));
        }
    }
}
