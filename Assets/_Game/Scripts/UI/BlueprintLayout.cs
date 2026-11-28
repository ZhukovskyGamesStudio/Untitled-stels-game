using UnityEngine;

namespace Stealth.UI
{
    /// <summary>
    /// How a blueprint shows its recipe in a grid (pure, unit tested): the pictures of the things it takes and, in the last
    /// place, the thing it makes — as big as they can be with all of them on the sheet, in a grid centred on it.
    /// </summary>
    public static class BlueprintLayout
    {
        /// <summary>
        /// The columns, rows and the size of a picture for <paramref name="count"/> pictures on a sheet
        /// <paramref name="width"/> × <paramref name="height"/> (<paramref name="gap"/> between them, never bigger than
        /// <paramref name="most"/>): the arrangement that gives the biggest pictures.
        /// </summary>
        public static (int columns, int rows, float size) Fit(int count, float width, float height, float gap, float most)
        {
            count = Mathf.Max(1, count);
            int bestColumns = 1;
            float best = -1f;
            for (int columns = 1; columns <= count; columns++)
            {
                int rows = Mathf.CeilToInt(count / (float)columns);
                float size = Mathf.Min((width - (columns - 1) * gap) / columns, (height - (rows - 1) * gap) / rows);
                if (size > best + 1e-4f) { best = size; bestColumns = columns; }
            }
            int r = Mathf.CeilToInt(count / (float)bestColumns);
            return (bestColumns, r, Mathf.Clamp(best, 1f, most));
        }

        /// <summary>
        /// Where picture <paramref name="index"/> of <paramref name="count"/> goes (column, row): the things in reading order,
        /// the last one — what it makes — always in the bottom right place, apart from them when the grid has room to spare.
        /// </summary>
        public static Vector2Int Place(int index, int count, int columns, int rows)
        {
            int at = index == count - 1 ? columns * rows - 1 : index;
            return new Vector2Int(at % columns, at / columns);
        }
    }
}
