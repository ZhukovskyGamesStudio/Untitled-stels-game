using System.Collections.Generic;
using UnityEngine;
using static Stealth.Level.LevelMetrics;

namespace Stealth.Level
{
    public enum MapPart : byte { Floor, Wall, Door, Vent, Stairs, Prop, Hatch }

    /// <summary>One rectangle of the map, x/y = world x/z in metres.</summary>
    public struct MapRect
    {
        public MapPart Part;
        public Rect Area;

        public MapRect(MapPart part, Rect area)
        {
            Part = part;
            Area = area;
        }
    }

    /// <summary>
    /// What the map draws of one floor (pure, unit tested): only what the team has seen (<see cref="ExplorationMap"/>).
    /// Revealed tiles merged into runs, the walls next to them with the gaps of doors and vents, stairs, furniture and the
    /// hatch. Few rectangles, so the HUD needs few images.
    /// </summary>
    public static class MapSketch
    {
        public const float WallWidth = 0.35f;
        public const float GapWidth = 0.14f;

        static readonly List<(float a, float b)> s_Line = new();

        public static void Build(ExplorationMap map, int floor, IReadOnlyList<MapProp> props, List<MapRect> output)
        {
            output.Clear();
            if (map == null || floor < 0 || floor >= map.Floors) return;
            var l = map.Layout;
            const float t = ExplorationMap.Tile;

            // Floor: revealed tiles merged along X.
            for (int ty = 0; ty < map.Height; ty++)
            {
                int start = -1;
                for (int tx = 0; tx <= map.Width; tx++)
                {
                    bool on = tx < map.Width && map.IsRevealed(floor, tx, ty);
                    if (on && start < 0) start = tx;
                    if (on || start < 0) continue;
                    output.Add(new MapRect(MapPart.Floor, new Rect(start * t, ty * t, (tx - start) * t, t)));
                    start = -1;
                }
            }

            // Stairs: drawn where they are seen, going up from this floor or arriving from below.
            foreach (var s in l.Stairs)
            {
                if (s.Floor != floor && s.Floor + 1 != floor) continue;
                var area = StairArea(s);
                if (AnyRevealed(map, floor, area)) output.Add(new MapRect(MapPart.Stairs, area));
            }

            // Walls along X (at x = k * Cell) and along Z (at z = k * Cell).
            for (int k = 0; k <= l.Width; k++) WallLine(map, floor, k, true, output);
            for (int k = 0; k <= l.Height; k++) WallLine(map, floor, k, false, output);

            // Furniture: once the tile under its centre is seen, clipped to its room (models may reach past a wall).
            if (props != null)
                foreach (var p in props)
                {
                    if (p.Floor != floor) continue;
                    var c = p.Area.center;
                    if (!map.IsRevealed(floor, Mathf.FloorToInt(c.x / t), Mathf.FloorToInt(c.y / t))) continue;
                    var room = new Rect(Mathf.Floor(c.x / Cell) * Cell, Mathf.Floor(c.y / Cell) * Cell, Cell, Cell);
                    var clipped = Rect.MinMaxRect(Mathf.Max(p.Area.xMin, room.xMin), Mathf.Max(p.Area.yMin, room.yMin),
                        Mathf.Min(p.Area.xMax, room.xMax), Mathf.Min(p.Area.yMax, room.yMax));
                    if (clipped.width > 0.01f && clipped.height > 0.01f) output.Add(new MapRect(MapPart.Prop, clipped));
                }

            if (floor == 0)
            {
                var h = l.HatchPosition;
                var hatch = new Rect(h.x - 0.8f, h.z - 0.8f, 1.6f, 1.6f);
                if (AnyRevealed(map, 0, hatch)) output.Add(new MapRect(MapPart.Hatch, hatch));
            }
        }

        /// <summary>The square of a staircase and its hole in the corner of its room.</summary>
        public static Rect StairArea(Stair s)
        {
            float x = s.Corner.x > 0 ? (s.Cell.x + 1) * Cell - StairSize : s.Cell.x * Cell;
            float z = s.Corner.y > 0 ? (s.Cell.y + 1) * Cell - StairSize : s.Cell.y * Cell;
            return new Rect(x, z, StairSize, StairSize);
        }

