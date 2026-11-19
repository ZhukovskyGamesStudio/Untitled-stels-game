using System.Linq;
using NUnit.Framework;
using Stealth.Game;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The team arrives in a basement (Basement): the stretch of the sewer where its car stopped — the way back behind an
    /// invisible wall, the car before the shut gate, a ladder in an alcove beside it climbing a shaft to the lid in the
    /// plant room's floor. The shaft's hole is exactly the lid's; the car drives in down its approach (DrivePath) and stops; the basement is inside (no gas).
    /// </summary>
    public class BasementTests
    {
        [OneTimeTearDown]
        public void Cleanup() => ActiveLevel.Clear();

        [Test]
        public void EveryHouseHasABasementUnderItsPlantRoom()
        {
            foreach (var l in TestHouses.Sample)
            {
                string name = $"seed {l.Seed}/{l.LevelIndex}";
                var f = l.Cellar;
                Assert.AreEqual(0f, Mathf.Repeat(l.BasementYaw, 90f), 1e-4f, $"{name}: the tunnel runs along the plan's axes");
                Assert.AreEqual(l.HatchPosition.y - Basement.Depth, f.Origin.y, 1e-4f);
                Assert.Less((f.ToWorld(Basement.Shaft) - l.HatchPosition).magnitude, 1e-3f, $"{name}: the shaft comes up at the lid");
                // the shaft's hole is 3 × 3 whole plan cells: its middle on a cell's middle
                Assert.AreEqual(HousePlan.Res / 2f, Mathf.Repeat(l.HatchPosition.x, HousePlan.Res), 1e-3f, $"{name}: the shaft on the cells (x)");
                Assert.AreEqual(HousePlan.Res / 2f, Mathf.Repeat(l.HatchPosition.z, HousePlan.Res), 1e-3f, $"{name}: the shaft on the cells (z)");
                Assert.AreEqual(3f, 2f * Basement.ShaftHalf / HousePlan.Res, 1e-4f);
                // the whole hole in the plant room (not into a wall)
                var room = l.Plan.RoomAt(l.HatchPosition + Vector3.up * 0.3f);
                Assert.IsNotNull(room, name);
                foreach (var c in new[] { new Vector3(1, 0, 1), new Vector3(-1, 0, 1), new Vector3(1, 0, -1), new Vector3(-1, 0, -1) })
                    Assert.AreSame(room, l.Plan.RoomAt(l.HatchPosition + c * (Basement.ShaftHalf + 0.1f) + Vector3.up * 0.3f), $"{name}: the lid's corner {c} in the room");
                Assert.IsTrue(Basement.Contains(f, l.EntryHatch), $"{name}: the stash is kept in the basement");
                Assert.AreEqual(-1, Hatch.IndexNear(l.EntryHatch), "no entry hatch to sit in");
                Assert.IsTrue(l.IsInside(l.EntryHatch + Vector3.up), "the basement is inside (no gas)");
                Assert.IsFalse(l.IsInside(l.EntryHatch + Vector3.up * 10f + Vector3.right * 500f), "far outside is outside");
                foreach (var s in l.PlayerSpawns) Assert.IsTrue(Basement.Contains(f, s + Vector3.up * 0.5f), $"{name}: a spawn outside the basement");
                Assert.IsTrue(Basement.Contains(f, Basement.CarPose(f).position + Vector3.up * 0.5f), $"{name}: the car stands in the basement");
                // the lid stands over the shaft, its hinge away from the side a climber steps off to
                var lid = l.Props.Single(p => p.Kind == PropKind.ShaftHatch);
                Assert.Less((lid.Position - l.HatchPosition).sqrMagnitude, 0.01f);
                Assert.Greater(Vector3.Dot(Quaternion.Euler(0f, lid.RotationY, 0f) * Vector3.forward, l.ShaftExit), 0.99f, $"{name}: the lid opens away from the way off the ladder");
                Assert.IsFalse(l.Props.Exists(p => p.Kind == PropKind.Hatch && (p.Position - l.HatchPosition).sqrMagnitude < 0.01f), "no hatch in the lobby any more");
            }
        }

        [Test]
        public void TheTunnelRunsUnderTheHouse()
        {
            // a shaft in a corner of a house: the tunnel goes in under it, not out under the street
            var house = new Rect(0f, 0f, 40f, 20f);
            foreach (var shaft in new[] { new Vector3(1.75f, 0f, 1.75f), new Vector3(38.25f, 0f, 1.75f), new Vector3(1.75f, 0f, 18.25f), new Vector3(38.25f, 0f, 18.25f) })
            {
                var f = Basement.Frame(shaft, Basement.YawUnder(shaft, house));
                var car = f.ToWorld(Basement.CarStop);
                var stretch = f.ToWorld(new Vector3(Basement.GateX / 2f, 0f, 0f));
                Assert.IsTrue(house.Contains(new Vector2(car.x, car.z)), $"shaft {shaft}: the car under the house");
                Assert.IsTrue(house.Contains(new Vector2(stretch.x, stretch.z)), $"shaft {shaft}: the car's stretch under the house");
            }
        }

        [Test]
        public void TheLadderClimbsFromTheAlcoveUpTheShaftToTheRoom()
        {
            var l = TestHouses.Get(1, 0);
            var f = l.Cellar;
            var path = Basement.ClimbPath(f, l.HatchPosition, l.ShaftExit);
            Assert.IsTrue(Basement.Contains(f, path[0] + Vector3.up * 0.5f), "starts in the basement");
            var foot = f.ToLocal(path[0]);
            Assert.AreEqual(Basement.WalkwayZ, foot.z, 1e-3f, "on the walkway in front of the alcove, off the car's way");
            var column = f.ToLocal(path[1]);
            Assert.Less(Mathf.Abs(column.x - Basement.AlcoveX), Basement.ShaftHalf - 0.35f, "the climber fits the shaft along the tunnel");
            Assert.Greater(column.z - 0.35f, Basement.ShaftZ - Basement.ShaftHalf, "and across it (the front wall)");
            Assert.Less(column.z + 0.35f, Basement.LadderZ, "clear of the rungs");
            Assert.AreEqual(path[1].x, path[2].x, 1e-4f, "straight up");
            Assert.AreEqual(path[1].z, path[2].z, 1e-4f);
            var end = path[path.Length - 1];
            Assert.AreEqual(l.HatchPosition.y + 0.05f, end.y, 1e-3f, "ends on the room's floor");
            var flat = new Vector2(end.x - l.HatchPosition.x, end.z - l.HatchPosition.z);
            Assert.Greater(Mathf.Max(Mathf.Abs(flat.x), Mathf.Abs(flat.y)), Basement.ShaftHalf + 0.3f, "beside the shaft, not over it");
            Assert.AreSame(l.Plan.RoomAt(l.HatchPosition + Vector3.up * 0.3f), l.Plan.RoomAt(end + Vector3.up * 0.3f), "in the same room, not in its wall");
        }

        [Test]
        public void TheCarDrivesDownTheApproachAndStopsBeforeTheGate()
        {
            // braking evenly to the stop from cruising
            Assert.AreEqual(0f, DrivePath.ToGo(0f, out float stopped), 1e-4f);
            Assert.AreEqual(0f, stopped, 1e-4f);
            Assert.AreEqual(DrivePath.BrakeDistance, DrivePath.ToGo(DrivePath.Brake, out float cruising), 1e-4f);
            Assert.AreEqual(DrivePath.Cruise, cruising, 1e-4f);
            float last = float.MaxValue, lastSpeed = float.MaxValue;
            for (float left = 12f; left >= 0f; left -= 0.1f)
            {
                float behind = DrivePath.ToGo(left, out float speed);
                Assert.LessOrEqual(behind, last + 1e-5f, "never backwards");
                Assert.LessOrEqual(speed, lastSpeed + 1e-5f, "never faster nearer the stop");
                last = behind;
                lastSpeed = speed;
            }
            // it changes tunnels in the approach, well behind the basement (the haze hides the basement from there)
            Assert.AreEqual(DrivePath.SwapX, DrivePath.InBasement(DrivePath.SwapLead, out _).position.x, 1e-3f);
            Assert.Less(DrivePath.SwapX, -Basement.BackSegments * SewerPlan.SegmentLength - 15f, "past the static back stretch: in the approach's tunnel");
            var f = Basement.Frame(new Vector3(10f, 0f, 10f), 90f);
            var stop = f.ToLocal(Basement.CarPose(f).position);
            Assert.AreEqual(stop.x, DrivePath.InBasement(0f, out _).position.x, 1e-4f, "it stops where the car stands");
            Assert.Less(stop.x + 2.8f, Basement.GateX - 0.35f, "its nose stops before the gate's frame");
            Assert.Less(Mathf.Abs(stop.x - Basement.AlcoveX), 2f, "the alcove right beside it");
            Assert.Greater(Vector3.Dot(Basement.CarPose(f).rotation * Vector3.forward, f.Dir(Vector3.right)), 0.99f, "facing the gate");
            // the change comes after the drive's own length and late enough for everyone to build the house
            Assert.AreEqual(16.5, Arrival.SwapAt(10.0, 12.0, 6.5f), 1e-6);
            Assert.AreEqual(11.0 + Arrival.ClientSlack, Arrival.SwapAt(0.0, 11.0, 6.5f), 1e-6);
        }

        [Test]
        public void TheDriveLeavesTheTunnelFromWhereTheCarStood()
        {
            var from = new Vector3(40f, 0.3f, 1.2f); // x, heading, z
            var start = DrivePath.InBranch(from, 2f, 0f, out float v0);
            Assert.AreEqual(40f, start.position.x, 1e-4f);
            Assert.AreEqual(1.2f, start.position.z, 1e-4f);
            Assert.AreEqual(2f, v0, 1e-4f);
            Assert.Greater(Vector3.Dot(start.rotation * Vector3.forward, new Vector3(Mathf.Cos(0.3f), 0f, Mathf.Sin(0.3f))), 0.999f, "heading as it stood");
            var settled = DrivePath.InBranch(from, 2f, DrivePath.Settle + 1f, out float v);
            Assert.AreEqual(DrivePath.Lane, settled.position.z, 1e-4f, "in the lane");
            Assert.Greater(Vector3.Dot(settled.rotation * Vector3.forward, Vector3.right), 0.999f, "straight along the tunnel");
            float x = start.position.x;
            for (float t = 0.1f; t < 10f; t += 0.1f)
            {
                var p = DrivePath.InBranch(from, 2f, t, out _).position;
                Assert.Greater(p.x, x, "always on");
                Assert.LessOrEqual(Mathf.Abs(p.z), 1.2f + 1e-4f, "never further out than it stood: inside the branch and through its reducer");
                x = p.x;
            }
        }

        [Test]
        public void TheCarChangesTunnelsAtTheSamePlaceOfThePattern([Values(5f, 40f, 88f)] float x0, [Values(0f, 4f)] float v0, [Values(0.0, 7.3, 30.0)] double late)
        {
            double departed = 100.0;
            var from = new Vector3(x0, 0f, 0.8f);
            double arrive = DrivePath.ArriveAtFor(departed, from, v0, departed + 9.0 + late);
            Assert.GreaterOrEqual(arrive, departed + 9.0 + late - 1e-6);
            double swap = arrive - DrivePath.SwapLead;
            float branchX = DrivePath.BranchXAt(departed, from, v0, arrive);
            Assert.GreaterOrEqual(branchX, DrivePath.TunnelFrom + DrivePath.ClearOfBranch - 1e-3f, "far past the branch: all round, only the endless tunnel");
            Assert.AreEqual(DrivePath.Cruise, DrivePath.SpeedAt((float)(swap - departed), v0), 1e-4f, "cruising in both");
            // the same place of the pattern in both tunnels, the same speed: nothing changes on screen
            float sewerU = branchX - DrivePath.TunnelFrom, basementU = DrivePath.InBasement(DrivePath.SwapLead, out float speed).position.x;
            float off = Mathf.Repeat(sewerU - basementU, DrivePath.Pattern);
            Assert.That(off < 0.02f || off > DrivePath.Pattern - 0.02f, $"pattern offset {off}");
            Assert.AreEqual(DrivePath.Cruise, speed, 1e-4f);
            Assert.AreEqual(DrivePath.Lane, DrivePath.InBranch(from, v0, (float)(swap - departed), out _).position.z, 1e-4f);
            Assert.AreEqual(DrivePath.Lane, DrivePath.InBasement(DrivePath.SwapLead, out _).position.z, 1e-4f);
            // the pattern itself: variant and lamp side by segment, the same for k and k ± 2
            for (int k = -9; k < 9; k++)
            {
                Assert.AreEqual(DrivePath.VariantOf(k), DrivePath.VariantOf(k + 2));
                Assert.AreEqual(DrivePath.LampSideOf(k), -DrivePath.LampSideOf(k + 1));
            }
        }

        [Test]
        public void TheAlcoveAndTheLidFitTheShaft()
        {
            // the kit's alcove (Tools/Blender/sewer.py kit_alcove): its shaft goes up to the house's floor, its ladder against the back wall
            var alcove = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Level/Sewer/Alcove.prefab");
            Assert.IsNotNull(alcove, "the alcove's segment");
            Bounds Of(string part) => alcove.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == part).Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            var shaft = Of("BrickAlcove");
            Assert.AreEqual(Basement.Depth, shaft.max.y, 0.03f, "the shaft's walls reach the floor");
            var ladder = Of("MetalLadder");
            float alcoveX = Basement.AlcoveX - (Basement.CarSegments - 1) * SewerPlan.SegmentLength;
            Assert.AreEqual(alcoveX, ladder.center.x, 0.05f, "the ladder in the alcove's middle");
            Assert.AreEqual(Basement.LadderZ - 0.03f, ladder.min.z, 0.02f, "its rungs where the climber is held in front of them");
            Assert.AreEqual(Basement.AlcoveBack, ladder.max.z, 0.02f, "fixed to the alcove's back wall");
            Assert.Greater(ladder.max.y, Basement.Depth - 0.2f, "up to just under the lid");
            Assert.IsNotNull(alcove.GetComponentInChildren<Light>(true), "a lamp in the shaft");
            Assert.IsTrue(alcove.GetComponentsInChildren<Transform>(true).Any(t => t.name == "MetalShutLid" && t.gameObject.activeSelf), "shut from below in the sewer");
            // the lid's frame lies right round the hole: inner edge at the hole's
            var hatch = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Level/ShaftHatch.prefab");
            var frames = hatch.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "Frame").Select(r => r.bounds).ToList();
            Assert.AreEqual(4, frames.Count);
            Assert.AreEqual(Basement.ShaftHalf, frames.Min(b => Mathf.Max(Mathf.Abs(b.center.x) - b.extents.x, Mathf.Abs(b.center.z) - b.extents.z)), 1e-3f, "the frame starts at the hole's edge");
            var plate = hatch.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "Plate").bounds;
            Assert.Greater(plate.size.x, 2f * Basement.ShaftHalf, "the lid covers the hole");
            Assert.Less(plate.size.x, 2f * Basement.ShaftHalf + 0.2f, "and rests on the frame");
        }

        [Test]
        public void TheBuiltBasementHasItsTunnelCarStopGateAndOpenShaft()
        {
            var l = TestHouses.Get(1, 0);
            var root = ActiveLevel.Load(l, AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset"));
            foreach (var lid in root.GetComponentsInChildren<ShaftHatch>(true)) lid.LidCollider.enabled = false; // the lid open
            Physics.SyncTransforms();
            var f = l.Cellar;
            var named = root.GetComponentsInChildren<Transform>(true).Where(t => t.parent == root.transform).ToList();
            Assert.AreEqual(1, named.Count(t => t.name == "BasementBackWall"), "the invisible wall behind the car");
            Assert.AreEqual(1, named.Count(t => t.name == "BasementGate"), "the gate ahead of it");
            Assert.AreEqual(1, named.Count(t => t.name == "BasementAlcove"), "the alcove beside it");
            var gate = named.Single(t => t.name == "BasementGate");
            Assert.IsFalse(gate.GetComponentsInChildren<Transform>(true).Any(t => t.name == "ConcreteHousing" && t.gameObject.activeInHierarchy), "no housing up through the floor");
            Assert.IsFalse(gate.GetComponentsInChildren<GateLever>(true).Any(g => g.gameObject.activeInHierarchy), "no lever here");
            var alcove = named.Single(t => t.name == "BasementAlcove");
            Assert.IsFalse(alcove.GetComponentsInChildren<Transform>(true).Any(t => t.name == "MetalShutLid" && t.gameObject.activeInHierarchy), "the real lid is in the floor");
            // looking down the hole: the alcove's floor 4.75 m down; just outside the hole: the room's floor
            float inner = Basement.ShaftHalf - 0.05f, outer = Basement.ShaftHalf + 0.05f;
            foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                var from = l.HatchPosition + Vector3.up * 2f;
                Assert.IsTrue(Physics.Raycast(from + d * inner, Vector3.down, out var down, 10f), "something under the hole");
                Assert.Less(down.point.y, l.HatchPosition.y - Basement.Depth + 0.3f, $"the hole is open at {d * inner}: hit {down.collider.name} at {down.point.y:0.00}");
                Assert.IsTrue(Physics.Raycast(from + d * outer, Vector3.down, out var floor, 10f));
                Assert.Greater(floor.point.y, l.HatchPosition.y - 0.25f, $"the floor right round the hole at {d * outer}: hit {floor.collider.name}");
            }
            // the car drives on the road between the wall and the gate
            var stop = Basement.CarPose(f).position;
            Assert.IsTrue(Physics.Raycast(stop + Vector3.up * 1.5f, Vector3.down, out var road, 3f), "a road under the car");
            Assert.AreEqual(stop.y, road.point.y, 0.1f);
            Assert.IsTrue(Physics.Raycast(stop + Vector3.up * 1f, f.Dir(Vector3.left), out var wall, 30f), "the way back is walled off");
            Assert.AreEqual(Basement.CarStop.x, wall.distance, 0.5f, "at the invisible wall");
        }

        [Test]
        public void TheLadderIsClimbedByWalkingIntoItWithWAndS()
        {
            var face = Vector3.left;
            // walking into it, facing it: on; walking away from it, or standing: not
            Assert.IsTrue(LadderMath.GetsOn(true, 1f, face, face, true, 0f, 4.9f));
            Assert.IsFalse(LadderMath.GetsOn(true, 1f, -face, face, true, 0f, 4.9f), "back to it");
            Assert.IsFalse(LadderMath.GetsOn(true, 0f, face, face, true, 0f, 4.9f), "just standing there");
            Assert.IsFalse(LadderMath.GetsOn(false, 1f, face, face, true, 0f, 4.9f), "out of reach");
            // stepping into the open shaft from above: it catches the falling body
            Assert.IsTrue(LadderMath.GetsOn(true, 0f, -face, face, false, 3.5f, 4.9f));
            // W up, S down, held on the column
            var up = LadderMath.Step(new Vector3(0.2f, 1f, 0f), Vector3.zero, 1f, 0.1f);
            Assert.AreEqual(LadderMath.Speed * 0.1f, up.y, 1e-4f);
            Assert.Less(up.x, 0f, "drawn to the rungs");
            Assert.AreEqual(-LadderMath.Speed * 0.1f, LadderMath.Step(Vector3.zero, Vector3.zero, -1f, 0.1f).y, 1e-4f);
            Assert.AreEqual(0f, LadderMath.Step(Vector3.zero, Vector3.zero, 0f, 0.1f).y, "no W or S: it hangs there");
            // looking away from the ladder (back to the rungs) W goes down, S up; facing it or side on, W goes up
            Assert.AreEqual(1f, LadderMath.Direction(face, face));
            Assert.AreEqual(-1f, LadderMath.Direction(-face, face));
            Assert.AreEqual(1f, LadderMath.Direction(Vector3.forward, face), "side on");
            // off at the top only through an open lid; off at the bottom going down onto the floor
            Assert.IsTrue(LadderMath.OffAtTop(4.7f, 4.9f, true));
            Assert.IsFalse(LadderMath.OffAtTop(4.7f, 4.9f, false), "the lid is shut: the head stops against it");
            Assert.IsTrue(LadderMath.OffAtBottom(-1f, true, 0.1f, 0f));
            Assert.IsFalse(LadderMath.OffAtBottom(1f, true, 0.1f, 0f), "climbing up from the floor");
        }

        [Test]
        public void TheSewerAfterAHouseShowsTheSameAlcoveShut()
        {
            var start = SewerPlan.Make(5, -1);
            Assert.IsFalse(start.Segments.Any(s => s.Alcove), "the run's first sewer: no house above");
            for (int seed = 1; seed < 6; seed++)
            {
                var p = SewerPlan.Make(seed, 2);
                var alcove = p.Segments.Where(s => s.Alcove).ToList();
                Assert.AreEqual(1, alcove.Count, "one alcove: the one the team climbed up from");
                Assert.AreEqual(p.GateX, alcove[0].X + SewerPlan.SegmentLength, 1e-4f, "the last segment before the gate, as in the basement");
                Assert.AreEqual(Basement.CarStop.x - Basement.GateX, p.CarX - p.GateX, 1e-4f, "the car stopped where it stopped down there");
                Assert.IsFalse(alcove[0].Junk.Any(j => j.side > 0), "nothing in front of the alcove");
            }
        }
    }
}
