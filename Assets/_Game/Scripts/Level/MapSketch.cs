using System.Collections.Generic;
using UnityEngine;
using static Stealth.Level.LevelMetrics;

namespace Stealth.Level
{
    public enum MapPart : byte { Floor, Wall, Door, Vent, Stairs, Prop, Hatch, Railing, Void, Ground }

    /// <summary>
    /// One rectangle of the map, x/y = world x/z in metres. <see cref="Angle"/> turns it (degrees, counter-clockwise)
    /// about its min corner: diagonal and curved walls.
    /// </summary>
    public struct MapRect
    {
        public MapPart Part;
        public Rect Area;
        public float Angle;

        public MapRect(MapPart part, Rect area, float angle = 0f)
        {
            Part = part;
            Area = area;
            Angle = angle;
        }
    }

    /// <summary>
    /// What the map draws of one floor (pure, unit tested): only what the team has seen (<see cref="ExplorationMap"/>).
    /// Revealed tiles merged into runs, the walls next to them (straight, diagonal, arcs) with the gaps of doors and
    /// vents, railings of galleries, voids of atriums, stairs, furniture and the hatch. Few rectangles, so the HUD needs
    /// few images.
    /// </summary>
    public static class MapSketch
    {
        public const float WallWidth = 0.35f;
        public const float GapWidth = 0.14f;
        const float Step = 0.5f;

        static readonly List<(float a, float b)> s_Run = new();
        static readonly List<(int x0, int x1, bool inside)> s_Rows = new();
        static readonly List<(int x0, int x1, int y0, bool inside)> s_Open = new();

