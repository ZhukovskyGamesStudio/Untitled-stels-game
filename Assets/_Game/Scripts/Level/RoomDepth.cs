using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// How deep a room lies in the house (pure): steps from the lobby over the ways people walk — doors, open passages,
    /// breaches, the hidden room's illusion walls, stairs (vents, slots, holes and inner windows are the intruder's, not the
    /// building's). Like a real building, public rooms are shallow and private ones deep (space syntax): the zoning puts
    /// types by it (<see cref="RoomRule.Privacy"/>), the loot is worth more deep inside, the robots keep to the deep rooms.
    /// </summary>
    public static class RoomDepth
    {
        /// <summary>An opening kind people walk through (the building's own ways).</summary>
        public static bool Walked(EdgeType k) => k is EdgeType.Door or EdgeType.Open or EdgeType.Breach or EdgeType.Illusion;

        /// <summary>Steps from the lobby per room id (−1: not reached that way).</summary>
        public static int[] Of(HousePlan plan, IReadOnlyList<Stair> stairs, int lobby)
        {
            var steps = new int[plan.Rooms.Count];
            for (int i = 0; i < steps.Length; i++) steps[i] = -1;
            if (lobby < 0 || lobby >= steps.Length) return steps;
            var links = new List<int>[plan.Rooms.Count];
            void Link(int a, int b)
            {
                if (a < 0 || b < 0 || a >= links.Length || b >= links.Length) return;
                (links[a] ??= new List<int>()).Add(b);
                (links[b] ??= new List<int>()).Add(a);
            }
            foreach (var o in plan.Openings)
            {
                if (!Walked(o.Kind)) continue;
                var w = plan.Walls[o.Wall];
                Link(w.RoomA, w.RoomB);
            }
            if (stairs != null) foreach (var s in stairs) Link(s.Room, s.RoomAbove);
            var q = new Queue<int>();
            steps[lobby] = 0;
            q.Enqueue(lobby);
            while (q.Count > 0)
            {
                int r = q.Dequeue();
                if (links[r] == null) continue;
                foreach (int n in links[r])
                    if (steps[n] < 0) { steps[n] = steps[r] + 1; q.Enqueue(n); }
            }
            return steps;
        }

        /// <summary>How deep a room is, 0 (the lobby) … 1 (the deepest room); unreached rooms count as deepest.</summary>
        public static float Share(int[] steps, int room)
        {
            if (steps == null || room < 0 || room >= steps.Length) return 0f;
            int max = 1;
            foreach (int s in steps) max = Mathf.Max(max, s);
            return steps[room] < 0 ? 1f : steps[room] / (float)max;
        }
    }
}
