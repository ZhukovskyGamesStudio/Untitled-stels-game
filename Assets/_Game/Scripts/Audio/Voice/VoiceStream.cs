using System.Threading;

namespace Stealth.Audio
{
    /// <summary>
    /// A ring of samples between one writer thread and one reader thread (the main thread decodes voice into it, the
    /// audio thread plays it out of it) without locks.
    /// </summary>
    public sealed class VoiceRing
    {
        readonly float[] m_Data;
        int m_Write, m_Read; // only the writer moves m_Write, only the reader moves m_Read

        public VoiceRing(int capacity) => m_Data = new float[capacity + 1];

        public int Capacity => m_Data.Length - 1;
        public int Count { get { int n = Volatile.Read(ref m_Write) - Volatile.Read(ref m_Read); return n < 0 ? n + m_Data.Length : n; } }
        public int Free => Capacity - Count;

        /// <summary>Appends what fits; returns how many were written.</summary>
        public int Write(float[] src, int offset, int count)
        {
            int w = m_Write, written = 0;
            int free = Free;
            for (; written < count && written < free; written++)
            {
                m_Data[w] = src[offset + written];
                if (++w == m_Data.Length) w = 0;
            }
            Volatile.Write(ref m_Write, w);
            return written;
        }

        /// <summary>The next sample, or 0 (and false) when it has run dry.</summary>
        public bool Read(out float sample)
        {
            int r = m_Read;
            if (r == Volatile.Read(ref m_Write)) { sample = 0f; return false; }
            sample = m_Data[r];
            if (++r == m_Data.Length) r = 0;
            Volatile.Write(ref m_Read, r);
            return true;
        }

        /// <summary>Reader side: drops everything buffered.</summary>
        public void Drain() => Volatile.Write(ref m_Read, Volatile.Read(ref m_Write));
    }

    /// <summary>
    /// Streaming linear resampler (mono): the microphone's rate to the codec's 48 kHz, 48 kHz to the speakers' rate.
    /// Keeps its phase across calls, so a stream cut into any chunks resamples the same.
    /// </summary>
    public sealed class VoiceResampler
    {
        readonly double m_Step; // input samples per output sample
        double m_Pos;           // position of the next output sample, relative to m_Last (−1 … 0) / the chunk
        float m_Last;
        bool m_Primed;

        public VoiceResampler(int fromRate, int toRate) => m_Step = (double)fromRate / toRate;

        public bool IsIdentity => System.Math.Abs(m_Step - 1.0) < 1e-9;

        /// <summary>Resamples <paramref name="count"/> input samples into <paramref name="dst"/>; returns how many were written.</summary>
        public int Process(float[] src, int offset, int count, float[] dst, int dstOffset)
        {
            if (IsIdentity)
            {
                System.Array.Copy(src, offset, dst, dstOffset, count);
                return count;
            }
            if (count <= 0) return 0;
            if (!m_Primed) { m_Last = src[offset]; m_Pos = 0; m_Primed = true; }
            int n = 0;
            // input index -1 is m_Last, 0..count-1 the chunk
            while (m_Pos < count - 1)
            {
                int i = (int)System.Math.Floor(m_Pos);
                float frac = (float)(m_Pos - i);
                float a = i < 0 ? m_Last : src[offset + i];
                float b = src[offset + i + 1];
                dst[dstOffset + n++] = a + (b - a) * frac;
                m_Pos += m_Step;
            }
            m_Last = src[offset + count - 1];
            m_Pos -= count; // re-base onto the next chunk (m_Last becomes index -1)
            return n;
        }

        /// <summary>How many output samples at most <see cref="Process"/> writes for this many input samples.</summary>
        public int MaxOut(int count) => (int)System.Math.Ceiling((count + 1) / m_Step) + 1;
    }
}
