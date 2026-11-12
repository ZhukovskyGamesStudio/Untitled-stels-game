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
        [TearDown]
        public void TearDown() => ActiveLevel.Clear();

        static LevelShell Shell(int seed, int level, out LevelLayout layout)
        {
            layout = LevelGenerator.Generate(seed, level);
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            ActiveLevel.Load(layout, art);
            return ActiveLevel.Shell;
        }

        static StyleGeometry Everything(CeilingKind ceiling) => new()
        {
            Baseboard = true, Crown = true, RailHeight = 0.9f, Pipes = true, StripSpacing = 2f, Doors = DoorTrim.Arch, Ceiling = ceiling,
            Floor = new Material(Shader.Find("Universal Render Pipeline/Lit")), Wall = new Material(Shader.Find("Universal Render Pipeline/Lit")),
        };

        [Test]
        public void DecayAndPostersAreSeededStayOnTheirSurfacesAndSkipTheWindows()
        {
            var shell = Shell(6, 3, out var layout);
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
        public void TheShellRecordsWallsFloorsOpeningsAndWindows()
        {
            var shell = Shell(3, 2, out var layout);
            Assert.IsNotEmpty(shell.Walls);
            Assert.IsNotEmpty(shell.Openings);
            Assert.AreEqual(LevelBuilder.WindowSpots(layout).Count, shell.Windows.Count);
            foreach (var w in shell.Walls.Where(w => w.Exterior))
            {
                // an outer wall's face looks into the building, never out into the gas
                var p = (w.A + w.B) / 2f + w.Normal * 0.4f;
                Assert.GreaterOrEqual(layout.Plan.SpaceAt(w.Floor, new Vector2(p.x, p.z)), 0, $"outer wall face at {p} looks outside");
            }
            float area = shell.Floors.Where(f => f.Floor == 0).Sum(f => (f.Max.x - f.Min.x) * (f.Max.y - f.Min.y));
            float rooms = layout.Rooms.Where(r => r.Floor == 0).Sum(r => r.Area);
            Assert.GreaterOrEqual(area, rooms - 0.5f, "the ground floor is covered");
        }

        [Test]
        public void TrimsNeverCrossADoorwayAndFinishesSkipTheWindows()
        {
            var shell = Shell(5, 3, out var layout);
            var pieces = ArchitecturePlanner.Plan(shell, layout, Everything(CeilingKind.Coffers));
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

        [Test]
        public void CeilingPiecesAvoidStairHolesAndLamps()
        {
            var shell = Shell(8, 4, out var layout);
            foreach (var kind in new[] { CeilingKind.Beams, CeilingKind.Coffers, CeilingKind.Trays })
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
            // every style but the base look has its own robots, devices and sensor modules (Tools/Blender/build_bestiary.py):
            // every part of every original model has its style version, or the original would show through
            string[] robots =
            {
                "robot_guard", "robot_hunter", "robot_hound", "robot_spider", "robot_listener", "robot_worker", "robot_sweeper",
                "robot_thumper", "robot_sorter", "robot_drone", "robot_turret", "robot_vacuum", "sensor_lidar", "sensor_pir",
                "sensor_plate", "security_camera", "laser_emitter", "attach_eye", "attach_mic", "attach_seismic", "attach_nose",
            };
            var originals = robots.SelectMany(name => AssetDatabase.FindAssets(name + " t:Model", new[] { "Assets/_Game/Art/Models" })
                    .Select(AssetDatabase.GUIDToAssetPath).Where(path => System.IO.Path.GetFileNameWithoutExtension(path) == name)
                    .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>()))
                .ToList();
            Assert.Greater(originals.Count, 90, "the original robot models are missing");
            foreach (var style in config.Styles.Where(s => s.Geometry.Family != ModelFamily.None))
            {
                var own = config.StyleModels.Where(m => m.Style == style && m.Variant < 0).SelectMany(m => m.From).ToHashSet();
                foreach (var mesh in originals)
                    Assert.IsTrue(own.Contains(mesh), $"{style.name}: no own version of {mesh.name} ({AssetDatabase.GetAssetPath(mesh)})");
            }
            // every style has the four departments of players, each with the four parts of the player model
            foreach (var style in config.Styles)
                for (int d = 0; d < 4; d++)
                    Assert.AreEqual(1, config.StyleModels.Count(m => m.Style == style && m.Variant == d && m.To.Length == 4),
                        $"{style.name}: no player of department {d}");
            foreach (ModelFamily t in System.Enum.GetValues(typeof(ModelFamily)))
            {
                if (t == ModelFamily.None) continue;
                var mats = config.Materials.FirstOrDefault(m => m.Family == t);
                Assert.IsNotNull(mats, $"no {t} materials");
                Assert.Greater(mats.To.Length, 40, $"{t}: too few materials have a version");
                var set = config.Meshes.FirstOrDefault(m => m.Family == t);
                Assert.IsNotNull(set, $"no {t} meshes");
                Assert.Greater(set.From.Length, 150, $"{t}: too few meshes have a variant");
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
    }
}
