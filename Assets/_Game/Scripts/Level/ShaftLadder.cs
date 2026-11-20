using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The ladder between the basement and the lobby (prefab Level/ShaftLadder at its foot; the lid's prefab has one at its
    /// top, unused): a ladder like any other — walk into it facing it (or step into the open shaft from above) and you are
    /// on it, W climbs, S goes down, Jump lets go (<see cref="PlayerController"/>, its ladder mode). Off at the bottom when
    /// you reach the floor, off at the top onto the lobby's floor when the lid is open (<see cref="ShaftHatch"/>; shut, your
    /// head stops against it — E on its handle opens it from the ladder too).
    /// </summary>
    public class ShaftLadder : MonoBehaviour
    {
        [Tooltip("The lobby's end of the ladder (else its foot in the basement): only the foot carries the ladder's data.")]
        public bool Top;

        /// <summary>The house's ladder (its foot), or null.</summary>
        public static ShaftLadder Main { get; private set; }

        /// <summary>Where a climber is held (x, z) and how far up it goes: from the basement's road to the lobby's floor.</summary>
        public Vector3 Column { get; private set; }
        public float Bottom { get; private set; }
        public float TopY { get; private set; }
        /// <summary>Where a climber steps off in the lobby.</summary>
        public Vector3 TopExit { get; private set; }
        /// <summary>The way a climber faces: to the ladder (flat, unit).</summary>
        public Vector3 Facing { get; private set; }
        /// <summary>The shaft's hole in the lobby's floor: its middle (on the floor) and half its side.</summary>
        public Vector3 Shaft { get; private set; }
        public float ShaftHalf { get; private set; } = Basement.ShaftHalf;

        /// <summary>Sets it up from the climb (<see cref="Basement.ClimbPath"/>: foot, the ladder's bottom, its top, the lobby's floor) and the shaft's hole.</summary>
        public void Setup(Vector3[] path, Vector3 shaft)
        {
            if (Top || path == null || path.Length < 4) return;
            Shaft = shaft;
            Column = path[1];
            Bottom = path[1].y;
            TopY = path[2].y;
            TopExit = path[3];
            var face = path[1] - path[0];
            face.y = 0f;
            Facing = face.sqrMagnitude > 1e-4f ? face.normalized : Vector3.forward;
            Main = this;
        }

        void OnDisable()
        {
            if (Main == this) Main = null;
        }

        /// <summary>A body at <paramref name="feet"/> is on the ladder's column (within reach of its rungs).</summary>
        public bool InZone(Vector3 feet) =>
            new Vector2(feet.x - Column.x, feet.z - Column.z).magnitude < LadderMath.Reach && feet.y > Bottom - 0.3f && feet.y < TopY + 0.3f;
    }

    /// <summary>The ladder's rules (pure, tested): when a body gets on, how it moves, when it gets off.</summary>
    public static class LadderMath
    {
        /// <summary>How far from the rungs' column a body takes hold, metres.</summary>
        public const float Reach = 0.5f;
        /// <summary>Climbing speed, metres a second (8 m of shaft in about 3 s).</summary>
        public const float Speed = 2.6f;

        /// <summary>
        /// Gets on: in reach and pushing forward at the ladder (facing it) down in the shaft, or falling down the shaft past it.
        /// Up on the lobby's floor walking past never takes hold (it used to grab and put the walker at the top's exit, again
        /// and again): from above one steps into the open shaft and the ladder catches the fall.
        /// </summary>
        public static bool GetsOn(bool inZone, float forward, Vector3 facing, Vector3 ladderFacing, bool grounded, float y, float top) =>
            inZone && y < top - 0.5f && ((forward > 0.1f && Vector3.Dot(facing, ladderFacing) > 0.3f) || !grounded);

        /// <summary>The climb of one frame: up or down with W/S, drawn to the column.</summary>
        public static Vector3 Step(Vector3 feet, Vector3 column, float forward, float dt)
        {
            var pull = new Vector3(column.x - feet.x, 0f, column.z - feet.z) * Mathf.Min(1f, dt * 8f);
            return pull + Vector3.up * (Mathf.Clamp(forward, -1f, 1f) * Speed * dt);
        }

        /// <summary>W's way on the ladder: up facing it (or side on), down with the back to it (S the other way).</summary>
        public static float Direction(Vector3 look, Vector3 ladderFacing) => Vector3.Dot(look, ladderFacing) < -0.2f ? -1f : 1f;

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
        /// Standing on the top of the ladder: keep standing there, off onto the floor (the body is over the floor beside
        /// the hole: walk on), or let go back into the shaft (Jump, or wandered over the hole away from the rungs: the
        /// ladder catches the fall).
        /// </summary>
        public enum TopState { Standing, OnFloor, LetGo }

        public static TopState AtTop(Vector3 feet, Vector3 shaft, float shaftHalf, Vector3 column, bool jump)
        {
            float off = Mathf.Max(Mathf.Abs(feet.x - shaft.x), Mathf.Abs(feet.z - shaft.z));
            if (off > shaftHalf + 0.05f) return TopState.OnFloor;
            if (jump || new Vector2(feet.x - column.x, feet.z - column.z).magnitude > Reach) return TopState.LetGo;
            return TopState.Standing;
        }

        /// <summary>Off at the bottom: going down and standing on the floor.</summary>
        public static bool OffAtBottom(float forward, bool grounded, float y, float bottom) => forward < -0.1f && grounded && y < bottom + 0.3f;
    }
}
