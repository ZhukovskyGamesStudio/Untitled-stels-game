using System.Collections.Generic;
using Stealth.Items;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// Linus's backpack as a box the stash grid is wrapped round (pure, unit tested): the grid's columns go up the left
    /// side, across the top and down the right side; its rows go from the head end along the top (and the sides) to the tail
    /// end and on down the back face. The front (his head) and the bottom (his back) stay bare. A thing keeps its own size:
    /// it lies on the face its middle falls on, flat against it and turned as on the grid — a long thing upright in the grid
    /// lies along the top under the straps, one lying across the grid that reaches a side hangs upright on it. The grid is spread over the
    /// faces in proportion, so a small pack is crammed: things that overlap on a face pile up outwards, in the stash's order.
    /// </summary>
    public static class PackWrap
    {
        /// <summary>
        /// A level's pack in the load's space: the middle of its top face at the origin, +X to the right seen from behind,
        /// +Y up, +Z towards the head; <see cref="Size"/> = width (x), length (z), height (y) of the box things lie on.
        /// </summary>
        public struct Box
        {
            public Vector3 Size;
            public Box(float width, float length, float height) => Size = new Vector3(width, height, length);
            public float W => Size.x;
            public float H => Size.y;
            public float L => Size.z;
            /// <summary>The columns' way round: up the left side, across the top, down the right.</summary>
            public float Around => 2f * H + W;
            /// <summary>The rows' way: along the top from the head end, then down the back.</summary>
            public float Along => L + H;
        }

        public enum Face : byte { Left, Top, Right, Back }

        /// <summary>How far round the back corner a side column's things go, as a share of half the pack's width.</summary>
        public const float CornerReach = 0.55f;
        /// <summary>Things overlap on a face only by more than this share of their footprint before one is laid on the other.</summary>
        public const float Snug = 0.7f;
        /// <summary>The share of a thing's thickness pressed into the pack (or into what it lies on): strapped down, not floating on it.</summary>
        public const float Sink = 1f / 3f;

        /// <summary>A point of the wrapped grid on the box and the face's frame there.</summary>
        public struct Spot
        {
            public Face Face;
            public Vector3 Point, Normal;
            /// <summary>Where the grid's columns run (+x of the grid) and its rows (+y of the grid, down the stash board).</summary>
            public Vector3 Across, Down;
        }

        /// <summary>
        /// The point of the box at <paramref name="s"/> metres round (0: the left side's bottom edge, <see cref="Box.Around"/>:
        /// the right side's) and <paramref name="t"/> metres along (0: the head end, past <see cref="Box.L"/>: down the back).
        /// </summary>
        public static Spot At(float s, float t, Box b)
        {
            float hw = b.W * 0.5f, hl = b.L * 0.5f;
            s = Mathf.Clamp(s, 0f, b.Around);
            t = Mathf.Clamp(t, 0f, b.Along);
            bool back = t > b.L;
            float past = t - b.L;
            if (s < b.H)
            {
                // the left side, going up
                float y = -b.H + s;
                // (round the back corner it stays near it: sliding on to the middle, every side column met there in a heap)
                return back
                    ? new Spot { Face = Face.Back, Point = new Vector3(-hw + Mathf.Min(past, hw * CornerReach), y, -hl), Normal = Vector3.back, Across = Vector3.up, Down = Vector3.right }
                    : new Spot { Face = Face.Left, Point = new Vector3(-hw, y, hl - t), Normal = Vector3.left, Across = Vector3.up, Down = Vector3.back };
            }
            if (s <= b.H + b.W)
            {
                float x = -hw + (s - b.H);
                return back
                    ? new Spot { Face = Face.Back, Point = new Vector3(x, -Mathf.Min(past, b.H), -hl), Normal = Vector3.back, Across = Vector3.right, Down = Vector3.down }
                    : new Spot { Face = Face.Top, Point = new Vector3(x, 0f, hl - t), Normal = Vector3.up, Across = Vector3.right, Down = Vector3.back };
            }
            {
                // the right side, going down
                float y = -(s - b.H - b.W);
                return back
                    ? new Spot { Face = Face.Back, Point = new Vector3(hw - Mathf.Min(past, hw * CornerReach), y, -hl), Normal = Vector3.back, Across = Vector3.down, Down = Vector3.left }
                    : new Spot { Face = Face.Right, Point = new Vector3(hw, y, hl - t), Normal = Vector3.right, Across = Vector3.down, Down = Vector3.back };
            }
        }

        /// <summary>
        /// Where the middle of a grid point (<paramref name="u"/> columns across, <paramref name="v"/> rows down, of a grid
        /// <paramref name="columns"/> × <paramref name="rows"/>) falls on the box: the grid spread over its faces in proportion.
        /// </summary>
        public static Spot OnBox(float u, float v, int columns, int rows, Box b) =>
            At(u / Mathf.Max(1, columns) * b.Around, v / Mathf.Max(1, rows) * b.Along, b);

        /// <summary>
        /// The turn that lays a thing modelled flat (StashCart: grid columns along +X, rows along −Z, up +Y) against a face:
        /// its +X along the face's <see cref="Spot.Across"/>, its rows along <see cref="Spot.Down"/>, its up out of the face.
        /// </summary>
        public static Quaternion Facing(in Spot spot) => Quaternion.LookRotation(-spot.Down, spot.Normal);

        /// <summary>A thing placed on the pack (in the load's space, its own size).</summary>
        public struct Placed
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public Face Face;
        }

        /// <summary>
        /// Every thing of the stash on the pack, in the stash's order: on the face its middle falls on, turned as on the grid
        /// (StashCart.Place's turn and mirror), lifted out of the face by <paramref name="lift"/> and by whatever it overlaps
        /// that lies there already (<paramref name="thickness"/>: how thick a shape lies, metres).
        /// </summary>
        public static List<Placed> Lay(IReadOnlyList<InventoryEntry> items, int columns, int rows, Box b, System.Func<int, float> thickness, float lift = 0.004f)
        {
            var placed = new List<Placed>(items.Count);
            var piles = new List<(Face face, Rect area, float top)>();
            foreach (var e in items)
            {
                var size = ItemCatalog.Size(ItemCatalog.RotatedCells(e.Shape, e.Rot));
                var spot = Where(e, size, columns, rows, b);
                // its footprint on the face in the face's own fixed 2D axes (the back face's corners agree whichever way in)
                var (ax, ay) = FaceAxes(spot.Face);
                var spanAcross = spot.Across * (size.x * StashCart.Cell);
                var spanDown = spot.Down * (size.y * StashCart.Cell);
                var c = new Vector2(Vector3.Dot(spot.Point, ax), Vector3.Dot(spot.Point, ay));
                var half = new Vector2(Mathf.Abs(Vector3.Dot(spanAcross, ax)) + Mathf.Abs(Vector3.Dot(spanDown, ax)),
                                       Mathf.Abs(Vector3.Dot(spanAcross, ay)) + Mathf.Abs(Vector3.Dot(spanDown, ay))) * 0.5f * Snug;
                var area = new Rect(c - half, half * 2f);
                float under = 0f;
                foreach (var (face, other, top) in piles)
                    if (face == spot.Face && other.Overlaps(area)) under = Mathf.Max(under, top);
                float thick = Mathf.Max(0.01f, thickness != null ? thickness(e.Shape) : 0.08f);
                float floor = under - thick * Sink;               // (a third of it pressed into what is under it)
                piles.Add((spot.Face, area, floor + thick));
                var (_, turn, scale) = StashCart.Place(e);
                placed.Add(new Placed
                {
                    Position = spot.Point + spot.Normal * (lift + floor),
                    Rotation = Facing(spot) * turn,
                    Scale = scale,
                    Face = spot.Face,
                });
            }
            return placed;
        }

        /// <summary>
        /// The spot a thing's middle goes to: the one its grid middle falls on — but a thing lying across the grid (wider
        /// than tall) that reaches onto a side hangs on that side (the one it covers more of), upright, by the side's
        /// middle: a long thing across the top would stick out over both sides.
        /// </summary>
        static Spot Where(InventoryEntry e, Vector2Int size, int columns, int rows, Box b)
        {
            float k = b.Around / Mathf.Max(1, columns);
            float s0 = e.X * k, s1 = (e.X + size.x) * k;
            float t = (e.Y + size.y * 0.5f) / Mathf.Max(1, rows) * b.Along;
            if (size.x > size.y)
            {
                float left = Mathf.Max(0f, Mathf.Min(s1, b.H) - s0);
                float right = Mathf.Max(0f, s1 - Mathf.Max(s0, b.H + b.W));
                if (left > 0f || right > 0f) return At(left >= right ? b.H * 0.5f : b.Around - b.H * 0.5f, t, b);
            }
            return At((s0 + s1) * 0.5f, t, b);
        }

        static (Vector3, Vector3) FaceAxes(Face f) => f switch
        {
            Face.Left or Face.Right => (Vector3.forward, Vector3.up),
            Face.Top => (Vector3.right, Vector3.forward),
            _ => (Vector3.right, Vector3.up),
        };
    }
}
