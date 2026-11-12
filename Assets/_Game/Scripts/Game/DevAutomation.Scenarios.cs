using System.Collections;
using System.Linq;
using Stealth.Enemies;
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
            m_Scenarios["menu"] = MenuScenario;
            m_Scenarios["floors"] = FloorsScenario;
            m_Scenarios["rooms"] = RoomsScenario;
            m_Scenarios["vent"] = VentScenario;
            m_Scenarios["perf"] = PerfScenario;
            m_Scenarios["gallery"] = GalleryScenario;
            m_Scenarios["hunter"] = HunterScenario;
            m_Scenarios["lasers"] = LasersScenario;
            m_Scenarios["radio"] = RadioScenario;
            m_Scenarios["hack"] = HackScenario;
            m_Scenarios["detect"] = DetectScenario;
            m_Scenarios["darkbox"] = DarkBoxScenario;
            m_Scenarios["newbots"] = NewBotsScenario;
            m_Scenarios["fog"] = FogScenario;
            m_Scenarios["styles"] = StylesScenario;
            m_Scenarios["map"] = MapScenario;
            m_Scenarios["sewer"] = SewerScenario;
            m_Scenarios["legs"] = LegsScenario;
            m_Scenarios["plate"] = PlateScenario;
            m_Scenarios["terminals"] = TerminalsScenario;
            m_Scenarios["cheats"] = CheatsScenario;
        }

        /// <summary>The F4 cheat panel (robots off, invulnerable) and the map outside the building.</summary>
        IEnumerator CheatsScenario()
        {
            var gm = GameManager.Instance;
            var panel = UI.CheatPanel.Instance;
            if (panel != null) panel.SetOpen(true);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("panel");
            Me.RequestCheat(Cheat.KillRobots, true);
            Me.RequestCheat(Cheat.Invulnerable, true);
            yield return new WaitForSeconds(0.4f);
            int hp = Me.Hp;
            if (Me.IsServer) Me.HitServer(Me.transform.position + Me.transform.right);
            yield return new WaitForSeconds(0.3f);
            Log($"cheats: robots off {Detector.All.Count(d => d.IsHacked)}/{Detector.All.Count}, invulnerable hp {hp} -> {Me.Hp}, panel open {UI.CheatPanel.IsOpen}");
            if (panel != null) panel.SetOpen(false);

            // outside: the yard round the player opens on the map, not further
            var l = ActiveLevel.Layout;
            Me.TeleportOwner(new Vector3(-3f, 0.05f, l.SizeZ / 2f), Quaternion.LookRotation(Vector3.right));
            yield return new WaitForSeconds(1.5f);
            var map = gm.Exploration;
            var here = ExplorationMap.TileOf(Me.transform.position);
            Log($"outside: tile under the player seen {map.IsRevealed(0, here.x, here.y)}, floor 0 seen {map.RevealedShare(0) * 100f:0.0}%");
            yield return Shot("outside_minimap");
            map.RevealAll();
            UI.GameHUD.Instance.SetBigMap(true);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("whole_map");
            UI.GameHUD.Instance.SetBigMap(false);
        }

        /// <summary>Screenshots of the HUD in typical situations.</summary>
        IEnumerator UiScenario()
        {
            var layout = ActiveLevel.Layout;
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
                Log($"hover item: {(UI.InventoryUI.Instance.Hover != null)} prompt='{UI.GameHUD.Instance.CurrentPrompt.Text}'");
                yield return Shot("hover_item");

                var inv = Me.GetComponent<PlayerInventory>();
                if (inv.FindSpot(item.Shape, out var x, out var y, out var rot)) inv.RequestPickup(item, x, y, rot);
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
                Me.HitServer(Me.transform.position + Me.transform.right * 1.5f);
                yield return new WaitForSeconds(0.25f);
                yield return Shot("hit_from_right");
                yield return new WaitForSeconds(2f);
                yield return Shot("low_hp");
            }
            yield return Shot("compact_inventory");
            DevFlags.ForceHandsMode = true;
            yield return new WaitForSeconds(0.4f);
            yield return Shot("hands_mode");
            DevFlags.ForceHandsMode = false;
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
