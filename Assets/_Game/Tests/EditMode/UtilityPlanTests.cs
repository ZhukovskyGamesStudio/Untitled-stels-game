using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The house's services: ducts in the corridors, a panel on every floor, sockets — never in a doorway; light switches by the doors.</summary>
    public class UtilityPlanTests
    {
        static LevelLayout s_Layout;

        [OneTimeTearDown]
        public void TearDown() => ActiveLevel.Clear();

        static List<UtilityPart> Plan(out LevelLayout layout)
        {
            if (s_Layout == null || ActiveLevel.Layout != s_Layout)
            {
                s_Layout = TestHouses.Get(99991, 4);
                ActiveLevel.Load(s_Layout, AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset"));
            }
            layout = s_Layout;
            return UtilityPlan.Make(layout, ActiveLevel.Shell);
        }

        [Test]
        public void EveryFloorHasItsServices()
        {
            var parts = Plan(out var l);
            Assert.AreEqual(parts.Count, UtilityPlan.Make(l, ActiveLevel.Shell).Count, "the same from the same house");
            Assert.IsTrue(parts.Any(p => p.Slot == UtilitySlot.Duct), "ducts in the corridors");
            Assert.IsTrue(parts.Any(p => p.Slot == UtilitySlot.Grille), "with grilles");
            Assert.IsTrue(parts.Any(p => p.Slot == UtilitySlot.Plate), "sockets");
            for (int f = 0; f < l.Floors; f++)
                if (l.Rooms.Any(r => r.Floor == f && r.Type is RoomType.Corridor or RoomType.Hall))
                    Assert.IsTrue(parts.Any(p => p.Slot == UtilitySlot.Panel && LevelLayout.FloorOf(p.Center.y) == f), $"an electrical panel on floor {f}");
            // ducts run between the doors' tops and the ceiling
            foreach (var p in parts.Where(p => p.Slot == UtilitySlot.Duct))
            {
                float y = p.Center.y - LevelLayout.FloorY(LevelLayout.FloorOf(p.Center.y - 1.5f)); // (the duct is near the ceiling: its floor from below)
                Assert.That(y - p.Size.y / 2f, Is.GreaterThan(LevelMetrics.DoorHeight), "over the doors");
                Assert.That(y + p.Size.y / 2f, Is.LessThanOrEqualTo(LevelMetrics.WallHeight + 1e-3f), "under the ceiling");
            }
        }

        [Test]
        public void NothingHangsInADoorway()
        {
            var parts = Plan(out _);
            foreach (var o in ActiveLevel.Shell.Openings)
            {
                var n = new Vector3(-o.Along.z, 0f, o.Along.x);
                // the opening's own volume: its width along the wall, the wall's thickness, from its floor to its top
                foreach (var p in parts)
                {
                    var d = p.Center - o.Center;
                    float along = Mathf.Abs(Vector3.Dot(d, o.Along)), across = Mathf.Abs(Vector3.Dot(d, n));
                    float halfAlong = Mathf.Abs(Vector3.Dot(p.Size, new Vector3(Mathf.Abs(o.Along.x), 0f, Mathf.Abs(o.Along.z)))) / 2f;
                    float halfAcross = Mathf.Abs(Vector3.Dot(p.Size, new Vector3(Mathf.Abs(n.x), 0f, Mathf.Abs(n.z)))) / 2f;
                    bool inside = along < o.Width / 2f - halfAlong - 0.01f && across < LevelMetrics.WallThickness / 2f + halfAcross
                        && d.y - p.Size.y / 2f < o.Height - 0.01f && d.y + p.Size.y / 2f > 0.01f;
                    Assert.IsFalse(inside, $"{p.Slot} at {p.Center} sits in the opening at {o.Center}");
                }
            }
        }

        [Test]
        public void LightSwitchesHangBesideTheDoorsInsideTheirRooms()
        {
            Plan(out var l);
            var spots = UtilityPlan.SwitchSpots(l, ActiveLevel.Shell);
            Assert.AreEqual(spots.Count, UtilityPlan.SwitchSpots(l, ActiveLevel.Shell).Count, "the same on every peer: the index is the switch");
            Assert.Greater(spots.Select(s => s.Room).Distinct().Count(), 3, "rooms get switches");
            foreach (var s in spots)
            {
                var room = l.Rooms[s.Room];
                Assert.AreNotEqual(RoomType.Corridor, room.Type, "not in corridors");
                float floorY = LevelLayout.FloorY(room.Floor);
                Assert.AreEqual(1.11f, s.Position.y - floorY, 0.01f, "at hand height");
                var inside = new Vector3(s.Position.x, floorY + 0.5f, s.Position.z) + s.Facing * 0.3f;
                Assert.AreSame(room, l.Plan.RoomAt(inside), $"the switch at {s.Position} faces into its room");
                foreach (var o in ActiveLevel.Shell.Openings)
                {
                    if (Mathf.Abs(o.Center.y - floorY) > 0.5f) continue;
                    var d = s.Position - o.Center;
                    var n = new Vector3(-o.Along.z, 0f, o.Along.x);
                    bool inOpening = Mathf.Abs(Vector3.Dot(d, o.Along)) < o.Width / 2f + 0.04f && Mathf.Abs(Vector3.Dot(d, n)) < LevelMetrics.WallThickness / 2f + 0.05f;
                    Assert.IsFalse(inOpening, $"the switch at {s.Position} sits in the opening at {o.Center}");
                }
            }
        }

        [Test]
        public void ASwitchTurnsALitRoomDarkAndADarkRoomLit()
        {
            Assert.IsTrue(LightMath.SwitchedOn(startedDark: false, switched: false));
            Assert.IsFalse(LightMath.SwitchedOn(startedDark: false, switched: true));
            Assert.IsFalse(LightMath.SwitchedOn(startedDark: true, switched: false));
            Assert.IsTrue(LightMath.SwitchedOn(startedDark: true, switched: true));
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            Assert.IsNotNull(art.LightSwitch, "the switch prefab");
            var sw = art.LightSwitch.GetComponent<LightSwitch>();
            Assert.IsNotNull(sw, "it is used with E");
            Assert.IsNotNull(sw.Rocker, "its rocker shows the state");
            Assert.IsNotNull(sw.Led, "its lamp is found in the dark");
            Assert.IsEmpty(art.LightSwitch.GetComponentsInChildren<Collider>(), "it stops nothing");
        }

        [Test]
        public void FacesAreWoundOutwards()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = Enumerable.Range(0, System.Enum.GetValues(typeof(UtilitySlot)).Length).Select(_ => new List<int>()).ToArray();
            UtilityPlan.AddFaces(UtilityPart.Box(Vector3.zero, new Vector3(1f, 2f, 3f), UtilitySlot.Duct), verts, normals, uvs, tris);
            UtilityPlan.AddFaces(UtilityPart.Pipe(Vector3.zero, Vector3.up * 2f, 0.05f, UtilitySlot.Pipe), verts, normals, uvs, tris);
            UtilityPlan.AddFaces(UtilityPart.Pipe(Vector3.zero, Vector3.right * 2f, 0.05f, UtilitySlot.Copper), verts, normals, uvs, tris);
            Assert.AreEqual(36, tris[(int)UtilitySlot.Duct].Count, "a box: six sides");
            foreach (var t in tris)
                for (int i = 0; i < t.Count; i += 3)
                {
                    Vector3 a = verts[t[i]], b = verts[t[i + 1]], c = verts[t[i + 2]];
                    Assert.Greater(Vector3.Dot(Vector3.Cross(b - a, c - a), normals[t[i]]), 0f, "clockwise seen from outside");
                }
        }
    }
}
