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
            // a book (2 x 2, anywhere, most in a library), canned food (2 x 2, kitchens), a screwdriver (1 x 3, tech rooms: in
            // hand it takes the closed lids off the vents), a belt bag (2 x 2 lying about; picked up it is worn: a 4 x 4 panel)
            new Shape { Id = "book",        Cells = new[] { V(0, 0), V(1, 0), V(0, 1), V(1, 1) } },
            new Shape { Id = "cans",        Cells = new[] { V(0, 0), V(1, 0), V(0, 1), V(1, 1) } },
            new Shape { Id = "screwdriver", Cells = new[] { V(0, 0), V(0, 1), V(0, 2) } },
            new Shape { Id = "beltbag",     Cells = new[] { V(0, 0), V(1, 0), V(0, 1), V(1, 1) } },
            // the map to the survivors' shelter (3 x 3), hidden in a flat of an apartment block: held, it opens in front of you
            new Shape { Id = "map", Cells = new[] { V(0, 0), V(1, 0), V(2, 0), V(0, 1), V(1, 1), V(2, 1), V(0, 2), V(1, 2), V(2, 2) } },
            // the classes' tools (a class starts with one, they also lie about): the radio hears the team from anywhere,
            // the keyboard hacks robots, the tablet is a map terminal in the hand
            new Shape { Id = "radio",    Cells = new[] { V(0, 0), V(0, 1) } },
            new Shape { Id = "keyboard", Cells = new[] { V(0, 0), V(1, 0), V(2, 0) } },
            new Shape { Id = "tablet",   Cells = new[] { V(0, 0), V(1, 0), V(0, 1), V(1, 1) } },
            // the starting items (one more each at the start, they also lie about): the medkit heals, the coffee speeds you
            // up, the fuel finder blinks faster near loot, the boots soften any fall
            new Shape { Id = "medkit",   Cells = new[] { V(0, 0), V(1, 0), V(0, 1), V(1, 1) } },
            new Shape { Id = "coffee",   Cells = new[] { V(0, 0) } },
            new Shape { Id = "finder",   Cells = new[] { V(0, 0), V(0, 1) } },
            new Shape { Id = "boots",    Cells = new[] { V(0, 0), V(1, 0), V(0, 1), V(1, 1) } },
            // junk: worth only its fuel, each of its own rarity (Rarity)
            new Shape { Id = "tomatoes",    Cells = new[] { V(0, 0) } },
            new Shape { Id = "chess",       Cells = new[] { V(0, 0), V(1, 0), V(0, 1), V(1, 1) } },
            new Shape { Id = "magazine",    Cells = new[] { V(0, 0), V(1, 0), V(0, 1), V(1, 1) } },
            new Shape { Id = "pills",       Cells = new[] { V(0, 0) } },
            new Shape { Id = "toothpaste",  Cells = new[] { V(0, 0), V(0, 1) } },
            new Shape { Id = "tape",        Cells = new[] { V(0, 0) } },
            new Shape { Id = "matches",     Cells = new[] { V(0, 0) } },
            new Shape { Id = "toiletpaper", Cells = new[] { V(0, 0) } },
            new Shape { Id = "applecore",   Cells = new[] { V(0, 0) } },
            new Shape { Id = "soap",        Cells = new[] { V(0, 0) } },
            new Shape { Id = "gloves",      Cells = new[] { V(0, 0), V(0, 1) } },
            new Shape { Id = "necklace",    Cells = new[] { V(0, 0), V(1, 0) } },
            new Shape { Id = "mop",         Cells = new[] { V(0, 0), V(0, 1), V(0, 2), V(0, 3) } },
            new Shape { Id = "plush",       Cells = new[] { V(0, 0), V(1, 0), V(0, 1) } },
            new Shape { Id = "socks",       Cells = new[] { V(0, 0), V(0, 1) } },
            // more loot: an SSD drive, a game controller (2 x 1), a gaming keyboard (3 x 1: not the hacker's tool), a fuel
            // canister (2 x 2, the best fuel there is) and a roll of bandages (in the hand it heals a little)
            new Shape { Id = "ssd",          Cells = new[] { V(0, 0) } },
            new Shape { Id = "gamepad",      Cells = new[] { V(0, 0), V(1, 0) } },
            new Shape { Id = "gamekeyboard", Cells = new[] { V(0, 0), V(1, 0), V(2, 0) } },
            new Shape { Id = "canister",     Cells = new[] { V(0, 0), V(1, 0), V(0, 1), V(1, 1) } },
            new Shape { Id = "bandage",      Cells = new[] { V(0, 0) } },
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
        /// <summary>The map to the survivors' shelter: one in every apartment block, hidden in one of its flats (worth no fuel, never burns).</summary>
        public const byte ShelterMap = 20;
        public const byte Radio = 21, Keyboard = 22, Tablet = 23;
        public const byte Medkit = 24, Coffee = 25, Finder = 26, Boots = 27;
        /// <summary>The junk (tomatoes … bandages): plain loot of a fixed rarity each.</summary>
        public const byte FirstJunk = 28, LastJunk = 47;
        public const byte Ssd = 43, Gamepad = 44, GameKeyboard = 45, Canister = 46;
        /// <summary>A roll of bandages: loot like the rest of the junk, and in the hand it heals (<see cref="GadgetSettings.BandageHp"/>).</summary>
        public const byte Bandage = 47;

        /// <summary>The classes a player picks at the start of a run: the tool each starts with (screwdriver, keyboard, radio, tablet).</summary>
        public static readonly byte[] ClassTools = { Screwdriver, Keyboard, Radio, Tablet };
        /// <summary>The extra item a player picks at the start of a run (flashlight, medkit, coffee, fuel finder, boots).</summary>
        public static readonly byte[] StartItems = { Flashlight, Medkit, Coffee, Finder, Boots };

        /// <summary>
        /// The rarity (a colour of <see cref="Colors"/>: grey, green, blue, purple, gold) each thing always has: no roll —
        /// a necklace is always gold, an apple core always grey. -1: none (tools) or made (the diamond: the workbench's colour).
        /// </summary>
        static readonly sbyte[] s_Rarity = BuildRarity();

        static sbyte[] BuildRarity()
        {
            var r = new sbyte[64];
            for (int i = 0; i < r.Length; i++) r[i] = -1;
            // (the old tetris pieces keep a rolled colour: the value budget is still measured against them, ItemsPerOldItem)
            r[Shovel] = 1; r[Battery] = 1; r[Book] = 0; r[Cans] = 1;
            r[28] = 1; // tomatoes
            r[29] = 3; // chess
            r[30] = 1; // magazine
            r[31] = 2; // pills
            r[32] = 0; // toothpaste
            r[33] = 1; // tape
            r[34] = 0; // matches
            r[35] = 0; // toilet paper
            r[36] = 0; // apple core
            r[37] = 0; // soap
            r[38] = 1; // gloves
            r[39] = 4; // necklace
            r[40] = 0; // mop
            r[41] = 2; // plush
            r[42] = 1; // socks
            r[Ssd] = 3;
            r[Gamepad] = 2;
            r[GameKeyboard] = 2;
            r[Canister] = 4; // fuel: gold
            r[Bandage] = 1;
            return r;
        }

        /// <summary>The fixed rarity of a thing (a colour index), -1 if it has none (<see cref="s_Rarity"/>).</summary>
        public static int Rarity(int shape) => shape >= 0 && shape < s_Rarity.Length ? s_Rarity[shape] : -1;

        /// <summary>The colour a thing counts as: its fixed rarity if it has one, else its own colour byte.</summary>
        static int RarityOr(int shape, int color) => Rarity(shape) >= 0 && shape != Diamond ? Mathf.Min(Rarity(shape), Colors.Length - 1) : color;

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

        /// <summary>
        /// The colour byte a new item of this shape spawns with: a flashlight comes fully charged, a thing of a fixed rarity
        /// has its rarity whatever was rolled (<see cref="Rarity"/>), the rest keep the colour given.
        /// </summary>
        public static byte FreshColor(int shape, int color) => shape == Flashlight ? FullCharge : (byte)RarityOr(shape, color);

        /// <summary>Real items fall and tumble in the world (a Rigidbody on the server); the tetris pieces stay put.</summary>
        public static bool HasPhysics(int shape) => shape == Shovel || shape == Flashlight || shape == Battery || shape == Key || shape == CageKey || shape == Stun
            || shape is Book or Cans or Screwdriver or BeltBag or ShelterMap || shape >= Radio;

        /// <summary>How much larger than life the keys' models are (Tools/Blender/rooms2_special.py): found on a floor.</summary>
        public const float KeyScale = 2f, CageKeyScale = 1.6f;

        /// <summary>What can go into the hand slot (and be used with the hand key).</summary>
        public static bool Holdable(int shape) => shape == Flashlight || shape == Battery || shape == Key || shape == CageKey || shape == Stun || shape == Screwdriver || shape == ShelterMap
            || shape is Radio or Keyboard or Tablet or Medkit or Coffee or Finder or Bandage;

        /// <summary>Used up from the hand (the gadget key): the medkit heals in full, a bandage a little, the coffee speeds you up.</summary>
        public static bool Consumable(int shape) => shape is Medkit or Coffee or Bandage;

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
            if (shape == ShelterMap) return new Vector3(0.30f, 0.025f, 0.22f); // folded (Tools/Blender/apartments.py item_map_model)
            if (shape == Stun) return new Vector3(0.36f, 0.291f, 0.318f); // items_more.py item_stun_model
            if (shape == Book) return new Vector3(0.24f, 0.042f, 0.17f);
            if (shape == Cans) return new Vector3(0.159f, 0.111f, 0.101f);
            if (shape == Screwdriver) return new Vector3(0.23f, 0.033f, 0.035f);
            if (shape == BeltBag) return new Vector3(0.262f, 0.079f, 0.138f);
            // the classes' tools, the starting items and the junk (Tools/Blender/items_more.py prints their bounds)
            if (shape >= Radio && shape - Radio < GearSizes.Length) return GearSizes[shape - Radio];
            var size = Size(Shapes[shape].Cells);
            return new Vector3(size.x * StorageMath.ItemCell, StorageMath.ItemCell, size.y * StorageMath.ItemCell);
        }

        /// <summary>
        /// How a thing sits in the hand (in the view and in front of a teammate): <see cref="Length"/> its longest side there,
        /// metres; <see cref="Forward"/> which end of its model's X (as imported: Blender's X is mirrored) points ahead — +1,
        /// −1, or 0 for a compact thing held in the palm; <see cref="Grip"/> where along it the fist closes, a share of its
        /// length from the held end; <see cref="Roll"/> degrees about the way it points (a key's flat face to the side).
        /// </summary>
        /// <summary>
        /// How a thing sits in the fist. <see cref="Length"/>: its longest side, metres. A long thing (<see cref="Forward"/>
        /// ±1: which end of the model's X points ahead — the Unity import mirrors Blender's X, so a part modelled at Blender
        /// +X is at −X here) is gripped <see cref="Grip"/> of its length from the held end, turned <see cref="Roll"/>° about
        /// its length and tipped up <see cref="Pitch"/>° (90: upright). A compact one (Forward 0) rests on the fingers,
        /// rolled <see cref="Roll"/>° about the model's Z (−90 stands a thing lying along X on its −X end… its −X up),
        /// turned <see cref="Yaw"/>° and tilted towards the eyes <see cref="Pitch"/>°.
        /// </summary>
        public readonly struct HeldPose
        {
            public readonly float Length, Forward, Grip, Roll, Pitch, Yaw;
            public HeldPose(float length, float forward, float grip, float roll, float pitch = 0f, float yaw = -90f)
            {
                Length = length; Forward = forward; Grip = grip; Roll = roll; Pitch = pitch; Yaw = yaw;
            }
            public bool Compact => Forward == 0f;
        }

        /// <summary>
        /// The way each thing is held: keys bow in the fist, blade ahead; the screwdriver by its handle, tip ahead; the radio
        /// upright, antenna up, its face to the eyes; the fuel finder by its ribbed grip, lamp and antenna ahead; the tablet
        /// across the hand, screen tilted to the eyes; the medkit latches out; the rest in the palm.
        /// </summary>
        public static HeldPose HeldPoseOf(int shape)
        {
            if (shape == Key) return new HeldPose(0.13f, 1f, 0.12f, 75f);        // blade at +X (the bow at −X)
            if (shape == CageKey) return new HeldPose(0.2f, 1f, 0.1f, 75f);      // the bit at +X, the trefoil bow at −X
            if (shape == Screwdriver) return new HeldPose(0.23f, -1f, 0.24f, 0f); // the handle at +X, the blade at −X
            if (shape == Battery) return new HeldPose(0.07f, 0f, 0f, 0f);
            if (shape == Stun) return new HeldPose(0.13f, 0f, 0f, 0f);
            if (shape == Radio) return new HeldPose(0.19f, -1f, 0.3f, 0f, 78f);  // the antenna at −X: up, the display (+Y) to the eyes
            if (shape == Keyboard) return new HeldPose(0.3f, 0f, 0f, 0f, 0f, 0f); // carried flat across the palm, keys up
            if (shape == Tablet) return new HeldPose(0.22f, 0f, 0f, 0f, -55f, 0f); // across the hand, the screen tilted up to the eyes
            if (shape == Medkit) return new HeldPose(0.2f, 0f, 0f, 0f, 0f, 90f);  // the latches (model +Z) out to the right
            if (shape == Coffee) return new HeldPose(0.11f, 0f, 0f, -90f);   // the jar lies along X, lid at −X: stood up, lid up
            if (shape == Finder) return new HeldPose(0.2f, -1f, 0.22f, 0f, 8f);  // the grip at +X in the fist, lamp and antenna (−X) ahead
            return new HeldPose(0.12f, 0f, 0f, 0f);
        }

        /// <summary>The models' sizes from <see cref="Radio"/> on (x length, y up, z width), metres.</summary>
        static readonly Vector3[] GearSizes =
        {
            new(0.221f, 0.038f, 0.066f), // radio
            new(0.455f, 0.029f, 0.158f), // keyboard
            new(0.263f, 0.025f, 0.195f), // tablet
            new(0.300f, 0.102f, 0.226f), // medkit
            new(0.126f, 0.083f, 0.083f), // coffee
            new(0.241f, 0.051f, 0.076f), // finder
            new(0.317f, 0.231f, 0.257f), // boots
            new(0.111f, 0.079f, 0.079f), // tomatoes
            new(0.300f, 0.084f, 0.160f), // chess
            new(0.280f, 0.023f, 0.211f), // magazine
            new(0.101f, 0.022f, 0.111f), // pills
            new(0.202f, 0.033f, 0.054f), // toothpaste
            new(0.100f, 0.049f, 0.103f), // tape
            new(0.128f, 0.022f, 0.062f), // matches
            new(0.101f, 0.110f, 0.185f), // toilet paper
            new(0.103f, 0.057f, 0.057f), // apple core
            new(0.090f, 0.033f, 0.064f), // soap
            new(0.236f, 0.031f, 0.211f), // gloves
            new(0.340f, 0.016f, 0.155f), // necklace
            new(0.908f, 0.042f, 0.295f), // mop
            new(0.202f, 0.269f, 0.208f), // plush
            new(0.116f, 0.063f, 0.090f), // socks
            new(0.100f, 0.007f, 0.070f), // ssd
            new(0.148f, 0.044f, 0.087f), // gamepad
            new(0.363f, 0.028f, 0.130f), // gaming keyboard
            new(0.295f, 0.461f, 0.163f), // canister
            new(0.111f, 0.046f, 0.070f), // bandage
        };

        /// <summary>A drawer (0.8 m wide inside) holds it lying flat.</summary>
        public static bool FitsInDrawer(int shape) => ModelSize(shape).x <= 0.8f;

        /// <summary>A diamond (workbench) is worth this many cells of its colour.</summary>
        public static int DiamondValueCells { get; set; } = 10;

        /// <summary>Plain loot: sold, burned, combined at a workbench.</summary>
        public static bool IsLoot(int shape) => shape < LootShapes || shape == Shovel || shape == Book || shape == Cans || IsJunk(shape);
        /// <summary>The junk: tomatoes, chess, a magazine, pills … — loot of a fixed rarity, nothing to use.</summary>
        public static bool IsJunk(int shape) => shape >= FirstJunk && shape <= LastJunk;
        /// <summary>A tool: the key block, the stun cube, the lure speaker, the screwdriver, the belt bag, the shelter map, the classes' tools and the starting items (worth nothing, used instead).</summary>
        public static bool IsTool(int shape) => shape == Key || shape == Stun || shape == Speaker || shape == CageKey || shape == Screwdriver || shape == BeltBag || shape == ShelterMap
            || shape is >= Radio and <= Boots;

        /// <summary>Never becomes fuel, not even one by one at the hatch or the hopper: the shelter map.</summary>
        public static bool Unburnable(int shape) => shape == ShelterMap;

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
            ShelterMap => MapColor,
            Screwdriver => ScrewdriverColor,
            BeltBag => BeltBagColor,
            Radio or Keyboard or Tablet => ClassToolColor,
            Medkit or Coffee or Finder or Boots => StartItemColor,
            _ => Colors[Mathf.Clamp(RarityOr(shape, color), 0, Colors.Length - 1)].Color,
        };

        public static readonly Color KeyColor = new(1.00f, 0.25f, 0.80f), StunColor = new(0.10f, 0.95f, 0.95f), SpeakerColor = new(1.00f, 0.32f, 0.02f),
            CageKeyColor = new(0.55f, 1.00f, 0.15f), FlashlightColor = new(0.95f, 0.78f, 0.25f), BatteryColor = new(0.35f, 0.75f, 0.95f),
            ScrewdriverColor = new(0.95f, 0.10f, 0.30f), BeltBagColor = new(0.55f, 0.40f, 0.25f), MapColor = new(0.86f, 0.74f, 0.52f),
            ClassToolColor = new(0.95f, 0.15f, 0.55f), StartItemColor = new(0.15f, 0.85f, 0.75f);
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
            if (Rarity(shape) >= 0) return Shapes[shape].Cells.Length * Colors[RarityOr(shape, 0)].Multiplier;
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

        public static int Value(int shape, int color) => ValueOf(shape, RarityOr(shape, color));

        static int ValueOf(int shape, int color) =>
            shape == ShelterMap ? 0
            : Plain(shape) ? Shapes[shape].Cells.Length * Colors[0].Multiplier * PointsPerCell
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

        /// <summary>The table key of a thing's name (<c>item.name.&lt;id&gt;</c>): the classes' tools, the starting items, the junk.</summary>
        public static string NameKey(int shape) => "item.name." + Shapes[shape].Id;

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
            _ when IsTool(shape) => Loc.T(NameKey(shape)),
            _ when IsJunk(shape) => Loc.F("item.junk", Loc.T(NameKey(shape)), ColorName(Rarity(shape)), Value(shape, color)),
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
