using NUnit.Framework;
using Stealth.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.Tests
{
    /// <summary>The HUD prefab as built: what fades must not switch off the buttons under it.</summary>
    public class HudPrefabTests
    {
        [Test]
        public void TheCheatPanelsButtonsCanBeClicked()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/HUD.prefab");
            var panel = hud.GetComponentInChildren<CheatPanel>(true);
            Assert.IsNotNull(panel);
            var buttons = panel.GetComponentsInChildren<Button>(true);
            Assert.IsNotEmpty(buttons, "the panel has buttons (or their template)");
            for (var t = panel.transform; t != null; t = t.parent)
            {
                if (!t.TryGetComponent<CanvasGroup>(out var g)) continue;
                Assert.IsTrue(g.interactable, $"{t.name}: a CanvasGroup that is not interactable turns the buttons under it off");
                Assert.IsTrue(g.blocksRaycasts, $"{t.name}: a CanvasGroup that lets raycasts through makes the buttons under it unclickable");
                if (g.ignoreParentGroups) break;
            }
        }
    }
}
