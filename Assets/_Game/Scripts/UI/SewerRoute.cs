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
    /// the panel over each one — the house it leads to (kind, size, floors, guards, cameras, dark rooms), who stands in it,
    /// the countdown once everyone has chosen, and a warning while the quota is not paid. A panel comes up fully when you
    /// look at its tunnel. Presenter only: the vote itself is decided by the server from where players stand.
    /// </summary>
    public class SewerRoute : MonoBehaviour
    {
        [Tooltip("Panels come up fully when the view is within this angle of them.")]
        public float LookAngle = 28f;

        SewerLocation m_Sewer;
        readonly Dictionary<int, string> m_Previews = new();

        void Awake() => m_Sewer = GetComponent<SewerLocation>();

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Route == null || m_Sewer == null) return;
            var options = gm.RouteOptions;
            m_Sewer.ShowWays(options.Count);
            var me = PlayerController.Local;
            var cam = me != null && me.Camera != null ? me.Camera.transform : null;
            int need = Mathf.Max(0, gm.Quota - gm.Collected);
            for (int tunnel = 0; tunnel < m_Sewer.Branches.Length; tunnel++)
            {
                int way = SewerBranches.WayOf(tunnel, options.Count);
                var sign = m_Sewer.Branches[tunnel].Sign;
                if (way < 0 || sign == null) continue;
                int lane = options[way];
                var node = gm.Route.Node(gm.Level.Index + 1, lane);
                sign.Title.text = $"{RouteNode.KindName(node.Kind)} · дом {gm.Level.Index + 2}";
                sign.Body.text = Preview(node, gm) + (need > 0 ? $"\n<color=#FF8A70>Квота: не хватает {need} — дальше не пустят</color>" : "");

                string voters = "";
                foreach (var p in PlayerController.All)
                    if (gm.Votes.All.TryGetValue(p.ClientId, out int l) && l == lane)
                        voters += (voters.Length > 0 ? ", " : "") +
                                  $"<color=#{ColorUtility.ToHtmlStringRGB(PlayerLooks.TeamColor(PlayerLooks.SlotOf(p.ClientId)))}>{p.Nick}</color>";
                bool going = gm.GoAt > 0 && gm.ChosenLane == lane;
                string status = going ? $"Идём сюда через {Mathf.CeilToInt(Mathf.Max(0f, (float)(gm.GoAt - Session.ServerTime)))}…"
                    : voters.Length > 0 ? $"Здесь: {voters}" : "Зайди в туннель, чтобы выбрать этот путь";
                sign.Votes.text = status;
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
            if (m_Previews.TryGetValue(node.Seed, out var text)) return text;
            var level = Tuning.Level;
            var l = LevelGenerator.Generate(node.Seed, node.Depth, level.Building, level.Security, Mathf.Max(1, PlayerController.All.Count));
            int dark = 0;
            foreach (var r in l.Rooms) if (r.Dark) dark++;
            int rooms = l.Rooms.Count, atriums = 0;
            foreach (var r in l.Rooms) if (r.Levels > 1) atriums++;
            text = $"{l.Floors} {Floors(l.Floors)} · {rooms} комнат" + (atriums > 0 ? $" · атриумов: {atriums}" : "") + $"\n" +
                   $"Охрана: {l.RobotSpawns.Count} · камер: {l.CameraSpots.Count} · лазеров: {l.LaserSpots.Count}\n" +
                   $"Темно в {dark} из {rooms} комнат · запертых дверей: {l.LockedDoors.Count}";
            m_Previews[node.Seed] = text;
            return text;
        }

        static string Floors(int n) => n % 10 == 1 && n % 100 != 11 ? "этаж" : n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 12 || n % 100 > 14) ? "этажа" : "этажей";
    }
}
