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

        /// <summary>
        /// A thing goes into the mouth smallest face down: its longest side upright within the lean (a shovel handle or blade
        /// first, never across), either end first and turned any way about the upright by its seed, the same every time.
        /// </summary>
        [Test]
        public void ThingsGoInSmallestFaceDown()
        {
            var up = UnityEngine.Vector3.up;
            var shovel = new UnityEngine.Vector3(0.22f, 0.06f, 1.15f); // long along Z
            var plank = new UnityEngine.Vector3(0.9f, 0.3f, 0.04f);    // long along X
            var can = new UnityEngine.Vector3(0.1f, 0.18f, 0.1f);      // long along Y
            bool down = false, upEnd = false;
            for (int seed = 0; seed < 64; seed++)
            {
                var a = Shredder.FeedTurn(shovel, seed);
                float lean = UnityEngine.Vector3.Angle(a * UnityEngine.Vector3.forward, up);
                Assert.That(UnityEngine.Mathf.Min(lean, 180f - lean), Is.LessThanOrEqualTo(Shredder.MaxTilt + 0.01f), $"seed {seed}: the shovel's length upright");
                if (lean < 90f) upEnd = true; else down = true;
                float plankLean = UnityEngine.Vector3.Angle(Shredder.FeedTurn(plank, seed) * UnityEngine.Vector3.right, up);
                Assert.That(UnityEngine.Mathf.Min(plankLean, 180f - plankLean), Is.LessThanOrEqualTo(Shredder.MaxTilt + 0.01f), $"seed {seed}: the plank on its narrow end");
                float canLean = UnityEngine.Vector3.Angle(Shredder.FeedTurn(can, seed) * UnityEngine.Vector3.up, up);
                Assert.That(UnityEngine.Mathf.Min(canLean, 180f - canLean), Is.LessThanOrEqualTo(Shredder.MaxTilt + 0.01f), $"seed {seed}: the can standing");
                // standing on its end: half its height is about half its length (a little less as it leans)
                float half = Shredder.HalfHeight(a, shovel);
                Assert.That(half, Is.InRange(0.5f * shovel.z * 0.97f, 0.5f * shovel.z + 0.5f * (shovel.x + shovel.y) * 0.25f), $"seed {seed}: its height in the mouth");
            }
            Assert.IsTrue(down && upEnd, "either end goes in first");
            Assert.AreEqual(Shredder.FeedTurn(shovel, 7), Shredder.FeedTurn(shovel, 7), "the same on every peer");
            Assert.AreNotEqual(Shredder.FeedTurn(shovel, 7), Shredder.FeedTurn(shovel, 8), "each its own way");
            Assert.AreEqual(0.5f, Shredder.HalfHeight(UnityEngine.Quaternion.identity, new UnityEngine.Vector3(3f, 1f, 2f)), 1e-5f);
        }
    }
}
