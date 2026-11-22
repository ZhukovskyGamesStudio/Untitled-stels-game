using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// The spider's web (pure, unit tested): webs spun across its room like the walls of a simple maze — each a whole
    /// sheet from the floor up to the ceiling, from one long wall towards the other, leaving a gap at alternating ends: no
    /// stepping over, no ducking under, the way through zig-zags from gap to gap. None runs past a doorway. A player who
    /// touches one brings the spider (<see cref="SpiderBot"/>); every peer spins the same webs from the room and the
    /// spider's place.
    /// </summary>
    public static class SpiderWebs
    {
        public struct Strand
        {
            /// <summary>The sheet's foot line along the floor (A to B) and the height of its top.</summary>
            public Vector3 A, B;
            public float Top;
            public float Height => Top - A.y;
        }

        /// <summary>The sheets hang this far over the floor and stop this far under the ceiling.</summary>
        public const float Foot = 0.03f, UnderCeiling = 0.05f;

        /// <summary>
        /// The wires of a room (<paramref name="room"/> in x/z, its floor at <paramref name="floorY"/>): across the shorter
        /// side every <paramref name="spacing"/> metres along the longer, a gap of <paramref name="gap"/> at alternating
        /// ends, at most <paramref name="max"/>, none within 1.5 m of a doorway (<paramref name="doors"/>).
        /// </summary>
        public static List<Strand> Plan(Rect room, float floorY, IReadOnlyList<Vector3> doors, System.Random rng,
            float spacing = 1.7f, float gap = 1.0f, int max = 6, float ceiling = Stealth.Level.LevelMetrics.WallHeight)
        {
            var list = new List<Strand>();
            bool alongX = room.width >= room.height;
            float length = alongX ? room.width : room.height, across = alongX ? room.height : room.width;
            if (length < 3f || across < gap + 1.2f) return list;
            const float inset = 0.2f;
            int n = Mathf.Min(max, Mathf.FloorToInt((length - spacing) / spacing));
            float start = (length - (n - 1) * spacing) / 2f;
            for (int i = 0; i < n; i++)
            {
                float u = start + i * spacing;
                // the wire's line, and its ends from one long wall to the gap before the other
                float from = inset, to = across - inset;
                if (i % 2 == 0) to -= gap; else from += gap;
                rng.NextDouble(); // (the wires' height once: the stream of the rest stays as it was)
                float y = floorY + Foot;
                Vector3 P(float along, float side) => alongX
                    ? new Vector3(room.xMin + along, y, room.yMin + side)
                    : new Vector3(room.xMin + side, y, room.yMin + along);
                var a = P(u, from);
                var b = P(u, to);
                bool nearDoor = false;
                foreach (var d in doors)
                    if (Mathf.Abs(d.y - floorY) < 1.5f && DistanceToSegment(new Vector2(d.x, d.z), new Vector2(a.x, a.z), new Vector2(b.x, b.z)) < 1.5f) { nearDoor = true; break; }
                if (!nearDoor) list.Add(new Strand { A = a, B = b, Top = floorY + ceiling - UnderCeiling });
            }
            return list;
        }

        /// <summary>A body (feet at <paramref name="feet"/>, this tall and wide) touches the sheet.</summary>
        public static bool Touches(in Strand s, Vector3 feet, float height, float radius = 0.3f)
        {
            if (feet.y + height < s.A.y || feet.y > s.Top) return false;
            return DistanceToSegment(new Vector2(feet.x, feet.z), new Vector2(s.A.x, s.A.z), new Vector2(s.B.x, s.B.z)) < radius + 0.03f;
        }

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }
    }
}
