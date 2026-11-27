using System;
using System.Collections.Generic;

namespace Stealth.Audio
{
    /// <summary>
    /// The server's short memory of what each player said in this house, for the relay that plays a teammate's own words
    /// back to lure the team (<see cref="Stealth.Enemies.LureRelay"/>): the last frames each player transmitted to the
    /// team, kept as the encoded packets the voice chat carried, in memory only. Pure: time and the roll are parameters.
    /// </summary>
    public sealed class VoiceMemory
    {
        /// <summary>One voice frame is 20 ms (<see cref="VoiceNative.Frame"/> at 48 kHz).</summary>
        public const float FrameSeconds = 0.02f;
        /// <summary>Two frames further apart than this (or not in sequence) are not one stretch of speech, seconds.</summary>
        const float MaxGap = 0.5f;

        sealed class Frame
        {
            public readonly byte[] Data = new byte[VoicePacket.MaxSize];
            public int Length;
            public ushort Seq;
            public float Time;
            public bool Loud;
        }

        readonly Dictionary<ulong, List<Frame>> m_Frames = new();
        readonly Stack<Frame> m_Spare = new();
        readonly List<(ulong Speaker, int Start, int End)> m_Options = new();

        /// <summary>Frames remembered of this speaker.</summary>
        public int Count(ulong speaker) => m_Frames.TryGetValue(speaker, out var list) ? list.Count : 0;

        /// <summary>
        /// A frame (as it came: header and payload) that <paramref name="speaker"/> transmitted at <paramref name="time"/>;
        /// <paramref name="loud"/> — a word, not the quiet tail the gate keeps open. Only the last <paramref name="keep"/>
        /// frames of each speaker are kept (0: nothing is recorded).
        /// </summary>
        public void Add(ulong speaker, ushort seq, bool loud, float time, byte[] buffer, int offset, int length, int keep)
        {
            if (keep <= 0 || length <= 0 || length > VoicePacket.MaxSize) return;
            if (!m_Frames.TryGetValue(speaker, out var list)) m_Frames[speaker] = list = new List<Frame>();
            while (list.Count >= keep)
            {
                m_Spare.Push(list[0]);
                list.RemoveAt(0);
            }
            var f = m_Spare.Count > 0 ? m_Spare.Pop() : new Frame();
            Buffer.BlockCopy(buffer, offset, f.Data, 0, length);
            f.Length = length;
            f.Seq = seq;
            f.Time = time;
            f.Loud = loud;
            list.Add(f);
        }

        /// <summary>Forgets the frames older than <paramref name="after"/> seconds.</summary>
        public void Forget(float now, float after)
        {
            foreach (var list in m_Frames.Values)
            {
                int n = 0;
                while (n < list.Count && now - list[n].Time > after) n++;
                for (int i = 0; i < n; i++) m_Spare.Push(list[i]);
                list.RemoveRange(0, n);
            }
        }

        /// <summary>
        /// Picks a stretch of one candidate's continuous speech: it starts at a loud frame, is at least
        /// <paramref name="minFrames"/> long (else it is no word anyone would recognise) and at most <paramref name="maxFrames"/>.
        /// What is older than <paramref name="forgetAfter"/> is forgotten first; <paramref name="roll"/> chooses among every
        /// such stretch of every candidate. False (and <paramref name="frames"/> empty) when no candidate has said enough.
        /// The segments point into the memory: use them before the next <see cref="Add"/>.
        /// </summary>
        public bool Pick(IReadOnlyList<ulong> candidates, int roll, int minFrames, int maxFrames, float now, float forgetAfter,
            List<ArraySegment<byte>> frames, out ulong speaker)
        {
            frames.Clear();
            speaker = 0;
            Forget(now, forgetAfter);
            minFrames = Math.Max(1, minFrames);
            maxFrames = Math.Max(minFrames, maxFrames);
            m_Options.Clear();
            foreach (ulong id in candidates)
            {
                if (!m_Frames.TryGetValue(id, out var list)) continue;
                for (int a = 0; a < list.Count;)
                {
                    int b = a + 1;
                    while (b < list.Count && Follows(list[b - 1], list[b])) b++;
                    for (int s = a; s + minFrames <= b; s++)
                        if (list[s].Loud)
                        {
                            m_Options.Add((id, s, Math.Min(b, s + maxFrames)));
                            break;
                        }
                    a = b;
                }
            }
            if (m_Options.Count == 0) return false;
            var o = m_Options[(int)((uint)roll % (uint)m_Options.Count)];
            speaker = o.Speaker;
            var picked = m_Frames[o.Speaker];
            for (int i = o.Start; i < o.End; i++) frames.Add(new ArraySegment<byte>(picked[i].Data, 0, picked[i].Length));
            return true;
        }

        static bool Follows(Frame a, Frame b) => (ushort)(a.Seq + 1) == b.Seq && b.Time - a.Time <= MaxGap;

        /// <summary>Forgets everything (a new house, a new session).</summary>
        public void Clear()
        {
            foreach (var list in m_Frames.Values)
                foreach (var f in list) m_Spare.Push(f);
            m_Frames.Clear();
        }
    }
}
