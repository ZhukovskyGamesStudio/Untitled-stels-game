using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// When the house sends more robots (pure, unit tested): the first, the second, then one every so often, up to a number.
    /// In a bigger house the times are longer (<see cref="RunSettings.ReinforceArea"/>): there is more of it to walk.
    /// </summary>
    public readonly struct ReinforcementClock
    {
        public readonly float First, Second, Every;
        public readonly int Max;

        public ReinforcementClock(float first, float second, float every, int max)
        {
            First = first;
            Second = second;
            Every = Mathf.Max(1f, every);
            Max = Mathf.Max(0, max);
        }

        /// <summary>The second of the house's clock the <paramref name="n"/>-th one (0 = the first) comes at.</summary>
        public float At(int n) => n <= 0 ? First : First + Second + Every * (n - 1);

        /// <summary>How many have come <paramref name="elapsed"/> seconds into the house.</summary>
        public int Due(float elapsed)
        {
            int n = elapsed < First ? 0 : elapsed < First + Second ? 1 : 2 + Mathf.FloorToInt((elapsed - First - Second) / Every);
            return Mathf.Min(n, Max);
        }

        /// <summary>When the next one comes; −1: no more.</summary>
        public float NextAt(float elapsed)
        {
            int due = Due(elapsed);
            return due >= Max ? -1f : At(due);
        }

        /// <summary>When the last one came (0 before the first).</summary>
        public float PreviousAt(float elapsed)
        {
            int due = Due(elapsed);
            return due <= 0 ? 0f : At(due - 1);
        }
    }
}
