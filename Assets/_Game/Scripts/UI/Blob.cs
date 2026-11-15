using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>A flat filled shape of the studio's mark (the wing, its eye): its triangles are set from code, in its own pixels.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class Blob : MaskableGraphic
    {
        readonly List<Vector2> m_Tris = new();

        public void SetTriangles(List<Vector2> tris)
        {
            m_Tris.Clear();
            m_Tris.AddRange(tris);
            SetVerticesDirty();
        }

        public void Ellipse(Vector2 c, float rx, float ry, int seg = 48)
        {
            m_Tris.Clear();
            if (rx > 0.01f && ry > 0.01f)
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                    m_Tris.Add(c);
                    m_Tris.Add(c + new Vector2(Mathf.Cos(a0) * rx, Mathf.Sin(a0) * ry));
                    m_Tris.Add(c + new Vector2(Mathf.Cos(a1) * rx, Mathf.Sin(a1) * ry));
                }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 0; i + 2 < m_Tris.Count; i += 3)
            {
                int b = vh.currentVertCount;
                for (int k = 0; k < 3; k++) vh.AddVert(m_Tris[i + k], color, Vector2.zero);
                vh.AddTriangle(b, b + 1, b + 2);
            }
        }
    }
}
