using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// Rooms read as what they are (an office has its desk, a server room its racks), and the walkability check after the
    /// furnishing has little left to take out (the checks while furnishing agree with it).
    /// </summary>
    public class FurnishingTests
    {
        static IEnumerable<LevelLayout> Houses()
        {
            for (int seed = 1; seed <= 12; seed++)
                for (int house = 0; house < 6; house += 2)
                    yield return LevelGenerator.Generate(seed * 7919 + house + (seed % 2), house + (seed % 2), null, null, 2);
        }

        [Test]
        public void RoomsHaveWhatMakesThemThatRoom()
        {
            var rooms = new Dictionary<RoomType, int>();
            var without = new Dictionary<RoomType, int>();
            foreach (var l in Houses())
                foreach (var r in l.Plan.Rooms)
                {
                    if (!Furnisher.Signatures.TryGetValue(r.Type, out var kinds) || r.Id == l.VaultRoom) continue;
                    rooms[r.Type] = (rooms.TryGetValue(r.Type, out var n) ? n : 0) + 1;
                    bool has = l.Props.Any(p => kinds.Contains(p.Kind) && l.Plan.RoomAt(p.Position + Vector3.up * 0.3f) == r);
                    if (!has) without[r.Type] = (without.TryGetValue(r.Type, out var m) ? m : 0) + 1;
                }
            // most rooms of each kind have it (small rooms with doors on every wall may not)
            var most = new Dictionary<RoomType, float>
            {
                [RoomType.Office] = 0.15f, [RoomType.OpenSpace] = 0.05f, [RoomType.Meeting] = 0.1f,
                [RoomType.Server] = 0.12f, [RoomType.Lab] = 0.25f, [RoomType.Infirmary] = 0.25f,
            };
            foreach (var kv in rooms)
            {
                int bad = without.TryGetValue(kv.Key, out var b) ? b : 0;
                Assert.That(bad, Is.LessThanOrEqualTo(kv.Value * most[kv.Key]), $"{kv.Key} without its piece: {bad} of {kv.Value}");
            }
        }

        [Test]
        public void TheFinalWalkabilityCheckHasLittleToTakeOut()
        {
            int removed = 0, houses = 0;
            foreach (var l in Houses()) { houses++; removed += Furnisher.LastRemoved; }
            // it used to take out three groups a house: meeting tables (their middle is the table) and whatever stood
            // against the far side of a wall from a room out of reach
            Assert.That(removed, Is.LessThanOrEqualTo(houses / 2), $"{removed} groups taken out in {houses} houses");
        }
    }
}
