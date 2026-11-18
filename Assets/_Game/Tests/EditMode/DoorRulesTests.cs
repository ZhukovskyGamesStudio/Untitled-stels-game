using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// Doors as a real building has them (<see cref="DoorRules"/>): a flat's entry door always there and opening out onto the
    /// landing, a bathroom's opening out of it, a door off a corridor opening into the room; hinged doors almost everywhere,
    /// glass sliding ones only where the tech is; no leaf sweeping into a wall, a stair, furniture or another door.
    /// </summary>
    public class DoorRulesTests
    {
        static readonly List<LevelLayout> s_Houses = new();

        /// <summary>Two houses of each kind with its own doors (apartments, hospital, shop) and the shared sample of offices.</summary>
        static List<LevelLayout> Houses()
        {
            if (s_Houses.Count > 0) return s_Houses;
            s_Houses.AddRange(TestHouses.Sample);
            foreach (var kind in new[] { HouseKind.Apartments, HouseKind.Hospital, HouseKind.Garage })
            {
                int found = 0;
                for (int seed = 1; found < 2 && seed < 400; seed++)
                {
                    int house = seed % 5;
                    if (HouseKinds.Of(seed, house) != kind) continue;
                    s_Houses.Add(TestHouses.Get(seed, house));
                    found++;
                }
            }
            return s_Houses;
        }

        static string Name(LevelLayout l) => $"{l.Seed}/{l.LevelIndex} {l.Kind}";

        static PlanRoom Into(LevelLayout l, int i)
        {
            var pose = l.AutoDoors[i];
            var spec = l.DoorLooks[i];
            var z = pose.rotation * Vector3.forward * (spec.Back ? -1f : 1f);
            return l.RoomAt(pose.position + z * 0.6f + Vector3.up * 0.3f);
        }

        [Test]
        public void LeavesFillTheOpening()
        {
            float clear = LevelMetrics.ClearWidth(LevelMetrics.DoorWidth);
            var (main, second) = DoorRules.Leaves(DoorKind.Wood, clear);
            Assert.AreEqual(clear - 2f * DoorRules.Lining, main + second, 1e-4f);
            Assert.That(main, Is.InRange(0.85f, 1.0f), "the main leaf: a player (0.7 m) passes it alone");
            Assert.Less(second, main);
            var (a, b) = DoorRules.Leaves(DoorKind.WoodDouble, clear);
            Assert.AreEqual(a, b, 1e-4f, "double doors: twin leaves");
        }

        [Test]
        public void ABothWaysDoorSwingsAwayFromWhoeverComes()
        {
            Assert.AreEqual(-1f, AutoDoor.AwayFrom(Vector3.zero, Vector3.forward, new Vector3(0.3f, 0f, 1.5f)));
            Assert.AreEqual(1f, AutoDoor.AwayFrom(Vector3.zero, Vector3.forward, new Vector3(-0.3f, 0f, -1.5f)));
        }

        [Test]
        public void EveryDoorHasItsLook()
        {
            foreach (var l in Houses())
                Assert.AreEqual(l.AutoDoors.Count, l.DoorLooks.Count, Name(l));
        }

        [Test]
        public void EveryFlatHasItsEntryDoorOpeningOntoTheLanding()
        {
            int checkedDoors = 0;
            foreach (var l in Houses().Where(h => h.Kind == HouseKind.Apartments))
            {
                foreach (var d in l.Doorways)
                {
                    if (!DoorRules.IsFlatEntry(l, d)) continue;
                    int i = l.AutoDoors.FindIndex(p => (p.position - d.Position).sqrMagnitude < 0.01f);
                    Assert.GreaterOrEqual(i, 0, $"{Name(l)}: a flat without its entry door at {d.Position}");
                    Assert.AreEqual(DoorKind.Entry, l.DoorLooks[i].Kind, $"{Name(l)}: {d.Position}");
                    Assert.AreEqual(RoomType.Corridor, Into(l, i)?.Type, $"{Name(l)}: the entry door at {d.Position} opens out onto the landing");
                    checkedDoors++;
                }
            }
            Assert.Greater(checkedDoors, 0, "flats were checked");
        }

        [Test]
        public void BathroomsAndToiletsAlwaysHaveADoorOpeningOutOfTheBathroom()
        {
            int bathrooms = 0;
            foreach (var l in Houses())
                foreach (var d in l.Doorways)
                {
                    if (d.Kind != EdgeType.Door || d.Exterior || d.RoomA < 0 || d.RoomB < 0) continue;
                    var a = l.Plan.Rooms[d.RoomA];
                    var b = l.Plan.Rooms[d.RoomB];
                    if (a.Type != RoomType.Bathroom && b.Type != RoomType.Bathroom && a.Type != RoomType.Restroom && b.Type != RoomType.Restroom) continue;
                    if (l.RobotSpawns.Count == 0) continue; // a quiet house has no doors at all
                    int i = l.AutoDoors.FindIndex(p => (p.position - d.Position).sqrMagnitude < 0.01f);
                    Assert.GreaterOrEqual(i, 0, $"{Name(l)}: a toilet without a door at {d.Position}");
                    if (a.Type != RoomType.Bathroom && b.Type != RoomType.Bathroom) continue;
                    if (!DoorRules.Hinged(l.DoorLooks[i].Kind)) continue; // the vault's own door (a bathroom made the vault)
                    bathrooms++;
                    Assert.AreNotEqual(RoomType.Bathroom, Into(l, i)?.Type, $"{Name(l)}: the bathroom's door at {d.Position} opens out of it");
                }
            Assert.Greater(bathrooms, 0, "flats' bathrooms were checked");
        }

        [Test]
        public void DoorsOffCorridorsMostlyOpenIntoTheRoom()
        {
            int off = 0, into = 0;
            foreach (var l in Houses())
                for (int i = 0; i < l.AutoDoors.Count; i++)
                {
                    var spec = l.DoorLooks[i];
                    if (!DoorRules.Hinged(spec.Kind) || DoorRules.BothWays(spec.Kind) || spec.Kind is DoorKind.Entry) continue;
                    var d = DoorRules.DoorwayAt(l, l.AutoDoors[i].position);
                    if (d.RoomA < 0 || d.RoomB < 0) continue;
                    var a = l.Plan.Rooms[d.RoomA];
                    var b = l.Plan.Rooms[d.RoomB];
                    bool ca = a.Type == RoomType.Corridor, cb = b.Type == RoomType.Corridor;
                    if (ca == cb || a.Type is RoomType.Bathroom || b.Type is RoomType.Bathroom || a.Area < 6f || b.Area < 6f) continue;
                    off++;
                    if (Into(l, i)?.Type != RoomType.Corridor) into++;
                }
            Assert.Greater(off, 20);
            Assert.GreaterOrEqual(into, off * 0.85f, $"{into} of {off} doors off corridors open into the room");
        }

        [Test]
        public void HingedDoorsAlmostEverywhereSlidingOnlyWhereTheTechIs()
        {
            int hinged = 0, all = 0;
            foreach (var l in Houses())
                for (int i = 0; i < l.DoorLooks.Count; i++)
                {
                    var k = l.DoorLooks[i].Kind;
                    if (k is DoorKind.Vault or DoorKind.Storage or DoorKind.Vehicle) continue;
                    all++;
                    if (DoorRules.Hinged(k)) hinged++;
                    if (k == DoorKind.Slide && l.Kind is HouseKind.Apartments or HouseKind.Hospital or HouseKind.Garage)
                        Assert.Fail($"{Name(l)}: a sliding door in a {l.Kind}");
                }
            Assert.GreaterOrEqual(hinged, all * 0.75f, $"{hinged} of {all} doors on hinges");
        }

        [Test]
        public void NoLeafSweepsIntoAWallAStairFurnitureOrAnotherDoor()
        {
            int clashes = 0;
            var where = new List<string>();
            foreach (var l in Houses())
            {
                int c = DoorRules.CountClashes(l);
                if (c > 0) where.Add($"{Name(l)}: {c}");
                clashes += c;
            }
            Assert.LessOrEqual(clashes, 2, string.Join("; ", where));
        }

        static readonly string[] HingedPrefabs = { "WoodDoor", "WoodDoubleDoor", "NarrowDoor", "ShopDoor", "FlatDoor", "EntranceDoor", "ClinicDoor", "ClinicSwingDoor" };

        /// <summary>The leaves' renderers of a door (not the frame, the lamps or the locks), bounds in the door's own frame.</summary>
        static Bounds LeafBounds(AutoDoor door, Transform hinge)
        {
            var b = new Bounds();
            bool any = false;
            foreach (var r in hinge.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.transform.IsChildOf(hinge.Find("Leaf"))) continue;
                foreach (var c in Corners(r.bounds))
                {
                    var p = door.transform.InverseTransformPoint(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (int i = 0; i < 8; i++)
                yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
        }

        [Test]
        public void HingedLeavesShutInTheOpeningAndOpenToTheirSide([ValueSource(nameof(HingedPrefabs))] string name, [Values(false, true)] bool right)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Level/{name}.prefab");
            Assert.IsNotNull(prefab, name);
            var go = Object.Instantiate(prefab);
            try
            {
                var door = go.GetComponent<AutoDoor>();
                Assert.IsNotNull(door.Swing, $"{name}: a hinged door");
                if (right) door.HingeRight();
                float half = door.Width / 2f, wall = LevelMetrics.WallThickness / 2f;
                door.ShowSwing(0f, 0f);
                foreach (var hinge in new[] { door.Swing, door.SwingB })
                {
                    var shut = LeafBounds(door, hinge);
                    Assert.That(shut.min.x, Is.GreaterThanOrEqualTo(-half - 0.01f), $"{name}: shut, within the opening");
                    Assert.That(shut.max.x, Is.LessThanOrEqualTo(half + 0.01f), $"{name}: shut, within the opening");
                    Assert.That(Mathf.Max(Mathf.Abs(shut.min.z), Mathf.Abs(shut.max.z)), Is.LessThanOrEqualTo(wall + 0.07f), $"{name}: shut, in the wall's plane (handles stand out)");
                }
                // the main leaf's hinges on −X (+X when hung right): its handle at the other edge
                var main = door.Swing.position.x - go.transform.position.x;
                Assert.AreEqual(right ? 1f : -1f, Mathf.Sign(main), $"{name}: the main leaf's hinges");
                var lever = door.Swing.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name is "LeverA" or "PullA" or "PushPlate");
                if (lever != null) Assert.Greater(Mathf.Abs(lever.position.x - door.Swing.position.x), 0.5f, $"{name}: the handle by the free edge, away from the hinges");
                door.ShowSwing(1f, 1f, 1f);
                foreach (var hinge in new[] { door.Swing, door.SwingB })
                {
                    var open = LeafBounds(door, hinge);
                    Assert.Greater(open.max.z, 0.5f, $"{name}: open, out into the room on +Z");
                    Assert.Greater(open.min.z, -wall, $"{name}: open, nothing on the other side");
                    Assert.That(open.min.x, Is.GreaterThanOrEqualTo(-half - 0.07f), $"{name}: open, standing at its jamb (a handle may reach past it)");
                    Assert.That(open.max.x, Is.LessThanOrEqualTo(half + 0.07f), $"{name}: open, standing at its jamb (a handle may reach past it)");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
