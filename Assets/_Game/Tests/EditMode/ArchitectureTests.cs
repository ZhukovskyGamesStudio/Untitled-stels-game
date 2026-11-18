using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The architecture a visual style adds to the house: where it goes and what it never covers.</summary>
    public class ArchitectureTests
    {
        static LevelShell s_Shell;
        static LevelLayout s_Layout;

        [OneTimeTearDown]
        public void TearDown()
        {
            ActiveLevel.Clear();
            s_Shell = null;
        }

        /// <summary>One shared house built once for the fixture (the planner only reads its shell).</summary>
        static LevelShell Shell(out LevelLayout layout)
        {
            if (s_Shell == null || ActiveLevel.Layout != s_Layout)
            {
                s_Layout = TestHouses.Get(99991, 4);
                ActiveLevel.Load(s_Layout, AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset"));
                s_Shell = ActiveLevel.Shell;
            }
            layout = s_Layout;
            return s_Shell;
        }

        static StyleGeometry Everything(CeilingKind ceiling) => new()
        {
            Baseboard = true, Crown = true, RailHeight = 0.9f, Pipes = true, StripSpacing = 2f, Doors = DoorTrim.Arch, Ceiling = ceiling,
            Floor = new Material(Shader.Find("Universal Render Pipeline/Lit")), Wall = new Material(Shader.Find("Universal Render Pipeline/Lit")),
        };

        [Test]
        public void ThresholdsLieWhereTwoFloorsMeetInADoorway()
        {
            var shell = Shell(out var layout);
            Material Lit() => new(Shader.Find("Universal Render Pipeline/Lit"));
            var g = new StyleGeometry { Floor = Lit() };
            Assert.IsFalse(ArchitecturePlanner.Plan(shell, layout, g).Any(p => p.Threshold), "one floor everywhere: no thresholds");
            var type = layout.Rooms.First(r => r.Type is not (RoomType.Corridor or RoomType.Hall or RoomType.Lobby)).Type;
            g.RoomFloors = new[] { new RoomFloor { Room = type, Material = Lit(), Tile = 1f } };
            var strips = ArchitecturePlanner.Plan(shell, layout, g).Where(p => p.Threshold).ToList();
            Assert.IsNotEmpty(strips, $"the {type} rooms' doorways get thresholds");
            foreach (var s in strips)
            {
                var o = shell.Openings.OrderBy(x => (x.Center - s.Position).sqrMagnitude).First();
                Assert.Less(Vector3.Distance(o.Center + Vector3.up * ArchitecturePlanner.ThresholdLift, s.Position), 0.01f, "in a doorway");
                Assert.AreEqual(o.Width, s.Scale.y, 1e-4f, "as wide as the opening");
                var n = new Vector3(-o.Along.z, 0f, o.Along.x);
                var a = layout.Plan.RoomAt(o.Center + n * 0.6f + Vector3.up * 0.5f);
                var b = layout.Plan.RoomAt(o.Center - n * 0.6f + Vector3.up * 0.5f);
                Assert.IsTrue((a.Type == type) != (b.Type == type), "between the room with its own floor and another");
            }
        }

        [Test]
        public void OuterWallsRunUpToTheNextFloor()
        {
            var shell = Shell(out _);
            // the slab's edge between the floors stays behind the facade: no dark band outside, one plane
            var tall = shell.Walls.Where(f => f.Exterior && f.Y0 < 0.01f && f.Y1 > 2.5f).ToList();
            Assert.IsNotEmpty(tall);
            Assert.IsTrue(tall.All(f => f.Y1 >= LevelMetrics.FloorHeight - 0.01f), "an outer wall stops under the slab");
        }

        [Test]
        public void ARoomTypeGetsItsOwnTilesPaintAndCeiling()
        {
            var shell = Shell(out var layout);
            Material Lit() => new(Shader.Find("Universal Render Pipeline/Lit"));
            var type = layout.Rooms.First(r => r.Type is not (RoomType.Corridor or RoomType.Hall or RoomType.Lobby) && r.Levels == 1).Type;
            var g = Everything(CeilingKind.Trays);
            g.WainscotHeight = 1f;
            g.RoomLooks = new[] { new RoomLook { Room = type, Wall = Lit(), WallTile = 2f, Lower = Lit(), LowerTile = 1f, LowerHeight = 1.6f, Plain = true, Ceiling = Lit(), CeilingTile = 1.2f } };
            var plan = ArchitecturePlanner.Plan(shell, layout, g);
            PlanRoom At(Vector3 p) => layout.Plan.RoomAt(p);
            // probed just over the piece's bottom edge (a piece's middle near the ceiling reads as the floor above)
            bool Ours(DecorPiece p, Vector3 into) => At(p.Position + into * 0.3f + Vector3.up * (0.1f - (p.IsQuad ? p.Scale.y / 2f : 0f)))?.Type == type;
            var finishes = plan.Where(p => p.Kind == DecorKind.WallFinish && Ours(p, -(p.Rotation * Vector3.forward))).ToList();
            var lower = finishes.Where(p => p.Variant == 2).ToList();
            var upper = finishes.Where(p => p.Variant == 1).ToList();
            Assert.IsNotEmpty(lower, "tiles at the bottom");
            Assert.IsNotEmpty(upper, "the room's paint above");
            // (the reveals inside the doorways keep the style's wall: they belong to the opening, both rooms see them)
            bool InOpening(DecorPiece p) => shell.Openings.Any(o => Mathf.Abs(o.Center.y - LevelLayout.FloorY(LevelLayout.FloorOf(p.Position.y - p.Scale.y / 2f + 0.1f))) < 0.5f
                && Vector2.Distance(new Vector2(o.Center.x, o.Center.z), new Vector2(p.Position.x, p.Position.z)) < o.Width / 2f + 0.3f);
            var left = finishes.Where(p => p.Variant == 0 && !InOpening(p)).ToList();
            Assert.IsEmpty(left, $"no style wallpaper left in the room: {string.Join(", ", left.Take(3).Select(p => p.Position))}");
            Assert.IsTrue(lower.All(p => p.Position.y + p.Scale.y / 2f - LevelLayout.FloorY(LevelLayout.FloorOf(p.Position.y - p.Scale.y / 2f + 0.1f)) <= 1.6f + 1e-3f), "tiles up to their height");
            Assert.IsFalse(plan.Any(p => p.Kind == DecorKind.Wainscot && Ours(p, p.Rotation * Vector3.forward)), "plain walls: no panels");
            Assert.IsTrue(plan.Any(p => p.Kind == DecorKind.CeilingFinish && p.Variant == 1), "a ceiling of its own");
            // its doorways: each half of a reveal (across the wall) in the finish of the room on its side, the arch's spandrels too
            const float halfWall = LevelMetrics.WallThickness / 2f;
            var reveals = plan.Where(p => p.Kind == DecorKind.WallFinish && Mathf.Abs(p.Scale.x - halfWall) < 1e-3f).ToList();
            Assert.IsTrue(reveals.Any(p => p.Variant is 1 or 2), "the room's half of its doorways' reveals");
            Assert.IsTrue(reveals.Any(p => p.Variant == 0), "the neighbours' halves keep theirs");
            Assert.IsTrue(plan.Any(p => p.Kind == DecorKind.Arch && p.Variant == 1), "arches on the room's side in its paint");
            Assert.IsFalse(plan.Any(p => p.Kind is DecorKind.Tray or DecorKind.TrayHanger && At(p.Position - Vector3.up * 0.5f)?.Type == type), "no trays under it");
        }

        [Test]
        public void DecayAndPostersAreSeededStayOnTheirSurfacesAndSkipTheWindows()
        {
            var shell = Shell(out var layout);
            Material Lit() => new(Shader.Find("Universal Render Pipeline/Lit"));
            var g = new StyleGeometry
            {
                FloorDecay = new[] { Lit(), Lit() }, WallDecay = new[] { Lit() }, Posters = new[] { Lit(), Lit(), Lit() },
                DecayDensity = 0.1f, PosterChance = 0.6f,
            };
            var a = ArchitecturePlanner.Plan(shell, layout, g);
            var b = ArchitecturePlanner.Plan(shell, layout, g);
            Assert.AreEqual(a.Count, b.Count, "the same house gets the same decay");
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i].Position, b[i].Position);
            var floors = a.Where(p => p.Kind == DecorKind.FloorDecal).ToList();
            var walls = a.Where(p => p.Kind == DecorKind.WallDecal).ToList();
            var posters = a.Where(p => p.Kind == DecorKind.Poster).ToList();
            Assert.IsNotEmpty(floors);
            Assert.IsNotEmpty(walls);
            Assert.IsNotEmpty(posters);
            Assert.IsTrue(a.Where(p => p.Kind is DecorKind.FloorDecal or DecorKind.WallDecal).All(p => p.Variant >= 0 && p.Variant < 2));
            Assert.IsTrue(posters.All(p => p.Variant >= 0 && p.Variant < 3));
            foreach (var p in floors)
            {
                // the whole decal lies on one floor rectangle (never pokes into a wall or a stair hole)
                float r = p.Scale.x / 2f;
                Assert.IsTrue(shell.Floors.Any(f => Mathf.Abs(p.Position.y - f.Y) < 0.05f &&
                                                    p.Position.x - r >= f.Min.x - 0.01f && p.Position.x + r <= f.Max.x + 0.01f &&
                                                    p.Position.z - r >= f.Min.y - 0.01f && p.Position.z + r <= f.Max.y + 0.01f),
                    $"a floor decal at {p.Position} hangs off its floor");
            }
            foreach (var p in posters)
            {
                float y = p.Position.y - LevelLayout.FloorOf(p.Position.y) * LevelMetrics.FloorHeight;
                Assert.That(y, Is.InRange(1.3f, 1.8f), "posters hang at eye height");
            }
            foreach (var (pos, _) in shell.Windows)
                foreach (var p in walls.Concat(posters))
                {
                    var local = p.Position - pos;
                    if (Mathf.Abs(Vector3.Dot(local, p.Rotation * Vector3.forward)) > 0.2f) continue; // another wall
                    bool overlapX = Mathf.Abs(Vector3.Dot(local, p.Rotation * Vector3.right)) < p.Scale.x / 2f + LevelMetrics.WindowWidth / 2f - 0.01f;
                    bool overlapY = Mathf.Abs(local.y) < p.Scale.y / 2f + LevelMetrics.WindowHeight / 2f - 0.01f;
                    Assert.IsFalse(overlapX && overlapY, $"a {p.Kind} covers the window at {pos}");
                }
            Assert.IsFalse(ArchitecturePlanner.Plan(shell, layout, new StyleGeometry { FloorDecay = g.FloorDecay })
                .Any(p => p.Kind == DecorKind.FloorDecal), "no density, no decay");
        }

        [Test]
        public void TheVaultsRoundDoorGetsNoTrimAndNoReveals()
        {
            // its own steel ring and wall corners frame it: a style's casing, arch or reveal finish would clash with them
            LevelShell ShellWith(bool vault)
            {
                var shell = new LevelShell();
                shell.Openings.Add(new WallOpening { Center = Vector3.zero, Along = Vector3.right, Width = 2.4f, Height = LevelMetrics.DoorHeight, Vault = vault });
                return shell;
            }
            var g = Everything(CeilingKind.None);
            Assert.IsNotEmpty(ArchitecturePlanner.Plan(ShellWith(false), null, g), "an ordinary doorway is trimmed");
            Assert.IsEmpty(ArchitecturePlanner.Plan(ShellWith(true), null, g), "the vault's is not");
            Assert.AreEqual(ArchitecturePlanner.FrameReach, ArchitecturePlanner.ReachOf(ShellWith(true).Openings[0], g), "trims along the wall stop short of its ring");
        }

        [Test]
        public void TheShellRecordsWallsFloorsOpeningsAndWindows()
        {
            var shell = Shell(out var layout);
            Assert.IsNotEmpty(shell.Walls);
            Assert.IsNotEmpty(shell.Openings);
            Assert.AreEqual(LevelBuilder.WindowSpots(layout).Count, shell.Windows.Count);
            foreach (var w in shell.Walls.Where(w => w.Exterior))
            {
                // an outer wall's face looks into the building, never out into the gas
                // (a bent wall has a margin of pocket cells on its room side: look a little further too)
                var p = (w.A + w.B) / 2f + w.Normal * 0.4f;
                var q = (w.A + w.B) / 2f + w.Normal * 0.8f;
                bool inside = layout.Plan.SpaceAt(w.Floor, new Vector2(p.x, p.z)) >= 0 || layout.Plan.SpaceAt(w.Floor, new Vector2(q.x, q.z)) >= 0;
                Assert.IsTrue(inside, $"outer wall face at {p} looks outside");
            }
            float area = shell.Floors.Where(f => f.Floor == 0).Sum(f => (f.Max.x - f.Min.x) * (f.Max.y - f.Min.y));
            float rooms = layout.Rooms.Where(r => r.Floor == 0).Sum(r => r.Area);
            float shaft = layout.HasBasement ? 4f * Basement.ShaftHalf * Basement.ShaftHalf : 0f; // (the hole down to the basement)
            Assert.GreaterOrEqual(area, rooms - shaft - 0.5f, "the ground floor is covered");
        }

        [Test]
        public void TrimsNeverCrossADoorwayAndFinishesSkipTheWindows()
        {
            var shell = Shell(out var layout);
            var pieces = ArchitecturePlanner.Plan(shell, layout, Everything(CeilingKind.Trays));
            foreach (var o in shell.Openings.Where(o => !o.Vent))
            {
                // the doorway's clear volume (walking height) must stay free of baseboards, rails, panels and strips
                foreach (var p in pieces.Where(p => p.Kind is DecorKind.Baseboard or DecorKind.Rail or DecorKind.Wainscot or DecorKind.Strip))
                {
                    var d = p.Position - o.Center;
                    float along = Mathf.Abs(Vector3.Dot(d, o.Along));
                    float half = Mathf.Abs(Vector3.Dot(p.Rotation * Vector3.right, o.Along)) * p.Scale.x / 2f;
                    bool sameWall = Mathf.Abs(Vector3.Dot(d, Vector3.Cross(Vector3.up, o.Along))) < 0.2f && Mathf.Abs(d.y) < 0.5f;
                    if (sameWall) Assert.Greater(along + 0.001f, o.Width / 2f + half - 0.0001f, $"{p.Kind} at {p.Position} (facing {p.Rotation * Vector3.forward}, {p.Scale.x:0.00} long) runs across the doorway at {o.Center} along {o.Along}");
                }
            }
            foreach (var (pos, _) in shell.Windows)
                foreach (var p in pieces.Where(p => p.Kind == DecorKind.WallFinish))
                {
                    var right = p.Rotation * Vector3.right;
                    var local = p.Position - pos;
                    if (Mathf.Abs(Vector3.Dot(local, p.Rotation * Vector3.forward)) > 0.2f) continue; // another wall
                    bool overlapX = Mathf.Abs(Vector3.Dot(local, right)) < p.Scale.x / 2f + LevelMetrics.WindowWidth / 2f - 0.01f;
                    bool overlapY = Mathf.Abs(local.y) < p.Scale.y / 2f + LevelMetrics.WindowHeight / 2f - 0.01f;
                    Assert.IsFalse(overlapX && overlapY, $"a wall finish covers the window at {pos}");
                }
        }

        [TestCase(DoorTrim.Casing)]
        [TestCase(DoorTrim.Arch)]
        [TestCase(DoorTrim.Pilasters)]
        public void LowTrimsStopWhereTheDoorTrimStarts(DoorTrim trim)
        {
            var shell = Shell(out var layout);
            var g = new StyleGeometry { Baseboard = true, Crown = true, RailHeight = 0.9f, Doors = trim };
            var pieces = ArchitecturePlanner.Plan(shell, layout, g);
            var panels = ArchitecturePlanner.Plan(shell, layout, new StyleGeometry { WainscotHeight = 1f, Doors = trim });
            int checkedRuns = 0;
            foreach (var o in shell.Openings.Where(o => !o.Exterior))
                foreach (var p in pieces.Concat(panels).Where(p => p.Kind is DecorKind.Baseboard or DecorKind.Rail or DecorKind.Wainscot))
                {
                    var d = p.Position - o.Center;
                    bool sameWall = Mathf.Abs(Vector3.Dot(d, Vector3.Cross(Vector3.up, o.Along))) < 0.2f && Mathf.Abs(d.y) < 1.5f;
                    if (!sameWall) continue;
                    float along = Mathf.Abs(Vector3.Dot(d, o.Along));
                    float half = Mathf.Abs(Vector3.Dot(p.Rotation * Vector3.right, o.Along)) * p.Scale.x / 2f;
                    float reach = ArchitecturePlanner.ReachOf(o, g);
                    if (along - half > o.Width / 2f + reach + 1f) continue; // far along the wall
                    checkedRuns++;
                    Assert.Greater(along - half + 0.001f, o.Width / 2f + reach,
                        $"{p.Kind} at {p.Position} pokes into the {(o.Vent ? "vent's flange" : o.Squeeze ? "slot's frame" : trim.ToString())} of the opening at {o.Center}");
                }
            Assert.Greater(checkedRuns, 0, "runs next to doorways were checked");
            // vents and squeeze slots have frames of their own: no door trim on them
            foreach (var o in shell.Openings.Where(o => o.Vent || o.Squeeze))
                Assert.IsFalse(pieces.Any(p => p.Kind is DecorKind.Arch or DecorKind.Casing or DecorKind.Pilaster &&
                                               Vector3.Distance(p.Position, o.Center) < o.Width / 2f + 0.6f),
                    $"a {trim} on the {(o.Vent ? "vent" : "slot")} at {o.Center}");
            if (trim == DoorTrim.Casing) return;
            foreach (var p in pieces.Where(p => p.Kind == DecorKind.Pilaster))
                Assert.AreEqual(LevelMetrics.WallHeight - ArchitecturePlanner.CrownDrop, p.Scale.y * LevelMetrics.WallHeight, 1e-3f,
                    "a pilaster stops under the cornice, which runs on over its capital");
        }

        [Test]
        public void TheArchsSpandrelsContinueTheWallsFinishWithoutASeam()
        {
            var g = Everything(CeilingKind.None);
            Assert.IsTrue(StyleDecor.IsWallPart(DecorKind.Arch, "Body", g), "the spandrels are the wall");
            Assert.IsFalse(StyleDecor.IsWallPart(DecorKind.Arch, "Accent", g), "the moulding is trim");
            Assert.IsFalse(StyleDecor.IsWallPart(DecorKind.Pilaster, "Body", g));
            Assert.IsFalse(StyleDecor.IsWallPart(DecorKind.Arch, "Body", new StyleGeometry { Doors = DoorTrim.Arch }), "no finish: nothing to continue");
            // a point on a wall gets the texture coordinate the wall's finish quad gives it
            var shell = Shell(out var layout);
            g.WallTile = 1.7f;
            int n = 0;
            foreach (var p in ArchitecturePlanner.Plan(shell, layout, g).Where(p => p.Kind == DecorKind.WallFinish && Mathf.Abs((p.Rotation * Vector3.forward).y) < 0.1f).Take(400))
            {
                var right = p.Rotation * Vector3.right;
                var corner = p.Position - right * (p.Scale.x / 2f) - Vector3.up * (p.Scale.y / 2f); // the quad's uv (0, 0)
                bool reveal = Mathf.Abs(Mathf.Abs(p.Surface.x) - LevelMetrics.WallThickness) < 1e-3f && p.Scale.x < LevelMetrics.WallThickness + 1e-3f;
                // a face: its normal is the wall's; a reveal (across the wall's thickness): the wall runs along its normal
                var normal = -(p.Rotation * Vector3.forward);
                var wallRight = reveal ? normal : right;
                var wall = new WallProjection
                {
                    Right = wallRight, Normal = Vector3.Cross(wallRight, Vector3.up), Across = ArchitecturePlanner.Across(wallRight),
                    Tile = g.WallTile, FloorY = corner.y - p.Surface.w,
                };
                var uv = MeshBatcher.Project(wall, corner, normal);
                Assert.AreEqual(p.Surface.z / g.WallTile, uv.x, 1e-3f, reveal ? "across the wall, like the arch's soffit" : "along the wall");
                Assert.AreEqual(p.Surface.w / g.WallTile, uv.y, 1e-3f, "up the wall");
                n++;
            }
            Assert.Greater(n, 0);
        }

        [Test]
        public void CeilingPiecesAvoidStairHolesAndLamps()
        {
            var shell = Shell(out var layout);
            foreach (var kind in new[] { CeilingKind.Trays })
            {
                var pieces = ArchitecturePlanner.Plan(shell, layout, new StyleGeometry { Ceiling = kind });
                Assert.IsNotEmpty(pieces, kind.ToString());
                foreach (var p in pieces)
                {
                    int f = LevelLayout.FloorOf(p.Position.y - LevelMetrics.WallHeight);
                    var flat = new Vector2(p.Position.x, p.Position.z);
                    foreach (var s in layout.Stairs)
                        if (s.Floor == f) Assert.IsFalse(PlanGenerator.StairArea(s, 0f).Contains(flat), $"{p.Kind} over a stair hole at {flat} floor {f}");
                    if (p.Kind is DecorKind.TrayHanger) continue;
                    // the ceiling lamps of the room (1.24 x 1.3 m fixtures)
                    var room = layout.RoomAt(new Vector3(p.Position.x, LevelLayout.FloorY(f), p.Position.z));
                    if (room == null) continue;
                    var dir = p.Rotation * Vector3.right;
                    foreach (var spot in LevelBuilder.LightSpots(layout, room))
                    {
                        var lamp = new Vector3(spot.x, p.Position.y, spot.y);
                        if (Mathf.Abs(Vector3.Dot(lamp - p.Position, dir)) > p.Scale.x / 2f + 0.7f) continue; // beyond its ends
                        var d = new Vector3(lamp.x - p.Position.x, 0f, lamp.z - p.Position.z);
                        float across = (d - dir * Vector3.Dot(d, dir)).magnitude;
                        Assert.Greater(across, 0.35f, $"{p.Kind} runs into a ceiling lamp");
                    }
                }
            }
        }

        [Test]
        public void EveryKitPieceHasAPrefabAndEveryFamilyHasVariants()
        {
            var config = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles;
            foreach (ModelFamily f in System.Enum.GetValues(typeof(ModelFamily)))
            {
                var kit = config.KitFor(f);
                Assert.IsNotNull(kit, $"no style kit for {f}");
                Assert.AreEqual(f, kit.Family, $"{f} falls back to the kit of another family");
                foreach (DecorKind kind in System.Enum.GetValues(typeof(DecorKind)))
                {
                    if (kind is DecorKind.FloorFinish or DecorKind.WallFinish or DecorKind.CeilingFinish or
                        DecorKind.FloorDecal or DecorKind.WallDecal or DecorKind.Poster) continue; // quads, not kit pieces
                    Assert.IsNotNull(kit.Pieces[(int)kind], $"{f}: no prefab for {kind}");
                    foreach (var c in kit.Pieces[(int)kind].GetComponentsInChildren<Collider>(true)) Assert.Fail($"{f}/{kind} has a collider");
                }
                Assert.IsNotNull(kit.Finish);
            }
            Assert.GreaterOrEqual(config.Props.Count(p => p.Family == ModelFamily.Realistic && p.Prefab != null && p.Key != null), 12,
                "the realistic family swaps the furniture for scanned models");
            foreach (var p in config.Props)
            {
                Assert.Greater(p.Scale.x, 0.05f); Assert.Greater(p.Scale.y, 0.05f); Assert.Greater(p.Scale.z, 0.05f);
                foreach (var c in p.Prefab.GetComponentsInChildren<Collider>(true)) Assert.Fail($"{p.Prefab.name} has a collider");
            }
            // the robots are creatures of their own, the same in every style (VisualStyles.Invariant): only the spider has
            // its style versions (Tools/Blender/build_bestiary.py) — every part of it, or the original would show through
            string[] robots = { "robot_spider" };
            foreach (var m in config.StyleModels.Concat<object>(config.Meshes))
            {
                var from = m is StyleMeshes sm ? sm.From : ((MeshVariants)m).From;
                foreach (var mesh in from)
                    Assert.IsFalse(VisualStyles.Creature(System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(mesh))),
                        $"{AssetDatabase.GetAssetPath(mesh)}: a robot with a style version");
            }
            var originals = robots.SelectMany(name => AssetDatabase.FindAssets(name + " t:Model", new[] { "Assets/_Game/Art/Models" })
                    .Select(AssetDatabase.GUIDToAssetPath).Where(path => System.IO.Path.GetFileNameWithoutExtension(path) == name)
                    .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>()))
                .ToList();
            Assert.Greater(originals.Count, 4, "the spider's model is missing");
            foreach (var style in config.Styles.Where(s => s.Geometry.Family != ModelFamily.None))
            {
                var own = config.StyleModels.Where(m => m.Style == style && m.Variant < 0).SelectMany(m => m.From).ToHashSet();
                foreach (var mesh in originals)
                    Assert.IsTrue(own.Contains(mesh), $"{style.name}: no own version of {mesh.name} ({AssetDatabase.GetAssetPath(mesh)})");
            }
            // every style has the four departments of players, each with the six parts of the player model
            foreach (var style in config.Styles)
                for (int d = 0; d < 4; d++)
                    Assert.AreEqual(1, config.StyleModels.Count(m => m.Style == style && m.Variant == d && m.To.Length == 6), // body, two legs, accent, face, glasses
                        $"{style.name}: no player of department {d}");
            foreach (ModelFamily t in System.Enum.GetValues(typeof(ModelFamily)))
            {
                if (t == ModelFamily.None) continue;
                var mats = config.Materials.FirstOrDefault(m => m.Family == t);
                Assert.IsNotNull(mats, $"no {t} materials");
                Assert.Greater(mats.To.Length, 40, $"{t}: too few materials have a version");
                var set = config.Meshes.FirstOrDefault(m => m.Family == t);
                Assert.IsNotNull(set, $"no {t} meshes");
                Assert.Greater(set.From.Length, 120, $"{t}: too few meshes have a variant"); // (the robots have none: they look the same in every style)
                for (int i = 0; i < set.From.Length; i++)
                {
                    // same size, give or take the reshaping (a variant must not poke through walls or float)
                    var a = set.From[i].bounds;
                    var b = set.To[i].bounds;
                    Assert.Less((a.center - b.center).magnitude, 0.12f + a.extents.magnitude * 0.1f, $"{t}/{set.From[i].name} moved");
                    Assert.Less(Mathf.Abs(a.extents.magnitude - b.extents.magnitude), 0.1f + a.extents.magnitude * 0.15f, $"{t}/{set.From[i].name} resized");
                }
            }
        }
    
        [Test]
        public void RoomsWithAFloorOfTheirOwnGetItAndNothingElseDoes()
        {
            var shell = Shell(out var layout);
            var tiles = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var g = new StyleGeometry
            {
                Floor = new Material(Shader.Find("Universal Render Pipeline/Lit")),
                RoomFloors = layout.Rooms.Select(r => r.Type).Distinct().Where(t => t != RoomType.Lobby)
                    .Select(t => new RoomFloor { Room = t, Material = tiles, Tile = 1f }).Take(3).ToArray(),
            };
            var floors = ArchitecturePlanner.Plan(shell, layout, g).Where(p => p.Kind == DecorKind.FloorFinish && !p.Threshold).ToList();
            Assert.IsTrue(floors.Any(p => p.Variant == 0), "the style's floor elsewhere");
            Assert.IsTrue(floors.Any(p => p.Variant > 0), "rooms of their own");
            foreach (var p in floors)
            {
                // the middle of every quad lies in a room of the type its floor belongs to (or, for the style's, not of those types)
                var at = new Vector3(p.Position.x, p.Position.y + 0.3f, p.Position.z);
                var room = layout.Plan.RoomAt(at);
                if (room == null) continue; // a pocket or a landing
                int expect = g.RoomFloorOf(room.Type) + 1;
                Assert.AreEqual(expect, p.Variant, $"{room.Type} at {p.Position}");
            }
        }

        /// <summary>A slanted wall cuts its pocket's cells at an angle: their room-side halves carry the room's floor, not the style's.</summary>
        [Test]
        public void ThePocketBehindASlantedWallHasItsRoomsFloor()
        {
            LevelLayout layout = null;
            foreach (var l in TestHouses.Sample)
                if (l.Rooms.Any(r => r.Shaped.Count > 0 && r.Type != RoomType.Lobby)) { layout = l; break; }
            Assume.That(layout, Is.Not.Null, "a house with a slanted or round wall");
            var plan = layout.Plan;
            // the planner only needs the slabs here: one over each whole floor
            var shell = new LevelShell();
            for (int f = 0; f < layout.Floors; f++)
                shell.Floors.Add(new FloorRect { Min = Vector2.zero, Max = new Vector2(plan.NX, plan.NZ) * HousePlan.Res, Y = LevelLayout.FloorY(f), Floor = f });
            var tiles = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var g = new StyleGeometry
            {
                Floor = new Material(Shader.Find("Universal Render Pipeline/Lit")),
                RoomFloors = layout.Rooms.Where(r => r.Shaped.Count > 0 && r.Type != RoomType.Lobby).Select(r => r.Type).Distinct()
                    .Select(t => new RoomFloor { Room = t, Material = tiles, Tile = 1f }).ToArray(),
            };
            var floors = ArchitecturePlanner.Plan(shell, layout, g).Where(p => p.Kind == DecorKind.FloorFinish && !p.Threshold).ToList();
            int pockets = 0;
            for (int f = 0; f < layout.Floors; f++)
                for (int x = 0; x < plan.NX; x++)
                    for (int z = 0; z < plan.NZ; z++)
                    {
                        if (plan.KindAt(f, x, z) != CellKind.Pocket) continue;
                        int id = plan.IdAt(f, x, z);
                        if (id < 0 || id >= plan.Rooms.Count || g.RoomFloorOf(plan.Rooms[id].Type) < 0) continue;
                        pockets++;
                        var c = HousePlan.CellCenter(x, z);
                        var quad = floors.First(p => Mathf.Abs(p.Position.y - LevelLayout.FloorY(f)) < 0.1f &&
                                                     Mathf.Abs(p.Position.x - c.x) < p.Scale.x / 2f && Mathf.Abs(p.Position.z - c.y) < p.Scale.y / 2f);
                        Assert.AreEqual(g.RoomFloorOf(plan.Rooms[id].Type) + 1, quad.Variant, $"the pocket cell {x},{z} of the {plan.Rooms[id].Type}");
                    }
            Assert.Greater(pockets, 0);
        }
    }
}
