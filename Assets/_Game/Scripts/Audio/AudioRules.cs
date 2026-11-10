using Stealth.Configs;
using Stealth.Enemies;
using UnityEngine;

namespace Stealth.Audio
{
    public enum MusicTrack : byte { None, Menu, Explore, Chase }

    /// <summary>What a mobile robot says when its state changes.</summary>
    public enum RobotVoice : byte { None, Spotted, Chase, Lost, Eliminated }

    /// <summary>What a detector's change of alertness sounds like.</summary>
    public enum AlertCue : byte { None, Suspicious, Alert, Lost }

    /// <summary>Decisions of the sound director (pure, unit tested).</summary>
    public static class AudioRules
    {
        /// <summary>Menu music outside a game; in a game — exploring, or the chase track while in danger and a bit after.</summary>
        public static MusicTrack ChooseMusic(bool inGame, float secondsSinceDanger, float chaseHold) =>
            !inGame ? MusicTrack.Menu : secondsSinceDanger <= chaseHold ? MusicTrack.Chase : MusicTrack.Explore;

        /// <summary>Seconds between footsteps at <paramref name="speed"/> m/s; infinity when standing.</summary>
        public static float StepInterval(float speed, float stepLength) =>
            speed < 0.6f ? float.PositiveInfinity : Mathf.Clamp(stepLength / speed, 0.18f, 0.9f);

        public static float StepVolume(float speed, bool crouching, AudioMix mix) =>
            crouching ? mix.CrouchStep : speed >= mix.RunSpeed ? mix.RunStep : mix.WalkStep;

        /// <summary>0 calm, 1 suspicious / investigating, 2 chasing / alarmed.</summary>
        public static int AlertLevel(float awareness, bool investigating, bool chasing) =>
            chasing || awareness >= 0.999f ? 2 : investigating || awareness >= 0.35f ? 1 : 0;

        /// <summary>
        /// «Обнаружен нарушитель» on noticing, «Веду преследование» when the chase starts, «Нарушитель потерян» when it gives up,
        /// «Нарушитель устранён» when it has knocked its target down.
        /// </summary>
        public static RobotVoice Voice(RobotState from, RobotState to) =>
            from == to ? RobotVoice.None
            : to == RobotState.Alerted ? RobotVoice.Spotted
            : to == RobotState.Chase ? RobotVoice.Chase
            : from == RobotState.Chase && to == RobotState.Investigate ? RobotVoice.Lost
            : from == RobotState.Chase && to == RobotState.Patrol ? RobotVoice.Eliminated // its target is lying on the floor
            : RobotVoice.None;

        /// <summary>
        /// A camera noticing someone beeps: slow at first, faster and faster as its suspicion fills (a second apart
        /// at the start, a rapid trill just before the alarm). Infinity when it notices nobody.
        /// </summary>
        public static float CameraBeepInterval(float awareness) =>
            awareness < 0.05f || awareness >= 0.999f ? float.PositiveInfinity : Mathf.Lerp(0.9f, 0.07f, Mathf.Pow(awareness, 0.8f));

        /// <summary>The beep rises in pitch as it speeds up.</summary>
        public static float CameraBeepPitch(float awareness) => Mathf.Lerp(0.9f, 1.35f, Mathf.Clamp01(awareness));

        public static AlertCue Cue(int from, int to) =>
            to > from ? (to == 2 ? AlertCue.Alert : AlertCue.Suspicious) : to == 0 && from > 0 ? AlertCue.Lost : AlertCue.None;
    }
}
