namespace Stealth.Player
{
    /// <summary>Keys at a terminal (pure): a short press of the use key leaves it, a long one is a hold — the hacking terminal's hack.</summary>
    public static class TerminalRules
    {
        /// <summary>A press of the use key shorter than this, seconds, is a tap.</summary>
        public const float Tap = 0.3f;

        /// <summary>The key was down this long and is let go: a tap? (0 — it was not down; negative — that press did something else.)</summary>
        public static bool IsTap(float held) => held > 0f && held < Tap;
    }
}
