using UnityEngine;

namespace Stealth.Level
{
    /// <summary>The basement's frame in the world: its origin and its turn (a multiple of 90°) about the vertical.</summary>
    public readonly struct BasementFrame
    {
        public readonly Vector3 Origin;
        public readonly float Yaw;

        public BasementFrame(Vector3 origin, float yaw)
        {
            Origin = origin;
            Yaw = yaw;
        }

        public Quaternion Rotation => Quaternion.Euler(0f, Yaw, 0f);
        public Vector3 ToWorld(Vector3 local) => Origin + Rotation * local;
        public Vector3 ToLocal(Vector3 world) => Quaternion.Inverse(Rotation) * (world - Origin);
        /// <summary>A direction of the frame in the world.</summary>
        public Vector3 Dir(Vector3 local) => Rotation * local;
    }

    /// <summary>
    /// The basement the team arrives in (pure geometry): the stretch of the sewer where the team's car stopped. A tunnel of
    /// the drivable kit (Tools/Blender/sewer.py) runs along the frame's +X: behind the car the way it came by — seen for a
    /// few segments, then a cap, but an invisible wall stands across it at x = 0 — then the car's stretch
    /// (<see cref="CarSegments"/> segments; the car stops at <see cref="CarStop"/>, its nose before the gate), the gate at
    /// <see cref="GateX"/> (shut: the team opens it from the far side after the house) and a segment beyond it. Beside
    /// the car, in the left (+Z) wall, a small alcove (the kit's sewer_seg_alcove, the last of the car's segments): a
    /// ladder against its back wall climbs a brick shaft straight up to the lid in the house's floor
    /// (<see cref="ShaftHatch"/>). No hatch: the team arrives sitting in the car, the stash rides in the storage robot
    /// down here, and being down here counts as being back at the way in (the leave rule).
    /// Local frame: x along the tunnel from the invisible wall, z across it (+Z: the alcove's side), y up from the road.
    /// </summary>
    public static class Basement
    {
        /// <summary>The road under the lobby's floor: the tunnel's vault (top 4.55 m) stays under the ground.</summary>
        public const float Depth = 4.9f;
        /// <summary>The road (over the water) and the walkways along the walls.</summary>
        public const float Road = 0.05f, Walk = 0.15f;
        /// <summary>The car's stretch from the invisible wall to the gate, the seen way back behind the wall, the stretch beyond the gate.</summary>
        public const int CarSegments = 3, BackSegments = 4, BeyondSegments = 1;
        public const float GateX = CarSegments * SewerPlan.SegmentLength;
        /// <summary>The alcove (the last car segment's, the kit's ALC_*): its middle along the tunnel, its span, its back wall.</summary>
        public const float AlcoveX = GateX - SewerPlan.SegmentLength + 3f, AlcoveHalf = 1.2f, AlcoveFront = 3.6f, AlcoveBack = 5.09f;
        /// <summary>The shaft over the alcove: half its width (its hole in the floor is 3 × 3 plan cells) and its middle across.</summary>
        public const float ShaftHalf = 0.75f, ShaftZ = 4.345f;
        /// <summary>The ladder's rungs (0.25 m off the back wall) and the column a climber is held on in front of them.</summary>
        public const float LadderZ = AlcoveBack - 0.25f, ClimbZ = LadderZ - 0.4f;
        /// <summary>The walkways' middle across the tunnel.</summary>
        public const float WalkwayZ = SewerPlan.WalkwayZ;

        /// <summary>Where the car stops (local: the car's pivot on the road, facing +X): its nose a little before the gate.</summary>
        public static readonly Vector3 CarStop = new(GateX - 4.2f, Road, -0.4f);
        /// <summary>The car rolls in and stops: this long (s), from this speed (m/s), braking evenly.</summary>
        public const float RollTime = 3.5f, RollSpeed = 6.3f;
        public const float RollDistance = RollSpeed * RollTime / 2f;

        /// <summary>The shaft's middle in the frame (at the lobby's floor).</summary>
        public static readonly Vector3 Shaft = new(AlcoveX, Depth, ShaftZ);

        /// <summary>The frame whose shaft comes up at <paramref name="shaft"/> (the lobby's floor), turned by <paramref name="yaw"/>.</summary>
        public static BasementFrame Frame(Vector3 shaft, float yaw) => new(shaft - Quaternion.Euler(0f, yaw, 0f) * Shaft, yaw);

