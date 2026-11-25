using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A little lamp that is part of a piece of furniture (the lamp on a bedside table, a banker's lamp, a pod's light
    /// strip) gives real light: a small pool under it, which robots see a player in like in any lamp's
    /// (<see cref="LightSource"/> on the light). The glowing part alone lit nothing — a lamp burning in a dark room that
    /// left the room dark. The light hangs over whatever glows now: a style may show its own model of the furniture.
    /// </summary>
    public class GlowLamp : MonoBehaviour
    {
        [Tooltip("The light: a spot shining down.")]
        public Light Lamp;
        [Tooltip("The parts that glow: the shade, the strip.")]
        public Renderer[] Bulbs = new Renderer[0];
        [Tooltip("How far over the top of the glowing part the light hangs, metres (right at it the light burns a white disc into the furniture).")]
        public float Lift = 0.5f;

        void OnEnable()
        {
            VisualStyles.GeometryChanged += Apply;
            Apply();
        }

        void OnDisable() => VisualStyles.GeometryChanged -= Apply;

        void Start() => Apply(); // (the level builder may have moved the furniture after it was made)

        /// <summary>Where the light hangs over what glows (pure).</summary>
        public static Vector3 Above(Bounds bulbs, float lift) => new(bulbs.center.x, bulbs.max.y + lift, bulbs.center.z);

        /// <summary>Hangs the light over what glows now; nothing glows (a style's model without the lamp) — no light.</summary>
        public void Apply()
        {
            if (Lamp == null) return;
            bool any = false;
            var box = new Bounds();
            if (Bulbs != null)
                foreach (var b in Bulbs)
                {
                    if (b == null || !b.enabled || !b.gameObject.activeInHierarchy) continue;
                    if (any) box.Encapsulate(b.bounds);
                    else box = b.bounds;
                    any = true;
                }
            if (Lamp.gameObject.activeSelf != any) Lamp.gameObject.SetActive(any);
            if (!any) return;
            Lamp.transform.SetPositionAndRotation(Above(box, Lift), Quaternion.LookRotation(Vector3.down, Vector3.forward));
        }
    }
}
