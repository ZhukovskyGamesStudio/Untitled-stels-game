using System;
using System.Collections.Generic;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Player;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Stealth.Game
{
    /// <summary>
    /// The running multiplayer session as game code sees it. Implemented by the network layer (assembly Stealth.Net,
    /// <c>NgoSessionBackend</c>); game code never touches Netcode directly.
    /// </summary>
    public interface ISessionBackend
    {
        bool IsRunning { get; }
        bool IsServer { get; }
        ulong LocalClientId { get; }
        /// <summary>Clock shared by all peers (seconds). Drives deterministic motion: camera sweeps, lidars, lasers.</summary>
        double ServerTime { get; }

        /// <summary>Server: instantiate a networked prefab, let <paramref name="configure"/> set it up, then spawn it for everyone.</summary>
        GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Action<GameObject> configure);
        /// <summary>Server: remove a networked object for everyone.</summary>
        void Despawn(GameObject instance);
    }

    /// <summary>Starting and leaving sessions (menu). Implemented by <c>GameNetwork</c> in Stealth.Net.</summary>
    public interface ISessionLauncher
    {
        string Status { get; }
        ushort LocalPort { get; }
        bool IsHost { get; }
        bool SteamAvailable { get; }
        /// <summary>"connected as …" / "unavailable — reason".</summary>
        string SteamStatus { get; }
        /// <summary>Steam persona name, or null without Steam.</summary>
        string LocalPlayerName { get; }
        bool CanInviteFriends { get; }
        /// <summary>Friends' sessions of this game (Steam), refreshed by <see cref="RefreshFriendLobbies"/>.</summary>
        IReadOnlyList<FriendLobby> FriendLobbies { get; }

        void HostSteam();
        /// <summary>Ask Steam which friends are in a session now (results arrive asynchronously).</summary>
        void RefreshFriendLobbies();
        void JoinFriendLobby(ulong lobbyId);
        void HostLocal();
        void JoinLocal(string address);
        void InviteFriends();
        void Leave();
    }

    /// <summary>
    /// Static access point to the session. Without a running session (tools, tests, the menu) this peer is its own
    /// authority, time is local time and spawning is a plain Instantiate.
    /// </summary>
    public static class Session
    {
        public static ISessionBackend Backend { get; set; }
        public static ISessionLauncher Launcher { get; set; }

        public static bool IsRunning => Backend != null && Backend.IsRunning;
        public static bool IsServer => !IsRunning || Backend.IsServer;
        public static ulong LocalClientId => IsRunning ? Backend.LocalClientId : 0;
        public static double ServerTime => IsRunning ? Backend.ServerTime : Time.timeAsDouble;
        public static float ServerTimeF => (float)ServerTime;

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Action<GameObject> configure = null)
        {
            if (IsRunning) return Backend.Spawn(prefab, position, rotation, configure);
            var go = Object.Instantiate(prefab, position, rotation);
            configure?.Invoke(go);
            return go;
        }

        public static void Despawn(GameObject instance)
        {
            if (instance == null) return;
            if (IsRunning) Backend.Despawn(instance);
            else if (Application.isPlaying) Object.Destroy(instance);
            else Object.DestroyImmediate(instance);
        }
    }

    // ---------------------------------------------------------------------- links to the network twins
    // Each networked game component has a twin in Stealth.Net that replicates its state and carries its messages.
    // The game component talks to the twin only through these interfaces.

    /// <summary>Player → network: owner requests to the server and server events for the owner.</summary>
    /// <summary>The test cheats of the F4 panel (<see cref="Stealth.UI.CheatPanel"/>), carried out by the server.</summary>
    public enum Cheat : byte { KillRobots, Invulnerable, Invisible, FillQuota }

    public interface IPlayerLink
    {
        /// <summary>
        /// Owner → server, while at a terminal (refreshed a few times a second): which terminal (-1 = stood up) and the
        /// camera being turned by hand (null if none) with its yaw.
        /// </summary>
        void SendTerminal(int terminal, Detector steered, float yaw);

        /// <summary>Owner → server: used something with E (<see cref="Stealth.Level.Interactable"/>: a crate, the med table…).</summary>
        void SendUse(Stealth.Level.UseKind kind, int index);

        /// <summary>Owner → server: set off the stun cube from the backpack.</summary>
        void SendGadget();

        /// <summary>Owner → server: a cheat from the F4 panel.</summary>
        void SendCheat(Cheat cheat, bool on);
        /// <summary>Owner: move without interpolation (respawn, test teleports).</summary>
        void Teleport(Vector3 position, Quaternion rotation);
        void SendNoise(Vector3 position, float radius);
        void SendGasKnockout();
        void SendRevive(PlayerController target);
        /// <summary>Owner → server: get up after being knocked down (HP left).</summary>
        void SendGetUp();
        void SendHack(Detector target);
        /// <summary>Owner → server: hack this robot / device from the hacking terminal (index in the layout's props).</summary>
        void SendRemoteHack(Detector target, int terminal);
        /// <summary>Owner → server: "here!" at a point the owner looks at.</summary>
        void SendPing(Vector3 position);
        /// <summary>Owner → server: mark this robot / device for the team.</summary>
        void SendMark(Detector target);

        /// <summary>Owner → server: mark the thing at this point for the team (loot, a crate, a panel…; not a robot).</summary>
        void SendMarkThing(Vector3 point);
        /// <summary>Owner → server: hide in the hatch (true) or climb back out (false).</summary>
        void SendHatch(bool hide);
        /// <summary>Owner → server: finished hacking the lock of this door (index in the layout's doors).</summary>
        void SendUnlockDoor(int door);
        /// <summary>Owner → server: cracked the dial of this safe (index in the layout's props).</summary>
        void SendOpenSafe(int safe);
        /// <summary>Owner → server: open or close a drawer / locker door (index in the layout's props, part of it).</summary>
        void SendToggleStorage(int prop, int part);
        /// <summary>Owner → server: use the item in the hand (switch the flashlight, put a battery in a flashlight).</summary>
        void SendUseHand();
        /// <summary>Server → owner: you were hit, knock the camera back.</summary>
        void SendHitFeedback(Vector3 awayFromSource);
    }

    /// <summary>Inventory → network: owner requests (the server validates them in <see cref="PlayerInventory"/>).</summary>
    public interface IInventoryLink
    {
        void RequestPickup(WorldItem item, byte x, byte y, byte rot);
        void RequestMove(int index, byte x, byte y, byte rot);
        void RequestDrop(int index, Vector3 position, float yaw);
        void RequestDeposit(int index);
        // Team stash at the hatch. Stash entries are named by value: another player may have changed the list meanwhile.
        void RequestStash(int index, byte x, byte y, byte rot);
        void RequestUnstash(InventoryEntry from, byte x, byte y, byte rot);
        void RequestStashMove(InventoryEntry from, byte x, byte y, byte rot);
        void RequestSellStashed(InventoryEntry from);
        void RequestStashWorld(WorldItem item, byte x, byte y, byte rot);
        void RequestSellWorld(WorldItem item);
        /// <summary>Everything in the backpack into the first free spots of the stash (what does not fit stays).</summary>
        void RequestStashAll();
        /// <summary>A thing from the car's bed into a cell of the backpack (named by value: <see cref="Vehicles.CargoItem.Pack"/>).</summary>
        void RequestTakeCargo(ulong cargo, byte x, byte y, byte rot);
        /// <summary>A battery (backpack index) onto a flashlight (backpack index): recharge.</summary>
        void RequestRecharge(int battery, int flashlight);
        /// <summary>A battery lying in the world onto a flashlight (backpack index): recharge.</summary>
        void RequestRechargeWorld(WorldItem battery, int flashlight);
        /// <summary>An item of the team stash dropped into the world.</summary>
        void RequestDropStashed(InventoryEntry from, Vector3 position, float yaw);
    }

    /// <summary>Run state → network: requests from any player and banners for everyone.</summary>
    public interface IGameLink
    {
        void RequestRestart();
        /// <summary>Any player → server: the whole team switches to this visual style (index in VisualStyles.All).</summary>
        void RequestStyle(int index);
        void BroadcastBanner(string text);
        /// <summary>Server → everyone: a teammate's ping or mark.</summary>
        void BroadcastPing(ulong owner, PingKind kind, Vector3 position, ulong target);
    }

    /// <summary>Robot / device → network: one-shot visual effects shown on every peer.</summary>
    public interface IDetectorLink
    {
        void BroadcastPulse(byte effect);
    }
}
