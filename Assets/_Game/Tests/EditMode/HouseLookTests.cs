using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Every kind of house has its look over the style: shell, furniture, light and colours.</summary>
    public class HouseLookTests
    {
        static StyleConfig Styles => Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles;

        static HouseKind[] Kinds => (HouseKind[])System.Enum.GetValues(typeof(HouseKind));

        [Test]
        public void EveryKindOfHouseHasItsLook([ValueSource(nameof(Kinds))] HouseKind kind)
        {
            var look = Styles.LookOf(kind);
            Assert.IsNotNull(look, kind.ToString());
            Assert.AreEqual(kind, look.Kind);
            foreach (var r in look.RoomLooks) Assert.IsNotNull(r.Wall, $"{kind}: {r.Room} without a wall");
            foreach (var f in look.RoomFloors) Assert.IsNotNull(f.Material, $"{kind}: {f.Room} without a floor");
            Assert.AreEqual(look.RoomLooks.Length, look.RoomLooks.Select(r => r.Room).Distinct().Count(), $"{kind}: a room dressed twice");
            Assert.AreEqual(look.RoomFloors.Length, look.RoomFloors.Select(r => r.Room).Distinct().Count(), $"{kind}: a floor laid twice");
            foreach (var m in look.Materials) { Assert.IsNotNull(m.From); Assert.IsNotNull(m.To); }
        }

        [Test]
        public void OnlyTheAbandonedStationIsGreen()
        {
            foreach (var style in Styles.Styles)
                foreach (var kind in Kinds)
                {
                    var g = Styles.LookOf(kind).Grade.Over(style.Grade, 1f);
                    float green = -g.Tint / 100f + (g.ColorFilter.g - (g.ColorFilter.r + g.ColorFilter.b) / 2f);
                    if (kind == HouseKind.Abandoned) Assert.Greater(green, 0.08f, $"{style.name}: the station's sick light");
                    else Assert.Less(Mathf.Abs(green), 0.02f, $"{style.name}, {kind}: no tint to green (or magenta)");
                }
            var fog = Styles.LookOf(HouseKind.Abandoned).Air.Over(Styles.Styles[0].World).IndoorFog;
            Assert.Greater(fog.g, fog.r * 1.3f);
        }

        [Test]
        public void TheStationIsFurnishedWithItsOwnModelsOnlyInTheScannedStyle()
        {
            foreach (var kind in Kinds)
            {
                var look = Styles.LookOf(kind);
                Assert.AreEqual(kind == HouseKind.Abandoned ? ModelFamily.Derelict : ModelFamily.None, look.FurnitureFor(ModelFamily.Realistic), kind.ToString());
                Assert.AreEqual(ModelFamily.None, look.FurnitureFor(ModelFamily.Clean), $"{kind}: the drawn style keeps its drawn furniture");
            }
            Assert.Greater(Styles.Props.Count(p => p.Family == ModelFamily.Derelict && p.Prefab != null && p.Key != null), 100, "the station's own furniture");
            Assert.IsTrue(Styles.Meshes.Any(m => m.Family == ModelFamily.Derelict));
        }

        [Test]
        public void ALookAddsToTheStyleAndLeavesItAlone()
        {
            var style = new StyleGeometry { Family = ModelFamily.Realistic, Crown = true, Doors = DoorTrim.Arch, RoomFloors = new[] { new RoomFloor { Room = RoomType.Gym, Tile = 1f }, new RoomFloor { Room = RoomType.Lobby, Tile = 2f } } };
            var look = ScriptableObject.CreateInstance<HouseLook>();
            try
            {
                Assert.AreSame(style, look.Dress(style), "nothing to say: the style itself");
                look.OwnTrim = true;
                look.RoomFloors = new[] { new RoomFloor { Room = RoomType.Lobby, Tile = 5f } };
                var dressed = look.Dress(style);
                Assert.IsTrue(style.Crown, "the style is not touched");
                Assert.IsFalse(dressed.Crown);
                Assert.AreEqual(DoorTrim.Casing, dressed.Doors);
                Assert.AreEqual(2, dressed.RoomFloors.Length, "the house's lobby, the style's gym");
                Assert.AreEqual(5f, dressed.RoomFloors.First(f => f.Room == RoomType.Lobby).Tile);
                look.KeepStyleRooms = false;
                Assert.AreEqual(1, look.Dress(style).RoomFloors.Length);
                look.ShellIn = new[] { ModelFamily.Clean };
                Assert.AreSame(style, look.Dress(style), "a shell for another style");
            }
            finally { Object.DestroyImmediate(look); }
        }

        [Test]
        public void ColoursFadeOutUndergroundAndTheAirIsTheHousesOwn()
        {
            var grade = new HouseGrade { Temperature = -10f, ColorFilter = new Color(0.8f, 1f, 1f), Split = 1f, SplitShadows = Color.green };
            var style = new StyleGrade();
            var none = grade.Over(style, 0f);
            Assert.AreEqual(style.Temperature, none.Temperature);
            Assert.AreEqual(style.ColorFilter, none.ColorFilter);
            Assert.AreEqual(style.SplitShadows, none.SplitShadows);
            var all = grade.Over(style, 1f);
            Assert.AreEqual(-10f, all.Temperature);
            Assert.AreEqual(0.8f, all.ColorFilter.r, 1e-4f);
            Assert.AreEqual(0f, style.Temperature, "the style is not touched");
            var air = new HouseAir { Ambient = 0.5f, FogDensity = 2f }.Over(new StyleWorld { Ambient = Color.white, IndoorFogDensity = 0.02f, LightScale = 1.5f });
            Assert.AreEqual(0.5f, air.Ambient.g, 1e-4f);
            Assert.AreEqual(0.04f, air.IndoorFogDensity, 1e-5f);
            Assert.AreEqual(1.5f, air.LightScale);
            // no house changes what its robots see: their light is the lamps', never the look's
            Assert.AreEqual(1f, new HouseAir().LightScale);
        }
    }
}
