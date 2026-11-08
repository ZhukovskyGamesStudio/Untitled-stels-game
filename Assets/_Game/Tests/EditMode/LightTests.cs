using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    public class LightTests
    {
        [Test]
        public void SomeRoomsAreDarkButNeverTheLobby()
        {
            int dark = 0, total = 0;
            foreach (var seed in new[] { 1, 2, 3, 4, 5, 6, 7, 8 })
            {
                var l = LevelGenerator.Generate(seed, 3);
                for (int f = 0; f < l.Floors; f++)
                    for (int x = 0; x < l.Width; x++)
                        for (int y = 0; y < l.Height; y++)
                        {
                            total++;
                            if (l.Dark[f, x, y]) dark++;
                            if (l.Rooms[f, x, y] == RoomType.Lobby) Assert.IsFalse(l.Dark[f, x, y]);
                            Assert.IsFalse(l.Dark[f, x, y] && l.Flicker[f, x, y]);
                        }
            }
            float share = dark / (float)total;
            Assert.That(share, Is.InRange(0.1f, 0.4f), $"dark share {share:0.00}");
        }

        [Test]
        public void IsDarkAtMatchesCells()
        {
            var l = LevelGenerator.Generate(9, 4);
            for (int f = 0; f < l.Floors; f++)
                for (int x = 0; x < l.Width; x++)
                    for (int y = 0; y < l.Height; y++)
                        Assert.AreEqual(l.Dark[f, x, y], l.IsDarkAt(l.CellCenter(f, x, y) + Vector3.up));
            Assert.IsFalse(l.IsDarkAt(new Vector3(-5, 0, -5)), "outside is not a dark room");
        }

        [Test]
        public void FlickerStaysInRangeAndActuallyFlickers()
        {
            float min = 1f, max = 0f;
            for (int i = 0; i < 2000; i++)
            {
                float k = LightMath.Flicker(i * 0.05f);
                Assert.That(k, Is.InRange(0f, 1f));
                min = Mathf.Min(min, k);
                max = Mathf.Max(max, k);
            }
            Assert.AreEqual(1f, max);
            Assert.Less(min, 0.5f);
            Assert.AreEqual(0.5f, LightMath.VisionFactor(true));
        }
    }
}
