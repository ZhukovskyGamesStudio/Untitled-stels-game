using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Stealth
{
    /// <summary>The languages of the game's texts (columns of the string tables after the key).</summary>
    public enum Language { Ru, En }

    /// <summary>
    /// Every text the player sees, by key. The tables are CSV files in <c>Resources/Strings/</c> (columns <c>key,ru,en</c>,
    /// UTF-8, opened in any spreadsheet); all of them are loaded and merged, a key may live in one file only
    /// (<c>LocalizationTests</c>). A missing translation falls back to Russian, a missing key shows the key itself.
    /// The language is a per-player setting (PlayerPrefs, the pause menu); by default — the system's language.
    /// Texts that travel over the network (banners) travel as <see cref="Pack"/>ed keys and are formatted by every peer
    /// in its own language (<see cref="Unpack"/>).
    /// </summary>
    public static class Loc
    {
        public const string Folder = "Strings";
        const string PrefKey = "stealth.lang";
        /// <summary>Starts a packed text: key and arguments separated by <see cref="ArgSep"/>.</summary>
        public const char PackMark = '\u0002';
        const char ArgSep = '\u001f';

        static Dictionary<string, string[]> s_Table;
        static Language? s_Language;

        /// <summary>Bumped on every language change: presenters that cache texts compare it (like <c>Controls.Version</c>).</summary>
        public static int Version { get; private set; }
        public static event Action Changed;

        public static Language Language
        {
            get
            {
                if (s_Language == null) s_Language = Saved();
                return s_Language.Value;
            }
        }

        public static readonly Language[] All = { Language.Ru, Language.En };

        /// <summary>The language's own name, for the setting.</summary>
        public static string NameOf(Language l) => l == Language.Ru ? "Русский" : "English"; // loc-ok: a language is named in itself

        /// <summary>Switch the language (and remember it unless <paramref name="save"/> is false: tests).</summary>
        public static void Use(Language l, bool save = true)
        {
            bool changed = s_Language != l;
            s_Language = l;
            if (save) { PlayerPrefs.SetInt(PrefKey, (int)l); PlayerPrefs.Save(); }
            if (!changed) return;
            Version++;
            Changed?.Invoke();
        }

        static Language Saved()
        {
            int v = PlayerPrefs.GetInt(PrefKey, -1);
            if (v >= 0 && v < All.Length) return (Language)v;
            var sys = Application.systemLanguage;
            return sys is SystemLanguage.Russian or SystemLanguage.Ukrainian or SystemLanguage.Belarusian ? Language.Ru : Language.En;
        }

        // ------------------------------------------------------------------ lookup

        /// <summary>The text of <paramref name="key"/> in the current language.</summary>
        public static string T(string key) => T(key, Language);

        public static string T(string key, Language l)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (!Table.TryGetValue(key, out var row)) return key;
            int i = (int)l;
            return Embed(i < row.Length && !string.IsNullOrEmpty(row[i]) ? row[i] : row[0], l, 0);
        }

        /// <summary>
        /// A text may carry another key's text as <c>{=key}</c> (a name used in many texts lives in one place: Linus's,
        /// <c>name.raccoon</c> and its cases); embedded keys may embed in turn, a few deep.
        /// </summary>
        static string Embed(string text, Language l, int depth)
        {
            int at = text.IndexOf("{=", StringComparison.Ordinal);
            if (at < 0 || depth > 3) return text;
            var sb = new StringBuilder(text.Length + 16);
            int from = 0;
            while (at >= 0)
            {
                int end = text.IndexOf('}', at + 2);
                if (end < 0) break;
                sb.Append(text, from, at - from);
                string inner = text.Substring(at + 2, end - at - 2);
                sb.Append(Table.TryGetValue(inner, out var row) ? Embed(row[Math.Min((int)l, row.Length - 1)] is { Length: > 0 } t ? t : row[0], l, depth + 1) : inner);
                from = end + 1;
                at = text.IndexOf("{=", from, StringComparison.Ordinal);
            }
            sb.Append(text, from, text.Length - from);
            return sb.ToString();
        }

        /// <summary><see cref="T(string)"/> formatted with <paramref name="args"/> (<c>{0}</c>, <c>{1:0}</c>…).</summary>
        public static string F(string key, params object[] args)
        {
            string format = T(key);
            try { return string.Format(CultureInfo.InvariantCulture, format, args); }
            catch (FormatException) { return format; }
        }

        public static bool Has(string key) => key != null && Table.ContainsKey(key);

        /// <summary>A plural form by the language's rules: Russian one / few / many (1 игрок, 2 игрока, 5 игроков), English one / other.</summary>
        public static string Plural(string key, int n)
        {
            if (Language == Language.En) return T(n == 1 ? key + ".one" : key + ".many");
            int a = Math.Abs(n) % 100, b = a % 10;
            string form = a is >= 11 and <= 14 ? "many" : b == 1 ? "one" : b is >= 2 and <= 4 ? "few" : "many";
            return T(key + "." + form);
        }

        // ------------------------------------------------------------------ network-safe texts

        /// <summary>A key and its arguments as one string, formatted later on each peer by <see cref="Unpack"/>.</summary>
        public static string Pack(string key, params object[] args)
        {
            var sb = new StringBuilder().Append(PackMark).Append(key);
            foreach (var a in args)
                sb.Append(ArgSep).Append(a is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : a?.ToString());
            return sb.ToString();
        }

        /// <summary>The text of a <see cref="Pack"/>ed string in this peer's language; any other string as it is.</summary>
        public static string Unpack(string text)
        {
            if (string.IsNullOrEmpty(text) || text[0] != PackMark) return text;
            var parts = text.Substring(1).Split(ArgSep);
            if (parts.Length == 1) return T(parts[0]);
            var args = new object[parts.Length - 1];
            for (int i = 1; i < parts.Length; i++)
            {
                // an argument may itself be a key (a robot's name, an item): "@key"
                string a = parts[i];
                args[i - 1] = a.Length > 1 && a[0] == '@' ? T(a.Substring(1)) : a;
            }
            return F(parts[0], args);
        }

        // ------------------------------------------------------------------ the tables

        public static IReadOnlyDictionary<string, string[]> Table
        {
            get
            {
                if (s_Table == null) Reload();
                return s_Table;
            }
        }

        /// <summary>Read every table in <c>Resources/Strings</c> again.</summary>
        public static void Reload()
        {
            var texts = new List<string>();
            foreach (var asset in Resources.LoadAll<TextAsset>(Folder)) texts.Add(asset.text);
            Load(texts, out _);
        }

        /// <summary>Load tables from CSV texts; <paramref name="duplicates"/> — keys found more than once.</summary>
        public static void Load(IEnumerable<string> csvTexts, out List<string> duplicates)
        {
            s_Table = new Dictionary<string, string[]>();
            duplicates = new List<string>();
            foreach (var text in csvTexts)
                foreach (var row in ParseCsv(text))
                {
                    if (row.Count == 0) continue;
                    string key = row[0].Trim();
                    if (key.Length == 0 || key[0] == '#' || key == "key") continue;
                    var values = new string[All.Length];
                    for (int i = 0; i < values.Length; i++)
                        values[i] = i + 1 < row.Count ? Unescape(row[i + 1]) : "";
                    if (s_Table.ContainsKey(key)) duplicates.Add(key);
                    s_Table[key] = values;
                }
            Version++;
            Changed?.Invoke();
        }

        /// <summary>A cell may hold "\n" for a line break.</summary>
        static string Unescape(string s) => s.Replace("\\n", "\n");

        /// <summary>RFC 4180 CSV: commas, quoted cells with "" for a quote, line breaks inside quotes.</summary>
        public static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(text)) return rows;
            int i = text[0] == '﻿' ? 1 : 0;
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false, any = false;
            for (; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                        else quoted = false;
                    }
                    else cell.Append(c);
                    continue;
                }
                switch (c)
                {
                    case '"': quoted = true; any = true; break;
                    case ',': row.Add(cell.ToString()); cell.Clear(); any = true; break;
                    case '\r': break;
                    case '\n':
                        if (any || cell.Length > 0) { row.Add(cell.ToString()); rows.Add(row); }
                        row = new List<string>(); cell.Clear(); any = false;
                        break;
                    default: cell.Append(c); any = true; break;
                }
            }
            if (any || cell.Length > 0) { row.Add(cell.ToString()); rows.Add(row); }
            return rows;
        }
    }
}
