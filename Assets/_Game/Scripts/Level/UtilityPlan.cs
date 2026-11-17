using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>What a piece of the house's services is drawn with (an index into <see cref="LevelArt.UtilityMaterials"/>). Append only.</summary>
    public enum UtilitySlot { Duct, Grille, Pipe, Copper, Conduit, Plate, Dark, Panel, Warning }

    /// <summary>One piece of the services: a box, or a round pipe (an octagonal prism) along an axis.</summary>
    public struct UtilityPart
    {
        public Vector3 Center, Size;
        public UtilitySlot Slot;
        /// <summary>A pipe along <see cref="Axis"/> (0 x, 1 y, 2 z): its diameter is the size across.</summary>
        public bool Round;
        public int Axis;

        public static UtilityPart Box(Vector3 min, Vector3 max, UtilitySlot slot) =>
            new() { Center = (min + max) / 2f, Size = max - min, Slot = slot };

        public static UtilityPart Pipe(Vector3 a, Vector3 b, float radius, UtilitySlot slot)
        {
            var d = b - a;
            int axis = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (Mathf.Abs(d.x) > Mathf.Abs(d.z) ? 0 : 2) : (Mathf.Abs(d.y) > Mathf.Abs(d.z) ? 1 : 2);
            var size = Vector3.one * (2f * radius);
            size[axis] = Mathf.Abs(d[axis]);
            return new UtilityPart { Center = (a + b) / 2f, Size = size, Slot = slot, Round = true, Axis = axis };
        }
    }

    /// <summary>A light switch beside a doorway (<see cref="UtilityPlan.SwitchSpots"/>): its plate's middle on the wall, facing into its room.</summary>
    public struct SwitchSpot
    {
        public Vector3 Position, Facing;
        public int Room;
    }

    /// <summary>
    /// The house's services, laid out from its plan and its surfaces (pure, from the house's seed; looks only — no colliders,
    /// robots and players are not affected): a ventilation duct under the ceiling of every corridor with grilles on its
    /// side (split round the high vents it would block), an electrical panel on a corridor wall of every floor with its
    /// conduit to the ceiling and a cable tray along the wall, surface-mounted sockets behind the desks and on the walls of
    /// rooms without them, risers and a pipe along the wall in the restrooms, kitchens
    /// and locker rooms. The level builder merges the parts into one mesh per material. The light switches beside the
    /// doorways are real things (<see cref="SwitchSpots"/> → <see cref="LightSwitch"/>), not part of the mesh.
    /// </summary>
    public static class UtilityPlan
    {
        const float T = LevelMetrics.WallThickness;
        /// <summary>The duct's underside and top over the floor, metres: under the ceiling, over the doors.</summary>
        public const float DuctBottom = 2.36f, DuctTop = 2.64f;
        const float DuctWidth = 0.4f, DuctOff = 0.25f; // its inner side this far from the wall's face
        const float SocketY = 0.3f, SwitchY = 1.05f, PipeRunY = 2.6f;

        public static List<UtilityPart> Make(LevelLayout l, LevelShell shell)
        {
            var parts = new List<UtilityPart>();
            if (l == null || l.Plan == null || shell == null) return parts;
            var rng = new System.Random(LevelRandom.Mix(l.Seed, 0x51E7, 0));
            var faces = new List<(WallFace f, PlanRoom room)>();
            foreach (var f in shell.Walls)
            {
                if (f.Y0 > 0.01f || f.Y1 < LevelMetrics.WallHeight - 0.05f || f.Length < 0.6f) continue;
                var room = l.Plan.RoomAt(f.At(f.Length / 2f) + f.Normal * 0.3f + Vector3.up * 0.5f);
                if (room == null || room.Id == l.VaultRoom || room.Levels > 1) continue;
                faces.Add((f, room));
            }
            Ducts(parts, l, shell);
            Panels(parts, l, shell, faces);
            Sockets(parts, l, shell, faces, rng);
            Pipes(parts, l, shell, faces);
            Drains(parts, l);
            return parts;
        }

        /// <summary>A spot on a wall face (u along it, half a width either side) clear of its doorways, vents and windows.</summary>
        static bool Clear(WallFace f, float u, float half, LevelShell shell, float margin = 0.2f)
        {
            if (u - half < 0.15f || u + half > f.Length - 0.15f) return false;
            var p = f.At(u);
            foreach (var o in shell.Openings)
            {
                if (Mathf.Abs(o.Center.y - f.A.y) > 1f) continue;
                var d = new Vector2(o.Center.x - p.x, o.Center.z - p.z);
                if (d.magnitude < o.Width / 2f + half + margin + T) return false;
            }
            foreach (var (pos, _) in shell.Windows)
            {
                if (Mathf.Abs(pos.y - f.A.y - LevelMetrics.WindowCenterY) > 1f) continue;
                if (new Vector2(pos.x - p.x, pos.z - p.z).magnitude < LevelMetrics.WindowWidth / 2f + half + margin + T) return false;
            }
            return true;
        }

        /// <summary>A box standing out of a wall face: <paramref name="w"/> along it, <paramref name="h"/> tall, <paramref name="d"/> deep.</summary>
        static UtilityPart OnFace(WallFace f, float u, float y, float w, float h, float d, UtilitySlot slot, float lift = 0f)
        {
            var c = f.At(u) + f.Normal * (lift + d / 2f) + Vector3.up * (f.A.y + y + h / 2f - f.A.y);
            c.y = f.A.y + y + h / 2f;
            var along = f.Along;
            var size = new Vector3(Mathf.Abs(along.x) * w + Mathf.Abs(f.Normal.x) * d, h, Mathf.Abs(along.z) * w + Mathf.Abs(f.Normal.z) * d);
            return new UtilityPart { Center = c, Size = size, Slot = slot };
        }

        static bool Straight(WallFace f) => Mathf.Abs(f.Along.x) > 0.999f || Mathf.Abs(f.Along.z) > 0.999f;

        // ------------------------------------------------------------------ ducts

        static void Ducts(List<UtilityPart> parts, LevelLayout l, LevelShell shell)
        {
            foreach (var room in l.Rooms)
            {
                if (room.Type is not (RoomType.Corridor or RoomType.Hall) || room.Levels > 1) continue;
                float y0 = LevelLayout.FloorY(room.Floor);
                foreach (var cells in room.Rects)
                {
                    var r = new Rect(cells.xMin * HousePlan.Res, cells.yMin * HousePlan.Res, cells.width * HousePlan.Res, cells.height * HousePlan.Res);
                    bool alongX = r.width >= r.height;
                    float length = alongX ? r.width : r.height, across = alongX ? r.height : r.width;
                    if (length < 3f || across < 1.4f) continue;
                    // along the rect's long side on its "low" edge: x along, z = edge + wall half + gap + half the duct
                    float side = (alongX ? r.yMin : r.xMin) + T / 2f + DuctOff + DuctWidth / 2f;
                    float a = (alongX ? r.xMin : r.yMin) + 0.35f, b = (alongX ? r.xMax : r.yMax) - 0.35f;
                    var line = alongX ? Rect.MinMaxRect(a, side - DuctWidth / 2f, b, side + DuctWidth / 2f) : Rect.MinMaxRect(side - DuctWidth / 2f, a, side + DuctWidth / 2f, b);
                    bool stair = false;
                    foreach (var s in l.Stairs)
                        if ((s.Floor == room.Floor || s.Floor + 1 == room.Floor) && PlanGenerator.StairArea(s, 0.1f).Overlaps(line)) stair = true;
                    if (stair) continue; // no duct through a stairwell
                    // the high vents in the wall it runs along stay clear (a duct across a vent's top would block its look)
                    var cuts = new List<(float, float)>();
                    foreach (var o in shell.Openings)
                    {
                        if (!o.Vent || Mathf.Abs(o.Center.y - y0) > 0.5f) continue;
                        float edge = alongX ? r.yMin : r.xMin;
                        float off = alongX ? o.Center.z : o.Center.x, at = alongX ? o.Center.x : o.Center.z;
                        if (Mathf.Abs(off - edge) < 0.3f) cuts.Add((at - o.Width / 2f - 0.3f, at + o.Width / 2f + 0.3f));
                    }
                    cuts.Sort();
                    float from = a;
                    foreach (var (c0, c1) in cuts) { DuctRun(parts, alongX, side, from, Mathf.Min(b, c0), y0); from = Mathf.Max(from, c1); }
                    DuctRun(parts, alongX, side, from, b, y0);
                }
            }
        }

        static void DuctRun(List<UtilityPart> parts, bool alongX, float side, float a, float b, float y0)
        {
            if (b - a < 1f) return;
            Vector3 P(float along, float acrossOff, float y) => alongX ? new Vector3(along, y, side + acrossOff) : new Vector3(side + acrossOff, y, along);
            float hw = DuctWidth / 2f;
            var lo = P(a, -hw, y0 + DuctBottom);
            var hi = P(b, hw, y0 + DuctTop);
            parts.Add(UtilityPart.Box(Vector3.Min(lo, hi), Vector3.Max(lo, hi), UtilitySlot.Duct));
            // joint flanges every 1.2 m, grilles on the side towards the corridor's middle every 2.4 m, hangers to the ceiling
            for (float s = a; s <= b + 0.01f; s += 1.2f)
            {
                var f0 = P(s - 0.02f, -hw - 0.015f, y0 + DuctBottom - 0.015f);
                var f1 = P(s + 0.02f, hw + 0.015f, y0 + DuctTop + 0.015f);
                parts.Add(UtilityPart.Box(Vector3.Min(f0, f1), Vector3.Max(f0, f1), UtilitySlot.Duct));
                var h0 = P(s - 0.015f, -0.015f, y0 + DuctTop);
                var h1 = P(s + 0.015f, 0.015f, y0 + LevelMetrics.WallHeight);
                parts.Add(UtilityPart.Box(Vector3.Min(h0, h1), Vector3.Max(h0, h1), UtilitySlot.Conduit));
            }
            for (float s = a + 0.9f; s + 0.4f <= b; s += 2.4f)
            {
                var g0 = P(s, hw, y0 + DuctBottom + 0.07f);
                var g1 = P(s + 0.4f, hw + 0.012f, y0 + DuctTop - 0.07f);
                parts.Add(UtilityPart.Box(Vector3.Min(g0, g1), Vector3.Max(g0, g1), UtilitySlot.Grille));
            }
        }

        // ------------------------------------------------------------------ the electrical panels

        static void Panels(List<UtilityPart> parts, LevelLayout l, LevelShell shell, List<(WallFace f, PlanRoom room)> faces)
        {
            for (int floor = 0; floor < l.Floors; floor++)
            {
                (WallFace f, float u)? best = null;
                float bestLen = 0f;
                foreach (var (f, room) in faces)
                {
                    if (room.Floor != floor || room.Type is not (RoomType.Corridor or RoomType.Hall) || !Straight(f) || f.Length < 2f) continue;
                    float u = f.Length / 2f;
                    for (float du = 0f; du < f.Length / 2f && !Clear(f, u, 0.6f, shell); du += 0.25f)
                        u = Clear(f, f.Length / 2f + du, 0.6f, shell) ? f.Length / 2f + du : f.Length / 2f - du;
                    if (!Clear(f, u, 0.6f, shell) || f.Length <= bestLen) continue;
                    best = (f, u);
                    bestLen = f.Length;
                }
                if (best == null) continue;
                var (face, at) = best.Value;
                parts.Add(OnFace(face, at, 1.2f, 0.62f, 0.9f, 0.16f, UtilitySlot.Panel));                    // the box
                parts.Add(OnFace(face, at, 1.24f, 0.56f, 0.82f, 0.012f, UtilitySlot.Panel, 0.16f));         // its door
                parts.Add(OnFace(face, at + 0.18f, 1.62f, 0.03f, 0.12f, 0.02f, UtilitySlot.Dark, 0.172f));  // the handle
                parts.Add(OnFace(face, at - 0.12f, 1.82f, 0.13f, 0.12f, 0.004f, UtilitySlot.Warning, 0.172f)); // the warning sign
                // the conduit up to the ceiling, the cable tray along the wall under it
                var top = face.At(at) + face.Normal * 0.06f;
                parts.Add(UtilityPart.Pipe(new Vector3(top.x, face.A.y + 2.1f, top.z), new Vector3(top.x, face.A.y + LevelMetrics.WallHeight, top.z), 0.03f, UtilitySlot.Conduit));
                float t0 = 0.3f, t1 = face.Length - 0.3f;
                if (t1 - t0 > 0.6f) parts.Add(OnFace(face, (t0 + t1) / 2f, 2.66f, t1 - t0, 0.06f, 0.16f, UtilitySlot.Conduit, 0.02f));
            }
        }

        // ------------------------------------------------------------------ sockets and switches

        static void Sockets(List<UtilityPart> parts, LevelLayout l, LevelShell shell, List<(WallFace f, PlanRoom room)> faces, System.Random rng)
        {
            var served = new HashSet<int>();
            foreach (var p in l.Props)
            {
                if (p.Kind is not (PropKind.Desk or PropKind.Terminal)) continue;
                var fwd = Quaternion.Euler(0f, p.RotationY, 0f) * Vector3.forward;
                var back = p.Position - fwd * 0.45f;
                foreach (var (f, room) in faces)
                {
                    if (Vector3.Dot(f.Normal, fwd) < 0.9f || Mathf.Abs(f.A.y - p.Position.y) > 0.5f) continue;
                    var a = f.At(0f);
                    float u = Vector3.Dot(back - a, f.Along), off = Vector3.Dot(back - a, f.Normal);
                    if (off < -0.05f || off > 0.5f || !Clear(f, u, 0.1f, shell, 0.05f)) continue;
                    Socket(parts, f, u);
                    served.Add(room.Id);
                    break;
                }
            }
            // rooms with no desk: a socket or two low on a wall
            foreach (var room in l.Rooms)
            {
                if (served.Contains(room.Id) || room.Id == l.VaultRoom || room.Type is RoomType.Corridor) continue;
                int want = room.Area > 25f ? 2 : 1;
                foreach (var (f, r) in faces)
                {
                    if (want == 0) break;
                    if (r.Id != room.Id || f.Length < 1.2f) continue;
                    float u = 0.4f + (float)rng.NextDouble() * (f.Length - 0.8f);
                    if (!Clear(f, u, 0.1f, shell)) continue;
                    Socket(parts, f, u);
                    want--;
                }
            }
        }

        /// <summary>A surface-mounted double socket: a white box, its two outlets dark.</summary>
        static void Socket(List<UtilityPart> parts, WallFace f, float u)
        {
            parts.Add(OnFace(f, u, SocketY, 0.16f, 0.085f, 0.04f, UtilitySlot.Plate));
            foreach (float du in new[] { -0.04f, 0.04f })
                parts.Add(OnFace(f, u + du, SocketY + 0.025f, 0.035f, 0.035f, 0.004f, UtilitySlot.Dark, 0.04f));
        }

        /// <summary>
        /// Where the light switches go (pure): on the wall beside every doorway, inside each room it opens into (not the
        /// corridors, not the vault), clear of the other openings. The same on every peer: the index in the list is the switch.
        /// </summary>
        public static List<SwitchSpot> SwitchSpots(LevelLayout l, LevelShell shell)
        {
            var list = new List<SwitchSpot>();
            if (l == null || l.Plan == null || shell == null) return list;
            foreach (var o in shell.Openings)
            {
                if (o.Vent || o.Squeeze || o.Vault) continue;
                var n = new Vector3(-o.Along.z, 0f, o.Along.x);
                foreach (float s in new[] { 1f, -1f })
                {
                    var side = n * s;
                    var room = l.Plan.RoomAt(o.Center + side * 0.6f + Vector3.up * 0.5f);
                    if (room == null || room.Id == l.VaultRoom || room.Type is RoomType.Corridor) continue;
                    // on the wall beside the doorway, inside the room: the frame takes ~0.17 m of wall beside the opening
                    var at = o.Center + o.Along * (o.Width / 2f + 0.28f) + side * (T / 2f);
                    if (l.Plan.RoomAt(at + side * 0.3f + Vector3.up * 0.5f) != room) continue;
                    bool free = true;
                    foreach (var other in shell.Openings)
                        if (!other.Equals(o) && Mathf.Abs(other.Center.y - o.Center.y) < 0.5f
                            && new Vector2(other.Center.x - at.x, other.Center.z - at.z).magnitude < other.Width / 2f + 0.25f) free = false;
                    if (!free) continue;
                    at.y = o.Center.y + SwitchY + 0.06f;
                    list.Add(new SwitchSpot { Position = at, Facing = side, Room = room.Id });
                }
            }
            return list;
        }

        // ------------------------------------------------------------------ the drains at the hatches

        /// <summary>
        /// Where the building meets the sewer: at each hatch the drain stack comes down the corner from the ceiling into the
        /// floor beside the hatch (a cast-iron pipe with its cleanout plug at knee height) and a branch runs low along the
        /// wall into it — the hatch is the drain's inspection chamber, not a hole in the middle of a room.
        /// </summary>
        static void Drains(List<UtilityPart> parts, LevelLayout l)
        {
            foreach (var hatch in new[] { l.HatchPosition, l.ExitHatchPosition })
            {
                var room = l.Plan.RoomAt(hatch + Vector3.up * 0.3f);
                if (room == null) continue;
                // the corner the hatch sits in: the nearer wall line on each axis
                var b = room.Bounds;
                float sx = hatch.x - b.xMin < b.xMax - hatch.x ? -1f : 1f, sz = hatch.z - b.yMin < b.yMax - hatch.z ? -1f : 1f;
                float wx = sx < 0 ? b.xMin : b.xMax, wz = sz < 0 ? b.yMin : b.yMax;
                float y = LevelLayout.FloorY(room.Floor), top = y + LevelMetrics.WallHeight;
                var stack = new Vector3(wx - sx * (T / 2f + 0.09f), y, wz - sz * (T / 2f + 0.09f));
                if (Mathf.Abs(stack.x - hatch.x) > 1.6f || Mathf.Abs(stack.z - hatch.z) > 1.6f) continue; // not in a corner after all
                parts.Add(UtilityPart.Pipe(stack, new Vector3(stack.x, top, stack.z), 0.065f, UtilitySlot.Pipe));
                parts.Add(UtilityPart.Pipe(stack + Vector3.up * 0.42f, stack + Vector3.up * 0.58f, 0.085f, UtilitySlot.Dark));   // the cleanout
                parts.Add(UtilityPart.Pipe(stack + Vector3.up * 2.2f, stack + Vector3.up * 2.28f, 0.08f, UtilitySlot.Conduit));   // a clamp
                // the branch along the wall at ankle height, from the room's far side into the stack
                var along = new Vector3(stack.x - sx * 2.2f, y + 0.18f, stack.z);
                parts.Add(UtilityPart.Pipe(along, new Vector3(stack.x, y + 0.18f, stack.z), 0.045f, UtilitySlot.Pipe));
            }
        }

        // ------------------------------------------------------------------ pipes

        static void Pipes(List<UtilityPart> parts, LevelLayout l, LevelShell shell, List<(WallFace f, PlanRoom room)> faces)
        {
            foreach (var room in l.Rooms)
            {
                if (room.Type is not (RoomType.Restroom or RoomType.Kitchen or RoomType.LockerRoom or RoomType.Bathroom) || room.Levels > 1) continue;
                // a corner of two plain walls: the end of a face whose neighbour round the corner is clear too
                foreach (var (f, r) in faces)
                {
                    if (r.Id != room.Id || !Straight(f) || f.Length < 1.6f || !Clear(f, 0.45f, 0.3f, shell)) continue;
                    float y = f.A.y;
                    var corner = f.At(0.18f) + f.Normal * 0.06f;
                    var corner2 = f.At(0.32f) + f.Normal * 0.07f;
                    parts.Add(UtilityPart.Pipe(new Vector3(corner.x, y, corner.z), new Vector3(corner.x, y + LevelMetrics.WallHeight, corner.z), 0.045f, UtilitySlot.Pipe));
                    parts.Add(UtilityPart.Pipe(new Vector3(corner2.x, y, corner2.z), new Vector3(corner2.x, y + LevelMetrics.WallHeight, corner2.z), 0.028f, UtilitySlot.Copper));
                    // the run along the wall to its far end, clear of the doors under it
                    float end = Mathf.Min(f.Length - 0.3f, 4f);
                    var a = f.At(0.18f) + f.Normal * 0.06f;
                    var b = f.At(end) + f.Normal * 0.06f;
                    parts.Add(UtilityPart.Pipe(new Vector3(a.x, y + PipeRunY, a.z), new Vector3(b.x, y + PipeRunY, b.z), 0.04f, UtilitySlot.Pipe));
                    for (float u = 0.8f; u < end; u += 1.0f)
                        parts.Add(OnFace(f, u, PipeRunY - 0.06f, 0.04f, 0.12f, 0.1f, UtilitySlot.Conduit)); // clamps
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ the mesh

        /// <summary>Appends a part's faces (every side: things under the ceiling are seen from below) to shared lists, world-metre UVs.</summary>
        public static void AddFaces(UtilityPart p, List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int>[] tris)
        {
            var t = tris[(int)p.Slot];
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
            {
                // a → b → c → d clockwise seen from the front (along −n)
                int i = verts.Count;
                var right = Mathf.Abs(n.y) > 0.9f ? Vector3.right : Vector3.Cross(Vector3.up, -n);
                var up = Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up;
                foreach (var q in new[] { a, b, c, d })
                {
                    verts.Add(q);
                    normals.Add(n);
                    uvs.Add(new Vector2(Vector3.Dot(q, right), Vector3.Dot(q, up)));
                }
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
                t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }
            if (!p.Round)
            {
                var h = p.Size / 2f;
                foreach (var n in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
                {
                    // the face's own frame: n out, r to the viewer's right, u up the face
                    var u = Mathf.Abs(n.y) > 0.9f ? Vector3.forward * (n.y > 0 ? 1f : -1f) : Vector3.up;
                    var r = Vector3.Cross(n, u); // the viewer's right, looking at the face from outside
                    float hn = Mathf.Abs(Vector3.Dot(h, n)), hr = Mathf.Abs(Vector3.Dot(h, r)), hu = Mathf.Abs(Vector3.Dot(h, u));
                    var c = p.Center + n * hn;
                    Quad(c - r * hr - u * hu, c - r * hr + u * hu, c + r * hr + u * hu, c + r * hr - u * hu, n);
                }
                return;
            }
            // a pipe: eight sides round its axis and its two ends
            var axis = p.Axis == 0 ? Vector3.right : p.Axis == 1 ? Vector3.up : Vector3.forward;
            var e1 = p.Axis == 1 ? Vector3.right : Vector3.up;
            var e2 = Vector3.Cross(axis, e1);
            float radius = p.Size[(p.Axis + 1) % 3] / 2f, half = p.Size[p.Axis] / 2f;
            const int sides = 8;
            Vector3 Rim(int k, float s) => p.Center + axis * (s * half) + (e1 * Mathf.Cos(2f * Mathf.PI * k / sides) + e2 * Mathf.Sin(2f * Mathf.PI * k / sides)) * radius;
            for (int k = 0; k < sides; k++)
            {
                var n = (e1 * Mathf.Cos(2f * Mathf.PI * (k + 0.5f) / sides) + e2 * Mathf.Sin(2f * Mathf.PI * (k + 0.5f) / sides)).normalized;
                var a = Rim(k, -1f);
                var b = Rim(k + 1, -1f);
                var c = Rim(k + 1, 1f);
                var d = Rim(k, 1f);
                // wind them clockwise as seen from outside
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) > 0f) Quad(a, b, c, d, n);
                else Quad(a, d, c, b, n);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                var n = axis * s;
                for (int k = 0; k < sides; k++)
                {
                    int i = verts.Count;
                    var a = p.Center + n * half;
                    var b = Rim(k, s);
                    var c = Rim(k + 1, s);
                    foreach (var q in new[] { a, b, c }) { verts.Add(q); normals.Add(n); uvs.Add(Vector2.zero); }
                    if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) > 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); }
                    else { t.Add(i); t.Add(i + 2); t.Add(i + 1); }
                }
            }
        }
    }
}
