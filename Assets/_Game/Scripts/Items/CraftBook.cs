using System.Collections.Generic;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Items
{
    /// <summary>Tuning of the car's workbench, its recipes and blueprints (ItemsConfig → Crafting).</summary>
    [System.Serializable]
    public class CraftingSettings
    {
        [Tooltip("The things a recipe draws its random ingredients from (shape ids of ItemCatalog): common loot of the houses.")]
        public string[] Pool =
        {
            "book", "cans", "battery", "tomatoes", "magazine", "pills", "toothpaste", "tape", "matches", "toiletpaper",
            "applecore", "soap", "gloves", "plush", "socks", "ssd", "gamepad", "gamekeyboard", "bandage",
        };
        [Tooltip("A recipe with a base thing (the screwdriver for the upgraded one…): how many random ingredients join it (min, max).")]
        public Vector2Int ExtraWithBase = new(1, 3);
        [Tooltip("A recipe without a base thing (gas mask, hammer, robot sensor): how many random ingredients (min, max).")]
        public Vector2Int IngredientsAlone = new(2, 4);
        [Tooltip("Chance that a failed combine breaks one of the things on the bench (it is gone).")]
        [Range(0f, 1f)] public float BreakChance = 0.5f;
        [Tooltip("Blueprints lying in every house.")]
        public int BlueprintsPerHouse = 2;
        [Tooltip("Chess pieces every chess detector carried by the team brings into a house.")]
        public int ChessPerDetector = 2;
        [Tooltip("How often each chess piece turns up (pawn, knight, bishop, rook, queen, king): the dearer in chess, the rarer.")]
        public int[] ChessWeights = { 32, 16, 16, 8, 3, 1 };
    }

    /// <summary>
    /// The recipes of one run (pure, unit tested): every run, from its seed, each thing the workbench makes gets its own
    /// set of ingredients — the thing it upgrades (the screwdriver for the upgraded screwdriver) plus random loot. A
    /// blueprint (<see cref="ItemCatalog.Blueprint"/>) carries the index of one recipe in its colour byte. Every peer makes
    /// the same book from the run seed (<see cref="For"/>), so the value of a blueprint and the bench's preview agree.
    /// </summary>
    public sealed class CraftBook
    {
        /// <summary>One recipe: what it makes and what goes in (sorted shapes, a thing can appear once).</summary>
        public sealed class Recipe
        {
            public byte Result;
            public byte[] Ingredients;
        }

        /// <summary>What the workbench makes (its recipes in this order: a blueprint's colour byte is the index).</summary>
        public static readonly byte[] Results =
        {
            ItemCatalog.ProScrewdriver, ItemCatalog.ProKeyboard, ItemCatalog.ProTablet, ItemCatalog.Espresso, ItemCatalog.JumpBoots,
            ItemCatalog.GasMask, ItemCatalog.Hammer, ItemCatalog.RoboSensor, ItemCatalog.ChessDetector, ItemCatalog.ProFinder,
        };

        /// <summary>The thing a result is always made from (255: none — any random ingredients).</summary>
        public static byte BaseOf(byte result) => result switch
        {
            ItemCatalog.ProScrewdriver => ItemCatalog.Screwdriver,
            ItemCatalog.ProKeyboard => ItemCatalog.Keyboard,
            ItemCatalog.ProTablet => ItemCatalog.Tablet,
            ItemCatalog.Espresso => ItemCatalog.Coffee,
            ItemCatalog.JumpBoots => ItemCatalog.Boots,
            ItemCatalog.ChessDetector => 29, // the folded chessboard
            ItemCatalog.ProFinder => ItemCatalog.Finder,
            _ => byte.MaxValue,
        };

        public const int BenchSize = 4;

        readonly List<Recipe> m_Recipes = new();
        public IReadOnlyList<Recipe> Recipes => m_Recipes;
        public int RunSeed { get; }

        static CraftBook s_Current;
        /// <summary>The book of the run being played (<see cref="Use"/>); a book of seed 0 before any.</summary>
        public static CraftBook Current => s_Current ??= new CraftBook(0, null);

        /// <summary>Makes <see cref="Current"/> the book of this run (kept while the seed is the same).</summary>
        public static CraftBook Use(int runSeed, CraftingSettings s)
        {
            if (s_Current == null || s_Current.RunSeed != runSeed) s_Current = new CraftBook(runSeed, s);
            return s_Current;
        }

        public CraftBook(int runSeed, CraftingSettings s)
        {
            s ??= new CraftingSettings();
            RunSeed = runSeed;
            var pool = new List<byte>();
            if (s.Pool != null)
                foreach (var id in s.Pool)
                {
                    int i = System.Array.FindIndex(ItemCatalog.Shapes, x => x.Id == id);
                    if (i >= 0 && !pool.Contains((byte)i)) pool.Add((byte)i);
                }
            if (pool.Count < 4) pool.AddRange(new byte[] { ItemCatalog.Book, ItemCatalog.Cans, ItemCatalog.Battery, 28 }); // tomatoes
            var rng = new System.Random(LevelRandom.Mix(runSeed, Salt.Recipes, 0));
            var taken = new HashSet<string>();
            foreach (var result in Results)
            {
                byte base_ = BaseOf(result);
                Recipe made = null;
                for (int attempt = 0; attempt < 64 && made == null; attempt++)
                {
                    var range = base_ != byte.MaxValue ? s.ExtraWithBase : s.IngredientsAlone;
                    int n = Mathf.Clamp(rng.Next(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y) + 1), 1, pool.Count);
                    var list = new List<byte>();
                    if (base_ != byte.MaxValue) list.Add(base_);
                    while (list.Count < n + (base_ != byte.MaxValue ? 1 : 0))
                    {
                        byte pick = pool[rng.Next(pool.Count)];
                        if (!list.Contains(pick)) list.Add(pick);
                    }
                    if (base_ == byte.MaxValue && list.Count < 2) continue;
                    list.Sort();
                    string key = Key(list);
                    if (taken.Contains(key) || !BenchRules.Pack(list, BenchSize, BenchSize, null)) continue;
                    taken.Add(key);
                    made = new Recipe { Result = result, Ingredients = list.ToArray() };
                }
                // (never happens with the default pool: the base thing alone with the fewest ingredients)
                made ??= new Recipe { Result = result, Ingredients = base_ != byte.MaxValue ? new[] { base_, pool[0] } : new[] { pool[0], pool[1] } };
                m_Recipes.Add(made);
            }
        }

        static string Key(List<byte> sorted) => string.Join(",", sorted);

        /// <summary>The recipe these things are, exactly (the same things, each once, nothing more), -1 if none.</summary>
        public int Match(IReadOnlyList<byte> shapes)
        {
            if (shapes == null || shapes.Count < 2) return -1;
            var sorted = new List<byte>(shapes);
            sorted.Sort();
            for (int i = 0; i < m_Recipes.Count; i++)
            {
                var r = m_Recipes[i].Ingredients;
                if (r.Length != sorted.Count) continue;
                bool same = true;
                for (int k = 0; k < r.Length && same; k++) same = r[k] == sorted[k];
                if (same) return i;
            }
            return -1;
        }

        public Recipe Get(int index) => index >= 0 && index < m_Recipes.Count ? m_Recipes[index] : null;

        /// <summary>How many things a blueprint's recipe takes (1 for a broken index).</summary>
        public int Size(int recipe) => Get(recipe)?.Ingredients.Length ?? 1;

        /// <summary>A blueprint's rarity: the more things to combine, the rarer (two green, three blue, four or more purple).</summary>
        public int BlueprintRarity(int recipe) => Mathf.Clamp(Size(recipe) - 1, 1, 3);
    }

    /// <summary>The workbench's rules (pure, unit tested): what pressing «combine» does and where things go on its grid.</summary>
    public static class BenchRules
    {
        /// <summary>What a combine does: makes a recipe's result, or (failed) breaks one of the things, or nothing.</summary>
        public struct Outcome
        {
            /// <summary>The recipe made (its result replaces everything on the bench), -1: failed.</summary>
            public int Recipe;
            /// <summary>Failed: the index of the thing that broke, -1 if none did.</summary>
            public int Broken;
        }

        /// <summary>The bench's things (at least two) combined: a recipe if they are exactly one, else maybe one breaks.</summary>
        public static Outcome Combine(IReadOnlyList<InventoryEntry> bench, CraftBook book, System.Random rng, float breakChance)
        {
            var o = new Outcome { Recipe = -1, Broken = -1 };
            if (bench == null || bench.Count < 2) return o;
            o.Recipe = book.Match(Shapes(bench));
            if (o.Recipe >= 0) return o;
            if (rng.NextDouble() < breakChance) o.Broken = rng.Next(bench.Count);
            return o;
        }

        public static List<byte> Shapes(IReadOnlyList<InventoryEntry> items)
        {
            var list = new List<byte>(items.Count);
            foreach (var e in items) list.Add(e.Shape);
            return list;
        }

        /// <summary>Whether a press of «combine» can do anything: two things or more on the bench.</summary>
        public static bool CanCombine(int count) => count >= 2;

        /// <summary>
        /// The preview over the button: the recipe these things are if the team already made it this run (its result is
        /// known), -1 otherwise («result unknown»).
        /// </summary>
        public static int KnownResult(IReadOnlyList<InventoryEntry> bench, CraftBook book, int knownMask)
        {
            int r = book.Match(Shapes(bench));
            return r >= 0 && IsKnown(knownMask, r) ? r : -1;
        }

        public static bool IsKnown(int knownMask, int recipe) => recipe >= 0 && recipe < 31 && (knownMask & (1 << recipe)) != 0;

        /// <summary>
        /// Whether all these shapes fit a grid of <paramref name="w"/> x <paramref name="h"/> together (any turn, mirrored or
        /// not; the biggest first). <paramref name="placed"/>, if given, gets (x, y, rot) per shape in the order given.
        /// </summary>
        public static bool Pack(IReadOnlyList<byte> shapes, int w, int h, List<(byte x, byte y, byte rot)> placed)
        {
            var order = new List<int>();
            for (int i = 0; i < shapes.Count; i++) order.Add(i);
            order.Sort((a, b) => ItemCatalog.Cells(shapes[b]).CompareTo(ItemCatalog.Cells(shapes[a])));
            var grid = new bool[w, h];
            var at = new (byte, byte, byte)[shapes.Count];
            if (!Place(0)) return false;
            if (placed != null) { placed.Clear(); placed.AddRange(at); }
            return true;

            bool Place(int k)
            {
                if (k == order.Count) return true;
                int s = shapes[order[k]];
                for (int rot = 0; rot < 8; rot++)
                {
                    int r = (rot & 3) | (rot >= 4 ? ItemCatalog.MirrorBit : 0);
                    var cells = ItemCatalog.RotatedCells(s, r);
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            if (!Fits(cells, x, y)) continue;
                            Mark(cells, x, y, true);
                            at[order[k]] = ((byte)x, (byte)y, (byte)r);
                            if (Place(k + 1)) return true;
                            Mark(cells, x, y, false);
                        }
                }
                return false;
            }

            bool Fits(Vector2Int[] cells, int x, int y)
            {
                foreach (var c in cells)
                {
                    int px = x + c.x, py = y + c.y;
                    if (px < 0 || py < 0 || px >= w || py >= h || grid[px, py]) return false;
                }
                return true;
            }

            void Mark(Vector2Int[] cells, int x, int y, bool on)
            {
                foreach (var c in cells) grid[x + c.x, y + c.y] = on;
            }
        }
    }
}
