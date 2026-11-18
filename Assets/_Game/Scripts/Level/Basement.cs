using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The basement the team arrives in (pure geometry): a short stretch of the sewer under the lobby — two tunnel segments
    /// (Tools/Blender/sewer.py, the drivable kit) closed by an end cap at the far end, lit by the sewer's lamps — and at
    /// its near end a brick wall with a doorway into the shaft chamber: a ladder against the chamber's back wall climbs
    /// straight up the shaft to the lobby's floor, where a lid closes it (<see cref="ShaftHatch"/>). No hatch: the team
    /// stands in the basement when a house starts, the stash is kept there, and being down here counts as being back at
    /// the way in (the leave rule). Local frame: x along the tunnel from its near end, z across it, y up from its road.
    /// </summary>
    public static class Basement
    {
        /// <summary>The road of the basement under the lobby's floor: the vault (top 4.55 m) stays under the ground.</summary>
        public const float Depth = 4.9f;
        public const int Segments = 2;
        public const float Length = Segments * SewerPlan.SegmentLength;
        /// <summary>The shaft chamber behind the near end wall: the shaft's middle (x), its inner half width, the wall's thickness.</summary>
        public const float ShaftX = -1.1f, ShaftHalf = 0.6f, ChamberHalf = 0.7f, EndWall = 0.4f, ChamberBack = -1.8f;
        /// <summary>The doorway from the tunnel into the chamber.</summary>
        public const float DoorHalf = 0.7f, DoorHeight = 2.4f;
        /// <summary>The ladder against the chamber's back wall, rails 0.25 m deep; the climber goes up 0.35 m in front of them.</summary>
        public const float LadderX = ChamberBack, ClimbX = ChamberBack + 0.65f; // (the climber's capsule, r 0.35, clear of the rails)
        /// <summary>The road's height in the tunnel (over the water), where the team stands.</summary>
        public const float Road = 0.05f;

        /// <summary>Where the basement's frame starts, for a shaft at <paramref name="shaft"/> (the lobby's middle).</summary>
        public static Vector3 Origin(Vector3 shaft) => new(shaft.x - ShaftX, shaft.y - Depth, shaft.z);

        /// <summary>Where the team's stash is kept (its board hangs over it): the middle of the tunnel.</summary>
        public static Vector3 StashSpot(Vector3 origin) => origin + new Vector3(Length * 0.55f, Road, 0f);

        /// <summary>Where a player stands at the ladder's foot: in the doorway, in front of the chamber.</summary>
        public static Vector3 LadderFoot(Vector3 origin) => origin + new Vector3(0.6f, Road + 0.05f, 0f);

        /// <summary>Where a player steps off the ladder in the lobby: beside the shaft, away from the hinge of its lid.</summary>
        public static Vector3 TopExit(Vector3 shaft) => new(shaft.x, shaft.y + 0.05f, shaft.z + ShaftHalf + 0.55f);

        /// <summary>
        /// The way up the ladder (reversed: down): from the doorway into the chamber, straight up the shaft in front of the
        /// ladder, and off beside the shaft onto the lobby's floor.
        /// </summary>
        public static Vector3[] ClimbPath(Vector3 origin, Vector3 shaft)
        {
            var foot = LadderFoot(origin);
            var column = origin + new Vector3(ClimbX, foot.y - origin.y, 0f);
            var top = new Vector3(column.x, shaft.y + 0.1f, column.z);
            return new[] { foot, column, top, TopExit(shaft) };
        }

        /// <summary>A point in the basement (its tunnel or the chamber, under the ground): it counts as inside the house (no gas).</summary>
        public static bool Contains(Vector3 origin, Vector3 p)
        {
            var d = p - origin;
            return d.x > ChamberBack - 0.5f && d.x < Length + 1f && Mathf.Abs(d.z) < 4.2f && d.y > -1.5f && d.y < Depth - 0.25f;
        }

        /// <summary>Four places in the tunnel where the team stands when a house starts, facing the ladder.</summary>
        public static Vector3[] Spawns(Vector3 origin)
        {
            var c = origin + new Vector3(4.2f, Road + 0.05f, 0f);
            return new[] { c + new Vector3(0f, 0f, 0.8f), c + new Vector3(0f, 0f, -0.8f), c + new Vector3(1.2f, 0f, 0.8f), c + new Vector3(1.2f, 0f, -0.8f) };
        }
    }
}
