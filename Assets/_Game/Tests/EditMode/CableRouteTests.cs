using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The cables from the vault's lock panels and the storage door's terminal to the doors they open.</summary>
    public class CableRouteTests
    {
        [Test]
        public void EveryLockOfTheVaultIsWiredToItsDoor()
        {
            int vaults = 0;
            foreach (var l in TestHouses.Sample.Where(h => h.VaultDoorway >= 0))
            {
                vaults++;
                var door = l.Doorways[l.VaultDoorway];
                var mid = new Vector2(door.Position.x, door.Position.z);
                var across = door.Rotation * Vector3.forward;
                var a2 = new Vector2(across.x, across.z).normalized;
                foreach (var c in l.VaultConsoles)
                {
                    if (LevelLayout.FloorOf(c.position.y + 0.1f) != door.Floor) continue;
                    var f = c.rotation * Vector3.forward;
                    var from = new Vector2(c.position.x, c.position.z) + new Vector2(f.x, f.z).normalized * 0.3f;
                    // to whichever side of the doorway is not the vault
                    var outside = l.Plan.IdAt(door.Floor, HousePlan.CellOf(mid + a2 * 0.3f).x, HousePlan.CellOf(mid + a2 * 0.3f).y) == l.VaultRoom ? mid - a2 * 0.3f : mid + a2 * 0.3f;
                    Assert.IsNotNull(CableRoutes.Route(l.Plan, door.Floor, from, outside), $"seed {l.Seed} house {l.LevelIndex}: a lock with no way along the ceiling to the door");
                }
            }
            Assert.Greater(vaults, 0, "the sample has vaults");
        }

        [Test]
        public void TheCablesAreStraightPiecesAlongTheAxesUnderTheCeiling()
        {
            int laid = 0;
            foreach (var l in TestHouses.Sample.Where(h => h.VaultDoorway >= 0 || h.StorageTerminal >= 0))
            {
                var parts = new List<UtilityPart>();
                CableRoutes.Lay(parts, l);
                laid += parts.Count;
                foreach (var p in parts)
                {
                    Assert.IsTrue(p.Round);
                    Assert.AreEqual(UtilitySlot.Dark, p.Slot);
                    int thin = 0;
                    for (int k = 0; k < 3; k++) if (Mathf.Abs(p.Size[k] - 2f * CableRoutes.Radius) < 1e-4f) thin++;
                    Assert.GreaterOrEqual(thin, 2, "a straight piece along one axis");
                    float top = LevelLayout.FloorY(LevelLayout.FloorOf(p.Center.y)) + LevelMetrics.WallHeight;
                    Assert.LessOrEqual(p.Center.y + p.Size.y / 2f, top + 0.001f, "under the ceiling");
                }
            }
            Assert.Greater(laid, 0, "some cables are laid");
        }

        [Test]
        public void ADiagonalStepGetsAnElbow()
        {
            var sq = CableRoutes.Square(new List<Vector3> { new(0, 3, 0), new(2, 3, 1), new(2, 1, 1) });
            for (int i = 1; i < sq.Count; i++)
            {
                var d = sq[i] - sq[i - 1];
                int axes = (Mathf.Abs(d.x) > 1e-4f ? 1 : 0) + (Mathf.Abs(d.y) > 1e-4f ? 1 : 0) + (Mathf.Abs(d.z) > 1e-4f ? 1 : 0);
                Assert.AreEqual(1, axes, $"piece {i}: along one axis");
            }
        }
    }
}