        public static void Build(ExplorationMap map, int floor, IReadOnlyList<MapProp> props, List<MapRect> output)
        {
            output.Clear();
            if (map == null || floor < 0 || floor >= map.Floors) return;
            var l = map.Layout;
            var plan = l.Plan;
            const float t = ExplorationMap.Tile;

            // Floor: revealed tiles merged into runs along X, and runs that repeat on the next rows into one rectangle
            // (voids of atriums drawn dark on top).
            s_Open.Clear();
            for (int ty = 0; ty <= map.Height; ty++)
            {
                s_Rows.Clear();
                if (ty < map.Height)
                {
                    int start = -1;
                    bool startInside = false;
                    for (int tx = 0; tx <= map.Width; tx++)
                    {
                        bool on = tx < map.Width && map.IsRevealed(floor, tx, ty);
                        // a run is all floor of the building or all ground outside it
                        bool inside = on && plan != null && plan.IsInside(floor, ExplorationMap.TileCenter(tx, ty));
                        if (start >= 0 && (!on || inside != startInside))
                        {
                            s_Rows.Add((start, tx, startInside));
                            start = -1;
                        }
                        if (on && start < 0) { start = tx; startInside = inside; }
                    }
                }
                // close the rectangles whose run does not go on in this row
                for (int i = s_Open.Count - 1; i >= 0; i--)
                {
                    var (x0, x1, y0, ins) = s_Open[i];
                    if (s_Rows.Remove((x0, x1, ins))) continue;
                    var min = ExplorationMap.TileMin(x0, y0);
                    output.Add(new MapRect(ins ? MapPart.Floor : MapPart.Ground, new Rect(min.x, min.y, (x1 - x0) * t, (ty - y0) * t)));
                    s_Open.RemoveAt(i);
                }
                foreach (var (x0, x1, ins) in s_Rows) s_Open.Add((x0, x1, ty, ins));
            }
            if (plan != null && floor > 0)
                for (int ty = 0; ty < map.Height; ty++)
                    for (int tx = 0; tx < map.Width; tx++)
                    {
                        if (!map.IsRevealed(floor, tx, ty)) continue;
                        var c = ExplorationMap.TileCenter(tx, ty);
                        var cell = HousePlan.CellOf(c);
                        if (plan.KindAt(floor, cell.x, cell.y) == CellKind.Void)
                        {
                            var min = ExplorationMap.TileMin(tx, ty);
                            output.Add(new MapRect(MapPart.Void, new Rect(min.x, min.y, t, t)));
                        }
                    }

            // Stairs: drawn where they are seen, going up from this floor or arriving from below.
            foreach (var s in l.Stairs)
            {
                if (s.Floor != floor && s.Floor + 1 != floor) continue;
                var area = StairArea(s);
                if (AnyRevealed(map, floor, area)) output.Add(new MapRect(MapPart.Stairs, area));
            }

            if (plan != null)
                for (int i = 0; i < plan.Walls.Count; i++)
                    if (plan.Walls[i].Floor == floor) Wall(map, plan, i, output);

            // Furniture: once the tile under its centre is seen, clipped to its room (models may reach past a wall).
            if (props != null)
                foreach (var p in props)
                {
                    if (p.Floor != floor) continue;
                    var c = p.Area.center;
                    var tile = ExplorationMap.TileOf(new Vector3(c.x, 0f, c.y));
                    if (!map.IsRevealed(floor, tile.x, tile.y)) continue;
                    var room = plan?.RoomAt(new Vector3(c.x, LevelLayout.FloorY(floor), c.y));
                    var box = room != null ? room.Bounds : p.Area;
                    var clipped = Rect.MinMaxRect(Mathf.Max(p.Area.xMin, box.xMin), Mathf.Max(p.Area.yMin, box.yMin),
                        Mathf.Min(p.Area.xMax, box.xMax), Mathf.Min(p.Area.yMax, box.yMax));
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
        public static Rect StairArea(Stair s) => PlanGenerator.StairArea(s, 0f);

        static bool AnyRevealed(ExplorationMap map, int floor, Rect area)
        {
            var lo = ExplorationMap.TileOf(new Vector3(area.xMin, 0f, area.yMin));
            var hi = ExplorationMap.TileOf(new Vector3(area.xMax - 0.01f, 0f, area.yMax - 0.01f));
            int x0 = lo.x, x1 = hi.x, y0 = lo.y, y1 = hi.y;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (map.IsRevealed(floor, x, y)) return true;
            return false;
        }

        static bool SeenAt(ExplorationMap map, int floor, Vector2 p, Vector2 n)
        {
            foreach (var q in new[] { p + n * 0.4f, p - n * 0.4f })
            {
                var tile = ExplorationMap.TileOf(new Vector3(q.x, 0f, q.y));
                if (map.IsRevealed(floor, tile.x, tile.y)) return true;
            }
            return false;
        }

        /// <summary>One wall, cut into the pieces next to revealed tiles; door / vent gaps and railings drawn thin.</summary>
        static void Wall(ExplorationMap map, HousePlan plan, int index, List<MapRect> output)
        {
            var w = plan.Walls[index];
            float len = w.Length;
            if (len < 0.01f) return;
            var dir = w.Dir;
            var n = new Vector2(-dir.y, dir.x);
            var openings = plan.OpeningsOf(index);
            foreach (var o in openings) if (o.Kind == EdgeType.Open) return;

            // revealed runs along the wall
            s_Run.Clear();
            for (float a = 0f; a < len; a += Step)
            {
                float b = Mathf.Min(len, a + Step);
                if (!SeenAt(map, w.Floor, w.At((a + b) / 2f), n)) continue;
                if (s_Run.Count > 0 && Mathf.Abs(s_Run[^1].b - a) < 0.001f) s_Run[^1] = (s_Run[^1].a, b);
                else s_Run.Add((a, b));
            }
            if (s_Run.Count == 0) return;
            var part = w.Railing ? MapPart.Railing : MapPart.Wall;
            float width = w.Railing ? GapWidth : WallWidth;
            foreach (var (a, b) in s_Run)
            {
                // minus the openings
                float from = a;
                foreach (var o in openings)
                {
                    float half = ClearWidth(o.Width) / 2f;
                    float o0 = o.At - half, o1 = o.At + half;
                    if (o1 <= from || o0 >= b) continue;
                    Strip(output, part, w, from, Mathf.Max(from, o0), width);
                    Strip(output, o.Kind is EdgeType.Vent or EdgeType.Squeeze ? MapPart.Vent : MapPart.Door, w, Mathf.Max(a, o0), Mathf.Min(b, o1), GapWidth);
                    from = Mathf.Min(b, o1);
                }
                Strip(output, part, w, from, b, width);
            }
        }

        static void Strip(List<MapRect> output, MapPart part, PlanWall w, float a, float b, float width)
        {
            if (b - a < 0.01f) return;
            var dir = w.Dir;
            var n = new Vector2(-dir.y, dir.x);
            var start = w.At(a);
            float len = b - a;
            // axis-aligned walls stay plain rectangles; others turn about their min corner
            if (Mathf.Abs(dir.x) > 0.999f || Mathf.Abs(dir.y) > 0.999f)
            {
                var end = w.At(b);
                var min = Vector2.Min(start, end) - new Vector2(Mathf.Abs(n.x), Mathf.Abs(n.y)) * (width / 2f);
                var size = new Vector2(Mathf.Abs(end.x - start.x), Mathf.Abs(end.y - start.y)) + new Vector2(Mathf.Abs(n.x), Mathf.Abs(n.y)) * width;
                output.Add(new MapRect(part, new Rect(min, size)));
                return;
            }
            var corner = start - n * (width / 2f);
            output.Add(new MapRect(part, new Rect(corner, new Vector2(len, width)), Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg));
        }
    }
}
