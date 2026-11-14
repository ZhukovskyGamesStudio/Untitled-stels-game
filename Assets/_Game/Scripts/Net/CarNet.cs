using Stealth.Vehicles;
using Unity.Netcode;
using Unity.Netcode.Components;

namespace Stealth.Net
{
    /// <summary>
    /// Network twin of <see cref="Car"/>: replicates who sits where and hands the car to its driver — the driver's peer
    /// owns the NetworkObject and moves it (owner-authoritative NetworkTransform), with no driver it goes back to the
    /// server. Carries the horn to everyone.
    /// </summary>
    public class CarNet : NetworkBehaviour, ICarLink, INetSync
    {
        readonly NetworkVariable<ulong> m_Seat0 = new(Car.Nobody), m_Seat1 = new(Car.Nobody), m_Seat2 = new(Car.Nobody), m_Seat3 = new(Car.Nobody);
        readonly ulong[] m_Buffer = new ulong[Car.SeatCount];
        Car m_Car;
        ulong m_Owner = ulong.MaxValue;

        void Awake() => m_Car = GetComponent<Car>();

        public override void OnNetworkSpawn()
        {
            m_Car.Bind(this, IsServer);
            if (IsServer) { PushState(); return; }
            Pull();
            m_Seat0.OnValueChanged += (_, _) => Pull();
            m_Seat1.OnValueChanged += (_, _) => Pull();
            m_Seat2.OnValueChanged += (_, _) => Pull();
            m_Seat3.OnValueChanged += (_, _) => Pull();
        }

        void Update()
        {
            if (IsSpawned) m_Car.HasAuthority = IsOwner;
        }

        void LateUpdate()
        {
            if (IsSpawned && IsServer) PushState();
        }

        public void PushState()
        {
            var seats = m_Car.Occupants;
            NetSync.Set(m_Seat0, seats[0]);
            NetSync.Set(m_Seat1, seats[1]);
            NetSync.Set(m_Seat2, seats[2]);
            NetSync.Set(m_Seat3, seats[3]);
            // the driver's peer moves the car; nobody at the wheel — the server holds it
            ulong owner = seats[0] != Car.Nobody && NetworkManager.ConnectedClients.ContainsKey(seats[0]) ? seats[0] : NetworkManager.ServerClientId;
            if (owner == m_Owner || NetworkObject.OwnerClientId == owner) { m_Owner = owner; return; }
            m_Owner = owner;
            if (owner == NetworkManager.ServerClientId) NetworkObject.RemoveOwnership();
            else NetworkObject.ChangeOwnership(owner);
        }

        void Pull()
        {
            m_Buffer[0] = m_Seat0.Value;
            m_Buffer[1] = m_Seat1.Value;
            m_Buffer[2] = m_Seat2.Value;
            m_Buffer[3] = m_Seat3.Value;
            m_Car.ApplySeats(m_Buffer);
        }

        public void SendHorn() => HornRpc();

        [Rpc(SendTo.Everyone, RequireOwnership = false)]
        void HornRpc() => m_Car.PlayHorn();
    }
}
