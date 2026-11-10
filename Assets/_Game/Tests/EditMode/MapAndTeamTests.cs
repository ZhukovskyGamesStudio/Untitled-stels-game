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
            Assert.Greater(big.Width * big.Height, small.Width * small.Height);
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
            Assert.AreEqual(l.Width * 4, map.Width);
            var eye = l.CellCenter(0, l.StartCell.x, l.StartCell.y) + Vector3.up * 1.6f;
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
            for (int x = 0; x < l.Width - 1; x++)
                for (int y = 0; y < l.Height; y++)
                {
                    var edge = l.EdgesX[0, x, y];
                    float midZ = (y + 0.5f) * LevelMetrics.Cell, wallX = (x + 1) * LevelMetrics.Cell;
                    // Straight through the middle of the wall: a door or an opening lets the view through, a wall does not.
                    bool through = ExplorationMap.CanSee(l, 0, new Vector2(wallX - 3f, midZ), new Vector2(wallX + 3f, midZ));
                    if (edge == EdgeType.Wall) { Assert.IsFalse(through); checkedWalls++; }
                    if (edge == EdgeType.Door || edge == EdgeType.Open) { Assert.IsTrue(through); checkedDoors++; }
                    // Off to the side of a door: the wall next to the doorway blocks.
                    if (edge == EdgeType.Door)
                        Assert.IsFalse(ExplorationMap.CanSee(l, 0, new Vector2(wallX - 3f, midZ + 3.5f), new Vector2(wallX + 3f, midZ + 3.5f)));
                }
            Assert.Greater(checkedWalls + checkedDoors, 3);
            Assert.IsFalse(ExplorationMap.CanSee(l, 0, new Vector2(1f, 5f), new Vector2(-3f, 5f)), "nothing outside the house");
        }

        [Test]
        public void PeersMergeWhatTheOthersSaw()
        {
            var l = LevelGenerator.Generate(4, 0);
            var server = new ExplorationMap(l);
            var client = new ExplorationMap(l);
            server.RevealAround(l.CellCenter(0, 0, 0) + Vector3.up, 8f);
            client.RevealAround(l.CellCenter(0, l.Width - 1, l.Height - 1) + Vector3.up, 8f);
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

            var room = l.CellCenter(0, l.StartCell.x, l.StartCell.y);
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
                Assert.IsTrue(Mathf.Approximately(r.Area.width, MapSketch.WallWidth) || Mathf.Approximately(r.Area.height, MapSketch.WallWidth));
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
