using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Audio;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using Stealth.Vehicles;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The special places of a house, the car, the sewer plan and the smaller new rules.</summary>
    public class HouseFeatureTests
    {
        [Test]
        public void HiddenRoomIsEnclosedAndEnteredOnlyThroughIllusions()
        {
            int found = 0;
            // the shared houses (half of the houses have one)
            foreach (var l in TestHouses.Sample)
                {
                    int seed = l.Seed;
                    if (l.HiddenRoom < 0) continue;
                    found++;
                    var plan = l.Plan;
                    foreach (var w in plan.Walls.Where(w => w.RoomA == l.HiddenRoom || w.RoomB == l.HiddenRoom))
                        Assert.IsFalse(w.Exterior || w.Railing, $"seed {seed}: the hidden room has an outer wall");
                    var openings = plan.Openings.Where(o => plan.Walls[o.Wall].RoomA == l.HiddenRoom || plan.Walls[o.Wall].RoomB == l.HiddenRoom).ToList();
                    Assert.IsNotEmpty(openings);
                    Assert.IsTrue(openings.All(o => o.Kind == EdgeType.Illusion), $"seed {seed}: only illusion entrances");
                    Assert.IsTrue(HouseFeatures.Reach(plan, l.Stairs, l.LobbyRoom, true).Contains(l.HiddenRoom), "players get in");
                    Assert.IsFalse(HouseFeatures.Reach(plan, l.Stairs, l.LobbyRoom, false).Contains(l.HiddenRoom), "robots do not");
                    // the map does not see through an illusion
                    var o0 = openings[0];
                    var wall = plan.Walls[o0.Wall];
                    var n = new Vector2(-wall.Dir.y, wall.Dir.x);
                    var mid = wall.At(o0.At);
                    Assert.Less(plan.SightShare(wall.Floor, mid - n * 1f, mid + n * 1f), 0.99f, "an illusion stops the view");
                }
            Assert.Greater(found, 0, "some houses have a hidden room");
        }

        [Test]
        public void KeyDoorsCloseOffAPartAndTheKeyLiesOutside()
        {
            int doors = 0;
            foreach (var l in TestHouses.Sample)
            {
                int seed = l.Seed;
                foreach (var k in l.KeyDoors)
                {
                    doors++;
                    Assert.IsFalse(l.LockedDoors.Contains(k.Door), "not a hackable lock too");
                    var pose = l.AutoDoors[k.Door];
                    int i = l.Doorways.FindIndex(d => (d.Position - pose.position).sqrMagnitude < 0.1f);
                    Assert.GreaterOrEqual(i, 0);
                    var without = HouseFeatures.Reach(l.Plan, l.Stairs, l.LobbyRoom, true, i);
                    foreach (var r in k.Rooms) Assert.IsFalse(without.Contains(r), $"seed {seed}: room {r} is reached without the key door");
                    var keyRoom = l.Plan.RoomAt(k.Key + Vector3.up * 0.3f);
                    Assert.IsNotNull(keyRoom);
                    Assert.IsTrue(without.Contains(keyRoom.Id), $"seed {seed}: the key lies on this side of its door");
                }
            }
            Assert.Greater(doors, 0, "the shared houses have key doors");
        }

        [Test]
        public void EveryLocationHasItsPointOfInterest()
        {
            // which location a house is comes from its kind alone
            var seen = new HashSet<Location>();
            for (int seed = 1; seed < 200; seed++) seen.Add(HouseFeatures.LocationOf(HouseKinds.Of(seed, 2)));
            Assert.AreEqual(3, seen.Count, "offices, hospitals and garages");
            foreach (var l in TestHouses.Sample)
            {
                int seed = l.Seed;
                Assert.GreaterOrEqual(l.PointOfInterest, 0, $"seed {seed} ({l.Kind})");
                Assert.AreEqual(HouseFeatures.PointOfInterestKind(l.Location), l.Props[l.PointOfInterest].Kind);
                Assert.Greater(l.Crates.Count, 0);
                Assert.IsTrue(l.Crates.All(c => l.Props[c].Kind == PropKind.Crate));
                Assert.Greater((l.ExitHatchPosition - l.HatchPosition).magnitude, 8f, $"seed {seed}: the exit is away from the entry");
            }
        }

        [Test]
        public void WardensStandBeforeTheOnlyDoorOfALootRoomWithLuresAround()
        {
            int wardens = 0;
            foreach (var l in TestHouses.Sample)
            {
                foreach (var w in l.Wardens)
                {
                    wardens++;
                    var room = l.Rooms[w.Room];
                    Assert.AreNotEqual(room.Id, l.Plan.RoomAt(w.A + Vector3.up * 0.3f)?.Id, "outside the room it guards");
                    Assert.LessOrEqual((w.A - l.Doorways[w.Door].Position).magnitude, 3f);
                }
                Assert.AreEqual(l.Wardens.Count, l.RobotSpawns.Count(r => r.Kind == RobotKind.Warden));
                Assert.GreaterOrEqual(l.LureSpots.Count, l.Wardens.Count);
            }
            Assert.Greater(wardens, 0, "the shared houses have wardens");
        }

        [Test]
        public void TheWorkbenchMakesDiamondsOfOneColour()
        {
            var items = new List<InventoryEntry>
            {
                new() { Shape = 3, Color = 1 }, new() { Shape = 4, Color = 1 }, // 4 + 4 cells of green: one diamond
                new() { Shape = 0, Color = 1 },                                  // left over
                new() { Shape = 2, Color = 2 },                                  // 3 cells of blue: not enough
                new() { Shape = ItemCatalog.Key, Color = 1 },                    // a tool is never used up
            };
            var plan = Crafting.Plan(items, new GadgetSettings { DiamondInputCells = 8 });
            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(1, plan[0].Color);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, plan[0].Items);
            Assert.Greater(ItemCatalog.Value(ItemCatalog.Diamond, 1), ItemCatalog.Value(3, 1) + ItemCatalog.Value(4, 1) - 1, "worth more than what went in");
            Assert.AreEqual(0, ItemCatalog.Value(ItemCatalog.Key, 4), "tools are worth nothing");
            Assert.IsFalse(ItemCatalog.IsLoot(ItemCatalog.Stun));
        }

        [Test]
        public void TheBedHeapsThingsWithoutCells()
        {
            var bed = new Cargo();
            for (int i = 0; i < 40; i++) bed.Add((byte)(i % 8), 1, 0f, 100f, i * 9f);
            Assert.AreEqual(40, bed.Count, "no limit");
            var h = bed.Heights();
            Assert.AreEqual(0f, h[0]);
            Assert.Greater(h[39], h[1], "a heap grows where things fall on each other");
            var item = new CargoItem { Shape = 5, Color = 3, X = -40, Z = 150, Yaw = 270 };
            Assert.AreEqual(item, CargoItem.Unpack(item.Pack()));
            bed.Add(9, 4, 500f, -50f, 0f);
            var last = bed.Items[^1];
            Assert.LessOrEqual(last.X, Cargo.HalfWidth);
            Assert.GreaterOrEqual(last.Z, 0);
        }

        [Test]
        public void ThePickupAcceleratesBrakesReversesAndTurns()
        {
            var car = new CarPhysics(new CarSettings());
            for (int i = 0; i < 300; i++) car.Step(new CarInput { Throttle = 1f }, 0.02f);
            Assert.AreEqual(car.Settings.MaxSpeed, car.Speed, 0.01f, "top speed");
            float before = car.Speed;
            car.Step(new CarInput { Throttle = -1f }, 0.1f);
            Assert.Less(car.Speed, before);
            Assert.Greater(car.Speed, 0f, "the pedal the other way brakes first");
            for (int i = 0; i < 400; i++) car.Step(new CarInput { Throttle = -1f }, 0.02f);
            Assert.AreEqual(-car.Settings.MaxReverse, car.Speed, 0.01f, "then reverses");
            var turn = new CarPhysics(new CarSettings());
            float yaw = 0f;
            for (int i = 0; i < 100; i++) yaw += turn.Step(new CarInput { Throttle = 0.5f, Steer = 1f }, 0.02f).yaw;
            Assert.Greater(yaw, 10f, "right turns right");
            Assert.Less(turn.SteerLimit(10f), turn.SteerLimit(0f), "less lock at speed");
            Assert.Less(turn.TurningRadius, 6f, "it turns round in the sewer's hall");
        }

        [Test]
        public void TheSewerIsDifferentEveryTimeAndJoinsUp()
        {
            var a = SewerPlan.Make(5, 0);
            var b = SewerPlan.Make(5, 1);
            var again = SewerPlan.Make(5, 0);
            CollectionAssert.AreEqual(a.Segments.Select(s => s.Variant), again.Segments.Select(s => s.Variant), "the same on every peer");
            Assert.IsTrue(a.Segments.Select(s => s.Variant).SequenceEqual(b.Segments.Select(s => s.Variant)) == false || a.LampColor != b.LampColor || a.Segments.Count != b.Segments.Count,
                "another stretch after another house");
            for (int i = 1; i < a.Segments.Count; i++)
                Assert.AreEqual(a.Segments[i - 1].X + SewerPlan.SegmentLength, a.Segments[i].X, 0.001f, "segments join end to end");
            Assert.Greater(a.GateX, a.CarX, "the car waits behind the gate");
            Assert.Greater(a.ExitLadderX, a.GateX, "the team comes down past it");
            Assert.AreEqual(a.Segments[^1].X + SewerPlan.SegmentLength, a.HubX, 0.001f);
            var start = SewerPlan.Make(5, -1);
            Assert.Less(start.GateX, 0f, "the start of a run has no gate");
            Assert.Less(start.ExitLadderX, 0f);
        }

        [Test]
        public void SoundsFromOtherFloorsAreMuffledAndLegsSwingWithSpeed()
        {
            Assert.AreEqual(1f, AudioRules.FloorFactor(1.6f, 0.2f, 0.12f), "the same floor");
            Assert.AreEqual(0.12f, AudioRules.FloorFactor(1.6f, 1.6f + LevelMetrics.FloorHeight, 0.12f), 1e-4f, "one floor up");
            Assert.Less(AudioRules.FloorFactor(1.6f, 1.6f + 2 * LevelMetrics.FloorHeight, 0.12f), 0.02f, "two floors up");
            Assert.AreEqual(1f, AudioRules.FloorFactor(1.6f, 2.9f, 0.12f), "a ceiling robot of this floor");
            Assert.Greater(PlayerRules.LegSwing(6f, false), PlayerRules.LegSwing(2f, false), "running: wider strides");
            Assert.Less(PlayerRules.LegSwing(3f, true), PlayerRules.LegSwing(3f, false), "crouched: shorter");
            Assert.AreNotEqual(AudioRules.VoicePitch(RobotKind.Hunter), AudioRules.VoicePitch(RobotKind.Drone), "robots speak in their own voices");
        }
    
        [Test]
        public void NoKeyDoorShutsAnAtriumAPlayerCouldDropInto()
        {
            int doors = 0;
            foreach (var l in TestHouses.Sample)
                foreach (var k in l.KeyDoors)
                {
                    doors++;
                    foreach (int r in k.Rooms)
                        Assert.AreEqual(1, l.Plan.Rooms[r].Levels, $"seed {l.Seed}: behind the key door is a room {l.Plan.Rooms[r].Levels} storeys high (a gallery above looks into it)");
                }
            Assert.Greater(doors, 0);
        }

        [Test]
        public void ThemedRoomsAreFurnishedAsWhatTheyAre()
        {
            var signature = new Dictionary<RoomType, PropKind[]>
            {
                [RoomType.Gym] = new[] { PropKind.Treadmill, PropKind.ExerciseBike, PropKind.WeightBench, PropKind.DumbbellRack, PropKind.PunchingBag },
                [RoomType.Library] = new[] { PropKind.TallBookcase, PropKind.ReadingTable, PropKind.Globe },
                [RoomType.CopyRoom] = new[] { PropKind.Copier, PropKind.PaperStack, PropKind.MailShelf, PropKind.Shredder },
                [RoomType.ControlRoom] = new[] { PropKind.MonitorWall, PropKind.ConsoleDesk },
                [RoomType.Lab] = new[] { PropKind.LabBench, PropKind.PartsCabinet, PropKind.RobotArm },
                [RoomType.NapRoom] = new[] { PropKind.SleepPod, PropKind.Hammock, PropKind.BeanBag },
                [RoomType.Studio] = new[] { PropKind.MixingDesk, PropKind.MicStand, PropKind.AcousticPanel, PropKind.DrumKit },
                [RoomType.LockerRoom] = new[] { PropKind.Lockers, PropKind.ShowerStall, PropKind.Bench },
                [RoomType.Infirmary] = new[] { PropKind.HospitalBed, PropKind.MedicineCabinet, PropKind.PrivacyScreen },
                [RoomType.GameRoom] = new[] { PropKind.PoolTable, PropKind.Foosball, PropKind.PingPongTable, PropKind.DartBoard, PropKind.BarCounter, PropKind.ArcadeMachine },
            };
            var seen = new HashSet<RoomType>();
            var bare = new List<string>();
            int themed = 0;
            foreach (var l in TestHouses.Sample)
                foreach (var r in l.Rooms)
                {
                    if (!signature.TryGetValue(r.Type, out var kinds) || r.Area < 12f || r.Id == l.HiddenRoom) continue;
                    seen.Add(r.Type);
                    themed++;
                    bool has = l.Props.Any(p => kinds.Contains(p.Kind) && l.Plan.RoomAt(p.Position + Vector3.up * 0.3f)?.Id == r.Id);
                    if (!has) bare.Add($"seed {l.Seed}: {r.Type} {r.Area:0} m²");
                }
            Assert.Greater(seen.Count, 4, "the shared houses have themed rooms: " + string.Join(", ", seen));
            // a small room with doors on every wall may have no wall left for its piece: a few, not many (one in ten)
            Assert.LessOrEqual(bare.Count, Mathf.Max(3, themed / 10), $"themed rooms without their furniture (of {themed}): " + string.Join("; ", bare));
        }
    }
}
