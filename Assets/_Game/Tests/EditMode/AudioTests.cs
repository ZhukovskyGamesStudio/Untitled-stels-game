using Stealth.Enemies;
using NUnit.Framework;
using Stealth.Audio;
using Stealth.Configs;
using UnityEditor;

namespace Stealth.Tests
{
    public class AudioTests
    {
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
