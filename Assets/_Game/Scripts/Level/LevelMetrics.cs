using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>Sizes of the office building shared by the generator, the builder, the prefabs and the sensors.</summary>
    public static class LevelMetrics
    {
        public const float Cell = 10f;
        public const float WallHeight = 3f;
        public const float FloorHeight = 3.5f; // floor-to-floor: walls + slab
        public const float SlabThickness = FloorHeight - WallHeight;
        public const float WallThickness = 0.2f;
        public const float DoorWidth = 1.8f;
        public const float DoorHeight = 2.3f;
        public const float VentWidth = 1.2f;  // nominal; the clear opening is 1.0 m (see ClearWidth)
        public const float VentHeight = 1.4f; // crouched capsule 1.1 m + PhysX step-up probe (stepOffset 0.35); standing 1.8 m does not fit
        public const float SqueezeWidth = 1.1f; // nominal; the clear slot is 0.9 m: a player (0.78 m with skin) fits, a robot does not (navmesh volume)
        public const float WindowWidth = 2.4f;   // the window model's opening
        public const float WindowHeight = 1.2f;
        public const float WindowCenterY = 1.7f; // above the floor
        /// <summary>World-space text (route panels, the stash board): drawn after the style filter, so every style keeps it readable.</summary>
        public const int WorldTextLayer = 9;
        public const int CeilingLayer = 2;    // "Ignore Raycast": kept out of the navmesh and vision checks
        public const int StairsArea = 3;      // NavMesh area "Stairs": robots without legs exclude it

        // Staircase geometry in the corner frame (u, v = meters from the corner walls into the room).
        public const int StepsPerFlight = 6;
        public const float StepRun = 0.42f;
        public const float StairSize = 4f;    // the stair + hole occupy [0, 4] x [0, 4] from the corner
        public const float FlightStart = 3.8f; // u where flight A starts / flight B ends
        public const float LandingEnd = FlightStart - StepsPerFlight * StepRun; // 1.28

        /// <summary>Clear width of an opening (door, vent): the jambs take half a wall thickness on each side.</summary>
        public static float ClearWidth(float nominal) => nominal - WallThickness;

        /// <summary>Furniture quadrants of a room (offsets from its centre).</summary>
        public static readonly Vector3[] Quadrants =
        {
            new(2.7f, 0, 2.7f), new(-2.7f, 0, 2.7f), new(2.7f, 0, -2.7f), new(-2.7f, 0, -2.7f),
        };
    }

    /// <summary>
    /// U-shaped staircase as boxes in the corner frame: x = u (along the corner wall), z = v (into the room), y up.
    /// Flight A runs from the room towards the corner, a landing, flight B runs back up to the next floor.
    /// The asset builder turns this into the Stairs prefabs; tests check the geometry.
    /// </summary>
    public static class StairGeometry
    {
        public readonly struct Box
        {
            public readonly string Name;
            public readonly Vector3 Center;
            public readonly Vector3 Size;
            public Box(string name, Vector3 center, Vector3 size) { Name = name; Center = center; Size = size; }
        }

        // Flight lanes across the stair (v ranges).
        public const float LaneA0 = 0.2f, LaneA1 = 1.95f, LaneB0 = 2.05f, LaneB1 = 3.85f;

        public static List<Box> Boxes()
        {
            const float half = LevelMetrics.FloorHeight / 2f;
            const float rise = half / LevelMetrics.StepsPerFlight;
            const float run = LevelMetrics.StepRun, size = LevelMetrics.StairSize, h = LevelMetrics.FloorHeight;
            const float start = LevelMetrics.FlightStart, landing = LevelMetrics.LandingEnd;
            var list = new List<Box>();
            void Add(string name, float u0, float u1, float v0, float v1, float y0, float y1) =>
                list.Add(new Box(name, new Vector3((u0 + u1) / 2f, (y0 + y1) / 2f, (v0 + v1) / 2f), new Vector3(Mathf.Abs(u1 - u0), y1 - y0, Mathf.Abs(v1 - v0))));

            for (int i = 0; i < LevelMetrics.StepsPerFlight; i++)
                Add("StepA", start - (i + 1) * run, start - i * run, LaneA0, LaneA1, 0f, (i + 1) * rise);
            Add("Landing", 0.12f, landing, 0.12f, LaneB1, 0f, half);
            for (int j = 0; j < LevelMetrics.StepsPerFlight; j++)
                Add("StepB", landing + j * run, landing + (j + 1) * run, LaneB0, LaneB1, 0f, half + (j + 1) * rise);
            Add("Exit", start, size + 0.08f, LaneB0, LaneB1, h - 0.25f, h);

            // Railings around the hole on the floor above (open only where flight B arrives).
            Add("Rail", 0.1f, size, size - 0.03f, size + 0.03f, h, h + 1.0f);
            Add("Rail", size - 0.03f, size + 0.03f, 0.1f, LaneB0 - 0.05f, h, h + 1.0f);
            // Between the flights: a thin wall so nobody walks off flight B onto flight A.
            // Starts two steps away from the landing: a divider right at the landing would leave no room to turn.
            Add("Divider", landing + 2 * run, start, LaneA1, LaneB0, 0f, h - 0.4f);
            return list;
        }

        /// <summary>
        /// Which prefab variant and yaw place the corner frame for a stair in <paramref name="corner"/> (±1, ±1).
        /// The frame maps u → -corner.x and v → -corner.y in world space; when that is a mirror image the
        /// mirrored variant is used, so the instance itself is only ever rotated (0° or 180°).
        /// </summary>
        public static (bool mirrored, float yaw) Placement(Vector2Int corner)
        {
            int ux = -corner.x, vz = -corner.y;
            bool mirrored = ux * vz < 0;
            int xAxis = mirrored ? -ux : ux; // world sign of the variant's local +X
            return (mirrored, xAxis > 0 ? 0f : 180f);
        }

        /// <summary>World position of the corner frame origin for a stair (floor level of its start).</summary>
        public static Vector3 Origin(Stair s) => new(
            (s.Cell.x + (s.Corner.x > 0 ? 1 : 0)) * LevelMetrics.Cell,
            LevelLayout.FloorY(s.Floor),
            (s.Cell.y + (s.Corner.y > 0 ? 1 : 0)) * LevelMetrics.Cell);

        /// <summary>World point of a corner-frame point (u, y, v) for a stair (the same mapping the prefab placement gives).</summary>
        public static Vector3 ToWorld(Stair s, Vector3 local) =>
            Origin(s) + new Vector3(-s.Corner.x * local.x, local.y, -s.Corner.y * local.z);
    }
}
