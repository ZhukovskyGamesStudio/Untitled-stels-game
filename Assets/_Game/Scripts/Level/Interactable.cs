using System.Collections.Generic;
using Stealth.Enemies;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>What a player uses with E (the server's handler is picked by it: <see cref="Game.GameManager.UseServer"/>).</summary>
    public enum UseKind : byte { Crate, MedTable, Workbench, KeyDoor, GateLever, CarSeat, CarHopper, CarCargo }

    /// <summary>
    /// Something in the world a player uses with E — press, or hold for <see cref="HoldTime"/>: a crate to search, the
    /// med table, the workbench, a key lock, the sewer gate's lever, a seat of the car, its engine hopper. Built on every
    /// peer (with the level, the sewer or the car); <see cref="Index"/> tells the server which one. The player finds the
    /// nearest one in reach (<see cref="Near"/>), the HUD shows <see cref="Prompt"/>, the request goes through
    /// <see cref="IPlayerLink.SendUse"/> and the server checks it again.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
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

        /// <summary>The hint for this player, and whether pressing E does something now.</summary>
        public abstract string Prompt(PlayerController player, out bool available);

        public void Configure(int index)
        {
            Index = index;
            OnConfigured();
        }

        protected virtual void OnConfigured() { }

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        /// <summary>The one in reach of a player (same floor, nothing between the hands and it), nearest first; null if none.</summary>
        public static Interactable Near(PlayerController player, Vector3 hands)
        {
            Interactable best = null;
            float bestD = float.MaxValue;
            var at = player.transform.position;
            foreach (var i in All)
            {
                if (i.Index < 0 || !i.isActiveAndEnabled) continue;
                var p = i.Point;
                if (Mathf.Abs(at.y + 0.9f - p.y) > 1.7f) continue;
                var to = p - at;
                to.y = 0f;
                float d = to.magnitude;
                if (d > i.Reach || d >= bestD) continue;
                if (!Perception.ReachClear(hands, p, i.transform)) continue;
                best = i;
                bestD = d;
            }
            return best;
        }
    }
}
