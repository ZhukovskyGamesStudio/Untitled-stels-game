using System.Linq;
using NUnit.Framework;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// Loot lies on the floor too (not only on desks and shelves), workers lift loot off tables, and the storage room's racks
    /// are empty pallet racks with places for the workers' loot.
    /// </summary>
    public class StorageAndFloorLootTests
    {
        [Category("Slow")] // generates many houses (Tools/dev.sh: SLOW=1 runs it)
        [Test]
        public void SomeLootLiesOnTheFloorClearOfTheFurniture()
        {
            int all = 0, floor = 0;
            for (int seed = 1; seed <= 16; seed++)
            {
                var l = LevelGenerator.Generate(seed * 613, 1 + seed % 4, null, null, 2);
                foreach (var x in LootPlanner.Plan(l, new System.Random(seed)))
                {
                    all++;
                    int f = LevelLayout.FloorOf(x.Position.y + 0.1f);
                    if (x.Position.y - LevelLayout.FloorY(f) > 0.1f) continue;
                    floor++;
                    // the furnisher's own floor spots (by boxes, in a stall, the storage room's pile) lie by furniture on purpose
                    if (l.ItemSpots.Any(s => (s - x.Position).sqrMagnitude < 0.01f)) continue;
                    Assert.IsTrue(LootPlanner.ClearOfFurniture(l, x.Position, f, 0.3f), $"seed {seed}: loot on the floor at {x.Position} lies in furniture");
                }
            }
            Assert.That(floor, Is.GreaterThan(all / 10), $"{floor} of {all} on the floor");
            // (the apartment block's real-sized flats have fewer tables and shelves: more of their loot lies on the floor)
            Assert.That(floor, Is.LessThan(all * 3 / 5), $"{floor} of {all} on the floor");
        }

        [Test]
        public void TheTrayLandsOnATableTopForALift()
        {
            foreach (float surface in new[] { 0.45f, 0.75f, 0.92f, CargoMotion.LiftMax })
            {
                var on = CargoMotion.At((CargoMotion.DownAt + CargoMotion.UpFrom) / 2f, SwingKind.Lift, surface);
                Assert.AreEqual(surface + CargoMotion.PinOnFloor, on.Tray.y, 1e-3f, $"tray on a {surface} m top");
                // rolled all the way in, the pin is where the item lies: the stand-off minus the roll
                Assert.AreEqual(CargoMotion.StandOff(SwingKind.Lift, surface) - CargoMotion.ShelfScoop, on.Tray.z, 1e-3f);
                Assert.AreEqual(CargoMotion.ShelfScoop, CargoMotion.Rolled(CargoMotion.TouchAt, SwingKind.Lift), 1e-4f, "fully in when it takes it");
            }
            Assert.AreEqual(SwingKind.Pickup, LootErrands.TakeKind(0.02f));
            Assert.AreEqual(SwingKind.Lift, LootErrands.TakeKind(0.76f));
            // on a desk it is a candidate; on the next floor up it is not
            var items = new[] { new Vector3(1f, 0.76f, 0f), new Vector3(0.5f, 3.52f, 0f) };
            Assert.AreEqual(0, LootErrands.Nearest(Vector3.zero, items, 6f));
            Assert.AreEqual(-1, LootErrands.Nearest(Vector3.zero, new[] { items[1] }, 6f));
        }

        [Category("Slow")] // generates many houses (Tools/dev.sh: SLOW=1 runs it)
        [Test]
        public void TheStorageRoomHasEmptyRacksWithPlacesForLoot()
        {
            int rooms = 0;
            for (int seed = 1; seed <= 40 && rooms < 8; seed++)
            {
                var l = LevelGenerator.Generate(seed * 271, 2 + seed % 3, null, null, 2);
                if (l.StorageRoom < 0) continue;
                rooms++;
                var inRoom = l.Props.Where(p => l.Plan.RoomAt(p.Position + Vector3.up * 0.3f)?.Id == l.StorageRoom).Select(p => p.Kind).ToList();
                Assert.IsFalse(inRoom.Contains(PropKind.Shelf), $"seed {seed}: a shelf with boxes in the storage room");
                Assert.IsFalse(inRoom.Contains(PropKind.MetalShelving), $"seed {seed}: shelving with boxes in the storage room");
                int shelfDrops = l.StorageDrops.Count(d => d.Shelf);
                Assert.AreEqual(inRoom.Count(k => k == PropKind.StorageRack) * 3, shelfDrops, $"seed {seed}: three places on each rack's bottom deck");
            }
            Assert.That(rooms, Is.GreaterThan(0), "no house with a storage room");
        }
    }
}
