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
    /// A map of one floor (prefab UI/HUD: the map of a map terminal, <see cref="Level.TerminalKind.Map"/>; the HUD has no
    /// map of its own). Draws the floor (<see cref="MapSketch"/> over an <see cref="ExplorationMap"/>: the whole building
    /// when <see cref="WholeHouse"/>, else what the team has seen) with pooled rectangles, and on top: you, teammates,
    /// pings and marked robots — and in the whole-house view every robot and device, the lasers, the ways in and out, the
    /// storage room and the other terminals. Everything is pre-built in the prefab; this only places, sizes and colours it.
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
        [Tooltip("The picture of each robot / device kind (indexed by RobotKind): the map terminal tells them apart.")]
        public Sprite[] KindIcons;

        static readonly Color[] PartColors =
        {
            new(0.32f, 0.38f, 0.44f, 0.6f),  // Floor
            new(0.92f, 0.94f, 0.97f, 0.95f), // Wall
            new(0.5f, 0.8f, 1f, 0.95f),      // Door
            new(0.75f, 0.7f, 0.6f, 0.9f),    // Vent
            new(0.95f, 0.78f, 0.3f, 0.55f),  // Stairs
            new(0.72f, 0.78f, 0.85f, 0.42f), // Prop
            new(0.4f, 0.72f, 1f, 0.95f),     // Hatch (the way in: blue)
            new(0.7f, 0.85f, 0.95f, 0.6f),   // Railing (the edge of a gallery)
            new(0.05f, 0.07f, 0.1f, 0.55f),  // Void (an atrium seen from above)
            new(0.22f, 0.3f, 0.22f, 0.45f),  // Ground (the yard outside, seen up close in the gas)
            new(0.45f, 1f, 0.5f, 0.95f),     // ExitHatch (the way out: green, like its marker in the HUD)
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

        /// <summary>The map terminal's view: the whole building, not only what was seen, and everything in it.</summary>
        public bool WholeHouse { get; set; }

        ExplorationMap m_Full;

        /// <summary>Every tile of this house seen: what a map terminal shows (built once per house on this peer).</summary>
        ExplorationMap FullMap(LevelLayout layout)
        {
            if (m_Full != null && m_Full.Layout == layout) return m_Full;
            m_Full = new ExplorationMap(layout);
            m_Full.RevealAll();
            return m_Full;
        }

        static readonly Color RobotColor = new(1f, 0.3f, 0.22f), DeviceColor = new(1f, 0.6f, 0.2f), HackedColor = new(0.55f, 0.6f, 0.65f),
            LaserColor = new(1f, 0.15f, 0.3f), TerminalColor = new(0.35f, 0.9f, 1f), StorageColor = new(1f, 0.85f, 0.3f),
            DoorColor = new(0.5f, 0.8f, 1f), EntryColor = new(0.4f, 0.72f, 1f), ExitColor = new(0.45f, 1f, 0.5f);

        void Awake()
        {
            m_Images = new UiPool<Image>(RectPrefab, RectLayer);
            m_Icons = new UiPool<MapIconView>(IconPrefab, IconLayer);
        }

        public void Refresh(GameManager gm, PlayerController me)
        {
            var map = gm.Exploration;
            if (map == null) return;
            if (WholeHouse) map = FullMap(map.Layout);
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
            float turn = 0f; // north up on every peer: the team reads the same map (the minimap follows the player, it does not turn)
            Pivot.localRotation = Quaternion.Euler(0f, 0f, turn);
            var centre = FollowPlayer ? me2 : new Vector2(layout.SizeX, layout.SizeZ) / 2f;
            Content.anchoredPosition = -centre * scale;

            m_Icons.Begin();
            float radius = Mathf.Min(size.x, size.y) / 2f - 9f;
            float now = Time.time;
            // Pings and marked robots under the players.
            foreach (var p in gm.Pings.Active)
            {
                if (p.Kind == PingKind.Thing)
                {
                    Icon(Flat(p.Position), LevelLayout.FloorOf(p.Position.y), floor, me2, scale, radius, turn, new Color(0.8f, 1f, 0.25f), null, IconShape.Diamond, 1f, 0f);
                    continue;
                }
                if (p.Kind is not (PingKind.Here or PingKind.Help)) continue;
                var color = PlayerLooks.TeamColor(PlayerLooks.SlotOf(p.Owner));
                float pulse = 1f + 0.25f * Mathf.Sin(p.Age(now) * 8f);
                Icon(Flat(p.Position), LevelLayout.FloorOf(p.Position.y), floor, me2, scale, radius, turn, color, null, IconShape.Diamond, pulse, 0f);
            }
            if (WholeHouse) HouseIcons(layout, floor, me2, scale, radius, turn);
            foreach (var d in Detector.All)
            {
                if (!d.IsMarked || WholeHouse) continue;
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
                string text = FollowPlayer ? Loc.F("map.floor_short", floor + 1)
                    : WholeHouse ? Loc.F("map.floor_whole", floor + 1, map.Floors)
                    : Loc.F("map.floor", floor + 1, map.Floors, Mathf.RoundToInt(map.RevealedShare(floor) * 100f));
                if (Caption.text != text) Caption.text = text;
            }
        }

        static Vector2 Flat(Vector3 p) => new(p.x, p.z);

        /// <summary>
        /// The map terminal's icons on the shown floor: the ways in and out (the street doors, the entry shaft, the exit
        /// hatch), the storage room, the other terminals, the lasers, every robot and device (grey once hacked).
        /// </summary>
        void HouseIcons(LevelLayout l, int floor, Vector2 me, float scale, float radius, float turn)
        {
            if (l == null) return;
            foreach (var d in l.Doorways)
                if (d.Exterior && LevelLayout.FloorOf(d.Position.y) == floor)
                    Icon(Flat(d.Position), floor, floor, me, scale, radius, turn, DoorColor, null, IconShape.Diamond, 0.7f, 0f);
            if (floor == 0)
            {
                Icon(Flat(l.HatchPosition), 0, 0, me, scale, radius, turn, EntryColor, Loc.T("map.entry"), IconShape.Diamond, 1f, 0f);
                Icon(Flat(l.ExitHatchPosition), 0, 0, me, scale, radius, turn, ExitColor, Loc.T("map.exit"), IconShape.Diamond, 1.1f, 0f);
            }
            if (l.StorageRoom >= 0 && l.StorageRoom < l.Rooms.Count && l.Rooms[l.StorageRoom].Floor == floor)
                Icon(Flat(l.Rooms[l.StorageRoom].Center), floor, floor, me, scale, radius, turn, StorageColor, Loc.T("map.storage"), IconShape.Diamond, 1f, 0f);
            foreach (var t in SecurityTerminal.All)
            {
                if (t.Index < 0 || LevelLayout.FloorOf(t.transform.position.y) != floor) continue;
                Icon(Flat(t.transform.position), floor, floor, me, scale, radius, turn, t.Spent ? HackedColor : TerminalColor, null, IconShape.Diamond, 0.75f, 0f);
            }
            foreach (var g in l.LaserSpots)
                if (LevelLayout.FloorOf(g.Position.y) == floor)
                    Icon(Flat(g.Position), floor, floor, me, scale, radius, turn, LaserColor, null, IconShape.Dot, 0.7f, 0f);
            foreach (var d in Detector.All)
            {
                if (LevelLayout.FloorOf(d.transform.position.y) != floor) continue;
                var color = d.IsHacked ? HackedColor : d is RobotController ? RobotColor : DeviceColor;
                if (d.IsMarked && !d.IsHacked) color = Marked;
                var glyph = KindIcons != null && (int)d.Kind < KindIcons.Length ? KindIcons[(int)d.Kind] : null;
                Icon(Flat(d.Position), floor, floor, me, scale, radius, turn, color, null, glyph != null ? IconShape.Glyph : IconShape.Dot,
                    d is RobotController ? 1f : 0.8f, 0f, glyph);
            }
        }

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

        enum IconShape { Arrow, Dot, Diamond, Glyph }

        void Icon(Vector2 at, int itsFloor, int floor, Vector2 me, float scale, float radius, float turn, Color color, string label,
            IconShape shape, float size, float yaw, Sprite glyph = null)
        {
            if (shape == IconShape.Glyph && glyph == null) shape = IconShape.Dot;
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
            if (icon.Glyph != null)
            {
                Ui.Show(icon.Glyph, shape == IconShape.Glyph);
                if (shape == IconShape.Glyph && icon.Glyph.sprite != glyph) icon.Glyph.sprite = glyph;
            }
            Image image = shape switch { IconShape.Arrow => icon.Arrow, IconShape.Dot => icon.Dot, IconShape.Glyph => icon.Glyph, _ => icon.Diamond };
            image.color = color;
            if (shape == IconShape.Arrow) image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, turn - yaw);
            string text = label == null ? null : itsFloor == floor ? label : $"{label} {(itsFloor > floor ? "↑" : "↓")}";
            Ui.Show(icon.Label, text != null);
            if (text != null) Ui.Set(icon.Label, text, new Color(color.r, color.g, color.b, Mathf.Max(0.6f, color.a)));
        }
    }
}
