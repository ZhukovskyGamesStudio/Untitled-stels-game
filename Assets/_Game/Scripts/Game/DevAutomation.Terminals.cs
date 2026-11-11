using System.Collections;
using System.Linq;
using Stealth.Enemies;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        /// <summary>
        /// Security terminals and "I'm stuck" (<c>LEVEL=3 Tools/dev.sh run terminals</c>): the camera terminal shows two
        /// cameras, the hacking terminal hacks the robot on its screen (a charge is spent); then the player is put inside a
        /// wall and gets out with "I'm stuck".
        /// </summary>
        IEnumerator TerminalsScenario()
        {
            if (!Me.IsServer) yield break;
            DevFlags.Peaceful = true;
            DevFlags.FreezeRobots = true;
            yield return new WaitForSeconds(0.5f);
            var terminals = SecurityTerminal.All.ToList();
            Log($"terminals: {string.Join(", ", terminals.Select(t => $"{t.Kind} #{t.Index} at {t.transform.position}"))}");
            if (terminals.Count == 0) { Log("FAIL: no terminals in the house"); yield break; }
            foreach (var t in terminals)
            {
                var front = t.Front + t.transform.forward * 0.4f + Vector3.up * 0.05f;
                Me.TeleportOwner(front, Quaternion.LookRotation(-t.transform.forward));
                Me.Motor.SetPitch(20f);
                yield return new WaitForSeconds(0.5f);
                bool near = SecurityTerminal.Near(Me.transform.position, Me.Hands) == t;
                Log($"{t.Kind}: in reach {near}, prompt '{UI.GameHUD.Instance.CurrentPrompt.Text}'");
                yield return Shot($"{t.Kind}_desk");
                DevFlags.AutoInteract = t.Kind == TerminalKind.Hacking;
                Me.StartTerminal(t, waitRelease: false);
                yield return new WaitForSeconds(0.5f);
                var first = Me.TerminalView;
                Log($"{t.Kind}: shows {(first != null ? first.DisplayName : "nothing")} ({Me.TerminalViewNumber}/{Me.TerminalViewCount}), camera at {Me.Camera.transform.position}");
                yield return Shot($"{t.Kind}_view_1");
                if (t.Kind == TerminalKind.Cameras)
                {
                    Me.NextTerminalViewForTest();
                    yield return new WaitForSeconds(0.4f);
                    yield return Shot($"{t.Kind}_view_2");
                }
                else
                {
                    int before = t.Charges;
                    yield return new WaitForSeconds(Configs.Tuning.Player.Interaction.TerminalHackTime + 0.6f);
                    Log($"Hacking: {(first != null ? first.DisplayName : "-")} hacked {first != null && first.IsHacked}, charges {before} -> {t.Charges}");
                    if (first == null || !first.IsHacked || t.Charges != before - 1) Log("FAIL: the hacking terminal did not hack");
                    yield return Shot($"{t.Kind}_view_hacked");
                }
                DevFlags.AutoInteract = false;
                Me.StopTerminal();
                yield return new WaitForSeconds(0.3f);
            }

            // The stash board (world-space text, drawn after the style filter) is still hidden by the walls.
            var board = FindFirstObjectByType<UI.StashBoard>();
            if (board != null)
            {
                var b = board.transform.position;
                var lobby = new Vector2Int(Mathf.FloorToInt(b.x / LevelMetrics.Cell), Mathf.FloorToInt(b.z / LevelMetrics.Cell));
                foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    var n = lobby + d;
                    if (n.x < 0 || n.y < 0 || n.x >= ActiveLevel.Layout.Width || n.y >= ActiveLevel.Layout.Height) continue;
                    var spot = ActiveLevel.Layout.CellCenter(0, n.x, n.y) + new Vector3(d.x, 0, d.y) * 1.5f;
                    if (!Physics.Linecast(spot + Vector3.up * 1.6f, b, Perception.SightMask, QueryTriggerInteraction.Ignore)) continue;
                    LookAtFrom(new Pose(spot + Vector3.up * 0.05f, Quaternion.LookRotation(Flat(b - spot))), b);
                    yield return new WaitForSeconds(0.4f);
                    Log("board behind a wall: shot taken (the board must not show through the wall)");
                    yield return Shot("board_behind_wall");
                    break;
                }
            }

            // "I'm stuck": inside a wall between two rooms, away from its middle (where a doorway could be).
            var layout = ActiveLevel.Layout;
            var wall = new Vector3(LevelMetrics.Cell, LevelLayout.FloorY(0) + 0.05f, LevelMetrics.Cell * 0.5f + 2.6f);
            Me.TeleportOwner(wall, Quaternion.identity);
            yield return new WaitForSeconds(0.3f);
            var stuckAt = Me.transform.position;
            Me.RequestUnstuck();
            yield return new WaitForSeconds(0.3f);
            var freed = Me.transform.position;
            bool clear = !Physics.CheckCapsule(freed + Vector3.up * 0.45f, freed + Vector3.up * 1.4f, 0.33f, Physics.DefaultRaycastLayers & ~(1 << 2), QueryTriggerInteraction.Ignore)
                         || Physics.OverlapCapsule(freed + Vector3.up * 0.45f, freed + Vector3.up * 1.4f, 0.33f).All(c => c.GetComponentInParent<PlayerController>() != null);
            Log($"unstuck: from {stuckAt} to {freed} ({Vector3.Distance(stuckAt, freed):0.00} m), standing free {clear}");
            if (!clear) Log("FAIL: still stuck");
            yield return Shot("unstuck");
        }
    }
}
