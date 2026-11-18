using System.Collections.Generic;
using Stealth.Items;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Where the worker's tray goes: down to the floor (to pick up or to put down), to the bottom board of a storage shelf,
    /// or onto a table, a counter or a shelf to take what lies there (<see cref="Lift"/>: the surface's height comes with it).
    /// </summary>
    public enum SwingKind : byte { Pickup, PutDown, Shelve, Lift }

    /// <summary>
    /// The worker's lift (pure: poses over time, no engine state). Two arms hinged low on the body's sides carry a level
    /// tray: at rest the tray lies over the crate on top; to take or leave something the arms swing forward and down until
    /// the tray lies on the surface in front (the floor, or a shelf's bottom board), and swing back up. There the tray
    /// moves like a forklift's while the robot rolls: picking up, the robot stops <see cref="Scoop"/> short and rolls
    /// forward so the tray slides under the item; putting down, it rolls back and the tray slides out from under it;
    /// shelving, the tray swings down in front of the shelf, the robot rolls in, leaves the item and rolls out again.
    /// Half way (<see cref="TouchAt"/>) the server swaps the world item and the cargo. Robot-local space. The geometry is
    /// the model's (Tools/Blender/worker_lift.py): keep them equal.
    /// </summary>
    public static class CargoMotion
    {
        /// <summary>The arms' hinge on the body.</summary>
        public static readonly Vector3 Hinge = new(0f, 0.562f, 0.15f);
        /// <summary>From the hinge to the pin that holds the tray.</summary>
        public const float ArmLength = 0.843f;
        /// <summary>The pin (the tray's mid-thickness, its pivot) at rest, over the crate.</summary>
        public static readonly Vector3 PinRest = new(0f, 0.562f + ArmLength, 0.15f);
        /// <summary>From the pin down to the tray's bottom (it is 3 cm thick).</summary>
        public const float TrayHalf = 0.015f;
        /// <summary>The tray lies this high over the surface it sets down on (it never scrapes).</summary>
        public const float Clearance = 0.01f;
        /// <summary>The pin's height when the tray lies on the floor.</summary>
        public const float PinOnFloor = Clearance + TrayHalf;
        /// <summary>Loot on the tray: its pivot over the pin (half the tray plus the grip ribs).</summary>
        public const float OnTray = 0.023f;
        /// <summary>The loot's pivot height on the floor (items spawn 2 cm up).</summary>
        public const float FloorHeight = 0.02f;
        /// <summary>The bottom board of a storage shelf (the furnisher's lowest shelf level), where a worker shelves loot.</summary>
        public const float ShelfTop = 0.47f;
        /// <summary>Loot on a shelf lies this far in from the shelf's front (half its 0.6 m depth).</summary>
        public const float ShelfDepth = 0.3f;
        /// <summary>How far the robot rolls while the tray slides under the item (or out from under it), metres.</summary>
        public const float Scoop = 0.3f;
        /// <summary>
        /// Shelving: how far it rolls in and out. The tray's front edge (0.2 m ahead of the pin, at most 1.2 m ahead of
        /// the robot mid-swing) must clear the shelf's front before it rolls in.
        /// </summary>
        public const float ShelfScoop = 0.55f;
        /// <summary>Shelving takes this much longer than the floor (it rolls twice as far).</summary>
        public const float ShelfTime = 1.5f;

        /// <summary>The tray has swung down by this fraction of the motion and the robot starts to roll…</summary>
        public const float DownAt = 0.32f;
        /// <summary>…half way through the roll the loot goes on or off the tray (the server swaps item and cargo)…</summary>
        public const float TouchAt = (DownAt + UpFrom) / 2f;
        /// <summary>…and it stops rolling and swings back up here.</summary>
        public const float UpFrom = 0.62f;
        /// <summary>The body leans over the tray on the floor, degrees.</summary>
        public const float BowAngle = 5f;

        public struct Pose
        {
            /// <summary>The arms' swing from rest, degrees (forward about the robot's X).</summary>
            public float Arm;
            /// <summary>Where the tray's pin is (the tray stays level).</summary>
            public Vector3 Tray;
            /// <summary>How far the body leans, 0..1 (× <see cref="BowAngle"/>).</summary>
            public float Bow;
            /// <summary>The body sinks a little, metres.</summary>
            public float Dip;
        }

        /// <summary>The surface the tray lands on: the floor, or the shelf's bottom board (a lift: its own, see <see cref="At(float, SwingKind, float)"/>).</summary>
        public static float Surface(SwingKind k) => k == SwingKind.Shelve ? ShelfTop : 0f;
        /// <summary>The highest surface a worker takes loot from (its arms reach over it, level, up to here).</summary>
        public const float LiftMax = 1.3f;
        /// <summary>A lift rolls in and out like shelving.</summary>
        static bool InAndOut(SwingKind k) => k is SwingKind.Shelve or SwingKind.Lift;

        /// <summary>The arms' swing that lands the tray on a surface this high, degrees.</summary>
        public static float AngleFor(float surface) => Mathf.Acos((surface + Clearance + TrayHalf - Hinge.y) / ArmLength) * Mathf.Rad2Deg;

        /// <summary>How far ahead of the robot's centre the pin is when the tray lies on a surface this high.</summary>
        public static float ReachFor(float surface) => Hinge.z + Mathf.Sin(AngleFor(surface) * Mathf.Deg2Rad) * ArmLength;

        /// <summary>To the floor, degrees.</summary>
        public static float ArmDown => AngleFor(0f);
        /// <summary>Where loot lies when the worker takes it from the floor or sets it down there: this far in front of its centre.</summary>
        public static float Reach => ReachFor(0f);

        public static float ScoopOf(SwingKind k) => InAndOut(k) ? ShelfScoop : Scoop;
        public static float TimeOf(SwingKind k, float handleTime) => handleTime * (InAndOut(k) ? ShelfTime : 1f);

        /// <summary>Where a loot it sets down on the floor ends up, from where the robot stands when it begins: the tray's spot.</summary>
        public static Vector3 FloorSpot => new(0f, FloorHeight, Reach);

        /// <summary>
        /// To begin, it stops with the item (or the place for it) this far in front: the tray lands just short of it and the
        /// robot rolls in. Putting down on the floor it lands right on the spot and rolls back.
        /// </summary>
        public static float StandOff(SwingKind k) => StandOff(k, Surface(k));

        /// <summary>The stand-off for a surface this high (a lift: the tray lands short of the item and rolls in under it).</summary>
        public static float StandOff(SwingKind k, float surface) => k == SwingKind.PutDown ? Reach : ReachFor(surface) + ScoopOf(k);

        /// <summary>For picking up from the floor (what the errands use most).</summary>
        public static float StandOff() => StandOff(SwingKind.Pickup);

        /// <summary>The pin for an arm swung <paramref name="degrees"/> from rest.</summary>
        public static Vector3 PinAt(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return Hinge + new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a)) * ArmLength;
        }

        /// <summary>The pose at <paramref name="t"/> (0..1) of one down-and-up swing to the floor.</summary>
        public static Pose At(float t) => At(t, SwingKind.Pickup);

        /// <summary>The pose at <paramref name="t"/> (0..1) of one down-and-up swing of this kind.</summary>
        public static Pose At(float t, SwingKind k) => At(t, k, Surface(k));

        /// <summary>The pose at <paramref name="t"/> (0..1) of one swing of this kind onto a surface this high.</summary>
        public static Pose At(float t, SwingKind k, float surface)
        {
            t = Mathf.Clamp01(t);
            float down = t < UpFrom ? Smooth(t / DownAt) : 1f - Smooth((t - UpFrom) / (1f - UpFrom));
            // the tray bumps the surface and settles: the body takes the knock
            float onFloor = t >= DownAt && t <= UpFrom ? Mathf.Sin((t - DownAt) / (UpFrom - DownAt) * Mathf.PI) : 0f;
            float arm = down * AngleFor(surface);
            return new Pose
            {
                Arm = arm,
                Tray = PinAt(arm),
                Bow = down * down,
                Dip = onFloor * 0.02f,
            };
        }

        /// <summary>How much of the roll is done at <paramref name="t"/>, 0..1 (one way).</summary>
        public static float Slide(float t) => Smooth((t - DownAt) / (UpFrom - DownAt));

        /// <summary>
        /// How far the robot has rolled forward (negative: back) from where it began, at <paramref name="t"/>. Shelving: in
        /// during the first half of the roll, out during the second.
        /// </summary>
        public static float Rolled(float t, SwingKind k) => k switch
        {
            SwingKind.Pickup => Scoop * Slide(t),
            SwingKind.PutDown => -Scoop * Slide(t),
            _ => ShelfScoop * (Smooth((t - DownAt) / (TouchAt - DownAt)) - Smooth((t - TouchAt) / (UpFrom - TouchAt))),
        };

        /// <summary>
        /// The loot keeps still on the floor while the tray slides under it or out from under it: on the tray it sits this
        /// far ahead of the pin (0 once it is loaded, at rest, and while shelving: it rides in on the tray).
        /// </summary>
        public static float CargoAhead(float t, SwingKind k) => k switch
        {
            SwingKind.Pickup => Scoop * (1f - Slide(t)),
            SwingKind.PutDown => Scoop * Slide(t),
            _ => 0f,
        };

        /// <summary>The swing crossed the moment the tray touches the loot (from <paramref name="before"/> to <paramref name="now"/>).</summary>
        public static bool Touched(float before, float now) => before < TouchAt && now >= TouchAt;

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }

    /// <summary>What a worker may pick up and where it stands to do it (pure).</summary>
    public static class LootErrands
    {
        /// <summary>Loot lying on the floor counts if its pivot is this close to the robot's floor (higher: on a desk or a shelf).</summary>
        public const float FloorTolerance = 0.25f;

        /// <summary>The surface an item lies on, from its pivot's height over the robot's floor: the floor, or a table's top.</summary>
        public static float SurfaceOf(float height) => height <= FloorTolerance ? 0f : Mathf.Max(0f, height - 0.01f);

        /// <summary>How it takes an item lying this high: scooped from the floor, or lifted off a surface it can reach over.</summary>
        public static SwingKind TakeKind(float height) => height <= FloorTolerance ? SwingKind.Pickup : SwingKind.Lift;

        /// <summary>Workers move loot, never the team's tools (keys, stunners, speakers, the cage key).</summary>
        public static bool CanTake(int shape) => ItemCatalog.IsLoot(shape) && !ItemCatalog.IsTool(shape);

        /// <summary>The nearest candidate on the robot's floor within <paramref name="radius"/> (on it, or on a surface it reaches), or -1.</summary>
        public static int Nearest(Vector3 robot, IReadOnlyList<Vector3> items, float radius)
        {
            int best = -1;
            float bestD = radius * radius;
            for (int i = 0; i < items.Count; i++)
            {
                var d = items[i] - robot;
                if (d.y < -FloorTolerance || d.y > CargoMotion.LiftMax + 0.02f) continue;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq <= bestD) { bestD = sq; best = i; }
            }
            return best;
        }

        /// <summary>Where to stop so the item lies the pick-up stand-off in front of the robot, coming from <paramref name="robot"/>.</summary>
        public static Vector3 Approach(Vector3 robot, Vector3 item)
        {
            var d = item - robot;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) d = Vector3.forward;
            return new Vector3(item.x, robot.y, item.z) - d.normalized * CargoMotion.StandOff();
        }
    }
}
