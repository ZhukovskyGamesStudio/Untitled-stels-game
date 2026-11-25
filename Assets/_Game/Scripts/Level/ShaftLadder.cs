using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A ladder up a shaft into the house (prefab Level/ShaftLadder at the basement's foot, and the passage's end in the
    /// sewer under the exit hatch; the lid's prefab has one at its top, unused): a ladder like any other — walk into it (or step out over the open shaft from above) and you are
    /// on it; W climbs the way you look — looking up, up; looking down, down — and S the other way, Jump lets go
    /// (<see cref="PlayerController"/>, its ladder mode). Off at the bottom when
    /// you reach the floor, off at the top onto the lobby's floor when the lid is open (<see cref="ShaftHatch"/>; shut, your
    /// head stops against it — E on its handle opens it from the ladder too).
    /// </summary>
    public class ShaftLadder : MonoBehaviour
    {
        [Tooltip("The lobby's end of the ladder (else its foot in the basement): only the foot carries the ladder's data.")]
        public bool Top;

        /// <summary>The ladders set up now (their feet): the entry's shaft to the basement, the exit's to the passage in the sewer.</summary>
        public static readonly System.Collections.Generic.List<ShaftLadder> All = new();

        /// <summary>The ladder whose column a body at <paramref name="feet"/> is on (within reach of its rungs), or null.</summary>
        public static ShaftLadder At(Vector3 feet)
        {
            foreach (var l in All)
                if (l != null && l.InZone(feet)) return l;
            return null;
        }

        /// <summary>The ladder whose hole in the floor a body at <paramref name="feet"/> stands at (over it or on its rim), or null.</summary>
        public static ShaftLadder AtHole(Vector3 feet)
        {
            foreach (var l in All)
                if (l != null && LadderMath.AtHole(feet, l.Shaft, l.ShaftHalf)) return l;
            return null;
        }

        /// <summary>The key of the lid over its top (<see cref="OpenKeys"/>): off at the top only through it open.</summary>
        public int LidKey { get; private set; } = OpenKeys.Shaft;

        /// <summary>Where a climber is held (x, z) and how far up it goes: from the basement's road to the lobby's floor.</summary>
        public Vector3 Column { get; private set; }
        public float Bottom { get; private set; }
        public float TopY { get; private set; }
        /// <summary>Where a climber steps off in the lobby.</summary>
        public Vector3 TopExit { get; private set; }
        /// <summary>The way a climber faces: to the ladder (flat, unit).</summary>
        public Vector3 Facing { get; private set; }
        /// <summary>A point on the rungs' line (at the ladder's bottom): <see cref="LadderMath.ColumnToRungs"/> past the column.</summary>
        public Vector3 Rungs => Column + Facing * LadderMath.ColumnToRungs;
        /// <summary>The shaft's hole in the lobby's floor: its middle (on the floor) and half its side.</summary>
        public Vector3 Shaft { get; private set; }
        public float ShaftHalf { get; private set; } = Basement.ShaftHalf;

        /// <summary>Sets it up from the climb (<see cref="Basement.ClimbPath"/>: foot, the ladder's bottom, its top, the lobby's floor) and the shaft's hole.</summary>
        public void Setup(Vector3[] path, Vector3 shaft, int lidKey)
        {
            if (Top || path == null || path.Length < 4) return;
            Shaft = shaft;
            LidKey = lidKey;
            Column = path[1];
            Bottom = path[1].y;
            TopY = path[2].y;
            TopExit = path[3];
            var face = path[1] - path[0];
            face.y = 0f;
            Facing = face.sqrMagnitude > 1e-4f ? face.normalized : Vector3.forward;
            if (!All.Contains(this)) All.Add(this);
        }

        void OnDisable() => All.Remove(this);

        /// <summary>A body at <paramref name="feet"/> is at the ladder: within reach of its rungs, or anywhere in the shaft's well.</summary>
        public bool InZone(Vector3 feet) => LadderMath.InZone(feet, Column, Shaft, ShaftHalf, Bottom, TopY);
    }

    /// <summary>The ladder's rules (pure, tested): when a body gets on, how it moves, when it gets off.</summary>
    public static class LadderMath
    {
        /// <summary>How far from the rungs' column a body takes hold, metres.</summary>
        public const float Reach = 0.5f;
        /// <summary>From the column a climber is held on to the rungs, metres (the basement's and the exit passage's ladders alike).</summary>
        public const float ColumnToRungs = 0.4f;
        /// <summary>Climbing speed, metres a second (8 m of shaft in about 3 s).</summary>
        public const float Speed = 2.6f;

        /// <summary>
        /// At the ladder: within reach of the rungs' column, or anywhere in the shaft's well (1.5 m square: the rungs are an
        /// arm away from every spot in it — a body that fell in by a corner used to miss the 0.5 m round the column and drop
        /// to the bottom), between the ladder's ends.
        /// </summary>
        public static bool InZone(Vector3 feet, Vector3 column, Vector3 shaft, float shaftHalf, float bottom, float top)
        {
            if (feet.y <= bottom - 0.3f || feet.y >= top + 0.3f) return false;
            if (new Vector2(feet.x - column.x, feet.z - column.z).magnitude < Reach) return true;
            return Mathf.Max(Mathf.Abs(feet.x - shaft.x), Mathf.Abs(feet.z - shaft.z)) < shaftHalf;
        }

        /// <summary>A body caught in the air this far over the ladder's foot fell in from above (lower: it jumped at the ladder from the floor), metres.</summary>
        public const float FellFrom = 1.5f;

        /// <summary>
        /// Gets on: at the ladder and walking at it (any keys: <paramref name="wish"/> is the walk asked for, flat) down in
        /// the shaft, or in the air there (fell in, jumped at it). Up on the lobby's floor walking past never takes hold
        /// (it used to grab and put the walker at the top's exit, again and again): from above see <see cref="GetsOnFromAbove"/>.
        /// </summary>
        public static bool GetsOn(bool inZone, Vector3 wish, Vector3 ladderFacing, bool grounded, float y, float top)
        {
            if (!inZone || y >= top - 0.5f) return false;
            if (!grounded) return true;
            wish.y = 0f;
            return wish.sqrMagnitude > 0.01f && Vector3.Dot(wish.normalized, ladderFacing) > 0.3f;
        }

        /// <summary>
        /// W's way on the ladder (1 up, −1 down): where the player looks — up the shaft, W climbs; down it, W goes down
        /// (S always the other way). Looking about level changes nothing: it stays <paramref name="was"/> (on getting on:
        /// up from below, down from above). <paramref name="pitch"/>: the look's pitch, degrees, positive down.
        /// </summary>
        public static float LookWay(float pitch, float was) => pitch < -LevelLook ? 1f : pitch > LevelLook ? -1f : was;

        /// <summary>A look within this of level says nothing of up or down, degrees.</summary>
        public const float LevelLook = 8f;

        /// <summary>How far round the hole's edge a body on the floor still counts as at it (its rim), metres.</summary>
        public const float HoleRim = 0.4f;

        /// <summary>A body at <paramref name="feet"/> stands at the shaft's hole: over it or on its rim, at the floor's height.</summary>
        public static bool AtHole(Vector3 feet, Vector3 shaft, float shaftHalf) =>
            Mathf.Abs(feet.y - shaft.y) < 0.45f && Mathf.Max(Mathf.Abs(feet.x - shaft.x), Mathf.Abs(feet.z - shaft.z)) < shaftHalf + HoleRim;

        /// <summary>
        /// Gets on from above: the feet are out over the open hole itself (the body would fall): the ladder holds it there.
        /// Nothing reaches for a body beside the hole — standing at its edge, walking along it or towards it does nothing
        /// (it used to take hold from the rim and carry the body to the rungs: the shaft sucked passers-by in).
        /// </summary>
        public static bool GetsOnFromAbove(Vector3 feet, Vector3 shaft, float shaftHalf, bool lidOpen)
        {
            if (!lidOpen || Mathf.Abs(feet.y - shaft.y) >= 0.45f) return false;
            return Mathf.Max(Mathf.Abs(feet.x - shaft.x), Mathf.Abs(feet.z - shaft.z)) < shaftHalf - OverHole;
        }

        /// <summary>How far in from the hole's edge the feet are over the hole (a body's middle past the edge by this), metres.</summary>
        public const float OverHole = 0.08f;

        /// <summary>The climb of one frame: up or down with W/S, drawn to the column.</summary>
        public static Vector3 Step(Vector3 feet, Vector3 column, float forward, float dt)
        {
            var pull = new Vector3(column.x - feet.x, 0f, column.z - feet.z) * Mathf.Min(1f, dt * 3f); // (gently: the body is not snatched to the rungs)
            return pull + Vector3.up * (Mathf.Clamp(forward, -1f, 1f) * Speed * dt);
        }


        /// <summary>Off at the top: high enough and the lid open (it steps onto the floor).</summary>
        public static bool OffAtTop(float y, float top, bool lidOpen) => lidOpen && y >= top - 0.35f;

        /// <summary>How high over the lobby's floor the feet rise before stepping off the top, metres (over the rim and the lid's frame).</summary>
        public const float OverRim = 0.25f;
        /// <summary>How fast one steps off the top of the ladder onto the floor, metres a second.</summary>
        public const float StepOffSpeed = 1.8f;

        /// <summary>
        /// Climbing out at the top, one frame: straight up until the feet are <see cref="OverRim"/> over the floor, never
        /// across (it used to carry the climber sideways onto a spot beside the shaft). Zero once up: the climber stands on
        /// the top of the ladder and steps off it himself (<see cref="StepOff"/>).
        /// </summary>
        public static Vector3 ClimbOut(Vector3 feet, float floorY, float dt)
        {
            float rise = floorY + OverRim - feet.y;
            return rise > 0.005f ? Vector3.up * Mathf.Min(rise, Speed * dt) : Vector3.zero;
        }

        /// <summary>Standing on the top of the ladder, one frame: the walk the player asks for (flat, at a step's pace), the height held.</summary>
        public static Vector3 StepOff(Vector3 wish, float dt)
        {
            wish.y = 0f;
            return Vector3.ClampMagnitude(wish, 1f) * (StepOffSpeed * dt);
        }

        /// <summary>
        /// Standing on the top of the ladder: keep standing there (anywhere over the hole: half a metre from the rungs
        /// it used to let go — short of the floor on every side, so a step towards a wall dropped the climber back in), off
        /// onto the floor (the body is over the floor beside the hole: walk on), or let go back into the shaft (Jump: the
        /// ladder catches the fall).
        /// </summary>
        public enum TopState { Standing, OnFloor, LetGo }

        public static TopState AtTop(Vector3 feet, Vector3 shaft, float shaftHalf, bool jump)
        {
            float off = Mathf.Max(Mathf.Abs(feet.x - shaft.x), Mathf.Abs(feet.z - shaft.z));
            if (off > shaftHalf + 0.05f) return TopState.OnFloor;
            return jump ? TopState.LetGo : TopState.Standing;
        }

        /// <summary>Off at the bottom: going down and standing on the floor.</summary>
        public static bool OffAtBottom(float forward, bool grounded, float y, float bottom) => forward < -0.1f && grounded && y < bottom + 0.3f;
    }
}
