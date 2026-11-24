using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>Cuts a mesh in two at a height (pure): a piece of wall whose lower band has a finish of its own (tiles, a painted dado).</summary>
    public static class MeshSplit
    {
        /// <summary>What of the mesh is under local height <paramref name="y"/> and what is over it (null: nothing there). Positions and normals only.</summary>
        public static (Mesh under, Mesh over) AtHeight(Mesh mesh, float y)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            var tris = mesh.triangles;
            if (n.Length != v.Length) n = null;
            var lists = new (List<Vector3> v, List<Vector3> n, List<int> t)[2];
            for (int k = 0; k < 2; k++) lists[k] = (new List<Vector3>(), new List<Vector3>(), new List<int>());
            var poly = new List<(Vector3 p, Vector3 n)>(5);
            for (int i = 0; i < tris.Length; i += 3)
                for (int side = 0; side < 2; side++)
                {
                    poly.Clear();
                    for (int c = 0; c < 3; c++)
                    {
                        int a = tris[i + c], b = tris[i + (c + 1) % 3];
                        Vector3 pa = v[a], pb = v[b], na = n != null ? n[a] : Vector3.up, nb = n != null ? n[b] : Vector3.up;
                        float da = side == 0 ? y - pa.y : pa.y - y, db = side == 0 ? y - pb.y : pb.y - y;
                        if (da >= 0f) poly.Add((pa, na));
                        if ((da >= 0f) == (db >= 0f)) continue;
                        float k = da / (da - db);
                        poly.Add((Vector3.Lerp(pa, pb, k), Vector3.Slerp(na, nb, k)));
                    }
                    var (lv, ln, lt) = lists[side];
                    for (int c = 2; c < poly.Count; c++)
                    {
                        int at = lv.Count;
                        foreach (var q in new[] { poly[0], poly[c - 1], poly[c] }) { lv.Add(q.p); ln.Add(q.n); }
                        if (Vector3.Cross(lv[at + 1] - lv[at], lv[at + 2] - lv[at]).sqrMagnitude < 1e-14f)
                        {
                            lv.RemoveRange(at, 3);
                            ln.RemoveRange(at, 3);
                            continue;
                        }
                        lt.Add(at); lt.Add(at + 1); lt.Add(at + 2);
                    }
                }
            return (Make(0), Make(1));

            Mesh Make(int k)
            {
                if (lists[k].t.Count == 0) return null;
                var m = new Mesh { name = mesh.name + (k == 0 ? "_Under" : "_Over") };
                m.SetVertices(lists[k].v);
                m.SetNormals(lists[k].n);
                m.SetUVs(0, new Vector2[lists[k].v.Count]);
                m.SetTriangles(lists[k].t, 0);
                m.RecalculateTangents();
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
