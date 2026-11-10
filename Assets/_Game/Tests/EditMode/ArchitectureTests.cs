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
        public void TheShellRecordsWallsFloorsOpeningsAndWindows()
        {
            var shell = Shell(3, 2, out var layout);
            Assert.IsNotEmpty(shell.Walls);
            Assert.IsNotEmpty(shell.Openings);
            Assert.AreEqual(LevelBuilder.WindowSpots(layout.Width, layout.Height, 0f).Count * layout.Floors, shell.Windows.Count);
            Assert.IsTrue(shell.Walls.Where(w => w.Exterior).All(w =>
                Vector3.Dot(w.Normal, new Vector3(layout.Width * 5f, 0, layout.Height * 5f) - (w.A + w.B) / 2f) > 0f), "outer walls only face inwards");
            float area = shell.Floors.Where(f => f.Floor == 0).Sum(f => (f.Max.x - f.Min.x) * (f.Max.y - f.Min.y));
            Assert.AreEqual(layout.Width * layout.Height * LevelMetrics.Cell * LevelMetrics.Cell, area, 1f, "the ground floor is covered");
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
                    if (sameWall) Assert.Greater(along + 0.001f, o.Width / 2f + half - 0.0001f, $"{p.Kind} runs across the doorway at {o.Center}");
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
                    int x = Mathf.FloorToInt(p.Position.x / LevelMetrics.Cell), y = Mathf.FloorToInt(p.Position.z / LevelMetrics.Cell);
                    Assert.IsFalse(ArchitecturePlanner.HasStairHole(layout, f, x, y), $"{p.Kind} over the stair hole of {x},{y} floor {f}");
                    if (p.Kind is DecorKind.TrayHanger) continue;
                    // the two ceiling lamps of a cell hang at ±2.5 m (x) from its centre, 1.2 x 0.6 m
                    var c = layout.CellCenter(f, x, y);
                    var dir = p.Rotation * Vector3.right;
                    foreach (float s in new[] { -RoomLighting.LampOffset, RoomLighting.LampOffset })
                    {
                        var lamp = c + Vector3.right * s;
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
                    if (kind is DecorKind.FloorFinish or DecorKind.WallFinish or DecorKind.CeilingFinish) continue;
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
            // every style but the base look has its own guard robot, with all five parts of the original
            Assert.AreEqual(config.Styles.Length - 1, config.StyleModels.Count(m => m.To.Length == 5), "a style without its own guard");
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
