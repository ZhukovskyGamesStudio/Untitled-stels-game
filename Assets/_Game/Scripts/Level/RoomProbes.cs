using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The rooms' own reflections (pure rules): every room has a reflection probe — a cube picture of the room from its
    /// middle, laid back onto its walls as a box — so that gloss and metal have something to mirror beyond what happens to
    /// be on the screen. A picture only: robots read the light through <see cref="LightSource"/>. A probe is taken again
    /// when its room's lamps change, and its strength follows the lamps at once (a room switched off mirrors nothing
    /// while its new picture is still being taken).
    /// </summary>
    public static class RoomProbes
    {
        /// <summary>Probes are taken only this near the camera, metres: farther lamps are not drawn (<see cref="LightBudget"/>), the picture would be dark.</summary>
        public const float Reach = 14f;
        /// <summary>The room the camera stands in is taken again this often, seconds: doors, drawers and robots have moved.</summary>
        public const float RefreshEvery = 12f;
        /// <summary>A room's lamps changed by this share: its picture is stale.</summary>
        public const float Changed = 0.05f;
        /// <summary>The probe's eye above the room's floor, metres.</summary>
        public const float EyeHeight = 1.5f;

        /// <summary>Where a room's probe stands and the box it fills (world): the room's bounds, floor to ceiling (all its storeys).</summary>
        public static (Vector3 eye, Vector3 centre, Vector3 size) Box(PlanRoom room)
        {
            float floor = LevelLayout.FloorY(room.Floor);
            float height = (Mathf.Max(1, room.Levels) - 1) * LevelMetrics.FloorHeight + LevelMetrics.WallHeight;
            var b = room.Bounds;
            return (new Vector3(room.Center.x, floor + EyeHeight, room.Center.z),
                    new Vector3(b.center.x, floor + height * 0.5f, b.center.y),
                    new Vector3(b.width, height, b.height));
        }

        /// <summary>
        /// How much of its picture a room shows: it follows the room's lamps down at once (never up: a brighter room needs a
        /// new picture). A room whose lamps are out mirrors nothing — and no picture is taken of it: what would be in it is
        /// whoever walked through with a flashlight.
        /// </summary>
        public static float Share(float lampsNow, float lampsThen) => lampsThen <= 0.001f ? 0f : Mathf.Clamp01(lampsNow / lampsThen);

        /// <summary>From this smoothness a surface mirrors its room.</summary>
        public const float MirrorFrom = 0.6f;
        /// <summary>From this much metal a surface mirrors its room whatever its smoothness (metal has no colour but what it mirrors).</summary>
        public const float MirrorMetal = 0.5f;

        /// <summary>
        /// Whether a material shows its room's picture at all. Matt things do not: on them the picture was no reflection,
        /// only a veil of light at glancing angles and grain — every bump of the relief mirrored another bit of the room.
        /// </summary>
        public static bool Mirrors(float smoothness, float metal) => smoothness >= MirrorFrom || metal >= MirrorMetal;

        /// <summary>Whether a picture is stale: the lamps burn differently from when it was taken.</summary>
        public static bool Stale(float lampsNow, float lampsThen) => Mathf.Abs(lampsNow - lampsThen) > Changed * Mathf.Max(lampsNow, lampsThen, 0.02f);

        /// <summary>One probe as the chooser sees it.</summary>
        public struct State
        {
            /// <summary>Metres from the camera to the probe's box (0 inside it).</summary>
            public float Distance;
            /// <summary>Whether it has a picture at all.</summary>
            public bool Taken;
            /// <summary>Its lamps changed since.</summary>
            public bool Stale;
            /// <summary>Seconds since its picture was taken.</summary>
            public float Age;
            /// <summary>The room's lamps are out: nothing of its own to mirror.</summary>
            public bool Dark;
        }

        /// <summary>
        /// Which probe to take next, or −1: within <see cref="Reach"/> — one with no picture yet, then a stale one (the
        /// nearest first), then the one the camera stands in when its picture is old.
        /// </summary>
        public static int Next(IReadOnlyList<State> probes)
        {
            int best = -1;
            float bestRank = float.MaxValue;
            for (int i = 0; i < probes.Count; i++)
            {
                var p = probes[i];
                if (p.Distance > Reach || p.Dark) continue;
                float rank;
                if (!p.Taken) rank = p.Distance;
                else if (p.Stale) rank = 1000f + p.Distance;
                else if (p.Distance <= 0f && p.Age >= RefreshEvery) rank = 2000f - p.Age;
                else continue;
                if (rank < bestRank) { bestRank = rank; best = i; }
            }
            return best;
        }
    }
}
