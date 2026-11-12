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
        /// <summary>Decals: a quad with one of the style's decay / poster materials (<see cref="DecorPiece.Variant"/>).</summary>
        FloorDecal, WallDecal, Poster,
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
        /// <summary>Decals: which material of the style's list.</summary>
        public int Variant;

        public bool IsQuad => Kind is DecorKind.FloorFinish or DecorKind.WallFinish or DecorKind.CeilingFinish or
                              DecorKind.FloorDecal or DecorKind.WallDecal or DecorKind.Poster;
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

        public static List<DecorPiece> Plan(LevelShell shell, LevelLayout layout, StyleGeometry g)
        {
            var list = new List<DecorPiece>();
            if (shell == null || g == null) return list;
            foreach (var face in shell.Walls) PlanFace(list, face, shell, g);
            foreach (var o in shell.Openings) PlanOpening(list, o, g);
            if (g.Wall != null)
                foreach (var o in shell.Openings) PlanReveals(list, o);
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
            if (layout != null) PlanDecay(list, shell, layout, g);
            return list;
        }

        // ------------------------------------------------------------------ decay and posters

        /// <summary>
        /// Big decals over the finishes so the tiled surfaces never read as a pattern: chunks on the floors, stains and
        /// leaks on the walls, posters at eye height; all from the house seed (every peer sees the same), never over windows.
        /// </summary>
        static void PlanDecay(List<DecorPiece> list, LevelShell shell, LevelLayout layout, StyleGeometry g)
        {
            int nf = g.FloorDecay?.Length ?? 0, nw = g.WallDecay?.Length ?? 0, np = g.Posters?.Length ?? 0;
            if ((g.DecayDensity <= 0f || nf + nw == 0) && (g.PosterChance <= 0f || np == 0)) return;
            var rng = new System.Random(LevelRandom.Mix(layout.Seed, 0x0DECA7, 0));
            int layer = 0;
            float Lift() => 0.002f * (layer++ % 6);              // overlapping decals never fight for the same depth

            if (nf > 0 && g.DecayDensity > 0f)
                foreach (var r in shell.Floors)
                {
                    var size = r.Max - r.Min;
                    int n = Count(rng, size.x * size.y * g.DecayDensity);
                    for (int i = 0; i < n; i++)
                    {
                        float s = Mathf.Min(rng.Range(1.6f, 4f), Mathf.Min(size.x, size.y) * 0.9f);
                        if (s < 0.5f) continue;
                        float x = rng.Range(r.Min.x + s * 0.5f, r.Max.x - s * 0.5f), z = rng.Range(r.Min.y + s * 0.5f, r.Max.y - s * 0.5f);
                        list.Add(new DecorPiece
                        {
                            Kind = DecorKind.FloorDecal, Variant = rng.Next(nf),
                            Position = new Vector3(x, r.Y + 0.014f + Lift(), z),
                            Rotation = Quaternion.AngleAxis(rng.Range(0f, 360f), Vector3.up) * Quaternion.LookRotation(Vector3.down, Vector3.forward),
                            Scale = new Vector3(s, s, 1f),
                        });
                    }
                }

            foreach (var f in shell.Walls)
            {
                if (f.Y0 > 0.001f || f.Length < 1.2f) continue;                // full-height faces only
                var holes = WindowHoles(f, shell);
                var rot = Quaternion.LookRotation(-f.Normal);
                if (nw > 0 && g.DecayDensity > 0f)
                {
                    int n = Count(rng, f.Length * g.DecayDensity * 0.6f);
                    for (int i = 0; i < n; i++)
                    {
                        int v = rng.Next(nw);
                        float aspect = Aspect(g.WallDecay[v]);
                        float s = Mathf.Min(rng.Range(0.9f, 2.2f), f.Length - 0.3f);
                        float h = Mathf.Min(s * aspect, LevelMetrics.WallHeight - 0.1f);
                        float u = rng.Range(s / 2f + 0.1f, f.Length - s / 2f - 0.1f);
                        if (Inside(holes, u, s / 2f)) continue;
                        // tall decals (leaks) run down from the ceiling, the rest sit low where dirt gathers
                        float yc = aspect > 1.4f ? LevelMetrics.WallHeight - 0.05f - h / 2f : rng.Range(h / 2f, Mathf.Max(h / 2f, 1.6f));
                        list.Add(new DecorPiece
                        {
                            Kind = DecorKind.WallDecal, Variant = v, Rotation = rot,
                            Position = f.At(u) + f.Normal * (0.014f + Lift()) + Vector3.up * yc, Scale = new Vector3(s, h, 1f),
                        });
                    }
                }
                if (np > 0 && g.PosterChance > 0f && rng.Value() < g.PosterChance * Mathf.Min(1.5f, f.Length / 4f) && f.Length > 1.2f)
                {
                    float u = rng.Range(0.6f, f.Length - 0.6f);
                    if (Inside(holes, u, 0.4f)) continue;
                    list.Add(new DecorPiece
                    {
                        Kind = DecorKind.Poster, Variant = rng.Next(np),
                        Rotation = rot * Quaternion.Euler(0f, 0f, rng.Range(-4f, 4f)),
                        Position = f.At(u) + f.Normal * 0.03f + Vector3.up * rng.Range(1.5f, 1.65f), Scale = new Vector3(0.75f, 1.06f, 1f),
                    });
                }
            }
        }

        /// <summary>Whole decals for an expected count (the fraction is a chance of one more).</summary>
        static int Count(System.Random rng, float expected)
        {
            int n = Mathf.FloorToInt(expected);
            return n + (rng.Value() < expected - n ? 1 : 0);
        }

        /// <summary>Height / width of a decal's texture (1 without one).</summary>
        static float Aspect(Material m)
        {
            var t = m != null ? m.mainTexture : null;
            return t != null && t.width > 0 ? (float)t.height / t.width : 1f;
        }

        // ------------------------------------------------------------------ walls

        static void PlanFace(List<DecorPiece> list, WallFace f, LevelShell shell, StyleGeometry g)
        {
            // Reach into the room's corners: past the corner post on that side; at the joint of a bent wall exactly to the joint.
            float u0 = f.CornerA ? -T : f.JointA ? -T / 2f : 0f, u1 = f.Length + (f.CornerB ? T : f.JointB ? T / 2f : 0f);
            float len = u1 - u0;
            if (len < 0.05f) return;
            var rot = Quaternion.LookRotation(f.Normal);
            Vector3 At(float u, float y) => f.At(u) + Vector3.up * y;
            bool fullHeight = f.Y0 <= 0.001f;
            // the piece under a vent is only knee-high: what does not fit under the hole stays off it
            bool toTop = f.Y1 >= LevelMetrics.WallHeight - 0.05f;
            var holes = fullHeight ? WindowHoles(f, shell) : new List<(float, float)>();

            void Run(DecorKind kind, float y)
            {
                list.Add(new DecorPiece { Kind = kind, Position = At((u0 + u1) / 2f, y), Rotation = rot, Scale = new Vector3(len, 1f, 1f) });
            }

            bool wainscot = fullHeight && g.WainscotHeight > 0f && f.Y1 >= g.WainscotHeight + 0.02f;
            if (fullHeight && g.Baseboard && !wainscot && f.Y1 >= 0.2f) Run(DecorKind.Baseboard, 0f);
            if (g.Crown && toTop) Run(DecorKind.Crown, LevelMetrics.WallHeight);
            if (fullHeight && g.RailHeight > 0f && f.Y1 >= g.RailHeight + 0.1f) Run(DecorKind.Rail, g.RailHeight);
            if (wainscot)
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
            if (g.Pipes && f.Y0 < 2.5f && toTop)
            {
                Run(DecorKind.Pipes, 0f);
                for (float u = u0 + 0.6f; u < u1 - 0.4f; u += 1.5f)
                    list.Add(new DecorPiece { Kind = DecorKind.PipeClamp, Position = At(u, 0f), Rotation = rot, Scale = Vector3.one });
            }
            if (fullHeight && toTop && g.StripSpacing > 0f)
            {
                int n = Mathf.FloorToInt((len - 0.8f) / g.StripSpacing);
                for (int i = 0; i <= n; i++)
                {
                    float u = (u0 + u1) / 2f + (i - n / 2f) * g.StripSpacing;
                    // clear of the face ends; an end at a doorway keeps the strip (half a metre each way) off the opening
                    if (u < u0 + (f.CornerA ? 0.4f : 0.6f) || u > u1 - (f.CornerB ? 0.4f : 0.6f) || Inside(holes, u, 0.1f)) continue;
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

        /// <summary>
        /// The wall's finish carried into a doorway: both reveals and the head, across the wall's thickness, so the opening
        /// reads as cut into the same wall instead of a bare block between two papered faces. Vents have their lining.
        /// </summary>
        static void PlanReveals(List<DecorPiece> list, WallOpening o)
        {
            if (o.Vent || o.Exterior) return;
            float w = o.Width, h = o.Height;
            const float lift = 0.006f;
            foreach (float s in new[] { -1f, 1f })
            {
                // the reveal at the +s end faces back into the opening (−s along the wall)
                var c = o.Center + o.Along * (s * (w / 2f - lift)) + Vector3.up * (h / 2f);
                list.Add(new DecorPiece
                {
                    Kind = DecorKind.WallFinish, Position = c, Rotation = Quaternion.LookRotation(o.Along * s),
                    Scale = new Vector3(T, h, 1f), Surface = new Vector4(T, h, 0f, 0f),
                });
            }
            list.Add(new DecorPiece
            {
                Kind = DecorKind.WallFinish, Position = o.Center + Vector3.up * (h - lift),
                Rotation = Quaternion.LookRotation(Vector3.up, o.Along), Scale = new Vector3(T, w, 1f), Surface = new Vector4(T, w, 0f, h),
            });
        }

        // ------------------------------------------------------------------ ceilings

        static void PlanCeilings(List<DecorPiece> list, LevelLayout l, CeilingKind kind)
        {
            var alongZ = Quaternion.LookRotation(Vector3.right);   // local X → world Z
            var alongX = Quaternion.LookRotation(Vector3.forward); // local X → world X
            foreach (var room in l.Rooms)
            {
                var lamps = LevelBuilder.LightSpots(l, room);
                // a beam along z at x (or along x at z) keeps clear of the lamps it passes; shifted aside or left out
                float? Dodge(float at, bool alongZ, Rect r)
                {
                    foreach (float shift in new[] { 0f, 1.25f, -1.25f })
                    {
                        float v = at + shift;
                        bool hit = false;
                        foreach (var p in lamps)
                        {
                            float across = alongZ ? p.x : p.y, along = alongZ ? p.y : p.x;
                            float lo = alongZ ? r.yMin : r.xMin, hi = alongZ ? r.yMax : r.xMax;
                            if (Mathf.Abs(across - v) < 1.0f && along > lo - 0.7f && along < hi + 0.7f) hit = true;
                        }
                        if (!hit && v > (alongZ ? r.xMin : r.yMin) + 0.5f && v < (alongZ ? r.xMax : r.yMax) - 0.5f) return v;
                    }
                    return null;
                }
                float ceilingY = LevelLayout.FloorY(room.Floor + room.Levels - 1) + LevelMetrics.WallHeight;
                if (kind is CeilingKind.Beams or CeilingKind.IBeams or CeilingKind.Coffers)
                {
                    // One system for the whole room: beams across its short side, each from wall to wall (broken only
                    // over a stair hole), so they never end in the middle of the ceiling or cross each other at odd heights.
                    var k = kind == CeilingKind.Beams ? DecorKind.Beam : kind == CeilingKind.IBeams ? DecorKind.IBeam : DecorKind.Coffer;
                    bool zBeams = room.Bounds.height <= room.Bounds.width;
                    BeamLines(list, l, room, lamps, k, zBeams, 3.75f, ceilingY);
                    if (kind == CeilingKind.Coffers) BeamLines(list, l, room, lamps, k, !zBeams, 5f, ceilingY);
                    continue;
                }
                foreach (var r in CeilingRects(l, room))
                {
                    float y = ceilingY;
                    var c = new Vector3(r.center.x, y, r.center.y);
                    float spanX = r.width - T;
                    switch (kind)
                    {
                        case CeilingKind.Trays:
                            if (r.height < 4f || Dodge(c.z + Mathf.Min(1.6f, r.height / 2f - 0.6f), false, r) is not float tz) break;
                            var at = new Vector3(c.x, y, tz);
                            list.Add(new DecorPiece { Kind = DecorKind.Tray, Position = at, Rotation = alongX, Scale = new Vector3(spanX, 1f, 1f) });
                            for (float u = -spanX / 2f + 1f; u < spanX / 2f; u += 2f)
                                list.Add(new DecorPiece { Kind = DecorKind.TrayHanger, Position = at + Vector3.right * u, Rotation = alongX, Scale = Vector3.one });
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Beams of one direction under a room's ceiling: lines every ~<paramref name="step"/> m across the room, each cut into
        /// the runs where the ceiling is the room's own (walls, pockets and stair holes end a run) and pulled back to the wall
        /// faces; a line that would hit a lamp moves aside, a run that still does is left out.
        /// </summary>
        static void BeamLines(List<DecorPiece> list, LevelLayout l, PlanRoom room, List<Vector2> lamps, DecorKind kind, bool alongZ, float step, float y)
        {
            var b = room.Bounds;
            float acrossMin = alongZ ? b.xMin : b.yMin, acrossMax = alongZ ? b.xMax : b.yMax;
            float lineMin = alongZ ? b.yMin : b.xMin, lineMax = alongZ ? b.yMax : b.xMax;
            var rot = alongZ ? Quaternion.LookRotation(Vector3.right) : Quaternion.LookRotation(Vector3.forward);
            const float res = HousePlan.Res;
            foreach (float offset in Spread(acrossMax - acrossMin, step))
            {
                float centre = (acrossMin + acrossMax) / 2f + offset;
                List<(float a, float b)> best = null;
                float bestV = centre;
                foreach (float shift in new[] { 0f, 1.25f, -1.25f, 0.6f, -0.6f })
                {
                    float v = centre + shift;
                    if (v < acrossMin + 0.5f || v > acrossMax - 0.5f) continue;
                    var runs = new List<(float a, float b)>();
                    float start = float.NaN;
                    for (float s = lineMin + res / 2f; s < lineMax + res; s += res)
                    {
                        var p = alongZ ? new Vector2(v, s) : new Vector2(s, v);
                        var cell = HousePlan.CellOf(p);
                        bool on = s < lineMax && CeilingCell(l, room, cell.x, cell.y);
                        if (on && float.IsNaN(start)) start = s - res / 2f;
                        if (on || float.IsNaN(start)) continue;
                        float a = start + T / 2f, e = s - res / 2f - T / 2f;
                        if (e - a >= 1.5f && !HitsLamp(lamps, v, a, e, alongZ)) runs.Add((a, e));
                        start = float.NaN;
                    }
                    if (best == null || Length(runs) > Length(best) + 0.01f) { best = runs; bestV = v; }
                    if (best.Count > 0 && shift == 0f && Length(best) >= (lineMax - lineMin) * 0.8f) break; // straight through: keep it centred
                }
                if (best == null) continue;
                foreach (var (a, e) in best)
                {
                    float mid = (a + e) / 2f;
                    var pos = alongZ ? new Vector3(bestV, y, mid) : new Vector3(mid, y, bestV);
                    list.Add(new DecorPiece { Kind = kind, Position = pos, Rotation = rot, Scale = new Vector3(e - a, 1f, 1f) });
                }
            }
        }

        static bool HitsLamp(List<Vector2> lamps, float v, float a, float e, bool alongZ)
        {
            foreach (var p in lamps)
            {
                float across = alongZ ? p.x : p.y, along = alongZ ? p.y : p.x;
                if (Mathf.Abs(across - v) < 1.0f && along > a - 0.7f && along < e + 0.7f) return true;
            }
            return false;
        }

        static float Length(List<(float a, float b)> runs)
        {
            float s = 0f;
            foreach (var (a, b) in runs) s += b - a;
            return s;
        }

        /// <summary>A cell of the room's floor whose ceiling is the room's own: not a pocket, not under a stair hole on any of its storeys.</summary>
        static bool CeilingCell(LevelLayout l, PlanRoom room, int cx, int cz)
        {
            var plan = l.Plan;
            if (plan.KindAt(room.Floor, cx, cz) != CellKind.Room || plan.IdAt(room.Floor, cx, cz) != room.Id) return false;
            var p = HousePlan.CellCenter(cx, cz);
            for (int f = room.Floor; f < room.Floor + room.Levels; f++)
                foreach (var s in l.Stairs)
                    if (s.Floor == f && PlanGenerator.StairArea(s, 0.3f).Contains(p)) return false;
            return true;
        }

        /// <summary>Offsets from a span's middle for parts every ~<paramref name="step"/> metres, none closer than 1 m to its ends.</summary>
        static IEnumerable<float> Spread(float span, float step)
        {
            int n = Mathf.FloorToInt((span - 2f) / step);
            for (int i = 0; i <= n; i++) yield return (i - n / 2f) * step;
        }

        /// <summary>
        /// The ceiling of a room as rectangles (pure): its floor's cells minus pockets and the holes of stairs on any of its
        /// storeys, merged; only rectangles big enough to carry beams (3 × 3 m).
        /// </summary>
        public static List<Rect> CeilingRects(LevelLayout l, PlanRoom room)
        {
            var plan = l.Plan;
            var list = new List<Rect>();
            int x0 = Mathf.FloorToInt(room.Bounds.xMin / HousePlan.Res), z0 = Mathf.FloorToInt(room.Bounds.yMin / HousePlan.Res);
            int nx = Mathf.RoundToInt(room.Bounds.width / HousePlan.Res), nz = Mathf.RoundToInt(room.Bounds.height / HousePlan.Res);
            bool Solid(int x, int z)
            {
                int cx = x0 + x, cz = z0 + z;
                if (plan.KindAt(room.Floor, cx, cz) != CellKind.Room || plan.IdAt(room.Floor, cx, cz) != room.Id) return false;
                var p = HousePlan.CellCenter(cx, cz);
                var p3 = new Vector3(p.x, 0f, p.y);
                for (int f = room.Floor; f < room.Floor + room.Levels; f++)
                    foreach (var s in l.Stairs)
                        if (s.Floor == f && PlanGenerator.StairArea(s, 0.3f).Contains(p)) return false;
                return true;
            }
            foreach (var r in LevelBuilder.Rectangles(nx, nz, Solid))
            {
                var rect = new Rect((x0 + r.x) * HousePlan.Res, (z0 + r.y) * HousePlan.Res, r.width * HousePlan.Res, r.height * HousePlan.Res);
                if (rect.width >= 3f && rect.height >= 3f) list.Add(rect);
            }
            return list;
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
            // under the roof: the top floor's rooms, pockets and atrium tops
            var plan = l.Plan;
            int top = l.Floors - 1;
            foreach (var r in LevelBuilder.Rectangles(plan.NX, plan.NZ, (x, z) => plan.KindAt(top, x, z) != CellKind.Outside))
                Quad(new Vector2(r.xMin, r.yMin) * HousePlan.Res, new Vector2(r.xMax, r.yMax) * HousePlan.Res, LevelLayout.FloorY(top) + LevelMetrics.WallHeight);
        }
    }
}
