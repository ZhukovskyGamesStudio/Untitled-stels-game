using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Enemies;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The new enemy mode against the old one (LevelGenerator.ClassicEnemies): no hounds, hunters or wardens; guards rest on
    /// the docks of the robots' charging room — a human tech room the robots took over — until an alarm, hunt, and go
    /// back; traps guard the valuable rooms.
    /// </summary>
    public class EnemyModeTests
    {
        static IEnumerable<LevelLayout> Houses(bool classicEnemies, int count = 18)
        {
            for (int k = 0; k < count; k++)
                yield return LevelGenerator.Generate(4111 + k * 97, 2 + k % 4, null, null, 2, null, false, classicEnemies);
        }

        [Category("Slow")] // generates many houses (Tools/dev.sh: SLOW=1 runs it)
        [Test]
        public void TheNewModeHasNoHoundsHuntersOrWardens()
        {
            foreach (var l in Houses(false))
            {
                Assert.IsFalse(l.ClassicEnemies);
                Assert.IsFalse(l.RobotSpawns.Any(r => RobotRoster.OldOnly(r.Kind)), $"seed {l.Seed}: an old-mode robot");
                Assert.AreEqual(0, l.Wardens.Count, $"seed {l.Seed}: warden posts");
            }
            // the old mode still has them somewhere
            Assert.IsTrue(Houses(true).Any(l => l.RobotSpawns.Any(r => r.Kind is RobotKind.Hound or RobotKind.Warden)), "the old mode lost its hounds and wardens");
        }

        [Category("Slow")] // generates many houses (Tools/dev.sh: SLOW=1 runs it)
        [Test]
        public void EveryHouseHasAChargingRoomTakenFromTheHumans()
        {
            foreach (var l in Houses(false))
            {
                Assert.GreaterOrEqual(l.ChargingRoom, 0, $"seed {l.Seed}: no charging room");
                var room = l.Plan.Rooms[l.ChargingRoom];
                Assert.AreEqual(RoomType.ChargingStation, room.Type);
                Assert.Greater(ChargingRooms.Preference(l.ChargingFrom), 0f, $"seed {l.Seed}: taken from a {l.ChargingFrom}");
                var kinds = l.Props.Where(p => l.Plan.RoomAt(p.Position + Vector3.up * 0.3f)?.Id == room.Id).Select(p => p.Kind).ToList();
                Assert.Contains(PropKind.ChargingDock, kinds, $"seed {l.Seed}: no dock");
                int guards = l.RobotSpawns.Count(r => r.Kind == RobotKind.Guard);
                Assert.GreaterOrEqual(l.ChargingDocks.Count, guards, $"seed {l.Seed}: a resting spot for every guard");
                // no two guards on one spot
                var spots = l.RobotSpawns.Where(r => r.Kind == RobotKind.Guard).Select(r => r.Position).ToList();
                for (int i = 0; i < spots.Count; i++)
                    for (int j = i + 1; j < spots.Count; j++)
                        Assert.Greater((spots[i] - spots[j]).magnitude, 0.9f, $"seed {l.Seed}: two guards on one dock");
            }
        }

        [Category("Slow")] // generates many houses (Tools/dev.sh: SLOW=1 runs it)
        [Test]
        public void GuardsRestOnTheDocks()
        {
            foreach (var l in Houses(false))
            {
                var guards = l.RobotSpawns.Where(r => r.Kind == RobotKind.Guard).ToList();
                Assert.GreaterOrEqual(guards.Count, 2, $"seed {l.Seed}: sleeping guards");
                foreach (var g in guards)
                {
                    Assert.IsTrue(g.Docked, $"seed {l.Seed}: a guard on patrol");
                    Assert.AreEqual(l.ChargingRoom, l.Plan.RoomAt(g.Position + Vector3.up * 0.3f)?.Id, $"seed {l.Seed}: a guard rests outside the charging room");
                }
            }
            foreach (var l in Houses(true, 6))
                Assert.IsFalse(l.RobotSpawns.Any(r => r.Docked), "the old mode's guards patrol");
        }

        [Category("Slow")] // generates many houses (Tools/dev.sh: SLOW=1 runs it)
        [Test]
        public void TheValuableRoomsAreGuardedByTraps()
        {
            foreach (var l in Houses(false))
            {
                // (the rooms by the team's way in get no ward: Wards.NearHatch)
                int candidates = Wards.Ranked(l).Count(r => r.Floor != 0 || Vector3.Distance(r.Center, l.HatchPosition) >= Wards.NearHatch);
                Assert.GreaterOrEqual(l.WardedRooms.Count, Mathf.Min(Wards.Count(new WardSettings(), l.LevelIndex), candidates), $"seed {l.Seed}");
                // (right by the lobby nothing hostile may start — DifficultyTests — so a vault there goes unguarded)
                bool ByHatch(PlanRoom r) => r.Floor == 0 && Vector3.Distance(r.Center, l.HatchPosition) < 12.5f;
                if (l.VaultRoom >= 0 && !ByHatch(l.Plan.Rooms[l.VaultRoom])) Assert.Contains(l.VaultRoom, l.WardedRooms, $"seed {l.Seed}: the vault is not guarded");
                foreach (var p in l.Props.Where(p => p.Kind == PropKind.Cage))
                {
                    var cage = l.Plan.RoomAt(p.Position + Vector3.up * 0.3f);
                    if (!ByHatch(cage)) Assert.Contains(cage.Id, l.WardedRooms, $"seed {l.Seed}: the hostage's room is not guarded");
                }
                // every guarded room has something watching it or its way in
                foreach (int id in l.WardedRooms)
                {
                    var r = l.Plan.Rooms[id];
                    bool laser = l.LaserSpots.Any(s => l.Doorways.Any(d => (d.RoomA == id || d.RoomB == id) && (d.Position - s.Position).magnitude < 0.5f));
                    bool watched = laser || l.CameraSpots.Any(c => l.Plan.RoomAt(new Vector3(c.position.x, LevelLayout.FloorY(r.Floor) + 0.3f, c.position.z))?.Id == id)
                        || l.RobotSpawns.Any(s => s.Kind is RobotKind.MotionSensor or RobotKind.PressurePlate
                            && l.Plan.RoomAt(new Vector3(s.Position.x, LevelLayout.FloorY(r.Floor) + 0.3f, s.Position.z))?.Id == id);
                    Assert.IsTrue(watched, $"seed {l.Seed}: guarded room {r.Type} has no watcher");
                }
            }
        }

        // ------------------------------------------------------------------ the guard's shift

        [Test]
        public void ARestingGuardSleepsThroughEverythingButAnAlarm()
        {
            var body = new FakeBody { Position = Vector3.zero, Arrived = true };
            var traits = new RobotTraits { AlarmHuntTime = 60f };
            var brain = new RobotBrain(new RobotSenses { HearingMultiplier = 1f }, traits, body, new System.Random(1));
            brain.SetDock(Vector3.zero, Vector3.forward, 0f);
            var player = new FakeIntruder { ClientId = 1, Position = new Vector3(0, 0, 2) };

            brain.OnSensed(new Sighting { Target = player, Rate = 5f }, 1f, 0.5f);
            brain.OnNoise(player.Position, 1f, player, 1f);
            brain.Investigate(player.Position, false, 1f);
            brain.Tick(1f, 0.1f);
            Assert.IsTrue(brain.Resting, "seen, heard, radioed: still asleep");
            Assert.AreEqual(0f, brain.Suspicion);
            Assert.AreEqual(0, body.PatrolRequests, "no patrol while resting");

            var alarm = new Vector3(20f, 0f, 5f);
            brain.Alarm(alarm, 2f);
            Assert.AreEqual(GuardShift.Hunting, brain.Shift);
            Assert.AreEqual(RobotState.Investigate, brain.State, "runs to the alarm");
            Assert.AreEqual(alarm, body.Destination);
            Assert.AreEqual(traits.ChaseSpeed * 0.85f, body.Speed, 1e-4f);
        }

        [Test]
        public void AfterTheHuntItWalksBackToItsDockAndRests()
        {
            var body = new FakeBody { Position = Vector3.zero, Arrived = true };
            var traits = new RobotTraits { AlarmHuntTime = 60f, InvestigateLookTime = 3f };
            var brain = new RobotBrain(new RobotSenses(), traits, body, new System.Random(1));
            var dock = new Vector3(1f, 0f, 1f);
            brain.SetDock(dock, Vector3.forward, 0f);
            brain.Alarm(new Vector3(30f, 0f, 0f), 10f);
            body.Position = new Vector3(30f, 0f, 0f);
            // looks around at the alarm, then patrols the house
            for (float t = 10f; t < 15f; t += 0.1f) brain.Tick(t, 0.1f);
            Assert.AreEqual(RobotState.Patrol, brain.State);
            Assert.AreEqual(GuardShift.Hunting, brain.Shift, "still hunting");
            // a second alarm restarts the hour of the hunt
            brain.Alarm(new Vector3(32f, 0f, 0f), 50f);
            for (float t = 50f; t < 109f; t += 0.5f) brain.Tick(t, 0.5f);
            Assert.AreEqual(GuardShift.Hunting, brain.Shift, "60 s after the last alarm, not the first");
            for (float t = 109f; t < 112f; t += 0.5f) brain.Tick(t, 0.5f);
            Assert.AreEqual(GuardShift.Returning, brain.Shift);
            Assert.AreEqual(dock, body.Destination, "heads home");
            body.Position = dock;
            body.Arrived = true;
            brain.Tick(113f, 0.1f);
            Assert.IsTrue(brain.Resting, "back on its dock");
        }

        [Test]
        public void WhenTheTimeIsUpItLeavesItsDockForGood()
        {
            var body = new FakeBody { Position = Vector3.zero, Arrived = true };
            var traits = new RobotTraits { AlarmHuntTime = 10f };
            var brain = new RobotBrain(new RobotSenses(), traits, body, new System.Random(1));
            brain.SetDock(Vector3.zero, Vector3.forward, 0f);
            brain.WakeForGood(5f);
            Assert.IsFalse(brain.Docked);
            Assert.IsFalse(brain.Resting);
            Assert.Greater(body.PatrolRequests, 0, "it sets off on patrol");
            // no hunt timer runs out any more: it never heads home
            for (float t = 5f; t < 100f; t += 0.5f) brain.Tick(t, 0.5f);
            Assert.IsFalse(brain.Resting, "it never rests again");
            brain.OnSensed(new Sighting { Target = new FakeIntruder { Position = new Vector3(0, 0, 2) }, Rate = 5f }, 100f, 0.5f);
            Assert.Greater(brain.Suspicion, 0f, "and it sees again");
        }

        [Test]
        public void AnUndockedRobotTreatsAnAlarmAsAlways()
        {
            var body = new FakeBody { Position = Vector3.zero };
            var brain = new RobotBrain(new RobotSenses(), new RobotTraits(), body, new System.Random(1));
            brain.Alarm(new Vector3(5f, 0f, 0f), 1f);
            Assert.IsFalse(brain.Docked);
            Assert.AreEqual(RobotState.Investigate, brain.State);
        }
    }
}
