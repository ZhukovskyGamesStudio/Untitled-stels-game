using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Items
{
    /// <summary>
    /// Placeholder items: tetris pieces. Quota value depends on size (cell count) and color.
    /// Shapes are fixed in code (they match the meshes); colours, values and spawn weights come from ItemsConfig
    /// (the same asset in every build, so server and clients agree).
    /// </summary>
    public static class ItemCatalog
    {
        public struct Shape
        {
            public string Id;
            public Vector2Int[] Cells;
        }

        [System.Serializable]
        public struct ItemColor
        {
            [Tooltip("Name in the inspector; the player sees the table's item.color.<index>")]
            public string Name;
            public Color Color;
            [Tooltip("Value per cell = PointsPerCell × Multiplier.")]
            public int Multiplier;
            [Tooltip("Relative chance to spawn (deeper houses shift towards valuable colours).")]
            public int SpawnWeight;
        }

        /// <summary>Colour table and points per cell; replaced by the tuning in ItemsConfig at startup.</summary>
        public static ItemColor[] Colors { get; private set; } = DefaultColors();
        public static int PointsPerCell { get; private set; } = 5;

        /// <summary>Use these colours and values (all peers load the same config).</summary>
        public static void Configure(ItemColor[] colors, int pointsPerCell)
        {
            if (colors != null && colors.Length > 0) Colors = colors;
            if (pointsPerCell > 0) PointsPerCell = pointsPerCell;
        }

        // Order must match the meshes in ItemVisuals (item_<id>.fbx).
        public static readonly Shape[] Shapes =
        {
            new Shape { Id = "mono",   Cells = new[] { V(0, 0) } },
            new Shape { Id = "domino", Cells = new[] { V(0, 0), V(1, 0) } },
            new Shape { Id = "tri_l",  Cells = new[] { V(0, 0), V(1, 0), V(0, 1) } },
            new Shape { Id = "tet_i",  Cells = new[] { V(0, 0), V(1, 0), V(2, 0), V(3, 0) } },
            new Shape { Id = "tet_o",  Cells = new[] { V(0, 0), V(1, 0), V(0, 1), V(1, 1) } },
            new Shape { Id = "tet_t",  Cells = new[] { V(0, 0), V(1, 0), V(2, 0), V(1, 1) } },
            new Shape { Id = "tet_l",  Cells = new[] { V(0, 0), V(0, 1), V(0, 2), V(1, 0) } },
            new Shape { Id = "tet_s",  Cells = new[] { V(0, 0), V(1, 0), V(1, 1), V(2, 1) } },
            // not loot that lies around: made at a workbench / tools (Diamond … Speaker)
            new Shape { Id = "diamond", Cells = new[] { V(0, 0) } },
            new Shape { Id = "key",     Cells = new[] { V(0, 0) } },
            new Shape { Id = "stun",    Cells = new[] { V(0, 0), V(1, 0), V(0, 1), V(1, 1) } },
            new Shape { Id = "speaker", Cells = new[] { V(0, 0), V(1, 0) } },
            new Shape { Id = "cagekey", Cells = new[] { V(0, 0), V(0, 1) } },
            // the first real loot: a shovel, 1 x 4 in the backpack (its picture: ItemVisuals.Icons), a model with physics in the world
            new Shape { Id = "shovel",  Cells = new[] { V(0, 0), V(0, 1), V(0, 2), V(0, 3) } },
            // the flashlight (1 x 3; it lights when held and switched on, its charge is its colour byte) and its batteries
            new Shape { Id = "flashlight", Cells = new[] { V(0, 0), V(0, 1), V(0, 2) } },
            new Shape { Id = "battery", Cells = new[] { V(0, 0) } },
            // a book (2 x 3, anywhere, most in a library), canned food (2 x 2, kitchens), a screwdriver (1 x 3, tech rooms: in
            // hand it takes the closed lids off the vents), a belt bag (2 x 2 lying about; picked up it is worn: a 4 x 4 panel)
            new Shape { Id = "book",        Cells = new[] { V(0, 0), V(1, 0), V(0, 1), V(1, 1), V(0, 2), V(1, 2) } },
            new Shape { Id = "cans",        Cells = new[] { V(0, 0), V(1, 0), V(0, 1), V(1, 1) } },
            new Shape { Id = "screwdriver", Cells = new[] { V(0, 0), V(0, 1), V(0, 2) } },
            new Shape { Id = "beltbag",     Cells = new[] { V(0, 0), V(1, 0), V(0, 1), V(1, 1) } },
        };

        /// <summary>The first shapes are the loot of the houses (what lies around, robots carry, crates hold).</summary>
        public const int LootShapes = 8;
        public const byte Diamond = 8, Key = 9, Stun = 10, Speaker = 11;
        /// <summary>The key of the hostage's cage (a guard carries it: hack him and he drops it).</summary>
        public const byte CageKey = 12;
        /// <summary>The shovel: the first real loot item (every loot of the houses is a shovel now, <see cref="LootShape"/>).</summary>
        public const byte Shovel = 13;

        public const byte Flashlight = 14, Battery = 15;
        public const byte Book = 16, Cans = 17, Screwdriver = 18, BeltBag = 19;
        /// <summary>A flashlight's full charge (its colour byte holds the charge, 0..FullCharge).</summary>
        public const byte FullCharge = 100;

        [System.Serializable]
        public struct LootWeight
        {
            [Tooltip("Shape id: shovel, flashlight, battery, book, cans, screwdriver, beltbag …")] public string Item;
            [Tooltip("Relative chance")] public int Weight;
            [Tooltip("Only in rooms of these types (empty: anywhere). Canned food: kitchens; the screwdriver: tech rooms.")]
            public Stealth.Level.RoomType[] Only;
            [Tooltip("More likely in rooms of these types (× FavouredBoost): books in a library.")]
            public Stealth.Level.RoomType[] Favoured;
            [Tooltip("How much more likely in a favoured room.")]
            public float FavouredBoost;
        }

        /// <summary>One row of the loot table: the shape, its weight, where only and where most of all.</summary>
        public struct LootRow
        {
            public byte Shape;
            public int Weight;
            public Stealth.Level.RoomType[] Only, Favoured;
            public float Boost;
            /// <summary>Its weight in a room of this type (null: no room — a crate, a safe, a robot's cargo: the anywhere rows only).</summary>
            public float WeightIn(Stealth.Level.RoomType? room)
            {
                bool only = Only != null && Only.Length > 0;
                if (room is not Stealth.Level.RoomType t) return only ? 0f : Weight;
                if (only && System.Array.IndexOf(Only, t) < 0) return 0f;
                return Favoured != null && System.Array.IndexOf(Favoured, t) >= 0 ? Weight * Mathf.Max(1f, Boost) : Weight;
            }
        }

        /// <summary>What the loot of the houses is (by shape, weighted); set from LevelConfig.Loot.Table (only shovels by default).</summary>
        public static LootRow[] LootRows { get; private set; } = { new LootRow { Shape = Shovel, Weight = 1 } };

        /// <summary>The table as (shape, weight) pairs, anywhere (the value budget's view).</summary>
        public static (byte shape, int weight)[] LootTable
        {
            get
            {
                var list = new (byte, int)[LootRows.Length];
                for (int i = 0; i < list.Length; i++) list[i] = (LootRows[i].Shape, LootRows[i].Weight);
                return list;
            }
        }

        /// <summary>Use this loot table (unknown ids are skipped; an empty table means shovels only).</summary>
        public static void ConfigureLoot(LootWeight[] table)
        {
            var list = new List<LootRow>();
            if (table != null)
                foreach (var t in table)
                {
                    int i = System.Array.FindIndex(Shapes, s => s.Id == t.Item);
                    if (i >= 0 && t.Weight > 0) list.Add(new LootRow { Shape = (byte)i, Weight = t.Weight, Only = t.Only, Favoured = t.Favoured, Boost = t.FavouredBoost });
                }
            LootRows = list.Count > 0 ? list.ToArray() : new[] { new LootRow { Shape = Shovel, Weight = 1 } };
        }

        /// <summary>
        /// The loot a house, a robot's cargo, a crate or the vault gets, from the loot table (by default every loot is a
        /// shovel). The old roll among the tetris pieces is still made, so the rest of the stream does not shift much.
        /// </summary>
        public static byte LootShape(System.Random rng) => LootShape(rng, null);

        /// <summary>
        /// The loot lying in a room of this type (null: no room — a crate, a safe, a robot's cargo): rows meant only for
        /// other rooms are out (canned food in a kitchen, the screwdriver in a tech room), favoured rows count more (books
        /// in a library).
        /// </summary>
        public static byte LootShape(System.Random rng, Stealth.Level.RoomType? room)
        {
            rng.Next(LootShapes);
            var rows = LootRows;
            if (rows.Length == 1) return rows[0].Shape;
            float total = 0f;
            foreach (var r in rows) total += r.WeightIn(room);
            if (total <= 0f) return rows[0].Shape;
            float roll = (float)rng.NextDouble() * total;
            foreach (var r in rows)
            {
                float w = r.WeightIn(room);
                if (roll < w) return r.Shape;
                roll -= w;
            }
            return rows[0].Shape;
        }

        /// <summary>The colour byte a new item of this shape spawns with: a flashlight comes fully charged.</summary>
        public static byte FreshColor(int shape, int color) => shape == Flashlight ? FullCharge : (byte)color;

        /// <summary>Real items fall and tumble in the world (a Rigidbody on the server); the tetris pieces stay put.</summary>
        public static bool HasPhysics(int shape) => shape == Shovel || shape == Flashlight || shape == Battery || shape == Key || shape == CageKey
            || shape is Book or Cans or Screwdriver or BeltBag;

        /// <summary>How much larger than life the keys' models are (Tools/Blender/rooms2_special.py): found on a floor.</summary>
        public const float KeyScale = 2f, CageKeyScale = 1.6f;

        /// <summary>What can go into the hand slot (and be used with the hand key).</summary>
        public static bool Holdable(int shape) => shape == Flashlight || shape == Battery || shape == Key || shape == CageKey || shape == Stun || shape == Screwdriver;

        /// <summary>
        /// The size of an item's model, metres: x along its length (the model's X), y its thickness (up), z its width.
        /// The tetris pieces are their cells (<see cref="Items.StorageMath.ItemCell"/> each); the shovel is a 1.1 m spade
        /// (Tools/Blender/poi.py shovel_parts).
        /// </summary>
        public static Vector3 ModelSize(int shape)
        {
            if (shape == Shovel) return new Vector3(1.105f, 0.06f, 0.22f);
            if (shape == Flashlight) return new Vector3(0.263f, 0.065f, 0.065f);
            if (shape == Battery) return new Vector3(0.066f, 0.032f, 0.061f);
            if (shape == Key) return new Vector3(0.086f, 0.012f, 0.024f) * KeyScale;
            if (shape == CageKey) return new Vector3(0.178f, 0.02f, 0.054f) * CageKeyScale;
            if (shape == Book) return new Vector3(0.24f, 0.042f, 0.17f);
            if (shape == Cans) return new Vector3(0.159f, 0.111f, 0.101f);
            if (shape == Screwdriver) return new Vector3(0.23f, 0.033f, 0.035f);
            if (shape == BeltBag) return new Vector3(0.262f, 0.079f, 0.138f);
            var size = Size(Shapes[shape].Cells);
            return new Vector3(size.x * StorageMath.ItemCell, StorageMath.ItemCell, size.y * StorageMath.ItemCell);
        }

        /// <summary>A drawer (0.8 m wide inside) holds it lying flat.</summary>
        public static bool FitsInDrawer(int shape) => ModelSize(shape).x <= 0.8f;

        /// <summary>A diamond (workbench) is worth this many cells of its colour.</summary>
        public static int DiamondValueCells { get; set; } = 10;

        /// <summary>Plain loot: sold, burned, combined at a workbench.</summary>
        public static bool IsLoot(int shape) => shape < LootShapes || shape == Shovel || shape == Book || shape == Cans;
        /// <summary>A tool: the key block, the stun cube, the lure speaker, the screwdriver, the belt bag (worth nothing, used instead).</summary>
        public static bool IsTool(int shape) => shape == Key || shape == Stun || shape == Speaker || shape == CageKey || shape == Screwdriver || shape == BeltBag;

        /// <summary>
        /// The colour a thing is drawn with in the inventory and the stash: loot by its colour (its value), the tools in
        /// colours no loot has — magenta key, cyan stun, orange speaker — so they never pass for a cube of loot.
        /// </summary>
        public static Color UiColor(int shape, int color) => shape switch
        {
            Key => KeyColor,
            Stun => StunColor,
            Speaker => SpeakerColor,
            CageKey => CageKeyColor,
            Flashlight => FlashlightColor,
            Battery => BatteryColor,
            Screwdriver => ScrewdriverColor,
            BeltBag => BeltBagColor,
            _ => Colors[Mathf.Clamp(color, 0, Colors.Length - 1)].Color,
        };

        public static readonly Color KeyColor = new(1.00f, 0.25f, 0.80f), StunColor = new(0.10f, 0.95f, 0.95f), SpeakerColor = new(1.00f, 0.32f, 0.02f),
            CageKeyColor = new(0.55f, 1.00f, 0.15f), FlashlightColor = new(0.95f, 0.78f, 0.25f), BatteryColor = new(0.35f, 0.75f, 0.95f),
            ScrewdriverColor = new(0.95f, 0.10f, 0.30f), BeltBagColor = new(0.55f, 0.40f, 0.25f);
        public static int Cells(int shape) => Shapes[shape].Cells.Length;

        public static ItemColor[] DefaultColors() => new[]
        {
            new ItemColor { Name = "Grey",       Color = new Color(0.62f, 0.64f, 0.66f), Multiplier = 1, SpawnWeight = 40 },
            new ItemColor { Name = "Green",      Color = new Color(0.30f, 0.80f, 0.35f), Multiplier = 2, SpawnWeight = 28 },
            new ItemColor { Name = "Blue",       Color = new Color(0.25f, 0.50f, 0.95f), Multiplier = 3, SpawnWeight = 18 },
            new ItemColor { Name = "Purple",     Color = new Color(0.70f, 0.35f, 0.90f), Multiplier = 5, SpawnWeight = 10 },
            new ItemColor { Name = "Gold",       Color = new Color(1.00f, 0.80f, 0.15f), Multiplier = 8, SpawnWeight = 4 },
        };

        static Vector2Int V(int x, int y) => new Vector2Int(x, y);

        /// <summary>
        /// The flashlight burns into fuel like loot (in the car's hopper, into the entry hatch), worth its cells at the grey
        /// colour's rate whatever its charge (its colour byte holds the charge). So do the tools (key, stun, speaker, cage key):
        /// whatever their colour, they burn at the grey rate. Batteries have a colour like any loot.
        /// </summary>
        static bool Plain(int shape) => shape == Flashlight || IsTool(shape);

        /// <summary>The value of a loot item in cells of the grey colour, on average (its cells × the mean colour multiplier).</summary>
        public static float MeanValueCells(int shape)
        {
            if (Plain(shape)) return Shapes[shape].Cells.Length * Colors[0].Multiplier;
            float w = 0f, m = 0f;
            foreach (var c in Colors) { w += c.SpawnWeight; m += c.SpawnWeight * c.Multiplier; }
            return Shapes[shape].Cells.Length * (w > 0f ? m / w : 1f);
        }

        /// <summary>
        /// How many items of the loot table make up the value the old loot (the tetris pieces, drawn evenly) had: the
        /// house's loot budget, and so its quota (a share of the loot's value), stays as it was whatever the table is.
        /// </summary>
        public static float ItemsPerOldItem()
        {
            float old = 0f;
            for (int i = 0; i < LootShapes; i++) old += MeanValueCells(i);
            old /= LootShapes;
            float now = 0f, w = 0f;
            foreach (var t in LootTable) { now += t.weight * MeanValueCells(t.shape); w += t.weight; }
            now = w > 0f ? now / w : old;
            return now > 0f ? old / now : 1f;
        }

        public static int Value(int shape, int color) =>
            Plain(shape) ? Shapes[shape].Cells.Length * Colors[0].Multiplier * PointsPerCell
            : (shape == Diamond ? DiamondValueCells : Shapes[shape].Cells.Length) * Colors[color].Multiplier * PointsPerCell;

        /// <summary>Bit of an item's rotation byte: the piece is mirrored (left-right) before it is turned.</summary>
        public const int MirrorBit = 4;

        public static bool IsMirrored(int rot) => (rot & MirrorBit) != 0;

        /// <summary>The next quarter turn, keeping the mirror.</summary>
        public static int Turned(int rot) => (rot & MirrorBit) | ((rot + 1) & 3);

        /// <summary>The same piece flipped left-right (its turn kept).</summary>
        public static int Mirrored(int rot) => rot ^ MirrorBit;

        /// <summary>
        /// Cells of a shape — mirrored if <see cref="MirrorBit"/> is set, then rotated clockwise (<paramref name="rot"/> &amp; 3)
        /// times — normalized to start at (0,0).
        /// </summary>
        public static Vector2Int[] RotatedCells(int shape, int rot)
        {
            var src = Shapes[shape].Cells;
            var cells = new Vector2Int[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var c = src[i];
                if (IsMirrored(rot)) c.x = -c.x;
                for (int r = 0; r < (rot & 3); r++) c = new Vector2Int(-c.y, c.x);
                cells[i] = c;
            }
            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (var c in cells) { minX = Mathf.Min(minX, c.x); minY = Mathf.Min(minY, c.y); }
            for (int i = 0; i < cells.Length; i++) cells[i] -= new Vector2Int(minX, minY);
            return cells;
        }

        public static Vector2Int Size(Vector2Int[] cells)
        {
            int w = 0, h = 0;
            foreach (var c in cells) { w = Mathf.Max(w, c.x + 1); h = Mathf.Max(h, c.y + 1); }
            return new Vector2Int(w, h);
        }

        public static int RandomColor(System.Random rng, int bonus)
        {
            int total = 0;
            for (int i = 0; i < Colors.Length; i++) total += Weight(i, bonus);
            int roll = rng.Next(total);
            for (int i = 0; i < Colors.Length; i++)
            {
                roll -= Weight(i, bonus);
                if (roll < 0) return i;
            }
            return 0;
        }

        // Deeper levels shift the distribution towards valuable colors.
        static int Weight(int color, int bonus) => Colors[color].SpawnWeight + color * bonus;

        /// <summary>The colour's name for the player (table <c>item.color.&lt;index&gt;</c>, the config's name if the table has none).</summary>
        public static string ColorName(int color) => Loc.Has(ColorKey(color)) ? Loc.T(ColorKey(color)) : Colors[color].Name;

        public static string ColorKey(int color) => "item.color." + color;

        /// <summary>The item's short name for the player: "Синий · 30 очк.", "Ключ".</summary>
        public static string Describe(int shape, int color) => shape switch
        {
            Key => Loc.T("item.key"),
            Stun => Loc.T("item.stun"),
            Speaker => Loc.T("item.speaker"),
            CageKey => Loc.T("item.cagekey"),
            Diamond => Loc.F("item.diamond", ColorName(color), Value(shape, color)),
            Shovel => Loc.F("item.shovel", ColorName(color), Value(shape, color)),
            Flashlight => Loc.F("item.flashlight", Mathf.Min(color, FullCharge)),
            Battery => Loc.F("item.battery", Value(shape, color)),
            Book => Loc.F("item.book", ColorName(color), Value(shape, color)),
            Cans => Loc.F("item.cans", ColorName(color), Value(shape, color)),
            Screwdriver => Loc.T("item.screwdriver"),
            BeltBag => Loc.T("item.beltbag"),
            _ => Loc.F("item.loot", ColorName(color), Value(shape, color)),
        };
    }

    /// <summary>Grid inventory layout. '#' = usable cell, '.' = hole.</summary>
    public static class InventoryLayout
    {
        public static readonly string[] Rows =
        {
            "######",
            "######",
            "######",
            ".####.",
        };

        public static int Width => Rows[0].Length;
        public static int Height => Rows.Length;

        public static bool IsCell(int x, int y) =>
            x >= 0 && y >= 0 && y < Rows.Length && x < Rows[y].Length && Rows[y][x] == '#';

        /// <summary>Checks whether an item fits at (x,y) given already occupied cells.</summary>
        public static bool Fits(Vector2Int[] cells, int x, int y, HashSet<Vector2Int> occupied)
        {
            foreach (var c in cells)
            {
                var p = new Vector2Int(x + c.x, y + c.y);
                if (!IsCell(p.x, p.y) || occupied.Contains(p)) return false;
            }
            return true;
        }
    }
}
