using System.Collections.Generic;
using Stealth.Enemies;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A thing the player works at with their hands that is not an <see cref="Interactable"/> (a safe's dial, a terminal,
    /// a locked door's lock): what <see cref="ReachSearch.Nearest{T}"/> needs to know to find the one under the crosshair.
    /// </summary>
    public interface IReachTarget
    {
        /// <summary>Can be worked at now, by a player standing here (a closed safe, a locked door, a set-up terminal).</summary>
        bool CanReach(Vector3 player);
        /// <summary>The span the crosshair must pass near (bottom, top, radius) for the player to be looking at it.</summary>
        (Vector3 from, Vector3 to, float radius) AimSpan { get; }
        /// <summary>Where the hands go (no wall in between); null: no such check (a door is the wall itself).</summary>
        Vector3? HandsTo { get; }
    }

    /// <summary>The one rule a hand target is found by: on the player's floor, in reach (flat), looked at, no wall between.</summary>
    public static class ReachSearch
    {
        /// <summary>How far above or below the player's feet such a thing may be.</summary>
        public const float Band = 1.6f;

        public static T Nearest<T>(IReadOnlyList<T> all, Vector3 player, Vector3 hands, float reach, Aim? aim) where T : Component, IReachTarget
        {
            T best = null;
            float bestD = reach;
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                if (t == null) continue;
                var p = t.transform.position;
                if (Mathf.Abs(player.y - p.y) > Band || !t.CanReach(player)) continue;
                if (aim.HasValue)
                {
                    var (a, b, r) = t.AimSpan;
                    if (!aim.Value.At(a, b, r)) continue;
                }
                var to = player - p;
                to.y = 0f;
                float d = to.magnitude;
                if (d > bestD) continue;
                var into = t.HandsTo;
                if (into.HasValue && !Perception.ReachClear(hands, into.Value, t.transform)) continue;
                best = t;
                bestD = d;
            }
            return best;
        }
    }
}
