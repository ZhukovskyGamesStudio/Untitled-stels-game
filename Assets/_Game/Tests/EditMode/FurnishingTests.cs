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
        static IEnumerable<LevelLayout> Houses() => TestHouses.Corpus(36);

        [Test]
        public void RoomsHaveWhatMakesThemThatRoom()
        {
            var rooms = new Dictionary<RoomType, int>();
            var without = new Dictionary<RoomType, int>();
            foreach (var l in Houses())
                foreach (var r in l.Plan.Rooms)
                {
                    var kinds = Furnisher.SignaturesOf(l, r); // (a hospital's office by its practice)
                    if (kinds == null || r.Id == l.VaultRoom) continue;
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
            // nine houses of the office generator (the locations' are checked by their own tests), whatever share of the
            // seeds the other kinds take
            int removed = 0, houses = 0;
            float area = 0f;
            foreach (var l in TestHouses.Offices(9))
            {
                houses++;
                removed += TestHouses.Removed(l);
                area += l.FloorArea;
            }
            Assert.AreEqual(9, houses);
            // it used to take out three groups a house: meeting tables (their middle is the table) and whatever stood
            // against the far side of a wall from a room out of reach
            // (the ways up to the vent grates — crates and a desk set first in their room — make it a little busier)
            // (walls 0.3 m thick leave a little less room: a few more; grates kept clear of the lamps put a few ways up in tighter corners)
            // (the exit hatch on the plan's cells, its well through the floor: its spot moved 15 cm, a couple more)
            // (houses differ in size: per 1000 m² of them, as many as it was)
            Assert.That(removed, Is.LessThanOrEqualTo(area / 1000f * 0.6f), $"{removed} groups taken out in {houses} houses of {area:0} m²");
        }
    }
}
