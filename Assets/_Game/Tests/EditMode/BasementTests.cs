using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The team arrives in a basement under the lobby (Basement): its manhole is the entry hatch, the team starts around it,
    /// a ladder climbs the shaft to the lobby's floor, where a lid closes it; the basement counts as inside (no gas).
    /// </summary>
    public class BasementTests
    {
        [Test]
        public void EveryHouseHasABasementUnderItsLobby()
        {
            foreach (var l in TestHouses.Sample)
            {
                Assert.IsTrue(l.HasBasement, $"seed {l.Seed}");
                var o = l.BasementOrigin;
                Assert.AreEqual(-Basement.Depth, o.y, 1e-4f);
                // the shaft over the chamber is the lobby's middle
                Assert.AreEqual(l.HatchPosition.x, o.x + Basement.ShaftX, 1e-4f);
                Assert.AreEqual(l.HatchPosition.z, o.z, 1e-4f);
                Assert.IsTrue(Basement.Contains(o, l.EntryHatch), "the stash is kept in the basement");
                Assert.AreEqual(-1, Hatch.IndexNear(l.EntryHatch), "no entry hatch to sit in");
                Assert.IsTrue(l.IsInside(l.EntryHatch + Vector3.up), "the basement is inside (no gas)");
                Assert.IsFalse(l.IsInside(l.EntryHatch + Vector3.up * 10f + Vector3.right * 500f), "far outside is outside");
                foreach (var s in l.PlayerSpawns) Assert.IsTrue(Basement.Contains(o, s + Vector3.up * 0.5f), $"seed {l.Seed}: a spawn outside the basement");
                // the lid over the shaft stands in the lobby
                Assert.IsTrue(l.Props.Exists(p => p.Kind == PropKind.ShaftHatch && (p.Position - l.HatchPosition).sqrMagnitude < 0.01f));
                Assert.IsFalse(l.Props.Exists(p => p.Kind == PropKind.Hatch && (p.Position - l.HatchPosition).sqrMagnitude < 0.01f), "no hatch in the lobby any more");
            }
        }

        [Test]
        public void TheLadderClimbsFromTheBasementToTheLobbysFloorBesideTheShaft()
        {
            var l = TestHouses.Get(1, 0);
            var path = Basement.ClimbPath(l.BasementOrigin, l.HatchPosition);
            Assert.IsTrue(Basement.Contains(l.BasementOrigin, path[0] + Vector3.up * 0.5f), "starts in the basement");
            var end = path[path.Length - 1];
            Assert.AreEqual(l.HatchPosition.y + 0.05f, end.y, 1e-3f, "ends on the lobby's floor");
            var flat = new Vector2(end.x - l.HatchPosition.x, end.z - l.HatchPosition.z);
            Assert.Greater(flat.magnitude, Basement.ShaftHalf + 0.3f, "beside the shaft, not over it");
            // straight up the shaft: the middle of the climb is over the manhole
            Assert.Less(new Vector2(path[1].x - l.HatchPosition.x, path[1].z - l.HatchPosition.z).magnitude, Basement.ShaftHalf);
            Assert.AreEqual(path[1].x, path[2].x, 1e-4f);
            Assert.AreEqual(path[1].z, path[2].z, 1e-4f);
        }

        [Test]
        public void TheLadderIsClimbedByWalkingIntoItWithWAndS()
        {
            var face = Vector3.left;
            // walking into it, facing it: on; walking away from it, or standing: not
            Assert.IsTrue(LadderMath.GetsOn(true, 1f, face, face, true, 0f, 4.9f));
            Assert.IsFalse(LadderMath.GetsOn(true, 1f, -face, face, true, 0f, 4.9f), "back to it");
            Assert.IsFalse(LadderMath.GetsOn(true, 0f, face, face, true, 0f, 4.9f), "just standing there");
            Assert.IsFalse(LadderMath.GetsOn(false, 1f, face, face, true, 0f, 4.9f), "out of reach");
            // stepping into the open shaft from above: it catches the falling body
            Assert.IsTrue(LadderMath.GetsOn(true, 0f, -face, face, false, 3.5f, 4.9f));
            // W up, S down, held on the column
            var up = LadderMath.Step(new Vector3(0.2f, 1f, 0f), Vector3.zero, 1f, 0.1f);
            Assert.AreEqual(LadderMath.Speed * 0.1f, up.y, 1e-4f);
            Assert.Less(up.x, 0f, "drawn to the rungs");
            Assert.AreEqual(-LadderMath.Speed * 0.1f, LadderMath.Step(Vector3.zero, Vector3.zero, -1f, 0.1f).y, 1e-4f);
            Assert.AreEqual(0f, LadderMath.Step(Vector3.zero, Vector3.zero, 0f, 0.1f).y, "no W or S: it hangs there");
            // looking away from the ladder (back to the rungs) W goes down, S up; facing it or side on, W goes up
            Assert.AreEqual(1f, LadderMath.Direction(face, face));
            Assert.AreEqual(-1f, LadderMath.Direction(-face, face));
            Assert.AreEqual(1f, LadderMath.Direction(Vector3.forward, face), "side on");
            // off at the top only through an open lid; off at the bottom going down onto the floor
            Assert.IsTrue(LadderMath.OffAtTop(4.7f, 4.9f, true));
            Assert.IsFalse(LadderMath.OffAtTop(4.7f, 4.9f, false), "the lid is shut: the head stops against it");
            Assert.IsTrue(LadderMath.OffAtBottom(-1f, true, 0.1f, 0f));
            Assert.IsFalse(LadderMath.OffAtBottom(1f, true, 0.1f, 0f), "climbing up from the floor");
        }
    }
}
