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
        public static MusicTrack ChooseMusic(bool inGame, float secondsSinceDanger, float chaseHold, bool levelMusic = true) =>
            !inGame ? MusicTrack.Menu : !levelMusic ? MusicTrack.None : secondsSinceDanger <= chaseHold ? MusicTrack.Chase : MusicTrack.Explore;

        /// <summary>Rungs of a ladder are this far apart, metres: robots hear a noise on each (people hear the climber's own feet and hands: HumanMath.ClimbContacts).</summary>
        public const float RungSpacing = 0.3f;

        /// <summary>
        /// Rungs passed by a climb of <paramref name="dy"/> metres, up or down: the height carried over from before is
        /// added, what is left of a rung is kept for the next call (hanging still passes none).
        /// </summary>
        public static int Rungs(ref float carried, float dy, float spacing = RungSpacing)
        {
            carried += Mathf.Abs(dy);
            int n = Mathf.FloorToInt(carried / spacing);
            carried -= n * spacing;
            return Mathf.Min(n, 3); // (a teleport is no drum roll)
        }

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
            Stealth.Level.RobotKind.Thumper => 0.84f,
            Stealth.Level.RobotKind.Listener => 0.92f,
            Stealth.Level.RobotKind.Sorter => 1.08f,
            Stealth.Level.RobotKind.Worker => 1.2f,
            Stealth.Level.RobotKind.Sweeper => 1.26f,
            Stealth.Level.RobotKind.Drone => 1.32f,
            _ => 1f,
        };

        public static AlertCue Cue(int from, int to) =>
            to > from ? (to == 2 ? AlertCue.Alert : AlertCue.Suspicious) : to == 0 && from > 0 ? AlertCue.Lost : AlertCue.None;
    }

    /// <summary>The voices of people: a player in the laughing gas, the hostage (pure).</summary>
    public static class PeopleSounds
    {
        /// <summary>The recorded voice's pitch for a person (pure): one man's coughs and groans, higher for the tester and the designer.</summary>
        public static float VoicePitch(Stealth.Player.Department who) => who switch
        {
            Stealth.Player.Department.Qa => 1.2f,
            Stealth.Player.Department.Sysadmin => 0.92f,
            Stealth.Player.Department.Designer => 1.24f,
            _ => 1f,
        };

        /// <summary>
        /// A player in the gas: <paramref name="share"/> of the way to passing out, coughing from <paramref name="laughAt"/>.
        /// First heavy breathing, then the coughing — a couple, hard ones, a fit — ever louder and more often.
        /// <c>clip</c> −2 silence, −1 a breath, 0..2 a cough; <c>gap</c> the pause after it (seconds).
        /// </summary>
        public static (int clip, float volume, float gap) Gas(float share, float laughAt)
        {
            if (share <= 0.04f) return (-2, 0f, 1f);
            laughAt = Mathf.Clamp(laughAt, 0.05f, 0.95f);
            if (share < laughAt)
            {
                float b = share / laughAt;
                return (-1, 0.3f + 0.45f * b, Mathf.Lerp(0.9f, 0.25f, b));
            }
            float t = Mathf.Clamp01((share - laughAt) / (1f - laughAt));
            int level = t < 0.34f ? 0 : t < 0.7f ? 1 : 2;
            return (level, 0.45f + 0.55f * t, Mathf.Lerp(1.6f, 0.2f, t));
        }

        /// <summary>How near the hostage must be (and in plain sight) to call to a player, metres.</summary>
        public const float HostageReach = 11f;

        /// <summary>The hostage speaks now: a player sees it close enough and its last line has had time to sink in.</summary>
        public static bool HostageSpeaks(float distance, bool inSight, float now, float next) => inSight && distance <= HostageReach && now >= next;

        /// <summary>
        /// Which line next (0 "help!", 1 "the guard has the key", 2 "get me out", 3 "quiet", 4 "hurry"): the call for help
        /// first; then the key while nobody carries it, the others at random, never the same twice in a row.
        /// </summary>
        public static int HostageLine(int said, int last, bool keyHeld, int roll)
        {
            if (said == 0) return 0;
            int[] pool = keyHeld ? new[] { 2, 3, 4 } : new[] { 1, 1, 2, 3, 4 };
            int k = Mathf.Abs(roll) % pool.Length;
            for (int step = 0; step < pool.Length; step++)
                if (pool[(k + step) % pool.Length] != last) return pool[(k + step) % pool.Length];
            return pool[k];
        }
    }

    /// <summary>What a body's vertical motion just did, for its jump and landing sounds.</summary>
    public enum AirEvent { None, Jump, Land }

    /// <summary>
    /// Jumps and landings from a body's height alone (pure; every peer sees only the replicated position): a jump is a
    /// climb that lasts, a fall is a drop that lasts, a landing ends a flight that lasted. A step up onto a desk or a
    /// stair (the controller lifts the body in one frame) and the jitter on a piece of furniture's edge are none of them —
    /// they sounded as a jump and a thud every time a player climbed on furniture or stepped off it.
    /// </summary>
    public sealed class AirState
    {
        /// <summary>Climbing or dropping must last this long to count (a step-up is one or two frames).</summary>
        public const float Hold = 0.08f;
        /// <summary>A flight shorter than this lands without a sound.</summary>
        public const float MinFlight = 0.18f;
        const float Rise = 1.8f, Drop = -2.5f, Still = 0.6f;

        public bool Airborne { get; private set; }
        float m_Rising, m_Dropping, m_FlightStart, m_Still;
        bool m_Fell; // came down during this flight (the top of a jump is still too, but not a landing)

        /// <summary>One frame: <paramref name="vy"/> the vertical speed this frame (m/s), <paramref name="now"/> the time.</summary>
        public AirEvent Step(float vy, float dt, float now)
        {
            m_Rising = vy > Rise ? m_Rising + dt : 0f;
            m_Dropping = vy < Drop ? m_Dropping + dt : 0f;
            if (!Airborne)
            {
                if (m_Rising >= Hold) { Airborne = true; m_Fell = false; m_FlightStart = now - m_Rising; m_Still = 0f; return AirEvent.Jump; }
                if (m_Dropping >= Hold) { Airborne = true; m_Fell = true; m_FlightStart = now - m_Dropping; m_Still = 0f; }
                return AirEvent.None;
            }
            if (vy < -1f) m_Fell = true;
            // down again: the height has stopped changing for a couple of frames after coming down
            m_Still = Mathf.Abs(vy) < Still ? m_Still + dt : 0f;
            if (!m_Fell || m_Still < 0.03f) return AirEvent.None;
            Airborne = false;
            m_Rising = m_Dropping = 0f;
            return now - m_FlightStart >= MinFlight ? AirEvent.Land : AirEvent.None;
        }
    }
}
