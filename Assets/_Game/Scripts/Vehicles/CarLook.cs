using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>
    /// Which side the pickup's steering wheel is on. The model (<c>Tools/Blender/car.py</c>) is built right-hand drive;
    /// a left-hand car (<see cref="CarSettings.LeftHandDrive"/>) is the same model mirrored left to right — the car in the
    /// sewer and the one in the drive scene alike.
    /// </summary>
    public static class CarLook
    {
        /// <summary>The model's own driver side: the steering wheel sits at +X.</summary>
        public const bool ModelIsLeftHand = false;

        /// <summary>Whether a model built as <see cref="ModelIsLeftHand"/> must be mirrored for these settings.</summary>
        public static bool NeedsMirror(CarSettings s) => (s ?? new CarSettings()).LeftHandDrive != ModelIsLeftHand;

        /// <summary>
        /// Mirror everything under <paramref name="root"/> across its YZ plane, without touching <paramref name="root"/> itself:
        /// each direct child gets the mirrored pose and a flipped X scale. Rotations stay proper, so whatever the car turns
        /// later (wheels, the steering wheel) still turns the right way. Call before reading the parts' rest poses.
        /// </summary>
        public static void Mirror(Transform root)
        {
            if (root == null) return;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                var p = c.localPosition;
                c.localPosition = new Vector3(-p.x, p.y, p.z);
                c.localRotation = Mirror(c.localRotation);
                var s = c.localScale;
                c.localScale = new Vector3(-s.x, s.y, s.z);
            }
        }

        /// <summary>The rotation seen in a mirror across the YZ plane (M·R·M).</summary>
        public static Quaternion Mirror(Quaternion q) => new(q.x, -q.y, -q.z, q.w);
    }
}
