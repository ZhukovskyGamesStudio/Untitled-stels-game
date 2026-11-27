using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;

namespace Stealth.Tests
{
    /// <summary>
    /// Every kind of house is laid out its own way — its own scheme, its own masses, and in the finished plan the thing
    /// that makes the scheme what it is — never one layout with other numbers (Docs/LevelRefinementPlan.md, section 11).
    /// A new kind of house fails here until it has a layout of its own.
    /// </summary>
    public class HouseKindLayoutTests
    {
        const int Each = 6;

        /// <summary>The kinds and how each is laid out (PlanGenerator.Output.Scheme).</summary>
        static readonly (HouseKind kind, string scheme)[] Schemes =
        {
            (HouseKind.Hospital, "streets"), (HouseKind.Abandoned, "hub"), (HouseKind.Office, "racetrack"), (HouseKind.Business, "court"),
            (HouseKind.DataCenter, "grid"), (HouseKind.Warehouse, "sheds"), (HouseKind.Headquarters, "axis"),
            (HouseKind.Garage, "garage"), (HouseKind.Apartments, "apartments"),
        };

        static readonly Dictionary<HouseKind, List<LevelLayout>> s_Houses = new();

        /// <summary>Six houses of a kind (houses 1–3 of a run, the default settings), made once.</summary>
        static List<LevelLayout> Of(HouseKind kind)
        {
            if (s_Houses.Count == 0)
            {
                var keys = new List<(HouseKind kind, TestHouses.Spec spec)>();
                foreach (var (k, _) in Schemes)
                    for (int i = 0; i < Each; i++)
                    {
                        int level = 1 + i % 3;
                        keys.Add((k, new TestHouses.Spec(HouseKinds.SeedOf(k, level, 300 + 41 * i), level)));
                    }
                var all = TestHouses.Many(keys.Select(k => k.spec));
                for (int i = 0; i < keys.Count; i++)
                {
                    if (!s_Houses.TryGetValue(keys[i].kind, out var list)) s_Houses[keys[i].kind] = list = new List<LevelLayout>();
                    list.Add(all[i]);
                }
            }
            return s_Houses[kind];
        }

        static float Mean(HouseKind kind, System.Func<LevelLayout, float> of) => Of(kind).Average(of);

        /// <summary>The numbers of every kind, for the messages.</summary>
        static string Table() => string.Join("; ", Schemes.Select(s =>
            $"{s.kind}: corridor {Mean(s.kind, LayoutSignature.CorridorShare):0.00}, islands {Mean(s.kind, LayoutSignature.Islands):0.0}, " +
            $"biggest {Mean(s.kind, LayoutSignature.BiggestRoom):0}, straight {Mean(s.kind, LayoutSignature.LongestStraight):0}, " +
            $"widest {Mean(s.kind, LayoutSignature.WidestCorridor):0.0}, mirrored {Mean(s.kind, LayoutSignature.Symmetry):0.00}, courts {Of(s.kind).Count(l => LayoutSignature.Court(l) >= 90f)}"));

        [Test]
        public void EveryKindOfHouseHasASchemeOfItsOwn()
        {
            foreach (HouseKind kind in System.Enum.GetValues(typeof(HouseKind)))
                if (kind != HouseKind.TestRooms) Assert.IsTrue(Schemes.Any(s => s.kind == kind), $"{kind}: a kind of house without a layout of its own");
            Assert.AreEqual(Schemes.Length, Schemes.Select(s => s.scheme).Distinct().Count(), "no two kinds one scheme");
            foreach (var (kind, scheme) in Schemes)
                foreach (var l in Of(kind)) Assert.AreEqual(scheme, l.Scheme, $"{kind} {l.Seed}/{l.LevelIndex}");
        }

        [Test]
        public void NoTwoKindsShareAMass()
        {
            for (int i = 0; i < Schemes.Length; i++)
                for (int j = i + 1; j < Schemes.Length; j++)
                {
                    var both = Of(Schemes[i].kind).Select(l => l.Mass).Intersect(Of(Schemes[j].kind).Select(l => l.Mass)).ToList();
                    Assert.IsEmpty(both, $"{Schemes[i].kind} and {Schemes[j].kind} are built on the same mass: {string.Join(", ", both)}");
                }
        }

        [Test]
        public void TheOfficeHasARingRoundACore()
        {
            // the ring corridor goes all the way round the core: an island on most floors
            Assert.GreaterOrEqual(Mean(HouseKind.Office, LayoutSignature.Islands), 0.8f, Table());
        }

        [Test]
        public void TheBusinessCentreStandsRoundACourt()
        {
            Assert.GreaterOrEqual(Of(HouseKind.Business).Count(l => LayoutSignature.Court(l) >= 90f), Each - 2, Table());
        }

        [Test]
        public void TheDataCentreIsAGridOfHalls()
        {
            // halls as islands, an aisle on every side: more islands a floor than any other kind has
            float islands = Mean(HouseKind.DataCenter, LayoutSignature.Islands);
            Assert.GreaterOrEqual(islands, 3f, Table());
            foreach (var (kind, _) in Schemes)
                if (kind != HouseKind.DataCenter) Assert.Greater(islands, Mean(kind, LayoutSignature.Islands), $"{kind}: {Table()}");
        }

        [Test]
        public void TheWarehouseIsHallsBehindOneCorridor()
        {
            // the least corridor of the houses of blocks, and halls
            float corridor = Mean(HouseKind.Warehouse, LayoutSignature.CorridorShare);
            foreach (var kind in new[] { HouseKind.Hospital, HouseKind.Abandoned, HouseKind.Office, HouseKind.Business, HouseKind.DataCenter, HouseKind.Headquarters })
                Assert.Less(corridor, Mean(kind, LayoutSignature.CorridorShare), $"{kind}: {Table()}");
            Assert.GreaterOrEqual(Mean(HouseKind.Warehouse, LayoutSignature.BiggestRoom), 150f, Table());
        }

        [Test]
        public void TheHeadquartersHasAGalleryOnItsAxis()
        {
            // the building mirrored about its entrance, a gallery (3 m, with halls on it) straight on from the lobby
            Assert.GreaterOrEqual(Of(HouseKind.Headquarters).Count(l => LayoutSignature.Symmetry(l) >= 0.9f), Each - 1,
                string.Join(", ", Of(HouseKind.Headquarters).Select(l => $"{l.Mass} {LayoutSignature.Symmetry(l):0.00}")));
            Assert.GreaterOrEqual(Mean(HouseKind.Headquarters, LayoutSignature.LongestStraight), 16f, Table());
        }

        [Test]
        public void TheStationHasAHubAndSpokes()
        {
            // a hall in the middle, and hardly a loop of corridor
            Assert.GreaterOrEqual(Of(HouseKind.Abandoned).Count(l => LayoutSignature.WidestCorridor(l) >= 5f), Each - 2, Table());
            Assert.Less(Mean(HouseKind.Abandoned, LayoutSignature.Islands), Mean(HouseKind.Hospital, LayoutSignature.Islands), Table());
        }
    }
}
