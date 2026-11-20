using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The way from the sewer under the house to the shaft under its exit hatch (SewerPlan.Passage).</summary>
    public class SewerPassageTests
    {
        static Vector2 HatchInFrame(LevelLayout l)
        {
            var d = l.Cellar.ToLocal(l.ExitHatchPosition);
            return new Vector2(d.x, d.z);
        }

        [Test]
        public void EveryHousesExitHatchHasAPassageToItsShaft()
        {
            foreach (var l in TestHouses.Sample)
            {
                var hatch = HatchInFrame(l);
                string at = $"seed {l.Seed} house {l.LevelIndex}";
                Assert.IsTrue(SewerPlan.PassageFits(hatch), $"{at}: the exit hatch at {hatch} is clear of the basement's tunnel");
                var p = SewerPlan.Make(5, l.LevelIndex, hatch);
                Assert.Greater(p.Passage.Count, 0, $"{at}: a passage");
                Assert.AreEqual(-1f, p.ExitLadderX, $"{at}: no ladder in the tunnel itself");
                Assert.AreEqual(1, p.Segments.Count(s => s.Door != 0), $"{at}: one doorway");
                int door = p.Segments.FindIndex(s => s.Door != 0);
                Assert.Greater(p.Segments[door].X, p.GateX + 1f, $"{at}: in the exit's stretch, not by the gate's lever");
                Assert.Less(p.Segments[door].X, p.HubX, $"{at}: before the hall");

                // the shaft's foot right under the hatch, the hall well past it
                float shift = Basement.GateX - p.GateX; // the sewer's frame: its gate where the basement's stands
                var end = p.Passage[p.Passage.Count - 1];
                Assert.AreEqual(PassageKind.End, end.Kind, at);
                var centre = end.Position + Quaternion.Euler(0f, end.Yaw, 0f) * Vector3.right * SewerPlan.PassageHalf;
                Assert.AreEqual(hatch.x - shift, centre.x, 1e-3f, $"{at}: under the hatch (along)");
                Assert.AreEqual(hatch.y, centre.z, 1e-3f, $"{at}: under the hatch (across)");
                Assert.GreaterOrEqual(p.HubX, hatch.x - shift + SewerPlan.HallPastHatch - 1e-3f, $"{at}: the hall begins past the hatch");

                foreach (var piece in p.Passage)
                {
                    if (piece.Kind == PassageKind.Straight) Assert.GreaterOrEqual(piece.Length, 0f, at);
                    // out of the tunnel (its doorway's lining ends at DoorOut), never as far as the hall
                    Assert.GreaterOrEqual(Mathf.Abs(piece.Position.z), SewerPlan.DoorOut - 1e-3f, $"{at}: a piece in the tunnel");
                    Assert.Less(piece.Position.x + 2f * SewerPlan.PassageHalf, p.HubX, $"{at}: a piece under the hall");
                }
            }
        }

        [Test]
        public void TheWayTurnsToReachAHatchNotStraightAcross()
        {
            // a hatch along the tunnel from every doorway: across, a turn, along, a turn back, across to the shaft
            var p = SewerPlan.Make(3, 1, new Vector2(30.4f, -11f));
            var kinds = p.Passage.Select(x => x.Kind).Where(k => k != PassageKind.Straight).ToArray(); // (a stretch of no length is left out)
            Assert.AreEqual(new[] { PassageKind.Corner, PassageKind.Corner, PassageKind.End }, kinds);
            Assert.IsTrue(p.Passage.All(x => x.Position.z < 0f), "on the hatch's side of the tunnel");
            // each turn opens back the way in and on the way out
            for (int i = 0; i < p.Passage.Count; i++)
            {
                if (p.Passage[i].Kind != PassageKind.Corner || i == 0) continue; // (right at the doorway: no stretch before it to check against)
                var r = Quaternion.Euler(0f, p.Passage[i].Yaw, 0f);
                Vector3 a = r * Vector3.left, b = r * Vector3.forward;
                var into = Quaternion.Euler(0f, p.Passage[i - 1].Yaw, 0f) * Vector3.right;
                var outof = Quaternion.Euler(0f, p.Passage[i + 1].Yaw, 0f) * Vector3.right;
                bool Match(Vector3 x, Vector3 y) => (x - y).sqrMagnitude < 1e-3f;
                Assert.IsTrue((Match(a, -into) && Match(b, outof)) || (Match(a, outof) && Match(b, -into)), $"turn {i} opens the right ways");
            }
        }

        [Test]
        public void NoPassageWhereItCannotGo()
        {
            Assert.IsFalse(SewerPlan.PassageFits(new Vector2(20f, 3f)), "over the basement's tunnel");
            Assert.IsTrue(SewerPlan.PassageFits(new Vector2(-4f, 12f)), "behind its invisible wall: alongside the tunnel");
            Assert.AreEqual(0, SewerPlan.Make(3, 1, new Vector2(20f, 3f)).Passage.Count);
            Assert.AreEqual(0, SewerPlan.Make(3, -1).Passage.Count, "the sewer at the run's start: no house above");
        }
    }
}
