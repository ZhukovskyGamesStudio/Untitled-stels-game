using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>
    /// The car's shredder (pure, unit tested): the things thrown into the hopper make a pile in its mouth and are chewed one
    /// after another — each falls in (<see cref="FallTime"/>), then the blades take <see cref="ChewTime"/> on it, and only
    /// then is it fuel. Kept by the server (<see cref="Game.GameManager.Shredder"/>) and replicated: every peer shows the
    /// very things being eaten (CarShredder), by the server's clock.
    /// </summary>
    public sealed class Shredder
    {
        /// <summary>Seconds a thing takes to drop into the mouth, and the blades take on it.</summary>
        public const float FallTime = 0.45f, ChewTime = 0.8f;

        readonly List<(byte shape, byte color)> m_Pile = new();

        /// <summary>The pile, the one being chewed first.</summary>
        public IReadOnlyList<(byte shape, byte color)> Pile => m_Pile;
        /// <summary>Server time the blades started on the first thing of the pile (its fall ends then).</summary>
        public double Started { get; private set; }
        public int Version { get; private set; }

        /// <summary>A thing thrown in at <paramref name="now"/>: on top of the pile (an empty mouth starts on it once it has fallen).</summary>
        public void Add(byte shape, byte color, double now)
        {
            if (m_Pile.Count == 0) Started = now + FallTime;
            m_Pile.Add((shape, color));
            Version++;
        }

        /// <summary>Server: the things chewed up by <paramref name="now"/> go into <paramref name="done"/> (their fuel is due).</summary>
        public int Tick(double now, List<(byte shape, byte color)> done)
        {
            int n = 0;
            while (m_Pile.Count > 0 && now >= Started + ChewTime)
            {
                done.Add(m_Pile[0]);
                m_Pile.RemoveAt(0);
                Started += ChewTime;
                Version++;
                n++;
            }
            return n;
        }

        /// <summary>How far the blades are through the first thing (0..1); below 0 while it is still falling in.</summary>
        public float Progress(double now) => m_Pile.Count == 0 ? 0f : Mathf.Min(1f, (float)((now - Started) / ChewTime));

        public bool Busy => m_Pile.Count > 0;

        public void Clear()
        {
            if (m_Pile.Count == 0) return;
            m_Pile.Clear();
            Version++;
        }

        public static ulong Pack((byte shape, byte color) item) => item.shape | (ulong)item.color << 8;

        /// <summary>Peers: the server's pile and its clock.</summary>
        public void ApplyReplicated(IReadOnlyList<ulong> packed, double started)
        {
            m_Pile.Clear();
            foreach (var v in packed) m_Pile.Add(((byte)(v & 0xFF), (byte)(v >> 8 & 0xFF)));
            Started = started;
            Version++;
        }
    }
}
