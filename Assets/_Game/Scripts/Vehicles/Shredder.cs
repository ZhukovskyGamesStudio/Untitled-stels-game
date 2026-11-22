using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>
    /// The car's shredder (pure, unit tested), like a twin-shaft shredder: the things thrown into the hopper heap up in its
    /// mouth, and the counter-rotating toothed shafts under the heap grab up to <see cref="Lanes"/> of them at a time — each
    /// is pulled down and torn up over <see cref="ChewTime"/>, and only then is it fuel; the heap settles onto the shafts as
    /// they free up. Every thing's start is fixed when it lands (<see cref="FallTime"/> after it is thrown in, or when a
    /// lane frees, whichever is later), so the schedule replicates as plain times. Kept by the server
    /// (<see cref="Game.GameManager.Shredder"/>) and replicated: every peer shows the very things being eaten (CarShredder).
    /// </summary>
    public sealed class Shredder
    {
        /// <summary>Seconds a thing takes to land in the mouth, and the shafts take tearing it up.</summary>
        public const float FallTime = 0.45f, ChewTime = 1.3f;
        /// <summary>How many things the shafts tear at once (the bottom of the heap).</summary>
        public const int Lanes = 3;

        public struct Piece
        {
            public byte Shape, Color;
            /// <summary>Server time the shafts take it.</summary>
            public double Start;
        }

        readonly List<Piece> m_Pile = new();
        readonly double[] m_LaneFree = new double[Lanes];

        /// <summary>The heap, in the order thrown in (the first thrown in are torn first).</summary>
        public IReadOnlyList<Piece> Pile => m_Pile;
        public int Version { get; private set; }

        /// <summary>The earliest start in the heap (the base the starts replicate from), 0 when empty.</summary>
        public double Started
        {
            get
            {
                if (m_Pile.Count == 0) return 0.0;
                double min = double.MaxValue;
                foreach (var p in m_Pile) if (p.Start < min) min = p.Start;
                return min;
            }
        }

        /// <summary>Server: a thing thrown in at <paramref name="now"/>: on top of the heap; the first free shaft lane takes it once it has landed.</summary>
        public void Add(byte shape, byte color, double now)
        {
            int lane = 0;
            for (int i = 1; i < Lanes; i++) if (m_LaneFree[i] < m_LaneFree[lane]) lane = i;
            double start = System.Math.Max(now + FallTime, m_LaneFree[lane]);
            m_LaneFree[lane] = start + ChewTime;
            m_Pile.Add(new Piece { Shape = shape, Color = color, Start = start });
            Version++;
        }

        /// <summary>Server: the things torn up by <paramref name="now"/> go into <paramref name="done"/> (their fuel is due).</summary>
        public int Tick(double now, List<(byte shape, byte color)> done)
        {
            int n = 0;
            for (int i = 0; i < m_Pile.Count; i++)
            {
                if (now < m_Pile[i].Start + ChewTime) continue;
                done.Add((m_Pile[i].Shape, m_Pile[i].Color));
                m_Pile.RemoveAt(i--);
                n++;
            }
            if (n > 0) Version++;
            return n;
        }

        /// <summary>How far the shafts are through a thing (0..1); below 0 while it waits on the heap.</summary>
        public static float Progress(Piece p, double now) => Mathf.Min(1f, (float)((now - p.Start) / ChewTime));

        public bool Busy => m_Pile.Count > 0;

        public void Clear()
        {
            System.Array.Clear(m_LaneFree, 0, Lanes);
            if (m_Pile.Count == 0) return;
            m_Pile.Clear();
            Version++;
        }

        /// <summary>A thing for the wire: shape, colour and its start in milliseconds after <paramref name="baseTime"/>.</summary>
        public static ulong Pack(Piece p, double baseTime)
        {
            double ms = System.Math.Round((p.Start - baseTime) * 1000.0);
            ulong offset = (ulong)System.Math.Clamp(ms, 0.0, uint.MaxValue);
            return p.Shape | (ulong)p.Color << 8 | offset << 16;
        }

        /// <summary>Peers: the server's heap, its starts from <paramref name="baseTime"/>.</summary>
        public void ApplyReplicated(IReadOnlyList<ulong> packed, double baseTime)
        {
            m_Pile.Clear();
            foreach (var v in packed)
                m_Pile.Add(new Piece { Shape = (byte)(v & 0xFF), Color = (byte)(v >> 8 & 0xFF), Start = baseTime + (v >> 16) / 1000.0 });
            Version++;
        }
    }
}
