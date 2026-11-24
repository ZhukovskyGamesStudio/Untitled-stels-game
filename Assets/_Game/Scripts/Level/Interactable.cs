using System.Collections.Generic;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// What a player uses with E (the server's handler is picked by it: <see cref="Game.GameManager.UseServer"/>).
    /// <see cref="DoorLock"/>, <see cref="Safe"/> and <see cref="StoragePart"/> are not <see cref="Interactable"/>s: a locked
    /// door's lock (index: the door), a safe's dial (the prop), a storage part (<see cref="OpenKeys.StoragePart"/>: the prop
    /// and its part). <see cref="CarBench"/> — the car's workbench — opens the bench on this peer, the server is not asked.
    /// </summary>
    public enum UseKind : byte { Crate, MedTable, Workbench, KeyDoor, GateLever, CarSeat, CarHopper, CarCargo, VaultConsole, Cage, HandDryer, VaultDoor, LightSwitch, VentGrate, ShaftHatch, LiftCall, LiftFloor, VentGrille, DoorLock, Safe, StoragePart, CarBench, Door, ExitButton }

    /// <summary>
    /// Something in the world a player uses with E — press, or hold for <see cref="HoldTime"/>: a crate to search, the
    /// med table, the workbench, a key lock, the sewer gate's lever, a seat of the car, its engine hopper. Built on every
    /// peer (with the level, the sewer or the car); <see cref="Index"/> tells the server which one. The player finds the
    /// nearest one in reach (<see cref="Near"/>), the HUD shows <see cref="Prompt"/>, the request goes through
    /// <see cref="IPlayerLink.SendUse"/> and the server checks it again.
    /// </summary>
    public abstract class Interactable : MonoBehaviour, IReachTarget
    {
        public static readonly List<Interactable> All = new();

        public abstract UseKind Kind { get; }
        /// <summary>Which one of its kind (prop index in the layout, door index, seat number…); -1 = not set up yet.</summary>
        public int Index { get; protected set; } = -1;
        /// <summary>Seconds E is held to use it (0 = a press).</summary>
        public virtual float HoldTime => 0f;
        /// <summary>Horizontal reach from the player, metres.</summary>
        public virtual float Reach => 1.8f;
        /// <summary>Where the hands go (a clear line from the player's hands to here is needed).</summary>
        public virtual Vector3 Point => transform.position + Vector3.up * 0.8f;
        /// <summary>How close to <see cref="Point"/> the crosshair must pass for the player to be looking at it, metres.</summary>
        public virtual float AimRadius => 0.45f;
        /// <summary>The span the crosshair must pass near (its point, unless the thing is bigger than that).</summary>
        protected virtual (Vector3 from, Vector3 to, float radius) Span { get { var p = Point; return (p, p, AimRadius); } }
        /// <summary>Colliders under this do not stand between the hands and <see cref="Point"/> (the thing's own body: a car's cab).</summary>
        public virtual Transform ReachRoot => transform;

        /// <summary>The hint for this player, and whether pressing E does something now.</summary>
        public abstract string Prompt(PlayerController player, out bool available);

        /// <summary>Taken by E at all just now (an open vent grate is climbed through, not used).</summary>
        public virtual bool Usable(PlayerController player) => true;

        public void Configure(int index)
        {
            Index = index;
            OnConfigured();
        }

        protected virtual void OnConfigured() { }

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        /// <summary>
        /// The one in reach of a player (same floor, nothing between the hands and it) and, with <paramref name="aim"/>,
        /// under the crosshair; nearest first; null if none.
        /// </summary>
        public static Interactable Near(PlayerController player, Vector3 hands, Aim? aim = null)
            => ReachSearch.Nearest(All, player.transform.position, hands, float.PositiveInfinity, aim, i => i.Usable(player));

        bool IReachTarget.CanReach(Vector3 player) => Index >= 0 && isActiveAndEnabled;
        Vector3 IReachTarget.ReachPoint => Point;
        float IReachTarget.MaxReach => Reach;
        (Vector3, Vector3, float) IReachTarget.AimSpan => Span;
        Vector3? IReachTarget.HandsTo => Point;
        Transform IReachTarget.ReachRoot => ReachRoot;
    }
}