        static bool AnyRevealed(ExplorationMap map, int floor, Rect area)
        {
            const float t = ExplorationMap.Tile;
            int x0 = Mathf.FloorToInt(area.xMin / t), x1 = Mathf.FloorToInt((area.xMax - 0.01f) / t);
            int y0 = Mathf.FloorToInt(area.yMin / t), y1 = Mathf.FloorToInt((area.yMax - 0.01f) / t);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (map.IsRevealed(floor, x, y)) return true;
            return false;
        }

        /// <summary>The wall between rooms on one grid line, cut into pieces next to revealed tiles; door / vent gaps drawn thin.</summary>
        static void WallLine(ExplorationMap map, int floor, int k, bool alongZ, List<MapRect> output)
        {
            var l = map.Layout;
            const float t = ExplorationMap.Tile;
            const int n = ExplorationMap.TilesPerCell;
            int cells = alongZ ? l.Height : l.Width;
            float line = k * Cell;
            s_Line.Clear();
            for (int c = 0; c < cells; c++)
            {
                var edge = Edge(l, floor, k, c, alongZ);
                if (edge == EdgeType.Open) continue;
                float mid = (c + 0.5f) * Cell;
                float half = edge == EdgeType.Wall ? 0f : ClearWidth(edge == EdgeType.Vent ? VentWidth : LevelMetrics.DoorWidth) / 2f;
                bool gapSeen = false;
                for (int s = 0; s < n; s++)
                {
                    int along = c * n + s;
                    int before = k * n - 1, after = k * n;
                    bool seen = alongZ
                        ? map.IsRevealed(floor, before, along) || map.IsRevealed(floor, after, along)
                        : map.IsRevealed(floor, along, before) || map.IsRevealed(floor, along, after);
                    if (!seen) continue;
                    float a = along * t, b = a + t;
                    if (half <= 0f) { Add(a, b); continue; }
                    if (a < mid - half) Add(a, Mathf.Min(b, mid - half));
                    if (b > mid + half) Add(Mathf.Max(a, mid + half), b);
                    if (b > mid - half && a < mid + half) gapSeen = true;
                }
                if (gapSeen)
                {
                    var part = edge == EdgeType.Vent ? MapPart.Vent : MapPart.Door;
                    output.Add(new MapRect(part, Strip(line, mid - half, mid + half, GapWidth, alongZ)));
                }
            }
            // Merge touching pieces so a long wall is one rectangle.
            s_Line.Sort((p, q) => p.a.CompareTo(q.a));
            for (int i = 0; i < s_Line.Count; i++)
            {
                var (a, b) = s_Line[i];
                while (i + 1 < s_Line.Count && s_Line[i + 1].a <= b + 0.001f) b = Mathf.Max(b, s_Line[++i].b);
                output.Add(new MapRect(MapPart.Wall, Strip(line, a, b, WallWidth, alongZ)));
            }
        }

        static void Add(float a, float b)
        {
            if (b - a > 0.01f) s_Line.Add((a, b));
        }

        static Rect Strip(float line, float a, float b, float width, bool alongZ) =>
            alongZ ? new Rect(line - width / 2f, a, width, b - a) : new Rect(a, line - width / 2f, b - a, width);

        /// <summary>The wall at grid line <paramref name="k"/>, cell <paramref name="c"/> along it; outer walls have the exterior doors.</summary>
        public static EdgeType Edge(LevelLayout l, int floor, int k, int c, bool alongZ)
        {
            int max = alongZ ? l.Width : l.Height;
            if (k > 0 && k < max) return alongZ ? l.EdgesX[floor, k - 1, c] : l.EdgesZ[floor, c, k - 1];
            if (floor != 0) return EdgeType.Wall;
            Vector2Int cell, dir;
            if (alongZ)
            {
                cell = new Vector2Int(k == 0 ? 0 : l.Width - 1, c);
                dir = k == 0 ? Vector2Int.left : Vector2Int.right;
            }
            else
            {
                cell = new Vector2Int(c, k == 0 ? 0 : l.Height - 1);
                dir = k == 0 ? Vector2Int.down : Vector2Int.up;
            }
            return l.ExteriorDoors.Exists(e => e.cell == cell && e.dir == dir) ? EdgeType.Door : EdgeType.Wall;
        }
    }
}
