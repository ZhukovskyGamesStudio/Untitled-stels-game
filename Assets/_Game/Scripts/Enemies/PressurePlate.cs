using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Pressure plate in a doorway: stepping on it raises an alarm. A steel slab with a glowing border — jump over it or hack it.</summary>
    public class PressurePlate : SecurityDevice
    {
        [Tooltip("Tuning (PressurePlateProfile) — a separate asset next to the prefab.")]
        public PressurePlateProfile Profile;
        protected override DeviceProfile Device => Profile;

        protected override Color IdleColor => new(0.35f, 0.5f, 0.55f);

        /// <summary>Feet on the plate (horizontal overlap with the player's capsule, standing on its surface).</summary>
        public static bool Steps(Vector3 plate, float halfSize, Vector3 feet, float radius = 0.3f) =>
            Mathf.Abs(feet.x - plate.x) <= halfSize + radius * 0.5f &&
            Mathf.Abs(feet.z - plate.z) <= halfSize + radius * 0.5f &&
            feet.y - plate.y < 0.25f && feet.y - plate.y > -0.3f;

        /// <summary>A point on the floor over the plate (the crosshair landing on it).</summary>
        public bool Covers(Vector3 point) =>
            Mathf.Abs(point.x - transform.position.x) <= Profile.HalfSize && Mathf.Abs(point.z - transform.position.z) <= Profile.HalfSize &&
            Mathf.Abs(point.y - transform.position.y) < 0.3f;

        protected override void TickServer()
        {
            foreach (var p in PlayerController.All)
                if (!p.IsDown && Steps(transform.position, Profile.HalfSize, p.Position)) { Trip(p); return; }
            Forget();
        }
    }
}
