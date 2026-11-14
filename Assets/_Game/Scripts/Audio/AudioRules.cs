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
        // Levels measured on the clips (loudest 20 % of 50 ms windows): the "?"/"!" stingers are ~-4 dB, the robot
        // voices ~-15 dB, steps ~-14 dB. The stingers are played lower so the voice phrase is what you hear.
        /// <summary>Volume of the "?", "!" and "lost" stingers of devices.</summary>
        public const float StingerVolume = 0.35f;
        /// <summary>Volume of the power-down of a hacked robot / device.</summary>
        public const float HackedVolume = 0.45f;
        /// <summary>Volume of a camera's beeping while it notices someone.</summary>
        public const float CameraBeepVolume = 0.5f;

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

        /// <summary>
        /// The share of a sound that gets through the floors between the source and the listener: 1 on the same floor,
        /// <paramref name="throughFloor"/> per slab in between (heights measured from the floor of the house).
        /// </summary>
        public static float FloorFactor(float listenerY, float sourceY, float throughFloor)
        {
            int a = Mathf.FloorToInt((listenerY + 0.2f) / Stealth.Level.LevelMetrics.FloorHeight);
            int b = Mathf.FloorToInt((sourceY + 0.2f) / Stealth.Level.LevelMetrics.FloorHeight);
            return Mathf.Pow(Mathf.Clamp01(throughFloor), Mathf.Abs(a - b));
        }

        /// <summary>Every kind of robot speaks in its own voice: the same phrases, pitched to its size.</summary>
        public static float VoicePitch(Stealth.Level.RobotKind kind) => kind switch
        {
            Stealth.Level.RobotKind.Hunter => 0.78f,
            Stealth.Level.RobotKind.Thumper => 0.84f,
            Stealth.Level.RobotKind.Listener => 0.92f,
            Stealth.Level.RobotKind.Sorter => 1.08f,
            Stealth.Level.RobotKind.Hound => 1.14f,
            Stealth.Level.RobotKind.Worker => 1.2f,
            Stealth.Level.RobotKind.Sweeper => 1.26f,
            Stealth.Level.RobotKind.Drone => 1.32f,
            _ => 1f,
        };

        public static AlertCue Cue(int from, int to) =>
            to > from ? (to == 2 ? AlertCue.Alert : AlertCue.Suspicious) : to == 0 && from > 0 ? AlertCue.Lost : AlertCue.None;
    }
}
