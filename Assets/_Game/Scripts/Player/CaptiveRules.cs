using UnityEngine;

namespace Stealth.Player
{
    /// <summary>
    /// Where a knocked-out player is held (replicated: append only). Carried — on a robot's back, on the way; the rest are
    /// places a teammate comes to: the servants' prison, a spider's cocoon under the ceiling, a hospital bed the nurse
    /// put them in (they wake by themselves), a renegade's lair.
    /// </summary>
    public enum CaptiveKind : byte { None, Carried, Prison, Cocoon, Ward, Lair }

    /// <summary>Who put a player down: nobody (a fall, the gas), a servant of the machine or its devices, a renegade.</summary>
    public enum DownCause : byte { Self, Servant, Renegade }

    /// <summary>Tuning of being knocked out and held (PlayerConfig → Captive).</summary>
    [System.Serializable]
    public class CaptiveSettings
    {
        [Tooltip("Seconds of E to free a teammate from the prison, a cocoon or a lair (a body on the floor takes Interaction.ReviveTime).")]
        public float FreeTime = 4f;
        [Tooltip("A lone player comes round by themself (nobody else could help) — so many times a house; then lying is the end of the run.")]
        public int SoloRescues = 2;
        [Tooltip("Alone and lying on the floor: seconds until they come round.")]
        public float SoloGetUpDelay = 10f;
        [Tooltip("Alone and held (the prison, a cocoon, a lair): seconds until they break out.")]
        public float SoloEscapeDelay = 25f;
        [Tooltip("Seconds asleep in the hospital bed a nurse puts a player in; then they wake by themselves with a muddled head.")]
        public float WardSleep = 12f;
        [Tooltip("How far a servant that carries players is called to a body lying on the floor, metres.")]
        public float CarrierRange = 40f;
        [Tooltip("Seconds a body lies before a carrier is sent for it (the robot that knocked it down picks it up at once).")]
        public float CarrierDelay = 4f;
    }

    /// <summary>The rules of knocked-out and held players (pure, unit tested).</summary>
    public static class CaptiveRules
    {
        /// <summary>A teammate can bring them round with E: lying on the floor or held in a place — not on a robot's back, not asleep in the ward.</summary>
        public static bool CanFree(CaptiveKind kind) => kind is not (CaptiveKind.Carried or CaptiveKind.Ward);

        /// <summary>Seconds of E it takes to bring them round.</summary>
        public static float FreeTime(CaptiveKind kind, float reviveTime, CaptiveSettings s) => kind == CaptiveKind.None ? reviveTime : s.FreeTime;

        /// <summary>The body does not lie where it fell: it is on a robot or in a place, and stays put there.</summary>
        public static bool Held(CaptiveKind kind) => kind != CaptiveKind.None;

        /// <summary>
        /// A lone player's way back: seconds after they fell (or were locked up) at which they come round by themselves;
        /// −1: they do not (a team, the rescues used up, on a robot's back — it puts them down first).
        /// </summary>
        public static float SoloRescueAfter(CaptiveKind kind, int players, int rescuesUsed, CaptiveSettings s)
        {
            if (players > 1 || rescuesUsed >= s.SoloRescues || kind == CaptiveKind.Carried || kind == CaptiveKind.Ward) return -1f;
            return kind == CaptiveKind.None ? s.SoloGetUpDelay : s.SoloEscapeDelay;
        }

        /// <summary>
        /// The run is lost: everybody lies or is held, and nobody comes back by themselves (a sleeper in the ward wakes,
        /// a lone player with a rescue left comes round).
        /// </summary>
        public static bool Wiped(int players, int knockedOut, int asleepInWard, bool soloRescueLeft) =>
            players > 0 && knockedOut >= players && asleepInWard == 0 && !(players == 1 && soloRescueLeft);

        /// <summary>Who carries a player this cause put down: the servants take theirs to prison; the rest lie where they fell.</summary>
        public static bool Carried(DownCause cause) => cause == DownCause.Servant;
    }
}
