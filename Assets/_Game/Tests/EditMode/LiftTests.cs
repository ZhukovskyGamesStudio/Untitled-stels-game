using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The apartment block's lift: a shaft one above the other through every floor with its doors on every landing, always
    /// in a block of six storeys or more; the cab goes only when called while it stands open, its doors open only where it
    /// stands and stay open there; nobody is shut in the doorway; a rider is carried the whole way.
    /// </summary>
    public class LiftTests
    {
        const float FloorTime = 2f, DoorTime = 1f;

        [Test]
        public void ACallSendsTheCabOnlyWhileItStandsOpen()
        {
            var s = LiftState.ParkedAt(0);
            Assert.AreEqual(LiftPhase.Open, LiftRules.Phase(s, 0, FloorTime, DoorTime));
            Assert.AreEqual(1f, LiftRules.DoorOpen(s, 0, 0, FloorTime, DoorTime), "parked: its doors stand open");
            Assert.AreEqual(0f, LiftRules.DoorOpen(s, 2, 0, FloorTime, DoorTime), "the other floors' doors are shut");
            Assert.AreEqual(s, LiftRules.Request(s, 0, 5, FloorTime, DoorTime), "already there: nothing happens");

            var go = LiftRules.Request(s, 3, 10, FloorTime, DoorTime);
            Assert.AreEqual(0, go.From);
            Assert.AreEqual(3, go.To);
            Assert.AreEqual(go, LiftRules.Request(go, 1, 11, FloorTime, DoorTime), "one trip at a time");

            // doors close on the ground floor, the cab rides 3 floors, the doors open on the third
            Assert.AreEqual(LiftPhase.Closing, LiftRules.Phase(go, 10.5, FloorTime, DoorTime));
            Assert.AreEqual(0.5f, LiftRules.DoorOpen(go, 0, 10.5, FloorTime, DoorTime), 1e-4f);
            Assert.AreEqual(0, LiftRules.StandsAt(go, 10.5, FloorTime, DoorTime));
            Assert.AreEqual(LiftPhase.Riding, LiftRules.Phase(go, 14, FloorTime, DoorTime));
            Assert.AreEqual(-1, LiftRules.StandsAt(go, 14, FloorTime, DoorTime));
            Assert.AreEqual(0f, LiftRules.DoorOpen(go, 0, 14, FloorTime, DoorTime));
            Assert.AreEqual(0f, LiftRules.DoorOpen(go, 3, 14, FloorTime, DoorTime), "no doors open while it rides");
            Assert.AreEqual(1.5f, LiftRules.CabFloor(go, 14, FloorTime, DoorTime), 1e-4f, "halfway at half the ride");
            Assert.AreEqual(LiftPhase.Opening, LiftRules.Phase(go, 17.5, FloorTime, DoorTime));
            Assert.AreEqual(LiftPhase.Open, LiftRules.Phase(go, 18.5, FloorTime, DoorTime));
            Assert.AreEqual(1f, LiftRules.DoorOpen(go, 3, 100, FloorTime, DoorTime), "and they stay open there");
            Assert.AreEqual(3f, LiftRules.CabFloor(go, 100, FloorTime, DoorTime));
            var back = LiftRules.Request(go, 1, 100, FloorTime, DoorTime);
            Assert.AreEqual(3, back.From, "the next trip leaves from where it stands");
        }

        [Test]
        public void SomeoneInTheDoorwayHoldsTheDoorsOpen()
        {
            var go = LiftRules.Request(LiftState.ParkedAt(2), 0, 10, FloorTime, DoorTime);
            var held = LiftRules.Hold(go, 10.8, FloorTime, DoorTime);
            Assert.AreEqual(1f, LiftRules.DoorOpen(held, 2, 10.8, FloorTime, DoorTime), "open again");
            Assert.AreEqual(LiftPhase.Closing, LiftRules.Phase(held, 11.5, FloorTime, DoorTime), "the trip waits");
            var riding = LiftRules.Hold(go, 13, FloorTime, DoorTime);
            Assert.AreEqual(go, riding, "once it rides, nothing to hold");
        }

        [Test]
        public void ARiderIsCarriedTheWholeWay()
        {
            var go = LiftRules.Request(LiftState.ParkedAt(0), 4, 0, FloorTime, DoorTime);
            float carried = 0f;
            for (double t = 0; t < 20; t += 0.02) carried += LiftRules.Carry(go, t, t + 0.02, FloorTime, DoorTime);
            Assert.AreEqual(4f * LevelMetrics.FloorHeight, carried, 1e-3f);
        }

        [Test]
        public void WhatMovesIsNeverBatched([Values("LiftDoor", "LiftCab", "VentFrame", "HighVentFrame")] string name)
        {
            // a batched mesh stays where it was merged: the lift's panels, its cab and a vent's grille must keep their own
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Level/{name}.prefab");
            Assert.IsNotNull(prefab, name);
            Transform[] moving = name switch
            {
                "LiftDoor" => new[] { prefab.GetComponent<LiftDoor>().PanelA, prefab.GetComponent<LiftDoor>().PanelB },
                "LiftCab" => new[] { prefab.GetComponent<LiftCab>().Car },
                _ => new[] { prefab.GetComponent<VentGrille>().Mounted.transform, prefab.GetComponent<VentGrille>().Removed.transform },
            };
            foreach (var t in moving)
                foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Assert.IsTrue(LevelBuilder.KeepsMoving(r), $"{name}: {r.name} would be static-batched");
                    Assert.IsFalse(PropBatches.IsShell(r, prefab.transform.parent), $"{name}: {r.name} would be merged into the shell");
                }
        }

        static IEnumerable<LevelLayout> Blocks(float chance)
        {
            var b = new BuildingSettings { LiftChance = chance };
            int found = 0;
            for (int seed = 1; found < 4 && seed < 400; seed++)
            {
                int house = seed % 5;
                if (HouseKinds.Of(seed, house) != HouseKind.Apartments) continue;
                found++;
                yield return LevelGenerator.Generate(seed, house, b);
            }
        }

        [Test]
        public void TheShaftStandsOneAboveTheOtherWithItsDoorsOnEveryLanding()
        {
            foreach (var l in Blocks(1f))
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                Assert.IsTrue(l.HasLift, $"{name}: a lift when the chance is certain");
                var plan = l.Plan;
                var well = plan.LiftWell;
                for (int f = 0; f < l.Floors; f++)
                {
                    for (int x = well.xMin; x < well.xMax; x++)
                        for (int z = well.yMin; z < well.yMax; z++)
                            Assert.AreEqual(CellKind.Pocket, plan.KindAt(f, x, z), $"{name}: floor {f}: the shaft is sealed, the same cells on every floor");
                    var doors = l.Doorways.Where(d => d.Kind == EdgeType.Lift && d.Floor == f).ToList();
                    Assert.AreEqual(1, doors.Count, $"{name}: floor {f}: one lift doorway");
                    var landing = plan.Rooms[Mathf.Max(doors[0].RoomA, doors[0].RoomB)];
                    Assert.AreEqual(RoomType.Corridor, landing.Type, $"{name}: floor {f}: onto the landing");
                    if (f > 0)
                    {
                        var below = l.Doorways.First(d => d.Kind == EdgeType.Lift && d.Floor == f - 1);
                        Assert.Less(new Vector2(doors[0].Position.x - below.Position.x, doors[0].Position.z - below.Position.z).magnitude, 0.01f,
                            $"{name}: floor {f}: right above the one below");
                    }
                }
                var w = l.LiftWell;
                foreach (var p in l.Props)
                    Assert.IsFalse(w.Contains(new Vector2(p.Position.x, p.Position.z)), $"{name}: nothing stands in the well ({p.Kind})");
                Assert.IsFalse(l.Props.Any(p => p.Kind == PropKind.ElevatorDoors), $"{name}: no painted-on lift doors any more");
            }
        }

        [Test]
        public void ATallBlockAlwaysHasALiftALowerOneMayNot()
        {
            int tall = 0, without = 0;
            foreach (var l in Blocks(0f))
            {
                if (l.Floors >= new BuildingSettings().LiftAlwaysFrom) { tall++; Assert.IsTrue(l.HasLift, $"{l.Seed}/{l.LevelIndex}: {l.Floors} storeys"); }
                else
                {
                    without++;
                    Assert.IsFalse(l.HasLift, $"{l.Seed}/{l.LevelIndex}: no chance, {l.Floors} storeys");
                    Assert.IsFalse(l.Doorways.Any(d => d.Kind == EdgeType.Lift), "no lift doorway without a lift");
                }
            }
            Assert.Greater(tall + without, 0);
        }
    }
}
