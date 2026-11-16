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
        /// <summary>A finish of the bank vault: its steel cladding and floor plate (<see cref="StyleKit.VaultWall"/>), whatever the style.</summary>
        public bool Vault;
        /// <summary>A floor finish that is a door's threshold strip (<see cref="StyleKit.Threshold"/>).</summary>
        public bool Threshold;
        // (Variant of a wall finish: 0 the style's wall, 1 + 2k the k-th room look's wall, 2 + 2k its lower band; of a ceiling
        // finish: 0 the style's, k + 1 the k-th room look's ceiling)

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
            foreach (var face in shell.Walls)
                if (InVault(face, layout)) PlanVaultFace(list, face);
                else PlanFace(list, face, shell, g, LookOf(face, layout, g));
            foreach (var o in shell.Openings) PlanOpening(list, o, g, layout);
            if (g.Wall != null || (g.RoomLooks != null && g.RoomLooks.Length > 0))
                foreach (var o in shell.Openings) PlanReveals(list, o, g, layout);
            if (g.Ceiling != CeilingKind.None && layout != null) PlanCeilings(list, layout, g.Ceiling, g);
            if (g.CeilingFinish != null && layout != null) PlanCeilingFinish(list, shell, layout);
            if (layout != null) PlanRoomCeilings(list, layout, g);
            if (layout != null) PlanThresholds(list, shell, layout, g);
            // (rooms with a floor of their own get it even where the style keeps the bare slab: the shop's epoxy, ShopLook)
            if (layout != null && (layout.VaultRoom >= 0 || g.RoomFloors != null && g.RoomFloors.Length > 0)) PlanFloors(list, shell, layout, g);
            else if (g.Floor != null)
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

        /// <summary>
        /// The floor finish with rooms of their own (<see cref="StyleGeometry.RoomFloors"/>): every cell of the slabs
        /// takes the floor of the room on it (pockets and stair landings that of the style), merged into rectangles per
        /// floor material; <see cref="DecorPiece.Variant"/> = 0 the style's floor, k + 1 its k-th room floor.
        /// </summary>
        static void PlanFloors(List<DecorPiece> list, LevelShell shell, LevelLayout layout, StyleGeometry g)
        {
            const int vaultCell = -2;
            var plan = layout.Plan;
            for (int f = 0; f < layout.Floors; f++)
            {
                var slabs = new List<FloorRect>();
                foreach (var r in shell.Floors) if (r.Floor == f) slabs.Add(r);
                if (slabs.Count == 0) continue;
                float y = slabs[0].Y;
                var kind = new int[plan.NX, plan.NZ];
                var used = new HashSet<int>();
                for (int x = 0; x < plan.NX; x++)
                    for (int z = 0; z < plan.NZ; z++)
                    {
                        var c = HousePlan.CellCenter(x, z);
                        bool slab = false;
                        foreach (var r in slabs)
                            if (c.x > r.Min.x && c.x < r.Max.x && c.y > r.Min.y && c.y < r.Max.y) { slab = true; break; }
                        if (!slab) { kind[x, z] = -1; continue; }
                        // a pocket behind a slanted or round wall belongs to its room: the wall cuts its cells at an angle, and
                        // the half on the room's side must have the room's floor (the style's floor showed there as a saw edge)
                        int id = plan.KindAt(f, x, z) is CellKind.Room or CellKind.Pocket ? plan.IdAt(f, x, z) : -1;
                        int k = id >= 0 && id == layout.VaultRoom ? vaultCell
                            : id >= 0 && id < plan.Rooms.Count ? g.RoomFloorOf(plan.Rooms[id].Type) + 1 : 0;
                        kind[x, z] = k;
                        used.Add(k);
                    }
                foreach (int k in used)
                {
                    if (k == 0 && g.Floor == null) continue; // the style keeps the bare slab
                    foreach (var cells in LevelBuilder.Rectangles(plan.NX, plan.NZ, (x, z) => kind[x, z] == k))
                    {
                        float x0 = cells.xMin * HousePlan.Res, x1 = cells.xMax * HousePlan.Res, z0 = cells.yMin * HousePlan.Res, z1 = cells.yMax * HousePlan.Res;
                        list.Add(new DecorPiece
                        {
                            Kind = DecorKind.FloorFinish, Variant = k == vaultCell ? 0 : k, Vault = k == vaultCell,
                            Position = new Vector3((x0 + x1) / 2f, y + 0.008f, (z0 + z1) / 2f),
                            Rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward),
                            Scale = new Vector3(x1 - x0, z1 - z0, 1f),
                            Surface = new Vector4(x1 - x0, z1 - z0, x0, z0),
                        });
                    }
                }
            }
        }

        /// <summary>Which floor finish a room has in this style (−1 none, −2 the vault's plate, 0 the style's, k + 1 its k-th room floor).</summary>
        static int FloorKindOf(PlanRoom room, LevelLayout layout, StyleGeometry g)
        {
            if (room == null) return -1;
            if (room.Id == layout.VaultRoom) return -2;
            int k = g.RoomFloorOf(room.Type) + 1;
            return k == 0 && g.Floor == null ? -1 : k;
        }

        /// <summary>Thickness of a threshold strip over the floor finish, metres (nearly flat, like a real one).</summary>
        public const float ThresholdLift = 0.014f;

        /// <summary>
        /// Threshold strips: where a doorway joins two rooms with different floors (carpet to tiles, parquet to rubber), a
        /// flat metal strip over the joint, the opening's width, across the wall and a few centimetres into each room.
        /// </summary>
        static void PlanThresholds(List<DecorPiece> list, LevelShell shell, LevelLayout layout, StyleGeometry g)
        {
            if (layout.Plan == null) return;
            foreach (var o in shell.Openings)
            {
                if (o.Vent || o.Squeeze || o.Exterior || o.Vault) continue;
                var n = new Vector3(-o.Along.z, 0f, o.Along.x);
                var a = layout.Plan.RoomAt(o.Center + n * 0.6f + Vector3.up * 0.5f);
                var b = layout.Plan.RoomAt(o.Center - n * 0.6f + Vector3.up * 0.5f);
                if (a == null || b == null || a == b || FloorKindOf(a, layout, g) == FloorKindOf(b, layout, g)) continue;
                float w = o.Width, d = T + 0.1f;
                var c = o.Center + Vector3.up * ThresholdLift;
                list.Add(new DecorPiece
                {
                    Kind = DecorKind.FloorFinish, Threshold = true,
                    Position = c, Rotation = Quaternion.LookRotation(Vector3.down, o.Along),
                    Scale = new Vector3(d, w, 1f), Surface = new Vector4(d, w, 0f, 0f),
                });
            }
        }

        /// <summary>
        /// The room a face looks into, or null: probed in front of its middle, then of its quarters, ever deeper (behind a
        /// slanted wall the cells next to it are the sealed pocket, not the room).
        /// </summary>
        static PlanRoom RoomOf(WallFace f, LevelLayout layout)
        {
            if (layout == null || layout.Plan == null) return null;
            foreach (float depth in new[] { 0.3f, 0.7f, 1.2f })
                foreach (float t in new[] { 0.5f, 0.25f, 0.75f })
                {
                    var room = layout.Plan.RoomAt(f.At(f.Length * t) + f.Normal * depth + Vector3.up * 0.5f);
                    if (room != null) return room;
                }
            return null;
        }

        /// <summary>The style's own look (<see cref="StyleGeometry.RoomLooks"/>) for the room a face looks into, or −1.</summary>
        static int LookOf(WallFace f, LevelLayout layout, StyleGeometry g)
        {
            if (g.RoomLooks == null || g.RoomLooks.Length == 0) return -1;
            var room = RoomOf(f, layout);
            return room != null ? g.RoomLookOf(room.Type) : -1;
        }

        /// <summary>The look of a room type with a ceiling of its own, or −1.</summary>
        static int CeilingLookOf(PlanRoom room, StyleGeometry g)
        {
            int k = g != null ? g.RoomLookOf(room.Type) : -1;
            return k >= 0 && g.RoomLooks[k].Ceiling != null && room.Levels == 1 ? k : -1;
        }

        /// <summary>Ceilings of their own (<see cref="RoomLook.Ceiling"/>): a finish just under the style's, over the whole room.</summary>
        static void PlanRoomCeilings(List<DecorPiece> list, LevelLayout l, StyleGeometry g)
        {
            if (g.RoomLooks == null || g.RoomLooks.Length == 0) return;
            foreach (var room in l.Rooms)
            {
                int k = CeilingLookOf(room, g);
                if (k < 0) continue;
                float y = LevelLayout.FloorY(room.Floor + room.Levels - 1) + LevelMetrics.WallHeight - 0.014f; // (a hall's: on its top storey)
                foreach (var r in CeilingRects(l, room))
                    list.Add(new DecorPiece
                    {
                        Kind = DecorKind.CeilingFinish, Variant = k + 1,
                        Position = new Vector3(r.center.x, y, r.center.y),
                        Rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward),
                        Scale = new Vector3(r.width, r.height, 1f),
                        Surface = new Vector4(r.width, r.height, r.xMin, r.yMin),
                    });
            }
        }

        /// <summary>The face looks into the bank vault (<see cref="LevelLayout.VaultRoom"/>).</summary>
        static bool InVault(WallFace f, LevelLayout layout)
        {
            if (layout == null || layout.VaultRoom < 0 || layout.Plan == null) return false;
            return RoomOf(f, layout)?.Id == layout.VaultRoom;
        }

        /// <summary>
        /// A wall of the bank vault: steel cladding from floor to ceiling instead of the style's panels, trims and finish
        /// (real vaults are steel-lined boxes; the deposit boxes stand along them). No windows there.
        /// </summary>
        static void PlanVaultFace(List<DecorPiece> list, WallFace f)
        {
            float u0 = f.CornerA ? (f.StraightA ? -T / 2f : -T) : f.JointA ? -T / 2f : 0f;
            float u1 = f.Length + (f.CornerB ? (f.StraightB ? T / 2f : T) : f.JointB ? T / 2f : 0f);
            if (u1 - u0 < 0.05f) return;
            PlanWallFinish(list, f, u0, u1, new List<(float a, float b)>(), true);
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
                        if (layout.VaultRoom >= 0 && layout.Plan.RoomAt(new Vector3(x, r.Y + 0.3f, z))?.Id == layout.VaultRoom) continue;
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
                if (f.Y0 > 0.001f || f.Length < 1.2f || InVault(f, layout)) continue; // full-height faces only; the vault is clean steel
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

        static void PlanFace(List<DecorPiece> list, WallFace f, LevelShell shell, StyleGeometry g, int lookIndex = -1)
        {
            var look = lookIndex >= 0 ? g.RoomLooks[lookIndex] : default;
            bool plain = lookIndex >= 0 && look.Plain, tiled = lookIndex >= 0 && look.Lower != null;
            // Reach into the room's corners: past the corner post on that side; at the joint of a bent wall exactly to the joint.
            // A post in the middle of a straight wall is shared with the face that runs on behind it: half each.
            float u0 = f.CornerA ? (f.StraightA ? -T / 2f : -T) : f.JointA ? -T / 2f : 0f;
            float u1 = f.Length + (f.CornerB ? (f.StraightB ? T / 2f : T) : f.JointB ? T / 2f : 0f);
            float len = u1 - u0;
            if (len < 0.05f) return;
            var rot = Quaternion.LookRotation(f.Normal);
            Vector3 At(float u, float y) => f.At(u) + Vector3.up * y;
            bool fullHeight = f.Y0 <= 0.001f;
            // the piece under a vent is only knee-high: what does not fit under the hole stays off it
            bool toTop = f.Y1 >= LevelMetrics.WallHeight - 0.05f;
            var holes = fullHeight ? WindowHoles(f, shell) : new List<(float, float)>();
            // the low runs (baseboard, panels, chair rail) stop where a doorway's trim starts instead of poking through it
            var low = Around(u0, u1, TrimSpans(f, shell, g));

            void Run(DecorKind kind, float y, float a, float b)
            {
                list.Add(new DecorPiece { Kind = kind, Position = At((a + b) / 2f, y), Rotation = rot, Scale = new Vector3(b - a, 1f, 1f) });
            }

            // under a vent the panels must clear its bottom flange too
            bool wainscot = !plain && fullHeight && g.WainscotHeight > 0f && f.Y1 >= g.WainscotHeight + (toTop ? 0.02f : FrameReach);
            if (fullHeight && g.Baseboard && !wainscot && !tiled && f.Y1 >= 0.2f) foreach (var (a, b) in low) Run(DecorKind.Baseboard, 0f, a, b);
            if (g.Crown && toTop) Run(DecorKind.Crown, LevelMetrics.WallHeight, u0, u1);
            if (!plain && fullHeight && g.RailHeight > 0f && f.Y1 >= g.RailHeight + 0.1f) foreach (var (a, b) in low) Run(DecorKind.Rail, g.RailHeight, a, b);
            // tiles (padding) up to some height: a cap rail along their top edge
            if (tiled && fullHeight && f.Y1 >= look.LowerHeight + 0.1f) foreach (var (a, b) in low) Run(DecorKind.Rail, look.LowerHeight, a, b);
            if (wainscot)
                foreach (var (a, b) in low)
                {
                    int n = Mathf.Max(1, Mathf.RoundToInt((b - a) / 1.1f));
                    float w = (b - a) / n;
                    for (int i = 0; i < n; i++)
                        list.Add(new DecorPiece
                        {
                            Kind = DecorKind.Wainscot, Position = At(a + w * (i + 0.5f), 0f), Rotation = rot,
                            Scale = new Vector3(w, g.WainscotHeight, 1f),
                        });
                }
            if (g.Pipes && f.Y0 < 2.5f && toTop)
            {
                Run(DecorKind.Pipes, 0f, u0, u1);
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
            if (lookIndex < 0)
            {
                if (g.Wall != null) PlanWallFinish(list, f, u0, u1, holes);
                return;
            }
            // the room's own walls: the lower band, then the wall above it (the room's finish, or the style's)
            int upper = look.Wall != null ? 1 + 2 * lookIndex : g.Wall != null ? 0 : -1;
            float split = tiled ? look.LowerHeight : f.Y0;
            if (tiled && f.Y0 < split) PlanWallFinish(list, f, u0, u1, holes, false, f.Y0, Mathf.Min(f.Y1, split), 2 + 2 * lookIndex);
            if (upper >= 0 && f.Y1 > split) PlanWallFinish(list, f, u0, u1, holes, false, Mathf.Max(f.Y0, split), f.Y1, upper);
        }

        /// <summary>How far a doorway's trim reaches onto the wall past the opening's edge (the kit's casing, the pilasters).</summary>
        public static float TrimReach(DoorTrim t) => t switch
        {
            DoorTrim.Casing => 0.17f,                         // jamb 12 cm + the head's cornice
            DoorTrim.Arch or DoorTrim.Pilasters => PilasterOffset + PilasterHalfWidth + 0.01f,
            _ => 0f,
        };

        /// <summary>How far the frame of a vent (its flanges) or of a squeeze slot reaches onto the wall past the opening's edge.</summary>
        public const float FrameReach = 0.08f;

        /// <summary>How far the wall finish quads stand off the wall (no z-fighting with it).</summary>
        public const float FinishLift = 0.006f;

        /// <summary>A pilaster's centre from the opening's edge, and half its base's width (kit_pilaster).</summary>
        const float PilasterOffset = 0.3f, PilasterHalfWidth = 0.23f;
        /// <summary>The cornice hangs this far under the ceiling (kit_crown): pilasters stop under it.</summary>
        public const float CrownDrop = 0.23f;

        /// <summary>How far whatever frames an opening reaches onto its wall: a vent's flanges, a slot's frame, the style's door trim.</summary>
        public static float ReachOf(WallOpening o, StyleGeometry g) => o.Vent || o.Squeeze || o.Vault ? FrameReach : TrimReach(g.Doors);

        /// <summary>Along-wall spans of this face covered by the frames and trims of openings in the same wall (and its other face).</summary>
        static List<(float, float)> TrimSpans(WallFace f, LevelShell shell, StyleGeometry g)
        {
            var spans = new List<(float, float)>();
            var dir = f.Along;
            foreach (var o in shell.Openings)
            {
                float reach = ReachOf(o, g);
                if (reach <= 0f || o.Exterior || o.Floor != f.Floor || Mathf.Abs(Vector3.Dot(o.Along, dir)) < 0.99f) continue;
                var d = new Vector3(o.Center.x - f.A.x, 0f, o.Center.z - f.A.z);
                if (Mathf.Abs(Vector3.Dot(d, f.Normal)) > 0.05f) continue; // another wall
                float u = Vector3.Dot(d, dir), half = o.Width / 2f + reach;
                if (u + half < -T || u - half > f.Length + T) continue;
                spans.Add((u - half, u + half));
            }
            return spans;
        }

        /// <summary>The parts of [<paramref name="u0"/>, <paramref name="u1"/>] outside <paramref name="spans"/> (pieces under 5 cm dropped).</summary>
        public static List<(float a, float b)> Around(float u0, float u1, List<(float a, float b)> spans)
        {
            var parts = new List<(float, float)>();
            spans.Sort((x, y) => x.a.CompareTo(y.a));
            float cur = u0;
            foreach (var (a, b) in spans)
            {
                if (b <= cur) continue;
                if (a >= u1) break;
                if (a - cur >= 0.05f) parts.Add((cur, a));
                cur = Mathf.Max(cur, b);
            }
            if (u1 - cur >= 0.05f) parts.Add((cur, u1));
            return parts;
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

        static void PlanWallFinish(List<DecorPiece> list, WallFace f, float u0, float u1, List<(float a, float b)> holes, bool vault = false,
            float yLo = float.NaN, float yHi = float.NaN, int variant = 0)
        {
            if (float.IsNaN(yLo)) yLo = f.Y0;
            if (float.IsNaN(yHi)) yHi = f.Y1;
            void Rect(float a, float b, float y0, float y1)
            {
                if (b - a < 0.02f || y1 - y0 < 0.02f) return;
                var c = f.At((a + b) / 2f) + f.Normal * FinishLift + Vector3.up * ((y0 + y1) / 2f);
                // the texture follows the world along the quad's own x axis, so neighbouring pieces line up
                var right = Vector3.Cross(Vector3.up, -f.Normal);
                float worldU = Vector3.Dot(c, right) - (b - a) / 2f;
                list.Add(new DecorPiece
                {
                    Kind = DecorKind.WallFinish, Position = c, Rotation = Quaternion.LookRotation(-f.Normal), Vault = vault, Variant = variant,
                    Scale = new Vector3(b - a, y1 - y0, 1f), Surface = new Vector4(b - a, y1 - y0, worldU, y0),
                });
            }
            float cur = u0;
            foreach (var (a, b) in holes)
            {
                float ha = Mathf.Max(a, u0), hb = Mathf.Min(b, u1);
                if (hb <= ha) continue;
                Rect(cur, ha, yLo, yHi);
                Rect(ha, hb, yLo, Mathf.Min(yHi, WindowBottom));
                Rect(ha, hb, Mathf.Max(yLo, WindowTop), yHi);
                cur = hb;
            }
            Rect(cur, u1, yLo, yHi);
        }

        // ------------------------------------------------------------------ doorways

        /// <summary>
        /// The wall finish of the room on one side of a doorway (probed <paramref name="into"/> it): the variant of its wall
        /// (<see cref="DecorPiece.Variant"/>: 0 the style's, 1 + 2k a room look's, −1 none), of its lower band (−1 none) and
        /// the band's top.
        /// </summary>
        static (int upper, int lower, float split) SideFinish(WallOpening o, Vector3 into, StyleGeometry g, LevelLayout layout)
        {
            int k = -1;
            if (layout != null && layout.Plan != null && g.RoomLooks != null && g.RoomLooks.Length > 0)
            {
                var room = layout.Plan.RoomAt(o.Center + into * 0.5f + Vector3.up * 0.5f);
                if (room != null) k = g.RoomLookOf(room.Type);
            }
            if (k < 0) return (g.Wall != null ? 0 : -1, -1, 0f);
            var look = g.RoomLooks[k];
            int upper = look.Wall != null ? 1 + 2 * k : g.Wall != null ? 0 : -1;
            return look.Lower != null ? (upper, 2 + 2 * k, look.LowerHeight) : (upper, -1, 0f);
        }

        static void PlanOpening(List<DecorPiece> list, WallOpening o, StyleGeometry g, LevelLayout layout = null)
        {
            // a vent and a squeeze slot carry their own frames (Level/VentFrame, Level/SqueezeFrame): a door trim would clash with them
            if (o.Vent || o.Squeeze || o.Vault || o.Exterior || g.Doors == DoorTrim.None) return; // the vault: its ring
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
                        // its spandrels are wall: the finish of the room on this side (two rooms, two papers, each its own half)
                        list.Add(new DecorPiece { Kind = DecorKind.Arch, Position = at, Rotation = rot, Scale = scale, Variant = Mathf.Max(0, SideFinish(o, side, g, layout).upper) });
                        goto case DoorTrim.Pilasters;
                    case DoorTrim.Pilasters:
                        foreach (float s in new[] { -1f, 1f })
                            list.Add(new DecorPiece
                            {
                                // full height, or up to the cornice that then runs over its capital
                                Kind = DecorKind.Pilaster, Position = at + o.Along * (s * (o.Width / 2f + PilasterOffset)), Rotation = rot,
                                Scale = new Vector3(1f, g.Crown ? (LevelMetrics.WallHeight - CrownDrop) / LevelMetrics.WallHeight : 1f, 1f),
                            });
                        break;
                }
            }
        }

        /// <summary>The horizontal normal of a wall running along <paramref name="along"/>, the same whichever face it is seen from.</summary>
        public static Vector3 Across(Vector3 along)
        {
            var n = new Vector3(-along.z, 0f, along.x).normalized;
            return n.x < -1e-4f || (Mathf.Abs(n.x) <= 1e-4f && n.z < 0f) ? -n : n;
        }

        /// <summary>
        /// The wall's finish carried into a doorway: both reveals and the head, across the wall's thickness, so the opening
        /// reads as cut into the same wall instead of a bare block between two papered faces. Each half of the thickness
        /// takes the finish of the room on its side (with its band of tiles), so a doorway between a tiled restroom and a
        /// papered corridor meets in the middle of the wall like a real one. Vents have their lining.
        /// </summary>
        static void PlanReveals(List<DecorPiece> list, WallOpening o, StyleGeometry g, LevelLayout layout = null)
        {
            if (o.Vent || o.Exterior || o.Vault) return; // the vault's steel sleeve lines its bore
            float w = o.Width, h = o.Height;
            const float lift = FinishLift, half = T / 2f;
            var n = new Vector3(-o.Along.z, 0f, o.Along.x);
            // the texture runs across the wall by the world (Across), like an arch's soffit over it (MeshBatcher.Project)
            var across = Across(o.Along);
            foreach (var side in new[] { n, -n })
            {
                var (upper, lower, split) = SideFinish(o, side, g, layout);
                foreach (float s in new[] { -1f, 1f })
                {
                    // the reveal at the +s end faces back into the opening (−s along the wall); this side's half of it
                    var c = o.Center + o.Along * (s * (w / 2f - lift)) + side * (half / 2f);
                    float sign = Mathf.Sign(Vector3.Dot(Vector3.Cross(Vector3.up, o.Along * s), across));
                    void Band(int variant, float y0, float y1)
                    {
                        if (variant < 0 || y1 - y0 < 0.02f) return;
                        var at = c + Vector3.up * ((y0 + y1) / 2f);
                        list.Add(new DecorPiece
                        {
                            Kind = DecorKind.WallFinish, Position = at, Rotation = Quaternion.LookRotation(o.Along * s), Variant = variant,
                            Scale = new Vector3(half, y1 - y0, 1f), Surface = new Vector4(half * sign, y1 - y0, Vector3.Dot(at, across) - sign * half / 2f, y0),
                        });
                    }
                    if (lower >= 0) { Band(lower, 0f, Mathf.Min(split, h)); Band(upper, Mathf.Min(split, h), h); }
                    else Band(upper, 0f, h);
                }
                if (upper >= 0)
                    list.Add(new DecorPiece
                    {
                        Kind = DecorKind.WallFinish, Position = o.Center + side * (half / 2f) + Vector3.up * (h - lift), Variant = upper,
                        Rotation = Quaternion.LookRotation(Vector3.up, o.Along), Scale = new Vector3(half, w, 1f), Surface = new Vector4(half, w, 0f, h),
                    });
            }
        }

        // ------------------------------------------------------------------ ceilings

        static void PlanCeilings(List<DecorPiece> list, LevelLayout l, CeilingKind kind, StyleGeometry g = null)
        {
            var alongZ = Quaternion.LookRotation(Vector3.right);   // local X → world Z
            var alongX = Quaternion.LookRotation(Vector3.forward); // local X → world X
            foreach (var room in l.Rooms)
            {
                if (CeilingLookOf(room, g) >= 0) continue; // a ceiling of its own: nothing hangs under it
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
