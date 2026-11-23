using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// Details of a built house: crawl ways start shut by their grilles, exit signs only on the way out, the flats' doors
    /// numbered, the apartment block's balconies one above the other and safe from the gas.
    /// </summary>
    public class HouseDetailTests
    {
        static GameObject Build(LevelLayout l)
        {
            if (ActiveLevel.Layout != l || ActiveLevel.Root == null)
                ActiveLevel.Load(l, AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset"));
            return ActiveLevel.Root;
        }

        static LevelLayout Block()
        {
            foreach (var l in TestHouses.First(HouseKind.Apartments, 8, seed => seed % 5)) // (the apartment tests' blocks first)
                if (l.Plan.Balconies.Count > 0) return l;
            Assert.Fail("no apartment block with balconies");
            return null;
        }

        [Test]
        public void EveryCrawlWayStartsShutByItsGrille()
        {
            var l = TestHouses.Get(99991, 4);
            var root = Build(l);
            var grilles = root.GetComponentsInChildren<VentGrille>(true);
            Assert.AreEqual(l.Doorways.Count(d => d.Kind == EdgeType.Vent), grilles.Length, "a grille in every vent");
            foreach (var g in grilles)
            {
                Assert.GreaterOrEqual(g.Index, 0, "it knows its doorway");
                Assert.AreEqual(EdgeType.Vent, l.Doorways[g.Index].Kind);
                Assert.IsTrue(g.Mounted.activeSelf && !g.Removed.activeSelf, "shut at the start");
                Assert.IsNotNull(g.Mounted.GetComponent<BoxCollider>(), "the shut grille stops a player");
            }
        }

        [Test]
        public void ExitSignsHangOnlyOnTheWayOut()
        {
            var l = TestHouses.Get(99991, 4);
            var root = Build(l);
            var signs = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "ExitSign" && t.parent == root.transform).ToList();
            int doors = l.Doorways.Count(d => d.Kind == EdgeType.Door && !d.Exterior);
            Assert.Less(signs.Count, doors / 2, "far fewer signs than doors");
            foreach (var s in signs)
            {
                var d = l.Doorways.OrderBy(x => new Vector2(x.Position.x - s.position.x, x.Position.z - s.position.z).sqrMagnitude).First();
                var types = new[] { d.RoomA, d.RoomB }.Where(i => i >= 0).Select(i => l.Plan.Rooms[i].Type).ToList();
                Assert.IsTrue(types.Contains(RoomType.Hall) || (types.Contains(RoomType.Lobby) && types.Contains(RoomType.Corridor)),
                    $"a sign over a doorway between {string.Join(" and ", types)}");
            }
        }

        [Test]
        public void TheFlatsDoorsCarryTheirNumbers()
        {
            var l = Block();
            var root = Build(l);
            var numbers = root.GetComponentsInChildren<AutoDoor>(true).Where(d => d.Number != null).Select(d => d.Number.text).ToList();
            int flats = l.Rooms.Where(r => r.Flat >= 0).Select(r => r.Flat).Distinct().Count();
            Assert.AreEqual(flats, numbers.Count, "every flat's entry door has its number plate");
            Assert.IsTrue(numbers.All(n => int.TryParse(n, out _)), string.Join(",", numbers));
            Assert.AreEqual(numbers.Count, numbers.Distinct().Count(), "no two flats share a number");
            Assert.Contains("1", numbers, "counted from 1");
        }

        [Test]
        public void BalconiesStandOneAboveTheOtherAndAreTheHouses()
        {
            var l = Block();
            foreach (var b in l.Plan.Balconies)
            {
                Assert.GreaterOrEqual(b.Floor, 1, "none on the ground floor");
                var at = new Vector3(b.Area.center.x, LevelLayout.FloorY(b.Floor) + 0.1f, b.Area.center.y);
                Assert.IsTrue(l.IsInside(at), "no gas on a balcony");
                Assert.IsFalse(l.IsInside(at + new Vector3(b.Out.x, 0f, b.Out.y) * 2f), "the street beyond it is outside");
                if (b.Floor + 1 < l.Floors)
                    Assert.IsTrue(l.Plan.Balconies.Any(o => o.Floor == b.Floor + 1 && (o.Center - b.Center).magnitude < 0.05f), "the one above stands right over it");
            }
            var root = Build(l);
            Assert.AreEqual(l.Plan.Balconies.Count, root.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Balcony" && t.parent == root.transform));
        }

        [Test]
        public void TheCheatStartsAHouseOfAnyKindAtAnyDepth()
        {
            foreach (HouseKind kind in System.Enum.GetValues(typeof(HouseKind)))
                for (int house = 0; house < 6; house++)
                {
                    int a = HouseKinds.SeedOf(kind, house, 11), b = HouseKinds.SeedOf(kind, house, 12);
                    Assert.AreNotEqual(0, a, $"{kind} as house {house}");
                    Assert.AreEqual(kind, HouseKinds.Of(a, house));
                    Assert.AreEqual(kind, HouseKinds.Of(b, house));
                    Assert.AreNotEqual(a, b, "another press, another house");
                }
            // the panel has a button for every kind
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/HUD.prefab");
            var panel = hud != null ? hud.GetComponentInChildren<Stealth.UI.CheatPanel>(true) : null;
            Assert.IsNotNull(panel, "the cheat panel is in the HUD");
            Assert.AreEqual(System.Enum.GetValues(typeof(HouseKind)).Length, panel.Houses.Length);
            Assert.IsTrue(panel.Houses.All(x => x != null));
        }
    }
}
