using System.Collections.Generic;
using Stealth.Configs;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>Pieces of the style architecture kit (Art/Models/StyleKit, prefabs in <see cref="StyleKit"/>). Append only.</summary>
    public enum DecorKind
    {
        Baseboard, Crown, Rail, Wainscot, Pipes, PipeClamp, Strip, Pilaster, Casing, Arch, Beam, IBeam, Coffer, Tray, TrayHanger,
        FloorFinish, WallFinish, CeilingFinish,
    }

    /// <summary>One piece to place: a kit prefab (or a finish quad) and its transform; finishes carry their size in metres.</summary>
    public struct DecorPiece
    {
        public DecorKind Kind;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        /// <summary>Finish quads: size (x, y) and the world offset of their corner (z, w) in metres, for texture tiling.</summary>
        public Vector4 Surface;
    }

    /// <summary>
    /// Lays the architecture of a visual style onto the house (pure, unit tested): trims along every wall face, panels,
    /// pipes and light strips around windows and openings, frames at doorways, beams under the ceilings that avoid the
    /// lamps and the stair holes, finish quads on floors and walls with windows cut out.
    /// </summary>
    public static class ArchitecturePlanner
    {
        const float T = LevelMetrics.WallThickness;
        /// <summary>Window holes in the finishes and the strips (the frame overhangs the opening by 6 cm).</summary>
        const float WindowHalfWidth = LevelMetrics.WindowWidth / 2f + 0.08f;
        const float WindowBottom = LevelMetrics.WindowCenterY - LevelMetrics.WindowHeight / 2f - 0.08f;
        const float WindowTop = LevelMetrics.WindowCenterY + LevelMetrics.WindowHeight / 2f + 0.08f;
        /// <summary>Ceiling lamps hang at ±2.5 m along x of a cell centre (see <see cref="RoomLighting.LampOffset"/>).</summary>
        static readonly float[] BeamsAlongZ = { -3.75f, 0f, 3.75f };
        static readonly float[] BeamsAlongX = { -2.5f, 2.5f };

        public static List<DecorPiece> Plan(LevelShell shell, LevelLayout layout, StyleGeometry g)
        {
            var list = new List<DecorPiece>();
            if (shell == null || g == null) return list;
            foreach (var face in shell.Walls) PlanFace(list, face, shell, g);
            foreach (var o in shell.Openings) PlanOpening(list, o, g);
            if (g.Ceiling != CeilingKind.None && layout != null) PlanCeilings(list, layout, g.Ceiling);
            if (g.CeilingFinish != null && layout != null) PlanCeilingFinish(list, shell, layout);
            if (g.Floor != null)
                foreach (var r in shell.Floors)
                {
                    var size = r.Max - r.Min;
                    list.Add(new DecorPiece
                    {
                        Kind = DecorKind.FloorFinish,
                        Position = new Vector3((r.Min.x + r.Max.x) / 2f, r.Y + 0.008f, (r.Min.y + r.Max.y) / 2f),
                        Rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward),
                        Scale = new Vector3(size.x, size.y, 1f),
                        Surface = new Vector4(size.x, size.y, r.Min.x, r.Min.y),
                    });
                }
            return list;
        }

        // ------------------------------------------------------------------ walls

        static void PlanFace(List<DecorPiece> list, WallFace f, LevelShell shell, StyleGeometry g)
        {
            // Reach into the room's corners: past the corner post on that side.
            float u0 = f.CornerA ? -T : 0f, u1 = f.Length + (f.CornerB ? T : 0f);
            float len = u1 - u0;
            if (len < 0.05f) return;
            var rot = Quaternion.LookRotation(f.Normal);
            Vector3 At(float u, float y) => f.At(u) + Vector3.up * y;
            bool fullHeight = f.Y0 <= 0.001f;
            var holes = fullHeight ? WindowHoles(f, shell) : new List<(float, float)>();

            void Run(DecorKind kind, float y)
            {
                list.Add(new DecorPiece { Kind = kind, Position = At((u0 + u1) / 2f, y), Rotation = rot, Scale = new Vector3(len, 1f, 1f) });
            }

            if (fullHeight && g.Baseboard && g.WainscotHeight <= 0f) Run(DecorKind.Baseboard, 0f);
            if (g.Crown) Run(DecorKind.Crown, LevelMetrics.WallHeight);
            if (fullHeight && g.RailHeight > 0f) Run(DecorKind.Rail, g.RailHeight);
            if (fullHeight && g.WainscotHeight > 0f)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(len / 1.1f));
                float w = len / n;
                for (int i = 0; i < n; i++)
                    list.Add(new DecorPiece
                    {
                        Kind = DecorKind.Wainscot, Position = At(u0 + w * (i + 0.5f), 0f), Rotation = rot,
                        Scale = new Vector3(w, g.WainscotHeight, 1f),
                    });
            }
            if (g.Pipes && f.Y0 < 2.5f)
            {
                Run(DecorKind.Pipes, 0f);
                for (float u = u0 + 0.6f; u < u1 - 0.4f; u += 1.5f)
                    list.Add(new DecorPiece { Kind = DecorKind.PipeClamp, Position = At(u, 0f), Rotation = rot, Scale = Vector3.one });
            }
            if (fullHeight && g.StripSpacing > 0f)
            {
                int n = Mathf.FloorToInt((len - 0.8f) / g.StripSpacing);
                for (int i = 0; i <= n; i++)
                {
                    float u = (u0 + u1) / 2f + (i - n / 2f) * g.StripSpacing;
                    if (u < u0 + 0.4f || u > u1 - 0.4f || Inside(holes, u, 0.1f)) continue;
                    list.Add(new DecorPiece
                    {
                        Kind = DecorKind.Strip, Position = At(u, 0.1f), Rotation = rot,
                        Scale = new Vector3(1f, LevelMetrics.WallHeight - 0.2f, 1f),
                    });
                }
            }
            if (g.Wall != null) PlanWallFinish(list, f, u0, u1, holes);
        }

        /// <summary>Along-wall spans of the windows on this face (only outer walls have windows).</summary>
        static List<(float, float)> WindowHoles(WallFace f, LevelShell shell)
        {
            var holes = new List<(float, float)>();
            if (!f.Exterior) return holes;
            var dir = f.Along;
            foreach (var (pos, _) in shell.Windows)
            {
                if (LevelLayout.FloorOf(pos.y - LevelMetrics.WindowCenterY) != f.Floor) continue;
                var d = new Vector3(pos.x - f.A.x, 0f, pos.z - f.A.z);
                float across = Vector3.Dot(d, f.Normal);
                if (Mathf.Abs(across) > 0.2f) continue;
                float u = Vector3.Dot(d, dir);
                if (u < -WindowHalfWidth || u > f.Length + WindowHalfWidth) continue;
                holes.Add((u - WindowHalfWidth, u + WindowHalfWidth));
            }
            holes.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            return holes;
        }

        static bool Inside(List<(float a, float b)> holes, float u, float margin)
        {
            foreach (var (a, b) in holes) if (u > a - margin && u < b + margin) return true;
            return false;
        }

        static void PlanWallFinish(List<DecorPiece> list, WallFace f, float u0, float u1, List<(float a, float b)> holes)
        {
            void Rect(float a, float b, float y0, float y1)
            {
                if (b - a < 0.02f || y1 - y0 < 0.02f) return;
                var c = f.At((a + b) / 2f) + f.Normal * 0.006f + Vector3.up * ((y0 + y1) / 2f);
                // the texture follows the world along the quad's own x axis, so neighbouring pieces line up
                var right = Vector3.Cross(Vector3.up, -f.Normal);
                float worldU = Vector3.Dot(c, right) - (b - a) / 2f;
                list.Add(new DecorPiece
                {
                    Kind = DecorKind.WallFinish, Position = c, Rotation = Quaternion.LookRotation(-f.Normal),
                    Scale = new Vector3(b - a, y1 - y0, 1f), Surface = new Vector4(b - a, y1 - y0, worldU, y0),
                });
            }
            float cur = u0;
            foreach (var (a, b) in holes)
            {
                float ha = Mathf.Max(a, u0), hb = Mathf.Min(b, u1);
                if (hb <= ha) continue;
                Rect(cur, ha, f.Y0, f.Y1);
                Rect(ha, hb, f.Y0, Mathf.Min(f.Y1, WindowBottom));
                Rect(ha, hb, Mathf.Max(f.Y0, WindowTop), f.Y1);
                cur = hb;
            }
            Rect(cur, u1, f.Y0, f.Y1);
        }

        // ------------------------------------------------------------------ doorways

        static void PlanOpening(List<DecorPiece> list, WallOpening o, StyleGeometry g)
        {
            if (o.Vent || o.Exterior || g.Doors == DoorTrim.None) return;
            var n = new Vector3(-o.Along.z, 0f, o.Along.x);
            foreach (var side in new[] { n, -n })
            {
                var at = o.Center + side * (T / 2f);
                var rot = Quaternion.LookRotation(side);
                var scale = new Vector3(o.Width / 1.6f, 1f, 1f); // the kit is modelled for a 1.6 m doorway
                switch (g.Doors)
                {
                    case DoorTrim.Casing:
                        list.Add(new DecorPiece { Kind = DecorKind.Casing, Position = at, Rotation = rot, Scale = scale });
                        break;
                    case DoorTrim.Arch:
                        list.Add(new DecorPiece { Kind = DecorKind.Arch, Position = at, Rotation = rot, Scale = scale });
                        goto case DoorTrim.Pilasters;
                    case DoorTrim.Pilasters:
                        foreach (float s in new[] { -1f, 1f })
                            list.Add(new DecorPiece
                            {
                                Kind = DecorKind.Pilaster, Position = at + o.Along * (s * (o.Width / 2f + 0.3f)), Rotation = rot, Scale = Vector3.one,
                            });
                        break;
                }
            }
        }

        // ------------------------------------------------------------------ ceilings

        static void PlanCeilings(List<DecorPiece> list, LevelLayout l, CeilingKind kind)
        {
            const float cell = LevelMetrics.Cell;
            float span = cell - T;
            for (int f = 0; f < l.Floors; f++)
                for (int x = 0; x < l.Width; x++)
                    for (int y = 0; y < l.Height; y++)
                    {
                        if (HasStairHole(l, f, x, y)) continue; // nothing may cross the opening to the floor above
                        var c = l.CellCenter(f, x, y) + Vector3.up * LevelMetrics.WallHeight;
                        var alongZ = Quaternion.LookRotation(Vector3.right);   // local X → world Z
                        var alongX = Quaternion.LookRotation(Vector3.forward); // local X → world X
                        switch (kind)
                        {
                            case CeilingKind.Beams:
                            case CeilingKind.IBeams:
                                var k = kind == CeilingKind.Beams ? DecorKind.Beam : DecorKind.IBeam;
                                foreach (float dx in BeamsAlongZ)
                                    list.Add(new DecorPiece { Kind = k, Position = c + Vector3.right * dx, Rotation = alongZ, Scale = new Vector3(span, 1f, 1f) });
                                break;
                            case CeilingKind.Coffers:
                                foreach (float dx in BeamsAlongZ)
                                    list.Add(new DecorPiece { Kind = DecorKind.Coffer, Position = c + Vector3.right * dx, Rotation = alongZ, Scale = new Vector3(span, 1f, 1f) });
                                foreach (float dz in BeamsAlongX)
                                    list.Add(new DecorPiece { Kind = DecorKind.Coffer, Position = c + Vector3.forward * dz, Rotation = alongX, Scale = new Vector3(span, 1f, 1f) });
                                break;
                            case CeilingKind.Trays:
                                var at = c + Vector3.forward * 1.6f;
                                list.Add(new DecorPiece { Kind = DecorKind.Tray, Position = at, Rotation = alongX, Scale = new Vector3(cell, 1f, 1f) });
                                for (float u = -cell / 2f + 1f; u < cell / 2f; u += 2f)
                                    list.Add(new DecorPiece { Kind = DecorKind.TrayHanger, Position = at + Vector3.right * u, Rotation = alongX, Scale = Vector3.one });
                                break;
                        }
                    }
        }

        /// <summary>
        /// Ceiling finish: the underside of every slab above a floor (so the stair holes stay open) and, on the top floor,
        /// the whole roof.
        /// </summary>
        static void PlanCeilingFinish(List<DecorPiece> list, LevelShell shell, LevelLayout l)
        {
            void Quad(Vector2 min, Vector2 max, float y)
            {
                var size = max - min;
                list.Add(new DecorPiece
                {
                    Kind = DecorKind.CeilingFinish,
                    Position = new Vector3((min.x + max.x) / 2f, y - 0.008f, (min.y + max.y) / 2f),
                    Rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward),
                    Scale = new Vector3(size.x, size.y, 1f),
                    Surface = new Vector4(size.x, size.y, min.x, min.y),
                });
            }
            foreach (var r in shell.Floors)
                if (r.Floor >= 1) Quad(r.Min, r.Max, r.Y - LevelMetrics.SlabThickness);
            Quad(Vector2.zero, new Vector2(l.Width * LevelMetrics.Cell, l.Height * LevelMetrics.Cell),
                 LevelLayout.FloorY(l.Floors - 1) + LevelMetrics.WallHeight);
        }

        /// <summary>The ceiling of this cell has the opening of a staircase going up.</summary>
        public static bool HasStairHole(LevelLayout l, int floor, int x, int y)
        {
            foreach (var s in l.Stairs) if (s.Floor == floor && s.Cell == new Vector2Int(x, y)) return true;
            return false;
        }
    }
}
