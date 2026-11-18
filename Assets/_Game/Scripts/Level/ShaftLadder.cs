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

        /// <summary>Sets it up from the climb (<see cref="Basement.ClimbPath"/>: foot, the ladder's bottom, its top, the lobby's floor).</summary>
        public void Setup(Vector3[] path)
        {
            if (Top || path == null || path.Length < 4) return;
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
        /// <summary>Climbing speed, metres a second.</summary>
        public const float Speed = 1.9f;

        /// <summary>Gets on: in reach and pushing forward at the ladder (facing it), or falling down the shaft past it.</summary>
        public static bool GetsOn(bool inZone, float forward, Vector3 facing, Vector3 ladderFacing, bool grounded, float y, float top) =>
            inZone && ((forward > 0.1f && Vector3.Dot(facing, ladderFacing) > 0.3f) || (!grounded && y < top - 0.5f));

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

        /// <summary>Off at the bottom: going down and standing on the floor.</summary>
        public static bool OffAtBottom(float forward, bool grounded, float y, float bottom) => forward < -0.1f && grounded && y < bottom + 0.3f;
    }
}
