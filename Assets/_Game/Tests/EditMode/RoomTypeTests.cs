using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// Room types: what each allows (size, shape, outside wall, loot), the kitchen (fridge, table with chairs), the locked
    /// storage room (one door, its terminal, shelves and a pile for the workers) and the workers that come only with it.
    /// </summary>
    public class RoomTypeTests
    {
        static FeatureSettings WithStorage(float chance = 1f) => new() { StorageChance = chance, StorageFromHouse = 0 };

        static IEnumerable<LevelLayout> Houses(FeatureSettings f, int count = 14, int house = 2) =>
            TestHouses.Many(Enumerable.Range(1, count).Select(seed => new TestHouses.Spec(seed * 7919, house, features: f)));

        [Test]
        public void ARuleSaysWhatRoomFits()
        {
            var kitchen = RoomRules.For(RoomType.Kitchen, RoomRules.Defaults());
            Assert.IsTrue(RoomRules.Fits(kitchen, 30f, 1.5f, 6f));
            Assert.IsFalse(RoomRules.Fits(kitchen, 20f, 1.5f, 6f), "too small");
            Assert.IsFalse(RoomRules.Fits(kitchen, 70f, 1.5f, 6f), "too big");
            Assert.IsFalse(RoomRules.Fits(kitchen, 30f, 4f, 6f), "a corridor of a room");
            Assert.IsTrue(RoomRules.Fits(kitchen, 30f, 1.5f, 0f), "no outside wall is fine: windows are not needed");
            Assert.IsTrue(RoomRules.Fits(kitchen, 30f, 1.5f, 6f, rect: false), "an L or a U is fine");
            var none = RoomRules.For(RoomType.Hall, RoomRules.Defaults());
            Assert.IsTrue(RoomRules.Fits(none, 1f, 9f, 0f), "a type without a rule has no limits");
            Assert.IsTrue(RoomRules.LootFits(kitchen, 2));
            Assert.IsFalse(RoomRules.LootFits(kitchen, 5), "a kitchen holds small things");
            Assert.IsTrue(RoomRules.LootFits(RoomRules.For(RoomType.Storage, RoomRules.Defaults()), 9), "storage: anything");
        }

        [Test]
        public void KitchensFitTheirRuleAndHaveAFridgeAndATable()
        {
            var rule = RoomRules.For(RoomType.Kitchen, RoomRules.Defaults());
            int kitchens = 0, tables = 0, bare = 0;
            var missing = new List<string>();
            var noTable = new List<string>();
            foreach (var l in Houses(WithStorage(0f)))
                foreach (var r in l.Rooms)
                {
                    if (r.Type != RoomType.Kitchen || r.Flat >= 0) continue; // (a flat's kitchen is a home's: small, its table where it fits)
                    kitchens++;
                    Assert.That(r.Area, Is.GreaterThanOrEqualTo(rule.Area.x - 0.01f), $"seed {l.Seed}: kitchen {r.Id} of {r.Area} m²");
                    bool fridge = false, table = false;
                    int fridges = 0;
                    foreach (var p in l.Props)
                    {
                        if (l.Plan.RoomAt(p.Position + Vector3.up * 0.3f) != r) continue;
                        fridge |= p.Kind == PropKind.Fridge;
                        if (p.Kind == PropKind.Fridge) fridges++;
                        table |= p.Kind is PropKind.RoundTable or PropKind.BarCounter;
                    }
                    Assert.That(fridges, Is.LessThanOrEqualTo(1), $"seed {l.Seed}: kitchen {r.Id}: one fridge at most");
                    var here = new List<string>();
                    foreach (var p in l.Props) if (l.Plan.RoomAt(p.Position + Vector3.up * 0.3f) == r) here.Add(p.Kind.ToString());
                    if (!fridge) { bare++; missing.Add($"seed {l.Seed}: kitchen {r.Id} ({r.Area:0} m²): {string.Join(",", here)}"); }
                    if (table) tables++;
                    else noTable.Add($"seed {l.Seed}: kitchen {r.Id} ({r.Area:0} m², {r.Bounds.width:0.0}x{r.Bounds.height:0.0}): {string.Join(",", here)}");
                }
            Assert.That(kitchens, Is.GreaterThan(3), "the houses have kitchens");
            // the fridge comes with the kitchen run; a narrow kitchen whose run would block the way between its doors loses it
            Assert.That(bare, Is.LessThanOrEqualTo(kitchens / 10), "kitchens without a fridge: " + string.Join("; ", missing));
            Assert.That(tables, Is.GreaterThan(kitchens / 2), $"most kitchens have their table ({tables} of {kitchens}; a small one with doors all round may have no room for it): " + string.Join("; ", noTable));
        }

        [Test]
        public void TheStorageRoomIsBehindOneLockedDoorWithItsTerminalElsewhere()
        {
            int found = 0;
            foreach (var l in Houses(WithStorage()))
            {
                if (l.StorageRoom < 0) continue;
                found++;
                var room = l.Plan.Rooms[l.StorageRoom];
                Assert.AreEqual(RoomType.Storage, room.Type);
                int storages = 0;
                foreach (var r in l.Rooms) if (r.Type == RoomType.Storage) storages++;
                Assert.AreEqual(1, storages, $"seed {l.Seed}: one storage room, the locked one");
                // the only way in: without its doorway the room cannot be reached at all (by players: no vent, no slot)
                var all = HouseFeatures.Reach(l.Plan, l.Stairs, l.LobbyRoom, true);
                var without = HouseFeatures.Reach(l.Plan, l.Stairs, l.LobbyRoom, true, l.StorageDoorway);
                Assert.IsTrue(all.Contains(room.Id));
                Assert.IsFalse(without.Contains(room.Id), $"seed {l.Seed}: one way in");
                // its door: an automatic door in that doorway, not a hackable lock, not a key door
                Assert.That(l.StorageDoor, Is.GreaterThanOrEqualTo(0));
                Assert.That((l.AutoDoors[l.StorageDoor].position - l.Doorways[l.StorageDoorway].Position).magnitude, Is.LessThan(0.1f));
                Assert.IsFalse(l.LockedDoors.Contains(l.StorageDoor));
                foreach (var k in l.KeyDoors) Assert.AreNotEqual(l.StorageDoor, k.Door);
                // its terminal: a desk turned door terminal, outside the room
                Assert.That(l.StorageTerminal, Is.GreaterThanOrEqualTo(0), $"seed {l.Seed}: a door terminal");
                Assert.AreEqual(PropKind.Terminal, l.Props[l.StorageTerminal].Kind);
                Assert.IsTrue(l.Terminals.Contains((l.StorageTerminal, TerminalKind.Doors)));
                Assert.AreNotEqual(room, l.Plan.RoomAt(l.Props[l.StorageTerminal].Position + Vector3.up * 0.3f));
                Assert.IsTrue(StorageRooms.Locked(l));
            }
            Assert.That(found, Is.GreaterThan(4), "most houses find a room for it");
        }

        [Test]
        public void TheStorageRoomHasShelvesAndAPileForTheWorkers()
        {
            int shelves = 0, piles = 0;
            foreach (var l in Houses(WithStorage()))
            {
                if (l.StorageRoom < 0) continue;
                var room = l.Plan.Rooms[l.StorageRoom];
                Assert.That(l.StorageDrops.Count, Is.GreaterThan(0), $"seed {l.Seed}: somewhere to leave loot");
                foreach (var d in l.StorageDrops)
                {
                    Assert.AreEqual(room, l.Plan.RoomAt(new Vector3(d.Spot.x, LevelLayout.FloorY(room.Floor) + 0.3f, d.Spot.z)), "in the room");
                    if (d.Shelf)
                    {
                        shelves++;
                        Assert.AreEqual(LevelLayout.FloorY(room.Floor) + CargoMotion.ShelfTop + 0.02f, d.Spot.y, 1e-3f, "on the bottom board");
                        Assert.AreEqual(1f, d.Facing.magnitude, 1e-3f);
                    }
                    else piles++;
                }
                // no searchable crate in it: its shelves are the workers'
                foreach (int c in l.Crates) Assert.AreNotEqual(room, l.Plan.RoomAt(l.Props[c].Position + Vector3.up * 0.3f));
            }
            Assert.That(shelves, Is.GreaterThan(0));
            Assert.That(piles, Is.GreaterThan(0));
        }

        [Test]
        public void WorkersComeOnlyWithAStorageRoomAndWorkOnItsFloor()
        {
            int withWorkers = 0;
            foreach (var none in Houses(WithStorage(0f)))
            {
                Assert.AreEqual(-1, none.StorageRoom);
                foreach (var r in none.RobotSpawns) Assert.AreNotEqual(RobotKind.Worker, r.Kind, $"seed {none.Seed}: no storage, no workers");
            }
            foreach (var l in Houses(WithStorage()))
            {
                if (l.StorageRoom < 0) continue;
                int floor = l.Plan.Rooms[l.StorageRoom].Floor, workers = 0;
                foreach (var r in l.RobotSpawns)
                {
                    if (r.Kind != RobotKind.Worker) continue;
                    workers++;
                    Assert.AreEqual(floor, r.Floor, "wheels: on the storage room's floor");
                }
                if (workers > 0) withWorkers++;
            }
            Assert.That(withWorkers, Is.GreaterThan(3), "a storage room brings its workers");
        }

        [Test]
        public void TheProgramComesFirstEveryRoomAtLeastItsTypesSmallest()
        {
            var b = new BuildingSettings();
            for (int seed = 1; seed <= 30; seed++)
            {
                var p = RoomProgram.Make(seed, 3, 700f, b, storage: seed % 2 == 0);
                Assert.That(p.Count, Is.GreaterThan(6));
                int storages = 0;
                for (int f = 0; f < 3; f++)
                {
                    Assert.That(p.AreaOf(f), Is.GreaterThanOrEqualTo(700f * 0.97f), $"seed {seed}: floor {f} filled");
                    foreach (var r in p.Floors[f])
                    {
                        Assert.That(r.Area, Is.GreaterThanOrEqualTo(RoomRules.MinArea(r.Type, b.RoomTypeRules)), $"seed {seed}: {r.Type}");
                        Assert.IsFalse(r.Type is RoomType.Corridor or RoomType.Lobby or RoomType.Hall, "corridors, the lobby and the halls are the plan's");
                        if (r.Type == RoomType.Storage) { storages++; Assert.AreEqual(0, f, "the storage room on the ground floor"); }
                    }
                }
                Assert.AreEqual(seed % 2 == 0 ? 1 : 0, storages, $"seed {seed}: a storage room only when the house has one");
            }
            var every = RoomProgram.Make(1, 1, 10f, new BuildingSettings { EveryRoomType = true }, false);
            foreach (var t in RoomRules.Programmed) Assert.IsTrue(every.Floors[0].Exists(r => r.Type == t), $"every type: {t}");
        }

        [Test]
        public void EveryRoomOfAHouseIsAtLeastItsTypesSmallest()
        {
            var rules = RoomRules.Defaults();
            foreach (var l in TestHouses.Offices()) // (a location's own plan cuts its rooms by its layout: the hospital's, the flats')
                foreach (var r in l.Rooms)
                {
                    if (r.Type is RoomType.Corridor or RoomType.Hall or RoomType.Lobby || r.Flat >= 0) continue; // (a flat's rooms are a home's: its kitchen is small)
                    if (r.Kept) continue; // the exit's and the entry's rooms keep the type they were given, whatever its rule (PlanRoom.Kept)
                    Assert.That(r.Area, Is.GreaterThanOrEqualTo(RoomRules.MinArea(r.Type, rules) - 0.01f), $"seed {l.Seed}: {r.Type} #{r.Id} {r.Area} m²");
                }
        }

        [Test]
        public void CorridorsAreNeverRooms()
        {
            foreach (var l in TestHouses.Corpus(12))
            {
                float rooms = 0f;
                int count = 0;
                foreach (var r in l.Rooms)
                    if (r.Type is not (RoomType.Corridor or RoomType.Hall)) { rooms += r.Area; count++; }
                Assert.AreEqual(rooms / 100f, l.RoomUnits, 1e-3f, "the house's size in rooms leaves the corridors out");
                Assert.AreEqual(count, BuildingSettings.CountRooms(l), "the room count leaves the corridors out");
            }
        }

        [Test]
        public void TheClassicGeneratorGuessesTypesAndStillHasItsStorageRoom()
        {
            var f = new FeatureSettings { StorageChance = 1f, StorageFromHouse = 0 };
            int storage = 0, houses = 0, typed = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                var l = TestHouses.Make(seed * 6151, 1, null, null, 1, f, classic: true);
                var program = TestHouses.Make(seed * 6151, 1, null, null, 1, f, classic: false);
                if (HouseKinds.OwnPlan(l.Kind)) continue; // a location's own plan: the classic switch is about the office generator
                houses++;
                if (l.StorageRoom >= 0) storage++;
                foreach (var r in l.Rooms) if (r.Type is not (RoomType.Corridor or RoomType.Hall or RoomType.Lobby)) typed++;
                Assert.That(l.Rooms.Count, Is.GreaterThan(3), $"seed {seed}: a house of rooms");
                Assert.IsFalse(l.Rooms.Select(r => (r.Type, r.Area)).SequenceEqual(program.Rooms.Select(r => (r.Type, r.Area))),
                    $"seed {seed}: the classic plan is its own");
            }
            Assert.That(typed, Is.GreaterThan(houses * 3));
            Assert.That(storage, Is.GreaterThanOrEqualTo(houses * 7 / 10), $"a dead end becomes the storage room ({storage} of {houses})");
        }

        [Test]
        public void TheStorageDoorOpensFromInsideOnly()
        {
            var door = new Vector3(5f, 0f, 5f);
            var inward = Vector3.right;
            Assert.IsTrue(AutoDoor.IsInside(door, inward, door + Vector3.right * 0.8f));
            Assert.IsFalse(AutoDoor.IsInside(door, inward, door - Vector3.right * 0.8f));
            Assert.IsFalse(AutoDoor.IsInside(door, inward, door + Vector3.forward * 0.8f), "beside the door is not inside");
        }

        [Test]
        public void TheStorageDoorIsLockedUnlessItsTerminalAWorkerOrAnEscapeOpensIt()
        {
            Assert.IsTrue(AutoDoor.StorageLocked(false, false, false), "locked by default");
            Assert.IsFalse(AutoDoor.StorageLocked(true, false, false), "unlocked from the terminal");
            Assert.IsFalse(AutoDoor.StorageLocked(false, true, false), "held open from inside");
            Assert.IsFalse(AutoDoor.StorageLocked(false, false, true), "a worker at the door or inside");

            var door = new Vector3(5f, 0f, 5f);
            var room = Rect.MinMaxRect(5f, 2f, 11f, 8f);
            Assert.IsTrue(AutoDoor.InRoom(door, Vector3.right, room, new Vector3(9f, 0f, 6f)));
            Assert.IsFalse(AutoDoor.InRoom(door, Vector3.right, room, new Vector3(13f, 0f, 6f)), "beyond the room");
            Assert.IsFalse(AutoDoor.InRoom(door, Vector3.right, room, new Vector3(9f, 3.2f, 6f)), "the floor above");
            Assert.IsFalse(AutoDoor.InRoom(door, Vector3.right, room, new Vector3(3f, 0f, 6f)), "outside the door");
            Assert.IsTrue(AutoDoor.AtDoor(door, new Vector3(3.5f, 0f, 5f)));
            Assert.IsFalse(AutoDoor.AtDoor(door, new Vector3(1f, 0f, 5f)));
        }
    }
}
