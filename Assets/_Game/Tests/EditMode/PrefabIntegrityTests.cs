using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using Stealth.UI;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stealth.Tests
{
    /// <summary>
    /// The game instantiates pre-built prefabs, so a broken reference in a prefab is a runtime bug.
    /// These checks catch them in the editor: missing scripts, unassigned references, unregistered network prefabs.
    /// </summary>
    public class PrefabIntegrityTests
    {
        const string Prefabs = "Assets/_Game/Prefabs";
        const string ScenePath = "Assets/_Game/Scenes/Game.unity";

        /// <summary>References that are allowed to stay empty (optional by design).</summary>
        static readonly HashSet<string> Optional = new()
        {
            "SecurityCamera.Status.Light",  // the spot light shows the camera's status instead
            "RobotController.PatchPrefab",  // only the robots that leave goo, smoke or gas
            "RobotController.Tracer",       // only the robot with a gun
            "RobotController.Muzzle",
            "HazardView.Body",              // a puddle has a body, a cloud has particles
            "HazardView.Cloud",
            "GameNetwork.SteamTransport",
            "CheatPanel.Items.Array.data[0]", "CheatPanel.Items.Array.data[1]", "CheatPanel.Items.Array.data[2]", "CheatPanel.Items.Array.data[3]",
            "CheatPanel.Items.Array.data[4]", "CheatPanel.Items.Array.data[5]", "CheatPanel.Items.Array.data[6]", "CheatPanel.Items.Array.data[7]", // (no buttons for the old tetris pieces)
            "RobotController.Lidar",         // only the sweeper has a floor lidar
            "RobotController.CargoMesh",     // only workers carry loot
            "RobotController.CargoRenderer",
            "RobotController.CargoVisuals",
            "RobotAnimator.LiftArm",         // only workers have a lift
            "RobotAnimator.LiftTray",
            "Blob.m_Material",               // a UI graphic: empty is Unity's default UI material
            "AutoDoor.Swing",                // sliding doors have panels, the vault's round door a hinge instead
            "AutoDoor.PanelA",
            "AutoDoor.PanelB",
            "AutoDoor.SwingB",               // only the street doors have a second leaf
            "AutoDoor.Wheel",                // only the vault's door has a wheel to spin
            "AutoDoor.LockedPicture",        // only the storage room's door shows its terminal's picture
            "AutoDoor.OpenPicture",
            "AutoDoor.Barrier",              // only the vault's door keeps robots out by itself
            "AutoDoor.BlockerB",             // only a door-and-a-half's narrow leaf has a blocker of its own
            "LiftButton.Cab",                // a call button on a landing is not in the cab
            "AutoDoor.Number",               // only a flat's entry door carries its number
            "PropVariants.Frame",            // only shelves swap their books for a style's own model
            "VoicePlayback.Owner",           // the relay's loudspeaker plays a voice that is nobody's own
        };

        static IEnumerable<string> AllPrefabPaths() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs }).Select(AssetDatabase.GUIDToAssetPath);

        static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        [Test]
        public void TheScreensAreDrawnOverTheViewNotInTheWorld()
        {
            // (a nested canvas's renderMode is its root's: setting it on a child once made the whole HUD a world-space canvas)
            foreach (var name in new[] { "HUD", "Menu", "Inventory" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/UI/{name}.prefab");
                Assert.IsNotNull(prefab, name);
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, prefab.GetComponent<Canvas>().renderMode, name);
            }
        }

        [Test]
        public void NoMissingScripts([ValueSource(nameof(AllPrefabPaths))] string path)
        {
            var go = Load(path);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject), $"{path}: {t.name}");
        }

        [Test]
        public void GameComponentsHaveTheirReferences([ValueSource(nameof(AllPrefabPaths))] string path)
        {
            var go = Load(path);
            foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (c == null || c.GetType().Namespace == null || !c.GetType().Namespace.StartsWith("Stealth")) continue;
                var so = new SerializedObject(c);
                var p = so.GetIterator();
                while (p.NextVisible(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference || p.name == "m_Script") continue;
                    string id = $"{c.GetType().Name}.{p.propertyPath}";
                    if (Optional.Contains(id) || IsGap(id)) continue;
                    Assert.IsNotNull(p.objectReferenceValue, $"{path}: {c.name}/{id} is not assigned");
                }
            }
        }

        /// <summary>A slot of a table by robot kind whose number no robot has any more (<see cref="RobotRoster.KindCount"/>).</summary>
        static bool IsGap(string id)
        {
            var m = System.Text.RegularExpressions.Regex.Match(id, @"^(GameManager.RobotPrefabs|GameHUD.HackKindIcons|GameHUD.HackKindPictures|MapView.KindIcons)\.Array\.data\[(\d+)\]$");
            return m.Success && !RobotRoster.Kinds.Contains((RobotKind)int.Parse(m.Groups[2].Value));
        }

        [Test]
        public void EveryKindHasAPrefabOfThatKind()
        {
            var gm = Load($"{Prefabs}/Network/Session.prefab").GetComponent<GameManager>();
            Assert.AreEqual(RobotRoster.KindCount, gm.RobotPrefabs.Length);
            foreach (var kind in RobotRoster.Kinds)
            {
                var robot = gm.RobotPrefabs[(int)kind];
                Assert.IsNotNull(robot, $"{kind} has a prefab");
                Assert.AreEqual(kind, robot.GetComponent<Detector>().Kind, $"{robot.name} is tagged with its kind");
            }
            Assert.IsNotNull(gm.ItemPrefab);
            Assert.IsNotNull(gm.LaserPrefab);
            Assert.IsNotNull(gm.Art);
        }

        [Test]
        public void NetworkPrefabsAreRegisteredWithHashes()
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/_Game/Data/NetworkPrefabs.asset");
            var registered = list.PrefabList.Select(p => p.Prefab).ToList();
            foreach (var path in AllPrefabPaths())
            {
                var no = Load(path).GetComponent<NetworkObject>();
                if (no == null) continue;
                Assert.Contains(no.gameObject, registered, $"{path} is in the network prefab list");
                Assert.AreNotEqual(0u, no.PrefabIdHash, $"{path} has a hash");
            }
        }

        [Test]
        public void LevelArtIsComplete()
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            var so = new SerializedObject(art);
            var p = so.GetIterator();
            while (p.NextVisible(true))
                if (p.propertyType == SerializedPropertyType.ObjectReference && p.name != "m_Script")
                    Assert.IsNotNull(p.objectReferenceValue, $"LevelArt.{p.name}");
            foreach (PropKind k in System.Enum.GetValues(typeof(PropKind)))
                Assert.IsNotNull(art.Prop(k), $"prop {k}");
        }

        [Test]
        public void MovingPartsHavePivots()
        {
            foreach (var path in AllPrefabPaths().Where(p => p.Contains("/Robots/") || p.Contains("/Sensors/")))
                foreach (var t in Load(path).GetComponentsInChildren<Transform>(true))
                {
                    bool moving = t.name.StartsWith("AccentLeg") || t.name.StartsWith("AccentWheel") || t.name.StartsWith("AccentRotor");
                    if (!moving || t.name.EndsWith(RobotAnimator.PivotSuffix)) continue;
                    Assert.IsTrue(t.parent.name.EndsWith(RobotAnimator.PivotSuffix), $"{path}: {t.name} hangs on a pivot");
                }
        }

        [Test]
        public void InventoryScreenHasACellPerSlot()
        {
            var ui = Load($"{Prefabs}/UI/Inventory.prefab").GetComponent<InventoryUI>();
            int slots = 0;
            for (int y = 0; y < InventoryLayout.Height; y++)
                for (int x = 0; x < InventoryLayout.Width; x++)
                    if (InventoryLayout.IsCell(x, y)) slots++;
            Assert.AreEqual(slots, ui.Cells.Length);
        }

        [Test]
        public void GameSceneHoldsTheInterfaceScreens()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                foreach (var screen in new[] { "HUD", "Inventory", "Menu", "StashBoard", "AudioDirector" })
                {
                    var go = roots.FirstOrDefault(r => r.name == screen);
                    Assert.IsNotNull(go, $"{screen} is in the scene");
                    Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(go), $"{screen} is a prefab instance, not a copy");
                }
                Assert.IsTrue(roots.Any(r => r.GetComponent<UnityEngine.EventSystems.EventSystem>() != null), "an event system for the menu");
                Assert.IsTrue(roots.Any(r => r.GetComponent<NetworkManager>() != null));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// Every robot and device, hacked any way (the state comes the same to every peer): its own hum goes quiet, and comes
        /// back when it wakes; and every sound source on it belongs to something that falls silent while it is hacked (its
        /// loop, a guard's voice loop, a relay's loudspeaker) — a new sound on a robot cannot go on humming after a hack.
        /// </summary>
        [Test]
        public void EveryRobotAndDeviceFallsSilentWhenHacked()
        {
            int robots = 0;
            var folders = new[] { "Assets/_Game/Prefabs/Robots", "Assets/_Game/Prefabs/Sensors" };
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", folders).Select(AssetDatabase.GUIDToAssetPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<Detector>() == null) continue;
                var loops = prefab.GetComponentsInChildren<Stealth.Audio.LoopSound>(true);
                var owned = new HashSet<AudioSource>(loops.Select(l => l.Source));
                foreach (var g in prefab.GetComponentsInChildren<Stealth.Audio.GuardSounds>(true)) owned.Add(g.Source);
                foreach (var v in prefab.GetComponentsInChildren<Stealth.Audio.VoicePlayback>(true)) owned.Add(v.Source);
                foreach (var source in prefab.GetComponentsInChildren<AudioSource>(true))
                    Assert.IsTrue(owned.Contains(source), $"{path}: {source.name}'s sound is nobody's — it would play on after a hack");

                var go = Object.Instantiate(prefab);
                try
                {
                    var detector = go.GetComponent<Detector>();
                    var mine = go.GetComponentsInChildren<Stealth.Audio.LoopSound>(true);
                    var state = DetectorSnapshot.Initial;
                    state.Hacked = true;
                    detector.ApplyReplicated(state);
                    foreach (var loop in mine) Assert.IsFalse(loop.On, $"{path}: hacked, {loop.name} still hums");
                    state.Hacked = false;
                    detector.ApplyReplicated(state);
                    foreach (var loop in mine) Assert.IsTrue(loop.On, $"{path}: awake again, {loop.name} hums again");
                    robots++;
                }
                finally { Object.DestroyImmediate(go); }
            }
            Assert.Greater(robots, 20, "every robot and device was looked at");
        }

        /// <summary>
        /// The car's three shelves (the trunk upgrade): each a shelf of its own number with its grid's corner on its board's
        /// top — the grid runs along the board and lies within it, clear of its ends — and a piece for every cell.
        /// </summary>
        [Test]
        public void TheCarsShelvesLieOnTheirBoards()
        {
            var car = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Vehicles/Car.prefab");
            Assert.IsNotNull(car);
            var shelves = car.GetComponentsInChildren<Stealth.Vehicles.CarShelf>(true).OrderBy(s => s.Number).ToArray();
            Assert.AreEqual(Stealth.Vehicles.CarShelves.Count, shelves.Length, "a shelf per trunk level");
            var grid = Stealth.Vehicles.CarShelves.GridSize;
            for (int i = 0; i < shelves.Length; i++)
            {
                var shelf = shelves[i];
                Assert.AreEqual(i, shelf.Number);
                Assert.IsNotNull(shelf.Board, $"shelf {i}: its board");
                Assert.AreEqual(Stealth.Vehicles.CarShelves.Columns * Stealth.Vehicles.CarShelves.Rows, shelf.Pieces.Length, $"shelf {i}: a piece per cell");
                var board = shelf.GetComponentsInChildren<MeshFilter>(true).First(f => f.name.Contains($"Shelf{i + 1}_Board"));
                var b = board.sharedMesh.bounds;
                // the grid's far corners in the board mesh's own space: on its top, within its length and depth
                foreach (var corner in new[] { Vector3.zero, new Vector3(grid.x, 0f, 0f), new Vector3(0f, 0f, -grid.y), new Vector3(grid.x, 0f, -grid.y) })
                {
                    var p = board.transform.InverseTransformPoint(shelf.Board.TransformPoint(corner));
                    var lo = b.min - Vector3.one * 0.01f;
                    var hi = b.max + Vector3.one * 0.01f;
                    Assert.IsTrue(p.x >= lo.x && p.x <= hi.x && p.y >= lo.y && p.y <= hi.y && p.z >= lo.z && p.z <= hi.z,
                        $"shelf {i}: the grid's corner {corner} lies off its board ({p} not in {b})");
                }
                Assert.AreEqual(0f, Vector3.Dot(shelf.Board.up, Vector3.up) - 1f, 1e-3f, $"shelf {i}: the grid lies flat");
            }
        }
    }
}
