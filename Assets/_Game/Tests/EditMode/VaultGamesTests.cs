using NUnit.Framework;
using Stealth.Items;

namespace Stealth.Tests
{
    /// <summary>The vault's switch and sequence locks.</summary>
    public class VaultGamesTests
    {
        [Test]
        public void TheSwitchLockStartsScrambledAndCanAlwaysBeSolved()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var p = new SwitchPuzzle(seed);
                Assert.IsFalse(p.Solved, $"seed {seed}: handed over open");
                // a ring of n switches where each flips two: brute force over all subsets finds the way, in no more presses than the scramble
                int best = int.MaxValue;
                for (int mask = 0; mask < 1 << p.Count; mask++)
                {
                    var q = new SwitchPuzzle(seed);
                    int presses = 0;
                    for (int i = 0; i < q.Count; i++) if ((mask & (1 << i)) != 0) { q.Press(i); presses++; }
                    if (q.Solved) best = System.Math.Min(best, presses);
                }
                Assert.LessOrEqual(best, new ObjectiveSettings().Scramble, $"seed {seed}: no short way to light them all");
            }
        }

        [Test]
        public void ASwitchFlipsItsLampAndTheNextOne()
        {
            var p = new SwitchPuzzle(3);
            var before = new bool[p.Count];
            for (int i = 0; i < p.Count; i++) before[i] = p.Lit(i);
            p.Press(0);
            Assert.AreNotEqual(before[0], p.Lit(0));
            Assert.AreNotEqual(before[1], p.Lit(1));
            Assert.AreEqual(before[2], p.Lit(2));
            Assert.AreEqual(before[p.Count - 1], p.Lit(p.Count - 1), "only the next one");
            p.Press(p.Count - 1);
            Assert.AreEqual(before[0], p.Lit(0), "the ring wraps round: the last switch flips the first lamp back");
            Assert.AreEqual(2, p.Presses);
        }

        [Test]
        public void TheSequencePlaysThenIsRepeatedAWrongPressStartsOver()
        {
            var s = new ObjectiveSettings();
            var lockA = new SequenceLock(11, 0f, s);
            Assert.IsTrue(lockA.Playing(0.7f));
            Assert.AreEqual(lockA.At(0), lockA.Shown(0.6f + 0.1f), "the first lamp lights first");
            Assert.AreEqual(-1, lockA.Shown(0.6f + s.FlashTime + 0.05f), "dark between flashes");
            Assert.AreEqual(SequenceLock.Result.Nothing, lockA.Press(lockA.At(0), 1f), "presses wait for the sequence");
            float after = 0.6f + lockA.Length * (s.FlashTime + s.GapTime) + 0.01f;
            Assert.IsFalse(lockA.Playing(after));
            Assert.AreEqual(SequenceLock.Result.Step, lockA.Press(lockA.At(0), after));
            int wrong = (lockA.At(1) + 1) % lockA.Lamps;
            Assert.AreEqual(SequenceLock.Result.Wrong, lockA.Press(wrong, after));
            Assert.AreEqual(0, lockA.Done, "starts over");
            Assert.IsTrue(lockA.Playing(after + 0.7f), "and plays again");
            float again = after + 0.6f + lockA.Length * (s.FlashTime + s.GapTime) + 0.01f;
            for (int i = 0; i < lockA.Length - 1; i++) Assert.AreEqual(SequenceLock.Result.Step, lockA.Press(lockA.At(i), again));
            Assert.AreEqual(SequenceLock.Result.Opened, lockA.Press(lockA.At(lockA.Length - 1), again));
            Assert.IsTrue(lockA.Open);
        }

        [Test]
        public void TheSequenceNeverRepeatsALampInARow()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var l = new SequenceLock(seed, 0f);
                for (int i = 1; i < l.Length; i++) Assert.AreNotEqual(l.At(i - 1), l.At(i));
            }
        }
    }
}
