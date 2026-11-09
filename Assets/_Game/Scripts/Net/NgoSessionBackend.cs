using System;
using System.Collections.Generic;
using Stealth.Game;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Stealth.Net
{
    /// <summary>
    /// A network twin that mirrors its game component into network variables. The authority calls
    /// <see cref="PushState"/> every frame (cheap: only changed values are written) and right before spawning.
    /// </summary>
    public interface INetSync
    {
        void PushState();
    }

    /// <summary>Helpers for network twins.</summary>
    public static class NetSync
    {
        /// <summary>Writes only when the value changed (no dirty flag, no traffic otherwise).</summary>
        public static void Set<T>(NetworkVariable<T> variable, T value)
        {
            if (!EqualityComparer<T>.Default.Equals(variable.Value, value)) variable.Value = value;
        }
    }

    /// <summary><see cref="ISessionBackend"/> on top of Netcode for GameObjects.</summary>
    public sealed class NgoSessionBackend : ISessionBackend
    {
        readonly NetworkManager m_Manager;

        public NgoSessionBackend(NetworkManager manager) => m_Manager = manager;

        public bool IsRunning => m_Manager != null && (m_Manager.IsServer || m_Manager.IsClient);
        public bool IsServer => m_Manager.IsServer;
        public ulong LocalClientId => m_Manager.LocalClientId;
        public double ServerTime => m_Manager.ServerTime.Time;

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Action<GameObject> configure)
        {
            var go = Object.Instantiate(prefab, position, rotation);
            configure?.Invoke(go);
            // Initial values go out with the spawn message.
            foreach (var sync in go.GetComponents<INetSync>()) sync.PushState();
            go.GetComponent<NetworkObject>().Spawn(true);
            return go;
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null) return;
            var no = instance.GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned) no.Despawn();
            else if (no == null) Object.Destroy(instance);
        }
    }
}
