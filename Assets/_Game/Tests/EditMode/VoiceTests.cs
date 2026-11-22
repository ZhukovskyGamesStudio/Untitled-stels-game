using NUnit.Framework;
using Stealth.Audio;
using Stealth.Configs;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    public class VoiceTests
    {
        static readonly VoiceSettings S = new();

        [Test]
        public void PacketsRoundTripAndRelayCarriesTheSpeaker()
        {
            var payload = new byte[] { 1, 2, 3, 250 };
            var buf = new byte[VoicePacket.MaxSize];
            int n = VoicePacket.Write(buf, 65535, -26f, payload, payload.Length);
            Assert.AreEqual(VoicePacket.Header + 4, n);
            var relay = new byte[VoicePacket.MaxSize];
            int r = VoicePacket.WriteRelay(relay, 0x0102030405060708UL, buf, 0, n);
            Assert.AreEqual(0x0102030405060708UL, VoicePacket.ReadSpeaker(relay, 0));
            Assert.IsTrue(VoicePacket.Read(relay, 8, r - 8, out ushort seq, out float db, out int off, out int len));
            Assert.AreEqual(65535, seq);
            Assert.AreEqual(-26f, db, 0.5f);
            Assert.AreEqual(4, len);
            Assert.AreEqual(250, relay[off + 3]);
            Assert.IsFalse(VoicePacket.Read(new byte[] { 9, 0, 0, 0, 1 }, 0, 5, out _, out _, out _, out _), "an unknown version is rejected");
            Assert.AreEqual(2, VoicePacket.Ahead(1, 65535), "sequence numbers wrap");
        }

        [Test]
        public void VoiceActivityOpensOnSpeechAndHoldsForWordEndings()
        {
            var gate = new VoiceGate();
            Assert.IsFalse(gate.Step(VoiceMode.VoiceActivity, false, 0.1f, -20f, 0.5f, 0.02f, S), "loud but not voice (a fan, a keyboard)");
            Assert.IsFalse(gate.Step(VoiceMode.VoiceActivity, false, 0.9f, -70f, 0.5f, 0.02f, S), "voice but too quiet for this sensitivity");
            Assert.IsTrue(gate.Step(VoiceMode.VoiceActivity, false, 0.9f, -25f, 0.5f, 0.02f, S));
            int held = 0;
            while (gate.Step(VoiceMode.VoiceActivity, false, 0f, -80f, 0.5f, 0.02f, S)) held++;
            Assert.AreEqual(S.Hangover / 0.02f, held, 1.5f, "the gate stays open for the hangover");
            Assert.IsTrue(gate.Step(VoiceMode.VoiceActivity, false, 0.9f, -60f, 1f, 0.02f, S), "full sensitivity hears a whisper");
        }

        [Test]
        public void PushToTalkAndOffIgnoreTheVoice()
        {
            var gate = new VoiceGate();
            Assert.IsFalse(gate.Step(VoiceMode.PushToTalk, false, 1f, -10f, 1f, 0.02f, S));
            Assert.IsTrue(gate.Step(VoiceMode.PushToTalk, true, 0f, -90f, 0f, 0.02f, S));
            Assert.IsFalse(gate.Step(VoiceMode.Off, true, 1f, -10f, 1f, 0.02f, S));
        }

        [Test]
        public void GainBringsQuietSpeechUpWithinItsRangeAndNeverClips()
        {
            var gain = new VoiceGain();
            var frame = new float[960];
            for (int k = 0; k < 500; k++)
            {
                for (int i = 0; i < frame.Length; i++) frame[i] = 0.01f * Mathf.Sin(i * 0.1f); // ≈ -43 dBFS
                gain.Process(frame, 0, frame.Length, VoiceLevel.RmsDb(frame, 0, frame.Length), true, 0.02f, S);
            }
            Assert.AreEqual(S.MaxGainDb, gain.GainDb, 0.5f, "a quiet voice gets the most gain, no more");
            for (int k = 0; k < 100; k++)
            {
                for (int i = 0; i < frame.Length; i++) frame[i] = 2f * Mathf.Sin(i * 0.1f);
                gain.Process(frame, 0, frame.Length, VoiceLevel.RmsDb(frame, 0, frame.Length), true, 0.02f, S);
                foreach (float v in frame) Assert.Less(Mathf.Abs(v), 1f);
            }
            Assert.Less(gain.GainDb, 0f, "a shout is cut");
            Assert.AreEqual(0.5f, VoiceGain.Limit(0.5f));
        }

        [Test]
        public void RobotsHearWhispersCloseAndShoutsFar()
        {
            Assert.AreEqual(0f, VoiceNoise.Radius(-80f, S));
            Assert.AreEqual(S.WhisperRadius, VoiceNoise.Radius(S.WhisperDb, S), 0.01f);
            Assert.AreEqual(S.SpeechRadius, VoiceNoise.Radius(S.SpeechDb, S), 0.01f);
            Assert.AreEqual(S.ShoutRadius, VoiceNoise.Radius(0f, S), 0.01f);
            float last = -1f;
            for (float db = -70f; db <= 0f; db += 1f)
            {
                float r = VoiceNoise.Radius(db, S);
                Assert.GreaterOrEqual(r, last, $"louder is never quieter ({db} dB)");
                last = r;
            }
        }

        static void Push(VoiceJitter j, ushort seq) => j.Push(seq, new[] { (byte)seq }, 0, 1);

        [Test]
        public void JitterBufferReordersAndWaitsForItsCushion()
        {
            var j = new VoiceJitter();
            var dst = new byte[VoicePacket.MaxPayload];
            Push(j, 11);
            Assert.AreEqual(VoiceFrame.None, j.Pop(dst, out _, 3, 12), "still filling");
            Push(j, 10);
            Push(j, 12);
            for (int s = 10; s <= 12; s++)
            {
                Assert.AreEqual(VoiceFrame.Packet, j.Pop(dst, out int len, 3, 12));
                Assert.AreEqual(1, len);
                Assert.AreEqual((byte)s, dst[0]);
            }
        }

        [Test]
        public void JitterBufferRebuildsALostFrameFromTheNextAndDropsLateOnes()
        {
            var j = new VoiceJitter();
            var dst = new byte[VoicePacket.MaxPayload];
            Push(j, 0); Push(j, 2); Push(j, 3);
            Assert.AreEqual(VoiceFrame.Packet, j.Pop(dst, out _, 3, 12));
            Assert.AreEqual(VoiceFrame.Fec, j.Pop(dst, out _, 3, 12), "1 is lost, 2 carries it");
            Assert.AreEqual(2, dst[0]);
            Push(j, 1); // too late
            Assert.AreEqual(VoiceFrame.Packet, j.Pop(dst, out _, 3, 12));
            Assert.AreEqual(2, dst[0], "2 is played as itself after its FEC");
            Assert.AreEqual(VoiceFrame.Packet, j.Pop(dst, out _, 3, 12));
            Assert.AreEqual(3, dst[0]);
        }

        [Test]
        public void JitterBufferConcealsAFewFramesThenGoesQuietAndRestarts()
        {
            var j = new VoiceJitter();
            var dst = new byte[VoicePacket.MaxPayload];
            Push(j, 100);
            Assert.AreEqual(VoiceFrame.Packet, j.Pop(dst, out _, 1, 12));
            for (int i = 0; i < VoiceJitter.ConcealFrames; i++) Assert.AreEqual(VoiceFrame.Lost, j.Pop(dst, out _, 1, 12));
            Assert.AreEqual(VoiceFrame.None, j.Pop(dst, out _, 1, 12));
            Assert.IsFalse(j.Playing);
            Push(j, 500); // the next words, much later
            Assert.AreEqual(VoiceFrame.Packet, j.Pop(dst, out _, 1, 12));
            Assert.AreEqual(500 & 0xFF, dst[0]); // Push stores the low byte of the sequence
        }

        [Test]
        public void JitterBufferCatchesUpWhenTooFarBehind()
        {
            var j = new VoiceJitter();
            var dst = new byte[VoicePacket.MaxPayload];
            for (ushort s = 0; s < 30; s++) Push(j, s);
            Assert.AreEqual(VoiceFrame.Packet, j.Pop(dst, out _, 3, 8));
            Assert.LessOrEqual(j.Span, 8, "latency is bounded");
            Assert.Greater(dst[0], 20);
        }

        [Test]
        public void JitterBufferWaitsForALatePacketWhilePlaybackStillHasSound()
        {
            // a packet a few ms late used to be concealed and then thrown away: constant little dropouts
            var j = new VoiceJitter();
            var dst = new byte[VoicePacket.MaxPayload];
            Push(j, 0); Push(j, 1); Push(j, 2);
            for (int i = 0; i < 3; i++) Assert.AreEqual(VoiceFrame.Packet, j.Pop(dst, out _, 3, 12, false));
            Assert.AreEqual(VoiceFrame.None, j.Pop(dst, out _, 3, 12, false), "3 is not here yet: wait, there is still sound");
            Assert.IsTrue(j.Playing);
            Push(j, 3);
            Assert.AreEqual(VoiceFrame.Packet, j.Pop(dst, out _, 3, 12, false));
            Assert.AreEqual(3, dst[0], "played as itself, not concealed");
            Assert.AreEqual(0, j.Late);
            // the sound is running out: now it is given up on, and its late arrival is counted
            Assert.AreEqual(VoiceFrame.Lost, j.Pop(dst, out _, 3, 12, true));
            Push(j, 4);
            Assert.AreEqual(1, j.Late);
        }

        [Test]
        public void TheCushionGrowsOnLatePacketsAndShrinksWhenCalm()
        {
            var c = new VoiceCushion();
            Assert.AreEqual(3, c.Frames(3, 12));
            c.Update(1, 0.02f, 8f, 4);
            c.Update(2, 0.02f, 8f, 4);
            Assert.AreEqual(5, c.Frames(3, 12), "two late packets: two frames more");
            for (int late = 3; late < 20; late++) c.Update(late, 0.02f, 8f, 4); // packet after packet late
            Assert.AreEqual(4, c.Extra, "never more than the limit");
            c.Update(19, 8.1f, 8f, 4);
            Assert.AreEqual(3, c.Extra, "a calm stretch: one back");
            Assert.LessOrEqual(new VoiceCushion().Frames(20, 12), 10, "never past the catch-up limit");
        }

        [Test]
        public void VoicesFadeSmoothlyAndCarryFar()
        {
            // one even fade: no step and no corner along the whole range, and a mate across a big floor is still heard
            float prev = 1f, prevSlope = 0f;
            for (float d = 0.05f; d < S.MaxDistance; d += 0.05f)
            {
                float g = VoiceAcoustics.DistanceGain(d, S);
                Assert.LessOrEqual(g, prev + 1e-6f, $"never louder further away ({d:0.00} m)");
                float slope = (prev - g) / 0.05f;
                Assert.Less(Mathf.Abs(slope - prevSlope), 0.02f, $"no corner in the curve at {d:0.00} m");
                prev = g;
                prevSlope = slope;
            }
            Assert.Greater(VoiceAcoustics.DistanceGain(20f, S), 0.2f, "20 m away: clearly heard");
            Assert.Greater(VoiceAcoustics.DistanceGain(40f, S), 0.05f, "40 m: still there");
            Assert.GreaterOrEqual(S.MaxDistance, 50f);
            // a wall coming between: the gain glides there in about Smoothing seconds instead of jumping
            float v = 1f;
            v = VoiceAcoustics.Follow(v, 0.1f, 0.016f, S.Smoothing);
            Assert.Greater(v, 0.8f, "not at once");
            for (int i = 0; i < 60; i++) v = VoiceAcoustics.Follow(v, 0.1f, 0.016f, S.Smoothing);
            Assert.AreEqual(0.1f, v, 0.01f, "there within a second");
        }

        [Test]
        public void RingPassesSamplesInOrderAndRefusesOverflow()
        {
            var ring = new VoiceRing(4);
            Assert.AreEqual(4, ring.Write(new[] { 1f, 2f, 3f, 4f, 5f }, 0, 5));
            Assert.IsTrue(ring.Read(out float a)); Assert.AreEqual(1f, a);
            Assert.AreEqual(1, ring.Write(new[] { 6f }, 0, 1));
            float sum = 0f;
            while (ring.Read(out float v)) sum += v;
            Assert.AreEqual(2f + 3f + 4f + 6f, sum);
            Assert.IsFalse(ring.Read(out float none));
            Assert.AreEqual(0f, none);
        }

        [Test]
        public void ResamplerIsTheSameInAnyChunksAndKeepsTheRate()
        {
            var src = new float[4410];
            for (int i = 0; i < src.Length; i++) src[i] = Mathf.Sin(i * 0.05f);
            var whole = new VoiceResampler(44100, 48000);
            var outWhole = new float[whole.MaxOut(src.Length)];
            int nWhole = whole.Process(src, 0, src.Length, outWhole, 0);
            Assert.AreEqual(4800, nWhole, 2);

            var chunked = new VoiceResampler(44100, 48000);
            var outChunked = new float[nWhole + 64];
            int n = 0;
            for (int off = 0; off < src.Length; off += 441)
            {
                var tmp = new float[chunked.MaxOut(441)];
                int m = chunked.Process(src, off, 441, tmp, 0);
                System.Array.Copy(tmp, 0, outChunked, n, m);
                n += m;
            }
            Assert.AreEqual(nWhole, n, 1);
            for (int i = 0; i < Mathf.Min(n, nWhole); i++) Assert.AreEqual(outWhole[i], outChunked[i], 1e-4f, $"sample {i}");
        }

        [Test]
        public void VoicesFadeWithDistanceAndGoDullBehindWalls()
        {
            Assert.AreEqual(1f, VoiceAcoustics.DistanceGain(0.5f, S), 0.05f, "right at the mouth: full");
            Assert.Greater(VoiceAcoustics.DistanceGain(5f, S), VoiceAcoustics.DistanceGain(15f, S));
            Assert.AreEqual(0f, VoiceAcoustics.DistanceGain(S.MaxDistance, S));
            VoiceAcoustics.Blocked(1f, S, out float openVol, out float openCut);
            VoiceAcoustics.Blocked(0.35f, S, out float wallVol, out float wallCut); // one wall (HearingSettings.Wall)
            VoiceAcoustics.Blocked(0f, S, out float shutVol, out float shutCut);
            Assert.AreEqual(1f, openVol);
            Assert.Greater(openCut, 20000f);
            Assert.Less(wallVol, openVol);
            Assert.Less(wallCut, 3000f, "a voice through a wall is muffled");
            Assert.AreEqual(S.BlockedVolume, shutVol, 1e-4f);
            Assert.AreEqual(S.BlockedCutoff, shutCut, 1f);
        }

        [Test]
        public void RoomsRingByTheirSurfacesAndSize()
        {
            var office = VoiceAcoustics.ReverbOf(VoiceAcoustics.SpaceOf(RoomType.Office), 20f, 3f, 1f);
            var toilet = VoiceAcoustics.ReverbOf(VoiceAcoustics.SpaceOf(RoomType.Restroom), 12f, 3f, 1f);
            var lobby = VoiceAcoustics.ReverbOf(VoiceAcoustics.SpaceOf(RoomType.Lobby), 150f, 8f, 1f);
            var sewer = VoiceAcoustics.ReverbOf(VoiceSpace.Tunnel, 60f, 4f, 1f);
            Assert.Greater(toilet.Decay, office.Decay, "tiles ring longer than carpet");
            Assert.Greater(toilet.Reverb, office.Reverb);
            Assert.Greater(lobby.Decay, toilet.Decay, "a big hall has the longest tail");
            Assert.Greater(sewer.EchoDelay, 0f, "the tunnels echo");
            Assert.AreEqual(0f, office.EchoDelay);
            Assert.IsTrue(VoiceAcoustics.ReverbOf(VoiceSpace.Outdoors, 100f, 3f, 1f).IsDry);
            Assert.IsTrue(VoiceAcoustics.ReverbOf(VoiceSpace.Tiled, 10f, 3f, 0f).IsDry, "reverb off in the config");
            var big = VoiceAcoustics.ReverbOf(VoiceSpace.Hard, 200f, 4f, 1f);
            var small = VoiceAcoustics.ReverbOf(VoiceSpace.Hard, 8f, 3f, 1f);
            Assert.Greater(big.Decay, small.Decay);
        }

        [Test]
        public void EveryRoomTypeHasASpace()
        {
            foreach (RoomType t in System.Enum.GetValues(typeof(RoomType)))
                Assert.AreNotEqual(VoiceSpace.Outdoors, VoiceAcoustics.SpaceOf(t), t.ToString());
        }

        // ---------------------------------------------------------------- the native plugin (Windows)

        [Test]
        public void OpusCarriesSpeechAndConcealsLoss()
        {
            if (!VoiceNative.Available) Assert.Ignore("StealthVoice plugin not available on this platform");
            using var enc = new VoiceEncoder(S.Bitrate, S.PacketLoss);
            using var dec = new VoiceDecoder();
            var frame = new float[VoiceNative.Frame];
            var output = new float[VoiceNative.Frame];
            var packet = new byte[VoicePacket.MaxPayload];
            double energyIn = 0, energyOut = 0;
            int bytes = 0;
            for (int f = 0; f < 50; f++)
            {
                for (int i = 0; i < frame.Length; i++)
                {
                    float t = (f * frame.Length + i) / 48000f;
                    frame[i] = 0.3f * Mathf.Sin(2 * Mathf.PI * 220f * t) + 0.15f * Mathf.Sin(2 * Mathf.PI * 660f * t);
                }
                int n = enc.Encode(frame, packet);
                Assert.Greater(n, 0);
                Assert.LessOrEqual(n, VoicePacket.MaxPayload);
                bytes += n;
                Assert.AreEqual(VoiceNative.Frame, dec.Decode(packet, n, output, false));
                if (f < 10) continue; // past the codec's start-up
                foreach (float v in frame) energyIn += v * v;
                foreach (float v in output) energyOut += v * v;
            }
            Assert.AreEqual(1.0, energyOut / energyIn, 0.15, "the level survives the codec");
            Assert.Less(bytes * 8 / 1f, S.Bitrate * 1.5f, "about the configured bitrate (1 s of audio)");
            Assert.AreEqual(VoiceNative.Frame, dec.Decode(null, 0, output, false), "a lost frame is concealed");
        }

        [Test]
        public void DenoiserRemovesHissAndKnowsItIsNotVoice()
        {
            if (!VoiceNative.Available) Assert.Ignore("StealthVoice plugin not available on this platform");
            using var dn = new VoiceDenoiser();
            var frame = new float[VoiceNative.DenoiseFrame];
            var rng = new System.Random(7);
            double inE = 0, outE = 0;
            float prob = 0f;
            for (int f = 0; f < 100; f++)
            {
                for (int i = 0; i < frame.Length; i++) frame[i] = (float)(rng.NextDouble() * 2 - 1) * 0.1f;
                if (f >= 20) foreach (float v in frame) inE += v * v;
                prob = dn.Process(frame);
                if (f >= 20) foreach (float v in frame) outE += v * v;
            }
            Assert.Less(outE, inE * 0.01, "hiss is cut by more than 20 dB");
            Assert.Less(prob, 0.5f);
        }
    }
}
