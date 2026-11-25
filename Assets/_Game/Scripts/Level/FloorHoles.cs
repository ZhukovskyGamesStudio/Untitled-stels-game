using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The holes the heavy hammer knocks in a floor. The rule (pure, unit tested): a hole is a square of the plan's cells
    /// under the aim, wholly over one room of its floor and wholly over one room of the floor below — never over a wall, a
    /// stair, the lift or a shaft, never in the ground floor. The cut (every peer, from the replicated key
    /// <see cref="OpenKeys.Hole"/>): the slab pieces it falls in are split round it, the shell's floors too (so the floor's
    /// and the ceiling's finish go round it once the style is put on again), and a <see cref="LevelArt.FloorHole"/> — its
    /// broken edge, the rubble below and a navmesh obstacle that keeps robots off the hole — is put in it.
    /// </summary>
    public static class FloorHoles
    {
        /// <summary>The hole's cells for a blow at <paramref name="point"/>: <paramref name="side"/> metres, centred on the nearest cell corner.</summary>
        public static RectInt CellsAt(Vector3 point, float side)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(side / HousePlan.Res));
            int x0 = Mathf.RoundToInt(point.x / HousePlan.Res - n / 2f), z0 = Mathf.RoundToInt(point.z / HousePlan.Res - n / 2f);
            return new RectInt(x0, z0, n, n);
        }

        /// <summary>The floor whose slab a blow at this height hits (its top is the floor's level), -1 if none near.</summary>
        public static int FloorAt(float y)
        {
            int f = Mathf.RoundToInt(y / LevelMetrics.FloorHeight);
            return Mathf.Abs(y - LevelLayout.FloorY(f)) < 0.3f ? f : -1;
        }

        /// <summary>A rect of cells in metres (x, z).</summary>
        public static Rect Metres(RectInt cells) => new(cells.x * HousePlan.Res, cells.y * HousePlan.Res, cells.width * HousePlan.Res, cells.height * HousePlan.Res);

        /// <summary>Whether a hole of these cells may be knocked in floor <paramref name="floor"/>'s slab.</summary>
        public static bool CanBreak(LevelLayout l, int floor, RectInt cells, IEnumerable<(int floor, RectInt cells)> made = null)
        {
            if (l == null || l.Plan == null || floor < 1 || floor >= l.Floors) return false;
            var plan = l.Plan;
            int above = -2, below = -2;
            for (int z = cells.yMin; z < cells.yMax; z++)
                for (int x = cells.xMin; x < cells.xMax; x++)
                {
                    if (plan.KindAt(floor, x, z) != CellKind.Room || plan.KindAt(floor - 1, x, z) != CellKind.Room) return false;
                    int a = plan.IdAt(floor, x, z), b = plan.IdAt(floor - 1, x, z);
                    if (above == -2) { above = a; below = b; }
                    else if (a != above || b != below) return false; // a wall runs across it, above or below
                }
            // a cell of margin round the hole is still the same room: no wall touching its edge
            for (int z = cells.yMin - 1; z <= cells.yMax; z++)
                for (int x = cells.xMin - 1; x <= cells.xMax; x++)
                    if (plan.IdAt(floor, x, z) != above || plan.KindAt(floor, x, z) != CellKind.Room) return false;
            // not a way round a lock: the vault, the hidden room, a room behind a key door
            if (below == l.VaultRoom || below == l.HiddenRoom) return false;
            foreach (var k in l.KeyDoors) if (k.Rooms != null && k.Rooms.Contains(below)) return false;
            var r = Metres(cells);
            foreach (var s in l.Stairs)
                if ((s.Floor == floor || s.Floor + 1 == floor || s.Floor == floor - 1) && PlanGenerator.StairArea(s, 0.6f).Overlaps(r)) return false;
            if (l.HasLift && Grow(l.LiftWell, 0.6f).Overlaps(r)) return false;
            foreach (var h in l.ShaftHoles) if (Grow(h, 0.6f).Overlaps(r)) return false;
            if (made != null)
                foreach (var (f, c) in made)
                    if (f == floor && Grow(Metres(c), 0.01f).Overlaps(r)) return false;
            return true;
        }

        static Rect Grow(Rect r, float m) => new(r.x - m, r.y - m, r.width + 2 * m, r.height + 2 * m);

        /// <summary>
        /// A rect less a hole inside or across it: up to four rects round the hole (the strips left, right, before and behind
        /// it). The whole rect if they do not overlap.
        /// </summary>
        public static List<Rect> Around(Rect whole, Rect hole)
        {
            var list = new List<Rect>();
            if (!whole.Overlaps(hole)) { list.Add(whole); return list; }
            float hx0 = Mathf.Max(whole.xMin, hole.xMin), hx1 = Mathf.Min(whole.xMax, hole.xMax);
            float hz0 = Mathf.Max(whole.yMin, hole.yMin), hz1 = Mathf.Min(whole.yMax, hole.yMax);
            void Add(float x0, float z0, float x1, float z1)
            {
                if (x1 - x0 > 0.001f && z1 - z0 > 0.001f) list.Add(Rect.MinMaxRect(x0, z0, x1, z1));
            }
            Add(whole.xMin, whole.yMin, hx0, whole.yMax);  // left, full depth
            Add(hx1, whole.yMin, whole.xMax, whole.yMax);  // right, full depth
            Add(hx0, whole.yMin, hx1, hz0);                // in front of the hole
            Add(hx0, hz1, hx1, whole.yMax);                // behind it
            return list;
        }

        /// <summary>Names of the level root's children that are a floor's slab (<see cref="LevelBuilder"/>).</summary>
        static bool IsSlab(string name) => name.StartsWith("Slab", System.StringComparison.Ordinal)
            || name.StartsWith("Deck", System.StringComparison.Ordinal) || name.StartsWith("CeilingBoard", System.StringComparison.Ordinal);

        /// <summary>
        /// Every peer: cuts the hole into the built house — the slab pieces round it, the shell's floor rects, the finish of
        /// a room's own ceiling below — and puts the hole's prefab in. The style is put on again afterwards by the caller.
        /// </summary>
        public static void Cut(int floor, RectInt cells)
        {
            var root = ActiveLevel.Root;
            var shell = ActiveLevel.Shell;
            var art = ActiveLevel.Art;
            if (root == null) return;
            var hole = Metres(cells);
            float top = LevelLayout.FloorY(floor), bottom = top - LevelMetrics.SlabThickness - 0.05f;
            var pieces = new List<Transform>();
            foreach (Transform t in root.transform)
            {
                if (!IsSlab(t.name)) continue;
                var p = t.position;
                var s = t.localScale;
                if (p.y > top + 0.05f || p.y < bottom) continue;
                var r = new Rect(p.x - s.x / 2f, p.z - s.z / 2f, s.x, s.z);
                if (r.Overlaps(hole)) pieces.Add(t);
            }
            foreach (var t in pieces)
            {
                var p = t.position;
                var s = t.localScale;
                var r = new Rect(p.x - s.x / 2f, p.z - s.z / 2f, s.x, s.z);
                var prefab = art != null ? art.FloorSlab : null;
                foreach (var part in Around(r, hole))
                {
                    var go = prefab != null ? Object.Instantiate(prefab, root.transform) : Object.Instantiate(t.gameObject, root.transform);
                    go.name = t.name;
                    go.layer = t.gameObject.layer;
                    go.transform.SetPositionAndRotation(new Vector3(part.center.x, p.y, part.center.y), t.rotation);
                    go.transform.localScale = new Vector3(part.width, s.y, part.height);
                }
                Object.Destroy(t.gameObject);
            }
            if (shell != null)
            {
                for (int i = shell.Floors.Count - 1; i >= 0; i--)
                {
                    var f = shell.Floors[i];
                    if (f.Floor != floor) continue;
                    var r = Rect.MinMaxRect(f.Min.x, f.Min.y, f.Max.x, f.Max.y);
                    if (!r.Overlaps(hole)) continue;
                    shell.Floors.RemoveAt(i);
                    foreach (var part in Around(r, hole))
                        shell.Floors.Add(new FloorRect { Min = part.min, Max = part.max, Y = f.Y, Floor = f.Floor });
                }
                shell.FloorHoles.Add((hole, floor));
            }
            if (art != null && art.FloorHole != null)
            {
                var h = Object.Instantiate(art.FloorHole, root.transform);
                h.name = "FloorHole";
                h.transform.SetPositionAndRotation(new Vector3(hole.center.x, top, hole.center.y), Quaternion.identity);
                h.transform.localScale = new Vector3(hole.width, 1f, hole.height);
            }
        }
    }
}
