using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Player
{
    /// <summary>A muddled head after the nurse's sedative (replicated: append only): the controls lie.</summary>
    public enum Muddle : byte { None, Inverted, Mirrored }

    /// <summary>Tuning of what robots do to a player short of knocking them out (PlayerConfig → Ailments).</summary>
    [System.Serializable]
    public class AilmentSettings
    {
        [Tooltip("Seconds the muddled head lasts after waking in the hospital bed.")]
        public float MuddleTime = 40f;
        [Tooltip("Seconds of E to tear out of a drone's net.")]
        public float NetHoldTime = 3f;
        [Tooltip("The net falls off by itself after this long, seconds.")]
        public float NetTimeout = 25f;
        [Tooltip("Share of the walking speed left in the cleaner's sticky goo.")]
        [Range(0.1f, 1f)] public float GooSpeed = 0.45f;
        [Tooltip("How thick the smoke of a smoke machine is to the eye (fog density inside the cloud).")]
        public float SmokeDensity = 0.55f;
    }

    /// <summary>The rules of a player's ailments (pure, unit tested).</summary>
    public static class AilmentRules
    {
        /// <summary>What the legs do with the keys: inverted — everything backwards; mirrored — left is right.</summary>
        public static Vector2 Move(Vector2 move, Muddle muddle) => muddle switch
        {
            Muddle.Inverted => -move,
            Muddle.Mirrored => new Vector2(-move.x, move.y),
            _ => move,
        };

        /// <summary>The mouse lies too when everything is inverted.</summary>
        public static Vector2 Look(Vector2 look, Muddle muddle) => muddle == Muddle.Inverted ? new Vector2(look.x, -look.y) : look;

        /// <summary>Which muddle a sleeper wakes with (by a seeded number).</summary>
        public static Muddle Pick(int seed) => (seed & 1) == 0 ? Muddle.Inverted : Muddle.Mirrored;

        /// <summary>Share of the speed left: none in a net or stunned, a part of it in the goo.</summary>
        public static float Speed(bool goo, bool netted, bool stunned, AilmentSettings s) => netted || stunned ? 0f : goo ? s.GooSpeed : 1f;
    }

    /// <summary>What lies or hangs in the air where robots left it (replicated as pulses: append only).</summary>
    public enum HazardKind : byte { Goo, Smoke, Gas }

    /// <summary>
    /// The puddles and clouds robots leave (pure): the cleaner's sticky goo, the smoke machine's smoke, the exterminator's
    /// laughing gas. Every peer keeps the same list (the server announces each one with its end on the shared clock);
    /// a player asks it what they stand in.
    /// </summary>
    public static class Hazards
    {
        public struct Patch
        {
            public HazardKind Kind;
            public Vector3 Position;
            public float Radius;
            /// <summary>Server time it is gone at.</summary>
            public double Until;
        }

        public static readonly List<Patch> All = new();

        /// <summary>How far above or below a patch still counts as in it (one storey apart is out), metres.</summary>
        public const float Height = 2f;

        public static void Add(HazardKind kind, Vector3 position, float radius, double until) =>
            All.Add(new Patch { Kind = kind, Position = position, Radius = radius, Until = until });

        public static void Expire(double now) => All.RemoveAll(p => p.Until <= now);

        public static void Clear() => All.Clear();

        /// <summary>A point is in a patch of this kind now.</summary>
        public static bool At(HazardKind kind, Vector3 point, double now)
        {
            foreach (var p in All)
            {
                if (p.Kind != kind || p.Until <= now || Mathf.Abs(point.y - p.Position.y) > Height) continue;
                float dx = point.x - p.Position.x, dz = point.z - p.Position.z;
                if (dx * dx + dz * dz <= p.Radius * p.Radius) return true;
            }
            return false;
        }
    }
}
