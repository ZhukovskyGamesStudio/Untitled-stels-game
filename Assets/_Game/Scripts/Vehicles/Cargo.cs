using System.Collections.Generic;
using Stealth.Items;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>A thing lying in the pickup's bed: what it is and where (bed space, centimetres; no cells — a heap).</summary>
    public struct CargoItem : System.IEquatable<CargoItem>
    {
        public byte Shape, Color;
        /// <summary>Across the bed (−85 … 85 cm) and along it (0 = the tailgate … 200 cm = the cab), cm.</summary>
        public short X, Z;
        /// <summary>Turn on the bed, degrees.</summary>
        public short Yaw;

        public bool Equals(CargoItem o) => Shape == o.Shape && Color == o.Color && X == o.X && Z == o.Z && Yaw == o.Yaw;
        public int Value => ItemCatalog.Value(Shape, Color);

        /// <summary>Packed for the network (one number per item).</summary>
        public ulong Pack() => Shape | (ulong)Color << 8 | (ulong)(ushort)X << 16 | (ulong)(ushort)Z << 32 | (ulong)(ushort)Yaw << 48;

        public static CargoItem Unpack(ulong v) => new()
        {
            Shape = (byte)(v & 0xFF), Color = (byte)(v >> 8 & 0xFF),
            X = (short)(ushort)(v >> 16 & 0xFFFF), Z = (short)(ushort)(v >> 32 & 0xFFFF), Yaw = (short)(ushort)(v >> 48 & 0xFFFF),
        };
    }

    /// <summary>
    /// The pickup's bed (pure, unit tested): any number of things heaped where they were dropped — no grid, no limit.
    /// A thing lies on top of whatever is under it already (<see cref="Heights"/>), so a pile grows. Kept by the server
    /// in <see cref="Game.GameManager.Cargo"/> for the whole run and replicated to everyone.
    /// </summary>
    public sealed class Cargo
    {
        /// <summary>The bed's inner area, cm: x across, z along (0 = tailgate).</summary>
        public const short HalfWidth = 85, Length = 200;
        /// <summary>A thing's radius on the bed (cm) for heaping: its cells are 22 cm.</summary>
        const float Radius = 14f;

        readonly List<CargoItem> m_Items = new();
        public IReadOnlyList<CargoItem> Items => m_Items;
        public int Version { get; private set; }
        public int Count => m_Items.Count;

        public int Value
        {
            get
            {
                int v = 0;
                foreach (var i in m_Items) v += i.Value;
                return v;
            }
        }

        /// <summary>A thing dropped at this spot of the bed (clamped into it).</summary>
        public void Add(byte shape, byte color, float x, float z, float yaw)
        {
            m_Items.Add(new CargoItem
            {
                Shape = shape, Color = color,
                X = (short)Mathf.Clamp(Mathf.RoundToInt(x), -HalfWidth + 12, HalfWidth - 12),
                Z = (short)Mathf.Clamp(Mathf.RoundToInt(z), 12, Length - 12),
                Yaw = (short)(Mathf.RoundToInt(yaw) % 360),
            });
            Version++;
        }

        /// <summary>A spot for a thing tossed onto the bed with no aim (a seeded scatter, lowest heap first).</summary>
        public Vector2 FreeSpot(System.Random rng)
        {
            var best = Vector2.zero;
            float lowest = float.MaxValue;
            for (int i = 0; i < 6; i++)
            {
                var p = new Vector2(rng.Next(-HalfWidth + 15, HalfWidth - 15), rng.Next(15, Length - 15));
                float h = HeightAt(p, m_Items.Count);
                if (h < lowest) { lowest = h; best = p; }
            }
            return best;
        }

        public bool Remove(CargoItem item)
        {
            int i = m_Items.IndexOf(item);
            if (i < 0) return false;
            m_Items.RemoveAt(i);
            Version++;
            return true;
        }

        public void Clear()
        {
            if (m_Items.Count == 0) return;
            m_Items.Clear();
            Version++;
        }

        public void ApplyReplicated(IEnumerable<ulong> packed)
        {
            m_Items.Clear();
            foreach (var p in packed) m_Items.Add(CargoItem.Unpack(p));
            Version++;
        }

        /// <summary>How high (cm above the bed floor) the top of the heap is at a spot, counting the first <paramref name="upTo"/> things.</summary>
        float HeightAt(Vector2 p, int upTo)
        {
            float top = 0f;
            for (int i = 0; i < upTo && i < m_Items.Count; i++)
            {
                var o = m_Items[i];
                if ((new Vector2(o.X, o.Z) - p).magnitude < Radius * 2f) top = Mathf.Max(top, m_Heights.Count > i ? m_Heights[i] + Thickness : Thickness);
            }
            return top;
        }

        /// <summary>Height of one thing lying flat, cm (an item cube is 22 cm).</summary>
        public const float Thickness = 21f;

        readonly List<float> m_Heights = new();

        /// <summary>Where each thing lies, cm above the bed floor: in the order they came, each on the heap under it.</summary>
        public IReadOnlyList<float> Heights()
        {
            m_Heights.Clear();
            for (int i = 0; i < m_Items.Count; i++) m_Heights.Add(HeightAt(new Vector2(m_Items[i].X, m_Items[i].Z), i));
            return m_Heights;
        }
    }
}
