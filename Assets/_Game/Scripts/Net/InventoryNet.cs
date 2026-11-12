using System;
using System.Collections.Generic;
using Stealth.Game;
using Stealth.Items;
using Stealth.Player;
using Unity.Netcode;
using UnityEngine;

namespace Stealth.Net
{
    /// <summary>Wire format of an <see cref="InventoryEntry"/> (5 bytes).</summary>
    public struct NetInventoryEntry : INetworkSerializeByMemcpy, IEquatable<NetInventoryEntry>
    {
        public byte Shape, Color, X, Y, Rot;

        public static NetInventoryEntry From(InventoryEntry e) => new() { Shape = e.Shape, Color = e.Color, X = e.X, Y = e.Y, Rot = e.Rot };
        public InventoryEntry ToEntry() => new() { Shape = Shape, Color = Color, X = X, Y = Y, Rot = Rot };
        public bool Equals(NetInventoryEntry o) => Shape == o.Shape && Color == o.Color && X == o.X && Y == o.Y && Rot == o.Rot;
    }

    /// <summary>
    /// Network twin of <see cref="PlayerInventory"/>: the server's contents are replicated to everyone (teammates see
    /// colours for sorters, the owner draws the grid); the owner's drag-and-drop requests go to the server.
    /// </summary>
    public class InventoryNet : NetworkBehaviour, IInventoryLink, INetSync
    {
        NetworkList<NetInventoryEntry> m_Items;
        PlayerInventory m_Inventory;
        int m_PushedVersion = -1;
        readonly List<InventoryEntry> m_Buffer = new();

        void Awake()
        {
            m_Inventory = GetComponent<PlayerInventory>();
            m_Items = new NetworkList<NetInventoryEntry>();
        }

        public override void OnNetworkSpawn()
        {
            m_Inventory.Bind(this);
            if (IsServer) { PushState(); return; }
            Pull();
            m_Items.OnListChanged += _ => Pull();
        }

        public override void OnNetworkDespawn() => m_Inventory.Bind(null);

        void LateUpdate()
        {
            if (IsSpawned && IsServer) PushState();
        }

        public void PushState()
        {
            if (m_PushedVersion == m_Inventory.Version) return;
            m_PushedVersion = m_Inventory.Version;
            m_Items.Clear();
            foreach (var e in m_Inventory.Items) m_Items.Add(NetInventoryEntry.From(e));
        }

        void Pull()
        {
            m_Buffer.Clear();
            foreach (var e in m_Items) m_Buffer.Add(e.ToEntry());
            m_Inventory.ApplyReplicated(m_Buffer);
        }

        // ------------------------------------------------------------------ requests (owner → server)

        public void RequestPickup(WorldItem item, byte x, byte y, byte rot) => PickupRpc(item.GetComponent<NetworkObject>(), x, y, rot);
        public void RequestMove(int index, byte x, byte y, byte rot) => MoveRpc(index, x, y, rot);
        public void RequestDrop(int index, Vector3 position, float yaw) => DropRpc(index, position, yaw);
        public void RequestDeposit(int index) => DepositRpc(index);
        public void RequestStash(int index, byte x, byte y, byte rot) => StashRpc(index, x, y, rot);
        public void RequestUnstash(InventoryEntry from, byte x, byte y, byte rot) => UnstashRpc(NetInventoryEntry.From(from), x, y, rot);
        public void RequestStashMove(InventoryEntry from, byte x, byte y, byte rot) => StashMoveRpc(NetInventoryEntry.From(from), x, y, rot);
        public void RequestSellStashed(InventoryEntry from) => SellStashedRpc(NetInventoryEntry.From(from));
        public void RequestStashWorld(WorldItem item, byte x, byte y, byte rot) => StashWorldRpc(item.GetComponent<NetworkObject>(), x, y, rot);
        public void RequestSellWorld(WorldItem item) => SellWorldRpc(item.GetComponent<NetworkObject>());
        public void RequestStashAll() => StashAllRpc();

        [Rpc(SendTo.Server)]
        void StashAllRpc() => m_Inventory.StashAllServer();

        [Rpc(SendTo.Server)]
        void PickupRpc(NetworkObjectReference itemRef, byte x, byte y, byte rot)
        {
            if (itemRef.TryGet(out var obj)) m_Inventory.PickupServer(obj.GetComponent<WorldItem>(), x, y, rot);
        }

        [Rpc(SendTo.Server)]
        void MoveRpc(int index, byte x, byte y, byte rot) => m_Inventory.MoveServer(index, x, y, rot);

        [Rpc(SendTo.Server)]
        void DropRpc(int index, Vector3 position, float yaw) => m_Inventory.DropServer(index, position, yaw);

        [Rpc(SendTo.Server)]
        void DepositRpc(int index) => m_Inventory.DepositServer(index);

        [Rpc(SendTo.Server)]
        void StashRpc(int index, byte x, byte y, byte rot) => m_Inventory.StashServer(index, x, y, rot);

        [Rpc(SendTo.Server)]
        void UnstashRpc(NetInventoryEntry from, byte x, byte y, byte rot) => m_Inventory.UnstashServer(from.ToEntry(), x, y, rot);

        [Rpc(SendTo.Server)]
        void StashMoveRpc(NetInventoryEntry from, byte x, byte y, byte rot) => m_Inventory.StashMoveServer(from.ToEntry(), x, y, rot);

        [Rpc(SendTo.Server)]
        void SellStashedRpc(NetInventoryEntry from) => m_Inventory.SellStashedServer(from.ToEntry());

        [Rpc(SendTo.Server)]
        void StashWorldRpc(NetworkObjectReference itemRef, byte x, byte y, byte rot)
        {
            if (itemRef.TryGet(out var obj)) m_Inventory.StashWorldServer(obj.GetComponent<WorldItem>(), x, y, rot);
        }

        [Rpc(SendTo.Server)]
        void SellWorldRpc(NetworkObjectReference itemRef)
        {
            if (itemRef.TryGet(out var obj)) m_Inventory.SellWorldServer(obj.GetComponent<WorldItem>());
        }
    }
}