        /// <summary>The way the tunnel runs (one of four) so that it stays under the house as much as it can.</summary>
        public static float YawUnder(Vector3 shaft, Rect house)
        {
            float best = 0f, most = -1f;
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
            {
                var f = Frame(shaft, yaw);
                var a = f.ToWorld(new Vector3(-BackSegments * SewerPlan.SegmentLength, 0f, -4f));
                var b = f.ToWorld(new Vector3(GateX + BeyondSegments * SewerPlan.SegmentLength, 0f, 4f));
                var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));
                float w = Mathf.Min(r.xMax, house.xMax) - Mathf.Max(r.xMin, house.xMin), h = Mathf.Min(r.yMax, house.yMax) - Mathf.Max(r.yMin, house.yMin);
                float under = w > 0f && h > 0f ? w * h : 0f;
                if (under > most + 0.01f) { most = under; best = yaw; }
            }
            return best;
        }

        /// <summary>Where the storage robot waits with the stash: on the right walkway, clear of the car's way in.</summary>
        public static Vector3 StashSpot(BasementFrame f) => f.ToWorld(new Vector3(CarStop.x - 2.5f, Walk, -WalkwayZ));

        /// <summary>Where a player stands at the ladder's foot: on the left walkway in front of the alcove.</summary>
        public static Vector3 LadderFoot(BasementFrame f) => f.ToWorld(new Vector3(AlcoveX, Walk + 0.05f, WalkwayZ));

        /// <summary>Where a player steps off the ladder in the lobby: beside the shaft on its open side (<paramref name="exit"/>, flat, unit).</summary>
        public static Vector3 TopExit(Vector3 shaft, Vector3 exit) => shaft + exit * (ShaftHalf + 0.55f) + Vector3.up * 0.05f;

        /// <summary>
        /// The way up the ladder (reversed: down): from the walkway into the alcove, straight up the shaft in front of the
        /// rungs, and off beside the shaft onto the lobby's floor.
        /// </summary>
        public static Vector3[] ClimbPath(BasementFrame f, Vector3 shaft, Vector3 exit)
        {
            var foot = LadderFoot(f);
            var column = f.ToWorld(new Vector3(AlcoveX, Walk + 0.05f, ClimbZ));
            var top = new Vector3(column.x, shaft.y + 0.1f, column.z);
            return new[] { foot, column, top, TopExit(shaft, exit) };
        }

        /// <summary>The car's pose when it has stopped (world).</summary>
        public static Pose CarPose(BasementFrame f) => new(f.ToWorld(CarStop), f.Rotation * Quaternion.Euler(0f, 90f, 0f));

        /// <summary>How far behind its stop the rolling car is <paramref name="left"/> seconds before it stops, and its speed then.</summary>
        public static float RollBehind(float left, out float speed)
        {
            float t = Mathf.Clamp(left, 0f, RollTime);
            speed = RollSpeed * t / RollTime;
            return RollSpeed * t * t / (2f * RollTime);
        }

        /// <summary>The rolling car's pose <paramref name="left"/> seconds before it stops (before the roll: at its start).</summary>
        public static Pose RollPose(BasementFrame f, float left, out float speed)
        {
            float behind = RollBehind(left, out speed);
            var stop = CarPose(f);
            return new Pose(stop.position - f.Dir(Vector3.right) * behind, stop.rotation);
        }

        /// <summary>A point in the basement (its tunnel up to the gate, the alcove, the shaft under the floor): it counts as inside the house (no gas).</summary>
        public static bool Contains(BasementFrame f, Vector3 p)
        {
            var d = f.ToLocal(p);
            if (d.y < -1.5f || d.y > Depth - 0.25f || d.x < -0.5f || d.x > GateX + 0.5f) return false;
            if (Mathf.Abs(d.z) < 4.2f) return true;
            return d.z > 0f && d.z < AlcoveBack + 0.3f && Mathf.Abs(d.x - AlcoveX) < AlcoveHalf + 0.3f;
        }

        /// <summary>Four places in the tunnel for a player who is not in the car when a house starts: on the walkways behind it.</summary>
        public static Vector3[] Spawns(BasementFrame f)
        {
            float x = CarStop.x - 4f;
            return new[]
            {
                f.ToWorld(new Vector3(x, Walk + 0.05f, WalkwayZ)), f.ToWorld(new Vector3(x, Walk + 0.05f, -WalkwayZ)),
                f.ToWorld(new Vector3(x - 1.4f, Walk + 0.05f, WalkwayZ)), f.ToWorld(new Vector3(x - 1.4f, Walk + 0.05f, -WalkwayZ)),
            };
        }
    }
}
