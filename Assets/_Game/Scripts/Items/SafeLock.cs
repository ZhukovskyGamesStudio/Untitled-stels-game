using UnityEngine;

namespace Stealth.Items
{
    /// <summary>Tuning of the safes (ItemsConfig): the dial and what is inside.</summary>
    [System.Serializable]
    public class SafeSettings
    {
        [Tooltip("Numbers on the dial (0 .. this − 1).")]
        public int DialSize = 40;
        [Tooltip("How many numbers the combination has.")]
        public int Digits = 3;
        [Tooltip("How close (in numbers) to the right one the ticks start to get louder.")]
        public int HearRange = 6;
        [Tooltip("Numbers per mouse unit (sensitivity of the dial).")]
        public float TurnSpeed = 0.06f;
        [Tooltip("How far a wrong try clanks: robots with ears hear it, metres.")]
        public float WrongNoise = 7f;
        [Tooltip("Valuable items inside.")]
        public int LootItems = 2;
        [Tooltip("Colours of the loot (index in ItemsConfig): the most valuable ones.")]
        public int[] LootColors = { 3, 4 };
    }

    /// <summary>
    /// A combination dial (pure, unit tested). The player turns it and listens: the closer to the right number, the
    /// louder the ticks (<see cref="Closeness"/>). On the right number a try sets that digit; a wrong try clanks
    /// (a noise robots hear) and the digit must be found again. All digits found — the safe opens.
    /// </summary>
    public sealed class SafeLock
    {
        public enum Result { Nothing, Digit, Wrong, Opened }

        readonly int[] m_Code;
        readonly SafeSettings m_S;

        public SafeLock(int seed, SafeSettings settings = null)
        {
            m_S = settings ?? new SafeSettings();
            var rng = new System.Random(seed);
            m_Code = new int[Mathf.Max(1, m_S.Digits)];
            for (int i = 0; i < m_Code.Length; i++)
            {
                // Next number at least a quarter turn away from the previous one: every digit needs a real search.
                int n;
                do n = rng.Next(m_S.DialSize);
                while (i > 0 && Distance(n, m_Code[i - 1]) < m_S.DialSize / 4);
                m_Code[i] = n;
            }
        }

        public int DialSize => m_S.DialSize;
        public int Digits => m_Code.Length;
        /// <summary>Digits already found.</summary>
        public int Found { get; private set; }
        public bool Open => Found >= m_Code.Length;
        /// <summary>The dial position, 0 .. DialSize (fractional while turning).</summary>
        public float Dial { get; private set; }
        public int Number => ((int)Mathf.Round(Dial) % DialSize + DialSize) % DialSize;

        /// <summary>For tests and the debug view only.</summary>
        public int CodeAt(int i) => m_Code[i];

        int Distance(int a, int b)
        {
            int d = Mathf.Abs(a - b) % m_S.DialSize;
            return Mathf.Min(d, m_S.DialSize - d);
        }

        /// <summary>Turns the dial by a mouse movement. Returns how many number marks were passed (each one ticks).</summary>
        public int Turn(float mouse)
        {
            int before = Number;
            Dial = Mathf.Repeat(Dial + mouse * m_S.TurnSpeed, DialSize);
            return Distance(before, Number);
        }

        public void SetDial(float value) => Dial = Mathf.Repeat(value, DialSize);

        /// <summary>0 far .. 1 on the number: how loud the ticks are.</summary>
        public float Closeness()
        {
            if (Open) return 0f;
            int d = Distance(Number, m_Code[Found]);
            return d > m_S.HearRange ? 0f : 1f - d / (float)(m_S.HearRange + 1);
        }

        /// <summary>Try the number under the pointer.</summary>
        public Result Try()
        {
            if (Open) return Result.Nothing;
            if (Number != m_Code[Found]) return Result.Wrong;
            Found++;
            return Open ? Result.Opened : Result.Digit;
        }
    }
}
