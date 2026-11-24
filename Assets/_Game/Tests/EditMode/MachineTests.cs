using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Stealth.Items;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Machines one can tell at a glance: the arcade game, the snack machine and its goods, keyboards, small displays, the electrical panel, the low shelf.</summary>
    public class MachineTests
    {
        static LevelArt Art => AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
        static GameObject Prop(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Props/{name}.prefab");
        static MeshRenderer[] Parts(GameObject go, string prefix) => go.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith(prefix)).ToArray();

        [Test]
        public void TheArcadeGameAndTheSnackMachineHaveARealisticModelOfTheirOwn([Values("ArcadeMachine", "VendingMachine")] string name)
        {
            var go = Object.Instantiate(Prop(name));
            try
            {
                var parts = go.GetComponent<StyleParts>();
                Assert.IsNotNull(parts);
                Assert.IsTrue(parts.NotWhereFurnished, "the abandoned facility keeps its own");
                Assert.AreEqual(1, parts.Own.Length);
                Assert.AreEqual(1, parts.Default.Length);
                Assert.IsEmpty(parts.Own.Concat(parts.Default).SelectMany(m => m.GetComponentsInChildren<Collider>(true)), "either model hides: what stops a player is on the piece itself");
                Assert.IsNotNull(go.GetComponent<BoxCollider>());
                var model = parts.Own[0];
                Assert.IsNotEmpty(Parts(model, "Glow"));
                foreach (var r in Parts(model, "Glow")) Assert.AreEqual("MachineGlow", r.sharedMaterial.name);
                foreach (var r in Parts(model, "Print")) Assert.AreEqual("MachinePrint", r.sharedMaterial.name);
                parts.Show("S16_Derelict");
                Assert.IsTrue(model.activeSelf && !parts.Default[0].activeSelf);
                parts.Show("S06_Storybook");
                Assert.IsTrue(!model.activeSelf && parts.Default[0].activeSelf);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void EverySpiralHoldsGoodsAndMachinesDiffer()
        {
            var go = Object.Instantiate(Prop("VendingMachine"));
            try
            {
                Physics.SyncTransforms();
                var v = go.GetComponent<PropVariants>();
                Assert.AreEqual(25, v.Slots.Length);
                var box = go.GetComponent<BoxCollider>();
                var outer = new Bounds(box.center, box.size);
                foreach (var slot in v.Slots)
                {
                    Assert.AreEqual(3, slot.childCount);
                    foreach (var r in slot.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        Assert.AreEqual("MachineLit", r.sharedMaterial.name, "goods with their labels");
                        var c = go.transform.InverseTransformPoint(r.transform.TransformPoint(r.GetComponent<MeshFilter>().sharedMesh.bounds.center));
                        Assert.IsTrue(outer.Contains(c), $"{slot.name}: goods outside the machine at {c}");
                    }
                }
                string Shown(int seed)
                {
                    v.Dress(seed);
                    return string.Join(",", v.Slots.Select(s =>
                    {
                        for (int i = 0; i < s.childCount; i++) if (s.GetChild(i).gameObject.activeSelf) return i;
                        return -1;
                    }));
                }
                Assert.AreEqual(Shown(5), Shown(5), "the same on every peer");
                Assert.AreNotEqual(Shown(5), Shown(6));
                StringAssert.Contains("-1", Shown(5) + Shown(6) + Shown(7), "something is sold out");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void DesksHaveRealKeyboards([Values("SupervisorDesk", "StandingDesk", "DoctorDesk", "ConsoleDesk", "NurseStationDesk", "Terminal")] string name)
        {
            var keys = Parts(Prop(name), "Keys");
            Assert.IsNotEmpty(keys);
            foreach (var r in keys)
            {
                Assert.AreEqual("KeyCaps", r.sharedMaterial.name);
                Assert.IsNotNull(r.sharedMaterial.GetTexture("_BaseMap"));
                var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                Assert.Greater(mesh.triangles.Length / 3, 900, "a hundred caps");
                Assert.IsTrue(mesh.uv.All(u => u.x >= 0f && u.x <= 1f && u.y >= 0f && u.y <= 1f), "on the picture");
            }
        }

        [Test]
        public void EveryDisplayShowsItsPicture()
        {
            // the two lists of pictures (the drawing's and the game's) are the same
            var py = File.ReadAllText("Tools/Blender/machine_atlas.py");
            var drawn = Regex.Matches(Regex.Match(py, @"DISPLAYS = \[([^\]]*)\]").Groups[1].Value, "\"(\\w+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            Assert.AreEqual(ScreenLooks.DisplayCols * ScreenLooks.DisplayRows, drawn.Length);
            Assert.AreEqual(System.Array.IndexOf(drawn, "plain"), ScreenLooks.PlainDisplay);
            Assert.AreEqual(System.Array.IndexOf(drawn, "clock"), ScreenLooks.DisplayOf(PropKind.Microwave));
            Assert.AreEqual(System.Array.IndexOf(drawn, "kiosk"), ScreenLooks.DisplayOf(PropKind.InfoKiosk));
            Assert.AreEqual(System.Array.IndexOf(drawn, "print3d"), ScreenLooks.DisplayOf(PropKind.Printer3d));
            Assert.IsNotNull(Art.Displays.GetTexture("_EmissionMap"));
            int kinds = 0;
            foreach (PropKind kind in System.Enum.GetValues(typeof(PropKind)))
            {
                int picture = ScreenLooks.DisplayOf(kind);
                if (picture < 0) continue;
                kinds++;
                var screens = Art.Prop(kind).GetComponentsInChildren<MeshRenderer>(true).Where(r => r.sharedMaterial == Art.Screens[0]).ToArray();
                Assert.IsNotEmpty(screens, $"{kind} has no screen to show it on");
                float w = 1f / ScreenLooks.DisplayCols, h = 1f / ScreenLooks.DisplayRows;
                var cell = new Rect(picture % ScreenLooks.DisplayCols * w, 1f - (picture / ScreenLooks.DisplayCols + 1) * h, w, h);
                Assert.IsTrue(screens.Any(r => ScreenFaces.Of(r.GetComponent<MeshFilter>().sharedMesh, picture).uv.Count(u => cell.Contains(u)) >= 3), $"{kind}: its picture is on none of its faces");
            }
            Assert.Greater(kinds, 20);
        }

        [Test]
        public void ADisplayOnAPanelIsTheRightWayRoundForWhoeverStandsBeforeIt()
        {
            // a face lying flat, seen from +Z (the front): its left is +X, its bottom the near edge
            var v = new[] { new Vector3(0.1f, 0f, 0.06f), new Vector3(0.1f, 0f, -0.06f), new Vector3(-0.1f, 0f, -0.06f), new Vector3(-0.1f, 0f, 0.06f) };
            var up = Vector3.Cross(v[1] - v[0], v[2] - v[0]).y > 0f;
            var uv = ScreenFaces.Fit(v, up ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 }, ScreenFaces.DisplaySide, true);
            void Same(Vector2 want, Vector2 got, string what) => Assert.Less(Vector2.Distance(want, got), 1e-3f, $"{what}: {got}");
            Same(new Vector2(0f, 0f), uv[0], "near left");
            Same(new Vector2(0f, 1f), uv[1], "far left");
            Same(new Vector2(1f, 1f), uv[2], "far right");
            Same(new Vector2(1f, 0f), uv[3], "near right");
        }

        [Test]
        public void TheLowShelfCarriesAPlayerAndHoldsDifferentThings()
        {
            var go = Prop("LowShelf");
            var v = go.GetComponent<PropVariants>();
            Assert.AreEqual(6, v.Slots.Length);
            foreach (var slot in v.Slots)
            {
                Assert.GreaterOrEqual(slot.childCount, 5);
                Assert.IsEmpty(slot.GetComponentsInChildren<Collider>(true));
                Assert.GreaterOrEqual(slot.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.sharedMaterial.name).Distinct().Count(), 6, "each thing its own stuff");
            }
            Assert.IsNotEmpty(go.GetComponentsInChildren<MeshCollider>(true));
        }

        [Test]
        public void TheCabinetsPullsGoWithTheirDrawers()
        {
            var go = Prop("Cabinet");
            for (int k = 0; k < 3; k++)
                foreach (var role in new[] { "BodyDrawer", "AccentDrawer", "PrintDrawer" })
                    Assert.AreEqual(1, Parts(go, role + k).Length, role + k);
            Assert.IsTrue(Storage.Follows("PrintDrawer2", "Drawer", out int drawer));
            Assert.AreEqual(2, drawer);
        }

        [Test]
        public void TheElectricalPanelsHangInTheCorridors()
        {
            Assert.IsNotNull(Art.ElectricPanel);
            var l = TestHouses.Get(99991, 4);
            ActiveLevel.Load(l, Art);
            try
            {
                var spots = UtilityPlan.PanelSpots(l, ActiveLevel.Shell);
                Assert.IsNotEmpty(spots);
                foreach (var (at, facing) in spots)
                {
                    Assert.AreEqual(1f, facing.magnitude, 1e-3f);
                    var room = l.Plan.RoomAt(at + facing * 0.4f);
                    Assert.IsTrue(room != null && room.Type is RoomType.Corridor or RoomType.Hall);
                }
                Assert.AreEqual(spots.Count, ActiveLevel.Root.GetComponentsInChildren<Transform>(true).Count(t => t.name == "ElectricPanel" && t.parent == ActiveLevel.Root.transform));
            }
            finally { ActiveLevel.Clear(); }
        }
    }
}
