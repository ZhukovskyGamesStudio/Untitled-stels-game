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
            "GameNetwork.SteamTransport",
        };

        static IEnumerable<string> AllPrefabPaths() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs }).Select(AssetDatabase.GUIDToAssetPath);

        static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

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
                    if (Optional.Contains(id)) continue;
                    Assert.IsNotNull(p.objectReferenceValue, $"{path}: {c.name}/{id} is not assigned");
                }
            }
        }

        [Test]
        public void EveryKindHasAPrefabOfThatKind()
        {
            var gm = Load($"{Prefabs}/Network/Session.prefab").GetComponent<GameManager>();
            Assert.AreEqual(RobotRoster.KindCount, gm.RobotPrefabs.Length);
            for (int i = 0; i < gm.RobotPrefabs.Length; i++)
            {
                Assert.IsNotNull(gm.RobotPrefabs[i], $"{(RobotKind)i} has a prefab");
                Assert.AreEqual((RobotKind)i, gm.RobotPrefabs[i].GetComponent<Detector>().Kind, $"{gm.RobotPrefabs[i].name} is tagged with its kind");
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
    }
}
