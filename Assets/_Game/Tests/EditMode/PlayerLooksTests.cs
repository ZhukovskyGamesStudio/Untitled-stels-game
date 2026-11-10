using System.Linq;
using NUnit.Framework;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Tests
{
    public class PlayerLooksTests
    {
        [Test]
        public void FourSlotsAreFourDepartments()
        {
            var depts = Enumerable.Range(0, PlayerLooks.Slots).Select(PlayerLooks.DepartmentOf).ToArray();
            Assert.AreEqual(4, depts.Distinct().Count());
            Assert.AreEqual(Department.Backend, PlayerLooks.DepartmentOf(PlayerLooks.SlotOf(4)), "the fifth player wraps around");
            Assert.AreEqual(4, PlayerLooks.DepartmentNames.Distinct().Count());
        }

        [Test]
        public void TheTeamColourIsOnlyOnTheSmallThings()
        {
            for (int slot = 0; slot < PlayerLooks.Slots; slot++)
            {
                var team = PlayerLooks.TeamColor(slot);
                Assert.IsTrue(PlayerLooks.PartColor("Accent", slot, out var accent));
                Assert.AreEqual(team, accent, "backpack, armband, lanyard or scarf");
                foreach (var part in new[] { "Body", "AccentHead", "EyeGlasses" })
                {
                    Assert.IsTrue(PlayerLooks.PartColor(part, slot, out var c), part);
                    Assert.AreNotEqual(team, c, $"{part} must not wear the team colour");
                }
                Assert.IsFalse(PlayerLooks.PartColor("Something", slot, out _));
            }
            Assert.AreNotEqual(PlayerLooks.TeamColor(0), PlayerLooks.TeamColor(1));
        }
    }
}
