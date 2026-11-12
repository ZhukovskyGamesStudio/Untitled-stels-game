using System.Collections.Generic;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.EditorTools
{
    /// <summary>
    /// The built looks of the staircase (<see cref="StairStyle"/>): concrete with solid parapets, painted wood with a
    /// stringer, balusters and a handrail, open steel with channel stringers, tube rails and a mesh cage under it, white
    /// slabs with glass balustrades. Each is one mesh asset (submesh per material) generated here in the staircase's own
    /// frame (<see cref="StairGeometry"/>: u = x, v = z), over the unchanged collider boxes. The space the colliders fill
    /// under the landing and the upper flight is always closed by a panel of the look, so nothing invisible blocks the way.
    /// </summary>
    public static partial class AssetBuilder
    {
        const string StairMeshDir = Root + "/Art/Models/Stairs";

        // the staircase's numbers (LevelMetrics / StairGeometry)
        const float SH = LevelMetrics.FloorHeight, SHalf = SH / 2f, SRun = LevelMetrics.StepRun, SStart = LevelMetrics.FlightStart,
            SLand = LevelMetrics.LandingEnd, SSize = LevelMetrics.StairSize;
        const int SSteps = LevelMetrics.StepsPerFlight;
        const float SRise = SHalf / SSteps;
        const float A0 = StairGeometry.LaneA0, A1 = StairGeometry.LaneA1, B0 = StairGeometry.LaneB0, B1 = StairGeometry.LaneB1;
        /// <summary>The outer edge of the upper flight and the landing: where its railing stands.</summary>
        const float Edge = B1 + 0.06f;
        /// <summary>Where the divider between the flights starts (two steps off the landing).</summary>
        const float DividerU = SLand + 2 * SRun;

        /// <summary>A mesh made of boxes, tubes and slabs, one submesh per material, UVs in metres.</summary>
        sealed class MeshKit
        {
            readonly List<Vector3> m_V = new(), m_N = new();
            readonly List<Vector2> m_Uv = new();
            readonly List<int>[] m_Tris;

            public MeshKit(int materials)
            {
                m_Tris = new List<int>[materials];
                for (int i = 0; i < materials; i++) m_Tris[i] = new List<int>();
            }

            void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
            {
                int i = m_V.Count;
                m_V.Add(a); m_V.Add(b); m_V.Add(c); m_V.Add(d);
                for (int k = 0; k < 4; k++) m_N.Add(n);
                // planar UVs in metres: project onto the two axes the face lies along
                var t = Mathf.Abs(n.y) > 0.7f ? Vector3.right : Vector3.up;
                var s = Vector3.Cross(n, t).normalized;
                t = Vector3.Cross(s, n).normalized;
                foreach (var p in new[] { a, b, c, d }) m_Uv.Add(new Vector2(Vector3.Dot(p, s), Vector3.Dot(p, t)));
                m_Tris[sub].AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            /// <summary>A box: centre, size, turned by <paramref name="rot"/>.</summary>
            public void Box(int sub, Vector3 c, Vector3 size, Quaternion rot)
            {
                var h = size / 2f;
                Vector3 P(float x, float y, float z) => c + rot * new Vector3(x * h.x, y * h.y, z * h.z);
                Vector3 R(Vector3 n) => rot * n;
                Quad(sub, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), R(Vector3.forward));
                Quad(sub, P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), R(Vector3.back));
                Quad(sub, P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1), R(Vector3.up));
                Quad(sub, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), R(Vector3.down));
                Quad(sub, P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), R(Vector3.right));
                Quad(sub, P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), R(Vector3.left));
            }

            /// <summary>An axis-aligned box between two corners.</summary>
            public void Span(int sub, float u0, float u1, float y0, float y1, float v0, float v1)
            {
                if (u1 - u0 < 1e-4f || y1 - y0 < 1e-4f || v1 - v0 < 1e-4f) return;
                Box(sub, new Vector3((u0 + u1) / 2f, (y0 + y1) / 2f, (v0 + v1) / 2f), new Vector3(u1 - u0, y1 - y0, v1 - v0), Quaternion.identity);
            }

            /// <summary>A bar of the given cross-section from <paramref name="a"/> to <paramref name="b"/> (square tubes, rails, stringers).</summary>
            public void Bar(int sub, Vector3 a, Vector3 b, float width, float height)
            {
                var d = b - a;
                if (d.sqrMagnitude < 1e-6f) return;
                var up = Mathf.Abs(Vector3.Dot(d.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
                Box(sub, (a + b) / 2f, new Vector3(width, height, d.magnitude), Quaternion.LookRotation(d, up));
            }

            /// <summary>A round tube from <paramref name="a"/> to <paramref name="b"/>.</summary>
            public void Tube(int sub, Vector3 a, Vector3 b, float r, int seg = 8)
            {
                var d = b - a;
                if (d.sqrMagnitude < 1e-6f) return;
                var rot = Quaternion.LookRotation(d, Mathf.Abs(Vector3.Dot(d.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up);
                float len = d.magnitude;
                for (int k = 0; k < seg; k++)
                {
                    float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                    Vector3 o0 = rot * new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r, 0f), o1 = rot * new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r, 0f);
                    var n = (o0 + o1).normalized;
                    Quad(sub, a + o0, a + o1, b + o1, b + o0, n);
                }
            }

            public Mesh Write(Mesh mesh, string name)
            {
                mesh.Clear();
                mesh.name = name;
                mesh.indexFormat = m_V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(m_V);
                mesh.SetNormals(m_N);
                mesh.SetUVs(0, m_Uv);
                mesh.subMeshCount = m_Tris.Length;
                for (int i = 0; i < m_Tris.Length; i++) mesh.SetTriangles(m_Tris[i], i);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }

        // ------------------------------------------------------------------ the shared shape

        static float TreadA(int i) => (i + 1) * SRise;
        static float TreadB(int j) => SHalf + (j + 1) * SRise;
        static (float u0, float u1) StepA(int i) => (SStart - (i + 1) * SRun, SStart - i * SRun);
        static (float u0, float u1) StepB(int j) => (SLand + j * SRun, SLand + (j + 1) * SRun);

        /// <summary>The nosing line of the upper flight: through the front edges of its treads (it climbs towards +u).</summary>
        static float LineB(float u) => SHalf + ((u - SLand) / SRun + 1f) * SRise;
        /// <summary>The nosing line of the lower flight (it climbs towards −u).</summary>
        static float LineA(float u) => ((SStart - u) / SRun + 1f) * SRise;

        /// <summary>
        /// The solid body every look keeps (where the colliders are solid and the eye can reach): the steps of both flights as
        /// blocks from the floor, the landing block, the exit lip and the end panel under it.
        /// </summary>
        static void SolidSteps(MeshKit k, int sub, float topInset = 0f)
        {
            for (int i = 0; i < SSteps; i++) { var (u0, u1) = StepA(i); k.Span(sub, u0, u1, 0f, TreadA(i) - topInset, 0.12f, A1); }
            k.Span(sub, 0.12f, SLand, 0f, SHalf - topInset, 0.12f, B1);
            for (int j = 0; j < SSteps; j++) { var (u0, u1) = StepB(j); k.Span(sub, u0, u1, 0f, TreadB(j) - topInset, B0, B1); }
            k.Span(sub, SStart, SSize + 0.08f, SH - 0.25f, SH - topInset, B0, B1);
            k.Span(sub, SSize - 0.06f, SSize + 0.08f, 0f, SH - 0.25f, B0, B1); // the end panel under the top of the upper flight
        }

        /// <summary>Tread caps with a nosing (a thin board on every step, a little proud of its front).</summary>
        static void Treads(MeshKit k, int sub, float thick, float nose)
        {
            for (int i = 0; i < SSteps; i++)
            {
                var (u0, u1) = StepA(i);
                k.Span(sub, u0, u1 + nose, TreadA(i) - thick, TreadA(i), 0.12f, A1); // its front faces +u
            }
            k.Span(sub, 0.12f, SLand, SHalf - thick, SHalf, 0.12f, B1);
            for (int j = 0; j < SSteps; j++)
            {
                var (u0, u1) = StepB(j);
                k.Span(sub, u0 - nose, u1, TreadB(j) - thick, TreadB(j), B0, B1); // its front faces −u
            }
            k.Span(sub, SStart, SSize + 0.08f, SH - thick, SH, B0, B1);
        }

        /// <summary>
        /// A slab leaning along the upper flight's outer edge: its lower edge <paramref name="above"/> over the nosing line,
        /// <paramref name="height"/> tall measured straight up (from the first tread to the last).
        /// </summary>
        static void InclinedPanel(MeshKit k, int sub, float v, float thick, float above, float height)
        {
            var a = new Vector3(SLand, LineB(SLand) + above, v);
            var b = new Vector3(SStart - SRun, LineB(SStart - SRun) + above, v);
            var dir = (b - a).normalized;
            // local x along the flight, y across the slope (in the u-y plane), z across the panel
            var q = Quaternion.FromToRotation(Vector3.right, dir);
            float across = height * dir.x; // straight-up height measured across the slope
            k.Box(sub, (a + b) / 2f + q * new Vector3(0f, across / 2f, 0f), new Vector3((b - a).magnitude, across, thick), q);
        }

        /// <summary>A rail along the upper flight's outer edge at <paramref name="above"/> over the nosing line.</summary>
        static void RailB(MeshKit k, int sub, float v, float above, float w, float h, bool round)
        {
            var a = new Vector3(SLand - 0.05f, LineB(SLand - 0.05f) + above, v);
            var b = new Vector3(SStart + 0.05f, LineB(SStart + 0.05f) + above, v);
            if (round) k.Tube(sub, a, b, w / 2f);
            else k.Bar(sub, a, b, w, h);
        }

        /// <summary>The railings around the hole upstairs (where the collider rails are): <paramref name="post"/> draws one at u/v.</summary>
        static void UpperEdges(System.Action<Vector3, Vector3> run)
        {
            run(new Vector3(0.1f, SH, SSize), new Vector3(SSize, SH, SSize));
            run(new Vector3(SSize, SH, 0.1f), new Vector3(SSize, SH, B0 - 0.05f));
        }

        // ------------------------------------------------------------------ the looks

        static Mesh StairConcrete()
        {
            var k = new MeshKit(3); // 0 concrete, 1 treads, 2 caps
            SolidSteps(k, 0, 0.03f);
            Treads(k, 1, 0.03f, 0.03f);
            // a solid parapet along the open edge: blocks up to the treads, a slab leaning along the flight, a cap
            for (int j = 0; j < SSteps; j++) { var (u0, u1) = StepB(j); k.Span(0, u0, u1, 0f, TreadB(j), B1, Edge + 0.06f); }
            k.Span(0, 0.12f, SLand, 0f, SHalf + 0.95f, B1, Edge + 0.06f);
            InclinedPanel(k, 0, Edge, 0.12f, -0.05f, 1.0f);
            RailB(k, 2, Edge, 0.97f, 0.16f, 0.05f, false);
            k.Span(2, 0.1f, SLand, SHalf + 0.95f, SHalf + 1.0f, B1 - 0.02f, Edge + 0.08f);
            // the spine wall between the flights
            k.Span(0, DividerU, SStart, 0f, SH - 0.4f, A1, B0);
            k.Span(2, DividerU - 0.02f, SStart + 0.02f, SH - 0.4f, SH - 0.35f, A1 - 0.02f, B0 + 0.02f);
            UpperEdges((a, b) =>
            {
                k.Box(0, (a + b) / 2f + Vector3.up * 0.5f, new Vector3(Mathf.Max(0.12f, Mathf.Abs(b.x - a.x)), 1f, Mathf.Max(0.12f, Mathf.Abs(b.z - a.z))), Quaternion.identity);
                k.Box(2, (a + b) / 2f + Vector3.up * 1.02f, new Vector3(Mathf.Max(0.18f, Mathf.Abs(b.x - a.x)), 0.05f, Mathf.Max(0.18f, Mathf.Abs(b.z - a.z))), Quaternion.identity);
            });
            return StairMesh("stair_concrete", k);
        }

        static Mesh StairWood()
        {
            var k = new MeshKit(3); // 0 painted panels, 1 wood, 2 dark wood (handrail, newels)
            SolidSteps(k, 0, 0.04f);
            Treads(k, 1, 0.04f, 0.035f);
            // a closed stringer board along the open edge and panelling battens under the flight
            InclinedPanel(k, 1, B1 + 0.03f, 0.05f, -0.28f, 0.34f);
            for (float u = 0.3f; u < SStart; u += 0.6f) k.Span(1, u - 0.02f, u + 0.02f, 0.05f, Mathf.Min(SHalf, LineB(Mathf.Max(u, SLand))) - 0.35f, B1, B1 + 0.02f);
            // balusters two per step, a newel at each end, the handrail
            for (int j = 0; j < SSteps; j++)
            {
                var (u0, u1) = StepB(j);
                foreach (float u in new[] { u0 + SRun * 0.25f, u0 + SRun * 0.75f })
                    k.Span(1, u - 0.018f, u + 0.018f, TreadB(j), LineB(u) + 0.9f, B1 - 0.02f, B1 + 0.016f);
            }
            k.Span(2, SLand - 0.12f, SLand - 0.02f, 0f, SHalf + 1.1f, B1 - 0.06f, B1 + 0.04f);
            k.Span(2, SStart - 0.04f, SStart + 0.06f, LineB(SStart) - 0.3f, SH + 1.1f, B1 - 0.06f, B1 + 0.04f);
            RailB(k, 2, B1, 0.92f, 0.08f, 0.06f, false);
            // the landing's edge: balusters and a flat rail
            for (float u = 0.22f; u < SLand - 0.15f; u += 0.14f) k.Span(1, u - 0.018f, u + 0.018f, SHalf, SHalf + 0.92f, B1 - 0.02f, B1 + 0.016f);
            k.Span(2, 0.12f, SLand - 0.02f, SHalf + 0.92f, SHalf + 0.98f, B1 - 0.04f, B1 + 0.04f);
            // the closed space under the top of the flight: a cupboard door in its end
            k.Span(1, SSize + 0.08f, SSize + 0.11f, 0f, 1.95f, B0 + 0.35f, B0 + 1.25f);
            k.Span(2, SSize + 0.11f, SSize + 0.14f, 0.95f, 1.1f, B0 + 1.1f, B0 + 1.14f);
            // the divider: a panelled wall with a wooden handrail along the lower flight
            k.Span(0, DividerU, SStart, 0f, SH - 0.4f, A1, B0);
            var a0 = new Vector3(DividerU, LineA(DividerU) + 0.9f, A1 - 0.04f);
            var a1 = new Vector3(SStart, LineA(SStart) + 0.9f, A1 - 0.04f);
            k.Bar(2, a0, a1, 0.06f, 0.06f);
            UpperEdges((a, b) =>
            {
                var d = b - a;
                int n = Mathf.Max(2, Mathf.RoundToInt(d.magnitude / 0.14f));
                for (int i = 0; i <= n; i++)
                {
                    var p = Vector3.Lerp(a, b, i / (float)n);
                    k.Span(1, p.x - 0.018f, p.x + 0.018f, SH, SH + 0.95f, p.z - 0.018f, p.z + 0.018f);
                }
                k.Box(2, (a + b) / 2f + Vector3.up * 0.98f, new Vector3(Mathf.Max(0.08f, Mathf.Abs(d.x)), 0.06f, Mathf.Max(0.08f, Mathf.Abs(d.z))), Quaternion.identity);
            });
            return StairMesh("stair_wood", k);
        }

        static Mesh StairSteel()
        {
            var k = new MeshKit(3); // 0 steel plate, 1 painted steel (stringers, rails), 2 mesh bars
            // open treads on two channel stringers per flight
            for (int i = 0; i < SSteps; i++) { var (u0, u1) = StepA(i); k.Span(0, u0, u1 + 0.02f, TreadA(i) - 0.04f, TreadA(i), 0.2f, A1); }
            for (int j = 0; j < SSteps; j++) { var (u0, u1) = StepB(j); k.Span(0, u0 - 0.02f, u1, TreadB(j) - 0.04f, TreadB(j), B0, B1); }
            k.Span(0, 0.12f, SLand, SHalf - 0.06f, SHalf, 0.12f, B1);
            k.Span(0, SStart, SSize + 0.08f, SH - 0.06f, SH, B0, B1);
            foreach (float v in new[] { 0.17f, A1 + 0.02f })
                k.Bar(1, new Vector3(SStart + 0.1f, -0.05f, v), new Vector3(SLand - 0.05f, SHalf - 0.12f, v), 0.05f, 0.24f);
            foreach (float v in new[] { B0 - 0.02f, B1 + 0.03f })
                k.Bar(1, new Vector3(SLand - 0.05f, SHalf - 0.12f, v), new Vector3(SStart + 0.12f, SH - 0.12f, v), 0.05f, 0.24f);
            k.Span(1, 0.12f, SLand, SHalf - 0.22f, SHalf - 0.06f, B1 - 0.04f, B1 + 0.04f); // landing edge beam
            // the cage that closes the solid space under the landing and the upper flight (bars every 12 cm in a frame)
            k.Span(1, 0.12f, SStart + 0.1f, 0f, 0.06f, B1 + 0.02f, B1 + 0.07f);
            for (float u = 0.16f; u < SStart + 0.05f; u += 0.12f)
            {
                float top = u < SLand ? SHalf - 0.22f : LineB(u) - 0.2f;
                k.Span(2, u - 0.008f, u + 0.008f, 0.06f, top, B1 + 0.035f, B1 + 0.05f);
            }
            k.Span(2, SSize - 0.04f, SSize, 0f, SH - 0.25f, B0, B1); // the end under the top: a plate
            for (float v = B0 + 0.1f; v < B1; v += 0.12f) k.Span(1, SSize, SSize + 0.02f, 0f, SH - 0.25f, v - 0.01f, v + 0.01f);
            // tube railings: posts every two steps, a top rail and a knee rail
            for (int j = 0; j <= SSteps; j += 2)
            {
                float u = SLand + j * SRun + 0.08f;
                k.Tube(1, new Vector3(u, LineB(u) - 0.1f, B1 + 0.03f), new Vector3(u, LineB(u) + 0.95f, B1 + 0.03f), 0.022f);
            }
            RailB(k, 1, B1 + 0.03f, 0.95f, 0.045f, 0f, true);
            RailB(k, 1, B1 + 0.03f, 0.5f, 0.03f, 0f, true);
            k.Tube(1, new Vector3(0.12f, SHalf + 0.95f, B1 + 0.03f), new Vector3(SLand, SHalf + 0.95f, B1 + 0.03f), 0.0225f);
            k.Tube(1, new Vector3(0.15f, SHalf, B1 + 0.03f), new Vector3(0.15f, SHalf + 0.95f, B1 + 0.03f), 0.022f);
            // the divider: a frame of mesh bars
            k.Span(1, DividerU, SStart, 0f, 0.06f, A1, B0);
            k.Span(1, DividerU, SStart, SH - 0.46f, SH - 0.4f, A1, B0);
            for (float u = DividerU + 0.03f; u < SStart; u += 0.1f) k.Span(2, u - 0.008f, u + 0.008f, 0.06f, SH - 0.46f, A1 + 0.04f, B0 - 0.04f);
            UpperEdges((a, b) =>
            {
                k.Tube(1, a + Vector3.up * 0.98f, b + Vector3.up * 0.98f, 0.0225f);
                k.Tube(1, a + Vector3.up * 0.5f, b + Vector3.up * 0.5f, 0.015f);
                var d = b - a;
                int n = Mathf.Max(1, Mathf.RoundToInt(d.magnitude / 1.0f));
                for (int i = 0; i <= n; i++) { var p = Vector3.Lerp(a, b, i / (float)n); k.Tube(1, p, p + Vector3.up * 0.98f, 0.022f); }
            });
            return StairMesh("stair_steel", k);
        }

        static Mesh StairGlass()
        {
            var k = new MeshKit(3); // 0 white stone, 1 glass, 2 steel caps
            SolidSteps(k, 0);
            // glass along the open edge (blocks of white up to the treads below it), a steel cap rail
            for (int j = 0; j < SSteps; j++) { var (u0, u1) = StepB(j); k.Span(0, u0, u1, 0f, TreadB(j), B1, Edge); }
            InclinedPanel(k, 1, Edge - 0.02f, 0.02f, -0.06f, 1.0f);
            RailB(k, 2, Edge - 0.02f, 0.96f, 0.05f, 0.03f, false);
            k.Span(1, 0.12f, SLand, SHalf, SHalf + 0.95f, Edge - 0.03f, Edge - 0.01f);
            k.Span(2, 0.12f, SLand, SHalf + 0.95f, SHalf + 0.98f, Edge - 0.04f, Edge);
            // the divider: a glass wall in a thin steel frame
            k.Span(1, DividerU, SStart, 0.04f, SH - 0.44f, (A1 + B0) / 2f - 0.01f, (A1 + B0) / 2f + 0.01f);
            k.Span(2, DividerU, SStart, 0f, 0.04f, A1, B0);
            k.Span(2, DividerU, SStart, SH - 0.44f, SH - 0.4f, A1, B0);
            UpperEdges((a, b) =>
            {
                var d = b - a;
                var size = new Vector3(Mathf.Max(0.02f, Mathf.Abs(d.x)), 0.95f, Mathf.Max(0.02f, Mathf.Abs(d.z)));
                k.Box(1, (a + b) / 2f + Vector3.up * 0.475f, size, Quaternion.identity);
                k.Box(2, (a + b) / 2f + Vector3.up * 0.965f, new Vector3(Mathf.Max(0.05f, size.x), 0.03f, Mathf.Max(0.05f, size.z)), Quaternion.identity);
            });
            return StairMesh("stair_glass", k);
        }

        static Mesh StairMesh(string name, MeshKit kit)
        {
            EnsureFolder(StairMeshDir);
            string path = $"{StairMeshDir}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool fresh = mesh == null;
            if (fresh) mesh = new Mesh();
            kit.Write(mesh, name);
            if (fresh) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>The looks in <see cref="StairStyle"/> order: mesh and its materials (submesh order).</summary>
        static (Mesh mesh, Material[] mats)[] StairLooks() => new[]
        {
            (StairConcrete(), new[] { M("Wall"), M("Floor"), M("DoorFrame") }),
            (StairWood(), new[] { M("Ceiling"), M("Wood"), M("DoorFrame") }),
            (StairSteel(), new[] { M("Metal"), M("PropBody"), M("Metal") }),
            (StairGlass(), new[] { M("Ceiling"), M("StairGlass"), M("Metal") }),
        };

        // ------------------------------------------------------------------ railings of galleries

        /// <summary>
        /// One bay of a gallery railing, 1 m along local X (centred), standing on y = 0, in each staircase look: a concrete
        /// parapet with a cap, a wooden post with balusters and a handrail, a steel post with tube rails and a kick plate,
        /// a glass pane under a steel cap. The level builder strings bays along a railing (each stretched a little to fit).
        /// </summary>
        static GameObject BuildRailingBay()
        {
            var root = new GameObject("RailingBay");
            var looks = new (string name, MeshKit kit, Material[] mats)[]
            {
                ("Concrete", new MeshKit(2), new[] { M("Wall"), M("DoorFrame") }),
                ("Wood", new MeshKit(2), new[] { M("Wood"), M("DoorFrame") }),
                ("Steel", new MeshKit(2), new[] { M("Metal"), M("PropBody") }),
                ("Glass", new MeshKit(2), new[] { M("StairGlass"), M("Metal") }),
            };
            const float h = 1.0f;
            var concrete = looks[0].kit;
            concrete.Span(0, -0.5f, 0.5f, 0f, h, -0.06f, 0.06f);
            concrete.Span(1, -0.5f, 0.5f, h, h + 0.05f, -0.09f, 0.09f);
            var wood = looks[1].kit;
            wood.Span(1, -0.54f, -0.46f, 0f, h + 0.08f, -0.04f, 0.04f);                   // the post
            wood.Span(1, -0.5f, 0.5f, h - 0.02f, h + 0.04f, -0.045f, 0.045f);             // the handrail
            wood.Span(0, -0.5f, 0.5f, 0.08f, 0.12f, -0.025f, 0.025f);                     // the bottom rail
            for (int k = 1; k < 8; k++) { float x = -0.5f + k * 0.125f; wood.Span(0, x - 0.017f, x + 0.017f, 0.12f, h - 0.02f, -0.017f, 0.017f); }
            var steel = looks[2].kit;
            steel.Tube(0, new Vector3(-0.5f, 0f, 0f), new Vector3(-0.5f, h + 0.02f, 0f), 0.024f);
            steel.Tube(0, new Vector3(-0.5f, h, 0f), new Vector3(0.5f, h, 0f), 0.022f);
            foreach (float y in new[] { 0.62f, 0.32f }) steel.Tube(0, new Vector3(-0.5f, y, 0f), new Vector3(0.5f, y, 0f), 0.013f);
            steel.Span(1, -0.5f, 0.5f, 0.02f, 0.13f, -0.006f, 0.006f);                    // the kick plate
            var glass = looks[3].kit;
            glass.Span(0, -0.49f, 0.49f, 0.06f, h - 0.02f, -0.01f, 0.01f);
            glass.Span(1, -0.5f, 0.5f, h - 0.02f, h + 0.02f, -0.03f, 0.03f);
            glass.Span(1, -0.5f, -0.46f, 0f, 0.1f, -0.03f, 0.03f);                        // a shoe that holds the pane
            var look = root.AddComponent<StairLook>();
            look.Blocks = new Renderer[0];
            look.Variants = new GameObject[looks.Length];
            for (int i = 0; i < looks.Length; i++)
            {
                var go = new GameObject(looks[i].name);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = StairMesh("railing_" + looks[i].name.ToLowerInvariant(), looks[i].kit);
                go.AddComponent<MeshRenderer>().sharedMaterials = looks[i].mats;
                go.SetActive(false);
                look.Variants[i] = go;
            }
            return root;
        }

        /// <summary>An invisible box: the railing's collider (players and robots cannot pass, the view goes over it).</summary>
        static GameObject BuildRailingBlock()
        {
            var go = new GameObject("RailingBlock");
            go.AddComponent<BoxCollider>();
            return go;
        }

        /// <summary>Every look under the staircase (inactive), and the component that shows the one that fits.</summary>
        static void AddStairLooks(GameObject root, bool mirrored)
        {
            var look = root.AddComponent<StairLook>();
            look.Blocks = root.GetComponentsInChildren<Renderer>(true);
            var holder = new GameObject("Looks").transform;
            holder.SetParent(root.transform, false);
            if (mirrored) holder.localScale = new Vector3(-1f, 1f, 1f); // the mirrored frame flips u
            var looks = StairLooks();
            look.Variants = new GameObject[looks.Length];
            for (int i = 0; i < looks.Length; i++)
            {
                var go = new GameObject(((StairStyle)i).ToString());
                go.transform.SetParent(holder, false);
                go.AddComponent<MeshFilter>().sharedMesh = looks[i].mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = looks[i].mats;
                go.SetActive(false);
                look.Variants[i] = go;
            }
        }
    }
}
