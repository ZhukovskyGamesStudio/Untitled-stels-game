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
        public static bool Walked(EdgeType k) => RoomGraph.Passes(Passage.Walkers, k);

        /// <summary>Steps from the lobby per room id (−1: not reached that way).</summary>
        public static int[] Of(HousePlan plan, IReadOnlyList<Stair> stairs, int lobby)
        {
            return new RoomGraph(plan, stairs, Passage.Walkers).Steps(lobby);
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
