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

        // A plan drawn on the sheet: rooms shaded, walls in ink, doors in pen blue, stairs in highlighter, furniture faint.
        static readonly Color[] PartColors =
        {
            UiPalette.PaperShade.A(0.9f),    // Floor
            UiPalette.Ink.A(0.95f),          // Wall
            UiPalette.Pen.A(0.9f),           // Door
            UiPalette.Warn.A(0.8f),          // Vent
            UiPalette.MarkYellow.A(0.8f),    // Stairs
            UiPalette.InkFaint.A(0.35f),     // Prop
            UiPalette.MarkBlue,              // Hatch (the way in: blue)
            UiPalette.InkSoft.A(0.6f),       // Railing (the edge of a gallery)
            UiPalette.InkFaint.A(0.25f),     // Void (an atrium seen from above)
            UiPalette.NoteGreen.A(0.55f),    // Ground (the yard outside, seen up close in the gas)
            UiPalette.MarkGreen,             // ExitHatch (the way out: green, like its marker in the HUD)
        };
        static readonly Color Marked = UiPalette.Stamp;

        readonly List<MapRect> m_Rects = new();
        UiPool<Image> m_Images;
        UiPool<MapIconView> m_Icons;
        int m_DrawnVersion = -1, m_DrawnFloor = -1, m_DrawnSeed;
        float m_NextDraw;
        float m_Fit = 1f;
        Vector2 m_Extent; // the shown floor's size in metres (labels keep inside it)

        /// <summary>The floor shown; -1 = the player's floor (the big map can browse others).</summary>
        public int ViewFloor { get; set; } = -1;

        /// <summary>The map terminal's view: the whole building, not only what was seen, and everything in it.</summary>
        public bool WholeHouse { get; set; }

        /// <summary>The upgraded minimap (in the hand): the devices by their pictures, the locked doors and the crates, searched or not.</summary>
        public bool Upgraded { get; set; }

        ExplorationMap m_Full;

        /// <summary>Every tile of this house seen: what a map terminal shows (built once per house on this peer).</summary>
        ExplorationMap FullMap(LevelLayout layout)
        {
            if (m_Full != null && m_Full.Layout == layout) return m_Full;
            m_Full = new ExplorationMap(layout);
            m_Full.RevealAll();
            return m_Full;
        }

        // the icons' fills (outlined pictures and marks in ink colours that read on the paper)
        static readonly Color RobotColor = UiPalette.Stamp, DeviceColor = UiPalette.MarkOrange, HackedColor = UiPalette.InkFaint.A(0.7f),
            LaserColor = UiPalette.MarkPink, TerminalColor = UiPalette.MarkBlue, StorageColor = UiPalette.MarkYellow,
            DoorColor = UiPalette.Pen, EntryColor = UiPalette.MarkBlue, ExitColor = UiPalette.MarkGreen;

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
            m_Extent = new Vector2(layout.SizeX, layout.SizeZ);
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
                    Icon(Flat(p.Position), LevelLayout.FloorOf(p.Position.y), floor, me2, scale, radius, turn, UiPalette.MarkYellow, null, IconShape.Diamond, 1f, 0f);
                    continue;
                }
                if (p.Kind is not (PingKind.Here or PingKind.Help)) continue;
                var color = PlayerLooks.TeamColor(PlayerLooks.SlotOf(p.Owner));
                float pulse = 1f + 0.25f * Mathf.Sin(p.Age(now) * 8f);
                Icon(Flat(p.Position), LevelLayout.FloorOf(p.Position.y), floor, me2, scale, radius, turn, color, null, IconShape.Diamond, pulse, 0f);
            }
            if (WholeHouse) HouseIcons(layout, floor, me2, scale, radius, turn);
            else if (Upgraded) UpgradedIcons(gm, layout, floor, me2, scale, radius, turn);
            foreach (var d in Detector.All)
            {
                if (!d.IsMarked || WholeHouse) continue;
                Icon(Flat(d.Position), LevelLayout.FloorOf(d.transform.position.y), floor, me2, scale, radius, turn, Marked,
                    FollowPlayer ? null : d.DisplayName, IconShape.Dot, 1f + 0.2f * Mathf.Sin(now * 10f), 0f);
            }
            foreach (var p in PlayerController.All)
            {
                if (p == me) continue;
                var color = p.Downed ? UiPalette.Stamp : UiPalette.InkOf(PlayerLooks.TeamColor(PlayerLooks.SlotOf(p.ClientId)));
                Icon(Flat(p.transform.position), LevelLayout.FloorOf(p.transform.position.y), floor, me2, scale, radius, turn, color,
                    FollowPlayer ? null : p.Nick, p.Downed ? IconShape.Dot : IconShape.Arrow, 1f, p.transform.eulerAngles.y);
            }
            Icon(me2, myFloor, floor, me2, scale, radius, turn, UiPalette.Ink, null, IconShape.Arrow, 1.15f, yaw);
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
                // a device stays where it is mounted (a camera's lens swings with its turn: the icon would wander with it)
                var at = d is RobotController ? d.Position : d.transform.position;
                Icon(Flat(at), floor, floor, me, scale, radius, turn, color, null, glyph != null ? IconShape.Glyph : IconShape.Dot,
                    d is RobotController ? 1f : 0.8f, 0f, glyph);
            }
        }

        /// <summary>
        /// The upgraded minimap's icons near the player on their floor: every device by its picture (grey once off), the
        /// locked doors (red), the crates (grey once searched, gold before).
        /// </summary>
        void UpgradedIcons(GameManager gm, LevelLayout l, int floor, Vector2 me, float scale, float radius, float turn)
        {
            if (l == null) return;
            bool Near(Vector2 at) => (at - me).magnitude * scale <= radius;
            foreach (var d in Detector.All)
            {
                if (d is RobotController || LevelLayout.FloorOf(d.transform.position.y) != floor) continue;
                var at = Flat(d.transform.position);
                if (!Near(at)) continue;
                var glyph = KindIcons != null && (int)d.Kind < KindIcons.Length ? KindIcons[(int)d.Kind] : null;
                var color = d.IsHacked ? HackedColor : d.IsMarked ? Marked : DeviceColor;
                Icon(at, floor, floor, me, scale, radius, turn, color, null, glyph != null ? IconShape.Glyph : IconShape.Dot, 0.8f, 0f, glyph);
            }
            foreach (var door in AutoDoor.All)
            {
                if (door == null || !door.Locked || LevelLayout.FloorOf(door.transform.position.y) != floor) continue;
                var at = Flat(door.transform.position);
                if (Near(at)) Icon(at, floor, floor, me, scale, radius, turn, LockedColor, null, IconShape.Diamond, 0.75f, 0f);
            }
            foreach (int prop in l.Crates)
            {
                if (prop < 0 || prop >= l.Props.Count) continue;
                var p = l.Props[prop].Position;
                if (LevelLayout.FloorOf(p.y) != floor) continue;
                var at = Flat(p);
                if (!Near(at)) continue;
                bool searched = gm.IsOpened(OpenKeys.Crate(prop));
                Icon(at, floor, floor, me, scale, radius, turn, searched ? HackedColor : StorageColor, null, IconShape.Dot, searched ? 0.6f : 0.8f, 0f);
            }
        }

        static readonly Color LockedColor = UiPalette.Stamp;

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
            if (text != null) Ui.Set(icon.Label, text, UiPalette.Ink.A(Mathf.Max(0.6f, color.a))); // labels in ink: a pale fill colour does not read as text
            if (text == null) return;
            // a label by the building's edge leans inwards (a corner's "way out" was cut by the outer wall)
            PlaceLabel(icon, FollowPlayer ? (0, false) : LabelPlace(pos * scale, m_Extent * scale, icon.Label.preferredWidth * size / 2f));
        }

        /// <summary>Puts an icon's label where <see cref="LabelPlace"/> said (the editor's picture of the map does the same).</summary>
        public static void PlaceLabel(MapIconView icon, (int side, bool above) place)
        {
            var rt = icon.Label.rectTransform;
            float half = rt.sizeDelta.x / 2f;
            rt.anchoredPosition = new Vector2(place.side * (half - 4f), place.above ? LabelDrop : -LabelDrop);
            icon.Label.alignment = place.side > 0 ? TextAnchor.MiddleLeft : place.side < 0 ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
        }

        const float LabelDrop = 14f; // the label's distance under (or over) its icon, in the icon's units

        /// <summary>
        /// Where an icon's label goes so it stays inside the floor drawn (both in map pixels, the floor from 0 to
        /// <paramref name="extent"/>): side 0 centred under the icon, 1 starting at it to the right, -1 ending at it to
        /// the left; above when the icon sits by the bottom edge.
        /// </summary>
        public static (int side, bool above) LabelPlace(Vector2 at, Vector2 extent, float halfWidth)
        {
            const float Pad = 14f; // (the outer wall's ink and a little air)
            int side = at.x - halfWidth < Pad ? 1 : at.x + halfWidth > extent.x - Pad ? -1 : 0;
            bool above = at.y - LabelDrop - 10f < Pad + 10f;
            return (side, above);
        }
    }
}
