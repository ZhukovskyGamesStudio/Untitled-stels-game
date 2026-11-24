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

        /// <summary>How far a thing going into the mouth leans off upright at most, degrees.</summary>
        public const float MaxTilt = 12f;

        /// <summary>
        /// How a thing of <paramref name="size"/> (its box in its own axes) goes into the mouth: its smallest face down —
        /// its longest side upright, as anyone would feed a long thing in (a shovel handle or blade first, never across) —
        /// which end first, its turn about the upright and a lean of up to <see cref="MaxTilt"/> by <paramref name="seed"/>.
        /// </summary>
        public static Quaternion FeedTurn(Vector3 size, int seed)
        {
            // the longest side's axis to the upright
            int longest = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            var axis = longest == 0 ? Vector3.right : longest == 1 ? Vector3.up : Vector3.forward;
            var upright = Quaternion.FromToRotation(axis, Vector3.up);
            uint h = Hash((uint)seed);
            if ((h & 1u) != 0u) upright = Quaternion.AngleAxis(180f, Vector3.forward) * upright; // the other end first
            float yaw = (h >> 1 & 0x3FF) / 1024f * 360f;
            float lean = (h >> 11 & 0x3FF) / 1023f * MaxTilt;
            float leanTo = (h >> 21 & 0x3FF) / 1024f * 360f;
            var tilt = Quaternion.AngleAxis(lean, Quaternion.AngleAxis(leanTo, Vector3.up) * Vector3.right);
            return tilt * Quaternion.AngleAxis(yaw, Vector3.up) * upright;
        }

        /// <summary>Half the height of a box of <paramref name="size"/> turned by <paramref name="turn"/> (from its centre to its lowest point).</summary>
        public static float HalfHeight(Quaternion turn, Vector3 size)
        {
            var x = turn * Vector3.right;
            var y = turn * Vector3.up;
            var z = turn * Vector3.forward;
            return 0.5f * (Mathf.Abs(x.y) * size.x + Mathf.Abs(y.y) * size.y + Mathf.Abs(z.y) * size.z);
        }

        static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352dU; x ^= x >> 15; x *= 0x846ca68bU; x ^= x >> 16;
            return x;
        }

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
