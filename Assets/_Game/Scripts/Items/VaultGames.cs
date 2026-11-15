using UnityEngine;

namespace Stealth.Items
{
    /// <summary>Tuning of the house's main job (ItemsConfig): the vault's three locks and what the vault and the hostage give.</summary>
    [System.Serializable]
    public class ObjectiveSettings
    {
        [Header("Vault: the switch lock")]
        [Tooltip("Lamps in the ring of switches: a switch flips its lamp and both neighbours; all lit opens the lock.")]
        public int Switches = 5;
        [Tooltip("Random presses the lock is scrambled with (from its seed).")]
        public int Scramble = 3;

        [Header("Vault: the sequence lock")]
        [Tooltip("Lamps the sequence plays on.")]
        public int SequenceLamps = 4;
        [Tooltip("How long the sequence is.")]
        public int SequenceLength = 5;
        [Tooltip("A lamp stays lit this long while the sequence plays, seconds.")]
        public float FlashTime = 0.45f;
        [Tooltip("The dark between two flashes, seconds.")]
        public float GapTime = 0.18f;

        [Tooltip("How far a wrong press on a lock clanks: robots with ears hear it, metres.")]
        public float WrongNoise = 6f;

        [Header("Rewards (not part of the house's quota: the job is optional)")]
        [Tooltip("Valuable items lying in the vault.")]
        public int VaultTreasure = 6;
        [Tooltip("Colours of the vault's treasure (index in ItemsConfig): the most valuable ones.")]
        public int[] TreasureColors = { 3, 4, 4 };
        [Tooltip("What the freed hostage gives: a diamond of this colour (index in ItemsConfig).")]
        public int HostageRewardColor = 4;
    }

    /// <summary>
    /// The vault's switch lock (pure, unit tested): a ring of lamps, one switch under each; a switch flips its own lamp
    /// and both neighbours. Scrambled from all-lit by a few presses (from the seed), so it can always be solved;
    /// lighting them all opens it. No penalty for a press: it is a puzzle, not a test of nerve.
    /// </summary>
    public sealed class SwitchPuzzle
    {
        readonly bool[] m_Lit;

        public SwitchPuzzle(int seed, ObjectiveSettings s = null)
        {
            s ??= new ObjectiveSettings();
            m_Lit = new bool[Mathf.Max(3, s.Switches)];
            for (int i = 0; i < m_Lit.Length; i++) m_Lit[i] = true;
            var rng = new System.Random(seed);
            for (int k = 0; k < Mathf.Max(1, s.Scramble); k++) Flip(rng.Next(m_Lit.Length));
            if (Solved) Flip(rng.Next(m_Lit.Length)); // never handed over open
        }

        public int Count => m_Lit.Length;
        public bool Lit(int i) => m_Lit[i];
        public bool Solved
        {
            get
            {
                foreach (bool b in m_Lit) if (!b) return false;
                return true;
            }
        }
        public int Presses { get; private set; }

        /// <summary>Press switch <paramref name="i"/> (0-based); true when that opened the lock.</summary>
        public bool Press(int i)
        {
            if (i < 0 || i >= m_Lit.Length || Solved) return false;
            Flip(i);
            Presses++;
            return Solved;
        }

        void Flip(int i)
        {
            int n = m_Lit.Length;
            for (int d = -1; d <= 1; d++) m_Lit[(i + d + n) % n] ^= true;
        }
    }

    /// <summary>
    /// The vault's sequence lock (pure, unit tested): the lamps play a sequence (from the seed), the player repeats it on
    /// the switches. A wrong press clanks (a noise robots hear) and the sequence plays again from the start; the whole
    /// sequence repeated opens it. Time comes in as a parameter.
    /// </summary>
    public sealed class SequenceLock
    {
        public enum Result { Nothing, Step, Wrong, Opened }

        readonly int[] m_Sequence;
        readonly ObjectiveSettings m_S;
        float m_ShowFrom;

        public SequenceLock(int seed, float now, ObjectiveSettings s = null)
        {
            m_S = s ?? new ObjectiveSettings();
            var rng = new System.Random(seed);
            m_Sequence = new int[Mathf.Max(2, m_S.SequenceLength)];
            for (int i = 0; i < m_Sequence.Length; i++)
            {
                // never the same lamp twice in a row: a repeat is hard to see
                int lamp;
                do lamp = rng.Next(Lamps); while (i > 0 && lamp == m_Sequence[i - 1]);
                m_Sequence[i] = lamp;
            }
            Replay(now);
        }

        public int Lamps => Mathf.Max(2, m_S.SequenceLamps);
        public int Length => m_Sequence.Length;
        /// <summary>Steps repeated right so far.</summary>
        public int Done { get; private set; }
        public bool Open => Done >= m_Sequence.Length;
        float Step => m_S.FlashTime + m_S.GapTime;

        /// <summary>The sequence is still playing (presses are ignored meanwhile).</summary>
        public bool Playing(float now) => now < m_ShowFrom + m_Sequence.Length * Step;

        /// <summary>The lamp lit by the playing sequence now, or −1.</summary>
        public int Shown(float now)
        {
            float t = now - m_ShowFrom;
            if (t < 0f || !Playing(now)) return -1;
            int i = (int)(t / Step);
            return t - i * Step < m_S.FlashTime ? m_Sequence[i] : -1;
        }

        /// <summary>Play the sequence again from the start, after a short pause.</summary>
        public void Replay(float now)
        {
            Done = 0;
            m_ShowFrom = now + 0.6f;
        }

        public Result Press(int lamp, float now)
        {
            if (Open || Playing(now) || lamp < 0 || lamp >= Lamps) return Result.Nothing;
            if (lamp != m_Sequence[Done])
            {
                Replay(now);
                return Result.Wrong;
            }
            Done++;
            return Open ? Result.Opened : Result.Step;
        }

        /// <summary>The n-th lamp of the sequence (tests).</summary>
        public int At(int i) => m_Sequence[i];
    }
}
