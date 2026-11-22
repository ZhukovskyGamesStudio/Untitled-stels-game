using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The sewer's hall (<see cref="SewerHall"/>): another shape every time, yet always the same on every peer, always
    /// three tunnels whose mouths sit in its boundary, a car's way from the way in to each of them, and no tunnel running
    /// through the hall, the incoming tunnel or another tunnel.
    /// </summary>
    public class SewerHallTests
    {
        static IEnumerable<SewerPlan> Plans(int count = 30)
        {
            for (int seed = 0; seed < count; seed++) yield return SewerPlan.Make(seed * 7919 + 3, seed % 5 - 1);
        }

        static string Footprint(SewerHall h) => h.Height + ":" + string.Join(";", h.Ordered.Select(c => $"{c.x},{c.y}"));

        [Test]
        public void TheSameOnEveryPeer()
        {
            for (int seed = 0; seed < 12; seed++)
            {
                var a = SewerPlan.Make(seed, 2).Hall;
                var b = SewerPlan.Make(seed, 2).Hall;
                Assert.AreEqual(Footprint(a), Footprint(b), "the same footprint from the same seed");
                Assert.AreEqual(a.Mouths.Length, b.Mouths.Length);
                for (int i = 0; i < a.Mouths.Length; i++) Assert.AreEqual(a.Mouths[i].position, b.Mouths[i].position);
                CollectionAssert.AreEqual(a.Tunnels, b.Tunnels);
                Assert.AreEqual(a.Lamps.Count, b.Lamps.Count);
            }
            int other = Enumerable.Range(0, 10).Count(seed => Footprint(SewerPlan.Make(seed, 0).Hall) != Footprint(SewerPlan.Make(seed, 1).Hall));
            Assert.GreaterOrEqual(other, 7, "another hall after another house");
        }

        [Test]
        public void ShapesDifferFromSewerToSewer()
        {
            var plans = Plans().ToList();
            var shapes = plans.Select(p => Footprint(p.Hall)).Distinct().Count();
            Assert.GreaterOrEqual(shapes, 20, "hardly two halls alike in 30 sewers");
            Assert.GreaterOrEqual(plans.Select(p => p.Hall.Form).Distinct().Count(), 4, "boxes, L, T, crosses, ragged");
            Assert.AreEqual(2, plans.Select(p => p.Hall.Height).Distinct().Count(), "low and high vaults");
            int notBoxes = plans.Count(p =>
            {
                var cells = p.Hall.Cells;
                int w = cells.Max(c => c.x) - cells.Min(c => c.x) + 1, d = cells.Max(c => c.y) - cells.Min(c => c.y) + 1;
                return cells.Count < w * d;
            });
            Assert.GreaterOrEqual(notBoxes, 12, "most halls are not a plain box");
            Assert.GreaterOrEqual(plans.Select(p => p.Mouths.Length).Distinct().Count(), 2, "more or fewer mouths bricked up");
            Assert.GreaterOrEqual(plans.Select(p => p.Hall.Cells.Count).Distinct().Count(), 5, "small halls and big ones");
        }

        [Test]
        public void ThreeTunnelsLeaveByMouthsOnTheBoundary()
        {
            int sideways = 0, ahead = 0;
            foreach (var p in Plans(40))
            {
                var h = p.Hall;
                Assert.GreaterOrEqual(h.Mouths.Length, 3);
                Assert.LessOrEqual(h.Mouths.Length, 5);
                Assert.AreEqual(3, p.TunnelMouths.Distinct().Count(), "three tunnels, three mouths");
                Assert.IsTrue(p.TunnelMouths.All(m => m >= 0 && m < p.Mouths.Length));
                for (int i = 0; i < h.Mouths.Length; i++)
                {
                    var (cell, side) = h.MouthSide(i);
                    Assert.IsTrue(h.Cells.Contains(cell), "a mouth is in a side of the hall's cell");
                    Assert.IsFalse(h.Cells.Contains(cell + side), "... on the hall's boundary");
                    Assert.AreNotEqual(new Vector2Int(-1, 0), side, "never back along the incoming tunnel");
                    var behind = cell - side;
                    Assert.IsTrue(h.Cells.Contains(behind) || behind == new Vector2Int(-1, 0), "a whole cell to line the car up in");
                    var out3 = h.Mouths[i].rotation * Vector3.right;
                    Assert.AreEqual(new Vector3(side.x, 0f, side.y).x, out3.x, 1e-4f, "+X out of the hall");
                    Assert.AreEqual(side.y, out3.z, 1e-4f);
                    Assert.AreEqual(1, h.Edges.Count(e => e.Kind == HallSide.Mouth && e.Mouth == i), "one wall with this mouth");
                }
                Assert.AreEqual(1, h.Edges.Count(e => e.Kind == HallSide.Entry), "one way in");
                var entry = h.Edges.First(e => e.Kind == HallSide.Entry);
                Assert.Less(entry.Frame.position.magnitude, 1e-4f, "the way in where the sewer's tunnel ends");
                foreach (int m in p.TunnelMouths)
                {
                    var out3 = p.Mouths[m].rotation * Vector3.right;
                    if (Mathf.Abs(out3.z) > 0.5f) sideways++;
                    else ahead++;
                }
                // the tunnels are listed left to right as the car comes in
                var bearings = p.TunnelMouths.Select(m => Mathf.Atan2(p.Mouths[m].position.z, p.Mouths[m].position.x)).ToArray();
                Assert.IsTrue(bearings[0] >= bearings[1] && bearings[1] >= bearings[2], "left, middle, right");
            }
            Assert.Greater(sideways, 15, "often a way to the side");
            Assert.Greater(ahead, 15, "often one ahead");
        }

        [Test]
        public void TunnelsRunClearOfTheHallTheWayInAndEachOther()
        {
            // the hall is at most ~45 m across: two tunnels that cross do it within 60 m of their mouths
            var lateral = new[] { -0.98f, 0f, 0.98f };
            foreach (var p in Plans(40))
            {
                var h = p.Hall;
                var cells = h.Ordered.Select(SewerHall.CellRect).ToArray();
                for (int i = 0; i < h.Mouths.Length; i++)
                {
                    var f = h.Mouths[i];
                    var along = f.rotation * Vector3.right;
                    var across = f.rotation * Vector3.forward;
                    for (float t = 0.3f; t < 60f; t += 1f)
                        foreach (float s in lateral)
                        {
                            var q = f.position + along * t + across * (s * SewerHall.TunnelHalf);
                            var q2 = new Vector2(q.x, q.z);
                            foreach (var c in cells)
                                if (c.Contains(q2)) Assert.Fail($"the tunnel from mouth {i} runs through the hall at {q}");
                            if (q.x < -0.01f && Mathf.Abs(q.z) < SewerHall.IncomingHalf) Assert.Fail($"the tunnel from mouth {i} runs into the incoming tunnel at {q}");
                            for (int j = 0; j < h.Mouths.Length; j++)
                            {
                                if (j == i) continue;
                                var local = Quaternion.Inverse(h.Mouths[j].rotation) * (q - h.Mouths[j].position);
                                if (local.x > 0.01f && Mathf.Abs(local.z) < SewerHall.TunnelHalf) Assert.Fail($"the tunnels from mouths {i} and {j} cross at {q}");
                            }
                        }
                }
            }
        }

        /// <summary>Half the car's width with a margin: where its middle may be (it turns on its own length, the cells are 8 m).</summary>
        const float CarHalf = 1.25f;

        static bool InHall(SewerHall h, Vector2 q) => h.Cells.Contains(new Vector2Int(
            Mathf.FloorToInt((q.x - SewerHall.Wall) / SewerHall.Cell), Mathf.FloorToInt((q.y + SewerHall.Cell / 2f) / SewerHall.Cell)));

        static bool CarFits(SewerHall h, HashSet<Vector2Int> columns, Vector2 p)
        {
            if (!InHall(h, p)) return false;
            for (int a = 0; a < 16; a++)
                if (!InHall(h, p + new Vector2(Mathf.Cos(a * Mathf.PI / 8f), Mathf.Sin(a * Mathf.PI / 8f)) * CarHalf)) return false;
            var v = new Vector2Int(Mathf.RoundToInt((p.x - SewerHall.Wall) / SewerHall.Cell), Mathf.RoundToInt((p.y + SewerHall.Cell / 2f) / SewerHall.Cell));
            if (!columns.Contains(v)) return true;
            var c = SewerHall.Corner(v);
            return Vector2.Distance(p, new Vector2(c.x, c.z)) >= CarHalf + SewerHall.ColumnHalf;
        }

        [Test]
        public void TheCarGetsFromTheWayInToEveryMouth()
        {
            const float step = 0.5f;
            foreach (var p in Plans())
            {
                var h = p.Hall;
                var columns = new HashSet<Vector2Int>(h.Columns);
                Vector2Int Key(Vector2 q) => new(Mathf.RoundToInt(q.x / step), Mathf.RoundToInt(q.y / step));
                Vector2 At(Vector2Int k) => new(k.x * step, k.y * step);
                var start = Key(new Vector2(SewerHall.Wall + 1.5f, 0f));
                Assert.IsTrue(CarFits(h, columns, At(start)), "the car drives in");
                var reached = new HashSet<Vector2Int> { start };
                var open = new Queue<Vector2Int>();
                open.Enqueue(start);
                var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
                while (open.Count > 0)
                {
                    var k = open.Dequeue();
                    foreach (var d in dirs)
                    {
                        var n = k + d;
                        if (n.x * step > h.MaxX || n.y * step < h.MinZ || n.y * step > h.MaxZ || n.x < 0) continue;
                        if (!reached.Contains(n) && CarFits(h, columns, At(n)))
                        {
                            reached.Add(n);
                            open.Enqueue(n);
                        }
                    }
                }
                foreach (int m in p.TunnelMouths)
                {
                    var f = p.Mouths[m];
                    var back = f.rotation * Vector3.left;
                    // lined up in front of the mouth over the car's length and more, and reached from the way in
                    for (float t = SewerHall.Wall + 1.5f; t <= SewerHall.Wall + 7f; t += 0.5f)
                    {
                        var q = f.position + back * t;
                        if (!CarFits(h, columns, new Vector2(q.x, q.z))) Assert.Fail($"the car lines up for mouth {m} ({h.Form}) at {q}");
                    }
                    var front = f.position + back * (SewerHall.Wall + 2.5f);
                    if (!reached.Contains(Key(new Vector2(front.x, front.z)))) Assert.Fail($"the car gets to mouth {m} of a {h.Form} hall ({Footprint(h)})");
                }
            }
        }

        [Test]
        public void ColumnsArchesAndLampsStandWhereTheyShould()
        {
            foreach (var p in Plans())
            {
                var h = p.Hall;
                var columns = new HashSet<Vector2Int>(h.Columns);
                foreach (var c in h.Ordered)
                    for (int dx = 0; dx <= 1; dx++)
                        for (int dz = 0; dz <= 1; dz++)
                            Assert.IsTrue(columns.Contains(c + new Vector2Int(dx, dz)), "a column under every corner of the vaults");
                int inner = h.Ordered.Count(c => h.Cells.Contains(c + new Vector2Int(1, 0))) + h.Ordered.Count(c => h.Cells.Contains(c + new Vector2Int(0, 1)));
                Assert.AreEqual(inner, h.Arches.Count, "an arch between every two neighbouring cells");
                int sides = h.Ordered.Sum(c => new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) }.Count(s => !h.Cells.Contains(c + s)));
                Assert.AreEqual(sides, h.Edges.Count, "a wall on every side of the boundary");
                Assert.GreaterOrEqual(h.Lamps.Count, 2, "a few lamps");
                foreach (var l in h.Lamps)
                {
                    var into = l.position + l.rotation * Vector3.right * 1.5f;
                    if (!InHall(h, new Vector2(into.x, into.z))) Assert.Fail($"the lamp at {l.position} lights the hall");
                    Assert.Less(l.position.y, SewerHall.Springing[h.Height], "under the vaults");
                }
                Assert.Greater(h.MaxX, SewerHall.Cell, "the hall reaches on past its first cell");
                Assert.AreEqual(p.HubX + h.MaxX, p.End, 1e-4f);
            }
        }
    }
}
