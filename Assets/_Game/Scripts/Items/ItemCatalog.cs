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
        };

        public static ItemColor[] DefaultColors() => new[]
        {
            new ItemColor { Name = "Серый",      Color = new Color(0.62f, 0.64f, 0.66f), Multiplier = 1, SpawnWeight = 40 },
            new ItemColor { Name = "Зелёный",    Color = new Color(0.30f, 0.80f, 0.35f), Multiplier = 2, SpawnWeight = 28 },
            new ItemColor { Name = "Синий",      Color = new Color(0.25f, 0.50f, 0.95f), Multiplier = 3, SpawnWeight = 18 },
            new ItemColor { Name = "Фиолетовый", Color = new Color(0.70f, 0.35f, 0.90f), Multiplier = 5, SpawnWeight = 10 },
            new ItemColor { Name = "Золотой",    Color = new Color(1.00f, 0.80f, 0.15f), Multiplier = 8, SpawnWeight = 4 },
        };

        static Vector2Int V(int x, int y) => new Vector2Int(x, y);

        public static int Value(int shape, int color) =>
            Shapes[shape].Cells.Length * Colors[color].Multiplier * PointsPerCell;

        /// <summary>Cells of a shape rotated clockwise <paramref name="rot"/> times, normalized to start at (0,0).</summary>
        public static Vector2Int[] RotatedCells(int shape, int rot)
        {
            var src = Shapes[shape].Cells;
            var cells = new Vector2Int[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var c = src[i];
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

        public static string Describe(int shape, int color) =>
            $"{Colors[color].Name} ({Shapes[shape].Cells.Length} кл.) — {Value(shape, color)} очк.";
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
