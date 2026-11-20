using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The hatches of the house (rules and places, pure): the way in is the basement the team arrives in (its stash rides in
    /// the storage robot), the way out the exit hatch's shaft down to the passage in the sewer — its lid is a
    /// <see cref="ShaftHatch"/> like the entry's; its stash goes down with the team in the car.
    /// </summary>
    public static class Hatch
    {
        public const float UseDistance = 1.6f; // standing at its rim, not across the room

        public static readonly Color EntryColor = new(0.3f, 0.6f, 1f), ExitColor = new(0.3f, 1f, 0.4f);

        /// <summary>Feet this far above or below the hatch are on another floor (a floor is 3.5 m).</summary>
        public const float SameFloor = 1.5f;

        public static bool IsNear(Vector3 position) => IndexNear(position) >= 0;

        /// <summary>1 near the exit hatch, −1 elsewhere (the way in is the basement: no entry hatch to sit in; index 0 is kept for it).</summary>
        public static int IndexNear(Vector3 position)
        {
            var layout = ActiveLevel.Layout;
            if (layout == null) return -1;
            if (IsNear(position, layout.ExitHatchPosition)) return 1;
            return -1;
        }

        public static Vector3 PositionOf(int hatch)
        {
            var layout = ActiveLevel.Layout;
            return layout == null ? Vector3.zero : hatch == 1 ? layout.ExitHatchPosition : Entry(layout);
        }

        /// <summary>The way in: where the team's stash is kept down in the basement.</summary>
        public static Vector3 Entry(LevelLayout layout) => layout.EntryHatch;

        /// <summary>Close to the hatch and on its floor — not right above it on the floor upstairs (pure).</summary>
        public static bool IsNear(Vector3 position, Vector3 hatch)
        {
            var d = position - hatch;
            if (Mathf.Abs(d.y) > SameFloor) return false;
            d.y = 0;
            return d.magnitude <= UseDistance;
        }
    }
}
