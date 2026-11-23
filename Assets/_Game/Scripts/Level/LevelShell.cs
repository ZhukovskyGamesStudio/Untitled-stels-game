using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>One face of a wall piece that a room can see (exterior walls only on their inner side).</summary>
    public struct WallFace
    {
        /// <summary>Ends of the piece on the wall's centre line (at floor level of its floor).</summary>
        public Vector3 A, B;
        /// <summary>Height range above the floor: 0..WallHeight for a wall, DoorHeight..WallHeight for a lintel.</summary>
        public float Y0, Y1;
        /// <summary>Horizontal unit vector out of the face into the room.</summary>
        public Vector3 Normal;
        public int Floor;
        public bool Exterior;
        /// <summary>The end meets a corner post (decoration reaches into the room's corner), not an opening.</summary>
        public bool CornerA, CornerB;
        /// <summary>
        /// The end meets the next piece of a bent wall (a chamfer, an arc) at a shallow angle: decoration runs exactly to the
        /// joint, so the pieces of both meet instead of crossing.
        /// </summary>
        public bool JointA, JointB;
        /// <summary>
        /// The corner post at this end stands in the middle of a straight wall (another wall runs on behind it, a third
        /// meets them from the other side): this face and the next one share the post's face — half of it each.
        /// </summary>
        public bool StraightA, StraightB;

        public Vector3 Along => (B - A).normalized;
        public float Length => (B - A).magnitude;
        /// <summary>A point on the face plane at the floor, at distance <paramref name="u"/> from <see cref="A"/>.</summary>
        public Vector3 At(float u) => A + Along * u + Normal * (LevelMetrics.WallThickness / 2f);
    }

    /// <summary>The walkable top of a floor slab (stair holes left out).</summary>
    public struct FloorRect
    {
        public Vector2 Min, Max; // x, z
        public float Y;
        public int Floor;
    }

    /// <summary>A doorway or a vent in a wall.</summary>
    public struct WallOpening
    {
        /// <summary>Bottom centre of the opening on the wall's centre line.</summary>
        public Vector3 Center;
        /// <summary>Unit vector along the wall.</summary>
        public Vector3 Along;
        public float Width, Height;
        public bool Vent, Exterior;
        /// <summary>A narrow slot with its own frame on both faces (Level/SqueezeFrame): no door trim of the style.</summary>
        public bool Squeeze;
        /// <summary>The vault's round door or the lift's doorway: their own steel ring / portal, no trim, no reveals of the style.</summary>
        public bool Vault;
        public int Floor;
    }

    /// <summary>
    /// The surfaces of the built house, recorded by <see cref="LevelBuilder"/>: what a visual style can dress
    /// (trims, panels, finishes, ceiling beams) without touching the geometry the game plays on.
    /// </summary>
    public sealed class LevelShell
    {
        public readonly List<WallFace> Walls = new();
        public readonly List<FloorRect> Floors = new();
        /// <summary>Holes in the ceilings (the duct grates): no ceiling finish there.</summary>
        public readonly List<Rect> CeilingHoles = new();
        /// <summary>Holes knocked in a floor (the heavy hammer, <see cref="FloorHoles"/>): x/z in metres and the floor whose slab it is.</summary>
        public readonly List<(Rect hole, int floor)> FloorHoles = new();
        public readonly List<WallOpening> Openings = new();
        /// <summary>Renderers that are the wall itself (<see cref="AutoDoor.WallParts"/>): <see cref="StyleDecor"/> dresses them in the wall finish.</summary>
        public readonly List<MeshRenderer> WallParts = new();
        /// <summary>Window centres and yaws (see <see cref="LevelBuilder.WindowSpots"/>).</summary>
        public readonly List<(Vector3 pos, float yaw)> Windows = new();
        /// <summary>Furniture footprints for the map (x/z in metres, from the placed models).</summary>
        public readonly List<MapProp> Props = new();
    }

    /// <summary>The footprint of a piece of furniture on the map.</summary>
    public struct MapProp
    {
        public int Floor;
        public PropKind Kind;
        /// <summary>x = world x, y = world z.</summary>
        public Rect Area;
    }
}
