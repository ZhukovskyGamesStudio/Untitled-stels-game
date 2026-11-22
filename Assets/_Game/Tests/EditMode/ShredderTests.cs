using System.Collections.Generic;
using NUnit.Framework;
using Stealth.Vehicles;

namespace Stealth.Tests
{
    /// <summary>The car's shredder: a heap in the mouth, the shafts tear several things at once, each is fuel only when torn up.</summary>
    public class ShredderTests
    {
        [Test]
        public void TheShaftsTearSeveralAtOnceAndTheHeapWaits()
        {
            var s = new Shredder();
            var done = new List<(byte, byte)>();
            for (byte i = 0; i < 5; i++) s.Add(i, 1, 10.0);
            Assert.AreEqual(5, s.Pile.Count, "a heap thrown in at once");
            double land = 10.0 + Shredder.FallTime;
            int chewing = 0;
            foreach (var p in s.Pile) if (Shredder.Progress(p, land + 0.01) >= 0f) chewing++;
            Assert.AreEqual(Shredder.Lanes, chewing, "the shafts take as many as they have lanes; the rest wait on the heap");
            Assert.AreEqual(0, s.Tick(land + Shredder.ChewTime * 0.5, done), "half torn: no fuel yet");
            Assert.AreEqual(Shredder.Lanes, s.Tick(land + Shredder.ChewTime + 0.01, done), "the first lanes' worth are gone together");
            Assert.AreEqual(((byte)0, (byte)1), done[0], "the very things thrown in first");
            Assert.AreEqual(2, s.Tick(land + Shredder.ChewTime * 2 + 0.01, done), "the heap settled onto the free shafts");
            Assert.IsFalse(s.Busy);
            s.Add(7, 1, 100.0);
            Assert.AreEqual(100.0 + Shredder.FallTime, s.Pile[0].Start, 1e-9, "empty shafts take a new thing once it has landed");
        }

        [Test]
        public void TheHeapTravelsPackedWithItsStarts()
        {
            var s = new Shredder();
            for (byte i = 0; i < 4; i++) s.Add((byte)(9 - i), i, 5.0 + i * 0.1);
            double baseTime = s.Started;
            var packed = new List<ulong>();
            foreach (var p in s.Pile) packed.Add(Shredder.Pack(p, baseTime));
            var peer = new Shredder();
            peer.ApplyReplicated(packed, baseTime);
            Assert.AreEqual(s.Pile.Count, peer.Pile.Count);
            for (int i = 0; i < s.Pile.Count; i++)
            {
                Assert.AreEqual(s.Pile[i].Shape, peer.Pile[i].Shape);
                Assert.AreEqual(s.Pile[i].Color, peer.Pile[i].Color);
                Assert.AreEqual(s.Pile[i].Start, peer.Pile[i].Start, 0.001, "the schedule survives the wire");
            }
        }
    }
}
