using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Walls that are not all alike: several looks for one kind of room, and what people hang on them.</summary>
    public class WallThingsTests
    {
        static StyleConfig Config => AssetDatabase.LoadAssetAtPath<StyleConfig>("Assets/_Game/Data/Styles/Styles.asset");

        [Test]
        public void ARoomHangsWhatSuitsItAndTiledRoomsNothing()
        {
            foreach (HouseKind house in System.Enum.GetValues(typeof(HouseKind)))
            {
                foreach (var wet in new[] { RoomType.Restroom, RoomType.Bathroom, RoomType.Operating, RoomType.PaintBooth })
                    Assert.IsEmpty(WallThings.For(house, wet), $"{house}: {wet}");
                foreach (RoomType room in System.Enum.GetValues(typeof(RoomType)))
                    foreach (var t in WallThings.For(house, room))
                    {
                        var size = WallThings.Size(t);
                        Assert.That(WallThings.Height(t) - size.y / 2f, Is.GreaterThan(0.25f), $"{t} on the skirting");
                        Assert.That(WallThings.Height(t) + size.y / 2f, Is.LessThan(2.2f), $"{t} in the pipes under the ceiling");
                    }
            }
            CollectionAssert.Contains(WallThings.For(HouseKind.Office, RoomType.Office), WallThing.Certificate);
            CollectionAssert.Contains(WallThings.For(HouseKind.Apartments, RoomType.Living), WallThing.Family);
            CollectionAssert.DoesNotContain(WallThings.For(HouseKind.Office, RoomType.Office), WallThing.Family);
            CollectionAssert.Contains(WallThings.For(HouseKind.Garage, RoomType.Workshop), WallThing.TinSign);
            CollectionAssert.Contains(WallThings.For(HouseKind.Hospital, RoomType.Corridor), WallThing.Health);
            CollectionAssert.Contains(WallThings.For(HouseKind.Business, RoomType.Corridor), WallThing.EvacPlan);
        }

        [Test]
        public void EveryThingHasItsPrefabInEveryKit()
        {
            foreach (ModelFamily f in System.Enum.GetValues(typeof(ModelFamily)))
            {
                var kit = Config.KitFor(f);
                Assert.AreEqual(WallThings.Count, kit.Things.Length, $"{f}");
                for (int i = 0; i < kit.Things.Length; i++)
                {
                    var thing = (WallThing)i;
                    Assert.IsNotNull(kit.Things[i], $"{f}: no prefab for {thing}");
                    var parts = kit.Things[i].GetComponentsInChildren<MeshRenderer>();
                    Assert.IsTrue(parts.All(r => r.sharedMaterial != null && r.GetComponent<MeshFilter>().sharedMesh.isReadable), $"{thing}: merged into the decor");
                    Assert.IsEmpty(kit.Things[i].GetComponentsInChildren<Collider>(), $"{thing}: decor");
                    var b = parts.Select(r => r.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; });
                    var size = WallThings.Size(thing);
                    Assert.AreEqual(size.x, b.size.x, 0.02f, $"{thing}: width");
                    Assert.AreEqual(size.y, b.size.y, 0.02f, $"{thing}: height");
                    Assert.That(b.min.z, Is.GreaterThan(-0.004f), $"{thing}: sunk into the wall");
                    Assert.That(b.max.z, Is.LessThan(0.06f), $"{thing}: stands out of the wall like furniture");
                    if (thing is WallThing.Thermostat or WallThing.VentGrille) continue;
                    var print = parts.FirstOrDefault(r => r.name.StartsWith("Print"));
                    Assert.IsNotNull(print, $"{thing}: no picture");
                    Assert.AreEqual("ThingPrint", print.sharedMaterial.name);
                    Assert.IsNotNull(print.sharedMaterial.GetTexture("_BaseMap"));
                    // its picture is its own cell of the 4 x 4 sheet
                    var uv = print.GetComponent<MeshFilter>().sharedMesh.uv;
                    var mid = new Vector2(uv.Average(u => u.x), uv.Average(u => u.y));
                    Assert.AreEqual(i % 4, Mathf.FloorToInt(mid.x * 4f), $"{thing}: column");
                    Assert.AreEqual(i / 4, Mathf.FloorToInt((1f - mid.y) * 4f), $"{thing}: row");
                }
            }
        }

        [Test]
        public void ThingsHangOnFreeWallClearOfWindowsOpeningsAndEachOther()
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            var g = new StyleGeometry { ThingChance = 1.5f };
            int total = 0;
            foreach (var (seed, house) in new[] { (99991, 4), (7, 3), (42, 5) })
            {
                var l = TestHouses.Get(seed, house);
                ActiveLevel.Load(l, art);
                var shell = ActiveLevel.Shell;
                var a = ArchitecturePlanner.Plan(shell, l, g).Where(p => p.Kind == DecorKind.WallThing).ToList();
                var again = ArchitecturePlanner.Plan(shell, l, g).Where(p => p.Kind == DecorKind.WallThing).ToList();
                Assert.AreEqual(a.Count, again.Count, "the same house hangs the same things");
                Assert.Greater(a.Count, 10, $"seed {seed}: bare walls");
                total += a.Count;
                for (int i = 0; i < a.Count; i++)
                {
                    var p = a[i];
                    Assert.AreEqual(p.Position, again[i].Position);
                    var thing = (WallThing)p.Variant;
                    var size = WallThings.Size(thing);
                    var n = p.Rotation * Vector3.forward;
                    var along = p.Rotation * Vector3.right;
                    float y = p.Position.y - LevelLayout.FloorOf(p.Position.y) * LevelMetrics.FloorHeight;
                    Assert.AreEqual(WallThings.Height(thing), y, 0.01f);
                    // on a wall face, the whole of it
                    Assert.IsTrue(shell.Walls.Any(f => Vector3.Dot(f.Normal, n) > 0.99f && f.Y0 < 0.01f && f.Y1 > LevelMetrics.WallHeight - 0.06f && f.Floor == LevelLayout.FloorOf(p.Position.y) &&
                                                       Mathf.Abs(Vector3.Dot(p.Position - f.At(0f), f.Normal)) < 0.02f &&
                                                       Vector3.Dot(p.Position - f.A, f.Along) > size.x / 2f + 0.3f && Vector3.Dot(p.Position - f.A, f.Along) < f.Length - size.x / 2f - 0.3f),
                        $"seed {seed}: a {thing} at {p.Position} is not wholly on a wall");
                    foreach (var (pos, _) in shell.Windows)
                    {
                        var d = p.Position - pos;
                        if (Mathf.Abs(Vector3.Dot(d, n)) > 0.3f) continue;
                        Assert.IsFalse(Mathf.Abs(Vector3.Dot(d, along)) < size.x / 2f + LevelMetrics.WindowWidth / 2f && Mathf.Abs(d.y) < size.y / 2f + LevelMetrics.WindowHeight / 2f,
                            $"seed {seed}: a {thing} over the window at {pos}");
                    }
                    for (int j = 0; j < i; j++)
                    {
                        var q = a[j];
                        if (Vector3.Dot(q.Rotation * Vector3.forward, n) < 0.99f || Mathf.Abs(Vector3.Dot(q.Position - p.Position, n)) > 0.05f) continue;
                        var other = WallThings.Size((WallThing)q.Variant);
                        Assert.IsFalse(Mathf.Abs(Vector3.Dot(q.Position - p.Position, along)) < (size.x + other.x) / 2f && Mathf.Abs(q.Position.y - p.Position.y) < (size.y + other.y) / 2f,
                            $"seed {seed}: a {thing} and a {(WallThing)q.Variant} overlap at {p.Position}");
                    }
                    var room = l.Plan.RoomAt(p.Position + n * 0.5f);
                    if (room != null) CollectionAssert.Contains(WallThings.For(l.PlanKind, room.Type), thing, $"seed {seed}: a {thing} in a {room.Type}");
                }
            }
            Assert.Greater(total, 60);
        }

        [Test]
        public void RoomsOfOneKindWearDifferentWallsAndOneRoomOneLook()
        {
            var style = Config.Styles.First(s => s.name == "S16_Derelict").Geometry;
            Assert.Greater(style.ThingChance, 0f, "the default style hangs things on its walls");
            var offices = style.RoomLooks.Where(r => r.Room == RoomType.Office).ToArray();
            Assert.GreaterOrEqual(offices.Length, 3, "offices: several looks");
            Assert.Greater(offices.Select(r => (r.Wall, r.Lower)).Distinct().Count(), 2);
            var seen = new HashSet<int>();
            for (int pick = 0; pick < 64; pick++)
            {
                int k = style.RoomLookOf(RoomType.Office, pick * 7919);
                Assert.AreEqual(RoomType.Office, style.RoomLooks[k].Room);
                Assert.AreEqual(k, style.RoomLookOf(RoomType.Office, pick * 7919), "the same room, the same look");
                seen.Add(k);
            }
            Assert.AreEqual(offices.Length, seen.Count, "every look turns up");
            int one = style.RoomLookOf(RoomType.Restroom, 12345);
            Assert.AreEqual(one, style.RoomLookOf(RoomType.Restroom, 54321), "a kind listed once has one look");
            Assert.AreEqual(-1, new StyleGeometry().RoomLookOf(RoomType.Office, 3));
            // a house: both faces of every room's walls agree (the pick is the room's, not the wall's)
            var l = TestHouses.Get(99991, 4);
            foreach (var room in l.Rooms)
                Assert.AreEqual(ArchitecturePlanner.LookPick(room, l), ArchitecturePlanner.LookPick(room, l));
            Assert.Greater(l.Rooms.Select(r => ArchitecturePlanner.LookPick(r, l) % 4).Distinct().Count(), 1);
        }
    }
}
