using System.Collections.Generic;
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
                sign.Body.text = Preview(node, gm); // the fuel the trip costs is the same for every way: not shown

                string voters = "";
                foreach (var p in PlayerController.All)
                    if (gm.Votes.All.TryGetValue(p.ClientId, out int l) && l == lane)
                        voters += (voters.Length > 0 ? ", " : "") +
                                  $"<color=#{ColorUtility.ToHtmlStringRGB(PlayerLooks.TeamColor(PlayerLooks.SlotOf(p.ClientId)))}>{p.Nick}</color>";
                bool going = gm.GoAt > 0 && gm.ChosenLane == lane;
                string status = going ? Loc.F("route.going", Mathf.CeilToInt(Mathf.Max(0f, (float)(gm.GoAt - Session.ServerTime))))
                    : voters.Length > 0 ? Loc.F("route.here", voters) : "";
                sign.Votes.text = status;
                sign.Body.enabled = status.Length == 0; // the countdown takes the body's line
                sign.Frame.color = going ? new Color(1f, 0.8f, 0.25f) : voters.Length > 0 ? new Color(0.45f, 0.85f, 1f) : new Color(1f, 1f, 1f, 0.35f);

                // Fully visible when looked at, otherwise only a hint of it.
                float target = 0.6f;
                if (cam != null)
                {
                    var to = sign.transform.position - cam.position;
                    if (Vector3.Angle(cam.forward, to) < LookAngle && to.magnitude < 26f) target = 1f;
                }
                if (going || voters.Length > 0) target = 1f;
                sign.Group.alpha = Mathf.MoveTowards(sign.Group.alpha, target, Time.deltaTime * 3f);
            }
        }

        /// <summary>What the team would face there (generated from the house's seed, cached).</summary>
        string Preview(RouteNode node, GameManager gm)
        {
            if (m_PreviewsLoc != Loc.Version) { m_Previews.Clear(); m_PreviewsLoc = Loc.Version; }
            if (m_Previews.TryGetValue(node.Seed, out var text)) return text;
            var level = Tuning.Level;
            var l = LevelGenerator.Generate(node.Seed, node.Depth, level.Building, level.Security, Mathf.Max(1, PlayerController.All.Count), level.Features);
            // only what tells the ways apart at a glance: the main job, how big, how guarded
            var job = ObjectiveRules.Name(l.Objective);
            text = (job != null ? Loc.F("route.objective", job) + " · " : "") + Loc.F("route.preview", l.Floors, l.RobotSpawns.Count);
            m_Previews[node.Seed] = text;
            return text;
        }
    }
}
