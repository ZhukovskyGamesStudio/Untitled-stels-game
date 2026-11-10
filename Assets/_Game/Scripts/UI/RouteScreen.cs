using System.Collections.Generic;
using System.Linq;
using Stealth.Configs;
using Stealth.Game;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// The route map in the sewer (prefab UI/Route): the next few columns of houses like a Slay the Spire map, the houses
    /// the team can go to next, who voted for which, and whether the quota is paid. Click a house to vote; the map key
    /// closes and opens it (the stash board to sell the rest is in the tunnel). Opens by itself when the team comes down.
    /// </summary>
    public class RouteScreen : MonoBehaviour
    {
        public const int ColumnsShown = 5;

        public static bool IsOpen { get; private set; }
        static bool s_CloseRequest;

        /// <summary>Test hook: close the map (as the map key does).</summary>
        public static void CloseForTest() => s_CloseRequest = true;

        public GameObject Panel;
        public Text Title;
        public Text Status;
        public Text Hint;
        [Tooltip("The area the columns are laid out in (pivot bottom-left).")]
        public RectTransform Graph;
        public RectTransform LineLayer;
        public RouteNodeView NodePrefab;
        public Image LinePrefab;

        static readonly Color Current = new(1f, 1f, 1f, 0.9f), Option = new(0.45f, 0.85f, 1f), Chosen = new(1f, 0.8f, 0.25f),
            Later = new(1f, 1f, 1f, 0.28f), LineOn = new(0.45f, 0.85f, 1f, 0.9f), LineOff = new(1f, 1f, 1f, 0.15f);

        UiPool<RouteNodeView> m_Nodes;
        UiPool<Image> m_Lines;
        readonly Dictionary<int, string> m_Previews = new();
        bool m_Open;
        RunPhase m_LastPhase = RunPhase.Playing;

        void Awake()
        {
            m_Nodes = new UiPool<RouteNodeView>(NodePrefab, Graph);
            m_Lines = new UiPool<Image>(LinePrefab, LineLayer);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            var me = PlayerController.Local;
            bool sewer = gm != null && me != null && gm.Phase == RunPhase.Sewer && gm.Route != null;
            // Opens when the team comes down; the map key toggles it.
            if (sewer && m_LastPhase != RunPhase.Sewer) m_Open = true;
            m_LastPhase = gm != null ? gm.Phase : RunPhase.Playing;
            if (sewer && !GameMenu.IsOpen && !me.CursorMode && Controls.Pressed(GameAction.Map)) m_Open = !m_Open;
            if (s_CloseRequest) { m_Open = false; s_CloseRequest = false; }
            IsOpen = sewer && m_Open && !GameMenu.IsOpen;
            PlayerController.ScreenWantsCursor = IsOpen;
            Ui.Show(Panel, IsOpen);
            if (IsOpen) Draw(gm, me);
        }

        void OnDisable()
        {
            IsOpen = false;
            PlayerController.ScreenWantsCursor = false;
        }

        void Draw(GameManager gm, PlayerController me)
        {
            var route = gm.Route;
            int depth = gm.Level.Index, here = gm.Level.Lane;
            var options = gm.RouteOptions;
            var size = Graph.rect.size;
            Vector2 At(int d, int lane)
            {
                float x = Mathf.Lerp(50f, size.x - 50f, (d - depth) / (float)(ColumnsShown - 1));
                float y = Mathf.Lerp(size.y - 40f, 60f, route.Across(d, lane));
                return new Vector2(x, y);
            }

            m_Lines.Begin();
            m_Nodes.Begin();
            for (int d = depth; d < depth + ColumnsShown; d++)
                for (int lane = 0; lane < route.LanesAt(d); lane++)
                {
                    if (d == depth && lane != here) continue; // the column just played: only where the team is
                    if (d < depth + ColumnsShown - 1)
                        foreach (int next in route.Next(d, lane))
                        {
                            bool on = d == depth && lane == here;
                            Line(At(d, lane), At(d + 1, next), on ? (gm.ChosenLane == next ? Chosen : LineOn) : LineOff);
                        }
                    Node(gm, me, route, d, lane, At(d, lane), depth, here, options);
                }
            m_Lines.End();
            m_Nodes.End();

            int need = Mathf.Max(0, gm.Quota - gm.Collected);
            string quota = need == 0
                ? $"<color=#7CFF8A>Квота дома выплачена: {gm.Collected} / {gm.Quota}</color>"
                : $"<color=#FF8A70>Квота: {gm.Collected} / {gm.Quota} — не хватает {need}. Допродайте добычу у доски: без квоты дальше не пустят, забег кончится.</color>";
            int voted = PlayerController.All.Count(p => gm.Votes.All.TryGetValue(p.ClientId, out int l) && options.Contains(l));
            string vote = gm.GoAt > 0
                ? $"Идём в дом {gm.ChosenLane + 1} — через {Mathf.Max(0, (float)(gm.GoAt - Session.ServerTime)):0} с"
                : options.Count == 1 ? $"Путь один. Готовы идти: {voted} из {PlayerController.All.Count}" : $"Голосуют: {voted} из {PlayerController.All.Count}. Ничья — решит жребий";
            Ui.Set(Status, $"{quota}\n{vote}", Color.white);
            Title.text = $"Канализация · пройдено домов: {depth + 1}";
            Hint.text = $"Клик по дому — голос · {Controls.Label(GameAction.Map)} — закрыть карту (продать добычу — у доски, {Controls.Label(GameAction.Hands)})";
        }

        void Line(Vector2 a, Vector2 b, Color color)
        {
            var img = m_Lines.Next();
            var rt = img.rectTransform;
            var d = b - a;
            rt.anchoredPosition = a;
            rt.sizeDelta = new Vector2(d.magnitude, 3f);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            img.color = color;
        }

        void Node(GameManager gm, PlayerController me, RunMap route, int d, int lane, Vector2 at, int depth, int here, IReadOnlyList<int> options)
        {
            var v = m_Nodes.Next();
            v.Rect.anchoredPosition = at;
            bool current = d == depth && lane == here;
            bool option = d == depth + 1 && options.Contains(lane);
            bool chosen = option && gm.ChosenLane == lane;
            var color = current ? Current : chosen ? Chosen : option ? Option : Later;
            v.Ring.color = color;
            v.Fill.color = new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, option || current ? 0.95f : 0.6f);
            var node = route.Node(d, lane);
            string kind = RouteNode.KindName(node.Kind);
            v.Label.text = current ? "Вы здесь" : option ? $"{kind} · {Preview(node, gm)}" : kind;
            v.Label.color = color;
            v.Button.interactable = option && gm.GoAt <= 0;
            v.Button.onClick.RemoveAllListeners();
            if (option) v.Button.onClick.AddListener(() => gm.RequestVote(lane));
            // Who voted here: a dot in each voter's team colour.
            string dots = "";
            if (option)
                foreach (var p in PlayerController.All)
                    if (gm.Votes.All.TryGetValue(p.ClientId, out int l) && l == lane)
                        dots += $"<color=#{ColorUtility.ToHtmlStringRGB(PlayerLooks.TeamColor(PlayerLooks.SlotOf(p.ClientId)))}>●</color>";
            Ui.Show(v.Votes, dots.Length > 0);
            if (dots.Length > 0) v.Votes.text = dots;
        }

        /// <summary>What the team would face there: size, floors, guards (generated from the house's seed, cached).</summary>
        string Preview(RouteNode node, GameManager gm)
        {
            if (m_Previews.TryGetValue(node.Seed, out var text)) return text;
            var level = Tuning.Level;
            var l = LevelGenerator.Generate(node.Seed, node.Depth, level.Building, level.Security, Mathf.Max(1, PlayerController.All.Count));
            int dark = 0;
            foreach (bool b in l.Dark) if (b) dark++;
            text = $"{l.Width}×{l.Height}, {l.Floors} эт.\nохрана {l.RobotSpawns.Count}, камер {l.CameraSpots.Count}\nтёмных комнат {dark}";
            m_Previews[node.Seed] = text;
            return text;
        }
    }
}
