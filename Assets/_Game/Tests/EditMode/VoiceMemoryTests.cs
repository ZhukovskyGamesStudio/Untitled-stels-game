using System;
using System.Collections.Generic;
using NUnit.Framework;
using Stealth.Audio;

namespace Stealth.Tests
{
    /// <summary>The server's memory of the players' words that the relay plays back (<see cref="VoiceMemory"/>).</summary>
    public class VoiceMemoryTests
    {
        const float Dt = VoiceMemory.FrameSeconds;
        readonly byte[] m_Packet = new byte[VoicePacket.MaxSize];
        readonly byte[] m_Payload = new byte[40];
        readonly List<ArraySegment<byte>> m_Out = new();

        /// <summary><paramref name="frames"/> frames of speech from <paramref name="speaker"/>, from <paramref name="start"/> s.</summary>
        void Speak(VoiceMemory m, ulong speaker, ushort firstSeq, int frames, float start, int keep = 1000, bool loud = true)
        {
            for (int i = 0; i < frames; i++)
            {
                m_Payload[0] = (byte)i;
                int n = VoicePacket.Write(m_Packet, (ushort)(firstSeq + i), -20f, m_Payload, m_Payload.Length);
                m.Add(speaker, (ushort)(firstSeq + i), loud, start + i * Dt, m_Packet, 0, n, keep);
            }
        }

        bool Pick(VoiceMemory m, ulong[] candidates, float now, out ulong speaker, int roll = 0, int min = 40, int max = 125, float forget = 60f) =>
            m.Pick(candidates, roll, min, max, now, forget, m_Out, out speaker);

        [Test]
        public void NothingWhenNobodySpoke()
        {
            var m = new VoiceMemory();
            Assert.IsFalse(Pick(m, new ulong[] { 1, 2 }, 10f, out _));
            Assert.IsEmpty(m_Out);
        }

        [Test]
        public void RemembersOnlyTheLastFramesOfEachSpeaker()
        {
            var m = new VoiceMemory();
            Speak(m, 1, 0, 300, 0f, keep: 200);
            Speak(m, 2, 0, 50, 0f, keep: 200);
            Assert.AreEqual(200, m.Count(1));
            Assert.AreEqual(50, m.Count(2));
            Speak(m, 3, 0, 50, 0f, keep: 0);
            Assert.AreEqual(0, m.Count(3), "no relay in the house: nothing is recorded");
        }

        [Test]
        public void TheWordsAreAtLeastMinAndAtMostMaxLong()
        {
            var m = new VoiceMemory();
            Speak(m, 1, 0, 30, 0f); // 0.6 s: too short to be recognised
            Assert.IsFalse(Pick(m, new ulong[] { 1 }, 1f, out _));
            Speak(m, 1, 100, 300, 2f); // 6 s of speech
            Assert.IsTrue(Pick(m, new ulong[] { 1 }, 9f, out ulong who));
            Assert.AreEqual(1ul, who);
            Assert.AreEqual(125, m_Out.Count, "cut to the longest allowed");
            Assert.IsTrue(VoicePacket.Read(m_Out[0].Array, m_Out[0].Offset, m_Out[0].Count, out ushort seq, out _, out _, out int size));
            Assert.AreEqual(100, seq, "from the start of the speech");
            Assert.AreEqual(m_Payload.Length, size, "the frame as it came");
        }

        [Test]
        public void ASnippetIsOneStretchOfSpeechAndStartsAtAWord()
        {
            var m = new VoiceMemory();
            Speak(m, 1, 0, 10, 0f, loud: false);  // the gate's quiet tail before the words
            Speak(m, 1, 10, 50, 10 * Dt);          // a second of words
            Speak(m, 1, 80, 50, 2f);               // a later sentence (a gap in the sequence)
            Assert.IsTrue(Pick(m, new ulong[] { 1 }, 5f, out _, roll: 0));
            Assert.AreEqual(50, m_Out.Count, "the first stretch without its quiet start, not run into the next");
            VoicePacket.Read(m_Out[0].Array, m_Out[0].Offset, m_Out[0].Count, out ushort first, out _, out _, out _);
            Assert.AreEqual(10, first);
            Assert.IsTrue(Pick(m, new ulong[] { 1 }, 5f, out _, roll: 1));
            VoicePacket.Read(m_Out[0].Array, m_Out[0].Offset, m_Out[0].Count, out first, out _, out _, out _);
            Assert.AreEqual(80, first, "the roll chooses the other one");
        }

        [Test]
        public void NeverTheVoiceOfAPlayerWhoIsNotACandidate()
        {
            var m = new VoiceMemory();
            Speak(m, 1, 0, 100, 0f); // standing by the relay: not a candidate
            Speak(m, 2, 0, 100, 0f);
            for (int roll = 0; roll < 8; roll++)
            {
                Assert.IsTrue(Pick(m, new ulong[] { 2, 3 }, 3f, out ulong who, roll));
                Assert.AreEqual(2ul, who);
            }
            Assert.IsFalse(Pick(m, new ulong[] { 3 }, 3f, out _), "only player 1 spoke, and they are by the relay");
        }

        [Test]
        public void OldWordsAreForgottenAndANewHouseForgetsAll()
        {
            var m = new VoiceMemory();
            Speak(m, 1, 0, 100, 0f);
            Assert.IsFalse(Pick(m, new ulong[] { 1 }, 70f, out _, forget: 60f), "a minute later");
            Assert.AreEqual(0, m.Count(1));
            Speak(m, 1, 200, 100, 80f);
            m.Clear();
            Assert.IsFalse(Pick(m, new ulong[] { 1 }, 82f, out _));
        }

        [Test]
        public void TheRelaysCallCarriesTheFramesAsTheyCame()
        {
            var m = new VoiceMemory();
            Speak(m, 1, 0, 60, 0f);
            Assert.IsTrue(Pick(m, new ulong[] { 1 }, 2f, out _));
            var message = new byte[VoicePacket.LureSize(m_Out)];
            int n = VoicePacket.WriteLure(message, 0x1234_5678_9ABCul, m_Out);
            Assert.AreEqual(message.Length, n);
            var back = new List<ArraySegment<byte>>();
            Assert.IsTrue(VoicePacket.ReadLure(message, 0, n, out ulong relay, back));
            Assert.AreEqual(0x1234_5678_9ABCul, relay);
            Assert.AreEqual(m_Out.Count, back.Count);
            for (int i = 0; i < back.Count; i++)
                CollectionAssert.AreEqual(m_Out[i], back[i]);
            Assert.IsFalse(VoicePacket.ReadLure(message, 0, n - 1, out _, back), "a cut message is refused");
        }
    }
}
