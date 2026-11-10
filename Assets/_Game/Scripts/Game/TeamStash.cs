using System.Collections.Generic;
using Stealth.Items;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The team's shared stash at the hatch (pure, unit tested): a rectangular grid anyone standing at the hatch can
    /// put loot into and take loot out of. It stays with the team for the whole run — loot kept here can be sold later,
    /// for a harder quota in a deeper house. Server-authoritative: <see cref="GameManager"/> owns it, the network twin
    /// replicates <see cref="Items"/>.
    /// </summary>
    public sealed class TeamStash
    {
        public const int Width = 8, Height = 4;

        readonly List<InventoryEntry> m_Items = new();

        public IReadOnlyList<InventoryEntry> Items => m_Items;
        /// <summary>Bumped on every change (the network twin replicates on change).</summary>
        public int Version { get; private set; }

        public int TotalValue
        {
            get
            {
                int sum = 0;
                foreach (var e in m_Items) sum += e.Value;
                return sum;
            }
        }

        public static bool IsCell(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        HashSet<Vector2Int> Occupied(int ignoreIndex)
        {
            var set = new HashSet<Vector2Int>();
            for (int i = 0; i < m_Items.Count; i++)
            {
                if (i == ignoreIndex) continue;
                var e = m_Items[i];
                foreach (var c in e.Cells) set.Add(new Vector2Int(e.X + c.x, e.Y + c.y));
            }
            return set;
        }

        public bool CanPlace(int shape, int rot, int x, int y, int ignoreIndex = -1)
        {
            var occupied = Occupied(ignoreIndex);
            foreach (var c in ItemCatalog.RotatedCells(shape, rot))
            {
                var p = new Vector2Int(x + c.x, y + c.y);
                if (!IsCell(p.x, p.y) || occupied.Contains(p)) return false;
            }
            return true;
        }

        public int IndexAt(Vector2Int cell)
        {
            for (int i = 0; i < m_Items.Count; i++)
            {
                var e = m_Items[i];
                foreach (var c in e.Cells)
                    if (e.X + c.x == cell.x && e.Y + c.y == cell.y) return i;
            }
            return -1;
        }

        /// <summary>Index of exactly this entry (requests name the entry they saw: the list may have changed meanwhile).</summary>
        public int IndexOf(InventoryEntry entry)
        {
            for (int i = 0; i < m_Items.Count; i++) if (m_Items[i].Equals(entry)) return i;
            return -1;
        }

        public bool Add(byte shape, byte color, byte x, byte y, byte rot)
        {
            if (!CanPlace(shape, rot, x, y)) return false;
            m_Items.Add(new InventoryEntry { Shape = shape, Color = color, X = x, Y = y, Rot = rot });
            Version++;
            return true;
        }

        public bool Move(InventoryEntry entry, byte x, byte y, byte rot)
        {
            int i = IndexOf(entry);
            if (i < 0 || !CanPlace(entry.Shape, rot, x, y, i)) return false;
            entry.X = x; entry.Y = y; entry.Rot = rot;
            m_Items[i] = entry;
            Version++;
            return true;
        }

        public bool Take(InventoryEntry entry)
        {
            int i = IndexOf(entry);
            if (i < 0) return false;
            m_Items.RemoveAt(i);
            Version++;
            return true;
        }

        public void Clear()
        {
            m_Items.Clear();
            Version++;
        }

        /// <summary>Client: contents as decided by the server.</summary>
        public void ApplyReplicated(IEnumerable<InventoryEntry> items)
        {
            m_Items.Clear();
            m_Items.AddRange(items);
            Version++;
        }
    }
}
