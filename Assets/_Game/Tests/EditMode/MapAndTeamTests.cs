using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Game;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stealth.Tests
{
    /// <summary>Houses that grow with the run and the team, the shared map, pings and rebindable controls.</summary>
    public class MapAndTeamTests
    {
        // ------------------------------------------------------------------ size and quota

        [Test]
        public void HousesKeepGrowingDuringARun()
        {
            var b = new BuildingSettings();
            int prev = 0;
            for (int house = 0; house < 12; house++)
            {
                var g = b.GridFor(house, 1);
                Assert.GreaterOrEqual(g.x, g.y, "wider than deep");
                Assert.GreaterOrEqual(g.x * g.y, prev, $"house {house}");
                prev = g.x * g.y;
            }
            Assert.AreEqual(new Vector2Int(3, 3), b.GridFor(0, 1), "the first house is 3 x 3");
            Assert.Greater(b.GridFor(6, 1).x * b.GridFor(6, 1).y, 20, "later houses are bigger than the old cap (5 x 4)");
            Assert.LessOrEqual(prev, Mathf.CeilToInt(b.MaxRooms), "never above the cap");
        }

        [Test]
        public void ABiggerTeamGetsABiggerHouseButNotTimesN()
        {
            var b = new BuildingSettings();
            for (int house = 0; house < 4; house++)
            {
                int solo = Area(b.GridFor(house, 1)), four = Area(b.GridFor(house, 4));
                Assert.Greater(four, solo, $"house {house}");
                Assert.Less(four, solo * 2.1f, "much softer than x4");
            }
            var small = LevelGenerator.Generate(7, 1, players: 1);
            var big = LevelGenerator.Generate(7, 1, players: 4);
            Assert.AreEqual(4, big.Players);
            Assert.Greater(big.RoomUnits, small.RoomUnits);
            Assert.Greater(big.RobotSpawns.Count, small.RobotSpawns.Count, "more rooms, more guards");
        }

        static int Area(Vector2Int g) => g.x * g.y;

        [Test]
        public void QuotaGrowsSoftlyWithTheTeam()
        {
            int solo = RunState.QuotaFor(1000, 1, 0), duo = RunState.QuotaFor(1000, 1, 0, null, 2), four = RunState.QuotaFor(1000, 1, 0, null, 4);
            Assert.Greater(duo, solo);
            Assert.Greater(four, duo);
            Assert.Less(four, solo * 1.5f, "the share grows softly (the bigger house adds the loot)");
            Assert.AreEqual(RunState.QuotaFor(1000, 1, 0, null, 4), RunState.QuotaFor(1000, 1, 0, null, 9), "the last entry repeats");
        }

        [Test]
        public void QuotaFollowsTheTeamDuringAHouse()
        {
            var run = new RunState();
            run.BeginLevel(0, 1000, 1);
            int solo = run.Quota;
            Assert.IsTrue(run.SetPlayers(3), "a friend joined");
            Assert.AreEqual(RunState.QuotaFor(1000, 0, 0, null, 3), run.Quota);
            Assert.Greater(run.Quota, solo);
            Assert.IsFalse(run.SetPlayers(3), "no change");
            run.SetPlayers(1);
            Assert.AreEqual(solo, run.Quota, "and back when they leave");
        }

        [Test]
        public void TheTeamSizeIsPartOfTheHouse()
        {
            var a = new LevelInfo { Seed = 5, Index = 1, Players = 1 };
            var b = new LevelInfo { Seed = 5, Index = 1, Players = 3 };
            Assert.IsFalse(a.Equals(b), "a client rebuilds the house when the team size differs");
        }

        // ------------------------------------------------------------------ exploration

        [Test]
        public void NothingIsSeenUntilSomebodyLooks()
        {
            var l = LevelGenerator.Generate(3, 1);
            var map = new ExplorationMap(l);
            Assert.AreEqual(0f, map.RevealedShare(0));
            Assert.AreEqual(Mathf.CeilToInt((l.SizeX + 2f * ExplorationMap.Margin) / ExplorationMap.Tile), map.Width);
            var eye = l.Lobby.Center + Vector3.up * 1.6f;
            Assert.Greater(map.RevealAround(eye, 8f), 0);
            var tile = ExplorationMap.TileOf(eye);
            Assert.IsTrue(map.IsRevealed(0, tile.x, tile.y), "where you stand");
            Assert.AreEqual(0f, map.RevealedShare(1), "other floors stay dark");
            int version = map.Version;
            Assert.AreEqual(0, map.RevealAround(eye, 8f), "seen once");
            Assert.AreEqual(version, map.Version);
        }

        [Test]
        public void WallsHideTheNextRoomDoorsShowIt()
        {
            var l = LevelGenerator.Generate(11, 2);
            int checkedWalls = 0, checkedDoors = 0;
            var plan = l.Plan;
            for (int i = 0; i < plan.Walls.Count; i++)
            {
                var w = plan.Walls[i];
                if (w.Floor != 0 || w.RoomA < 0 || w.RoomB < 0 || w.Railing || w.Shaped || w.Length < 3f) continue;
                var mid = w.At(w.Length / 2f);
                var n = new Vector2(-w.Dir.y, w.Dir.x);
                var openings = plan.OpeningsOf(i);
                // straight through the middle of the wall: an opening there lets the view through, a wall does not
                bool through = ExplorationMap.CanSee(l, 0, mid - n * 0.6f, mid + n * 0.6f);
                bool open = openings.Any(o => o.Kind == EdgeType.Open || Mathf.Abs(o.At - w.Length / 2f) < LevelMetrics.ClearWidth(o.Width) / 2f - 0.05f);
                bool solid = !openings.Any(o => o.Kind == EdgeType.Open || Mathf.Abs(o.At - w.Length / 2f) < o.Width / 2f + 0.1f);
                if (open) { Assert.IsTrue(through, $"through the opening at {mid}"); checkedDoors++; }
                else if (solid) { Assert.IsFalse(through, $"through the wall at {mid}"); checkedWalls++; }
            }
            Assert.Greater(checkedWalls, 3);
            Assert.Greater(checkedDoors, 0);
            var lobby = l.Lobby.Center;
            Assert.IsFalse(ExplorationMap.CanSee(l, 0, new Vector2(lobby.x, lobby.z), new Vector2(-3f, -3f)), "nothing outside the house");
        }

        [Test]
        public void PassingADoorRevealsWhatIsSeenThroughIt()
        {
            int checkedDoors = 0;
            foreach (int seed in new[] { 11, 12, 13, 14 })
            {
                var l = LevelGenerator.Generate(seed, 2);
                var plan = l.Plan;
                foreach (var o in plan.Openings)
                {
                    if (o.Kind != EdgeType.Door) continue;
                    var w = plan.Walls[o.Wall];
                    if (w.Floor != 0 || w.Railing || w.Shaped || w.RoomA < 0 || w.RoomB < 0) continue;
                    var n = new Vector2(-w.Dir.y, w.Dir.x);
                    var door = w.At(o.At);
                    var eye = door - n * 1.2f;                                  // a step in front of the door
                    var through = door + n * 2.0f;                              // straight through it
                    var beside = w.At(o.At > w.Length / 2f ? o.At - 2.6f : o.At + 2.6f) + n * 0.4f; // behind the wall next to it
                    if (!plan.IsInside(0, eye) || !plan.IsInside(0, through) || !plan.IsInside(0, beside)) continue;
                    if (!plan.CanSee(0, eye, through) || plan.CanSee(0, eye, beside)) continue;
                    var map = new ExplorationMap(l);
                    map.RevealAround(new Vector3(eye.x, 1.6f, eye.y), 14f);
                    var t = ExplorationMap.TileOf(new Vector3(through.x, 0f, through.y));
                    var b = ExplorationMap.TileOf(new Vector3(beside.x, 0f, beside.y));
                    Assert.IsTrue(map.IsRevealed(0, t.x, t.y), $"seed {seed}: the room through the door at {through}");
                    Assert.IsFalse(map.IsRevealed(0, b.x, b.y), $"seed {seed}: behind the wall at {beside}");
                    if (++checkedDoors >= 12) return;
                }
            }
            Assert.Greater(checkedDoors, 2);
        }

        [Test]
        public void OutsideTheMapOpensOnlyCloseBy()
        {
            var l = LevelGenerator.Generate(5, 1);
            var map = new ExplorationMap(l);
            var eye = new Vector3(-3f, 1.6f, l.SizeZ / 2f); // in the yard, 3 m off the building's side
            Assert.Greater(map.RevealAround(eye, 14f, 4f), 0, "the yard round the player opens");
            var here = ExplorationMap.TileOf(eye);
            Assert.IsTrue(map.IsRevealed(0, here.x, here.y));
            var far = ExplorationMap.TileOf(eye + new Vector3(0f, 0f, 6f));
            Assert.IsFalse(map.IsRevealed(0, far.x, far.y), "not far in the gas");
            var rects = new List<MapRect>();
            MapSketch.Build(map, 0, null, rects);
            Assert.IsTrue(rects.Any(r => r.Part == MapPart.Ground), "the yard is drawn as ground");
        }

        [Test]
        public void TheHouseHasAMadeUpCompanyName()
        {
            string a = HouseKinds.Title(7, HouseKind.Office);
            Assert.AreEqual(a, HouseKinds.Title(7, HouseKind.Office), "the same house, the same name");
            StringAssert.StartsWith("Офис ", a);
            StringAssert.Contains("«", a);
            var names = new HashSet<string>();
            for (int s = 0; s < 40; s++) names.Add(HouseKinds.Title(s, HouseKind.DataCenter));
            Assert.Greater(names.Count, 20, "houses get different companies");
        }

        [Test]
        public void StairLooksSuitTheFamily()
        {
            foreach (Stealth.Configs.ModelFamily f in System.Enum.GetValues(typeof(Stealth.Configs.ModelFamily)))
            {
                var options = StairStyles.For(f);
                var seen = new HashSet<StairStyle>();
                for (int s = 0; s < 200; s++)
                {
                    var pick = StairStyles.Pick(f, s);
                    CollectionAssert.Contains(options, pick, $"{f}");
                    seen.Add(pick);
                }
                Assert.AreEqual(options.Length, seen.Count, $"{f}: every suitable look turns up");
                Assert.AreEqual(StairStyles.Pick(f, 42), StairStyles.Pick(f, 42));
            }
            Assert.AreEqual(StairStyles.Count, System.Enum.GetValues(typeof(StairStyle)).Length);
        }

        [Test]
        public void PeersMergeWhatTheOthersSaw()
        {
            var l = LevelGenerator.Generate(4, 0);
            var server = new ExplorationMap(l);
            var client = new ExplorationMap(l);
            var ground = l.Rooms.Where(r => r.Floor == 0).OrderBy(r => r.Center.x + r.Center.z).ToList();
            server.RevealAround(ground[0].Center + Vector3.up, 8f);
            client.RevealAround(ground[ground.Count - 1].Center + Vector3.up, 8f);
            float before = client.RevealedShare(0);
            for (int i = 0; i < server.ChunkCount; i++) client.Merge(i, server.Chunk(i));
            Assert.Greater(client.RevealedShare(0), before);
            Assert.GreaterOrEqual(client.RevealedShare(0), server.RevealedShare(0), "bits are only added");
            for (int i = 0; i < server.ChunkCount; i++) Assert.IsFalse(client.Merge(i, server.Chunk(i)), "twice changes nothing");
        }

        [Test]
        public void TheMapDrawsOnlyWhatWasSeen()
        {
            var l = LevelGenerator.Generate(6, 1);
            var map = new ExplorationMap(l);
            var rects = new List<MapRect>();
            MapSketch.Build(map, 0, null, rects);
            Assert.IsEmpty(rects, "nothing seen, nothing drawn");

            var room = l.Lobby.Center;
            map.RevealAround(room + Vector3.up * 1.6f, 8f);
            var props = new List<MapProp>
            {
                new() { Floor = 0, Kind = PropKind.Desk, Area = new Rect(room.x - 1f, room.z - 1f, 2f, 1f) },
                new() { Floor = 0, Kind = PropKind.Desk, Area = new Rect(-50f, -50f, 1f, 1f) }, // far away: unseen
            };
            MapSketch.Build(map, 0, props, rects);
            Assert.IsTrue(rects.Any(r => r.Part == MapPart.Floor));
            Assert.IsTrue(rects.Any(r => r.Part == MapPart.Wall), "the walls of the room");
            Assert.AreEqual(1, rects.Count(r => r.Part == MapPart.Prop), "only the furniture that was seen");
            Assert.IsTrue(rects.Any(r => r.Part == MapPart.Hatch), "the lobby has the hatch");
            // Floor runs are merged: far fewer rectangles than tiles.
            int tiles = Mathf.RoundToInt(map.RevealedShare(0) * map.Width * map.Height);
            Assert.Less(rects.Count(r => r.Part == MapPart.Floor), tiles);
            foreach (var r in rects.Where(r => r.Part == MapPart.Wall))
                Assert.IsTrue(Mathf.Approximately(r.Area.width, MapSketch.WallWidth) || Mathf.Approximately(r.Area.height, MapSketch.WallWidth) || r.Angle != 0f);
        }

        // ------------------------------------------------------------------ pings

        [Test]
        public void OneHerePingPerPlayerAndTheyExpire()
        {
            var board = new PingBoard();
            board.Add(new TeamPing { Owner = 1, Kind = PingKind.Here, Position = Vector3.zero, Until = 5f });
            board.Add(new TeamPing { Owner = 1, Kind = PingKind.Here, Position = Vector3.one, Until = 6f });
            board.Add(new TeamPing { Owner = 2, Kind = PingKind.Here, Position = Vector3.one, Until = 6f });
            board.Add(new TeamPing { Owner = 1, Kind = PingKind.Enemy, Target = 40, Until = 9f });
            board.Add(new TeamPing { Owner = 2, Kind = PingKind.Enemy, Target = 40, Until = 10f });
            Assert.AreEqual(3, board.Active.Count, "a new ping replaces the player's old one; a robot is marked once");
            Assert.AreEqual(Vector3.one, board.Active.First(p => p.Owner == 1 && p.Kind == PingKind.Here).Position);
            board.Expire(7f);
            Assert.AreEqual(1, board.Active.Count);
            Assert.AreEqual(PingKind.Enemy, board.Active[0].Kind);
        }

        [Test]
        public void CalloutsCannotBeSpammed()
        {
            var board = new PingBoard();
            Assert.IsTrue(board.TryCallout(1, 0f, 0.5f));
            Assert.IsFalse(board.TryCallout(1, 0.2f, 0.5f));
            Assert.IsTrue(board.TryCallout(2, 0.2f, 0.5f), "per player");
            Assert.IsTrue(board.TryCallout(1, 0.6f, 0.5f));
        }

        // ------------------------------------------------------------------ controls

        [Test]
        public void EveryActionHasADefaultKeyAndATitle()
        {
            Assert.AreEqual(System.Enum.GetValues(typeof(GameAction)).Length, Controls.All.Length);
            for (int i = 0; i < Controls.All.Length; i++)
            {
                var info = Controls.All[i];
                Assert.AreEqual(i, (int)info.Action, "listed in enum order");
                Assert.IsFalse(info.Default.IsNone, info.Title);
                Assert.IsNotEmpty(info.Title);
            }
            Assert.AreEqual("F8", Controls.Name(Controls.All[(int)GameAction.Style].Default), "the style key is listed");
        }

        [Test]
        public void DefaultsDoNotClashExceptOnPurpose()
        {
            Controls.ResetToDefaults();
            foreach (var info in Controls.All)
                Assert.IsFalse(Controls.Conflicts(info.Action, out var other), $"{info.Title} vs {other}");
        }

        [Test]
        public void RebindingRenamesThePrompts()
        {
            Controls.ResetToDefaults();
            try
            {
                Assert.AreEqual("E", Controls.Label(GameAction.Interact));
                Controls.Set(GameAction.Interact, KeyBinding.Of(Key.G));
                Assert.AreEqual("G", Controls.Label(GameAction.Interact));
                Assert.IsTrue(Stealth.UI.PromptRules.DownedText(0, false).Contains("G"));
                Controls.Set(GameAction.Ping, KeyBinding.MouseButton(4));
                Assert.AreEqual("Мышь 5", Controls.Label(GameAction.Ping));
                Controls.Set(GameAction.Mark, KeyBinding.Of(Key.G));
                Assert.IsTrue(Controls.Conflicts(GameAction.Mark, out var other));
                Assert.AreEqual(GameAction.Interact, other);
                Assert.AreEqual("Пробел", Controls.Name(KeyBinding.Of(Key.Space)));
                Assert.AreEqual("5", Controls.Name(KeyBinding.Of(Key.Digit5)));
            }
            finally
            {
                Controls.ResetToDefaults();
            }
        }
    }
}
