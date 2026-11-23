using System.Collections.Generic;
using NUnit.Framework;

namespace Stealth.Tests
{
    /// <summary>The house's state keys: two different things never share one (a crate and a drawer, a vault panel and the gate).</summary>
    public class OpenKeysTests
    {
        [Test]
        public void KeysOfDifferentThingsNeverCollide()
        {
            var seen = new Dictionary<int, string>();
            void Add(int key, string what)
            {
                Assert.IsFalse(seen.TryGetValue(key, out var other), $"{what} and {other} share the key {key}");
                seen[key] = what;
            }
            for (int i = 0; i < 400; i++)
            {
                Add(OpenKeys.Door(i), "door " + i);
                Add(OpenKeys.Safe(i), "safe " + i);
                Add(OpenKeys.Escape(i), "escape " + i);
                Add(OpenKeys.Crate(i), "crate " + i);
                Add(OpenKeys.Dryer(i), "dryer " + i);
                Add(OpenKeys.Lights(i), "lights " + i);
                Add(OpenKeys.Crawl(i), "crawl " + i);
                Add(OpenKeys.Vent(i), "vent " + i);
                Add(OpenKeys.Console(i), "console " + i);
                Add(OpenKeys.CageOpen(i), "cage " + i);
                for (int kind = 0; kind < Stealth.Level.RobotRoster.KindCount; kind++) Add(OpenKeys.HackSlot(i, kind), $"slot {i}/{kind}");
                Add(OpenKeys.Med((ulong)i), "med " + i);
                for (int part = 0; part < OpenKeys.StorageParts; part++) Add(OpenKeys.Storage(i, part), $"storage {i}/{part}");
                for (int part = 0; part < OpenKeys.StorageParts; part++) Add(OpenKeys.Ajar(i, part), $"ajar {i}/{part}");
                for (int use = 0; use < OpenKeys.TerminalUses; use++) Add(OpenKeys.Terminal(i, use), $"terminal {i}/{use}");
            }
            Add(OpenKeys.Shaft, "shaft");
            Add(OpenKeys.Gate, "gate");
            foreach (JobStage stage in System.Enum.GetValues(typeof(JobStage))) Add(OpenKeys.Stage(stage), stage.ToString());
        }

        [Test]
        public void KeyTellsItsKindAndIndex()
        {
            int key = OpenKeys.Crate(1234);
            Assert.AreEqual(OpenKind.Crate, OpenKeys.KindOf(key));
            Assert.AreEqual(1234, OpenKeys.IndexOf(key));
            Assert.AreEqual(OpenKind.Door, OpenKeys.KindOf(OpenKeys.Door(0)));
            Assert.AreNotEqual(0, OpenKeys.Door(0), "no key is 0 (a default int means nothing)");
        }

        [Test]
        public void WithoutGameNothingIsOpen()
        {
            Assert.IsFalse(World.IsOpened(OpenKeys.Door(3)));
            Assert.AreEqual(0, World.State.VaultSolved);
        }
    }
}
