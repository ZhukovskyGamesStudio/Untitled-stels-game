using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The faces of a screen's mesh (pure but for the cache): furniture models carry a screen as a thin box (a monitor, a row
    /// of them) whose texture coordinates are anything — often none. A copy of the mesh gets a whole picture on each flat
    /// upright face big enough to be a screen: 0..1 across it, the right way up and round for whoever looks at it; all the
    /// rest lies on the picture's dark corner.
    /// </summary>
    public static class ScreenFaces
    {
        /// <summary>A face smaller than this either way is an edge or a lamp, metres.</summary>
        public const float MinSide = 0.1f;

        static readonly Dictionary<Mesh, Mesh> s_Fitted = new();
        static readonly HashSet<Mesh> s_Copies = new();
        static readonly Dictionary<(Mesh, int), Mesh> s_Pictures = new();

        /// <summary>A display smaller than this either way is a lamp or an edge, metres; longer than this many times its height: a strip of light.</summary>
        public const float DisplaySide = 0.012f, DisplayStretch = 5f;

        /// <summary>The mesh of small displays, every face of it showing one picture of the displays' texture.</summary>
        public static Mesh Of(Mesh mesh, int picture) => Of(mesh, new PictureSheet
        {
            Cols = ScreenLooks.DisplayCols, Rows = ScreenLooks.DisplayRows, Cell = picture, Rest = ScreenLooks.PlainDisplay, MinSide = DisplaySide, Lying = true,
        });

        /// <summary>The mesh with one picture of a sheet on each of its flat faces (<see cref="PictureSheet"/>).</summary>
        public static Mesh Of(Mesh mesh, PictureSheet sheet)
        {
            if (mesh == null || s_Copies.Contains(mesh) || !mesh.isReadable) return mesh;
            int key = ((sheet.Cols * 31 + sheet.Rows) * 31 + sheet.Cell) * 31 + sheet.Rest + (sheet.Lying ? 1 << 20 : 0);
            if (s_Pictures.TryGetValue((mesh, key), out var known) && known != null) return known;
            var uv = Fit(mesh.vertices, mesh.triangles, sheet.MinSide, sheet.Lying, true);
            float w = 1f / sheet.Cols, h = 1f / sheet.Rows;
            var rest = new Vector2((sheet.Rest % sheet.Cols + 0.5f) * w, 1f - (sheet.Rest / sheet.Cols + 0.5f) * h);
            float x = sheet.Cell % sheet.Cols * w, y = 1f - (sheet.Cell / sheet.Cols + 1) * h;
            for (int i = 0; i < uv.Length; i++) uv[i] = uv[i].x < 0f ? rest : new Vector2(x + (0.02f + 0.96f * uv[i].x) * w, y + (0.02f + 0.96f * uv[i].y) * h);
            var copy = Object.Instantiate(mesh);
            copy.name = mesh.name;
            copy.hideFlags = HideFlags.DontSave;
            copy.uv = uv;
            s_Pictures[(mesh, key)] = copy;
            s_Copies.Add(copy);
            VisualStyles.KnowCopy(copy, mesh);
            return copy;
        }

        /// <summary>The mesh with its screens' pictures fitted (made once; the mesh itself when it already is, or cannot be read).</summary>
        public static Mesh Of(Mesh mesh)
        {
            if (mesh == null || s_Copies.Contains(mesh) || !mesh.isReadable) return mesh;
            if (s_Fitted.TryGetValue(mesh, out var known) && known != null) return known;
            var uv = Fit(mesh.vertices, mesh.triangles);
            var copy = Object.Instantiate(mesh);
            copy.name = mesh.name;
            copy.hideFlags = HideFlags.DontSave;
            copy.uv = uv;
            s_Fitted[mesh] = copy;
            s_Copies.Add(copy);
            VisualStyles.KnowCopy(copy, mesh);
            return copy;
        }

        /// <summary>
        /// The texture coordinates (pure): triangles in one plane that touch are a face; a face turned to the side (not up or
        /// down) at least <see cref="MinSide"/> both ways gets 0..1, u to the right and v up as seen from in front of it.
        /// </summary>
        /// <param name="lying">A face that lies flat counts too, seen from the model's front (a panel's display).</param>
        public static Vector2[] Fit(Vector3[] v, int[] tris, float minSide = MinSide, bool lying = false, bool mark = false)
        {
            var uv = new Vector2[v.Length]; // the corner: dark
            if (lying || mark) System.Array.Fill(uv, new Vector2(-1f, -1f)); // (no face of a picture: marked for the caller)
            int n = tris.Length / 3;
            var normal = new Vector3[n];
            var parent = new int[n];
            var at = new Dictionary<(Vector3Int point, Vector3Int facing), int>();
            for (int t = 0; t < n; t++)
            {
                parent[t] = t;
                Vector3 a = v[tris[t * 3]], b = v[tris[t * 3 + 1]], c = v[tris[t * 3 + 2]];
                normal[t] = Vector3.Cross(b - a, c - a).normalized;
                var facing = Vector3Int.RoundToInt(normal[t] * 25f);
                foreach (var p in new[] { a, b, c })
                {
                    var key = (Vector3Int.RoundToInt(p * 1000f), facing);
                    if (at.TryGetValue(key, out int other)) parent[Root(t)] = Root(other);
                    else at[key] = t;
                }
            }
            int Root(int t)
            {
                while (parent[t] != t) t = parent[t] = parent[parent[t]];
                return t;
            }
            var faces = new Dictionary<int, List<int>>();
            for (int t = 0; t < n; t++)
            {
                if (normal[t] == Vector3.zero) continue;
                int root = Root(t);
                if (!faces.TryGetValue(root, out var list)) faces[root] = list = new List<int>();
                list.Add(t);
            }
            foreach (var face in faces.Values)
            {
                var f = normal[face[0]];
                bool flat = Mathf.Abs(f.y) > 0.9f;
                if (flat && (!lying || f.y < 0f)) continue; // lies flat: a tray, a top
                var right = flat ? Vector3.left : Vector3.Cross(f, Vector3.up).normalized; // of whoever looks at it (from +Z)
                var up = Vector3.Cross(right, f).normalized;
                float u0 = float.MaxValue, u1 = float.MinValue, w0 = float.MaxValue, w1 = float.MinValue;
                foreach (int t in face)
                    for (int k = 0; k < 3; k++)
                    {
                        var p = v[tris[t * 3 + k]];
                        float u = Vector3.Dot(p, right), w = Vector3.Dot(p, up);
                        u0 = Mathf.Min(u0, u); u1 = Mathf.Max(u1, u);
                        w0 = Mathf.Min(w0, w); w1 = Mathf.Max(w1, w);
                    }
                if (u1 - u0 < minSide || w1 - w0 < minSide || lying && (u1 - u0 > (w1 - w0) * DisplayStretch || w1 - w0 > (u1 - u0) * DisplayStretch)) continue;
                if (lying)
                {
                    // a ring, a sign's shape, a printer's lit bed: no display either
                    float area = 0f;
                    foreach (int t in face) area += Vector3.Cross(v[tris[t * 3 + 1]] - v[tris[t * 3]], v[tris[t * 3 + 2]] - v[tris[t * 3]]).magnitude / 2f;
                    if (area < 0.75f * (u1 - u0) * (w1 - w0) || flat && Mathf.Max(u1 - u0, w1 - w0) > 0.25f) continue;
                }
                foreach (int t in face)
                    for (int k = 0; k < 3; k++)
                    {
                        int i = tris[t * 3 + k];
                        uv[i] = new Vector2((Vector3.Dot(v[i], right) - u0) / (u1 - u0), (Vector3.Dot(v[i], up) - w0) / (w1 - w0));
                    }
            }
            return uv;
        }
    }
}
