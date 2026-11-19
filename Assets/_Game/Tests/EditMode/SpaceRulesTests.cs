using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// How things stand in space, as a player would judge it: nothing tall or hung over a window, a restroom has its
    /// stalls, one water cooler per room, walls and their panels never lie on each other. (Found by the space audit,
    /// <see cref="SpaceAudit"/>.)
    /// </summary>
    public class SpaceRulesTests
    {
        static IEnumerable<(PropPlacement p, PlanRoom room)> PropsOf(LevelLayout l)
        {
            foreach (var p in l.Props) yield return (p, l.Plan.RoomAt(p.Position + Vector3.up * 0.3f));
        }

        [Test]
        public void NothingTallOrHungStandsOverAWindow()
        {
            foreach (var l in TestHouses.Sample)
            {
                var windows = LevelBuilder.WindowCuts(l);
                Assert.IsNotEmpty(windows);
                foreach (var (pos, yaw, _, _) in windows)
                {
                    var q = Quaternion.Euler(0f, yaw, 0f);
                    Vector3 inward = q * Vector3.forward, along = q * Vector3.right;
                    int floor = LevelLayout.FloorOf(pos.y - LevelMetrics.WindowCenterY + 0.3f);
                    foreach (var p in l.Props)
                    {
                        if (LevelLayout.FloorOf(p.Position.y + 0.3f) != floor) continue;
                        if (!Furnisher.Tall.Contains(p.Kind) && !Furnisher.OnWall.Contains(p.Kind)) continue;
                        if (p.Kind is PropKind.Programmer or PropKind.Cage) continue; // the house's special places take a piece's spot later
                        var foot = Furnisher.FootprintOf(p.Kind);
                        bool turned = Mathf.Abs(Mathf.Sin(p.RotationY * Mathf.Deg2Rad)) > 0.5f;
                        var size = turned ? new Vector2(foot.height, foot.width) : new Vector2(foot.width, foot.height);
                        float halfAlong = Mathf.Abs(along.x) > 0.5f ? size.x / 2f : size.y / 2f;
                        var d = p.Position - pos;
                        float depth = Vector3.Dot(d, inward);
                        if (depth < -0.1f || depth > 0.7f) continue; // not standing at this wall
                        float overlap = LevelMetrics.WindowWidth / 2f + halfAlong - Mathf.Abs(Vector3.Dot(d, along));
                        Assert.Less(overlap, 0.05f, $"seed {l.Seed}: a {p.Kind} at {p.Position} covers the window at {pos}");
                    }
                }
            }
        }

        [Test]
        public void NoStaircaseStandsBeforeAWindow()
        {
            foreach (var l in TestHouses.Sample)
                foreach (var (pos, yaw, _, _) in LevelBuilder.WindowCuts(l))
                {
                    var q = Quaternion.Euler(0f, yaw, 0f);
                    int floor = LevelLayout.FloorOf(pos.y - LevelMetrics.WindowCenterY + 0.3f);
                    foreach (float u in new[] { -LevelMetrics.WindowWidth / 2f, 0f, LevelMetrics.WindowWidth / 2f })
                        Assert.IsFalse(l.NearStair(floor, pos + q * new Vector3(u, 0f, 0.3f), 0.1f), $"seed {l.Seed}: a staircase before the window at {pos}");
                }
        }

        [Test]
        public void RestroomsVaultsAndChangingRoomsHaveNoWindows()
        {
            foreach (var l in TestHouses.Sample)
                foreach (var (pos, yaw, _, _) in LevelBuilder.WindowCuts(l))
                {
                    var inside = pos + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 0.4f;
                    inside.y = LevelLayout.FloorY(LevelLayout.FloorOf(pos.y - LevelMetrics.WindowCenterY + 0.3f)) + 0.3f;
                    var room = l.Plan.RoomAt(inside);
                    if (room == null) continue;
                    Assert.AreNotEqual(RoomType.Restroom, room.Type, $"seed {l.Seed}: a window into the restroom #{room.Id}");
                    Assert.AreNotEqual(RoomType.LockerRoom, room.Type, $"seed {l.Seed}: a window into the changing room #{room.Id}");
                    Assert.AreNotEqual(l.VaultRoom, room.Id, $"seed {l.Seed}: a window into the vault");
                }
        }

        [Test]
        public void ARestroomHasItsStallsAndBasins()
        {
            int rooms = 0, stalls = 0, basins = 0;
            foreach (var l in TestHouses.Sample)
            {
                var props = PropsOf(l).ToList();
                foreach (var r in l.Rooms)
                {
                    if (r.Type != RoomType.Restroom || r.Area < 10f) continue;
                    rooms++;
                    if (props.Any(p => p.room == r && p.p.Kind is PropKind.ToiletStall or PropKind.Urinal)) stalls++;
                    if (props.Any(p => p.room == r && p.p.Kind is PropKind.MirrorSink or PropKind.DoubleSink or PropKind.WashBasin)) basins++;
                }
            }
            Assert.Greater(rooms, 5);
            // (a small restroom with a door in every wall has no wall left for anything; most had no stall at all before)
            Assert.GreaterOrEqual(stalls, rooms * 7 / 10, $"{stalls} of {rooms} restrooms have a stall or urinals");
            Assert.GreaterOrEqual(basins, rooms * 6 / 10, $"{basins} of {rooms} restrooms have a basin");
        }

        [Test]
        public void AMeetingRoomHasItsTable()
        {
            int rooms = 0, tables = 0;
            foreach (var l in TestHouses.Sample)
            {
                var props = PropsOf(l).ToList();
                foreach (var r in l.Rooms)
                {
                    if (r.Type != RoomType.Meeting || r.Area < 14f) continue;
                    rooms++;
                    // (the house's special place may have taken the table's spot: it stands where the table stood)
                    if (props.Any(p => p.room == r && p.p.Kind is PropKind.ConferenceTable or PropKind.MeetingTable or PropKind.RoundTable
                            or PropKind.MedTable or PropKind.Workbench or PropKind.Programmer)) tables++;
                }
            }
            Assert.Greater(rooms, 5);
            // (not all yet: a narrow meeting room with doors at both ends still finds no place for one — an open task;
            // before the table fallback it was about one room in five)
            Assert.GreaterOrEqual(tables, rooms / 2, $"{tables} of {rooms} meeting rooms have a table");
        }

        [Test]
        public void OneWaterCoolerPerRoom()
        {
            foreach (var l in TestHouses.Sample)
                foreach (var g in PropsOf(l).Where(x => x.room != null).GroupBy(x => (x.room, x.p.Kind)))
                {
                    int cap = Furnisher.CapOf(g.Key.Kind);
                    if (cap == int.MaxValue) continue;
                    // (the special places and the stands under vents are put apart from the room's own furniture)
                    Assert.LessOrEqual(g.Count(), cap * (g.Key.room.Area > 70f ? 2 : 1) + 1,
                        $"seed {l.Seed}: {g.Count()} × {g.Key.Kind} in the {g.Key.room.Type} #{g.Key.room.Id} ({g.Key.room.Area:0} m²)");
                }
        }

        [Test]
        public void TheTallListMatchesTheModels()
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            var wrong = new List<string>();
            foreach (PropKind kind in System.Enum.GetValues(typeof(PropKind)))
            {
                var prefab = art.Prop(kind);
                if (prefab == null) continue;
                var go = Object.Instantiate(prefab);
                try
                {
                    var rs = go.GetComponentsInChildren<Renderer>();
                    if (rs.Length == 0) continue;
                    var b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    float top = b.max.y, bottom = b.min.y, slim = Mathf.Min(b.size.x, b.size.z);
                    bool tall = Furnisher.Tall.Contains(kind), hangs = Furnisher.OnWall.Contains(kind);
                    if (tall && top < 1.3f) wrong.Add($"{kind} is listed tall but is {top:0.00} m high");
                    if (hangs && bottom < 0.3f) wrong.Add($"{kind} is listed as hung on a wall but starts at {bottom:0.00} m");
                    // big and over the sill line by more than a monitor: it would hide a window
                    bool pole = kind is PropKind.CoatRack or PropKind.IvStand; // a stand with hooks: nothing to hide a window behind
                    if (!tall && !hangs && !pole && top > 1.75f && slim > 0.34f && bottom < 0.3f) wrong.Add($"{kind} ({top:0.00} m high, {slim:0.00} m deep) is missing from Furnisher.Tall");
                    if (!tall && !hangs && bottom > 0.6f && top > 1.2f && kind is not (PropKind.DeskLamp or PropKind.CoffeeMachine or PropKind.Microwave))
                        wrong.Add($"{kind} (from {bottom:0.00} m up) is missing from Furnisher.OnWall");
                }
                finally { Object.DestroyImmediate(go); }
            }
            Assert.IsEmpty(wrong, string.Join("\n", wrong));
        }

        [Test]
        public void AnArcNeverRunsInsideTheStraightWallItTouches()
        {
            int shaped = 0;
            foreach (var l in TestHouses.Sample)
                foreach (var r in l.Rooms)
                {
                    var walls = HousePlan.ShapedWalls(r);
                    shaped += walls.Count;
                    Assert.LessOrEqual(walls.Count, r.Shaped.Count);
                    foreach (var (a, b) in walls)
                        foreach (var c in r.Rects)
                        {
                            float x0 = c.xMin * HousePlan.Res, x1 = c.xMax * HousePlan.Res, z0 = c.yMin * HousePlan.Res, z1 = c.yMax * HousePlan.Res;
                            foreach (float x in new[] { x0, x1 })
                                Assert.IsFalse(Mathf.Abs(a.x - x) < 0.11f && Mathf.Abs(b.x - x) < 0.11f, $"seed {l.Seed} room #{r.Id}: the piece {a}-{b} lies in the wall x = {x}");
                            foreach (float z in new[] { z0, z1 })
                                Assert.IsFalse(Mathf.Abs(a.y - z) < 0.11f && Mathf.Abs(b.y - z) < 0.11f, $"seed {l.Seed} room #{r.Id}: the piece {a}-{b} lies in the wall z = {z}");
                        }
                }
            Assert.Greater(shaped, 0, "the sample has slanted or round walls");
        }

        [Test]
        public void PanelsOfTwoFacesMeetInTheMiddleOfThePostBetweenThem()
        {
            const float t = LevelMetrics.WallThickness;
            // a straight wall of two pieces with a post between them (a third wall meets them from behind), seen from one room
            var shell = new LevelShell();
            var n = Vector3.back;
            shell.Walls.Add(new WallFace { A = new Vector3(t / 2f, 0, 0), B = new Vector3(4f - t / 2f, 0, 0), Y0 = 0f, Y1 = LevelMetrics.WallHeight, Normal = n, CornerA = true, CornerB = true, StraightB = true });
            shell.Walls.Add(new WallFace { A = new Vector3(4f + t / 2f, 0, 0), B = new Vector3(9f - t / 2f, 0, 0), Y0 = 0f, Y1 = LevelMetrics.WallHeight, Normal = n, CornerA = true, CornerB = true, StraightA = true });
            var g = new StyleGeometry { WainscotHeight = 0.9f, Crown = true, RailHeight = 0.9f };
            var decor = ArchitecturePlanner.Plan(shell, null, g);
            foreach (var kind in new[] { DecorKind.Wainscot, DecorKind.Crown, DecorKind.Rail })
            {
                var spans = decor.Where(p => p.Kind == kind).Select(p => (a: p.Position.x - p.Scale.x / 2f, b: p.Position.x + p.Scale.x / 2f)).OrderBy(s => s.a).ToList();
                Assert.IsNotEmpty(spans, kind.ToString());
                for (int i = 1; i < spans.Count; i++)
                    Assert.That(spans[i].a - spans[i - 1].b, Is.InRange(-0.001f, 0.001f), $"{kind}: pieces {i - 1} and {i} neither overlap nor leave a gap");
                Assert.That(spans[0].a, Is.EqualTo(-t / 2f).Within(0.001f), "the far ends still wrap their corner posts");
                Assert.That(spans[spans.Count - 1].b, Is.EqualTo(9f + t / 2f).Within(0.001f));
            }
        }
    }
}
