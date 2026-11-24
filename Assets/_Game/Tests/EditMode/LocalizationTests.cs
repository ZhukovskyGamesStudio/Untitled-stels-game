using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Every test of the game reads the Russian texts (the language of the asserts).</summary>
    [SetUpFixture]
    public class LanguageSetup
    {
        [OneTimeSetUp]
        public void Russian() => Loc.Use(Language.Ru, save: false);
    }

    /// <summary>The string tables: complete, without duplicates, and no text for the player left in the code.</summary>
    public class LocalizationTests
    {
        static string GameDir => Path.Combine(Application.dataPath, "_Game");

        static IEnumerable<string> SourceFiles(params string[] dirs) =>
            dirs.SelectMany(d => Directory.GetFiles(Path.Combine(GameDir, d), "*.cs", SearchOption.AllDirectories));

        [Test]
        public void TablesHaveEveryLanguageAndNoDuplicates()
        {
            var texts = Resources.LoadAll<TextAsset>(Loc.Folder).Select(a => a.text).ToList();
            Assert.IsNotEmpty(texts, "Resources/Strings/*.csv");
            Loc.Load(texts, out var duplicates);
            CollectionAssert.IsEmpty(duplicates, "a key lives in one table only");
            var missing = Loc.Table.Where(kv => kv.Value.Any(string.IsNullOrWhiteSpace)).Select(kv => kv.Key).ToList();
            CollectionAssert.IsEmpty(missing, "every key has a Russian and an English text");
            foreach (var kv in Loc.Table)
            {
                int args(string s) => Regex.Matches(s, @"\{(\d+)").Cast<Match>().Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(-1).Max();
                Assert.AreEqual(args(kv.Value[0]), args(kv.Value[1]), $"{kv.Key}: the same arguments in both languages");
            }
        }

        [Test]
        public void EveryKeyInTheCodeIsInTheTables()
        {
            Loc.Reload();
            var call = new Regex(@"(?:Loc\.(?:T|F|Pack|Has)|\bL)\(\s*""([a-z][\w\.\-]*)""");
            var plural = new Regex(@"Loc\.Plural\(\s*""([a-z][\w\.\-]*)""");
            var missing = new List<string>();
            foreach (var file in SourceFiles("Scripts", "Editor"))
            {
                string text = File.ReadAllText(file);
                foreach (Match m in call.Matches(text))
                    if (!Loc.Has(m.Groups[1].Value)) missing.Add($"{Path.GetFileName(file)}: {m.Groups[1].Value}");
                foreach (Match m in plural.Matches(text))
                    foreach (var form in new[] { "one", "few", "many" })
                        if (!Loc.Has(m.Groups[1].Value + "." + form)) missing.Add($"{Path.GetFileName(file)}: {m.Groups[1].Value}.{form}");
            }
            CollectionAssert.IsEmpty(missing.Distinct().ToList());
        }

        /// <summary>
        /// No Russian string literal in the code: texts for the player come from the tables. Allowed: inspector tooltips and
        /// headers, comments, the autopilot's logs (DevAutomation*), the sample states of the editor's UI pictures (UiSheet), and a line marked <c>// loc-ok</c> (a dev-only text).
        /// </summary>
        [Test]
        public void NoPlayerTextIsLeftInTheCode()
        {
            var literal = new Regex(@"""(?:[^""\\\n]|\\.)*[А-Яа-яЁё](?:[^""\\\n]|\\.)*""");
            var found = new List<string>();
            foreach (var file in SourceFiles("Scripts", "Editor"))
            {
                string name = Path.GetFileName(file);
                if (name.StartsWith("DevAutomation") || name == "UiSheet.cs") continue; // (the UI sheet fills the screens with sample Russian text: a picture, not the game)
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string code = line.Trim();
                    if (code.StartsWith("//") || code.StartsWith("///") || code.StartsWith("*")) continue;
                    if (line.Contains("[Tooltip(") || line.Contains("[Header(") || line.Contains("loc-ok")) continue;
                    int comment = line.IndexOf("//", System.StringComparison.Ordinal);
                    foreach (Match m in literal.Matches(line))
                        if (comment < 0 || m.Index < comment) found.Add($"{name}:{i + 1}: {m.Value}");
                }
            }
            CollectionAssert.IsEmpty(found);
        }

        [Test]
        public void PackedTextsAreFormattedInTheReadersLanguage()
        {
            Loc.Load(new[] { "key,ru,en\nt.hello,\"Привет, {0}\",\"Hello, {0}\"\nt.bot,Охранник,Guard\nt.n,\"строка\\nвторая\",\"line\\nsecond\"" }, out _);
            try
            {
                string packed = Loc.Pack("t.hello", "@t.bot");
                Assert.AreEqual("Привет, Охранник", Loc.Unpack(packed));
                Loc.Use(Language.En, save: false);
                Assert.AreEqual("Hello, Guard", Loc.Unpack(packed));
                Assert.AreEqual("line\nsecond", Loc.T("t.n"));
                Assert.AreEqual("no.such.key", Loc.T("no.such.key"), "a missing key shows itself");
                Assert.AreEqual("plain", Loc.Unpack("plain"));
            }
            finally
            {
                Loc.Use(Language.Ru, save: false);
                Loc.Reload();
            }
        }

        [Test]
        public void RussianPluralsFollowTheNumber()
        {
            Loc.Load(new[] { "key,ru,en\nt.p.one,игрок,player\nt.p.few,игрока,players\nt.p.many,игроков,players" }, out _);
            try
            {
                Assert.AreEqual("игрок", Loc.Plural("t.p", 1));
                Assert.AreEqual("игрока", Loc.Plural("t.p", 3));
                Assert.AreEqual("игроков", Loc.Plural("t.p", 11));
                Assert.AreEqual("игрок", Loc.Plural("t.p", 21));
            }
            finally { Loc.Reload(); }
        }
    }
}
