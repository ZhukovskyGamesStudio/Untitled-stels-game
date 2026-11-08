using System.Collections;
using System.Linq;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        partial void RegisterExtraScenarios()
        {
            m_Scenarios["ui"] = UiScenario;
            m_Scenarios["floors"] = FloorsScenario;
            m_Scenarios["rooms"] = RoomsScenario;
        }

        /// <summary>Screenshots of the HUD in typical situations.</summary>
        IEnumerator UiScenario()
        {
            var layout = LevelGenerator.Current;
            Me.TeleportOwner(layout.PlayerSpawns[0] + Vector3.up * 0.05f, Quaternion.LookRotation(Flat(layout.HatchPosition - layout.PlayerSpawns[0])));
            Me.Motor.SetPitch(10f);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("spawn");

            var item = WorldItem.All.OrderBy(i => Vector3.Distance(i.transform.position, Me.transform.position)).FirstOrDefault();
            if (item != null)
            {
                var ip = item.transform.position + Vector3.up * 0.1f;
                LookAtFrom(ViewPoint(ip, 1.6f), ip);
                yield return new WaitForSeconds(0.6f);
                Log($"hover item: {(UI.InventoryUI.Local.Hover != null)} prompt='{UI.GameHUD.Instance.CurrentPrompt.Text}'");
                yield return Shot("hover_item");

                var inv = Me.GetComponent<PlayerInventory>();
                if (inv.FindSpot(item.Shape.Value, out var x, out var y, out var rot)) inv.PickupRpc(item.NetworkObject, x, y, rot);
                yield return new WaitForSeconds(0.5f);
            }

            Me.TeleportOwner(layout.HatchPosition + new Vector3(1.3f, 0.05f, 0), Quaternion.LookRotation(Vector3.left));
            Me.Motor.SetPitch(30f);
            yield return new WaitForSeconds(0.6f);
            Log($"at hatch prompt='{UI.GameHUD.Instance.CurrentPrompt.Text}'");
            yield return Shot("hatch_with_item");

            // Hit from the right side: arc should point right, light vignette afterwards.
            if (Me.IsServer)
            {
                Me.TakeHitServer(Me.transform.position + Me.transform.right * 1.5f);
                yield return new WaitForSeconds(0.25f);
                yield return Shot("hit_from_right");
                yield return new WaitForSeconds(2f);
                yield return Shot("low_hp");
            }
            yield return Shot("compact_inventory");
            UI.InventoryUI.ForceHandsModeForTest = true;
            yield return new WaitForSeconds(0.4f);
            yield return Shot("hands_mode");
            UI.InventoryUI.ForceHandsModeForTest = false;
            yield return UiScenarioExtra();
        }

        partial void UiScenarioExtraImpl(System.Collections.Generic.List<IEnumerator> steps);

        IEnumerator UiScenarioExtra()
        {
            var steps = new System.Collections.Generic.List<IEnumerator>();
            UiScenarioExtraImpl(steps);
            foreach (var s in steps) yield return s;
        }
    }
}
