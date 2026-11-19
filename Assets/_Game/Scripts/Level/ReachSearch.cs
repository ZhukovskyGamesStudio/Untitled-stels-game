using System.Collections.Generic;
using Stealth.Enemies;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A thing the player works at with their hands (everything used with E — <see cref="Interactable"/> — and a safe's
    /// dial, a terminal, a locked door's lock): what <see cref="ReachSearch.Nearest{T}"/> needs to find the one under the crosshair.
    /// </summary>
    public interface IReachTarget
    {
        /// <summary>Can be worked at now, by a player standing here (a closed safe, a locked door, a set-up terminal).</summary>
        bool CanReach(Vector3 player);
        /// <summary>Where it is, at a standing player's middle height: the flat reach and the floor are measured to here.</summary>
        Vector3 ReachPoint { get; }
        /// <summary>Its own horizontal reach, metres (or <see cref="float.PositiveInfinity"/>: the caller's reach only).</summary>
        float MaxReach { get; }
        /// <summary>The span the crosshair must pass near (bottom, top, radius) for the player to be looking at it.</summary>
        (Vector3 from, Vector3 to, float radius) AimSpan { get; }
        /// <summary>Where the hands go (no wall in between); null: no such check (a door is the wall itself).</summary>
        Vector3? HandsTo { get; }
        /// <summary>Colliders under this do not stand between the hands and <see cref="HandsTo"/> (the thing's own body).</summary>
        Transform ReachRoot { get; }
    }

    /// <summary>The one rule a hand target is found by: on the player's floor, in reach (flat), looked at, no wall between.</summary>
    public static class ReachSearch
    {
        /// <summary>How far above or below a standing player's middle (<see cref="Middle"/> over the feet) such a thing may be.</summary>
        public const float Band = 1.7f;
        /// <summary>A standing player's middle over the feet.</summary>
        public const float Middle = 0.9f;

        /// <summary>
        /// The nearest of <paramref name="all"/> a player standing at <paramref name="player"/> (the feet) can work at with
        /// hands at <paramref name="hands"/>, within <paramref name="reach"/> and the thing's own reach, and — with
        /// <paramref name="aim"/> — under the crosshair, passing <paramref name="also"/> if given; null if none.
        /// </summary>
        public static T Nearest<T>(IReadOnlyList<T> all, Vector3 player, Vector3 hands, float reach, Aim? aim, System.Func<T, bool> also = null) where T : Component, IReachTarget
        {
            T best = null;
            float bestD = reach;
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                if (t == null) continue;
                var p = t.ReachPoint;
                if (Mathf.Abs(player.y + Middle - p.y) > Band || !t.CanReach(player) || (also != null && !also(t))) continue;
                var to = p - player;
                to.y = 0f;
                float d = to.magnitude;
                if (d > bestD || d > t.MaxReach) continue;
                if (aim.HasValue)
                {
                    var (a, b, r) = t.AimSpan;
                    if (!aim.Value.At(a, b, r)) continue;
                }
                var into = t.HandsTo;
                if (into.HasValue && !Perception.ReachClear(hands, into.Value, t.ReachRoot)) continue;
                best = t;
                bestD = d;
            }
            return best;
        }
    }
}
