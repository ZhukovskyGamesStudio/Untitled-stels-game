using System.Collections.Generic;
using NUnit.Framework;
using Stealth.Vehicles;

namespace Stealth.Tests
{
    /// <summary>The car's shredder: things thrown in pile up, fall in, are chewed one after another, and only then are fuel.</summary>
    public class ShredderTests
    {
        [Test]
        public void ThingsAreChewedOneAfterAnotherAndOnlyThenAreFuel()
        {
            var s = new Shredder();
            var done = new List<(byte, byte)>();
            s.Add(1, 2, 10.0);
            s.Add(3, 4, 10.0);
            s.Add(5, 0, 10.0);
            Assert.AreEqual(3, s.Pile.Count, "a heap thrown in at once");
            Assert.Less(s.Progress(10.0), 0f, "the first is still falling in");
            Assert.AreEqual(0, s.Tick(10.0 + Shredder.FallTime + Shredder.ChewTime * 0.5, done), "half chewed: no fuel yet");
            Assert.AreEqual(1, s.Tick(10.0 + Shredder.FallTime + Shredder.ChewTime + 0.01, done), "the first is gone");
            Assert.AreEqual(((byte)1, (byte)2), done[0], "the very thing thrown in first");
            Assert.AreEqual(2, s.Tick(10.0 + Shredder.FallTime + Shredder.ChewTime * 3 + 0.01, done), "late: the rest come out together");
            Assert.IsFalse(s.Busy);
            s.Add(7, 1, 100.0);
            Assert.AreEqual(100.0 + Shredder.FallTime, s.Started, 1e-9, "an empty mouth starts on a new thing once it has fallen");
        }

        [Test]
        public void ThePileTravelsPackedAndKeepsItsClock()
        {
            var s = new Shredder();
            s.Add(9, 3, 5.0);
            s.Add(2, 1, 5.0);
            var packed = new List<ulong>();
            foreach (var item in s.Pile) packed.Add(Shredder.Pack(item));
            var peer = new Shredder();
            peer.ApplyReplicated(packed, s.Started);
            CollectionAssert.AreEqual(s.Pile, peer.Pile);
            Assert.AreEqual(s.Progress(6.0), peer.Progress(6.0), 1e-6f);
        }
    }
}
