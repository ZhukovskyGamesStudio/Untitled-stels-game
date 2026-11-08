using System;
using System.Collections.Generic;
using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using Unity.Netcode;
using UnityEngine;

namespace Stealth.Player
{
    public struct InventoryEntry : INetworkSerializeByMemcpy, IEquatable<InventoryEntry>
    {
        public byte Shape, Color, X, Y, Rot;

        public bool Equals(InventoryEntry o) => Shape == o.Shape && Color == o.Color && X == o.X && Y == o.Y && Rot == o.Rot;
        public int Value => ItemCatalog.Value(Shape, Color);
        public Vector2Int[] Cells => ItemCatalog.RotatedCells(Shape, Rot);
    }

    /// <summary>Tarkov-like grid inventory. Server-authoritative; the owner sends requests.</summary>
    public class PlayerInventory : NetworkBehaviour
    {
        public const float ReachDistance = 3.5f;

        public NetworkList<InventoryEntry> Items;

        void Awake() => Items = new NetworkList<InventoryEntry>();

        public int TotalValue
        {
            get
            {
                int sum = 0;
                foreach (var e in Items) sum += e.Value;
                return sum;
            }
        }

        public HashSet<Vector2Int> Occupied(int ignoreIndex = -1)
        {
            var set = new HashSet<Vector2Int>();
            for (int i = 0; i < Items.Count; i++)
            {
                if (i == ignoreIndex) continue;
                var e = Items[i];
                foreach (var c in e.Cells) set.Add(new Vector2Int(e.X + c.x, e.Y + c.y));
            }
            return set;
        }

        public int IndexAt(Vector2Int cell)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                var e = Items[i];
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

        bool CanAct => !GetComponent<PlayerController>().Downed.Value;

        // ------------------------------------------------------------------ requests (owner -> server)

        [Rpc(SendTo.Server)]
        public void PickupRpc(NetworkObjectReference itemRef, byte x, byte y, byte rot)
        {
            if (!CanAct || !itemRef.TryGet(out var obj)) return;
            var item = obj.GetComponent<WorldItem>();
            if (item == null) return;
            if ((item.transform.position - transform.position).magnitude > ReachDistance + 1.5f) return;
            if (!CanPlace(item.Shape.Value, rot, x, y)) return;
            Items.Add(new InventoryEntry { Shape = item.Shape.Value, Color = item.Color.Value, X = x, Y = y, Rot = rot });
            obj.Despawn();
        }

        [Rpc(SendTo.Server)]
        public void MoveRpc(int index, byte x, byte y, byte rot)
        {
            if (index < 0 || index >= Items.Count) return;
            var e = Items[index];
            if (!CanPlace(e.Shape, rot, x, y, index)) return;
            e.X = x; e.Y = y; e.Rot = rot;
            Items[index] = e;
        }

        [Rpc(SendTo.Server)]
        public void DropRpc(int index, Vector3 position, float yaw)
        {
            if (index < 0 || index >= Items.Count) return;
            if ((position - transform.position).magnitude > ReachDistance + 1.5f)
                position = transform.position + transform.forward * 0.8f;
            var e = Items[index];
            Items.RemoveAt(index);
            var gm = GameManager.Instance;
            var obj = Instantiate(gm.ItemPrefab, position, Quaternion.Euler(0, yaw, 0));
            var item = obj.GetComponent<WorldItem>();
            item.Shape.Value = e.Shape;
            item.Color.Value = e.Color;
            obj.Spawn(true);
        }

        [Rpc(SendTo.Server)]
        public void DepositRpc(int index)
        {
            if (index < 0 || index >= Items.Count || !Hatch.IsNear(transform.position)) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.Phase.Value != RunPhase.Playing) return;
            var e = Items[index];
            Items.RemoveAt(index);
            gm.AddCollectedServer(e.Value);
        }

        public void ClearServer() => Items.Clear();
    }
}
