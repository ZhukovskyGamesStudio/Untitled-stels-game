using System.Collections.Generic;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    public class LevelGeneratorTests
    {
        static readonly int[] Seeds = { 1, 7, 42, 1337, 99991, 123456789 };

        [Test]
        public void SameSeedGivesSameLayout()
        {
            foreach (var seed in Seeds)
            {
                var a = LevelGenerator.Generate(seed, 2);
                var b = LevelGenerator.Generate(seed, 2);
                Assert.AreEqual(a.Props.Count, b.Props.Count);
                Assert.AreEqual(a.ItemSpots.Count, b.ItemSpots.Count);
                Assert.AreEqual(a.HatchPosition, b.HatchPosition);
                for (int i = 0; i < a.Props.Count; i++) Assert.AreEqual(a.Props[i].Position, b.Props[i].Position);
            }
        }

        [Test]
        public void EveryRoomIsReachableOnEveryFloor()
        {
            foreach (var seed in Seeds)
                for (int level = 0; level < 5; level++)
                {
                    var l = LevelGenerator.Generate(seed, level);
                    Assert.AreEqual(l.Width * l.Height * l.Floors, CountReachable(l), $"seed {seed} level {level}");
                }
        }

        [Test]
        public void SecurityIsPlacedAndGrowsWithLevel()
        {
            var easy = LevelGenerator.Generate(5, 0);
            var hard = LevelGenerator.Generate(5, 4);
            Assert.Greater(easy.RobotSpawns.Count, 0);
            Assert.Greater(hard.RobotSpawns.Count, easy.RobotSpawns.Count);
            Assert.GreaterOrEqual(hard.CameraSpots.Count, easy.CameraSpots.Count);
        }

        [Test]
        public void SpawnsAreInsideTheBuilding()
        {
            foreach (var seed in Seeds)
            {
                var l = LevelGenerator.Generate(seed, 3);
                foreach (var p in l.PlayerSpawns) Assert.IsTrue(l.IsInside(p), $"spawn {p}");
                foreach (var p in l.ItemSpots) Assert.IsTrue(l.IsInside(p), $"item {p}");
                foreach (var r in l.RobotSpawns) Assert.IsTrue(l.IsInside(r.Position), $"robot {r.Position}");
            }
        }

        /// <summary>Flood fill over doors/openings inside a floor and stairwells between floors.</summary>
        static int CountReachable(LevelLayout l)
        {
            var seen = new HashSet<Vector3Int>();
            var queue = new Queue<Vector3Int>();
            var start = new Vector3Int(l.StartCell.x, l.StartCell.y, 0);
            seen.Add(start);
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var n in l.Neighbours(c.z, new Vector2Int(c.x, c.y)))
                    if (seen.Add(n)) queue.Enqueue(n);
            }
            return seen.Count;
        }
    }
}
