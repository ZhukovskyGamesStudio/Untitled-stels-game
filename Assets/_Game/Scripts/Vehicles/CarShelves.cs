using Stealth.Game;
using Stealth.Items;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>
    /// The trunk upgrade's shelves in the car's bed (pure, unit tested): one per level, each a grid of its own
    /// (<see cref="Columns"/> × <see cref="Rows"/>: the board is 1.36 × 0.27 m, a cell 22 cm — the things lie there in their
    /// own size, along the board), the team's for the whole run (<see cref="GameManager.Shelves"/>). A shelf takes things
    /// once its level is bought.
    /// </summary>
    public static class CarShelves
    {
        public const int Count = CarUpgrades.MaxLevelled;
        /// <summary>A shelf's grid: six cells along the board, one across it (geometry of the board, not tuning).</summary>
        public const int Columns = 6, Rows = 1;

        /// <summary>The shelf of this number is in the car (its trunk level is bought).</summary>
        public static bool Fitted(int shelf, int trunkLevel) => shelf >= 0 && shelf < Count && shelf < trunkLevel;

        /// <summary>
        /// Where a thing of a shelf's grid lies on its board (in the board's space: the corner of the grid on the board's
        /// top, +X along the board's length, rows along −Z, +Y up): over the middle of the cells it covers, turned and
        /// mirrored as in the grid (<see cref="StashCart.Place"/>'s turn: the same flat models as the cart and the pack).
        /// </summary>
        public static (Vector3 position, Quaternion rotation, Vector3 scale) Place(InventoryEntry e)
        {
            var size = ItemCatalog.Size(ItemCatalog.RotatedCells(e.Shape, e.Rot));
            var centre = new Vector3((e.X + size.x / 2f) * StashCart.Cell, 0f, -(e.Y + size.y / 2f) * StashCart.Cell);
            var (_, turn, scale) = StashCart.Place(e);
            return (centre, turn, scale);
        }

        /// <summary>The board's length and depth the grid takes, metres.</summary>
        public static Vector2 GridSize => new(Columns * StashCart.Cell, Rows * StashCart.Cell);
    }
}
