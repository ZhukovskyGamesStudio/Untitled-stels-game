using System.Collections.Generic;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Items
{
    /// <summary>Tuning of loot in a house (LevelConfig).</summary>
    [System.Serializable]
    public class LootSettings
    {
        [Tooltip("Items in the first house.")]
        public int BaseItems = 6;
        [Tooltip("More items per house.")]
        public int ItemsPerHouse = 2;
        [Tooltip("One more item per this many rooms of the building.")]
        public int RoomsPerExtraItem = 3;
        [Tooltip("Items never lie closer than this, meters.")]
        public float MinSpacing = 1.5f;
        [Tooltip("Chance of an item to be hidden in a drawer or a locker instead (it appears when that part is opened).")]
        [Range(0, 1)] public float InStorageChance = 0.3f;
        [Tooltip("What the loot is (shape id and weight): shovel, flashlight, battery. Empty = shovels only.")]
        public ItemCatalog.LootWeight[] Table = { new() { Item = "shovel", Weight = 1 } };

        [Header("Value bonus (shifts colours towards valuable ones)")]
        public int BonusPerHouse = 2;
        public int BonusPerFloor = 4;
        [Tooltip("Dark rooms are riskier and richer.")]
        public int DarkRoomBonus = 6;

        [Header("Locations")]
        [Tooltip("An office has more things lying around (× this) and rarer ones (+ this colour bonus).")]
        public float OfficeItems = 1.5f;
        public int OfficeBonus = 6;
        [Tooltip("The hidden room: this many items in it first, and richer ones.")]
        public int HiddenRoomItems = 3;
        public int HiddenRoomBonus = 14;
        [Tooltip("Behind a key door (the key is a detour): this many items there first, and richer and bigger ones.")]
        public int KeyRoomItems = 3;
        public int KeyRoomBonus = 12;
    }

    /// <summary>Server-side loot placement rules (pure, unit tested).</summary>
    public static class LootPlanner
    {
        public struct Loot
        {
            public Vector3 Position;
            public int Shape;
            public int Color;
        }

        /// <summary>How many items a building gets: grows with the level and the number of rooms.</summary>
        public static int ItemCount(LevelLayout l, LootSettings s = null)
        {
            s ??= new LootSettings();
            int count = s.BaseItems + l.LevelIndex * s.ItemsPerHouse + Mathf.RoundToInt(l.RoomUnits) / Mathf.Max(1, s.RoomsPerExtraItem);
            return l.Location == Location.Office ? Mathf.RoundToInt(count * s.OfficeItems) : count;
        }

        /// <summary>
        /// Picks loot positions: round-robin over floors (every floor gets loot), never closer than
        /// <see cref="LootSettings.MinSpacing"/> to each other. Value bonus: higher floors and dark rooms are richer.
        /// </summary>
        public static List<Loot> Plan(LevelLayout l, System.Random rng, LootSettings s = null)
        {
            s ??= new LootSettings();
            float spacing = s.MinSpacing;
            var byFloor = new List<List<Vector3>>();
            for (int f = 0; f < l.Floors; f++) byFloor.Add(new List<Vector3>());
            foreach (var spot in l.ItemSpots) byFloor[Mathf.Clamp(LevelLayout.FloorOf(spot.y), 0, l.Floors - 1)].Add(spot);

            int want = ItemCount(l, s);
            var result = new List<Loot>();
            var behind = KeyRegions(l);
            int RegionOf(Vector3 p)
            {
                var room = l.Plan.RoomAt(p + Vector3.up * 0.3f);
                if (room == null) return -1;
                for (int k = 0; k < behind.Count; k++) if (behind[k].Contains(room.Id)) return k;
                return -1;
            }
            int Bonus(Vector3 p) => l.LevelIndex * s.BonusPerHouse + LevelLayout.FloorOf(p.y) * s.BonusPerFloor
                + (l.IsDarkAt(p + Vector3.up * 0.5f) ? s.DarkRoomBonus : 0)
                + (l.Location == Location.Office ? s.OfficeBonus : 0)
                + (InHidden(p) ? s.HiddenRoomBonus : 0)
                + (RegionOf(p) >= 0 ? s.KeyRoomBonus : 0);
            bool InHidden(Vector3 p) => l.HiddenRoom >= 0 && l.Plan.RoomAt(p + Vector3.up * 0.3f)?.Id == l.HiddenRoom;
            // in a room worth the detour the shape is the bigger of two draws (bigger things are worth more)
            int Shape(Vector3 p)
            {
                int a = ItemCatalog.LootShape(rng); // from the loot table (shovels, flashlights, batteries…)
                if (!InHidden(p) && RegionOf(p) < 0) return a;
                int b = ItemCatalog.LootShape(rng);
                return ItemCatalog.Value(b, 0) > ItemCatalog.Value(a, 0) ? b : a;
            }
            // the hidden room and the parts behind key doors are worth finding: a few things there first
            void First(System.Func<Vector3, bool> inside, int count)
            {
                int placed = 0;
                foreach (var spots in byFloor)
                    for (int i = spots.Count - 1; i >= 0 && placed < count && result.Count < want; i--)
                    {
                        if (!inside(spots[i])) continue;
                        var p = spots[i];
                        spots.RemoveAt(i);
                        if (result.Exists(r => (r.Position - p).sqrMagnitude < spacing * spacing)) continue;
                        { int shape = Shape(p); result.Add(new Loot { Position = p, Shape = shape, Color = ItemCatalog.FreshColor(shape, ItemCatalog.RandomColor(rng, Bonus(p))) }); }
                        placed++;
                    }
            }
            if (l.HiddenRoom >= 0) First(InHidden, s.HiddenRoomItems);
            for (int k = 0; k < behind.Count; k++)
            {
                // behind a key door: things there first; a bare room gets them on its floor
                int region = k;
                int spots = 0;
                foreach (var list in byFloor) foreach (var q in list) if (RegionOf(q) == region) spots++;
                if (spots < s.KeyRoomItems)
                    foreach (var q in FloorSpots(l, behind[k], s.KeyRoomItems - spots, spacing))
                        byFloor[Mathf.Clamp(LevelLayout.FloorOf(q.y), 0, l.Floors - 1)].Add(q);
                First(p => RegionOf(p) == region, s.KeyRoomItems);
            }
            int floor = 0, misses = 0;
            while (result.Count < want && misses < l.Floors)
            {
                var spots = byFloor[floor];
                floor = (floor + 1) % l.Floors;
                bool placed = false;
                while (spots.Count > 0 && !placed)
                {
                    int i = rng.Next(spots.Count);
                    var p = spots[i];
                    spots.RemoveAt(i);
                    if (result.Exists(r => (r.Position - p).sqrMagnitude < spacing * spacing)) continue;
                    { int shape = Shape(p); result.Add(new Loot { Position = p, Shape = shape, Color = ItemCatalog.FreshColor(shape, ItemCatalog.RandomColor(rng, Bonus(p))) }); }
                    placed = true;
                }
                misses = placed ? 0 : misses + 1;
            }
            return result;
        }

        /// <summary>The parts of the house behind each key door (the key is a detour: things worth it lie there). One set of room ids per door.</summary>
        public static List<HashSet<int>> KeyRegions(LevelLayout l)
        {
            var list = new List<HashSet<int>>();
            foreach (var k in l.KeyDoors) list.Add(new HashSet<int>(k.Rooms));
            return list;
        }

        /// <summary>
        /// Up to <paramref name="count"/> spots on the floor of these rooms, clear of the furniture (0.8 m from any piece's
        /// middle, 0.6 m from the walls) and apart from each other — for a room that has nothing to lay a thing on.
        /// </summary>
        public static List<Vector3> FloorSpots(LevelLayout l, HashSet<int> rooms, int count, float spacing)
        {
            var found = new List<Vector3>();
            var plan = l.Plan;
            foreach (int id in rooms)
            {
                if (id < 0 || id >= plan.Rooms.Count) continue;
                var r = plan.Rooms[id];
                float y = LevelLayout.FloorY(r.Floor);
                // from the room's middle outwards: the middle is the room, the edges are walls and furniture
                var c = new Vector2(r.Center.x, r.Center.z);
                var cells = new List<Vector2>();
                for (float x = r.Bounds.xMin + 0.75f; x < r.Bounds.xMax - 0.5f; x += 0.5f)
                    for (float z = r.Bounds.yMin + 0.75f; z < r.Bounds.yMax - 0.5f; z += 0.5f)
                        cells.Add(new Vector2(x, z));
                cells.Sort((a, b) => (a - c).sqrMagnitude.CompareTo((b - c).sqrMagnitude));
                foreach (var q in cells)
                {
                    if (found.Count >= count) return found;
                    bool inside = true;
                    foreach (var d in new[] { Vector2.zero, new Vector2(0.6f, 0), new Vector2(-0.6f, 0), new Vector2(0, 0.6f), new Vector2(0, -0.6f) })
                        if (plan.RoomAt(new Vector3(q.x + d.x, y + 0.3f, q.y + d.y))?.Id != id) { inside = false; break; }
                    if (!inside) continue;
                    var p = new Vector3(q.x, y + 0.02f, q.y);
                    bool clear = true;
                    foreach (var prop in l.Props)
                        if (LevelLayout.FloorOf(prop.Position.y) == r.Floor && (new Vector2(prop.Position.x - q.x, prop.Position.z - q.y)).magnitude < 0.8f) { clear = false; break; }
                    if (!clear || found.Exists(o => (o - p).magnitude < spacing)) continue;
                    found.Add(p);
                }
            }
            return found;
        }
    }
}
