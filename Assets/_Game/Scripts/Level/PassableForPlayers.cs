using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Parts of a piece of furniture a player's body goes through (a desk's monitor and its stand: they stood in the way
    /// into the duct over the desk, and snagged anyone climbing across). Only the players' bodies: sight, the crosshair,
    /// dropped things and robots meet them as before.
    /// </summary>
    public class PassableForPlayers : MonoBehaviour
    {
        public Collider[] Parts;

        static readonly List<PassableForPlayers> s_All = new();
        static readonly List<Collider> s_Bodies = new();

        void OnEnable()
        {
            s_All.Add(this);
            foreach (var body in s_Bodies) Ignore(body, true);
        }

        void OnDisable() => s_All.Remove(this);

        /// <summary>Lets this body through these parts (as <see cref="Register"/> does for every piece in the world).</summary>
        public void LetThrough(Collider body) => Ignore(body, true);

        void Ignore(Collider body, bool ignore)
        {
            if (body == null || Parts == null) return;
            foreach (var part in Parts)
                if (part != null && part != body) Physics.IgnoreCollision(body, part, ignore);
        }

        /// <summary>A player's body (its character controller) is in the world: it passes through every such part, now and later.</summary>
        public static void Register(Collider body)
        {
            if (body == null || s_Bodies.Contains(body)) return;
            s_Bodies.Add(body);
            foreach (var p in s_All) p.Ignore(body, true);
        }

        public static void Unregister(Collider body) => s_Bodies.Remove(body);

        /// <summary>Whether a part is one of those (tests, tools).</summary>
        public bool Has(Collider c) => Parts != null && System.Array.IndexOf(Parts, c) >= 0;
    }
}
