using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>Sizes of the office building shared by the generator, the builder, the prefabs and the sensors.</summary>
    public static class LevelMetrics
    {
        /// <summary>Size of a chunk of the house that merged meshes are cut into (and the old room grid). Rooms are free now (HousePlan).</summary>
        public const float Cell = 10f;
        /// <summary>A room's ceiling over its floor.</summary>
        public const float WallHeight = 2.75f;
        /// <summary>
        /// Floor to floor: the room, then the crawl void over its ceiling (the ducts, <see cref="VentPlan"/>), then the floor
        /// of the next storey. The stairs keep their 4 m: 14 steps of 0.29 m.
        /// </summary>
        public const float FloorHeight = 4.1f;
        /// <summary>Everything between a room's ceiling and the floor above: solid, but where a duct runs through it.</summary>
        public const float SlabThickness = FloorHeight - WallHeight;
        /// <summary>A duct's bottom (the ceiling board under it) and top (the floor of the storey above, its deck).</summary>
        public const float CeilingBoard = 0.05f, DeckThickness = 0.15f;
        /// <summary>The headroom in a duct: a crouched player (1.1 m) fits, a robot (2 m) does not.</summary>
        public const float VoidHeight = SlabThickness - CeilingBoard - DeckThickness;
        public const float WallThickness = 0.3f;
        /// <summary>
        /// A corner motion sensor's place from the corner of the walls' axes, along each axis: its back plate (0.16 m wide,
        /// 0.08 m deep, turned 45°) touches both wall faces — the inner corner is half a wall in, the plate's middle 0.08 m
        /// out of it along the diagonal, the sensor's origin (its front) 0.08 m further.
        /// </summary>
        public const float SensorCornerInset = WallThickness / 2f + (0.08f + 0.08f) * 0.70710678f;
        /// <summary>What the jambs of an opening take from its nominal width (half the old 0.2 m wall a side): doors, vents
        /// and slots keep their clear sizes whatever the walls' thickness.</summary>
        public const float OpeningInset = 0.2f;
        public const float DoorWidth = 1.8f;
        /// <summary>A narrow door (a flat's bathroom): nominal, the clear opening 1.4 m — a robot (0.5 m radius) and the walk check pass.</summary>
        public const float NarrowDoorWidth = 1.6f;
        public const float DoorHeight = 2.3f;
        /// <summary>The vault's doorway (nominal): the round bank door (bore d 2.27 m) needs 2.4 m clear; the corners round it are wall.</summary>
        public const float VaultDoorWidth = 2.6f;
        /// <summary>The auto repair shop's paint booth door, nominal (a car drives through it: 1.9 m wide with its mirrors).</summary>
        public const float VehicleDoorWidth = 3.4f;
        /// <summary>The lift's doorway on a landing, nominal (clear 1.1 m: its two sliding panels).</summary>
        public const float LiftDoorWidth = 1.3f;
        public const float VentWidth = 1.2f;  // nominal; the clear opening is 1.0 m (see ClearWidth)
        // A vent is a duct through the wall: its floor is a sill above the room's floor (a step for a player, a wall for a robot),
        // the clear height above the sill fits the crouched capsule (1.1 m) + the step-up probe (0.35); standing 1.8 m does not fit.
        public const float VentSill = 0.25f;
        public const float VentHeight = 1.5f;
        // A high vent: up under the ceiling, over a desk standing against the wall on both sides (the furniture the players
        // climb: a jump onto the desk, JumpHeight 0.9; from its top a short crouched step into the duct).
        public const float HighVentSill = 1.05f;
        /// <summary>Top of a desk (the climbing furniture under a high vent), metres.</summary>
        public const float DeskTop = 0.918f;    // the office desk's top (generate_models.DESK_TOP_NEW): taller desks, still a jump up
        /// <summary>How far the desk under a high vent and the room to climb onto it reach into the room from the wall's face.</summary>
        public const float VentDeskReach = 0.9f, VentDeskAisle = 1.0f;
        public const float SqueezeWidth = 1.1f; // nominal; the clear slot is 0.9 m: a player (0.78 m with skin) fits, a robot does not (navmesh volume)
        // Ruins between rooms (EdgeType.Breach / Hole / InnerWindow), the loops that are not doors:
        /// <summary>A breach: a stretch of wall knocked out, everyone walks through (clear 1.6 m, up to the door height).</summary>
        public const float BreachWidth = 1.8f;
        /// <summary>A hole broken through at the floor: a player crawls through crouched (1.1 m), a robot does not (navmesh volume).</summary>
        public const float HoleWidth = 1.1f, HoleHeight = 1.25f;
        /// <summary>
        /// A window between rooms without glass: the sill above a robot's step (navmesh climb 0.75 m) and under a player's jump
        /// (1.05 m); through it crouched (1.1 m under 1.25 m). Robots see and hear through it.
        /// </summary>
        public const float InnerWindowWidth = 1.6f, InnerWindowSill = 0.9f, InnerWindowHeight = 1.25f;
        public const float WindowWidth = 2.4f;   // the window model's opening
        public const float WindowHeight = 1.2f;
        public const float WindowCenterY = 1.7f; // above the floor
        /// <summary>World-space text (route panels, the stash board): drawn after the style filter, so every style keeps it readable.</summary>
        public const int WorldTextLayer = 9;
        public const int CeilingLayer = 2;    // "Ignore Raycast": kept out of the navmesh and vision checks
        public const int StairsArea = 3;      // NavMesh area "Stairs": robots without legs exclude it
        public const int LadderArea = 4;      // NavMesh area "Ladder" (the link up the basement's shaft): only the storage robot takes it

        // Staircase geometry in the corner frame (u, v = meters from the corner walls into the room).
        // seven steps a flight: a step of 0.29 m (4.1 m a storey), well under the player's 0.35 m — no jumping on the stairs
        public const int StepsPerFlight = 7;
        public const float StepRun = 0.38f;
        public const float StairSize = 4f;    // the stair + hole occupy [0, 4] x [0, 4] from the corner
        public const float FlightStart = 3.8f; // u where flight A starts / flight B ends
        public const float LandingEnd = FlightStart - StepsPerFlight * StepRun; // 1.14

        /// <summary>Nominal width of an opening of this kind (an open passage: its whole wall, not this).</summary>
        public static float OpeningWidth(EdgeType kind) => kind switch
        {
            EdgeType.Vent => VentWidth,
            EdgeType.Squeeze => SqueezeWidth,
            EdgeType.Breach => BreachWidth,
            EdgeType.Hole => HoleWidth,
            EdgeType.InnerWindow => InnerWindowWidth,
            EdgeType.Lift => LiftDoorWidth,
            _ => DoorWidth,
        };

        /// <summary>Clear width of an opening (door, vent): the jambs take half a wall thickness on each side.</summary>
        public static float ClearWidth(float nominal) => nominal - OpeningInset;

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

        /// <summary>How thick a stacked flight and its half landing are (thin: the flight above passes over the one below).</summary>
        public const float StackedThickness = 0.28f;

        /// <summary>
        /// The colliders of a staircase. <paramref name="stacked"/>: a stairwell's flight stacked floor over floor — the same
        /// steps and landing but thin (their undersides follow them up, so the flight of the floor above clears the head of
        /// whoever climbs this one), no rails (the stairwell's walls close both sides; the hole above is only over the
        /// flights) and the divider the full storey high, a wall from floor to floor.
        /// </summary>
        public static List<Box> Boxes(bool stacked)
        {
            if (!stacked) return Boxes();
            const float half = LevelMetrics.FloorHeight / 2f;
            const float rise = half / LevelMetrics.StepsPerFlight;
            const float run = LevelMetrics.StepRun, size = LevelMetrics.StairSize, h = LevelMetrics.FloorHeight;
            const float start = LevelMetrics.FlightStart, landing = LevelMetrics.LandingEnd, t = StackedThickness;
            var list = new List<Box>();
            void Add(string name, float u0, float u1, float v0, float v1, float y0, float y1) =>
                list.Add(new Box(name, new Vector3((u0 + u1) / 2f, (y0 + y1) / 2f, (v0 + v1) / 2f), new Vector3(Mathf.Abs(u1 - u0), y1 - y0, Mathf.Abs(v1 - v0))));
            for (int i = 0; i < LevelMetrics.StepsPerFlight; i++)
                Add("StepA", start - (i + 1) * run, start - i * run, LaneA0, LaneA1, Mathf.Max(0f, (i + 1) * rise - t), (i + 1) * rise);
            Add("Landing", 0.12f, landing, 0.12f, LaneB1, half - t, half);
            for (int j = 0; j < LevelMetrics.StepsPerFlight; j++)
                Add("StepB", landing + j * run, landing + (j + 1) * run, LaneB0, LaneB1, half + (j + 1) * rise - t, half + (j + 1) * rise);
            Add("Exit", start, size + 0.08f, LaneB0, LaneB1, h - 0.25f, h);
            Add("Divider", landing + 2 * run, start, LaneA1, LaneB0, 0f, h);
            return list;
        }

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
        /// The frame maps u → -corner.x and v → -corner.y in world space (flipped: u → -corner.y along z, v → -corner.x
        /// along x — the flights along the other wall); when that is a mirror image the
        /// mirrored variant is used, so the instance itself is only ever rotated (0° or 180°).
        /// </summary>
        public static (bool mirrored, float yaw) Placement(Vector2Int corner, bool flip = false)
        {
            int ux = -corner.x, vz = -corner.y;
            if (flip) // u ↔ v: local z → world x along ux, local x → world z along vz (a turn by ±90°, mirrored when ux·vz > 0)
                return (ux * vz > 0, ux > 0 ? 90f : -90f);
            bool mirrored = ux * vz < 0;
            int xAxis = mirrored ? -ux : ux; // world sign of the variant's local +X
            return (mirrored, xAxis > 0 ? 0f : 180f);
        }

        /// <summary>World position of the corner frame origin for a stair (floor level of its start).</summary>
        public static Vector3 Origin(Stair s) => new(s.Origin.x, LevelLayout.FloorY(s.Floor), s.Origin.y);

        /// <summary>World point of a corner-frame point (u, y, v) for a stair (the same mapping the prefab placement gives).</summary>
        public static Vector3 ToWorld(Stair s, Vector3 local) => s.Flip
            ? Origin(s) + new Vector3(-s.Corner.x * local.z, local.y, -s.Corner.y * local.x)
            : Origin(s) + new Vector3(-s.Corner.x * local.x, local.y, -s.Corner.y * local.z);
    }
}
