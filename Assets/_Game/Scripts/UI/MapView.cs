using System.Collections.Generic;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// A map of one floor (prefab UI/HUD: the minimap in the corner and the big map on M). Draws what the team has seen
    /// (<see cref="MapSketch"/> over <see cref="ExplorationMap"/>) with pooled rectangles, and on top: you, teammates,
    /// pings and marked robots. The minimap turns with your view and follows you; the big map is north-up and shows the
    /// whole floor. Everything is pre-built in the prefab; this only places, sizes and colours it.
    /// </summary>
    public class MapView : MonoBehaviour
    {
        [Tooltip("Turns with the view and keeps the player in the centre (minimap); otherwise north-up, the whole floor.")]
        public bool FollowPlayer = true;
        [Tooltip("Pixels per metre (minimap); the big map fits the floor.")]
        public float Scale = 4.5f;
        public RectTransform Viewport;
        [Tooltip("Rotates with the view (minimap); its child Content holds the map in metres.")]
        public RectTransform Pivot;
        public RectTransform Content;
        public RectTransform RectLayer;
        public RectTransform IconLayer;
        public Image RectPrefab;
        public MapIconView IconPrefab;
        public Text Caption;

        static readonly Color[] PartColors =
        {
            new(0.32f, 0.38f, 0.44f, 0.6f),  // Floor
            new(0.92f, 0.94f, 0.97f, 0.95f), // Wall
            new(0.5f, 0.8f, 1f, 0.95f),      // Door
            new(0.75f, 0.7f, 0.6f, 0.9f),    // Vent
            new(0.95f, 0.78f, 0.3f, 0.55f),  // Stairs
            new(0.72f, 0.78f, 0.85f, 0.42f), // Prop
            new(0.45f, 1f, 0.5f, 0.95f),     // Hatch
            new(0.7f, 0.85f, 0.95f, 0.6f),   // Railing (the edge of a gallery)
            new(0.05f, 0.07f, 0.1f, 0.55f),  // Void (an atrium seen from above)
            new(0.22f, 0.3f, 0.22f, 0.45f),  // Ground (the yard outside, seen up close in the gas)
        };
        static readonly Color Marked = new(1f, 0.22f, 0.15f);

        readonly List<MapRect> m_Rects = new();
        UiPool<Image> m_Images;
        UiPool<MapIconView> m_Icons;
        int m_DrawnVersion = -1, m_DrawnFloor = -1, m_DrawnSeed;
        float m_NextDraw;
        float m_Fit = 1f;

        /// <summary>The floor shown; -1 = the player's floor (the big map can browse others).</summary>
        public int ViewFloor { get; set; } = -1;

        void Awake()
        {
            m_Images = new UiPool<Image>(RectPrefab, RectLayer);
            m_Icons = new UiPool<MapIconView>(IconPrefab, IconLayer);
        }

        public void Refresh(GameManager gm, PlayerController me)
        {
            var map = gm.Exploration;
            if (map == null) return;
            int myFloor = Mathf.Clamp(LevelLayout.FloorOf(me.transform.position.y), 0, map.Floors - 1);
            int floor = ViewFloor < 0 ? myFloor : Mathf.Clamp(ViewFloor, 0, map.Floors - 1);
            var size = Viewport.rect.size;
            float scale = Scale;
            var layout = map.Layout;
            if (!FollowPlayer)
            {
                // The whole floor fits the view.
                var extent = new Vector2(layout.SizeX, layout.SizeZ);
                scale = Mathf.Min(size.x / extent.x, size.y / extent.y) * 0.92f;
            }
            if (!Mathf.Approximately(scale, m_Fit)) { m_Fit = scale; m_DrawnVersion = -1; }

            // Redraw the map itself when something new was seen (a few times a second at most).
            if ((map.Version != m_DrawnVersion || floor != m_DrawnFloor || map.Seed != m_DrawnSeed) && Time.unscaledTime >= m_NextDraw)
            {
                m_NextDraw = Time.unscaledTime + 0.25f;
                m_DrawnVersion = map.Version;
                m_DrawnFloor = floor;
                m_DrawnSeed = map.Seed;
                Draw(map, floor, scale);
            }

            // Place the content: around the player (turning with the view) or the floor centred.
            var me2 = Flat(me.transform.position);
            float yaw = me.transform.eulerAngles.y;
            float turn = FollowPlayer ? yaw : 0f;
            Pivot.localRotation = Quaternion.Euler(0f, 0f, turn);
            var centre = FollowPlayer ? me2 : new Vector2(layout.SizeX, layout.SizeZ) / 2f;
            Content.anchoredPosition = -centre * scale;

            m_Icons.Begin();
            float radius = Mathf.Min(size.x, size.y) / 2f - 9f;
            float now = Time.time;
            // Pings and marked robots under the players.
            foreach (var p in gm.Pings.Active)
            {
                if (p.Kind != PingKind.Here) continue;
                var color = PlayerLooks.TeamColor(PlayerLooks.SlotOf(p.Owner));
                float pulse = 1f + 0.25f * Mathf.Sin(p.Age(now) * 8f);
                Icon(Flat(p.Position), LevelLayout.FloorOf(p.Position.y), floor, me2, scale, radius, turn, color, null, IconShape.Diamond, pulse, 0f);
            }
            foreach (var d in Detector.All)
            {
                if (!d.IsMarked) continue;
                Icon(Flat(d.Position), LevelLayout.FloorOf(d.transform.position.y), floor, me2, scale, radius, turn, Marked,
                    FollowPlayer ? null : d.DisplayName, IconShape.Dot, 1f + 0.2f * Mathf.Sin(now * 10f), 0f);
            }
            foreach (var p in PlayerController.All)
            {
                if (p == me) continue;
                var color = p.Downed ? new Color(1f, 0.3f, 0.25f) : PlayerLooks.TeamColor(PlayerLooks.SlotOf(p.ClientId));
                Icon(Flat(p.transform.position), LevelLayout.FloorOf(p.transform.position.y), floor, me2, scale, radius, turn, color,
                    FollowPlayer ? null : p.Nick, p.Downed ? IconShape.Dot : IconShape.Arrow, 1f, p.transform.eulerAngles.y);
            }
            Icon(me2, myFloor, floor, me2, scale, radius, turn, Color.white, null, IconShape.Arrow, 1.15f, yaw);
            m_Icons.End();

            if (Caption != null)
            {
                string text = FollowPlayer
                    ? $"этаж {floor + 1}"
                    : $"Этаж {floor + 1} из {map.Floors} · исследовано {map.RevealedShare(floor) * 100f:0}%" +
                      (map.Floors > 1 ? " · колесо мыши — другой этаж" : "") + $" · {Controls.Label(GameAction.Map)} — закрыть";
                if (Caption.text != text) Caption.text = text;
            }
        }

        static Vector2 Flat(Vector3 p) => new(p.x, p.z);

        void Draw(ExplorationMap map, int floor, float scale)
        {
            MapSketch.Build(map, floor, ActiveLevel.Shell?.Props, m_Rects);
            m_Images.Begin();
            foreach (var r in m_Rects)
            {
                var img = m_Images.Next();
                var rt = img.rectTransform;
                rt.anchoredPosition = r.Area.min * scale;
                // Thin parts stay visible on a small map.
                var s = r.Area.size * scale;
                if (r.Part is MapPart.Wall or MapPart.Door or MapPart.Vent or MapPart.Railing) s = Vector2.Max(s, Vector2.one * 1.5f);
                rt.sizeDelta = s;
                // diagonal and curved walls: turned about the rect's min corner (its pivot)
                rt.localRotation = Quaternion.Euler(0f, 0f, r.Angle);
                img.color = PartColors[(int)r.Part];
            }
            m_Images.End();
        }

        enum IconShape { Arrow, Dot, Diamond }

        void Icon(Vector2 at, int itsFloor, int floor, Vector2 me, float scale, float radius, float turn, Color color, string label,
            IconShape shape, float size, float yaw)
        {
            var pos = at;
            if (FollowPlayer)
            {
                // Off the minimap: kept on its edge, pointing the way.
                var d = (at - me) * scale;
                if (d.magnitude > radius) pos = me + d.normalized * (radius / scale);
            }
            var icon = m_Icons.Next();
            icon.Rect.anchoredPosition = pos * scale;
            icon.Rect.localRotation = Quaternion.Euler(0f, 0f, -turn); // labels stay upright
            icon.Rect.localScale = Vector3.one * size;
            if (itsFloor != floor) color.a *= 0.4f; // on another floor
            Ui.Show(icon.Arrow, shape == IconShape.Arrow);
            Ui.Show(icon.Dot, shape == IconShape.Dot);
            Ui.Show(icon.Diamond, shape == IconShape.Diamond);
            Image image = shape == IconShape.Arrow ? icon.Arrow : shape == IconShape.Dot ? icon.Dot : icon.Diamond;
            image.color = color;
            if (shape == IconShape.Arrow) image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, turn - yaw);
            string text = label == null ? null : itsFloor == floor ? label : $"{label} {(itsFloor > floor ? "↑" : "↓")}";
            Ui.Show(icon.Label, text != null);
            if (text != null) Ui.Set(icon.Label, text, new Color(color.r, color.g, color.b, Mathf.Max(0.6f, color.a)));
        }
    }
}
