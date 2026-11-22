using System;

namespace Stealth.Audio
{
    /// <summary>What the jitter buffer hands the decoder for the next 20 ms.</summary>
    public enum VoiceFrame : byte
    {
        /// <summary>Nothing to play (the speaker is silent, or the buffer is still filling).</summary>
        None,
        /// <summary>The frame itself.</summary>
        Packet,
        /// <summary>This frame was lost, the next one is here: decode the lost one from its redundancy (Opus FEC).</summary>
        Fec,
        /// <summary>This frame was lost: let the decoder conceal it.</summary>
        Lost,
    }

    /// <summary>
    /// Voice packets arrive late, early and out of order; the buffer puts them back in sequence and plays them at a steady
    /// 20 ms. It waits for <c>target</c> frames before starting (the cushion against jitter), drops packets that arrive
    /// after their turn, skips ahead when it holds more than <c>max</c> frames (latency must not grow), conceals a few
    /// missing frames and goes quiet when the speaker stops sending. A missing frame is only given up on when playback
    /// is about to run dry (<c>urgent</c>): before that it is waited for — a packet a few milliseconds late used to be
    /// concealed, then thrown away when it came, and network jitter became constant little dropouts.
    /// </summary>
    public sealed class VoiceJitter
    {
        public const int Slots = 64;
        /// <summary>Missing frames concealed before the buffer decides the speaker has stopped.</summary>
        public const int ConcealFrames = 3;

        readonly byte[][] m_Data = new byte[Slots][];
        readonly int[] m_Length = new int[Slots];
        readonly ushort[] m_Seq = new ushort[Slots];
        readonly bool[] m_Has = new bool[Slots];
        int m_Count;
        bool m_Playing, m_Started;
        ushort m_Next, m_Newest;
        int m_Missing;

        public bool Playing => m_Playing;
        public int Count => m_Count;
        /// <summary>Packets that came after their turn had been concealed (the cushion was too thin): <see cref="VoiceCushion"/> grows on them.</summary>
        public int Late { get; private set; }

        /// <summary>Frames from the next to play up to the newest held.</summary>
        public int Span => m_Count == 0 ? 0 : Math.Max(0, VoicePacket.Ahead(m_Newest, m_Next) + 1);

        public VoiceJitter()
        {
            for (int i = 0; i < Slots; i++) m_Data[i] = new byte[VoicePacket.MaxPayload];
        }

        public void Push(ushort seq, byte[] src, int offset, int length)
        {
            if (length > VoicePacket.MaxPayload) return;
            if (!m_Started)
            {
                m_Started = true;
                m_Next = m_Newest = seq;
            }
            int ahead = VoicePacket.Ahead(seq, m_Next);
            if (ahead < 0)
            {
                if (m_Playing) { Late++; return; } // its turn has passed
                m_Next = seq;           // still filling: an earlier frame came late, start from it
            }
            else if (ahead >= Slots)
            {
                Clear(); // far ahead (a long silence, or a restart): start over from it
                m_Started = true;
                m_Next = m_Newest = seq;
            }
            int slot = seq & (Slots - 1);
            if (m_Has[slot] && m_Seq[slot] == seq) return; // a duplicate
            if (!m_Has[slot]) m_Count++;
            m_Has[slot] = true;
            m_Seq[slot] = seq;
            m_Length[slot] = length;
            Buffer.BlockCopy(src, offset, m_Data[slot], 0, length);
            if (VoicePacket.Ahead(seq, m_Newest) > 0 || m_Count == 1) m_Newest = seq;
        }

        /// <summary>
        /// The next 20 ms; copies the packet to decode (the frame, or the next one for FEC) into <paramref name="dst"/>.
        /// Not <paramref name="urgent"/> (playback still has sound buffered) and the frame not here yet: None, and it is
        /// waited for instead of concealed.
        /// </summary>
        public VoiceFrame Pop(byte[] dst, out int length, int target, int max, bool urgent = true)
        {
            length = 0;
            if (!m_Playing)
            {
                if (m_Count == 0 || Span < Math.Max(1, target)) return VoiceFrame.None;
                m_Playing = true;
                m_Missing = 0;
            }
            while (Span > Math.Max(target + 1, max)) Drop(m_Next++);

            if (Take(m_Next, dst, out length, true))
            {
                m_Next++;
                m_Missing = 0;
                return VoiceFrame.Packet;
            }
            if (!urgent) return VoiceFrame.None; // not here yet and there is still sound to play: wait for it
            ushort after = (ushort)(m_Next + 1);
            if (m_Missing < ConcealFrames && Take(after, dst, out length, false))
            {
                m_Next++;
                m_Missing++;
                return VoiceFrame.Fec;
            }
            m_Next++;
            m_Missing++;
            if (m_Count == 0 && m_Missing > ConcealFrames)
            {
                m_Playing = false; // the speaker stopped: fill again before the next words
                m_Started = false;
                return VoiceFrame.None;
            }
            return m_Missing <= ConcealFrames ? VoiceFrame.Lost : VoiceFrame.None;
        }

        public void Clear()
        {
            Array.Clear(m_Has, 0, Slots);
            m_Count = 0;
            m_Playing = m_Started = false;
            m_Missing = 0;
        }

        bool Take(ushort seq, byte[] dst, out int length, bool remove)
        {
            int slot = seq & (Slots - 1);
            length = 0;
            if (!m_Has[slot] || m_Seq[slot] != seq) return false;
            length = m_Length[slot];
            Buffer.BlockCopy(m_Data[slot], 0, dst, 0, length);
            if (remove) Drop(seq);
            return true;
        }

        void Drop(ushort seq)
        {
            int slot = seq & (Slots - 1);
            if (m_Has[slot] && m_Seq[slot] == seq)
            {
                m_Has[slot] = false;
                m_Count--;
            }
        }
    }

    /// <summary>
    /// The cushion the listener keeps against jitter, in frames: the configured one to start with, one more each time a
    /// packet came too late, back down by one after a quiet stretch with none late (it applies from the next words on).
    /// </summary>
    public sealed class VoiceCushion
    {
        int m_Extra, m_SeenLate;
        float m_CalmFor;

        public int Extra => m_Extra;

        public int Frames(int configured, int max) => Math.Min(Math.Max(1, configured + m_Extra), Math.Max(1, max - 2));

        /// <summary>Each frame: <paramref name="late"/> is the jitter buffer's count so far, <paramref name="dt"/> seconds passed.</summary>
        public void Update(int late, float dt, float calmToShrink, int maxExtra)
        {
            if (late > m_SeenLate)
            {
                m_Extra = Math.Min(m_Extra + 1, maxExtra);
                m_CalmFor = 0f;
            }
            else if ((m_CalmFor += dt) >= calmToShrink && m_Extra > 0)
            {
                m_Extra--;
                m_CalmFor = 0f;
            }
            m_SeenLate = late;
        }
    }
}
