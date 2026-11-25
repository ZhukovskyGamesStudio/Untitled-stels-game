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

        /// <summary>
        /// A headlight's strength with something <paramref name="distance"/> metres before it (pure): its full strength,
        /// but no more than puts <paramref name="glare"/> on that surface (a beam made to reach 65 m of tunnel puts a
        /// thousand times as much on a gate 2 m away: two burnt-out discs that hurt to look at). Nearer than a metre counts
        /// as a metre; nothing ahead, or no limit (<paramref name="glare"/> 0): full.
        /// </summary>
        public static float Headlight(float full, float distance, float glare)
        {
            if (glare <= 0f || float.IsInfinity(distance)) return full;
            float d = Mathf.Max(1f, distance);
            return Mathf.Min(full, glare * d * d);
        }

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

        /// <summary>The steering column of the model (car.py STEER_AXIS in Unity axes): from the wheel's face to the driver.</summary>
        public static readonly Vector3 SteeringAxis = new Vector3(0f, Mathf.Sin(25f * Mathf.Deg2Rad), -Mathf.Cos(25f * Mathf.Deg2Rad));

        /// <summary>
        /// The steering wheel's turn for the front wheels' angle (positive = right): clockwise as the driver sees it for a
        /// right turn (Unity turns a positive angle clockwise round an axis that points at the viewer), <paramref name="ratio"/>
        /// times the wheels' angle.
        /// </summary>
        public static Quaternion SteeringWheel(float steer, float ratio) => Quaternion.AngleAxis(steer * ratio, SteeringAxis);

        /// <summary>The steering wheel's rim: its radius about the hub (car.py steering(): the ring's torus), metres.</summary>
        public const float RimRadius = 0.18f;

        /// <summary>The driver's hands on the rim with the wheel straight: this many degrees either side of its top (ten to two).</summary>
        public const float HandsAt = 60f;

        /// <summary>
        /// The hands turn with the wheel this far (degrees of the wheel); past it the wheel slides on in the palms. At full
        /// lock it turns 136°: hands that followed all the way crossed the arms and pulled one elbow through the body.
        /// </summary>
        public const float HandsFollow = 90f;

        /// <summary>How far the hands turn for the wheel's turn (degrees, + clockwise as the driver sees it): with it, up to <see cref="HandsFollow"/>.</summary>
        public static float HandTurn(float wheelTurn) => Mathf.Clamp(wheelTurn, -HandsFollow, HandsFollow);

        /// <summary>
        /// A point of the rim in the steering wheel's own space (pivot at the hub, the column along <see cref="SteeringAxis"/>):
        /// <paramref name="degrees"/> clockwise from its top as the driver sees it (+90: the driver's right, the model's +X).
        /// </summary>
        public static Vector3 RimPoint(float degrees)
        {
            var axis = SteeringAxis;
            var top = (Vector3.up - axis * Vector3.Dot(Vector3.up, axis)).normalized;
            var right = Vector3.Cross(axis, top);
            float r = degrees * Mathf.Deg2Rad;
            return (top * Mathf.Cos(r) + right * Mathf.Sin(r)) * RimRadius;
        }

        /// <summary>The rotation seen in a mirror across the YZ plane (M·R·M).</summary>
        public static Quaternion Mirror(Quaternion q) => new(q.x, -q.y, -q.z, q.w);
    }
}
