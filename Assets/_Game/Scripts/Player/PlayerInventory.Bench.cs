using Stealth.Game;
using Stealth.Items;

namespace Stealth.Player
{
    /// <summary>
    /// The backpack and the car's workbench (<see cref="GameManager.Bench"/>): things onto it, off it, about on it, «combine»;
    /// and the trunk's shelves (<see cref="GameManager.Shelves"/>) the same way, each by its number.
    /// </summary>
    public partial class PlayerInventory
    {
        public void RequestBench(int index, byte x, byte y, byte rot) => Link.RequestBench(index, x, y, rot);
        public void RequestUnbench(InventoryEntry from, byte x, byte y, byte rot) => Link.RequestUnbench(from, x, y, rot);
        public void RequestBenchMove(InventoryEntry from, byte x, byte y, byte rot) => Link.RequestBenchMove(from, x, y, rot);
        public void RequestCombine() => Link.RequestCombine();

        bool AtBench(out TeamStash bench)
        {
            var gm = GameManager.Instance;
            bench = CanAct && gm != null && gm.AtBench(transform.position) ? gm.Bench : null;
            return bench != null;
        }

        /// <summary>Server: a thing of the backpack onto the bench.</summary>
        public void BenchServer(int index, byte x, byte y, byte rot)
        {
            if (index < 0 || index >= m_Items.Count || !AtBench(out var bench) || Locked(index)) return;
            var e = m_Items[index];
            if (!bench.Add(e.Shape, e.Color, x, y, rot)) return;
            m_Items.RemoveAt(index);
            Changed();
        }

        /// <summary>Server: a thing of the bench into the backpack.</summary>
        public void UnbenchServer(InventoryEntry from, byte x, byte y, byte rot)
        {
            if (!AtBench(out var bench) || bench.IndexOf(from) < 0 || !CanTake(from.Shape, rot, x, y)) return;
            bench.Take(from);
            // (into a busy hand: the held thing makes way, like from the stash; with no room for it, it goes onto the bench)
            PlaceTaken(from.Shape, from.Color, x, y, rot, out var evicted);
            if (evicted is not { } held) return;
            if (bench.AddAll(new[] { held }, out _) == 0)
                GameManager.Instance?.SpawnItemServer(held.Shape, held.Color, transform.position + transform.forward * 0.6f + UnityEngine.Vector3.up * 0.3f, 0f);
        }

        public void BenchMoveServer(InventoryEntry from, byte x, byte y, byte rot)
        {
            if (AtBench(out var bench)) bench.Move(from, x, y, rot);
        }

        public void CombineServer()
        {
            if (CanAct) GameManager.Instance?.CombineServer(m_Player);
        }

        // ------------------------------------------------------------------ the trunk's shelves

        public void RequestShelf(int shelf, int index, byte x, byte y, byte rot) => Link.RequestShelf((byte)shelf, index, x, y, rot);
        public void RequestUnshelf(int shelf, InventoryEntry from, byte x, byte y, byte rot) => Link.RequestUnshelf((byte)shelf, from, x, y, rot);
        public void RequestShelfMove(int shelf, InventoryEntry from, byte x, byte y, byte rot) => Link.RequestShelfMove((byte)shelf, from, x, y, rot);

        /// <summary>The shelf of this number is fitted and in reach (and the player can act).</summary>
        bool AtShelf(int number, out TeamStash shelf)
        {
            var gm = GameManager.Instance;
            shelf = CanAct && gm != null && gm.AtShelf(transform.position, number) ? gm.Shelf(number) : null;
            return shelf != null;
        }

        /// <summary>Server: a thing of the backpack onto a shelf.</summary>
        public void ShelfServer(int number, int index, byte x, byte y, byte rot)
        {
            if (index < 0 || index >= m_Items.Count || !AtShelf(number, out var shelf) || Locked(index)) return;
            var e = m_Items[index];
            if (!shelf.Add(e.Shape, e.Color, x, y, rot)) return;
            m_Items.RemoveAt(index);
            Changed();
        }

        /// <summary>Server: a thing of a shelf into the backpack.</summary>
        public void UnshelfServer(int number, InventoryEntry from, byte x, byte y, byte rot)
        {
            if (!AtShelf(number, out var shelf) || shelf.IndexOf(from) < 0 || !CanTake(from.Shape, rot, x, y)) return;
            shelf.Take(from);
            // (into a busy hand: the held thing makes way; with no room for it, it goes onto the shelf, else the floor)
            PlaceTaken(from.Shape, from.Color, x, y, rot, out var evicted);
            if (evicted is not { } held) return;
            if (shelf.AddAll(new[] { held }, out _) == 0)
                GameManager.Instance?.SpawnItemServer(held.Shape, held.Color, transform.position + transform.forward * 0.6f + UnityEngine.Vector3.up * 0.3f, 0f);
        }

        public void ShelfMoveServer(int number, InventoryEntry from, byte x, byte y, byte rot)
        {
            if (AtShelf(number, out var shelf)) shelf.Move(from, x, y, rot);
        }
    }
}
