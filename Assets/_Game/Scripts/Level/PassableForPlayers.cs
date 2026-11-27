using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Parts of a piece of furniture a player's body goes through (a desk's monitor and its stand: they stood in the way
    /// into the duct over the desk, and snagged anyone climbing across). Only the players' bodies: sight, the crosshair,
    /// dropped things and robots meet them as before. Unity keeps Physics.IgnoreCollision only while both colliders stay
    /// enabled: a body switched off and on again (placed somewhere, out of a seat, carried by the lift) is let through
    /// anew (<see cref="Refresh"/>). The body's own probes — headroom, the ground under it — skip these parts too
    /// (<see cref="Raycast"/>, <see cref="SphereCast"/>).
    /// </summary>
    public class PassableForPlayers : MonoBehaviour
    {
        public Collider[] Parts;

        static readonly List<PassableForPlayers> s_All = new();
        static readonly List<Collider> s_Bodies = new();
        static readonly HashSet<Collider> s_Parts = new();
        static readonly RaycastHit[] s_Hits = new RaycastHit[24];

        void OnEnable()
        {
            s_All.Add(this);
            Track(true);
            foreach (var body in s_Bodies) Ignore(body, true);
        }

        void OnDisable()
        {
            s_All.Remove(this);
            Track(false);
        }

        /// <summary>Its parts are (or are no longer) what a body's probes skip (public for the tests: edit mode enables nothing).</summary>
        public void Track(bool on)
        {
            if (Parts == null) return;
            foreach (var part in Parts)
            {
                if (part == null) continue;
                if (on) s_Parts.Add(part); else s_Parts.Remove(part);
            }
        }

        /// <summary>The collider is a part a player's body goes through.</summary>
        public static bool IsPart(Collider c) => c != null && s_Parts.Contains(c);

        /// <summary>
        /// The body was switched off and on again: it has lost everything it ignored — let it through every such part anew.
        /// </summary>
        public static void Refresh(Collider body)
        {
            if (body == null) return;
            foreach (var p in s_All) p.Ignore(body, true);
        }

        /// <summary>A ray as a player's body meets the world: the nearest thing that is not such a part.</summary>
        public static bool Raycast(Vector3 from, Vector3 direction, out RaycastHit hit, float distance, int mask) =>
            Nearest(Physics.RaycastNonAlloc(from, direction, s_Hits, distance, mask, QueryTriggerInteraction.Ignore), out hit);

        /// <summary>A sphere swept as a player's body meets the world (what it starts inside does not count, as with Physics.SphereCast).</summary>
        public static bool SphereCast(Vector3 from, float radius, Vector3 direction, out RaycastHit hit, float distance, int mask) =>
            Nearest(Physics.SphereCastNonAlloc(from, radius, direction, s_Hits, distance, mask, QueryTriggerInteraction.Ignore), out hit);

        static bool Nearest(int count, out RaycastHit hit)
        {
            hit = default;
            bool any = false;
            for (int i = 0; i < count; i++)
            {
                var h = s_Hits[i];
                if (IsPart(h.collider) || (h.distance <= 0f && h.point == Vector3.zero) || (any && h.distance >= hit.distance)) continue;
                hit = h;
                any = true;
            }
            return any;
        }

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
