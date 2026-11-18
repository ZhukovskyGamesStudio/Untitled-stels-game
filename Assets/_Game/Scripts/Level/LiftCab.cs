using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The apartment block's lift cab (prefab Level/LiftCab, one per house that has a lift): its root stands in the well on
    /// the ground floor, +Z towards the landings; <see cref="Car"/> (floor, walls, ceiling, lamp, the floor buttons) rides
    /// up and down to where <see cref="LiftRules.CabFloor"/> has it from the replicated state — every peer the same.
    /// A player standing in it is carried with it (<see cref="CarryAt"/>, used by the owner's motor). Robots never ride:
    /// the cab is left out of the navmesh and the landing doorways keep them out.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class LiftCab : MonoBehaviour
    {
        [Tooltip("What rides: the cab's floor, walls, ceiling, lamp and buttons.")]
        public Transform Car;
        [Tooltip("The floor buttons on the cab's panel, bottom up; those over the house's floors are hidden.")]
        public LiftButton[] Buttons = new LiftButton[0];
        [Tooltip("The cab's inside, half extents in its own frame (x across, z from the back to the doors).")]
        public Vector2 HalfSize = new(1.3f, 1.05f);

        /// <summary>The cab of this house, or null (no lift).</summary>
        public static LiftCab Current { get; private set; }
        /// <summary>The lift's state and the server's clock (set by the game; without it: parked on the ground floor).</summary>
        public static System.Func<LiftState> State = () => LiftState.ParkedAt(0);
        public static System.Func<double> Now = () => Time.timeAsDouble;

        static float FloorTime => Configs.Tuning.Level.Building.LiftFloorTime;
        static float DoorTime => Configs.Tuning.Level.Building.LiftDoorTime;

        public int Floors { get; private set; } = 1;

        /// <summary>Sets it up for a house of <paramref name="floors"/> storeys: a button for each, bottom up.</summary>
        public void Configure(int floors)
        {
            Floors = Mathf.Max(1, floors);
            for (int i = 0; i < Buttons.Length; i++)
            {
                if (Buttons[i] == null) continue;
                Buttons[i].gameObject.SetActive(i < Floors);
                if (i < Floors) Buttons[i].Configure(i);
            }
        }

        /// <summary>The cab's floor height above the ground floor now, metres.</summary>
        public float CabHeight => LiftRules.CabFloor(State(), Now(), FloorTime, DoorTime) * LevelMetrics.FloorHeight;

        /// <summary>A point inside the cab (on its floor, under its ceiling).</summary>
        public bool Holds(Vector3 p)
        {
            var local = transform.InverseTransformPoint(p);
            float y = local.y - CabHeight;
            return Mathf.Abs(local.x) < HalfSize.x && Mathf.Abs(local.z) < HalfSize.y + 0.1f && y > -0.4f && y < 2.2f;
        }

        /// <summary>How far the cab carries a rider standing at <paramref name="p"/> over the last <paramref name="dt"/> seconds (0: not in it).</summary>
        public static float CarryAt(Vector3 p, float dt)
        {
            if (Current == null || !Current.Holds(p)) return 0f;
            double now = Now();
            return LiftRules.Carry(State(), now - dt, now, FloorTime, DoorTime);
        }

        void OnEnable() => Current = this;
        void OnDisable() { if (Current == this) Current = null; }

        void Update()
        {
            if (Car != null) Car.localPosition = new Vector3(0f, CabHeight, 0f);
        }
    }
}
