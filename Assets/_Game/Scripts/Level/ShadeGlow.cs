using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A lamp's shade lit from inside (Art/Shaders/Shade.shader): tells the shade where its bulb is, so its glow falls off
    /// from it instead of being one flat colour. On the lamp prefabs; a picture only (the lamp's light is its Light).
    /// The property block also keeps the shade out of the furniture merge: it draws itself, where it stands.
    /// </summary>
    public class ShadeGlow : MonoBehaviour
    {
        static readonly int AtId = Shader.PropertyToID("_InnerAt"), SpanId = Shader.PropertyToID("_InnerSpan");

        [Tooltip("The shade: the parts that glow by the bulb.")]
        public Renderer[] Shades = new Renderer[0];
        [Tooltip("The bulb or the diffuser: where the light comes from.")]
        public Renderer[] Bulbs = new Renderer[0];

        MaterialPropertyBlock m_Block;

        void OnEnable()
        {
            VisualStyles.GeometryChanged += Apply;
            Apply();
        }

        void OnDisable() => VisualStyles.GeometryChanged -= Apply;

        void Start() => Apply(); // (the level builder may have moved the lamp after it was made)

        /// <summary>The nearest and the farthest a box comes to a point (pure): 0 for the nearest when the point is inside.</summary>
        public static (float near, float far) Reach(Bounds box, Vector3 point)
        {
            float near = Mathf.Sqrt(box.SqrDistance(point));
            var c = box.center - point;
            var e = box.extents;
            float far = new Vector3(Mathf.Abs(c.x) + e.x, Mathf.Abs(c.y) + e.y, Mathf.Abs(c.z) + e.z).magnitude;
            return (near, far);
        }

        /// <summary>Works out where the bulb is and hands it to the shade (again when the style changes the models).</summary>
        public void Apply()
        {
            if (Bulbs == null || Bulbs.Length == 0 || Shades == null) return;
            var at = Vector3.zero;
            int n = 0;
            foreach (var b in Bulbs)
                if (b != null) { at += b.bounds.center; n++; }
            if (n == 0) return;
            at /= n;
            m_Block ??= new MaterialPropertyBlock();
            foreach (var shade in Shades)
            {
                if (shade == null) continue;
                var (near, far) = Reach(shade.bounds, at);
                shade.GetPropertyBlock(m_Block);
                m_Block.SetVector(AtId, new Vector4(at.x, at.y, at.z, near));
                m_Block.SetFloat(SpanId, Mathf.Max(far - near, 0.01f));
                shade.SetPropertyBlock(m_Block);
            }
        }
    }
}
