using System.Collections.Generic;
using System.Threading.Tasks;
using Stealth.Configs;
using Stealth.Game;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.UI
{
    /// <summary>
    /// The ways on in the sewer (on the prefab Level/Sewer): opens as many tunnels as the route has ways from here and fills
    /// the panel over each one — short: the house it leads to (kind and number), its floors and guards, the countdown once
    /// the car stands in that tunnel. A panel comes up fully when you look at its tunnel. Presenter only: the vote itself is decided by the server from where players stand.
    /// </summary>
    public class SewerRoute : MonoBehaviour
    {
        [Tooltip("Panels come up fully when the view is within this angle of them.")]
        public float LookAngle = 28f;

        SewerLocation m_Sewer;
        readonly Dictionary<int, string> m_Previews = new();
        readonly Dictionary<int, ObjectiveKind> m_Jobs = new();
        /// <summary>The houses being generated for their previews (on worker threads: three houses at once were a long frame).</summary>
        readonly Dictionary<int, Task<LevelLayout>> m_Making = new();
        int m_PreviewsLoc = -1;

        void Awake() => m_Sewer = GetComponent<SewerLocation>();

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Route == null || m_Sewer == null) return;
            var options = gm.RouteOptions;
            m_Sewer.ShowWays(options.Count);
            var me = PlayerController.Local;
            var cam = me != null && me.Camera != null ? me.Camera.transform : null;
            for (int tunnel = 0; tunnel < m_Sewer.Branches.Length; tunnel++)
            {
                int way = SewerBranches.WayOf(tunnel, options.Count);
                var sign = m_Sewer.Branches[tunnel].Sign;
                if (way < 0 || sign == null) continue;
                int lane = options[way];
                var node = gm.Route.Node(gm.Level.Index + 1, lane);
                sign.Title.text = Loc.F("route.title", RouteNode.KindName(node.Kind), gm.Level.Index + 2);
                string preview = Preview(node, gm), fuel = gm.Quota > 0 ? Loc.F("route.fuel", gm.CostOf(lane)) : null;
                sign.Body.text = fuel == null ? preview : preview.Length > 0 ? preview + " · " + fuel : fuel; // every way costs its own fuel
                sign.ShowIcons(node.Kind, m_Jobs.TryGetValue(node.Seed, out var job) ? job : ObjectiveKind.None);

                string voters = "";
                foreach (var p in PlayerController.All)
                    if (gm.Votes.All.TryGetValue(p.ClientId, out int l) && l == lane)
                        voters += (voters.Length > 0 ? ", " : "") +
                                  $"<color=#{ColorUtility.ToHtmlStringRGB(UiPalette.InkOf(PlayerLooks.TeamColor(PlayerLooks.SlotOf(p.ClientId))))}>{p.Nick}</color>";
                bool going = gm.GoAt > 0 && gm.ChosenLane == lane;
                bool waiting = !going && gm.ChosenLane == lane; // chosen: the car waits for everyone to get in
                string status = going ? Loc.F("route.going", Mathf.CeilToInt(Mathf.Max(0f, (float)(gm.GoAt - Session.ServerTime))))
                    : waiting ? (gm.Collected < gm.CostOf(lane) ? Loc.F("banner.fuel_short", gm.Collected, gm.CostOf(lane)) : Loc.T("route.board"))
                    : voters.Length > 0 ? Loc.F("route.here", voters) : "";
                sign.Votes.text = status;
                sign.Body.enabled = status.Length == 0; // the countdown takes the body's line
                sign.Frame.color = going || waiting ? UiPalette.MarkOrange : voters.Length > 0 ? UiPalette.MarkBlue : UiPalette.PaperShade;

                // Fully visible when looked at, otherwise only a hint of it.
                float target = 0.6f;
                if (cam != null)
                {
                    var to = sign.transform.position - cam.position;
                    if (Vector3.Angle(cam.forward, to) < LookAngle && to.magnitude < 26f) target = 1f;
                }
                if (going || waiting || voters.Length > 0) target = 1f;
                sign.Group.alpha = Mathf.MoveTowards(sign.Group.alpha, target, Time.deltaTime * 3f);
            }
        }

        /// <summary>What the team would face there (generated from the house's seed on a worker thread, cached; empty until it is made).</summary>
        string Preview(RouteNode node, GameManager gm)
        {
            if (m_PreviewsLoc != Loc.Version) { m_Previews.Clear(); m_PreviewsLoc = Loc.Version; }
            if (m_Previews.TryGetValue(node.Seed, out var text)) return text;
            if (!m_Making.TryGetValue(node.Seed, out var making))
            {
                var level = Tuning.LevelOf(node.Kind);
                var (building, security, features) = (level.Building, level.Security, level.Features);
                int seed = node.Seed, depth = node.Depth, players = Mathf.Max(1, PlayerController.All.Count);
                m_Making[seed] = Task.Run(() =>
                {
                    try { return LevelGenerator.Generate(seed, depth, building, security, players, features); }
                    catch (System.Exception) { return null; }
                });
                return "";
            }
            if (!making.IsCompleted) return "";
            m_Making.Remove(node.Seed);
            var l = making.Result;
            if (l == null) return m_Previews[node.Seed] = "";
            // only what tells the ways apart at a glance: the main job, how big, how guarded
            var job = ObjectiveRules.Name(l.Objective);
            text = (job != null ? Loc.F("route.objective", job) + " · " : "") + Loc.F("route.preview", l.Floors, l.RobotSpawns.Count);
            m_Previews[node.Seed] = text;
            m_Jobs[node.Seed] = l.Objective;
            return text;
        }
    }
}
