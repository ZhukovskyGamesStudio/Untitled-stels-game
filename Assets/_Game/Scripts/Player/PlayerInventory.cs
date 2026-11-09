using System;
using System.Collections.Generic;
using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Player
{
    public struct InventoryEntry : IEquatable<InventoryEntry>
    {
        public byte Shape, Color, X, Y, Rot;

        public bool Equals(InventoryEntry o) => Shape == o.Shape && Color == o.Color && X == o.X && Y == o.Y && Rot == o.Rot;
        public int Value => ItemCatalog.Value(Shape, Color);
        public Vector2Int[] Cells => ItemCatalog.RotatedCells(Shape, Rot);
    }

    /// <summary>
    /// Tarkov-like grid inventory. Server-authoritative: the owner calls <c>Request…</c>, the server validates in
    /// <c>…Server</c> handlers. The network twin (InventoryNet) replicates <see cref="Items"/> and carries the requests.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        public const float ReachDistance = 3.5f;

        readonly List<InventoryEntry> m_Items = new();
        IInventoryLink m_Link;
        PlayerController m_Player;

        public IReadOnlyList<InventoryEntry> Items => m_Items;
        /// <summary>Bumped on every change (the network twin replicates on change).</summary>
        public int Version { get; private set; }

        void Awake() => m_Player = GetComponent<PlayerController>();

        public void Bind(IInventoryLink link) => m_Link = link;

        /// <summary>Client: contents as decided by the server.</summary>
        public void ApplyReplicated(IEnumerable<InventoryEntry> items)
        {
            m_Items.Clear();
            m_Items.AddRange(items);
            Version++;
        }

        public int TotalValue
        {
            get
            {
                int sum = 0;
                foreach (var e in m_Items) sum += e.Value;
                return sum;
            }
        }

        public bool Carries(int color)
        {
            foreach (var e in m_Items) if (e.Color == color) return true;
            return false;
        }

        public HashSet<Vector2Int> Occupied(int ignoreIndex = -1)
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

        public bool CanPlace(int shape, int rot, int x, int y, int ignoreIndex = -1) =>
            InventoryLayout.Fits(ItemCatalog.RotatedCells(shape, rot), x, y, Occupied(ignoreIndex));

        /// <summary>First free position for an item, trying all rotations. Used by tests and quick pickup.</summary>
        public bool FindSpot(int shape, out byte x, out byte y, out byte rot)
        {
            var occupied = Occupied();
            for (int r = 0; r < 4; r++)
            {
                var cells = ItemCatalog.RotatedCells(shape, r);
                for (int yy = 0; yy < InventoryLayout.Height; yy++)
                    for (int xx = 0; xx < InventoryLayout.Width; xx++)
                        if (InventoryLayout.Fits(cells, xx, yy, occupied))
                        {
                            x = (byte)xx; y = (byte)yy; rot = (byte)r;
                            return true;
                        }
            }
            x = y = rot = 0;
            return false;
        }

        // ------------------------------------------------------------------ requests (owner → server)

        public void RequestPickup(WorldItem item, byte x, byte y, byte rot)
        {
            if (m_Link != null) m_Link.RequestPickup(item, x, y, rot);
            else PickupServer(item, x, y, rot);
        }

        public void RequestMove(int index, byte x, byte y, byte rot)
        {
            if (m_Link != null) m_Link.RequestMove(index, x, y, rot);
            else MoveServer(index, x, y, rot);
        }

        public void RequestDrop(int index, Vector3 position, float yaw)
        {
            if (m_Link != null) m_Link.RequestDrop(index, position, yaw);
            else DropServer(index, position, yaw);
        }

        public void RequestDeposit(int index)
        {
            if (m_Link != null) m_Link.RequestDeposit(index);
            else DepositServer(index);
        }

        // ------------------------------------------------------------------ server

        bool CanAct => m_Player == null || !m_Player.Downed;

        void Changed() => Version++;

        public void PickupServer(WorldItem item, byte x, byte y, byte rot)
        {
            if (!CanAct || item == null) return;
            if ((item.transform.position - transform.position).magnitude > ReachDistance + 1.5f) return;
            if (!CanPlace(item.Shape, rot, x, y)) return;
            m_Items.Add(new InventoryEntry { Shape = item.Shape, Color = item.Color, X = x, Y = y, Rot = rot });
            Changed();
            Session.Despawn(item.gameObject);
        }

        public void MoveServer(int index, byte x, byte y, byte rot)
        {
            if (index < 0 || index >= m_Items.Count) return;
            var e = m_Items[index];
            if (!CanPlace(e.Shape, rot, x, y, index)) return;
            e.X = x; e.Y = y; e.Rot = rot;
            m_Items[index] = e;
            Changed();
        }

        public void DropServer(int index, Vector3 position, float yaw)
        {
            if (index < 0 || index >= m_Items.Count) return;
            if ((position - transform.position).magnitude > ReachDistance + 1.5f)
                position = transform.position + transform.forward * 0.8f;
            var e = m_Items[index];
            m_Items.RemoveAt(index);
            Changed();
            var gm = GameManager.Instance;
            if (gm != null) gm.SpawnItemServer(e.Shape, e.Color, position, yaw);
        }

        public void DepositServer(int index)
        {
            if (index < 0 || index >= m_Items.Count || !Hatch.IsNear(transform.position)) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.Phase != RunPhase.Playing) return;
            var e = m_Items[index];
            m_Items.RemoveAt(index);
            Changed();
            gm.AddCollectedServer(e.Value);
        }

        /// <summary>Server: put an item straight into the first free spot (rewards, scripted checks). False if it does not fit.</summary>
        public bool GiveServer(byte shape, byte color)
        {
            if (!FindSpot(shape, out var x, out var y, out var rot)) return false;
            m_Items.Add(new InventoryEntry { Shape = shape, Color = color, X = x, Y = y, Rot = rot });
            Changed();
            return true;
        }

        public void ClearServer()
        {
            m_Items.Clear();
            Changed();
        }
    }
}
