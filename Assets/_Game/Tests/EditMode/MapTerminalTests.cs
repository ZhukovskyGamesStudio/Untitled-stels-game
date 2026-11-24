using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using Stealth.Player;
using Stealth.UI;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The HUD has no map: the map is a terminal's (TerminalKind.Map), and every kind of house has one.</summary>
    public class MapTerminalTests
    {
        [Test]
        public void EveryKindOfHouseHasMapTerminalsSpreadOut()
        {
            var missing = new List<string>();
            foreach (var (kind, house, l) in TestHouses.Kinds(1, 3))
                {
                    int seed = l.Seed;
                    var maps = l.Terminals.Where(t => t.kind == TerminalKind.Map).ToList();
                    if (maps.Count == 0) { missing.Add($"{kind} {seed}/{house}"); continue; }
                    foreach (var (prop, _) in maps)
                    {
                        Assert.AreEqual(PropKind.Terminal, l.Props[prop].Kind, $"{kind} {seed}/{house}");
                        Assert.AreNotEqual(l.LobbyRoom, l.Plan.RoomAt(l.Props[prop].Position + Vector3.up * 0.3f)?.Id ?? -1, $"{kind} {seed}/{house}: not in the lobby");
                    }
                    Assert.AreEqual(l.Terminals.Count, l.Terminals.Select(t => t.prop).Distinct().Count(), $"{kind} {seed}/{house}: a desk is one terminal");
                    if (maps.Count > 1)
                        Assert.Greater((l.Props[maps[0].prop].Position - l.Props[maps[1].prop].Position).magnitude, 6f, $"{kind} {seed}/{house}: the map terminals apart");
                }
            CollectionAssert.IsEmpty(missing, "houses with no map terminal");
        }

        [Test]
        public void TheMapTerminalsComeAfterTheOthers()
        {
            // added last: the hacking and camera terminals stand where they stood before there were map terminals
            var b = new SecuritySettings { MapTerminals = 0 };
            var l = TestHouses.Get(42, 2);
            var without = TestHouses.Make(42, 2, null, b);
            // (the storage door's kiosk is chosen after them, from the desks left: it may take another one)
            static bool Robots((int prop, TerminalKind kind) t) => t.kind is TerminalKind.Hacking or TerminalKind.Cameras;
            CollectionAssert.AreEqual(without.Terminals.Where(Robots).ToList(), l.Terminals.Where(Robots).ToList());
            Assert.AreEqual(new SecuritySettings().MapTerminals, l.Terminals.Count(t => t.kind == TerminalKind.Map));
        }

        [Test]
        public void AtAMapTerminalTheKeysFlipTheFloors()
        {
            var c = new PromptContext { AtTerminal = true, SittingKind = TerminalKind.Map };
            var hints = HintRules.Collect(c, new List<Hint>());
            Assert.IsTrue(hints.Any(h => h.Key.Equals(Controls.Get(GameAction.Interact))), "stand up");
            Assert.AreEqual(2, hints.Count, "stand up and the floor: nothing to hack or mark");
        }

        [Test]
        public void ThereIsNoMapKey()
        {
            // the M key is gone with the HUD's map
            foreach (var info in Controls.All) Assert.AreNotEqual("Map", info.Action.ToString());
        }
    }
}
